using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

// One-time resource migration. Combat uses AttackData; effPos is only an anchor.
public static class AttackDataBindings
{
    public const string Root = "Assets/Prefabs/Weapon/";
    public static readonly string[] Ids = { "weapon_001", "weapon_002", "weapon_003", "weapon_004", "EnemyWeapon_001", "MossstoneBlade", "ReinforcedBlade", "FalconOath", "MossstoneFists", "ReinforcedFists" };

    public static void BindAll()
    {
        foreach (string id in Ids)
        {
            string folder = Root + id + "/";
            var effect = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "Effects/AttackEffect.prefab");
            if (effect)
            {
                var contents = PrefabUtility.LoadPrefabContents(folder + "Effects/AttackEffect.prefab");
                try
                {
                    foreach (var source in contents.GetComponentsInChildren<AudioSource>(true)) Object.DestroyImmediate(source);
                    PrefabUtility.SaveAsPrefabAsset(contents, folder + "Effects/AttackEffect.prefab");
                }
                finally { PrefabUtility.UnloadPrefabContents(contents); }
                effect = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "Effects/AttackEffect.prefab");
            }

            var so = AssetDatabase.LoadAssetAtPath<WeaponSO>(folder + id + ".asset");
            if (so)
            {
                for (int i = 0; i < so.combo.Count; i++)
                {
                    string audioPath = folder + "Audio/" + (id == "weapon_001" ? "SwordSlash_0" + (i + 1) + ".wav" : effect ? "attack_swing.wav" : "unarmed_swing.wav");
                    var clip = AssetDatabase.LoadAssetAtPath<AudioClip>(audioPath);
                    if (!clip) throw new Exception("Missing attack audio: " + audioPath);
                    so.combo[i].audioClip = clip;
                    so.combo[i].eff = effect;
                }
                EditorUtility.SetDirty(so);
            }

            string prefabPath = folder + id + ".prefab";
            var root = PrefabUtility.LoadPrefabContents(prefabPath);
            try
            {
                var tip = root.transform.Find("TipPoint");
                var embedded = tip ? tip.Find("AttackEffect") : null;
                if (embedded) Object.DestroyImmediate(embedded.gameObject);
                foreach (var source in root.GetComponentsInChildren<AudioSource>(true)) Object.DestroyImmediate(source);
                foreach (var weapon in root.GetComponentsInChildren<WeaponObj>(true)) weapon.effPos = tip;
                PrefabUtility.SaveAsPrefabAsset(root, prefabPath);
            }
            finally { PrefabUtility.UnloadPrefabContents(root); }
        }

        foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { Root.TrimEnd('/') }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!path.EndsWith(".anim")) continue;
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            var events = AnimationUtility.GetAnimationEvents(clip);
            var retained = events.Where(e => e.functionName != "PlayHandingEffORAudioClip" && e.functionName != "EndEff" && e.functionName != "PlayGroundImpact").ToArray();
            if (retained.Length == events.Length) continue;
            AnimationUtility.SetAnimationEvents(clip, retained);
            EditorUtility.SetDirty(clip);
        }
        AssetDatabase.SaveAssets();
    }

    public static bool CheckAll()
    {
        var report = new List<string>();
        try
        {
            foreach (string id in Ids)
            {
                string folder = Root + id + "/";
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(folder + id + ".prefab");
                var weapon = prefab.GetComponentInChildren<WeaponObj>(true);
                if (!weapon || weapon.effPos != prefab.transform.Find("TipPoint") || prefab.GetComponentsInChildren<AudioSource>(true).Length != 0)
                    throw new Exception(id + " has stale prefab bindings");
                var effect = AssetDatabase.LoadAssetAtPath<GameObject>(folder + "Effects/AttackEffect.prefab");
                if (effect && effect.GetComponentsInChildren<AudioSource>(true).Length != 0) throw new Exception(id + " duplicates audio inside its effect");
                var so = AssetDatabase.LoadAssetAtPath<WeaponSO>(folder + id + ".asset");
                if (so)
                {
                    var data = so.CreateData();
                    var copy = (WeaponData)data.Clone();
                    for (int i = 0; i < so.combo.Count; i++)
                    {
                        var a = so.combo[i]; var b = data.combo[i]; var c = copy.combo[i];
                        if (!a.audioClip || a.eff != effect || b.audioClip != a.audioClip || b.eff != a.eff || c.audioClip != a.audioClip || c.eff != a.eff || ReferenceEquals(a, b) || ReferenceEquals(b, c))
                            throw new Exception(id + " combo " + i + " lost attack resource references during cloning");
                        if (id == "weapon_001" && AssetDatabase.GetAssetPath(a.audioClip) != folder + "Audio/SwordSlash_0" + (i + 1) + ".wav") throw new Exception("Sword combo audio mismatch");
                    }
                }
                report.Add("PASS " + id + ": tip anchor, no duplicate AudioSource, SO/CreateData/Clone resources" + (so ? "" : " (FalconOath has no SO yet)"));
            }
            foreach (string guid in AssetDatabase.FindAssets("t:AnimationClip", new[] { Root.TrimEnd('/') }))
            {
                var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(guid));
                if (AnimationUtility.GetAnimationEvents(clip).Any(e => e.functionName == "PlayHandingEffORAudioClip" || e.functionName == "EndEff" || e.functionName == "PlayGroundImpact")) throw new Exception("Stale event " + clip.name);
            }
            report.Add("ALL PASSED: attack-data resource bindings and obsolete animation events.");
            File.WriteAllLines("attack-data-binding-validation.txt", report);
            return true;
        }
        catch (Exception e) { report.Add("FAIL " + e); File.WriteAllLines("attack-data-binding-validation.txt", report); Debug.LogException(e); return false; }
    }

    public static void Run()
    {
        try { AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport); BindAll(); EditorApplication.Exit(CheckAll() ? 0 : 1); }
        catch (Exception e) { File.WriteAllText("attack-data-binding-validation.txt", "FAIL " + e); Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
