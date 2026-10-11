using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class TestEnemyChaseValidation
{
    static float Movement(RuntimeAnimatorController controller, string state)
    {
        var actor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/TestEnemy/TestEnemy.prefab"));
        try
        {
            // Measure the motion consumed by EnemyAI.OnAnimatorMove without scene/AI side effects.
            foreach (var component in actor.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(component);
            var animator = actor.GetComponent<Animator>();
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            animator.applyRootMotion = true;
            animator.runtimeAnimatorController = controller;
            animator.Rebind();
            animator.SetBool("isWalking", true);
            animator.SetBool("isRunning", state == "Chase");
            animator.Play("Base Layer." + state, 0, 0);
            animator.Update(0);
            float distance = 0;
            for (int i = 0; i < 60; i++)
            {
                animator.Update(1f / 60f);
                distance += new Vector2(animator.deltaPosition.x, animator.deltaPosition.z).magnitude;
            }
            return distance;
        }
        finally { Object.DestroyImmediate(actor); }
    }

    public static void Run()
    {
        var report = new List<string>();
        int exit = 0;
        try
        {
            var so = AssetDatabase.LoadAssetAtPath<WeaponSO>("Assets/Prefabs/Weapon/EnemyWeapon_001/EnemyWeapon_001.asset");
            var original = AssetDatabase.LoadAssetAtPath<UnityEditor.Animations.AnimatorController>("Assets/Prefabs/Enemies/TestEnemy/Animations/TestEnemy.controller");
            bool matches = true;
            foreach (string state in new[] { "Walk", "Chase" })
            {
                float reference = Movement(original, state);
                float actual = Movement(so.overrideController, state);
                report.Add(state + " original=" + reference.ToString("F3") + " m/s; equipped=" + actual.ToString("F3") + " m/s");
                if (reference < .1f || Mathf.Abs(reference - actual) > .05f) matches = false;
            }
            if (!matches) throw new Exception("Equipped animation lost the original locomotion root motion");
            report.Add("ALL PASSED: equipped test enemy supplies locomotion root motion to EnemyAI.");
        }
        catch (Exception e) { report.Add("FAIL " + e); exit = 1; }
        File.WriteAllLines("test-enemy-chase-validation.txt", report);
        Debug.Log(string.Join("\n", report));
        EditorApplication.Exit(exit);
    }

    public static void Repair()
    {
        foreach (string name in new[] { "Walk", "Run" })
        {
            string path = "Assets/Prefabs/Weapon/EnemyWeapon_001/Animations/" + name + ".anim";
            var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(path);
            var source = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Prefabs/Enemies/TestEnemy/Animations/TestEnemy_" + (name == "Run" ? "Chase" : "Walk") + ".anim");
            EnemyWeaponProduction.PreserveLocomotionRoot(clip, source);
            EditorUtility.SetDirty(clip);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceSynchronousImport | ImportAssetOptions.ForceUpdate);
        }
        Run();
    }
}
