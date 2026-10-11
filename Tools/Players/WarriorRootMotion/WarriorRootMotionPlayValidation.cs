using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class WarriorRootMotionPlayValidation
{
    const string Pending = "WarriorRootMotionPlayValidation";
    static readonly MethodInfo Move = typeof(PlayerController).GetMethod("MoveCharacter", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly MethodInfo Resolve = typeof(WarriorRootMotionController).GetMethod("ResolveAnimatorController", BindingFlags.Instance | BindingFlags.NonPublic);
    static List<string> report;
    static WarriorRootMotionPlayValidation() { EditorApplication.update += Update; }
    public static void Run()
    {
        BuildWarriorRootMotion.BindSceneMission();
        SessionState.SetBool(Pending, true);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }
    static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); report.Add("PASS " + message); }
    static float Walk(WarriorRootMotionController controller)
    {
        var animator = controller.animator;
        animator.fireEvents = false; animator.Rebind();
        animator.SetBool("IsWalking", true); animator.SetBool("IsRunning", false);
        animator.Play("Base Layer.Walk", 0, 0); animator.Update(0);
        float distance = 0;
        for (int i = 0; i < 60; i++)
        {
            Move.Invoke(controller, new object[] { Vector3.forward * 100, 1f / 60 });
            animator.Update(1f / 60);
            distance += new Vector2(animator.deltaPosition.x, animator.deltaPosition.z).magnitude;
        }
        return distance;
    }
    static void Update()
    {
        if (!EditorApplication.isPlaying || !SessionState.GetBool(Pending, false)) return;
        SessionState.SetBool(Pending, false); report = new List<string>();
        try
        {
            new GameObject("UIManager").AddComponent<UIManager>();
            var camera = new GameObject("Camera"); camera.tag = "MainCamera"; camera.AddComponent<Camera>();
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube); floor.transform.position = new Vector3(0, -.5f, 0); floor.transform.localScale = new Vector3(50, 1, 50);
            var actor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(BuildWarriorRootMotion.PrefabPath));
            var root = actor.GetComponent<WarriorRootMotionController>();
            Assert(actor.GetComponent<PlayerObj>().Data != null && root.animator && root.characterController && root.CurrentStateName != "(无 Animator)", "copied player initializes inherited controller, player data, Animator and CharacterController");
            foreach (var mapping in root.controllers)
                Assert(Resolve.Invoke(root, new object[] { mapping.source }) == mapping.rootMotion, "weapon switching resolves " + mapping.source.name + " to its copied controller");
            actor.transform.position = Vector3.zero; Physics.SyncTransforms();
            Vector3 start = actor.transform.position;
            Move.Invoke(root, new object[] { Vector3.forward * 100, 1f });
            Assert(Vector3.Distance(actor.transform.position, start) < .001f, "root-motion player ignores the legacy horizontal velocity, avoiding double movement");
            float animationDistance = Walk(root), actualDistance = (actor.transform.position - start).z;
            Assert(Mathf.Abs(actualDistance - animationDistance) < .03f && actualDistance > 2, "OnAnimatorMove applies the root delta once via CharacterController: actual=" + actualDistance.ToString("F3") + " animation=" + animationDistance.ToString("F3") + " m");
            actor.transform.position = Vector3.zero;
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube); wall.transform.position = new Vector3(0, 2, 1.7f); wall.transform.localScale = new Vector3(8, 4, .2f); Physics.SyncTransforms();
            Walk(root);
            Assert(actor.transform.position.z < 1.25f && actor.transform.position.z > .5f, "root-motion player stops at colliders instead of crossing the wall");
            Object.DestroyImmediate(wall); actor.transform.position = Vector3.zero; Physics.SyncTransforms();
            root.isTalking = true; Walk(root);
            Assert(Mathf.Abs(actor.transform.position.z) < .001f, "dialogue blocks root displacement"); root.isTalking = false;
            int freeze = actor.GetComponent<CharacterTime>().Apply(ETimePriority.Highest, 0, 10);
            Walk(root);
            Assert(Mathf.Abs(actor.transform.position.z) < .001f, "CharacterTime freeze also stops root displacement"); actor.GetComponent<CharacterTime>().Cancel(freeze);
            Move.Invoke(root, new object[] { Vector3.down * 10, .1f }); root.animator.Update(1f / 60);
            Assert(actor.transform.position.y > -.12f && actor.transform.position.y < .12f, "gravity is still handled through CharacterController ground collision");
            actor.transform.position = Vector3.zero; root.animator.Rebind(); root.animator.SetBool("IsWalking", false); root.animator.Play("Base Layer.Jump", 0, 0); root.animator.Update(0);
            float peak = 0;
            for (int i = 0; i < 120; i++)
            {
                Move.Invoke(root, new object[] { Vector3.down * 2, 1f / 60 }); root.animator.Update(1f / 60);
                peak = Mathf.Max(peak, actor.transform.position.y);
            }
            Assert(peak > .3f && actor.transform.position.y > -.12f && actor.transform.position.y < .12f, "jump root moves the collision body upward and lands on the ground: peak=" + peak.ToString("F3") + " m");
            report.Add("ALL PASSED: actual Play Mode initialization, mapping, root-motion application, collision, dialogue, hit-stop and gravity.");
            File.WriteAllLines("warrior-root-motion-play-validation.txt", report); EditorApplication.Exit(0);
        }
        catch (Exception e) { report.Add("FAIL " + e); File.WriteAllLines("warrior-root-motion-play-validation.txt", report); Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
