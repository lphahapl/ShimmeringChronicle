using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class ValidateWarriorRootMotion
{
    static readonly List<string> report = new List<string>();
    static float Measure(RuntimeAnimatorController controller, string state, float duration, float speed = 1)
    {
        var actor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(BuildWarriorRootMotion.PrefabPath));
        try
        {
            foreach (var behaviour in actor.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(behaviour);
            actor.transform.position = Vector3.zero; actor.transform.rotation = Quaternion.identity;
            var animator = actor.GetComponent<Animator>(); animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = true; animator.fireEvents = false; animator.Rebind();
            animator.SetBool("IsWalking", state == "Walk" || state == "Run"); animator.SetBool("IsRunning", state == "Run");
            if (state == "Dead") animator.SetBool("IsDead", true);
            animator.Play("Base Layer." + state, 0, 0); animator.Update(0); animator.speed = speed;
            float distance = 0;
            for (int i = 0; i < Mathf.CeilToInt(duration * 120); i++)
            {
                animator.Update(1f / 120);
                distance += new Vector2(animator.deltaPosition.x, animator.deltaPosition.z).magnitude;
            }
            return distance;
        }
        finally { Object.DestroyImmediate(actor); }
    }
    static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); report.Add("PASS " + message); }
    static void Jump(RuntimeAnimatorController controller)
    {
        var actor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(BuildWarriorRootMotion.PrefabPath));
        try
        {
            foreach (var behaviour in actor.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(behaviour);
            var animator = actor.GetComponent<Animator>(); animator.runtimeAnimatorController = controller;
            animator.applyRootMotion = true; animator.fireEvents = false; animator.Rebind();
            animator.Play("Base Layer.Jump", 0, 0); animator.Update(0);
            float height = 0, peak = 0;
            for (int i = 0; i < 240; i++) { animator.Update(1f / 120); height += animator.deltaPosition.y; peak = Mathf.Max(peak, height); }
            Assert(peak > .3f && Mathf.Abs(height) < .1f, controller.name + " jump lifts/lands the root: peak=" + peak.ToString("F3") + " final=" + height.ToString("F3") + " m");
        }
        finally { Object.DestroyImmediate(actor); }
    }
    public static void Run()
    {
        try
        {
            BuildWarriorRootMotion.RepairFirstSwordAttack();
            BuildWarriorRootMotion.RepairJump();
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BuildWarriorRootMotion.PrefabPath);
            var root = prefab.GetComponent<WarriorRootMotionController>();
            Assert(root && prefab.GetComponent<PlayerController>() == root && root.animator.applyRootMotion, "new prefab retains PlayerController compatibility and enables root motion");
            var original = AssetDatabase.LoadAssetAtPath<GameObject>(BuildWarriorRootMotion.OriginalPrefab);
            Assert(!original.GetComponent<WarriorRootMotionController>(), "original prefab continues to use its original movement controller");
            foreach (var mapping in root.controllers)
            {
                string id = mapping.source.name;
                float walk = Measure(mapping.rootMotion, "Walk", 1), run = Measure(mapping.rootMotion, "Run", 1);
                Assert(walk > 1 && walk < 4 && run > walk && run < 7, id + " actual Animator root motion Walk=" + walk.ToString("F3") + " m/s Run=" + run.ToString("F3") + " m/s");
                float idle = Measure(mapping.rootMotion, "Idle", 2);
                Assert(idle < .01f, id + " idle stays in place");
                Jump(mapping.rootMotion);
                for (int i = 1; i <= 3; i++)
                {
                    float advance = Measure(mapping.rootMotion, "Attack_0" + i, 3.5f);
                    Assert(advance > .08f && advance < .9f, id + " Attack_0" + i + " advance=" + advance.ToString("F3") + " m without backward reset");
                }
                float slow = Measure(mapping.rootMotion, "Walk", 1, .1f);
                Assert(slow > 0 && slow < walk * .2f, id + " slowing Animator also slows displacement");
                Assert(Measure(mapping.rootMotion, "Walk", 1, 0) < .001f, id + " frozen Animator has no horizontal motion");
                foreach (var clip in mapping.rootMotion.animationClips)
                    Assert(AssetDatabase.GetAssetPath(clip).StartsWith(BuildWarriorRootMotion.Folder + "/"), id + " uses copied clip " + clip.name);
            }
            report.Add("ALL PASSED: copied clips/controllers, root motion, idle, attacks, slow/frozen movement.");
            File.WriteAllLines("warrior-root-motion-validation.txt", report); EditorApplication.Exit(0);
        }
        catch (Exception e) { report.Add("FAIL " + e); File.WriteAllLines("warrior-root-motion-validation.txt", report); Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
