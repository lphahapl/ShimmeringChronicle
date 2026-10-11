using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// Drives production input methods and Animator transitions, without Animator.Play.
[InitializeOnLoad]
public static class ValidateWarriorTransitions
{
    const string Pending = "ValidateWarriorTransitions";
    const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    static readonly List<string> report = new List<string>();
    static readonly MethodInfo Locomotion = typeof(PlayerController).GetMethod("UpdateLocomotion", Private);
    static readonly MethodInfo Combo = typeof(PlayerController).GetMethod("UpdateComboReset", Private);
    static readonly MethodInfo Refresh = typeof(PlayerController).GetMethod("RefreshItemAnimator", Private);
    static readonly FieldInfo State = typeof(PlayerController).GetField("stateInfo", Private);
    static int failures;
    static ValidateWarriorTransitions() { EditorApplication.update += Tick; }

    public static void Run()
    {
        SessionState.SetBool(Pending, true);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    static void Check(bool ok, string message)
    {
        report.Add((ok ? "PASS " : "FAIL ") + message);
        if (!ok) failures++;
    }

    static void Step(PlayerController player, int frames, Action<int> input = null)
    {
        for (int i = 0; i < frames; i++)
        {
            State.SetValue(player, player.animator.GetCurrentAnimatorStateInfo(0));
            Combo.Invoke(player, null);
            input?.Invoke(i);
            Locomotion.Invoke(player, null);
            player.animator.Update(1f / 120);
        }
    }

    static void Stable(PlayerController player, string expected, string label)
    {
        Check(!player.animator.IsInTransition(0) &&
            player.animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer." + expected),
            label + " settles in " + expected + ": " + player.CurrentStateName);
    }

    static void Reset(PlayerController player)
    {
        player.StopMove();
        typeof(PlayerController).GetMethod("ResetCombo", Private).Invoke(player, null);
        player.animator.Rebind(); player.animator.Update(0);
        Step(player, 30);
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
            floor.transform.position = Vector3.down * .5f; floor.transform.localScale = new Vector3(100, 1, 100);
            var parent = new GameObject("Inactive spawn"); parent.SetActive(false);
            var actor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(
                "Assets/Prefabs/Player/Warrior/Character Warrior.prefab"), parent.transform);
            var owner = actor.GetComponent<PlayerObj>();
            owner.t = AssetDatabase.LoadAssetAtPath<MissionSO>(AssetDatabase.GUIDToAssetPath("c863cb31e34bc504993698488f9e177b"));
            actor.transform.SetParent(null);
            var player = actor.GetComponent<PlayerController>(); player.showDebugHUD = false;
            player.animator.fireEvents = false;
            var slot = new GameObject("Quick slot").AddComponent<ItemSlot>();
            typeof(ItemSlot).GetField("owner", Private).SetValue(slot, owner.QuickBar);
            typeof(PlayerObj).GetField("<SelectedSlot>k__BackingField", Private).SetValue(owner, slot);
            foreach (string id in new[] { "weapon_001", "weapon_002", "weapon_003", "weapon_004" })
            {
                owner.Data.quickBar.Clear();
                owner.Data.quickBar.Add(AssetDatabase.LoadAssetAtPath<WeaponSO>(
                    "Assets/Prefabs/Weapon/" + id + "/" + id + ".asset").CreateData());
                Refresh.Invoke(player, null);
                Reset(player);
                player.SetMove(Vector3.forward, false); Step(player, 90); Stable(player, "Walk", id + " idle -> walk");
                player.SetMove(Vector3.forward, true); Step(player, 90); Stable(player, "Run", id + " walk -> run");
                player.SetMove(Vector3.forward, false); Step(player, 90); Stable(player, "Walk", id + " run -> walk");
                player.StopMove(); Step(player, 90); Stable(player, "Idle", id + " walk -> idle");
                player.SetMove(Vector3.forward, true); Step(player, 90); Stable(player, "Run", id + " idle -> run");
                player.StopMove(); Step(player, 90); Stable(player, "Idle", id + " run -> idle");
                Step(player, 72, frame => player.SetMove(frame % 8 < 4 ? Vector3.forward : Vector3.zero, frame % 16 < 8));
                player.StopMove(); Step(player, 90); Stable(player, "Idle", id + " rapid movement -> idle");

                Reset(player); player.Attack(); Step(player, 30); Stable(player, "Attack_01", id + " idle -> attack");
                Step(player, 480); Stable(player, "Idle", id + " single attack -> idle");
                Reset(player);
                var visited = new HashSet<string>();
                Step(player, 960, frame => {
                    string state = player.CurrentStateName;
                    foreach (string attack in new[] { "Attack_01", "Attack_02", "Attack_03" })
                        if (state == attack) visited.Add(attack);
                    if (visited.Count < 3) player.Attack();
                });
                Check(visited.Count == 3, id + " input reaches all three combo stages: " + string.Join(",", visited));
                Stable(player, "Idle", id + " combo -> idle");

                foreach (bool run in new[] { false, true })
                {
                    Reset(player);
                    Step(player, 960, frame => {
                        if (player.animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Attack_03"))
                            player.SetMove(Vector3.forward, run);
                        else if (player.CurrentStateName != "Walk" && player.CurrentStateName != "Run") player.Attack();
                    });
                    Stable(player, run ? "Run" : "Walk", id + " movement cancels third attack");
                }
                Reset(player); player.Jump(); Step(player, 360); Stable(player, "Idle", id + " jump -> idle");
                foreach (string destination in new[] { "Idle", "Walk", "Run" })
                {
                    Reset(player);
                    player.SetMove(destination == "Idle" ? Vector3.zero : Vector3.forward, destination == "Run");
                    Step(player, 90); player.animator.SetTrigger("Hit"); Step(player, 40);
                    Stable(player, "Hit", id + " trigger enters hit from " + destination);
                    Step(player, 150); Stable(player, destination, id + " hit recovery");
                }
            }
            report.Add("RESULT: " + failures + " failures; production input and automatic transition paths.");
            File.WriteAllLines("warrior-transition-validation.txt", report);
            EditorApplication.Exit(failures == 0 ? 0 : 1);
        }
        catch (Exception e)
        {
            report.Add("ERROR " + e); File.WriteAllLines("warrior-transition-validation.txt", report);
            Debug.LogException(e); EditorApplication.Exit(2);
        }
    }
}
