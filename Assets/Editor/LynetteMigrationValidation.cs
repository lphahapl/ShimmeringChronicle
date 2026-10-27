using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// 琳妮特使用真实玩家预制体和通用状态机进行运行检查。
[InitializeOnLoad]
public static class LynetteMigrationValidation
{
    static bool preparing;
    static LynetteMigrationValidation()
    {
        EditorApplication.update += CheckRequest;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    public static void RunBatch()
    {
        Directory.CreateDirectory(LynetteMigrationSetup.Report);
        File.WriteAllText(LynetteMigrationSetup.Report + "/verify.request", "batch");
    }

    static void CheckRequest()
    {
        if (SessionState.GetBool("LynetteCheckRunning", false) && EditorApplication.isPlaying)
        {
            Application.runInBackground = true;
            EditorApplication.isPaused = false;
            EditorApplication.QueuePlayerLoopUpdate();
        }
        if (preparing || EditorApplication.isCompiling || EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (File.Exists(LynetteMigrationSetup.Report + "/scenecheck.request"))
        {
            File.Delete(LynetteMigrationSetup.Report + "/scenecheck.request");
            var currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (currentScene.isDirty) EditorSceneManager.SaveScene(currentScene);
            SessionState.SetString("LynetteReturnScene", currentScene.path);
            EditorSceneManager.OpenScene(LynetteMigrationSetup.ScenePath);
            SessionState.SetBool("LynetteCheckRunning", true);
            SessionState.SetBool("LynetteSceneCheck", true);
            EditorApplication.EnterPlaymode();
            return;
        }
        if (!File.Exists(LynetteMigrationSetup.Report + "/verify.request")) return;
        File.Delete(LynetteMigrationSetup.Report + "/verify.request");
        preparing = true;
        try
        {
            LynetteMigrationSetup.Validate();
            var current = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (current.isDirty) EditorSceneManager.SaveScene(current);
            SessionState.SetString("LynetteReturnScene", current.path);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Floor";
            floor.transform.position = new Vector3(0f, -0.15f, 0f);
            floor.transform.localScale = new Vector3(30f, 0.3f, 30f);
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "Wall";
            wall.transform.position = new Vector3(0f, 1f, 3f);
            wall.transform.localScale = new Vector3(20f, 2f, 0.2f);
            GameObject camera = new GameObject("Main Camera");
            camera.tag = "MainCamera";
            camera.AddComponent<Camera>();
            camera.transform.position = new Vector3(2f, 1.8f, 2.5f);
            camera.transform.LookAt(Vector3.up * 0.9f);
            GameObject light = new GameObject("Directional Light");
            light.AddComponent<Light>().type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            GameObject player = PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(LynetteMigrationSetup.PlayerPath)) as GameObject;
            player.transform.position = Vector3.zero;
            player.GetComponent<PlayerController>().readInput = false;
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), LynetteMigrationSetup.Folder + "/LynetteValidation.unity");
            SessionState.SetBool("LynetteCheckRunning", true);
            Type gameView = Type.GetType("UnityEditor.GameView,UnityEditor");
            if (gameView != null) EditorWindow.GetWindow(gameView).Focus();
            EditorApplication.EnterPlaymode();
        }
        catch (Exception error)
        {
            File.WriteAllText(LynetteMigrationSetup.Report + "/play-validation.txt", error.ToString());
            Debug.LogException(error);
        }
        preparing = false;
    }

    static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (!SessionState.GetBool("LynetteCheckRunning", false)) return;
        if (change == PlayModeStateChange.EnteredPlayMode)
        {
            if (SessionState.GetBool("LynetteSceneCheck", false))
                new GameObject("LynetteSceneCheck").AddComponent<LynetteSceneCheck>();
            else new GameObject("LynetteMotionCheck").AddComponent<LynetteMotionCheck>();
        }
        if (change == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool("LynetteCheckRunning", false);
            SessionState.SetBool("LynetteSceneCheck", false);
            string path = SessionState.GetString("LynetteReturnScene", "Assets/Scenes/SampleScene.unity");
            if (!string.IsNullOrEmpty(path)) EditorSceneManager.OpenScene(path);
            if (Application.isBatchMode)
            {
                string reportPath = LynetteMigrationSetup.Report + "/play-validation.txt";
                int code = 1;
                if (File.Exists(reportPath) && !File.ReadAllText(reportPath).Contains("FAIL:")) code = 0;
                EditorApplication.Exit(code);
            }
        }
    }
}

public class LynetteMotionCheck : MonoBehaviour
{
    PlayerObj player;
    PlayerController controller;
    Animator animator;
    PlayerInput input = new PlayerInput(0.15f, 0.5f);
    List<string> checks = new List<string>();
    List<string> errors = new List<string>();
    int[] hits = new int[3];
    bool[] queued = new bool[3];
    bool[] seen = new bool[3];
    int stage;
    float stageTime;
    bool firstQueued;
    bool sawDodge;
    bool sawHit;
    float hp;
    Vector3 origin;
    int staggers;
    int progressFrame;
    LynetteDamageDummy dummy;
    Vector3 lockedPosition;

    void Start()
    {
        Application.runInBackground = true;
        player = Object.FindFirstObjectByType<PlayerObj>();
        controller = player.GetComponent<PlayerController>();
        animator = player.GetComponent<Animator>();
        this.Subscribe(GameEvents.OnPlayerCombo, OnHit);
        player.Staggered += OnStaggered;
        Application.logMessageReceived += OnLog;
        stageTime = Time.time;
    }
    void OnHit(PlayerObj attacker, int index)
    {
        if (attacker == player && index >= 0 && index < hits.Length) hits[index]++;
    }
    void OnStaggered(GameObject attacker) { staggers++; }
    void OnLog(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception) errors.Add(message + "\n" + stack);
    }
    void Check(bool passed, string name)
    {
        string prefix = "FAIL: ";
        if (passed) prefix = "PASS: ";
        checks.Add(prefix + name);
    }
    void Next()
    {
        stage++;
        stageTime = Time.time;
        input.Clear();
    }
    void Submit(Vector2 move, bool flash = false)
    {
        input.RecordInput(move, flash, Time.time);
        controller.ApplyInput(input, Vector3.forward, player.dtFix, Time.time);
    }

    void Update()
    {
        float elapsed = Time.time - stageTime;
        if (Time.frameCount - progressFrame >= 30)
        {
            progressFrame = Time.frameCount;
            File.WriteAllText(LynetteMigrationSetup.Report + "/motion-progress.txt", "stage=" + stage + " elapsed=" + elapsed + " frame=" + Time.frameCount + " hits=" + string.Join(",", hits));
        }
        if (stage == 0)
        {
            Submit(Vector2.zero);
            if (elapsed < 0.8f) return;
            Check(animator.avatar.isHuman && animator.avatar.isValid, "使用琳妮特自身有效 Humanoid Avatar");
            Check(player.Data.Bag[0] == player.EquippedItem && player.Data.GetWeaponData().combo.Count == 3, "开局装备背包中的三段太刀");
            Check(player.HandingItem != null && player.HandingItem.transform.parent == player.handPos, "太刀挂在源场景对应右手挂点");
            Transform grip = player.HandingItem.transform.Find("GripPoint_R");
            Check(grip != null && Vector3.Distance(grip.position, player.handPos.position) < 0.12f, "握持标记贴近手部");
            Check(animator.runtimeAnimatorController is AnimatorOverrideController, "运行时使用通用状态机的武器覆盖动画");
            ScreenCapture.CaptureScreenshot(LynetteMigrationSetup.Report + "/lynette-idle.png");
            origin = player.transform.position;
            Next();
        }
        else if (stage == 1)
        {
            Submit(Vector2.up);
            if (elapsed < 3f) return;
            Check(player.transform.position.z - origin.z > 0.3f, "移动动画实际推动角色");
            Check(player.transform.position.z < 2.75f, "移动受墙体碰撞约束");
            Next();
        }
        else if (stage == 2)
        {
            Submit(Vector2.right);
            if (elapsed < 1.2f) return;
            Check(Vector3.Dot(player.transform.forward, Vector3.right) > 0.7f, "转身动画正常重定向");
            Next();
        }
        else if (stage == 3)
        {
            Submit(Vector2.zero, elapsed < 0.04f);
            if (animator.GetCurrentAnimatorStateInfo(0).IsTag("Dodge")) sawDodge = true;
            if (elapsed < 1.8f) return;
            Check(sawDodge, "闪避进入通用 Dodge 状态");
            controller.ClearInput();
            animator.Play("Base Layer.Locomotion.Idle_Wait_A", 0, 0f);
            Next();
        }
        else if (stage == 4)
        {
            if (dummy == null)
            {
                GameObject target = new GameObject("DamageDummy");
                target.layer = LayerMask.NameToLayer("enemy");
                target.AddComponent<SphereCollider>().radius = 0.5f;
                dummy = target.AddComponent<LynetteDamageDummy>();
                dummy.data = new EnemyData();
                dummy.data.hp = 200f;
                dummy.data.maxHp = 200f;
                dummy.poise = 200f;
            }
            dummy.transform.position = player.transform.position + player.transform.forward * 1.2f + Vector3.up * 0.8f;
            Physics.SyncTransforms();
            if (elapsed > 0.2f && !firstQueued)
            {
                input.RecordComboInput(Time.time);
                firstQueued = true;
            }
            AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
            for (int i = 0; i < 3; i++)
            {
                if (state.shortNameHash != Animator.StringToHash("Combo" + (i + 1))) continue;
                seen[i] = true;
                if (i < 2 && state.normalizedTime >= 0.45f && !queued[i])
                {
                    input.RecordComboInput(Time.time);
                    queued[i] = true;
                }
            }
            Submit(Vector2.zero);
            if (elapsed < 7.5f) return;
            for (int i = 0; i < 3; i++)
                Check(seen[i] && hits[i] == 1, "第 " + (i + 1) + " 段源攻击完成一次命中，实际 " + hits[i]);
            float expectedDamage = 0f;
            foreach (AttackData attack in player.Data.GetWeaponData().combo) expectedDamage += attack.damagePerHit;
            Check(Mathf.Approximately(200f - dummy.data.hp, expectedDamage), "三段动画命中沿用真实 WeaponObj/EnemyObj 扣血，总伤害 " + (200f - dummy.data.hp));
            hp = player.Data.hp;
            player.poise = 100f;
            player.TakeDamage(1f, null, 1f);
            Check(staggers == 0, "未破韧不触发硬直");
            player.TakeDamage(1f, null, 200f);
            Next();
        }
        else if (stage == 5)
        {
            Submit(Vector2.zero);
            if (animator.GetCurrentAnimatorStateInfo(0).IsName("Hit")) sawHit = true;
            if (elapsed < 1.8f) return;
            Check(staggers == 1 && sawHit, "破韧播放受击且只通知一次");
            Check(Mathf.Approximately(player.Data.hp, hp - 2f), "受击不重复扣血");
            this.Publish(GameEvents.OnPlayerTalk, player, true);
            lockedPosition = player.transform.position;
            Next();
        }
        else if (stage == 6)
        {
            input.RecordComboInput(Time.time);
            Submit(Vector2.up, true);
            if (elapsed < 0.5f) return;
            Vector3 delta = player.transform.position - lockedPosition;
            delta.y = 0f;
            Check(player.IsTalking && delta.magnitude < 0.05f && input.BufferedComboCount == 0, "对话锁定移动和攻击缓存");
            this.Publish(GameEvents.OnPlayerTalk, player, false);
            player.Die();
            Next();
        }
        else if (stage == 7)
        {
            Submit(Vector2.up, true);
            if (elapsed < 0.8f) return;
            Check(player.IsDead && controller.IsDead && animator.GetCurrentAnimatorStateInfo(0).IsName("Dead"), "死亡状态与通用死亡动画一致");
            Check(errors.Count == 0, "运行期间没有 Error 或 Exception，实际 " + errors.Count);
            checks.AddRange(errors);
            File.WriteAllLines(LynetteMigrationSetup.Report + "/play-validation.txt", checks);
            EditorApplication.ExitPlaymode();
            enabled = false;
        }
    }
    void OnDestroy()
    {
        Application.logMessageReceived -= OnLog;
        this.UnSubscribe(GameEvents.OnPlayerCombo, OnHit);
        if (player != null) player.Staggered -= OnStaggered;
    }
}


public class LynetteSceneCheck : MonoBehaviour
{
    PlayerObj player;
    List<string> checks = new List<string>();
    List<string> errors = new List<string>();
    float started;
    bool opened;
    void Start()
    {
        Application.runInBackground = true;
        started = Time.realtimeSinceStartup;
        player = Object.FindFirstObjectByType<PlayerObj>();
        player.GetComponent<PlayerController>().readInput = false;
        Application.logMessageReceived += OnLog;
        foreach (EnemyAI ai in Object.FindObjectsByType<EnemyAI>(FindObjectsSortMode.None))
        {
            if (ai.self != null && ai.self.enabled && ai.self.isOnNavMesh) ai.self.isStopped = true;
            ai.enabled = false;
        }
    }
    void OnLog(string message, string stack, LogType type)
    {
        if (type == LogType.Error || type == LogType.Exception) errors.Add(message + "\n" + stack);
    }
    void Check(bool passed, string name)
    {
        string prefix = "FAIL: ";
        if (passed) prefix = "PASS: ";
        checks.Add(prefix + name);
    }
    void Update()
    {
        if (!opened)
        {
            if (Time.realtimeSinceStartup - started < 2f) return;
            GameManager manager = GameManager.Instance;
            if (manager != null && !manager.LoadingStatus.ItemIsRead && Time.realtimeSinceStartup - started < 15f) return;
            Check(player.name == "LynettePlayer", "独立试玩场景使用琳妮特");
            Check(manager != null && manager.GetItem("weapon_lynette_katana") != null, "太刀已登记到物品表");
            Check(player.HandingItem != null && player.Data.GetWeaponData().combo.Count == 3, "场景开局装备三段太刀");
            ItemData startingItem = player.EquippedItem;
            QuickBar quickBar = UIManager.Instance.Get<QuickBar>();
            Check(quickBar != null && quickBar.player == player, "快捷栏引用当前琳妮特玩家");
            bool ownersMatch = quickBar != null && quickBar.quickSlots.Count > 0;
            if (quickBar != null)
                foreach (ItemSlot quickSlot in quickBar.quickSlots)
                    if (quickSlot.Owner != player.QuickBar) ownersMatch = false;
            Check(ownersMatch, "快捷栏全部格子绑定当前玩家容器");
            PlayerController controller = player.GetComponent<PlayerController>();
            var inputField = typeof(PlayerController).GetField("playerInput", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            PlayerInput wheelInput = inputField.GetValue(controller) as PlayerInput;
            typeof(PlayerInput).GetProperty("ScrollValue").SetValue(wheelInput, -1f);
            typeof(PlayerController).GetMethod("ReadQuickBarWheel", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).Invoke(controller, null);
            Check(player.SelectedSlot != null && player.SelectedSlot.Owner == player.QuickBar && player.EquippedItem == null && player.HandingItem == null, "开局滚轮切到空快捷格，清除手持太刀");
            typeof(PlayerInput).GetProperty("ScrollValue").SetValue(wheelInput, 0f);
            player.ChangeItem(startingItem);
            UIManager.Instance.Open<BagPanel>();
            BagPanel bag = UIManager.Instance.Get<BagPanel>();
            bool found = false;
            foreach (ItemSlot slot in bag.GetComponentsInChildren<ItemSlot>(true))
            {
                if (slot.Item == null || slot.Item.id != "weapon_lynette_katana") continue;
                found = true;
                SerializedObject serialized = new SerializedObject(slot);
                var image = serialized.FindProperty("iconImage").objectReferenceValue as UnityEngine.UI.Image;
                Check(image != null && image.sprite == slot.Item.icon && image.enabled, "背包显示太刀图标");
                Check(player.SelectSlot(slot), "背包太刀格子可选中装备");
            }
            Check(found, "背包绑定琳妮特数据");
            bool materials = true;
            foreach (Renderer renderer in player.GetComponentsInChildren<Renderer>())
            foreach (Material material in renderer.sharedMaterials)
                if (material == null || material.shader == null || !material.shader.isSupported) materials = false;
            Check(materials, "角色与武器材质均兼容当前渲染管线");
            ScreenCapture.CaptureScreenshot(LynetteMigrationSetup.Report + "/lynette-scene.png");
            opened = true;
            started = Time.realtimeSinceStartup;
            return;
        }
        if (Time.realtimeSinceStartup - started < 2f) return;
        UIManager.Instance.Close<BagPanel>();
        EnemyObj target = null;
        foreach (EnemyObj enemy in Object.FindObjectsByType<EnemyObj>(FindObjectsSortMode.None))
            if (enemy.data != null && enemy.data.hp > 50f) { target = enemy; break; }
        if (target != null)
        {
            var agent = target.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null) agent.enabled = false;
            target.transform.position = player.transform.position + player.transform.forward * 1.2f;
            Physics.SyncTransforms();
            WeaponData weapon = player.Data.GetWeaponData();
            float hp = target.data.hp;
            player.PublishAttackHit(weapon, 0);
            Check(Mathf.Approximately(target.data.hp, hp - weapon.combo[0].damagePerHit), "太刀判定可伤害真实敌人且只扣一次血");
        }
        else Check(false, "场景存在可验证伤害的敌人");
        Check(errors.Count == 0, "主场景检查期间没有 Error 或 Exception，实际 " + errors.Count);
        checks.AddRange(errors);
        File.WriteAllLines(LynetteMigrationSetup.Report + "/scene-validation.txt", checks);
        EditorApplication.ExitPlaymode();
        enabled = false;
    }
    void OnDestroy() { Application.logMessageReceived -= OnLog; }
}


// 只跳过敌人的导航初始化，受伤仍执行现有 EnemyObj.TakeDamage。
public class LynetteDamageDummy : EnemyObj
{
    void Awake() { }
    void OnEnable() { }
    void Update() { }
    void OnDisable() { }
}
