using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class ValidateBoundWarrior
{
    const string Pending = "ValidateBoundWarrior";
    const string Prefab = "Assets/Prefabs/Player/Warrior/Character Warrior.prefab";
    const string Root = "Assets/Prefabs/Player/Warrior/RootMotion/";
    static readonly List<string> report = new List<string>();
    static readonly MethodInfo Gravity = typeof(PlayerController).GetMethod("ApplyGravity", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly MethodInfo Refresh = typeof(PlayerController).GetMethod("RefreshItemAnimator", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly FieldInfo Selected = typeof(PlayerObj).GetField("<SelectedSlot>k__BackingField", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly FieldInfo SlotOwner = typeof(ItemSlot).GetField("owner", BindingFlags.Instance | BindingFlags.NonPublic);
    static ValidateBoundWarrior() { EditorApplication.update += Tick; }

    static void Check(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
        report.Add("PASS " + message);
    }

    public static void Run()
    {
        var asset = AssetDatabase.LoadAssetAtPath<GameObject>(Prefab);
        Check(asset && asset.GetComponent<PlayerController>(), "formal prefab has PlayerController");
        var animator = asset.GetComponent<Animator>();
        Check(animator.applyRootMotion && animator.cullingMode == AnimatorCullingMode.AlwaysAnimate,
            "formal root animator enables root motion and evaluates off camera");
        Check(AssetDatabase.GetAssetPath(animator.runtimeAnimatorController) == Root + "Controllers/Warrior_Base_RootMotion.controller",
            "formal prefab directly binds the root-motion base controller");
        foreach (string guid in AssetDatabase.FindAssets("t:AnimatorController", new[] { Root + "Controllers" }))
        {
            var runtime = AssetDatabase.LoadAssetAtPath<AnimatorController>(AssetDatabase.GUIDToAssetPath(guid));
            foreach (var state in runtime.layers[0].stateMachine.states.Select(s => s.state))
                Check(Mathf.Abs(state.speed - (state.name == "Walk" || state.name == "Run" ? 1.25f : 1)) < .0001f,
                    runtime.name + " state " + state.name + " speed=" + state.speed);
        }
        var third = AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + "Animations/weapon_001/3A_RootMotion.anim");
        Check(Mathf.Abs(third.length - 1.7f) < .001f, "sword third clip excludes the backward recovery segment");
        var events = AnimationUtility.GetAnimationEvents(third);
        Check(events.Length == 3 && Mathf.Abs(events[0].time - .4f) < .001f &&
            Mathf.Abs(events[1].time - .73333335f) < .001f && Mathf.Abs(events[2].time - 1.4f) < .001f,
            "trimmed sword third clip preserves all damage event times");
        var hit = AssetDatabase.LoadAssetAtPath<AnimationClip>(Root + "Animations/Common/Get_Hit_RootMotion.anim");
        Check(Mathf.Abs(hit.length - 17f / 30f) < .001f, "hit clip excludes its second half");
        SessionState.SetBool(Pending, true);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    static float Simulate(PlayerController controller, string state, float duration, float gravity = 0)
    {
        var animator = controller.animator;
        animator.fireEvents = false;
        animator.Rebind();
        animator.SetBool("IsWalking", state == "Walk" || state == "Run");
        animator.SetBool("IsRunning", state == "Run");
        animator.Play("Base Layer." + state, 0, 0);
        animator.Update(0);
        float distance = 0;
        for (int i = 0; i < Mathf.CeilToInt(duration * 120); i++)
        {
            Gravity.Invoke(controller, new object[] { Vector3.up * gravity, 1f / 120 });
            animator.Update(1f / 120);
            distance += new Vector2(animator.deltaPosition.x, animator.deltaPosition.z).magnitude;
        }
        return distance;
    }

    static void ResetPosition(GameObject actor)
    {
        actor.transform.position = Vector3.zero;
        actor.transform.rotation = Quaternion.identity;
        Physics.SyncTransforms();
    }

    static void Tick()
    {
        if (!EditorApplication.isPlaying || !SessionState.GetBool(Pending, false)) return;
        SessionState.SetBool(Pending, false);
        try
        {
            new GameObject("UIManager").AddComponent<UIManager>();
            var camera = new GameObject("Camera"); camera.tag = "MainCamera"; camera.AddComponent<Camera>();
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.transform.position = new Vector3(0, -.5f, 0);
            floor.transform.localScale = new Vector3(50, 1, 50);
            // The production scene supplies this mission override before Awake.
            var parent = new GameObject("Inactive spawn"); parent.SetActive(false);
            var actor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Prefab), parent.transform);
            var player = actor.GetComponent<PlayerObj>();
            player.t = AssetDatabase.LoadAssetAtPath<MissionSO>(AssetDatabase.GUIDToAssetPath("c863cb31e34bc504993698488f9e177b"));
            actor.transform.SetParent(null);
            var controller = actor.GetComponent<PlayerController>();
            Check(player.Data != null && controller.animator && controller.characterController, "formal player initializes with the scene mission override");
            controller.showDebugHUD = false;
            var slot = new GameObject("Test quick slot").AddComponent<ItemSlot>();
            SlotOwner.SetValue(slot, player.QuickBar);
            Selected.SetValue(player, slot);
            var controllers = new List<RuntimeAnimatorController> { controller.animator.runtimeAnimatorController };
            foreach (string id in new[] { "weapon_001", "weapon_002", "weapon_003", "weapon_004" })
            {
                var weapon = AssetDatabase.LoadAssetAtPath<WeaponSO>("Assets/Prefabs/Weapon/" + id + "/" + id + ".asset");
                var item = weapon.CreateData();
                player.Data.quickBar.Clear(); player.Data.quickBar.Add(item);
                Refresh.Invoke(controller, null);
                Check(controller.animator.runtimeAnimatorController == weapon.overrideController &&
                    AssetDatabase.GetAssetPath(weapon.overrideController) == Root + "Controllers/" + id + "_RootMotion.overrideController",
                    id + " selection directly uses its root-motion override through runtime ItemData");
                controllers.Add(controller.animator.runtimeAnimatorController);
            }
            player.Data.quickBar.Clear(); Refresh.Invoke(controller, null);
            Check(controller.animator.runtimeAnimatorController == controllers[0], "empty hand restores the root-motion default");
            Selected.SetValue(player, null);
            foreach (var runtime in controllers)
            {
                controller.animator.runtimeAnimatorController = runtime;
                ResetPosition(actor);
                float walk = Simulate(controller, "Walk", 1);
                Check(walk > 2.8f && walk < 3.5f && Mathf.Abs(actor.transform.position.z - walk) < .04f,
                    runtime.name + " walk moves CC exactly once: " + walk.ToString("F3") + " m");
                ResetPosition(actor);
                float run = Simulate(controller, "Run", 1);
                Check(run > 5.5f && run < 6.5f && Mathf.Abs(actor.transform.position.z - run) < .04f,
                    runtime.name + " run uses animation displacement: " + run.ToString("F3") + " m");
                ResetPosition(actor);
                Check(Simulate(controller, "Idle", 1) < .01f, runtime.name + " idle stays in place");
                for (int i = 1; i <= 3; i++)
                {
                    ResetPosition(actor);
                    float attack = Simulate(controller, "Attack_0" + i, 3.5f);
                    bool swordThird = runtime.name == "weapon_001_RootMotion" && i == 3;
                    Check(swordThird ? attack > 2 && attack < 3 : attack > .08f && attack < .9f,
                        runtime.name + " Attack_0" + i + " keeps forward root motion: " + attack.ToString("F3"));
                }
                ResetPosition(actor);
                var animator = controller.animator; animator.Rebind();
                animator.SetBool("IsWalking", false); animator.Play("Base Layer.Jump", 0, 0); animator.Update(0);
                float peak = 0;
                for (int i = 0; i < 240; i++)
                {
                    Gravity.Invoke(controller, new object[] { Vector3.down * 2, 1f / 120 });
                    animator.Update(1f / 120); peak = Mathf.Max(peak, actor.transform.position.y);
                }
                Check(peak > .3f && Mathf.Abs(actor.transform.position.y) < .12f,
                    runtime.name + " jump lifts the collider and lands, peak=" + peak.ToString("F3"));
                ResetPosition(actor); Simulate(controller, "Hit", 1.5f, -2);
                Check(actor.transform.position.z < -.8f && actor.transform.position.z > -1.4f &&
                    animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Idle"),
                    runtime.name + " hit recoil moves CC and recovery holds the new position: " + actor.transform.position.z.ToString("F3"));
                foreach (var clip in runtime.animationClips)
                    Check(AssetDatabase.GetAssetPath(clip).StartsWith(Root + "Animations/"), runtime.name + " uses copied clip " + clip.name);
            }
            var probe = actor.AddComponent<WarriorPolishEventProbe>();
            var hip = actor.GetComponentsInChildren<Transform>().First(t => t.name == "CATRigHub001");
            foreach (string next in new[] { "Idle", "Run", "Walk" })
            {
                ResetPosition(actor); probe.times.Clear();
                var animator = controller.animator;
                animator.runtimeAnimatorController = controllers[1]; animator.Rebind(); animator.fireEvents = true;
                animator.SetBool("IsWalking", false); animator.SetBool("IsRunning", false);
                animator.Play("Base Layer.Attack_03", 0, 0); animator.Update(0);
                float previous = hip.position.z, largestBackward = 0;
                for (int frame = 0; frame < 360; frame++)
                {
                    if (frame == 102 && next != "Idle")
                    { animator.SetBool("IsWalking", true); animator.SetBool("IsRunning", next == "Run"); }
                    Gravity.Invoke(controller, new object[] { Vector3.down * 2, 1f / 120 }); animator.Update(1f / 120);
                    if (frame >= 186) largestBackward = Mathf.Max(largestBackward, previous - hip.position.z);
                    previous = hip.position.z;
                }
                Check(next == "Idle" ? probe.times.Count == 3 && Mathf.Abs(probe.times[2] - 1.4f) < .001f :
                    probe.times.Count == 2 && Mathf.Abs(probe.times[1] - .73333335f) < .001f,
                    next == "Idle" ? "standing third attack publishes all three damage events" : "movement intentionally cancels the final swing into " + next);
                Check(largestBackward < .015f, "sword recovery has no backward rig snap, target=" + next + " max=" + largestBackward.ToString("F4"));
                Check(animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer." + next),
                    "third attack smoothly recovers directly into " + next);
            }
            controller.animator.fireEvents = false; controller.animator.runtimeAnimatorController = controllers[0]; ResetPosition(actor);
            controller.animator.Rebind(); controller.animator.Play("Base Layer.Hit", 0, 0); controller.animator.Update(0);
            float hitPrevious = hip.position.z, largestRebound = 0;
            for (int frame = 0; frame < 180; frame++)
            {
                Gravity.Invoke(controller, new object[] { Vector3.down * 2, 1f / 120 }); controller.animator.Update(1f / 120);
                if (frame >= 54) largestRebound = Mathf.Max(largestRebound, hip.position.z - hitPrevious);
                hitPrevious = hip.position.z;
            }
            Check(largestRebound < .015f && actor.transform.position.z < -.8f, "hit recovery does not spring forward to its original position, max=" + largestRebound.ToString("F4"));
            controller.animator.fireEvents = false;
            controller.animator.runtimeAnimatorController = controllers[0]; ResetPosition(actor);
            var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.transform.position = new Vector3(0, 2, 1.7f); wall.transform.localScale = new Vector3(8, 4, .2f);
            Physics.SyncTransforms(); Simulate(controller, "Walk", 1);
            Check(actor.transform.position.z > .5f && actor.transform.position.z < 1.25f, "CC blocks root motion at a wall");
            Object.DestroyImmediate(wall); ResetPosition(actor);
            controller.isTalking = true; Simulate(controller, "Walk", 1);
            Check(Mathf.Abs(actor.transform.position.z) < .001f, "dialogue blocks horizontal root motion"); controller.isTalking = false;
            int freeze = actor.GetComponent<CharacterTime>().Apply(ETimePriority.Highest, 0, 10);
            Simulate(controller, "Walk", 1);
            Check(Mathf.Abs(actor.transform.position.z) < .001f, "hit-stop freezes displacement"); actor.GetComponent<CharacterTime>().Cancel(freeze);
            Simulate(controller, "Idle", 1, -10);
            Check(Mathf.Abs(actor.transform.position.y) < .12f, "gravity respects ground collision");
            controller.Die(); Simulate(controller, "Walk", 1);
            Check(Mathf.Abs(actor.transform.position.z) < .001f, "death blocks horizontal displacement");
            report.Add("ALL PASSED: production prefab, actual weapon selection, walk/run/attacks/jump, collisions, dialogue, hit-stop and gravity.");
            File.WriteAllLines("warrior-bound-root-motion-validation.txt", report);
            EditorApplication.Exit(0);
        }
        catch (Exception e)
        {
            report.Add("FAIL " + e); File.WriteAllLines("warrior-bound-root-motion-validation.txt", report);
            Debug.LogException(e); EditorApplication.Exit(1);
        }
    }
}

public class WarriorPolishEventProbe : MonoBehaviour
{
    public readonly List<float> times = new List<float>();
    public void PublishPlayerCombo(AnimationEvent animationEvent) { times.Add(animationEvent.time); }
}
