using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// 用真实 Animator 和 CharacterController 检查迁移后的动作，不依赖手动按键。
[InitializeOnLoad]
public static class KeqingMigrationValidation
{
    const string Folder = "Tools/Players/KeqingMigration";
    static bool preparing;
    static double lastProgress;

    static KeqingMigrationValidation()
    {
        EditorApplication.update += CheckRequest;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }

    static void CheckRequest()
    {
        bool mainCheck = SessionState.GetBool("KeqingMainCheckRunning", false);
        bool motionCheck = SessionState.GetBool("KeqingCheckRunning", false);
        if (EditorApplication.isPlaying && (mainCheck || motionCheck))
        {
            Application.runInBackground = true;
            EditorApplication.isPaused = false;
            EditorApplication.QueuePlayerLoopUpdate();
            if (mainCheck && Object.FindFirstObjectByType<PlayerObj>() != null && Object.FindFirstObjectByType<KeqingMainSceneCheck>() == null)
                new GameObject("KeqingMainSceneCheck").AddComponent<KeqingMainSceneCheck>();
            if (EditorApplication.timeSinceStartup - lastProgress > 1.0)
            {
                lastProgress = EditorApplication.timeSinceStartup;
                File.WriteAllText(Folder + "/check-progress.txt", "playing=" + EditorApplication.isPlaying + " time=" + Time.time + " frame=" + Time.frameCount + " main=" + mainCheck);
            }
        }
        if (preparing || EditorApplication.isCompiling || EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (File.Exists(Folder + "/scenecheck.request"))
        {
            File.Delete(Folder + "/scenecheck.request");
            var currentScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (currentScene.isDirty) EditorSceneManager.SaveScene(currentScene);
            if (currentScene.path != "Assets/Scenes/SampleScene.unity")
                EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
            SessionState.SetBool("KeqingMainCheckRunning", true);
            Type gameView = Type.GetType("UnityEditor.GameView,UnityEditor");
            if (gameView != null) EditorWindow.GetWindow(gameView).Focus();
            EditorApplication.EnterPlaymode();
            return;
        }
        if (!File.Exists(Folder + "/playcheck.request")) return;
        File.Delete(Folder + "/playcheck.request");
        preparing = true;
        try
        {
            KeqingMigrationSetup.ValidateAssets();
            var current = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (current.isDirty) EditorSceneManager.SaveScene(current);
            SessionState.SetString("KeqingCheckReturnScene", current.path);
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "ValidationFloor";
            floor.transform.position = new Vector3(0f, -0.15f, 0f);
            floor.transform.localScale = new Vector3(30f, 0.3f, 30f);
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = "ValidationWall";
            wall.transform.position = new Vector3(0f, 1f, 3f);
            wall.transform.localScale = new Vector3(20f, 2f, 0.2f);
            GameObject camera = new GameObject("Main Camera");
            camera.tag = "MainCamera";
            camera.AddComponent<Camera>();
            camera.transform.position = new Vector3(2f, 2f, -4f);
            camera.transform.LookAt(Vector3.up);
            GameObject light = new GameObject("Light");
            light.AddComponent<Light>().type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(45f, -30f, 0f);
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Keqing/KeqingPlayer.prefab");
            GameObject player = PrefabUtility.InstantiatePrefab(prefab) as GameObject;
            player.transform.position = Vector3.zero;
            player.GetComponent<PlayerController>().readInput = false;
            EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(), "Assets/Prefabs/Player/Keqing/KeqingValidation.unity");
            SessionState.SetBool("KeqingCheckRunning", true);
            EditorApplication.EnterPlaymode();
        }
        catch (Exception error)
        {
            File.WriteAllText(Folder + "/play-validation.txt", error.ToString());
            Debug.LogException(error);
        }
        preparing = false;
    }

    static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (SessionState.GetBool("KeqingMainCheckRunning", false))
        {
            if (change == PlayModeStateChange.EnteredPlayMode)
                new GameObject("KeqingMainSceneCheck").AddComponent<KeqingMainSceneCheck>();
            if (change == PlayModeStateChange.EnteredEditMode)
                SessionState.SetBool("KeqingMainCheckRunning", false);
            return;
        }
        if (!SessionState.GetBool("KeqingCheckRunning", false)) return;
        if (change == PlayModeStateChange.EnteredPlayMode)
        {
            GameObject check = new GameObject("KeqingMigrationPlayCheck");
            check.AddComponent<KeqingMigrationPlayCheck>();
        }
        if (change == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.SetBool("KeqingCheckRunning", false);
            string scene = SessionState.GetString("KeqingCheckReturnScene", "Assets/Scenes/SampleScene.unity");
            if (!string.IsNullOrEmpty(scene)) EditorSceneManager.OpenScene(scene);
        }
    }
}

public class KeqingMigrationPlayCheck : MonoBehaviour
{
    PlayerObj player;
    PlayerController controller;
    Animator animator;
    PlayerInput input = new PlayerInput(0.15f, 0.5f);
    List<string> checks = new List<string>();
    List<string> errors = new List<string>();
    int stage;
    float stageTime;
    int hit1;
    int hit2;
    int staggerCount;
    bool secondQueued;
    bool sawDodge;
    bool sawHit;
    Vector3 origin;
    float beforeHp;
    Vector3 lockedPosition;

    void Start()
    {
        Application.runInBackground = true;
        player = Object.FindFirstObjectByType<PlayerObj>();
        controller = player.GetComponent<PlayerController>();
        animator = player.GetComponent<Animator>();
        this.Subscribe(GameEvents.OnPlayerCombo, OnAttackHit);
        player.Staggered += OnStaggered;
        Application.logMessageReceived += OnLog;
        stageTime = Time.time;
    }

    void OnAttackHit(PlayerObj attacker, int index)
    {
        if (attacker != player) return;
        if (index == 0) hit1++;
        if (index == 1) hit2++;
    }

    void OnStaggered(GameObject attacker) { staggerCount++; }
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
        if (stage == 0)
        {
            Submit(Vector2.zero);
            if (elapsed < 0.6f) return;
            Check(animator.HasState(0, Animator.StringToHash("Base Layer.Locomotion.Idle_Wait_A")), "使用通用状态机");
            Check(player.Data.Bag[0] == player.EquippedItem && player.EquippedItem is WeaponData, "背包空手武器开局装备且共用运行数据");
            Check(player.HandingItem != null && player.HandingItem.GetComponent<WeaponObj>() != null, "空手伤害判定对象已创建");
            origin = player.transform.position;
            Next();
        }
        else if (stage == 1)
        {
            Submit(Vector2.up);
            if (elapsed < 3f) return;
            Check(player.transform.position.z - origin.z > 0.3f, "根运动实际推动角色");
            Check(player.transform.position.z < 2.75f, "CharacterController 阻挡墙体穿透");
            Next();
        }
        else if (stage == 2)
        {
            Submit(Vector2.right);
            if (elapsed < 1.2f) return;
            Check(Vector3.Dot(player.transform.forward, Vector3.right) > 0.7f, "转身动画根旋转改变朝向");
            Next();
        }
        else if (stage == 3)
        {
            bool flash = elapsed < 0.04f;
            Submit(Vector2.zero, flash);
            if (animator.GetCurrentAnimatorStateInfo(0).IsTag("Dodge")) sawDodge = true;
            if (elapsed < 1.8f) return;
            Check(sawDodge, "闪避请求进入 Dodge 状态");
            controller.ClearInput();
            animator.Play("Base Layer.Locomotion.Idle_Wait_A", 0, 0f);
            Next();
        }
        else if (stage == 4)
        {
            if (elapsed < 0.04f) input.RecordComboInput(Time.time);
            if (elapsed > 0.6f && !secondQueued)
            {
                input.RecordComboInput(Time.time);
                secondQueued = true;
            }
            Submit(Vector2.zero);
            if (elapsed < 3.4f) return;
            Check(hit1 == 1, "第一段命中事件一次，实际 " + hit1);
            Check(hit2 == 1, "输入缓存接第二段且命中一次，实际 " + hit2);
            beforeHp = player.Data.hp;
            player.poise = 100f;
            player.TakeDamage(1f, null, 1f);
            Check(staggerCount == 0, "韧性未破时不中断动作");
            player.TakeDamage(1f, null, 200f);
            Next();
        }
        else if (stage == 5)
        {
            Submit(Vector2.zero);
            if (animator.GetCurrentAnimatorStateInfo(0).IsName("Hit")) sawHit = true;
            if (elapsed < 1.8f) return;
            Check(staggerCount == 1 && sawHit, "破韧通知一次并播放受击动画");
            Check(Mathf.Approximately(player.Data.hp, beforeHp - 2f), "受击表现不重复扣血");
            this.Publish(GameEvents.OnPlayerTalk, player, true);
            lockedPosition = player.transform.position;
            Next();
        }
        else if (stage == 6)
        {
            input.RecordComboInput(Time.time);
            Submit(Vector2.up, true);
            if (elapsed < 0.5f) return;
            Check(player.IsTalking && !player.CanAct && input.BufferedComboCount == 0, "对话统一锁定动作并清空缓存");
            Vector3 change = player.transform.position - lockedPosition;
            change.y = 0f;
            Check(change.magnitude < 0.05f, "对话期间无水平位移");
            this.Publish(GameEvents.OnPlayerTalk, player, false);
            WeaponData oldWeapon = player.Data.GetWeaponData();
            ItemData replacement = oldWeapon.Clone();
            player.ChangeItem(replacement);
            int hitCount = hit1 + hit2;
            player.PublishAttackHit(oldWeapon, 0);
            Check(hitCount == hit1 + hit2, "换武器后旧攻击事件失效");
            player.ChangeItem(oldWeapon);
            player.Die();
            Next();
        }
        else if (stage == 7)
        {
            Submit(Vector2.up, true);
            if (elapsed < 0.8f) return;
            Check(player.IsDead && controller.IsDead && player.Data.hp == 0f, "玩家和动作脚本死亡状态一致");
            Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Dead"), "死亡可打断动作并进入 Dead");
            Check(errors.Count == 0, "运行期间没有 Error 或 Exception，实际 " + errors.Count);
            checks.AddRange(errors);
            File.WriteAllLines("Tools/Players/KeqingMigration/play-validation.txt", checks);
            EditorApplication.ExitPlaymode();
            stage++;
        }
    }

    void OnDestroy()
    {
        Application.logMessageReceived -= OnLog;
        this.UnSubscribe(GameEvents.OnPlayerCombo, OnAttackHit);
        if (player != null) player.Staggered -= OnStaggered;
    }
}


public class KeqingMainSceneCheck : MonoBehaviour
{
    List<string> checks = new List<string>();
    List<string> errors = new List<string>();
    PlayerObj player;
    bool opened;
    float started;

    void Start()
    {
        Application.runInBackground = true;
        started = Time.realtimeSinceStartup;
        Application.logMessageReceived += OnLog;
        player = Object.FindFirstObjectByType<PlayerObj>();
        player.GetComponent<PlayerController>().readInput = false;
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
            Check(player.gameObject.name == "KeqingPlayer", "SampleScene 使用刻晴玩家");
            Check(manager != null && manager.GetItem("weapon_keqing_unarmed") != null, "空手武器可通过物品表查询");
            UIManager.Instance.Open<BagPanel>();
            BagPanel bag = UIManager.Instance.Get<BagPanel>();
            bool found = false;
            foreach (ItemSlot slot in bag.GetComponentsInChildren<ItemSlot>(true))
            {
                if (slot.Item == null || slot.Item.id != "weapon_keqing_unarmed") continue;
                found = true;
                SerializedObject serialized = new SerializedObject(slot);
                var image = serialized.FindProperty("iconImage").objectReferenceValue as UnityEngine.UI.Image;
                Check(image != null && image.sprite == slot.Item.icon && image.enabled, "背包空手图标显示正确");
                Check(player.SelectSlot(slot), "背包空手格子可以选择装备");
            }
            Check(found, "背包界面绑定刻晴数据");
            bool materials = true;
            foreach (Renderer renderer in player.GetComponentsInChildren<Renderer>())
            foreach (Material material in renderer.sharedMaterials)
                if (material == null || material.shader == null || !material.shader.isSupported) materials = false;
            Check(materials, "刻晴材质与当前渲染管线兼容");
            Camera sceneCamera = Camera.main;
            checks.Add("INFO: player=" + player.transform.position + " camera=" + sceneCamera.transform.position + " pitch=" + sceneCamera.transform.eulerAngles.x);
            foreach (Renderer renderer in player.GetComponentsInChildren<Renderer>())
                checks.Add("INFO: mesh=" + renderer.name + " bounds=" + renderer.bounds);

            ScreenCapture.CaptureScreenshot("Tools/Players/KeqingMigration/main-scene.png");
            opened = true;
            started = Time.realtimeSinceStartup;
            return;
        }
        if (Time.realtimeSinceStartup - started < 2f) return;
        UIManager.Instance.Close<BagPanel>();
        EnemyObj target = null;
        foreach (EnemyObj enemy in Object.FindObjectsByType<EnemyObj>(FindObjectsSortMode.None))
        {
            if (enemy.data != null && enemy.data.hp > 50f) { target = enemy; break; }
        }
        if (target != null)
        {
            var agent = target.GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null) agent.enabled = false;
            target.transform.position = player.transform.position + player.transform.forward * 1.2f;
            Physics.SyncTransforms();
            WeaponData weapon = player.Data.GetWeaponData();
            float hp = target.data.hp;
            player.PublishAttackHit(weapon, 0);
            Check(Mathf.Approximately(target.data.hp, hp - weapon.combo[0].damagePerHit), "空手判定命中真实敌人且只扣一次伤害");
            hp = target.data.hp;
            this.Publish(GameEvents.OnPlayerCombo, (PlayerObj)null, 0);
            Check(Mathf.Approximately(target.data.hp, hp), "武器忽略无关攻击者的命中通知");
        }
        else Check(false, "主场景存在可用于命中验证的敌人");
        Check(errors.Count == 0, "主场景检查期间没有 Error 或 Exception，实际 " + errors.Count);
        checks.AddRange(errors);
        File.WriteAllLines("Tools/Players/KeqingMigration/main-scene-validation.txt", checks);
        EditorApplication.ExitPlaymode();
        enabled = false;
    }

    void OnDestroy() { Application.logMessageReceived -= OnLog; }
}
