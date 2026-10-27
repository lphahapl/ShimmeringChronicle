using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEditor.AddressableAssets;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

// 迁移、引用替换和资源验收都集中在这一个编辑器工具里。
[InitializeOnLoad]
public static class KeqingMigrationSetup
{
    const string Folder = "Assets/Prefabs/Player/Keqing";
    const string Imported = Folder + "/Imported/Arts/Models/53 刻晴";
    const string WeaponFolder = "Assets/Prefabs/Weapon/KeqingUnarmed";
    const string ReportFolder = "Tools/Players/KeqingMigration";
    const string ControllerPath = Folder + "/Keqing.controller";
    const string PrefabPath = Folder + "/KeqingPlayer.prefab";
    const string ConfigPath = Folder + "/KeqingPlayer.asset";
    static bool running;

    static KeqingMigrationSetup()
    {
        EditorApplication.update += CheckRequest;
    }

    static void CheckRequest()
    {
        if (running || EditorApplication.isCompiling) return;
        if (File.Exists(ReportFolder + "/build.request") && EditorApplication.isPlaying)
        {
            EditorApplication.ExitPlaymode();
            return;
        }
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        string request = ReportFolder + "/build.request";
        if (!File.Exists(request)) return;
        File.Delete(request);
        running = true;
        try
        {
            Build();
        }
        catch (Exception error)
        {
            File.WriteAllText(ReportFolder + "/build-result.txt", error.ToString());
            Debug.LogException(error);
        }
        finally { running = false; }
    }

    [MenuItem("Tools/Keqing/Build Migrated Player")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("先退出播放模式");
        Directory.CreateDirectory(ReportFolder);
        AssetDatabase.Refresh();
        Sprite icon = ImportIcon();
        AnimatorController controller = BuildController();
        AddAttackEvents();
        WeaponSO weapon = BuildWeapon(controller, icon);
        PlayerSO config = BuildConfig(weapon);
        GameObject prefab = BuildPlayer(controller, config);
        RegisterWeapon(weapon);
        CleanOldPlayer();
        ReplaceScenePlayer(prefab);
        AssetDatabase.SaveAssets();
        ValidateAssets();
        File.WriteAllText(ReportFolder + "/build-result.txt", "PASS: 刻晴、空手武器、背包、伤害动画事件和 SampleScene 引用已配置。\n");
        Debug.Log("[KeqingMigration] 迁移配置完成");
    }

    static Sprite ImportIcon()
    {
        string path = WeaponFolder + "/UI/KeqingUnarmed.png";
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) throw new InvalidOperationException("空手图标未导入");
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.maxTextureSize = 512;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static AnimationClip LoadReaction(string fileName)
    {
        string path = Folder + "/Imported/Girl_GreatSword_AnimSet/Animation/Humanoid/" + fileName + ".fbx";
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            AnimationClip clip = asset as AnimationClip;
            if (clip != null && !clip.name.StartsWith("__preview__")) return clip;
        }
        throw new InvalidOperationException("找不到动作 " + path);
    }

    static AnimatorState FindState(AnimatorStateMachine machine, string name)
    {
        foreach (ChildAnimatorState child in machine.states)
            if (child.state.name == name) return child.state;
        foreach (ChildAnimatorStateMachine child in machine.stateMachines)
        {
            AnimatorState state = FindState(child.stateMachine, name);
            if (state != null) return state;
        }
        return null;
    }

    static void AddParameter(AnimatorController controller, string name, AnimatorControllerParameterType type)
    {
        foreach (AnimatorControllerParameter parameter in controller.parameters)
            if (parameter.name == name) return;
        controller.AddParameter(name, type);
    }

    static AnimatorController BuildController()
    {
        if (AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath) == null)
            AssetDatabase.CopyAsset(Imported + "/Keqing_CLazyIdle.controller", ControllerPath);
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(ControllerPath);
        AnimatorStateMachine root = controller.layers[0].stateMachine;
        AddParameter(controller, "Hit", AnimatorControllerParameterType.Trigger);
        AddParameter(controller, "IsDead", AnimatorControllerParameterType.Bool);
        AnimatorState idle = FindState(root, "Idle_Wait_A");
        if (idle == null) throw new InvalidOperationException("找不到刻晴待机状态");
        AnimatorState hit = FindState(root, "Hit");
        if (hit == null)
        {
            hit = root.AddState("Hit", new Vector3(800, 150));
            hit.tag = "Hit";
            hit.motion = LoadReaction("hit_head_front_inplace");
            AnimatorStateTransition enterHit = root.AddAnyStateTransition(hit);
            enterHit.hasExitTime = false;
            enterHit.duration = 0.05f;
            enterHit.canTransitionToSelf = false;
            enterHit.AddCondition(AnimatorConditionMode.If, 0f, "Hit");
            enterHit.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsDead");
            AnimatorStateTransition leaveHit = hit.AddTransition(idle);
            leaveHit.hasExitTime = true;
            leaveHit.exitTime = 0.9f;
            leaveHit.duration = 0.1f;
            leaveHit.AddCondition(AnimatorConditionMode.IfNot, 0f, "IsDead");
        }
        AnimatorState dead = FindState(root, "Dead");
        if (dead == null)
        {
            dead = root.AddState("Dead", new Vector3(800, 300));
            dead.tag = "Dead";
            dead.motion = LoadReaction("dead_01_inplace");
            AnimatorStateTransition enterDead = root.AddAnyStateTransition(dead);
            enterDead.hasExitTime = false;
            enterDead.duration = 0.06f;
            enterDead.canTransitionToSelf = false;
            enterDead.AddCondition(AnimatorConditionMode.If, 0f, "IsDead");
        }
        EditorUtility.SetDirty(controller);
        return controller;
    }

    static void AddAttackEvents()
    {
        for (int i = 1; i <= 2; i++)
        {
            string path = Imported + "/Animations/Attack/Keqing_Combo" + i + ".anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            List<AnimationEvent> events = new List<AnimationEvent>();
            foreach (AnimationEvent existing in AnimationUtility.GetAnimationEvents(clip))
                if (existing.functionName != "PublishPlayerCombo") events.Add(existing);
            AnimationEvent hit = new AnimationEvent();
            hit.functionName = "PublishPlayerCombo";
            hit.time = clip.length * 0.35f;
            events.Add(hit);
            events.Sort(CompareEventTime);
            AnimationUtility.SetAnimationEvents(clip, events.ToArray());
            EditorUtility.SetDirty(clip);
        }
    }

    static int CompareEventTime(AnimationEvent first, AnimationEvent second)
    {
        return first.time.CompareTo(second.time);
    }

    static WeaponSO BuildWeapon(AnimatorController controller, Sprite icon)
    {
        string overridePath = WeaponFolder + "/KeqingUnarmed.overrideController";
        AnimatorOverrideController animation = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(overridePath);
        if (animation == null)
        {
            animation = new AnimatorOverrideController(controller);
            AssetDatabase.CreateAsset(animation, overridePath);
        }
        animation.runtimeAnimatorController = controller;
        string configPath = WeaponFolder + "/KeqingUnarmed.asset";
        WeaponSO weapon = AssetDatabase.LoadAssetAtPath<WeaponSO>(configPath);
        if (weapon == null)
        {
            weapon = ScriptableObject.CreateInstance<WeaponSO>();
            AssetDatabase.CreateAsset(weapon, configPath);
        }
        weapon.id = "weapon_keqing_unarmed";
        weapon.itemName = "空手";
        weapon.description = "刻晴的空手格斗。两段连击，Shift 闪避；在背包中选择即可装备。";
        weapon.stackCount = 1;
        weapon.icon = icon;
        weapon.overrideController = animation;
        weapon.stopDistance = 1.4f;
        weapon.attackBreak = 0.8f;
        weapon.combo = new List<AttackData>();
        for (int i = 0; i < 2; i++)
        {
            AttackData attack = new AttackData();
            attack.damagePerHit = 14f + i * 6f;
            attack.poiseDamage = 35f + i * 20f;
            attack.radius = 1.4f + i * 0.15f;
            attack.angle = 110f;
            attack.judgeOffset = new Vector3(0f, 0.9f, 0.65f);
            attack.priority = ETimePriority.HitStop;
            attack.scale = 0.08f;
            attack.time = 0.045f;
            weapon.combo.Add(attack);
        }
        GameObject root = new GameObject("KeqingUnarmed");
        WeaponObj weaponObj = root.AddComponent<WeaponObj>();
        weaponObj.SO = weapon;
        weaponObj.effPos = root.transform;
        weaponObj.showDebug = false;
        weapon.prefab = PrefabUtility.SaveAsPrefabAsset(root, WeaponFolder + "/KeqingUnarmed.prefab");
        Object.DestroyImmediate(root);
        EditorUtility.SetDirty(weapon);
        EditorUtility.SetDirty(animation);
        return weapon;
    }

    static PlayerSO BuildConfig(WeaponSO weapon)
    {
        PlayerSO config = AssetDatabase.LoadAssetAtPath<PlayerSO>(ConfigPath);
        if (config == null)
        {
            PlayerSO original = AssetDatabase.LoadAssetAtPath<PlayerSO>("Assets/Prefabs/Player/Warrior/TestPlayer1.asset");
            config = Object.Instantiate(original);
            AssetDatabase.CreateAsset(config, ConfigPath);
        }
        config.targetName = "刻晴";
        config.description = "使用迁入状态机的刻晴，开局装备背包中的空手武器。";
        config.startingWeapon = weapon;
        config.quickBar = new List<ItemSlotData>();
        config.Bag = new List<ItemSlotData>();
        ItemSlotData slot = new ItemSlotData();
        slot.config = weapon;
        slot.num = 1;
        config.Bag.Add(slot);
        config.initialHp = config.maxHp;
        EditorUtility.SetDirty(config);
        return config;
    }

    static GameObject BuildPlayer(AnimatorController controller, PlayerSO config)
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(Imported + "/Keqing_Humanoid.prefab");
        GameObject root = PrefabUtility.InstantiatePrefab(model) as GameObject;
        PrefabUtility.UnpackPrefabInstance(root, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
        root.name = "KeqingPlayer";
        root.tag = "Player";
        int playerLayer = LayerMask.NameToLayer("Player");
        if (playerLayer >= 0) root.layer = playerLayer;
        root.transform.position = Vector3.zero;
        root.transform.rotation = Quaternion.identity;
        Animator animator = root.GetComponent<Animator>();
        animator.runtimeAnimatorController = controller;
        animator.applyRootMotion = true;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        CharacterController capsule = root.AddComponent<CharacterController>();
        capsule.height = 1.6f;
        capsule.radius = 0.25f;
        capsule.center = new Vector3(0f, 0.8f, 0f);
        capsule.stepOffset = 0.25f;
        capsule.skinWidth = 0.03f;
        PlayerObj player = root.AddComponent<PlayerObj>();
        SerializedObject data = new SerializedObject(player);
        data.FindProperty("config").objectReferenceValue = config;
        data.ApplyModifiedPropertiesWithoutUndo();
        player.handPos = animator.GetBoneTransform(HumanBodyBones.RightHand);
        CharacterTime time = root.AddComponent<CharacterTime>();
        player.hitStop = time;
        SerializedObject clock = new SerializedObject(time);
        clock.FindProperty("animator").objectReferenceValue = animator;
        clock.ApplyModifiedPropertiesWithoutUndo();
        PlayerController controllerObj = root.AddComponent<PlayerController>();
        controllerObj.animator = animator;
        controllerObj.characterController = capsule;

        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
        Object.DestroyImmediate(root);
        return prefab;
    }

    static void CleanOldPlayer()
    {
        string path = "Assets/Prefabs/Player/Warrior/Character Warrior.prefab";
        GameObject root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(root);
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    static void RegisterWeapon(WeaponSO weapon)
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null) return;
        string guid = AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(weapon));
        var entry = settings.CreateOrMoveEntry(guid, settings.DefaultGroup);
        entry.address = weapon.id;
        settings.AddLabel("ItemSO");
        entry.SetLabel("ItemSO", true);
        EditorUtility.SetDirty(settings);
    }

    static void ReplaceScenePlayer(GameObject prefab)
    {
        string scenePath = "Assets/Scenes/SampleScene.unity";
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != scenePath)
        {
            if (scene.isDirty) EditorSceneManager.SaveScene(scene);
            scene = EditorSceneManager.OpenScene(scenePath);
        }
        PlayerObj oldPlayer = Object.FindFirstObjectByType<PlayerObj>();
        if (oldPlayer == null) throw new InvalidOperationException("SampleScene 没有玩家");
        if (oldPlayer.gameObject.name == "KeqingPlayer")
        {
            GameObjectUtility.RemoveMonoBehavioursWithMissingScript(oldPlayer.gameObject);
            ConfigureCamera(oldPlayer.transform);
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            return;
        }
        GameObject oldRoot = oldPlayer.gameObject;
        GameObject newRoot = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
        newRoot.transform.SetPositionAndRotation(oldRoot.transform.position, oldRoot.transform.rotation);
        PlayerObj newPlayer = newRoot.GetComponent<PlayerObj>();
        newPlayer.t = oldPlayer.t;
        newPlayer.prompt = oldPlayer.prompt;
        Dictionary<Object, Object> map = new Dictionary<Object, Object>();
        map[oldRoot] = newRoot;
        map[oldRoot.transform] = newRoot.transform;
        foreach (Component old in oldRoot.GetComponents<Component>())
        {
            if (old == null) continue;
            Component replacement = newRoot.GetComponent(old.GetType());
            if (replacement != null) map[old] = replacement;
        }
        // 逐个替换场景引用，血条、背包、快捷栏和相机继续指向新玩家。
        int replacements = 0;
        foreach (GameObject sceneRoot in scene.GetRootGameObjects())
        foreach (Component component in sceneRoot.GetComponentsInChildren<Component>(true))
        {
            if (component == null || component.gameObject == oldRoot || component.transform.IsChildOf(oldRoot.transform)) continue;
            SerializedObject serialized = new SerializedObject(component);
            SerializedProperty property = serialized.GetIterator();
            while (property.Next(true))
            {
                if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                Object oldValue = property.objectReferenceValue;
                if (oldValue == null) continue;
                Object newValue;
                if (!map.TryGetValue(oldValue, out newValue)) continue;
                property.objectReferenceValue = newValue;
                replacements++;
            }
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
        Object.DestroyImmediate(oldRoot);
        ConfigureCamera(newRoot.transform);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        File.WriteAllText(ReportFolder + "/scene-reference-replacements.txt", "Replaced references: " + replacements + "\n");
        Selection.activeGameObject = newRoot;
    }

    static void ConfigureCamera(Transform player)
    {
        CameraMove camera = Object.FindFirstObjectByType<CameraMove>();
        if (camera == null) return;
        camera.player = player;
        camera.pivotHeight = 1.1f;
        camera.offset = new Vector3(0.5f, 0.25f, -3.2f);
        camera.transform.rotation = Quaternion.Euler(10f, camera.transform.eulerAngles.y, 0f);
        EditorUtility.SetDirty(camera);
    }

    [MenuItem("Tools/Keqing/Validate Migrated Assets")]
    public static void ValidateAssets()
    {
        List<string> checks = new List<string>();
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabPath);
        Require(prefab != null, "玩家预制体存在", checks);
        Animator animator = prefab.GetComponent<Animator>();
        Require(animator.avatar != null && animator.avatar.isHuman && animator.avatar.isValid, "Humanoid Avatar 有效", checks);
        Require(prefab.GetComponent<PlayerController>() != null, "通用动作控制器已挂载", checks);
        Require(prefab.GetComponent<CharacterController>() != null, "碰撞控制器存在", checks);
        WeaponSO weapon = AssetDatabase.LoadAssetAtPath<WeaponSO>(WeaponFolder + "/KeqingUnarmed.asset");
        Require(weapon != null && weapon.combo.Count == 2 && weapon.icon != null && weapon.prefab != null, "空手两段攻击、图标和判定预制体有效", checks);
        PlayerSO config = AssetDatabase.LoadAssetAtPath<PlayerSO>(ConfigPath);
        Require(config.Bag.Count == 1 && config.Bag[0].config == weapon && config.startingWeapon == weapon, "空手位于背包且配置为开局装备", checks);
        AnimatorOverrideController animation = weapon.overrideController;
        Require(animation != null && animation.runtimeAnimatorController == animator.runtimeAnimatorController, "空手覆盖控制器使用刻晴基础状态机", checks);
        for (int i = 1; i <= 2; i++)
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Imported + "/Animations/Attack/Keqing_Combo" + i + ".anim");
            bool hit = false;
            bool combo = false;
            foreach (AnimationEvent entry in AnimationUtility.GetAnimationEvents(clip))
            {
                if (entry.functionName == "PublishPlayerCombo") hit = true;
                if (entry.functionName == "TryComsumeCombo") combo = true;
            }
            Require(hit, "Combo" + i + " 有伤害事件", checks);
            if (i == 1) Require(combo, "Combo1 保留接招事件", checks);
        }
        File.WriteAllLines(ReportFolder + "/asset-validation.txt", checks);
    }

    static void Require(bool passed, string name, List<string> checks)
    {
        if (!passed) throw new InvalidOperationException("FAIL: " + name);
        checks.Add("PASS: " + name);
    }
}
