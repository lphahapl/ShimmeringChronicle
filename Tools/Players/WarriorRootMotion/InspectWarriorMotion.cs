using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class InspectWarriorMotion
{
    public static void Run()
    {
        var report = new StringBuilder();
        var actor = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Warrior/Character Warrior.prefab"));
        foreach (var behaviour in actor.GetComponentsInChildren<MonoBehaviour>(true)) Object.DestroyImmediate(behaviour);
        actor.transform.position = Vector3.zero;
        var animator = actor.GetComponent<Animator>(); animator.enabled = false;
        report.AppendLine("Avatar human=" + animator.avatar.isHuman + " valid=" + animator.avatar.isValid);
        var bones = actor.GetComponentsInChildren<Transform>();
        var left = bones.First(t => t.name == "CATRigLLegAnkle");
        var right = bones.First(t => t.name == "CATRigRLegAnkle");
        foreach (string controllerPath in new[] {
            "Assets/Prefabs/Player/Warrior/Animations/Character Warrior.controller",
            "Assets/Prefabs/Weapon/weapon_001/Animations/sword.overrideController",
            "Assets/Prefabs/Weapon/weapon_002/Animations/Warrior/weapon_002.overrideController",
            "Assets/Prefabs/Weapon/weapon_003/Animations/Warrior/weapon_003.overrideController",
            "Assets/Prefabs/Weapon/weapon_004/Animations/Warrior/weapon_004.overrideController" })
        {
            var controller = AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(controllerPath);
            report.AppendLine("CONTROLLER " + controllerPath);
            foreach (var clip in controller.animationClips.Distinct())
            {
                report.AppendLine("CLIP " + clip.name + " " + clip.length + " " + AssetDatabase.GetAssetPath(clip) + " root=" + clip.hasRootCurves);
                if (!(clip.name.Contains("Walk") || clip.name.Contains("Run"))) continue;
                for (int i = 0; i <= 12; i++)
                {
                    clip.SampleAnimation(actor, clip.length * i / 12f);
                    report.AppendLine("  " + i + " left=" + left.position.ToString("F3") + " right=" + right.position.ToString("F3") + " actor=" + actor.transform.position.ToString("F3"));
                }
            }
        }
        File.WriteAllText("warrior-motion-inspection.txt", report.ToString());
        Object.DestroyImmediate(actor);
        EditorApplication.Exit(0);
    }
}
