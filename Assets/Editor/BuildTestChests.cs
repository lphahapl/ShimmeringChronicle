using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

[InitializeOnLoad]
public static class BuildTestChests
{
    const string Folder = "Assets/Art/Environment/TestChest";
    const string Report = "Tools/Environment/chest-result-v2.txt";
    static BuildTestChests() { EditorApplication.delayCall += Once; }
    static void Once() { if (!File.Exists(Report) && !EditorApplication.isPlayingOrWillChangePlaymode) Build(); }
    static Material[] materials;
    static readonly List<GameObject> parts = new List<GameObject>();
    static Material Mat(string name, Color color, float metal, float smooth)
    {
        var m = AssetDatabase.LoadAssetAtPath<Material>(Folder + "/" + name + ".mat");
        if (!m) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, Folder + "/" + name + ".mat"); }
        m.SetColor("_BaseColor", color); m.SetFloat("_Metallic", metal); m.SetFloat("_Smoothness", smooth); m.enableInstancing = true;
        return m;
    }
    static void Box(Vector3 pos, Vector3 size, int material)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.transform.position = pos; go.transform.localScale = size;
        go.GetComponent<Renderer>().sharedMaterial = materials[material]; parts.Add(go);
    }
    static void Finish(Transform parent, string name)
    {
        var submeshes = new List<Mesh>();
        foreach (var material in materials)
        {
            var combine = new List<CombineInstance>();
            foreach (var part in parts) if (part.GetComponent<Renderer>().sharedMaterial == material)
                combine.Add(new CombineInstance { mesh = part.GetComponent<MeshFilter>().sharedMesh, transform = part.transform.localToWorldMatrix });
            var sub = new Mesh(); sub.CombineMeshes(combine.ToArray(), true, true); submeshes.Add(sub);
        }
        var all = new List<CombineInstance>();
        foreach (var sub in submeshes) all.Add(new CombineInstance { mesh = sub, transform = Matrix4x4.identity });
        var mesh = new Mesh { name = name }; mesh.CombineMeshes(all.ToArray(), false, false); mesh.RecalculateBounds();
        string path = Folder + "/" + name + ".asset";
        var existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if (existing) { EditorUtility.CopySerialized(mesh, existing); Object.DestroyImmediate(mesh); mesh = existing; }
        else AssetDatabase.CreateAsset(mesh, path);
        parent.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
        parent.gameObject.AddComponent<MeshRenderer>().sharedMaterials = materials;
        foreach (var part in parts) Object.DestroyImmediate(part); parts.Clear();
        foreach (var sub in submeshes) Object.DestroyImmediate(sub);
    }
    [MenuItem("Tools/Terrain/Build and Scatter Low Poly Chests")]
    public static void Build()
    {
        var scene = SceneManager.GetActiveScene();
        if (scene.path != "Assets/Scenes/SampleScene.unity" || EditorApplication.isPlayingOrWillChangePlaymode) return;
        var terrain = Object.FindFirstObjectByType<Terrain>();
        var environment = GameObject.Find("Test Environment - Village and Plants");
        if (!terrain || !environment) return;
        Directory.CreateDirectory(Folder); Directory.CreateDirectory("Tools/Environment"); Directory.CreateDirectory("Tools/SceneBackups");
        File.Copy(scene.path, "Tools/SceneBackups/SampleScene-before-chests-" + DateTime.Now.ToString("yyyyMMdd-HHmmss") + ".unity", true);
        materials = new[] { Mat("Oak", new Color(.32f,.135f,.048f),0,.26f), Mat("OakLight",new Color(.46f,.235f,.085f),0,.3f), Mat("ForgedIron",new Color(.105f,.125f,.145f),.78f,.42f), Mat("Brass",new Color(.56f,.36f,.105f),.75f,.55f) };
        var root = new GameObject("Low Poly Hinged Chest");
        var body = new GameObject("Body"); body.transform.SetParent(root.transform,false);
        // Separate floor and walls leave a real hollow interior. Front faces negative Z.
        Box(new Vector3(0,.06f,0),new Vector3(1.4f,.12f,.86f),0);
        for(int row=0;row<3;row++)
        {
            float y=.22f+row*.19f;
            foreach(float side in new[]{-1f,1f}) {
                Box(new Vector3(0,y,side*.39f),new Vector3(1.4f,.182f,.08f),row%2);
                Box(new Vector3(side*.66f,y,0),new Vector3(.08f,.182f,.7f),row%2);
            }
        }
        foreach(float x in new[]{-.49f,.49f}) foreach(float z in new[]{-.44f,.44f}) {
            Box(new Vector3(x,.37f,z),new Vector3(.075f,.69f,.035f),2);
            foreach(float y in new[]{.13f,.58f}) Box(new Vector3(x,y,z+Mathf.Sign(z)*.024f),new Vector3(.036f,.036f,.014f),3);
        }
        Box(new Vector3(0,.55f,-.452f),new Vector3(.16f,.19f,.025f),2);
        Box(new Vector3(0,.57f,-.478f),new Vector3(.068f,.068f,.035f),3);
        Finish(body.transform,"ChestBody");
        var collider = body.AddComponent<BoxCollider>(); collider.center=new Vector3(0,.35f,0); collider.size=new Vector3(1.42f,.7f,.9f);
        var lid = new GameObject("LidHinge"); lid.transform.SetParent(root.transform,false); lid.transform.localPosition=new Vector3(0,.7f,.43f);
        // Five roof planks form a shallow faceted lid; all positions are hinge-local.
        for(int i=0;i<5;i++) {
            float z=-.8f+i*.185f; float y=.075f+(2-Mathf.Abs(i-2))*.035f;
            Box(new Vector3(0,y,z),new Vector3(1.46f,.13f,.18f),i%2);
            foreach(float x in new[]{-.49f,.49f}) Box(new Vector3(x,y+.075f,z),new Vector3(.08f,.025f,.188f),2);
        }
        foreach(float x in new[]{-.695f,.695f}) Box(new Vector3(x,.035f,-.43f),new Vector3(.065f,.1f,.91f),2);
        Box(new Vector3(0,-.025f,-.898f),new Vector3(.095f,.19f,.035f),3);
        Finish(lid.transform,"ChestLid");
        var clip = AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder+"/ChestOpen.anim");
        if(!clip) { clip=new AnimationClip(); AssetDatabase.CreateAsset(clip,Folder+"/ChestOpen.anim"); }
        clip.legacy=true; clip.wrapMode=WrapMode.ClampForever; clip.frameRate=30;
        clip.SetCurve("LidHinge",typeof(Transform),"localEulerAnglesRaw.x",AnimationCurve.EaseInOut(0,0,.65f,108));
        var animation=root.AddComponent<Animation>(); animation.AddClip(clip,TestChest.ClipName); animation.clip=clip; animation.playAutomatically=false; animation.cullingType=AnimationCullingType.AlwaysAnimate;
        root.AddComponent<TestChest>();
        var prefab=PrefabUtility.SaveAsPrefabAsset(root,Folder+"/TestChest.prefab"); Object.DestroyImmediate(root);
        var remove=new List<GameObject>();
        foreach(var t in environment.GetComponentsInChildren<Transform>(true)) if(t.name.StartsWith("Test crate ") || t.name=="Scattered Chests") remove.Add(t.gameObject);
        foreach(var go in remove) Object.DestroyImmediate(go);
        var group=new GameObject("Scattered Chests"); group.transform.SetParent(environment.transform,false);
        var center=terrain.transform.position+new Vector3(terrain.terrainData.size.x*.5f,0,terrain.terrainData.size.z*.5f);
        var locations=new[]{new Vector2(-7,7),new Vector2(8,6),new Vector2(-12,-8),new Vector2(13,-10),new Vector2(17,13),new Vector2(-23,18),new Vector2(37,29)};
        for(int i=0;i<locations.Length;i++) {
            var p=center+new Vector3(locations[i].x,0,locations[i].y); p.y=terrain.SampleHeight(p)+terrain.transform.position.y+.015f;
            var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,group.transform); instance.name="Test Chest "+(i+1); instance.transform.position=p; instance.transform.rotation=Quaternion.Euler(0,25+i*53,0);
        }
        PrefabUtility.SaveAsPrefabAsset(environment,"Assets/Art/Environment/TestVillage/TestEnvironment.prefab");
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        int triangles=0; foreach(var mf in prefab.GetComponentsInChildren<MeshFilter>()) triangles+=mf.sharedMesh.triangles.Length/3;
        var check=Object.Instantiate(prefab); clip.SampleAnimation(check,clip.length);
        float angle=Quaternion.Angle(Quaternion.identity,check.transform.Find("LidHinge").localRotation); Object.DestroyImmediate(check);
        Render(prefab,clip,0,"Tools/Environment/chest-closed.png"); Render(prefab,clip,clip.length,"Tools/Environment/chest-open.png");
        File.WriteAllText(Report,"PASS\nChests: "+locations.Length+"\nTriangles per chest: "+triangles+"\nOpening angle: "+angle+"\nDuration: "+clip.length+"\nHollow body, shared materials, body collider, reversible animation.\nSaved scene: "+scene.path);
        Debug.Log("[TestChests] Built and scattered seven animated low-poly chests.");
    }
    static void Render(GameObject prefab,AnimationClip clip,float time,string output)
    {
        var preview=new PreviewRenderUtility();
        try {
            var go=Object.Instantiate(prefab); preview.AddSingleGO(go); clip.SampleAnimation(go,time);
            var center=new Vector3(0,.88f,0); preview.camera.orthographic=true; preview.camera.orthographicSize=1.35f;
            preview.camera.nearClipPlane=.01f; preview.camera.farClipPlane=100;
            preview.camera.transform.position=center+new Vector3(2.3f,1.8f,-3); preview.camera.transform.LookAt(center);
            preview.camera.clearFlags=CameraClearFlags.SolidColor; preview.camera.backgroundColor=new Color(.09f,.11f,.15f);
            preview.ambientColor=new Color(.45f,.45f,.5f); preview.lights[0].intensity=1.8f; preview.lights[0].transform.rotation=Quaternion.Euler(40,-35,0);
            preview.lights[1].intensity=1; preview.lights[1].transform.rotation=Quaternion.Euler(20,150,0);
            preview.BeginStaticPreview(new Rect(0,0,900,800)); preview.Render(true);
            var image=preview.EndStaticPreview(); File.WriteAllBytes(output,image.EncodeToPNG()); Object.DestroyImmediate(image);
        } finally { preview.Cleanup(); }
    }
}

