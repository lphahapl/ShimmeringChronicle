using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
[InitializeOnLoad]
public static class FalconSceneCheck
{
    static FalconSceneCheck(){EditorApplication.delayCall+=Run;}
    [MenuItem("Tools/Weapons/Check Scene Appearance")]
    public static void Run()
    {
        var s=new StringBuilder();
        foreach(SceneView v in SceneView.sceneViews)s.AppendLine("View: "+v.cameraMode.name+" lighting="+v.sceneLighting+" effects="+v.sceneViewState.showImageEffects);
        s.AppendLine("Environment: "+RenderSettings.defaultReflectionMode+" intensity="+RenderSettings.reflectionIntensity+" sky="+RenderSettings.skybox+" custom="+RenderSettings.customReflectionTexture);
        foreach(var r in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            if(r.transform.root.name.Contains("Falcon"))
            {
                s.AppendLine(r.name+" active="+r.gameObject.activeInHierarchy+" probeUsage="+r.reflectionProbeUsage);
                foreach(var m in r.sharedMaterials)s.AppendLine("  "+AssetDatabase.GetAssetPath(m)+" shader="+m.shader.name+" metallic="+m.GetFloat("_Metallic")+" smooth="+m.GetFloat("_Smoothness"));
            }
        }
        foreach(var p in Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None))s.AppendLine("Probe: "+p.name+" mode="+p.mode+" texture="+p.texture+" bounds="+p.bounds);
        File.WriteAllText("Tools/WeaponConcepts/scene-appearance-check.txt",s.ToString());
    }
}
