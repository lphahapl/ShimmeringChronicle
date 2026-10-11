using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

public static class BuildWarriorRootMotion
{
    public const string Folder = "Assets/Prefabs/Player/Warrior/RootMotion";
    public const string OriginalPrefab = "Assets/Prefabs/Player/Warrior/Character Warrior.prefab";
    public const string PrefabPath = Folder + "/WarriorRootMotionPlayer.prefab";
    public static readonly string[] Sources = {
        "Assets/Prefabs/Player/Warrior/Animations/Character Warrior.controller",
        "Assets/Prefabs/Weapon/weapon_001/Animations/sword.overrideController",
        "Assets/Prefabs/Weapon/weapon_002/Animations/Warrior/weapon_002.overrideController",
        "Assets/Prefabs/Weapon/weapon_003/Animations/Warrior/weapon_003.overrideController",
        "Assets/Prefabs/Weapon/weapon_004/Animations/Warrior/weapon_004.overrideController"
    };
    static readonly Dictionary<AnimationClip, AnimationClip> clips = new Dictionary<AnimationClip, AnimationClip>();
    static readonly Dictionary<RuntimeAnimatorController, RuntimeAnimatorController> controllers = new Dictionary<RuntimeAnimatorController, RuntimeAnimatorController>();
    static readonly Dictionary<AnimationClip, string> states = new Dictionary<AnimationClip, string>();
    static readonly List<string> report = new List<string>();
    static GameObject actor;
    static Transform leftFoot, rightFoot;

    static void FolderAt(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/'); FolderAt(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }

    static AnimatorController Base(RuntimeAnimatorController controller)
    {
        while (controller is AnimatorOverrideController overrides) controller = overrides.runtimeAnimatorController;
        return (AnimatorController)controller;
    }

    static void RegisterStates(RuntimeAnimatorController controller)
    {
        var overrides = new Dictionary<AnimationClip, AnimationClip>();
        if (controller is AnimatorOverrideController aoc)
        {
            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>(); aoc.GetOverrides(pairs);
            foreach (var pair in pairs) if (pair.Value) overrides[pair.Key] = pair.Value;
        }
        foreach (var state in Base(controller).layers[0].stateMachine.states)
        {
            if (!(state.state.motion is AnimationClip clip)) continue;
            if (overrides.TryGetValue(clip, out var replacement)) clip = replacement;
            states[clip] = state.state.name;
        }
    }

    static string Group(AnimationClip clip)
    {
        string path = AssetDatabase.GetAssetPath(clip);
        foreach (string id in new[] { "weapon_001", "weapon_002", "weapon_003", "weapon_004" })
            if (path.Contains("/" + id + "/")) return id;
        return "Common";
    }

    static void CloseLoop(AnimationClip clip)
    {
        foreach (var binding in AnimationUtility.GetCurveBindings(clip))
        {
            if (binding.type != typeof(Transform) || binding.path == "") continue;
            var curve = AnimationUtility.GetEditorCurve(clip, binding); var keys = curve.keys;
            if (keys.Length < 2) continue;
            // The source's last Run frame contains a rig translation jump. Close only the copy.
            keys[keys.Length - 1].value = keys[0].value;
            curve.keys = keys; AnimationUtility.SetEditorCurve(clip, binding, curve);
        }
        clip.EnsureQuaternionContinuity();
    }

    static AnimationCurve Locomotion(AnimationClip clip)
    {
        int count = Mathf.CeilToInt(clip.length * 120);
        float dt = clip.length / count;
        var left = new Vector3[count + 1]; var right = new Vector3[count + 1];
        for (int i = 0; i <= count; i++)
        {
            actor.transform.position = Vector3.zero; actor.transform.rotation = Quaternion.identity;
            clip.SampleAnimation(actor, i == count ? 0 : i * dt);
            left[i] = leftFoot.position; right[i] = rightFoot.position;
        }
        float minLeft = left.Min(v => v.y), minRight = right.Min(v => v.y);
        var speed = new float[count];
        for (int i = 0; i < count; i++)
        {
            var previousLeft = left[i]; var previousRight = right[i];
            var nextLeft = left[i + 1]; var nextRight = right[i + 1];
            bool l = previousLeft.y < minLeft + .065f && nextLeft.z < previousLeft.z;
            bool r = previousRight.y < minRight + .065f && nextRight.z < previousRight.z;
            if (l && (!r || previousLeft.y - minLeft < previousRight.y - minRight)) speed[i] = (previousLeft.z - nextLeft.z) / dt;
            else if (r) speed[i] = (previousRight.z - nextRight.z) / dt;
        }
        var valid = speed.Where(v => v > .1f && v < 12f).ToArray();
        if (valid.Length == 0) throw new Exception("No stance samples: " + clip.name);
        float average = valid.Average();
        for (int i = 0; i < count; i++) if (speed[i] < .1f || speed[i] > 12f) speed[i] = average;
        var smooth = new float[count];
        for (int i = 0; i < count; i++)
            for (int k = -3; k <= 3; k++) smooth[i] += speed[(i + k + count) % count] / 7f;
        var keys = new Keyframe[count + 1]; float distance = 0;
        for (int i = 0; i <= count; i++)
        {
            float velocity = smooth[i % count]; keys[i] = new Keyframe(i * dt, distance, velocity, velocity);
            distance += velocity * dt;
        }
        report.Add(clip.name + " stance-matched " + (keys[count].value / clip.length).ToString("F3") + " m/s, cycle=" + keys[count].value.ToString("F3") + " m");
        return new AnimationCurve(keys);
    }

    static AnimationCurve Attack(AnimationClip clip, string state, string group)
    {
        int index = int.Parse(state.Substring(state.Length - 2)) - 1;
        float[] distances = group == "weapon_001" ? new[] { .25f, .40f, .60f } :
            group == "weapon_004" ? new[] { .20f, .24f, .40f } :
            group == "weapon_003" ? new[] { .26f, .30f, .42f } : new[] { .22f, .26f, .36f };
        var hits = AnimationUtility.GetAnimationEvents(clip).Where(e => e.functionName == "PublishPlayerCombo").Select(e => e.time).ToArray();
        if (hits.Length == 0) hits = new[] { clip.length * .42f };
        float perHit = distances[index] / hits.Length;
        int count = Mathf.CeilToInt(clip.length * 120); var keys = new Keyframe[count + 1];
        for (int i = 0; i <= count; i++)
        {
            float t = clip.length * i / count, z = 0, velocity = 0;
            foreach (float hit in hits)
            {
                float start = Mathf.Max(0, hit - .18f), end = Mathf.Min(clip.length, hit + .12f);
                float u = Mathf.Clamp01((t - start) / (end - start));
                z += perHit * u * u * (3 - 2 * u);
                if (u > 0 && u < 1) velocity += perHit * 6 * u * (1 - u) / (end - start);
            }
            keys[i] = new Keyframe(t, z, velocity, velocity);
        }
        report.Add(group + " " + state + " advance=" + distances[index] + " m; hits=" + hits.Length);
        return new AnimationCurve(keys);
    }

    static AnimationClip CopyClip(AnimationClip source)
    {
        if (clips.TryGetValue(source, out var prior)) return prior;
        string group = Group(source), folder = Folder + "/Animations/" + group; FolderAt(folder);
        var copy = Object.Instantiate(source); copy.name = source.name + "_RootMotion"; copy.hideFlags = HideFlags.None;
        string path = folder + "/" + copy.name + ".anim";
        if (AssetDatabase.LoadAssetAtPath<AnimationClip>(path)) throw new Exception("Output already exists: " + path);
        bool looping = states.TryGetValue(source, out string state) && (state == "Walk" || state == "Run");
        if (group == "weapon_001" && (source.name == "1A" || source.name == "2A" || source.name == "3A"))
            state = "Attack_0" + source.name[0];
        if (looping) CloseLoop(copy);
        var z = looping ? Locomotion(copy) : state != null && state.StartsWith("Attack_") ? Attack(copy, state, group) : AnimationCurve.Constant(0, copy.length, 0);
        foreach (char axis in "xyz")
            AnimationUtility.SetEditorCurve(copy, EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition." + axis), axis == 'z' ? z : AnimationCurve.Constant(0, copy.length, 0));
        var settings = AnimationUtility.GetAnimationClipSettings(copy);
        settings.loopTime = state == "Idle" || looping || settings.loopTime;
        settings.keepOriginalPositionXZ = true; settings.keepOriginalPositionY = true; settings.keepOriginalOrientation = true;
        settings.loopBlendPositionXZ = false; settings.loopBlendPositionY = true;
        AnimationUtility.SetAnimationClipSettings(copy, settings);
        AssetDatabase.CreateAsset(copy, path); clips[source] = copy;
        if (state == "Jump") AddJumpRoot(copy);
        return copy;
    }

    static void AddJumpRoot(AnimationClip clip)
    {
        var hip = actor.GetComponentsInChildren<Transform>().First(t => t.name == "CATRigHub001");
        string hipPath = AnimationUtility.CalculateTransformPath(hip, actor.transform);
        int count = Mathf.CeilToInt(clip.length * 120);
        var pose = new Vector3[count + 1]; var offsets = new Vector3[count + 1]; var height = new float[count + 1];
        float initial = 0;
        for (int i = 0; i <= count; i++)
        {
            actor.transform.position = Vector3.zero; actor.transform.rotation = Quaternion.identity;
            clip.SampleAnimation(actor, clip.length * i / count);
            if (i == 0) initial = hip.position.y;
            height[i] = Mathf.Max(0, hip.position.y - initial);
            if (i == count) height[i] = 0;
            offsets[i] = hip.parent.InverseTransformVector(Vector3.up * height[i]);
            pose[i] = hip.localPosition;
        }
        for (int axis = 0; axis < 3; axis++)
        {
            var keys = new Keyframe[count + 1];
            for (int i = 0; i <= count; i++) keys[i] = new Keyframe(clip.length * i / count, pose[i][axis] - offsets[i][axis]);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(hipPath, typeof(Transform), "m_LocalPosition." + "xyz"[axis]), new AnimationCurve(keys));
        }
        var vertical = new Keyframe[count + 1];
        for (int i = 0; i <= count; i++) vertical[i] = new Keyframe(clip.length * i / count, height[i]);
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.y"), new AnimationCurve(vertical));
        EditorUtility.SetDirty(clip);
        report.Add("Jump extracts " + height.Max().ToString("F3") + " m vertical root motion; rig counter-offset preserves the original visible pose.");
    }

    static RuntimeAnimatorController CopyController(RuntimeAnimatorController source)
    {
        if (controllers.TryGetValue(source, out var prior)) return prior;
        FolderAt(Folder + "/Controllers");
        string originalPath = AssetDatabase.GetAssetPath(source);
        string name = source is AnimatorOverrideController ? "weapon_" + (source == AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(Sources[1]) ? "001" : originalPath.Contains("weapon_002") ? "002" : originalPath.Contains("weapon_003") ? "003" : "004") :
            originalPath.Contains("weapon_002") ? "weapon_002_Base" : originalPath.Contains("weapon_003") ? "weapon_003_Base" : originalPath.Contains("weapon_004") ? "weapon_004_Base" : "Warrior_Base";
        string target = Folder + "/Controllers/" + name + "_RootMotion" + Path.GetExtension(originalPath);
        if (!AssetDatabase.CopyAsset(originalPath, target)) throw new Exception("Cannot copy controller: " + target);
        var result = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(target); controllers[source] = result;
        if (source is AnimatorOverrideController originalOverrides)
        {
            var aoc = (AnimatorOverrideController)result;
            aoc.runtimeAnimatorController = CopyController(originalOverrides.runtimeAnimatorController);
            var pairs = new List<KeyValuePair<AnimationClip, AnimationClip>>(); originalOverrides.GetOverrides(pairs);
            aoc.ApplyOverrides(pairs.Select(p => new KeyValuePair<AnimationClip, AnimationClip>(CopyClip(p.Key), p.Value ? CopyClip(p.Value) : null)).ToList());
            EditorUtility.SetDirty(aoc);
        }
        else
        {
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(target))
            {
                var serialized = new SerializedObject(asset); var property = serialized.GetIterator();
                while (property.Next(true))
                    if (property.propertyType == SerializedPropertyType.ObjectReference && property.objectReferenceValue is AnimationClip clip)
                        property.objectReferenceValue = CopyClip(clip);
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
        }
        return result;
    }

    public static void Run()
    {
        try
        {
            actor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(OriginalPrefab));
            foreach (var behaviour in actor.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(behaviour);
            actor.GetComponent<Animator>().enabled = false;
            actor.transform.position = Vector3.zero; actor.transform.rotation = Quaternion.identity;
            var bones = actor.GetComponentsInChildren<Transform>();
            leftFoot = bones.First(t => t.name == "CATRigLLegAnkle"); rightFoot = bones.First(t => t.name == "CATRigRLegAnkle");
            foreach (string path in Sources) RegisterStates(AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(path));
            var mappings = Sources.Select(path => {
                var source = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(path);
                return new WarriorRootMotionController.ControllerMapping { source = source, rootMotion = CopyController(source) };
            }).ToArray();
            if (!AssetDatabase.CopyAsset(OriginalPrefab, PrefabPath)) throw new Exception("Cannot copy Warrior prefab");
            var contents = PrefabUtility.LoadPrefabContents(PrefabPath);
            try
            {
                var original = contents.GetComponent<PlayerController>();
                var controller = contents.AddComponent<WarriorRootMotionController>();
                EditorUtility.CopySerializedManagedFieldsOnly(original, controller);
                Object.DestroyImmediate(original);
                controller.controllers = mappings;
                controller.animator = contents.GetComponent<Animator>(); controller.characterController = contents.GetComponent<CharacterController>();
                controller.animator.runtimeAnimatorController = mappings[0].rootMotion;
                controller.animator.applyRootMotion = true; controller.animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
                // The current scene supplies this mission reference outside the original prefab.
                var player = contents.GetComponent<PlayerObj>();
                if (!player.t) player.t = AssetDatabase.LoadAssetAtPath<MissionSO>("Assets/Configs/missionSO/击杀敌人.asset");
                contents.name = "WarriorRootMotionPlayer"; contents.transform.localPosition = Vector3.zero;
                PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(contents); Object.DestroyImmediate(actor); }
            AssetDatabase.SaveAssets();
            report.Add("PASS copied " + clips.Count + " clips, " + controllers.Count + " controllers, one independent Warrior prefab; originals read only.");
            File.WriteAllLines("warrior-root-motion-build.txt", report);
            EditorApplication.Exit(0);
        }
        catch (Exception e) { report.Add("FAIL " + e); File.WriteAllLines("warrior-root-motion-build.txt", report); Debug.LogException(e); EditorApplication.Exit(1); }
    }

    public static void RepairFirstSwordAttack()
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/Animations/weapon_001/1A_RootMotion.anim");
        AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.z"), Attack(clip, "Attack_01", "weapon_001"));
        EditorUtility.SetDirty(clip); AssetDatabase.SaveAssets();
    }

    public static void BindSceneMission()
    {
        var contents = PrefabUtility.LoadPrefabContents(PrefabPath);
        try
        {
            var player = contents.GetComponent<PlayerObj>();
            if (!player.t) player.t = AssetDatabase.LoadAssetAtPath<MissionSO>("Assets/Configs/missionSO/击杀敌人.asset");
            if (!player.t) throw new Exception("Current PlayerObj requires its scene mission reference");
            PrefabUtility.SaveAsPrefabAsset(contents, PrefabPath);
        }
        finally { PrefabUtility.UnloadPrefabContents(contents); }
    }

    public static void RepairJump()
    {
        actor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(OriginalPrefab));
        try
        {
            foreach (var behaviour in actor.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(behaviour);
            actor.GetComponent<Animator>().enabled = false;
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder + "/Animations/Common/Jump_RootMotion.anim");
            var source = AssetDatabase.LoadAllAssetsAtPath("Assets/Prefabs/Player/Warrior/Animations/Warrior@Jump.FBX").OfType<AnimationClip>().First(c => !c.name.StartsWith("__"));
            string name = clip.name; EditorUtility.CopySerialized(source, clip); clip.name = name;
            foreach (char axis in "xyz") AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition." + axis), AnimationCurve.Constant(0, clip.length, 0));
            AddJumpRoot(clip);
            var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.keepOriginalPositionY = true; settings.keepOriginalPositionXZ = true; settings.keepOriginalOrientation = true;
            AnimationUtility.SetAnimationClipSettings(clip, settings);
            AssetDatabase.SaveAssets();
            File.WriteAllLines("warrior-jump-root-motion.txt", report);
        }
        finally { Object.DestroyImmediate(actor); }
    }
}
