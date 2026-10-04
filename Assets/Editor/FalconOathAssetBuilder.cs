using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Only creates this weapon's assets. Does not modify the open scene or character.
[InitializeOnLoad]
public static class FalconOathAssetBuilder
{
    const string Folder = "Assets/Art/Weapons/FalconOath";
    const string ModelPath = Folder + "/FalconOath.obj";
    const string PrefabPath = Folder + "/FalconOath.prefab";
    static FalconOathAssetBuilder() { EditorApplication.delayCall += CreateIfMissing; }
    static void CreateIfMissing()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode || !File.Exists(ModelPath) || File.Exists(PrefabPath)) return;
        Build();
    }
    [MenuItem("Tools/Weapons/Build Falcon Oath")]
    public static void Build()
    {
        AssetDatabase.ImportAsset(ModelPath, ImportAssetOptions.ForceSynchronousImport);
        var importer = AssetImporter.GetAtPath(ModelPath) as ModelImporter;
        if (importer == null) throw new InvalidOperationException("Falcon Oath OBJ importer unavailable.");
        importer.globalScale = 1;
        importer.useFileScale = false;
        importer.importNormals = ModelImporterNormals.Calculate;
        importer.normalSmoothingAngle = 35;
        importer.importCameras = false;
        importer.importLights = false;
        importer.SaveAndReimport();
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        if (!model) throw new InvalidOperationException("Falcon Oath model import failed.");
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (!shader) throw new InvalidOperationException("URP Lit shader missing.");
        var names = new[] { "Steel", "DarkSteel", "Brass", "Leather", "Garnet", "Cloth" };
        var colors = new[] {new Color(.48f,.55f,.61f),new Color(.10f,.125f,.145f),new Color(.62f,.40f,.15f),new Color(.23f,.047f,.038f),new Color(.48f,.016f,.03f),new Color(.38f,.045f,.04f)};
        var materials = new Material[names.Length];
        for (int i=0;i<names.Length;i++)
        {
            string path = Folder + "/" + names[i] + ".mat";
            materials[i] = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (!materials[i])
            {
                var mat = new Material(shader) {name=names[i]};
                mat.SetColor("_BaseColor",colors[i]);
                mat.SetFloat("_Metallic",i<3 ? .8f : 0);
                mat.SetFloat("_Smoothness",i==4 ? .82f : (i<3 ? .5f : .24f));
                AssetDatabase.CreateAsset(mat,path);
                materials[i]=mat;
            }
        }
        // Remap the source model as well as the wrapper prefab, so dragging the
        // OBJ directly into a scene also uses the authored URP materials.
        for (int i=0;i<names.Length;i++)
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),names[i]),materials[i]);
        importer.SaveAndReimport();
        model = AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath);
        var root = UnityEngine.Object.Instantiate(model);
        root.name="FalconOath";
        try
        {
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>())
            {
                var slots=renderer.sharedMaterials;
                for(int j=0;j<slots.Length;j++)
                {
                    string name=slots[j] ? slots[j].name : renderer.name;
                    int index=Array.FindIndex(names,n=>name==n || name.StartsWith(n+" ",StringComparison.Ordinal));
                    if(index<0) throw new InvalidOperationException("Unknown spear material: "+name);
                    slots[j]=materials[index];
                }
                renderer.sharedMaterials=slots;
            }
            var bounds = new Bounds(); bool first=true;
            int triangles=0;
            foreach(var filter in root.GetComponentsInChildren<MeshFilter>())
            {
                triangles+=filter.sharedMesh.triangles.Length/3;
                var b=filter.GetComponent<Renderer>().bounds;
                if(first){bounds=b;first=false;}else bounds.Encapsulate(b);
            }
            if(Mathf.Abs(bounds.size.y-2.25f)>.025f) throw new InvalidOperationException("Unexpected imported height: "+bounds.size.y);
            var grip = new GameObject("GripPoint");grip.transform.SetParent(root.transform,false);
            var tip = new GameObject("TipPoint");tip.transform.SetParent(root.transform,false);tip.transform.localPosition=new Vector3(0,1.485f,0);
            PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);
            AssetDatabase.SaveAssets();
            File.WriteAllText(Folder+"/unity-validation.txt","PASS\nHeight: "+bounds.size.y+" m\nTriangles: "+triangles+"\nURP materials: 6\nPrefab: "+PrefabPath);
            Debug.Log("[FalconOath] Prefab built and dimensions validated: "+PrefabPath);
        }
        finally { UnityEngine.Object.DestroyImmediate(root); }
    }
}
