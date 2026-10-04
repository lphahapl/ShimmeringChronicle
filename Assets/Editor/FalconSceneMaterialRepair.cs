using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

[InitializeOnLoad]
public static class FalconSceneMaterialRepair
{
    const string Folder="Assets/Art/Weapons/FalconOath";
    const string Marker="Tools/WeaponConcepts/scene-material-repair.txt";
    static FalconSceneMaterialRepair(){EditorApplication.delayCall+=Once;}
    static void Once(){if(!File.Exists(Marker) && !EditorApplication.isPlayingOrWillChangePlaymode)Run();}
    [MenuItem("Tools/Weapons/Repair Falcon Scene Materials")]
    public static void Run()
    {
        FalconOathAssetBuilder.Build();
        int repaired=0;
        foreach(var renderer in Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include,FindObjectsSortMode.None))
        {
            var slots=renderer.sharedMaterials;bool changed=false;
            for(int i=0;i<slots.Length;i++)
            {
                if(!slots[i] || AssetDatabase.GetAssetPath(slots[i])!=Folder+"/FalconOath.obj")continue;
                var correct=AssetDatabase.LoadAssetAtPath<Material>(Folder+"/"+slots[i].name+".mat");
                if(!correct)continue;
                slots[i]=correct;changed=true;repaired++;
            }
            if(changed)
            {
                Undo.RecordObject(renderer,"Repair Falcon materials");
                renderer.sharedMaterials=slots;
                PrefabUtility.RecordPrefabInstancePropertyModifications(renderer);
                EditorSceneManager.MarkSceneDirty(renderer.gameObject.scene);
            }
        }
        foreach(SceneView view in SceneView.sceneViews){view.sceneLighting=true;view.Repaint();}
        FalconSceneCheck.Run();
        string check=File.ReadAllText("Tools/WeaponConcepts/scene-appearance-check.txt");
        if(check.Contains("FalconOath.obj shader="))throw new System.Exception("Scene still references embedded OBJ materials");
        File.WriteAllText(Marker,"PASS: OBJ mapped to six external PBR materials. Scene lighting enabled. Explicit repaired slots: "+repaired+"\n"+check);
        Debug.Log("[FalconSceneMaterialRepair] Scene material references verified.");
    }
}
