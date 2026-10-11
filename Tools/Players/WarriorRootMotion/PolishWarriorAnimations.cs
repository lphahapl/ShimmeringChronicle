using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object = UnityEngine.Object;

public static class PolishWarriorAnimations
{
    const string Root = "Assets/Prefabs/Player/Warrior/RootMotion/";
    static readonly List<string> report = new List<string>();

    static AnimationCurve Curve(float[] values, float dt, bool monotone)
    {
        var keys = new Keyframe[values.Length];
        for (int i = 0; i < values.Length; i++)
        {
            float before = i == 0 ? 0 : (values[i] - values[i - 1]) / dt;
            float after = i == values.Length - 1 ? 0 : (values[i + 1] - values[i]) / dt;
            float slope = (before + after) * .5f;
            if (monotone) slope = Mathf.Min(slope, 3 * Mathf.Min(before, after));
            keys[i] = new Keyframe(i * dt, values[i], slope, slope);
        }
        return new AnimationCurve(keys);
    }

    static void Cut(AnimationClip clip, float end)
    {
        if (AnimationUtility.GetAnimationEvents(clip).Any(e => e.time >= end))
            throw new Exception("Cut would remove an animation event: " + clip.name);
        foreach (var binding in AnimationUtility.GetCurveBindings(clip))
        {
            var curve = AnimationUtility.GetEditorCurve(clip, binding);
            var keys = curve.keys.Where(k => k.time < end - .00001f).ToList();
            float slope = (curve.Evaluate(end + .0001f) - curve.Evaluate(end - .0001f)) / .0002f;
            keys.Add(new Keyframe(end, curve.Evaluate(end), slope, 0));
            curve.keys = keys.ToArray(); AnimationUtility.SetEditorCurve(clip, binding, curve);
        }
        var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.stopTime = end;
        AnimationUtility.SetAnimationClipSettings(clip, settings);
    }

    static void RemoveHitRebound()
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + "Animations/Common/Get_Hit_RootMotion.anim");
        if (clip.length < 1) throw new Exception("Run once on the original hit copy.");
        const float end = 17f / 30f;
        var actor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Warrior/Character Warrior.prefab"));
        try
        {
            foreach (var animator in actor.GetComponentsInChildren<Animator>()) animator.enabled = false;
            actor.transform.position = Vector3.zero; actor.transform.rotation = Quaternion.identity;
            var hip = actor.GetComponentsInChildren<Transform>().First(t => t.name == "CATRigHub001");
            clip.SampleAnimation(actor, 0); float baseline = hip.position.z - actor.transform.position.z;
            int count = Mathf.RoundToInt(end * 120); float dt = end / count;
            var back = new float[count + 1]; var x = new float[count + 1]; var y = new float[count + 1]; var z = new float[count + 1];
            for (int i = 0; i <= count; i++)
            {
                actor.transform.position = Vector3.zero; clip.SampleAnimation(actor, i * dt);
                float offset = Mathf.Min(0, hip.position.z - actor.transform.position.z - baseline);
                back[i] = Mathf.Max(i == 0 ? 0 : back[i - 1], -offset);
                hip.position -= Vector3.forward * offset;
                x[i] = hip.localPosition.x; y[i] = hip.localPosition.y; z[i] = hip.localPosition.z;
            }
            Cut(clip, end);
            string path = AnimationUtility.CalculateTransformPath(hip, actor.transform);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalPosition.x"), Curve(x, dt, false));
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalPosition.y"), Curve(y, dt, false));
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalPosition.z"), Curve(z, dt, false));
            var recoil = Curve(back, dt, true); var keys = recoil.keys;
            for (int i = 0; i < keys.Length; i++) { keys[i].value = -keys[i].value; keys[i].inTangent = -keys[i].inTangent; keys[i].outTangent = -keys[i].outTangent; }
            recoil.keys = keys;
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.z"), recoil);
            clip.EnsureQuaternionContinuity(); EditorUtility.SetDirty(clip);
            report.Add("Hit 1.133 -> 0.567 s; recoil belongs to root so recovery does not spring to the original position, recoil=" + back[count].ToString("F3") + " m.");
        }
        finally { Object.DestroyImmediate(actor); }
    }

    static void RemoveSwordRetreat()
    {
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + "Animations/weapon_001/3A_RootMotion.anim");
        if (clip.length < 3) throw new Exception("Run once on the untrimmed third attack.");
        var events = AnimationUtility.GetAnimationEvents(clip);
        const float end = 1.7f;
        if (events.Any(e => e.time >= end)) throw new Exception("Cut would remove an animation event.");
        var actor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Warrior/Character Warrior.prefab"));
        try
        {
            foreach (var animator in actor.GetComponentsInChildren<Animator>()) animator.enabled = false;
            actor.transform.position = Vector3.zero; actor.transform.rotation = Quaternion.identity;
            var hip = actor.GetComponentsInChildren<Transform>().First(t => t.name == "CATRigHub001");
            clip.SampleAnimation(actor, 0);
            float baseline = hip.position.z - actor.transform.position.z;
            int count = Mathf.RoundToInt(end * 120); float dt = end / count;
            var rootZ = new float[count + 1]; var hipX = new float[count + 1];
            var hipY = new float[count + 1]; var hipZ = new float[count + 1];
            for (int i = 0; i <= count; i++)
            {
                actor.transform.position = Vector3.zero;
                clip.SampleAnimation(actor, i * dt);
                float offset = Mathf.Max(0, hip.position.z - actor.transform.position.z - baseline);
                rootZ[i] = Mathf.Max(i == 0 ? 0 : rootZ[i - 1], actor.transform.position.z + offset);
                // Transfer the rig's forward travel to the collision body's root.
                hip.position -= Vector3.forward * offset;
                hipX[i] = hip.localPosition.x; hipY[i] = hip.localPosition.y; hipZ[i] = hip.localPosition.z;
            }
            Cut(clip, end);
            string path = AnimationUtility.CalculateTransformPath(hip, actor.transform);
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalPosition.x"), Curve(hipX, dt, false));
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalPosition.y"), Curve(hipY, dt, false));
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve(path, typeof(Transform), "m_LocalPosition.z"), Curve(hipZ, dt, false));
            AnimationUtility.SetEditorCurve(clip, EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.z"), Curve(rootZ, dt, true));
            var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.stopTime = end;
            AnimationUtility.SetAnimationClipSettings(clip, settings); clip.EnsureQuaternionContinuity(); EditorUtility.SetDirty(clip);
            report.Add("Sword third attack 3.3 -> 1.7 s; damage events remain 0.4 / 0.73333335 / 1.4 s; forward travel now belongs to root, final=" + rootZ[count].ToString("F3") + " m.");
        }
        finally { Object.DestroyImmediate(actor); }
    }

    static void SmoothCycle(AnimationClip clip)
    {
        foreach (var binding in AnimationUtility.GetCurveBindings(clip))
        {
            if (binding.type != typeof(Transform) || binding.path == "") continue;
            var curve = AnimationUtility.GetEditorCurve(clip, binding); var keys = curve.keys;
            if (keys.Length < 2) continue;
            var first = keys[0]; var last = keys[keys.Length - 1];
            // The copies already share end/start poses; also match their seam velocity.
            if (Mathf.Abs(first.value - last.value) > .001f) continue;
            float slope = (first.outTangent + last.inTangent) * .5f;
            if (float.IsNaN(slope) || float.IsInfinity(slope)) continue;
            first.inTangent = first.outTangent = last.inTangent = last.outTangent = slope;
            keys[0] = first; keys[keys.Length - 1] = last;
            curve.keys = keys; AnimationUtility.SetEditorCurve(clip, binding, curve);
        }
        var rootBinding = EditorCurveBinding.FloatCurve("", typeof(Transform), "m_LocalPosition.z");
        var rootCurve = AnimationUtility.GetEditorCurve(clip, rootBinding);
        int count = Mathf.RoundToInt(clip.length * 120); float dt = clip.length / count;
        float distance = rootCurve.Evaluate(clip.length) - rootCurve.Evaluate(0);
        var velocities = new float[count];
        for (int i = 0; i < count; i++) velocities[i] = (rootCurve.Evaluate((i + 1) * dt) - rootCurve.Evaluate(i * dt)) / dt;
        var smooth = new float[count];
        for (int i = 0; i < count; i++)
            for (int j = -5; j <= 5; j++) smooth[i] += velocities[(i + j + count) % count] * (6 - Mathf.Abs(j)) / 36f;
        var keysRoot = new Keyframe[count + 1]; float z = rootCurve.Evaluate(0);
        float factor = distance / (smooth.Sum() * dt);
        for (int i = 0; i <= count; i++)
        {
            float speed = smooth[i % count] * factor;
            keysRoot[i] = new Keyframe(i * dt, z, speed, speed); z += speed * dt;
        }
        AnimationUtility.SetEditorCurve(clip, rootBinding, new AnimationCurve(keysRoot));
        clip.EnsureQuaternionContinuity(); EditorUtility.SetDirty(clip);
        report.Add("Smoothed loop seam and root velocity, stride unchanged: " + AssetDatabase.GetAssetPath(clip));
    }

    static IEnumerable<AnimatorState> States(AnimatorStateMachine machine)
    {
        foreach (var child in machine.states) yield return child.state;
        foreach (var child in machine.stateMachines)
            foreach (var state in States(child.stateMachine)) yield return state;
    }

    static void SetController(AnimatorController controller)
    {
        var states = States(controller.layers[0].stateMachine).ToDictionary(s => s.name);
        if (!states["Idle"].transitions.Any(t => t.destinationState == states["Run"]))
        {
            var transition = states["Idle"].AddTransition(states["Run"]);
            transition.AddCondition(AnimatorConditionMode.If, 0, "IsRunning");
        }
        if (!states["Run"].transitions.Any(t => t.destinationState == states["Idle"]))
        {
            var transition = states["Run"].AddTransition(states["Idle"]);
            transition.AddCondition(AnimatorConditionMode.IfNot, 0, "IsWalking");
        }
        foreach (var state in states.Values)
        {
            if (state.name == "Walk" || state.name == "Run") { state.speed = 1.25f; state.speedParameterActive = false; }
            foreach (var transition in state.transitions)
            {
                if (!transition.destinationState) continue;
                string target = transition.destinationState.name;
                if (new[] { "Idle", "Walk", "Run" }.Contains(state.name))
                {
                    transition.hasFixedDuration = true;
                    transition.duration = target.StartsWith("Attack_") ? .10f : .12f;
                    if (new[] { "Idle", "Walk", "Run" }.Contains(target))
                    {
                        transition.hasExitTime = false;
                        transition.interruptionSource = TransitionInterruptionSource.SourceThenDestination;
                        // Stop at the active transition in the interruption queue.
                        // Otherwise held movement restarts Idle -> Walk/Run every frame.
                        transition.orderedInterruption = true;
                    }
                }
                if (state.name.StartsWith("Attack_") && target.StartsWith("Attack_"))
                { transition.duration = .10f; transition.hasFixedDuration = true; transition.exitTime = state.name == "Attack_01" ? .65f : .72f; }
            }
            if (!state.name.StartsWith("Attack_")) continue;
            // The final swing can intentionally be cancelled by movement input.
            float exit = state.name == "Attack_03" ? .94f : state.name == "Attack_02" ? .85f : .80f;
            foreach (var transition in state.transitions.Where(t => t.destinationState && new[] { "Idle", "Walk", "Run" }.Contains(t.destinationState.name)).ToArray())
            { state.RemoveTransition(transition); Object.DestroyImmediate(transition, true); }
            var run = state.AddTransition(states["Run"]); run.AddCondition(AnimatorConditionMode.If, 0, "IsRunning");
            var walk = state.AddTransition(states["Walk"]); walk.AddCondition(AnimatorConditionMode.If, 0, "IsWalking"); walk.AddCondition(AnimatorConditionMode.IfNot, 0, "IsRunning");
            var idle = state.AddTransition(states["Idle"]);
            foreach (var transition in new[] { run, walk, idle })
            { transition.hasExitTime = true; transition.exitTime = exit; transition.hasFixedDuration = true; transition.duration = .16f; transition.canTransitionToSelf = false; }
            if (state.name == "Attack_03") { run.hasExitTime = false; walk.hasExitTime = false; }
            // The original/common controller already allowed running to cancel the earlier swings.
            if (controller.name == "Warrior_Base_RootMotion") run.hasExitTime = false;
            EditorUtility.SetDirty(state);
        }
        var hit = states["Hit"];
        foreach (var transition in hit.transitions.ToArray())
        { hit.RemoveTransition(transition); Object.DestroyImmediate(transition, true); }
        var hitRun = hit.AddTransition(states["Run"]); hitRun.AddCondition(AnimatorConditionMode.If, 0, "IsRunning");
        var hitWalk = hit.AddTransition(states["Walk"]); hitWalk.AddCondition(AnimatorConditionMode.If, 0, "IsWalking"); hitWalk.AddCondition(AnimatorConditionMode.IfNot, 0, "IsRunning");
        var hitIdle = hit.AddTransition(states["Idle"]);
        foreach (var transition in new[] { hitRun, hitWalk, hitIdle })
        { transition.hasExitTime = true; transition.exitTime = .85f; transition.hasFixedDuration = true; transition.duration = .14f; }
        // Inputs choose their direct destination instead of detouring through Walk.
        foreach (var state in new[] { states["Idle"], states["Walk"], states["Run"] })
            state.transitions = state.transitions.OrderBy(t => t.destinationState && t.destinationState.name.StartsWith("Attack_") ? 0 :
                t.destinationState && t.destinationState.name == "Jump" ? 1 :
                t.destinationState && t.destinationState.name == "Idle" ? 2 :
                t.destinationState && t.destinationState.name == "Run" ? 3 : 4).ToArray();
        EditorUtility.SetDirty(controller);
        report.Add("1.25x Walk/Run; 0.12 s movement blends; 0.10 s combo blends; 0.16 s recovery: " + controller.name);
    }

    public static void Run()
    {
        try
        {
            RemoveSwordRetreat();
            RemoveHitRebound();
            foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { Root + "Animations" }))
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(guid));
                if (clip.name == "Walk_RootMotion" || clip.name == "Run_RootMotion") SmoothCycle(clip);
            }
            foreach (string guid in AssetDatabase.FindAssets("t:AnimatorController", new[] { Root + "Controllers" }))
                SetController(AssetDatabase.LoadAssetAtPath<AnimatorController>(AssetDatabase.GUIDToAssetPath(guid)));
            AssetDatabase.SaveAssets(); File.WriteAllLines("warrior-animation-polish.txt", report); EditorApplication.Exit(0);
        }
        catch (Exception e) { report.Add("FAIL " + e); File.WriteAllLines("warrior-animation-polish.txt", report); Debug.LogException(e); EditorApplication.Exit(1); }
    }

    public static void RefreshControllers()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:AnimatorController", new[] { Root + "Controllers" }))
            SetController(AssetDatabase.LoadAssetAtPath<AnimatorController>(AssetDatabase.GUIDToAssetPath(guid)));
        AssetDatabase.SaveAssets(); File.AppendAllLines("warrior-animation-polish.txt", report); EditorApplication.Exit(0);
    }

    public static void FinishHitAndCancellation()
    {
        RemoveHitRebound();
        foreach (string guid in AssetDatabase.FindAssets("t:AnimatorController", new[] { Root + "Controllers" }))
            SetController(AssetDatabase.LoadAssetAtPath<AnimatorController>(AssetDatabase.GUIDToAssetPath(guid)));
        AssetDatabase.SaveAssets(); File.AppendAllLines("warrior-animation-polish.txt", report); EditorApplication.Exit(0);
    }
}
