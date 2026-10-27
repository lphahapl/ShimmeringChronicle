using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

// 琳妮特只配置自己的资源，角色逻辑和基础状态机继续共用。
[InitializeOnLoad]
public static class LynetteMigrationSetup
{
    public const string Folder = "Assets/Prefabs/Player/Lynette";
    public const string CommonController = "Assets/Prefabs/Player/Keqing/Keqing.controller";
    public const string Report = "Tools/Players/LynetteMigration";
    public const string ScenePath = "Assets/Scenes/LynetteDemo.unity";
    public const string PlayerPath = Folder + "/LynettePlayer.prefab";
    public const string WeaponPath = Folder + "/Weapons/LynetteKatana.asset";
    static bool building;

    [Serializable] public class AttackSource { public string state; public string path; public string fileID; }
    [Serializable] public class Manifest { public string model; public string weapon; public string sourceHandFileID; public AttackSource[] attacks; }

    static LynetteMigrationSetup() { EditorApplication.update += CheckRequest; }

    static void CheckRequest()
    {
        if (building || EditorApplication.isCompiling || EditorApplication.isPlaying || EditorApplication.isPlayingOrWillChangePlaymode) return;
        if (File.Exists(Report + "/sync.request"))
        {
            File.Delete(Report + "/sync.request");
            try { SyncAnimations(); }
            catch (Exception error) { File.WriteAllText(Report + "/sync-result.txt", error.ToString()); Debug.LogException(error); }
            return;
        }
        if (!File.Exists(Report + "/build.request")) return;
        File.Delete(Report + "/build.request");
        building = true;
        try { Build(); }
        catch (Exception error)
        {
            File.WriteAllText(Report + "/build-result.txt", error.ToString());
            Debug.LogException(error);
        }
        finally { building = false; }
    }

    [MenuItem("Tools/Characters/Build Lynette")]
    public static void Build()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("先退出播放模式");
        Directory.CreateDirectory(Report);
        Directory.CreateDirectory(Folder + "/Animations");
        Directory.CreateDirectory(Folder + "/Materials");
        Directory.CreateDirectory(Folder + "/Weapons/UI");
        AssetDatabase.Refresh();
        Manifest manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(Report + "/import-manifest.json"));
        AnimatorController common = AssetDatabase.LoadAssetAtPath<AnimatorController>(CommonController);
        EnsureThirdCombo(common);
        AnimatorOverrideController animation = BuildAnimations(common, manifest);
        WeaponSO weapon = BuildWeapon(animation, manifest.weapon);
        PlayerSO config = BuildPlayerConfig(weapon);
        GameObject player = BuildPlayer(common, config, manifest);
        RegisterWeapon(weapon);
        BuildScene(player);
        AssetDatabase.SaveAssets();
        Validate();
        File.WriteAllText(Report + "/build-result.txt", "PASS: 琳妮特、源场景太刀、三段攻击、独立动画目录与试玩场景已配置。\n");
    }

    [MenuItem("Tools/Characters/Sync Lynette Animations")]
    public static void SyncAnimations()
    {
        Manifest manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(Report + "/import-manifest.json"));
        AnimatorController common = AssetDatabase.LoadAssetAtPath<AnimatorController>(CommonController);
        AnimatorOverrideController animation = BuildAnimations(common, manifest);
        WeaponSO weapon = AssetDatabase.LoadAssetAtPath<WeaponSO>(WeaponPath);
        weapon.overrideController = animation;
        EditorUtility.SetDirty(weapon);
        AssetDatabase.SaveAssets();
        Validate();
        File.WriteAllText(Report + "/sync-result.txt", "PASS: 已同步当前通用状态机的全部动画引用。\n");
    }

    public static AnimatorState FindState(AnimatorStateMachine machine, string name)
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

    static AnimatorStateMachine FindMachine(AnimatorStateMachine machine, string name)
    {
        if (machine.name == name) return machine;
        foreach (ChildAnimatorStateMachine child in machine.stateMachines)
        {
            AnimatorStateMachine result = FindMachine(child.stateMachine, name);
            if (result != null) return result;
        }
        return null;
    }

    static void EnsureThirdCombo(AnimatorController controller)
    {
        AnimatorStateMachine root = controller.layers[0].stateMachine;
        AnimatorState existing = FindState(root, "Combo3");
        if (existing != null)
        {
            foreach (AnimatorStateTransition transition in existing.transitions)
                if (transition.conditions.Length == 0 && transition.hasExitTime) transition.exitTime = 0.95f;
            EditorUtility.SetDirty(controller);
            return;
        }
        AnimatorState second = FindState(root, "Combo2");
        AnimatorStateMachine attack = FindMachine(root, "Attack");
        if (second == null || attack == null) throw new InvalidOperationException("通用状态机缺少攻击状态");
        Directory.CreateDirectory("Assets/Prefabs/Player/Keqing/Animations");
        AssetDatabase.Refresh();
        string placeholderPath = "Assets/Prefabs/Player/Keqing/Animations/Combo3.anim";
        AnimationClip placeholder = AssetDatabase.LoadAssetAtPath<AnimationClip>(placeholderPath);
        if (placeholder == null)
        {
            placeholder = Object.Instantiate(second.motion as AnimationClip);
            placeholder.name = "Combo3_Placeholder";
            AnimationUtility.SetAnimationEvents(placeholder, new AnimationEvent[0]);
            AssetDatabase.CreateAsset(placeholder, placeholderPath);
        }
        controller.AddParameter("Combo3", AnimatorControllerParameterType.Trigger);
        AnimatorState third = attack.AddState("Combo3", new Vector3(750, 100));
        third.motion = placeholder;
        third.tag = "Attack";
        foreach (AnimatorStateTransition source in second.transitions)
        {
            AnimatorStateTransition transition = null;
            if (source.isExit) transition = third.AddExitTransition();
            else if (source.destinationState != null) transition = third.AddTransition(source.destinationState);
            else if (source.destinationStateMachine != null) transition = third.AddTransition(source.destinationStateMachine);
            if (transition == null) continue;
            transition.conditions = source.conditions;
            transition.hasExitTime = source.hasExitTime;
            transition.exitTime = source.exitTime;
            if (source.conditions.Length == 0 && source.hasExitTime) transition.exitTime = 0.95f;
            transition.duration = source.duration;
            transition.hasFixedDuration = source.hasFixedDuration;
            transition.interruptionSource = source.interruptionSource;
            transition.orderedInterruption = source.orderedInterruption;
            transition.canTransitionToSelf = false;
        }
        AnimatorStateTransition next = second.AddTransition(third);
        next.hasExitTime = true;
        next.exitTime = 0.65f;
        next.duration = 0.08f;
        next.hasFixedDuration = true;
        next.AddCondition(AnimatorConditionMode.If, 0f, "Combo3");
        List<AnimatorStateTransition> transitions = new List<AnimatorStateTransition>(second.transitions);
        transitions.Remove(next);
        int insert = transitions.Count;
        for (int i = 0; i < transitions.Count; i++)
            if (transitions[i].conditions.Length == 0) { insert = i; break; }
        transitions.Insert(insert, next);
        second.transitions = transitions.ToArray();
        EditorUtility.SetDirty(controller);
    }

    static AnimationClip ReadClip(string path, string fileID)
    {
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            AnimationClip clip = asset as AnimationClip;
            if (clip == null || clip.name.StartsWith("__preview__")) continue;
            string guid;
            long id;
            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(clip, out guid, out id) && id.ToString() == fileID) return clip;
        }
        throw new InvalidOperationException("找不到源场景动画 " + path + " / " + fileID);
    }

    static AnimatorOverrideController BuildAnimations(AnimatorController common, Manifest manifest)
    {
        Dictionary<string, AnimationClip> attacks = new Dictionary<string, AnimationClip>();
        foreach (AttackSource source in manifest.attacks) attacks[source.state] = ReadClip(source.path, source.fileID);
        string path = Folder + "/Animations/LynetteKatana.overrideController";
        AnimatorOverrideController result = AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(path);
        if (result == null)
        {
            result = new AnimatorOverrideController(common);
            AssetDatabase.CreateAsset(result, path);
        }
        result.runtimeAnimatorController = common;
        List<KeyValuePair<AnimationClip, AnimationClip>> overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        result.GetOverrides(overrides);
        AnimatorStateMachine root = common.layers[0].stateMachine;
        Dictionary<AnimationClip, int> stages = new Dictionary<AnimationClip, int>();
        for (int i = 1; i <= 3; i++) stages[FindState(root, "Combo" + i).motion as AnimationClip] = i;
        for (int i = 0; i < overrides.Count; i++)
        {
            AnimationClip original = overrides[i].Key;
            AnimationClip source = original;
            string name = "Lynette_" + original.name;
            int stage;
            bool isAttack = stages.TryGetValue(original, out stage);
            if (isAttack)
            {
                source = attacks["Attack" + stage];
                name = "Lynette_Attack" + stage;
            }
            string clipPath = Folder + "/Animations/" + name + ".anim";
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(clipPath);
            if (clip == null)
            {
                clip = Object.Instantiate(source);
                clip.name = name;
                AssetDatabase.CreateAsset(clip, clipPath);
            }
            if (isAttack)
            {
                List<AnimationEvent> events = new List<AnimationEvent>();
                AnimationEvent hit = new AnimationEvent();
                hit.functionName = "PublishPlayerCombo";
                hit.time = clip.length * 0.35f;
                events.Add(hit);
                if (stage < 3)
                {
                    AnimationEvent next = new AnimationEvent();
                    next.functionName = "TryComsumeCombo";
                    next.time = clip.length * 0.55f;
                    events.Add(next);
                }
                AnimationUtility.SetAnimationEvents(clip, events.ToArray());
                var settings = AnimationUtility.GetAnimationClipSettings(clip);
                settings.loopTime = false;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
                EditorUtility.SetDirty(clip);
            }
            overrides[i] = new KeyValuePair<AnimationClip, AnimationClip>(original, clip);
        }
        result.ApplyOverrides(overrides);
        EditorUtility.SetDirty(result);
        return result;
    }

    static void ConvertMaterials(GameObject root, string prefix)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) throw new InvalidOperationException("没有找到 URP Lit");
        Dictionary<Material, Material> converted = new Dictionary<Material, Material>();
        List<string> report = new List<string>();
        foreach (Renderer renderer in root.GetComponentsInChildren<Renderer>(true))
        {
            Material[] slots = renderer.sharedMaterials;
            for (int i = 0; i < slots.Length; i++)
            {
                Material original = slots[i];
                if (original == null) continue;
                Material material;
                if (!converted.TryGetValue(original, out material))
                {
                    string safeName = original.name;
                    foreach (char invalid in Path.GetInvalidFileNameChars()) safeName = safeName.Replace(invalid, '_');
                    string path = Folder + "/Materials/" + prefix + "_" + safeName + ".mat";
                    material = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (material == null)
                    {
                        material = new Material(shader);
                        material.name = prefix + "_" + safeName;
                        AssetDatabase.CreateAsset(material, path);
                    }
                    material.shader = shader;
                    material.SetColor("_BaseColor", original.color);
                    material.SetTexture("_BaseMap", original.mainTexture);
                    material.SetTextureScale("_BaseMap", original.mainTextureScale);
                    material.SetTextureOffset("_BaseMap", original.mainTextureOffset);
                    material.SetFloat("_Metallic", 0f);
                    material.SetFloat("_Smoothness", 0.2f);
                    material.SetFloat("_Cull", 0f);
                    material.SetFloat("_AlphaClip", 1f);
                    material.EnableKeyword("_ALPHATEST_ON");
                    material.doubleSidedGI = true;
                    converted.Add(original, material);
                    string texture = "none";
                    if (original.mainTexture != null) texture = AssetDatabase.GetAssetPath(original.mainTexture);
                    report.Add(original.name + " -> " + texture);
                    EditorUtility.SetDirty(material);
                }
                slots[i] = material;
            }
            renderer.sharedMaterials = slots;
        }
        File.WriteAllLines(Report + "/" + prefix + "-materials.txt", report);
    }

    static WeaponSO BuildWeapon(AnimatorOverrideController animation, string sourcePath)
    {
        WeaponSO weapon = AssetDatabase.LoadAssetAtPath<WeaponSO>(WeaponPath);
        if (weapon == null)
        {
            weapon = ScriptableObject.CreateInstance<WeaponSO>();
            AssetDatabase.CreateAsset(weapon, WeaponPath);
        }
        weapon.id = "weapon_lynette_katana";
        weapon.itemName = "琳妮特的太刀";
        weapon.description = "源场景使用的太刀，三段斩击，共用通用状态机。";
        weapon.stackCount = 1;
        weapon.overrideController = animation;
        weapon.combo = new List<AttackData>();
        for (int i = 0; i < 3; i++)
        {
            AttackData attack = new AttackData();
            attack.damagePerHit = 18f + i * 6f;
            attack.poiseDamage = 30f + i * 15f;
            attack.radius = 2f + i * 0.15f;
            attack.angle = 130f;
            attack.judgeOffset = new Vector3(0f, 0.9f, 0.8f);
            attack.priority = ETimePriority.HitStop;
            attack.scale = 0.08f;
            attack.time = 0.045f;
            weapon.combo.Add(attack);
        }
        GameObject source = AssetDatabase.LoadAssetAtPath<GameObject>(sourcePath);
        GameObject root = Object.Instantiate(source);
        root.name = "LynetteKatana";
        ConvertMaterials(root, "Katana");
        WeaponObj weaponObj = root.GetComponent<WeaponObj>();
        if (weaponObj == null) weaponObj = root.AddComponent<WeaponObj>();
        weaponObj.SO = weapon;
        weaponObj.effPos = root.transform;
        weaponObj.showDebug = false;
        weapon.prefab = PrefabUtility.SaveAsPrefabAsset(root, Folder + "/Weapons/LynetteKatana.prefab");
        weapon.icon = RenderWeaponIcon(root);
        Object.DestroyImmediate(root);
        EditorUtility.SetDirty(weapon);
        return weapon;
    }

    static Sprite RenderWeaponIcon(GameObject weapon)
    {
        string path = Folder + "/Weapons/UI/LynetteKatana.png";
        Scene preview = EditorSceneManager.NewPreviewScene();
        RenderTexture target = RenderTexture.GetTemporary(256, 256, 24);
        RenderTexture previous = RenderTexture.active;
        try
        {
            GameObject model = Object.Instantiate(weapon);
            Object.DestroyImmediate(model.GetComponent<WeaponObj>());
            SceneManager.MoveGameObjectToScene(model, preview);
            Bounds bounds = new Bounds();
            bool first = true;
            foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>())
            {
                if (first) { bounds = renderer.bounds; first = false; }
                else bounds.Encapsulate(renderer.bounds);
            }
            GameObject cameraObject = new GameObject("WeaponIconCamera");
            SceneManager.MoveGameObjectToScene(cameraObject, preview);
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.scene = preview;
            camera.orthographic = true;
            camera.orthographicSize = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z) * 0.6f;
            camera.transform.position = bounds.center + new Vector3(1f, 0.3f, -2f).normalized * 3f;
            camera.transform.LookAt(bounds.center);
            camera.nearClipPlane = 0.01f;
            camera.farClipPlane = 10f;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.1f, 0.16f, 1f);
            GameObject lightObject = new GameObject("WeaponIconLight");
            SceneManager.MoveGameObjectToScene(lightObject, preview);
            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 2f;
            light.transform.rotation = Quaternion.Euler(35f, 30f, 0f);
            UniversalRenderPipeline.SingleCameraRequest request = new UniversalRenderPipeline.SingleCameraRequest();
            request.destination = target;
            RenderPipeline.SubmitRenderRequest(camera, request);
            RenderTexture.active = target;
            Texture2D texture = new Texture2D(256, 256, TextureFormat.RGBA32, false);
            texture.ReadPixels(new Rect(0, 0, 256, 256), 0, 0);
            texture.Apply();
            Color32[] pixels = texture.GetPixels32();
            int visible = 0;
            Color32 background = pixels[0];
            foreach (Color32 color in pixels)
                if (Mathf.Abs(color.r - background.r) + Mathf.Abs(color.g - background.g) + Mathf.Abs(color.b - background.b) > 40) visible++;
            File.WriteAllText(Report + "/icon-validation.txt", "Weapon foreground pixels: " + visible);
            if (visible < 200) throw new InvalidOperationException("武器图标没有渲染出模型");
            File.WriteAllBytes(path, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);
        }
        finally
        {
            RenderTexture.active = previous;
            RenderTexture.ReleaseTemporary(target);
            EditorSceneManager.ClosePreviewScene(preview);
        }
        AssetDatabase.ImportAsset(path);
        TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }

    static PlayerSO BuildPlayerConfig(WeaponSO weapon)
    {
        string path = Folder + "/LynettePlayer.asset";
        PlayerSO config = AssetDatabase.LoadAssetAtPath<PlayerSO>(path);
        if (config == null)
        {
            config = Object.Instantiate(AssetDatabase.LoadAssetAtPath<PlayerSO>("Assets/Prefabs/Player/Keqing/KeqingPlayer.asset"));
            AssetDatabase.CreateAsset(config, path);
        }
        config.targetName = "琳妮特";
        config.description = "共用通用状态机，使用源场景太刀和攻击动画。";
        config.startingWeapon = weapon;
        config.Bag = new List<ItemSlotData>();
        ItemSlotData slot = new ItemSlotData();
        slot.config = weapon;
        slot.num = 1;
        config.Bag.Add(slot);
        config.quickBar = new List<ItemSlotData>();
        EditorUtility.SetDirty(config);
        return config;
    }

    static GameObject BuildPlayer(AnimatorController controller, PlayerSO config, Manifest manifest)
    {
        GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(manifest.model);
        string handName = "";
        foreach (Transform bone in model.GetComponentsInChildren<Transform>())
        {
            string guid;
            long id;
            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(bone, out guid, out id) && id.ToString() == manifest.sourceHandFileID)
                handName = bone.name;
        }
        GameObject root = Object.Instantiate(model);
        root.name = "LynettePlayer";
        root.tag = "Player";
        int layer = LayerMask.NameToLayer("Player");
        if (layer >= 0) root.layer = layer;
        root.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
        ConvertMaterials(root, "Lynette");
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
        SerializedObject playerSettings = new SerializedObject(player);
        playerSettings.FindProperty("config").objectReferenceValue = config;
        playerSettings.ApplyModifiedPropertiesWithoutUndo();
        player.handPos = animator.GetBoneTransform(HumanBodyBones.RightHand);
        foreach (Transform bone in root.GetComponentsInChildren<Transform>())
            if (bone.name == handName) player.handPos = bone;
        CharacterTime time = root.AddComponent<CharacterTime>();
        player.hitStop = time;
        SerializedObject timeSettings = new SerializedObject(time);
        timeSettings.FindProperty("animator").objectReferenceValue = animator;
        timeSettings.ApplyModifiedPropertiesWithoutUndo();
        PlayerController motion = root.AddComponent<PlayerController>();
        motion.animator = animator;
        motion.characterController = capsule;
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, PlayerPath);
        File.WriteAllText(Report + "/hand-mount.txt", "Source hand=" + handName + "\nBound hand=" + player.handPos.name);
        Object.DestroyImmediate(root);
        return prefab;
    }

    static void RegisterWeapon(WeaponSO weapon)
    {
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null) return;
        var entry = settings.CreateOrMoveEntry(AssetDatabase.AssetPathToGUID(WeaponPath), settings.DefaultGroup);
        entry.address = weapon.id;
        settings.AddLabel("ItemSO");
        entry.SetLabel("ItemSO", true);
        EditorUtility.SetDirty(settings);
    }

    static void BuildScene(GameObject prefab)
    {
        Scene original = SceneManager.GetActiveScene();
        string returnPath = original.path;
        if (original.isDirty) EditorSceneManager.SaveScene(original);
        if (!File.Exists(ScenePath)) AssetDatabase.CopyAsset("Assets/Scenes/SampleScene.unity", ScenePath);
        Scene scene = EditorSceneManager.OpenScene(ScenePath);
        PlayerObj previous = Object.FindFirstObjectByType<PlayerObj>();
        if (previous == null) throw new InvalidOperationException("试玩场景没有玩家");
        if (previous.name != "LynettePlayer")
        {
            GameObject oldRoot = previous.gameObject;
            GameObject root = PrefabUtility.InstantiatePrefab(prefab, scene) as GameObject;
            root.transform.SetPositionAndRotation(oldRoot.transform.position, oldRoot.transform.rotation);
            PlayerObj player = root.GetComponent<PlayerObj>();
            player.t = previous.t;
            player.prompt = previous.prompt;
            Dictionary<Object, Object> map = new Dictionary<Object, Object>();
            map[oldRoot] = root;
            foreach (Component old in oldRoot.GetComponents<Component>())
            {
                if (old == null) continue;
                Component next = root.GetComponent(old.GetType());
                if (next != null) map[old] = next;
            }
            foreach (GameObject sceneRoot in scene.GetRootGameObjects())
            foreach (Component component in sceneRoot.GetComponentsInChildren<Component>(true))
            {
                if (component == null || component.transform.IsChildOf(oldRoot.transform)) continue;
                SerializedObject serialized = new SerializedObject(component);
                SerializedProperty property = serialized.GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference) continue;
                    Object value = property.objectReferenceValue;
                    if (value == null || !map.ContainsKey(value)) continue;
                    property.objectReferenceValue = map[value];
                }
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            Object.DestroyImmediate(oldRoot);
        }
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        if (!string.IsNullOrEmpty(returnPath) && returnPath != ScenePath) EditorSceneManager.OpenScene(returnPath);
    }

    [MenuItem("Tools/Characters/Validate Lynette")]
    public static void Validate()
    {
        List<string> checks = new List<string>();
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PlayerPath);
        Require(prefab != null, "琳妮特玩家预制体存在", checks);
        Animator animator = prefab.GetComponent<Animator>();
        Require(animator.avatar != null && animator.avatar.isValid && animator.avatar.isHuman, "琳妮特自身 Humanoid Avatar 有效", checks);
        Require(AssetDatabase.GetAssetPath(animator.runtimeAnimatorController) == CommonController, "直接共用通用基础状态机", checks);
        Require(prefab.GetComponent<PlayerController>() != null && prefab.GetComponent<PlayerObj>() != null, "共用玩家逻辑", checks);
        WeaponSO weapon = AssetDatabase.LoadAssetAtPath<WeaponSO>(WeaponPath);
        Require(weapon.combo.Count == 3 && weapon.prefab != null && weapon.icon != null, "源场景太刀与三段伤害配置、图标存在", checks);
        AnimatorOverrideController animation = weapon.overrideController;
        Require(animation.runtimeAnimatorController == animator.runtimeAnimatorController, "太刀覆盖动画基于同一通用状态机", checks);
        List<KeyValuePair<AnimationClip, AnimationClip>> overrides = new List<KeyValuePair<AnimationClip, AnimationClip>>();
        animation.GetOverrides(overrides);
        foreach (var pair in overrides)
            Require(pair.Value != null && AssetDatabase.GetAssetPath(pair.Value).StartsWith(Folder + "/Animations/"), "独立动画 " + pair.Key.name, checks);
        for (int i = 1; i <= 3; i++)
        {
            AnimationClip clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/Animations/Lynette_Attack" + i + ".anim");
            Require(clip != null && clip.humanMotion, "源攻击 " + i + " 为 Humanoid 动画", checks);
        }
        File.WriteAllLines(Report + "/asset-validation.txt", checks);
    }

    static void Require(bool passed, string name, List<string> checks)
    {
        if (!passed) throw new InvalidOperationException("FAIL: " + name);
        checks.Add("PASS: " + name);
    }
}
