using System;
using System.IO;
using UnityEditor;
using UnityEngine;

// Editor-side resource binding; runtime only reads WeaponObj.effPos and AudioSource.
public static class WeaponPrefabEffects
{
    const string Root = "Assets/Prefabs/Weapon/";
    public static void ConfigureAudioOnly(string id)
    {
        string path = Root + id + "/" + id + ".prefab";
        var root = PrefabUtility.LoadPrefabContents(path);
        try
        {
            foreach (var weapon in root.GetComponentsInChildren<WeaponObj>(true))
            {
                weapon.effPos = null;
                var audio = weapon.GetComponent<AudioSource>();
                if (!audio) audio = weapon.gameObject.AddComponent<AudioSource>();
                audio.clip = AssetDatabase.LoadAssetAtPath<AudioClip>(Root + id + "/Audio/unarmed_swing.wav");
                if (!audio.clip) throw new Exception(id + " missing unarmed prefab audio");
                audio.playOnAwake = false;
                audio.loop = false;
                audio.spatialBlend = 1;
                audio.dopplerLevel = 0;
                audio.minDistance = 2;
                audio.maxDistance = 20;
                audio.volume = .65f;
            }
            PrefabUtility.SaveAsPrefabAsset(root, path);
        }
        finally { PrefabUtility.UnloadPrefabContents(root); }
    }

    public static void Run()
    {
        try
        {
            foreach (string id in WeaponAssetProduction.Names)
            {
                string path = Root + id + "/" + id + ".prefab";
                var root = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    var effect = root.transform.Find("TipPoint/AttackEffect");
                    if (!effect) throw new Exception(id + " missing tip effect");
                    effect.gameObject.SetActive(false);
                    var weapons = root.GetComponentsInChildren<WeaponObj>(true);
                    if (weapons.Length == 0) weapons = new[] { root.AddComponent<WeaponObj>() };
                    foreach (var weapon in weapons) weapon.effPos = effect;
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                }
                finally { PrefabUtility.UnloadPrefabContents(root); }
            }
            ConfigureAudioOnly("MossstoneFists");
            ConfigureAudioOnly("ReinforcedFists");
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            bool passed = ValidateWeaponProduction.CheckAll();
            File.WriteAllText("prefab-effects-result.txt", passed ? "ALL PASSED: WeaponObj.effPos bound on eight prefabs; AudioSource configured on both unarmed prefabs. SO/data have no effect/audio fields." : "FAIL: see production-validation.txt");
            EditorApplication.Exit(passed ? 0 : 1);
        }
        catch (Exception e)
        {
            File.WriteAllText("prefab-effects-result.txt", "FAIL " + e);
            Debug.LogException(e);
            EditorApplication.Exit(1);
        }
    }
}
