using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class AttackDataPlayValidation
{
    const string Pending = "AttackDataPlayValidation";
    static List<string> report;
    static readonly List<GameObject> effects = new List<GameObject>();
    static double deadline;
    static bool awaitingCleanup;
    static AttackDataPlayValidation() { EditorApplication.update += Update; }
    public static void Run()
    {
        if (!AttackDataBindings.CheckAll()) { EditorApplication.Exit(1); return; }
        SessionState.SetBool(Pending, true);
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }
    static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); report.Add("PASS " + message); }
    static int Sources(AudioClip clip) => Object.FindObjectsByType<AudioSource>(FindObjectsSortMode.None).Count(a => a.clip == clip);
    static void Update()
    {
        if (!EditorApplication.isPlaying) return;
        if (SessionState.GetBool(Pending, false))
        {
            SessionState.SetBool(Pending, false);
            report = new List<string>();
            try
            {
                new GameObject("Listener").AddComponent<AudioListener>();
                var so = AssetDatabase.LoadAssetAtPath<WeaponSO>(AttackDataBindings.Root + "weapon_001/weapon_001.asset");
                var root = Object.Instantiate(so.prefab);
                var weapon = root.GetComponentInChildren<WeaponObj>(true);
                weapon.showDebug = false; weapon.targetLayer = 0;
                for (int i = 0; i < 3; i++)
                {
                    var attack = weapon.weaponData.combo[i];
                    int before = Sources(attack.audioClip), children = weapon.effPos.childCount;
                    weapon.CheckAttack(i);
                    var effect = weapon.effPos.GetChild(children).gameObject;
                    effects.Add(effect);
                    Assert(Sources(attack.audioClip) == before + 1 && weapon.effPos.childCount == children + 1 && effect.activeSelf && effect.transform.localPosition.sqrMagnitude < .00001f, "sword stage " + i + " emits exactly one sound/effect at its instance tip on damage detection, including a miss");
                }
                var fistSO = AssetDatabase.LoadAssetAtPath<WeaponSO>(AttackDataBindings.Root + "MossstoneFists/MossstoneFists.asset");
                var fists = Object.Instantiate(fistSO.prefab).GetComponentInChildren<WeaponObj>(true);
                fists.showDebug = false; fists.targetLayer = 0;
                int count = Sources(fists.weaponData.combo[0].audioClip);
                fists.CheckAttack(0);
                Assert(!fists.effPos && Sources(fists.weaponData.combo[0].audioClip) == count + 1, "unarmed attacks play audio without a tip anchor or effect");
                var owner = new GameObject("EnemyOwner"); owner.SetActive(false);
                var enemy = owner.AddComponent<EnemyObj>();
                var stranger = new GameObject("OtherEnemy"); stranger.SetActive(false);
                var otherEnemy = stranger.AddComponent<EnemyObj>();
                weapon.InitWeapon(owner); weapon.targetLayer = 0;
                int childCount = weapon.effPos.childCount;
                weapon.EnemyCheckAttack(otherEnemy, 0);
                Assert(weapon.effPos.childCount == childCount, "another enemy's attack cannot trigger this weapon's resources");
                weapon.EnemyCheckAttack(enemy, 0);
                effects.Add(weapon.effPos.GetChild(childCount).gameObject);
                Assert(weapon.effPos.childCount == childCount + 1, "the owning enemy's damage detection triggers its own effect");
                weapon.weaponData.combo[0].eff = null; weapon.weaponData.combo[0].audioClip = null;
                weapon.CheckAttack(0);
                Assert(weapon.effPos.childCount == childCount + 1, "empty attack resources are optional");
                awaitingCleanup = true; deadline = EditorApplication.timeSinceStartup + .65;
            }
            catch (Exception e) { Finish(e); }
        }
        if (awaitingCleanup && EditorApplication.timeSinceStartup >= deadline)
        {
            try { Assert(effects.All(e => !e), "all temporary attack effects are destroyed automatically"); Finish(null); }
            catch (Exception e) { Finish(e); }
        }
    }
    static void Finish(Exception error)
    {
        awaitingCleanup = false;
        report.Add(error == null ? "ALL PASSED: actual player/enemy damage-triggered effect and audio lifecycle." : "FAIL " + error);
        File.WriteAllLines("attack-data-play-validation.txt", report);
        if (error != null) Debug.LogException(error);
        EditorApplication.Exit(error == null ? 0 : 1);
    }
}
