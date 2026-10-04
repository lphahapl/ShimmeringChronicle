using System;
using System.IO;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object=UnityEngine.Object;

[InitializeOnLoad]
public static class UpgradeTestHouses
{
    const string Folder="Assets/Art/Environment/TestHouses";
    const string Report="Tools/Environment/house-upgrade-v2.txt";
    static Material plaster,wood,stone,tile,tileLight,glass,metal;
    static UpgradeTestHouses(){EditorApplication.delayCall+=Once;}
    static void Once(){if(!File.Exists(Report)&&!EditorApplication.isPlayingOrWillChangePlaymode)Build();}
    static Material Mat(string name,Color color,float smooth=0.15f)
    {
        string path=Folder+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(path);
        if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,path);}
        m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",smooth);m.enableInstancing=true;return m;
    }
    static GameObject Box(Transform parent,string name,Vector3 p,Vector3 s,Material mat,bool solid=false)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=p;go.transform.localScale=s;
        go.GetComponent<Renderer>().sharedMaterial=mat;if(!solid)Object.DestroyImmediate(go.GetComponent<Collider>());go.isStatic=true;return go;
    }
    static void Beam(Transform parent,Vector3 a,Vector3 b,float size)
    {
        var go=Box(parent,"Timber brace",(a+b)*.5f,new Vector3(size,(b-a).magnitude,size),wood);
        go.transform.localRotation=Quaternion.FromToRotation(Vector3.up,b-a);
    }
    [MenuItem("Tools/Terrain/Upgrade Accessible Test Houses")]
    public static void Build()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode)return;
        var scene=SceneManager.GetActiveScene();if(scene.path!="Assets/Scenes/SampleScene.unity")return;
        var buildings=GameObject.Find("Test Environment - Village and Plants/Buildings");var terrain=Object.FindFirstObjectByType<Terrain>();
        if(!buildings||!terrain)return;
        HousePassageCheck.Run("Tools/Environment/house-pre-upgrade.txt");
        Directory.CreateDirectory(Folder);Directory.CreateDirectory("Tools/SceneBackups");
        File.Copy(scene.path,"Tools/SceneBackups/SampleScene-before-house-upgrade-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity",true);
        plaster=Mat("Warm plaster",new Color(.78f,.69f,.51f));wood=Mat("Dark timber",new Color(.22f,.10f,.045f));stone=Mat("Foundation stone",new Color(.39f,.41f,.40f));
        tile=Mat("Clay roof",new Color(.40f,.105f,.055f));tileLight=Mat("Clay roof variation",new Color(.53f,.18f,.09f));glass=Mat("Blue window",new Color(.22f,.43f,.48f),.7f);metal=Mat("Iron",new Color(.10f,.12f,.13f),.4f);
        ConfigureGlass(glass);
        var houses=new List<Transform>();foreach(Transform h in buildings.transform)if(h.name.StartsWith("Test House"))houses.Add(h);
        foreach(var house in houses)
        {
            Undo.RegisterFullObjectHierarchyUndo(house.gameObject,"Upgrade test house");
            while(house.childCount>0)Object.DestroyImmediate(house.GetChild(0).gameObject);
            float highest=float.NegativeInfinity;
            for(float x=-4;x<=4;x+=1)for(float z=-3;z<=3;z+=1){var p=house.TransformPoint(new Vector3(x,0,z));highest=Mathf.Max(highest,terrain.SampleHeight(p)+terrain.transform.position.y);}
            var pos=house.position;pos.y=highest+.12f;house.position=pos;
            Box(house,"Foundation",new Vector3(0,-.4f,0),new Vector3(8,.8f,6),stone,true);
            // Floor boards are visual only: a single continuous foundation collider avoids seams.
            for(int i=0;i<16;i++)Box(house,"Floor board",new Vector3(-3.75f+i*.5f,.006f,0),new Vector3(.487f,.012f,5.65f),i%3==0?wood:plaster);
            Box(house,"Back wall",new Vector3(0,2.15f,2.85f),new Vector3(8,4.3f,.3f),plaster,true);
            foreach(float side in new[]{-1f,1f})
            {
                Box(house,"Entrance wall",new Vector3(side*2.65f,2.15f,-2.85f),new Vector3(2.7f,4.3f,.3f),plaster,true);
                Box(house,"Side sill wall",new Vector3(side*3.85f,.8f,0),new Vector3(.3f,1.6f,6),plaster,true);
                Box(house,"Side upper wall",new Vector3(side*3.85f,3.65f,0),new Vector3(.3f,1.3f,6),plaster,true);
                foreach(float z in new[]{-2f,2f})Box(house,"Side window pier",new Vector3(side*3.85f,2.3f,z),new Vector3(.3f,1.4f,2),plaster,true);
                Box(house,"Window glass",new Vector3(side*3.85f,2.3f,0),new Vector3(.05f,1.4f,2),glass,true);
                foreach(float z in new[]{-1.04f,0,1.04f})Box(house,"Window upright",new Vector3(side*4.02f,2.3f,z),new Vector3(.14f,1.65f,.12f),wood);
                foreach(float y in new[]{1.55f,2.3f,3.05f})Box(house,"Window rail",new Vector3(side*4.02f,y,0),new Vector3(.14f,.12f,2.2f),wood);
                foreach(float z in new[]{-2.88f,2.88f})Box(house,"Corner timber",new Vector3(side*3.9f,2.15f,z),new Vector3(.25f,4.4f,.25f),wood);
                Box(house,"Door jamb",new Vector3(side*1.38f,1.8f,-3.04f),new Vector3(.16f,3.6f,.22f),wood);
                Box(house,"Front timber band",new Vector3(side*2.7f,1,-3.02f),new Vector3(2.55f,.14f,.12f),wood);
                Beam(house,new Vector3(side*1.5f,3.85f,-3.06f),new Vector3(side*3.72f,2.5f,-3.06f),.14f);
                var roofRoot=new GameObject("Tiled roof slope");roofRoot.transform.SetParent(house,false);roofRoot.transform.localPosition=new Vector3(side*2.05f,5.1f,0);roofRoot.transform.localRotation=Quaternion.Euler(0,0,-side*20);
                Box(roofRoot.transform,"Roof slab",Vector3.zero,new Vector3(4.6f,.18f,6.8f),tile,true);
                for(int row=0;row<8;row++)for(int col=0;col<12;col++)Box(roofRoot.transform,"Clay tile",new Vector3(-2.03f+row*.58f,.11f,-3.12f+col*.57f),new Vector3(.60f,.055f,.54f),(row+col)%4==0?tileLight:tile);
                Box(house,"Eave beam",new Vector3(side*4.15f,4.32f,0),new Vector3(.22f,.25f,6.9f),wood);
                Beam(house,new Vector3(side*4.3f,4.28f,-3.42f),new Vector3(0,5.84f,-3.42f),.22f);
                Box(house,"Porch post",new Vector3(side*1.9f,1.92f,-4.25f),new Vector3(.2f,3.84f,.2f),wood,true);
                Box(house,"Porch post base",new Vector3(side*1.9f,.1f,-4.25f),new Vector3(.34f,.2f,.34f),stone);
            }
            Box(house,"Door lintel",new Vector3(0,3.95f,-2.85f),new Vector3(2.6f,.7f,.3f),plaster,true);
            Gable(house,-2.85f);Gable(house,2.85f);
            Box(house,"Door lintel trim",new Vector3(0,3.72f,-3.04f),new Vector3(2.9f,.2f,.2f),wood);
            Box(house,"Ridge cap",new Vector3(0,5.86f,0),new Vector3(.3f,.18f,7),tileLight);
            Box(house,"Porch canopy",new Vector3(0,3.99f,-3.9f),new Vector3(4.25f,.15f,2.2f),tile,true);
            Box(house,"Porch crossbeam",new Vector3(0,3.84f,-4.3f),new Vector3(4,.2f,.2f),wood);
            Box(house,"Chimney",new Vector3(2.4f,5.45f,1.65f),new Vector3(.7f,2.0f,.8f),stone);
            Box(house,"Chimney cap",new Vector3(2.4f,6.5f,1.65f),new Vector3(.94f,.16f,1.04f),stone);
            // Furniture stays outside the centre travel corridor.
            Box(house,"Table top",new Vector3(-2.4f,1.05f,.8f),new Vector3(1.5f,.12f,1.6f),wood,true);
            foreach(float x in new[]{-2.98f,-1.82f})foreach(float z in new[]{.18f,1.42f})Box(house,"Table leg",new Vector3(x,.5f,z),new Vector3(.12f,1,.12f),wood);
            Box(house,"Bench",new Vector3(-2.4f,.5f,-.5f),new Vector3(1.5f,.16f,.48f),wood,true);
            for(int shelf=0;shelf<3;shelf++)Box(house,"Shelf",new Vector3(2.95f,.55f+shelf*.8f,1.4f),new Vector3(.9f,.1f,1.9f),wood,true);
            foreach(float z in new[]{.45f,2.35f})Box(house,"Shelf upright",new Vector3(2.95f,1.35f,z),new Vector3(.9f,2.7f,.12f),wood,true);
            for(int i=0;i<4;i++)Box(house,"Stored package",new Vector3(2.95f,.83f+(i/2)*.8f,.92f+(i%2)*.85f),new Vector3(.52f,.45f,.55f),i%2==0?plaster:stone);
            MakeRamp(house,terrain);
            PrefabUtility.SaveAsPrefabAsset(house.gameObject,Folder+"/"+house.name.Replace(" ","")+".prefab");
        }
        AssetDatabase.SaveAssets();Physics.SyncTransforms();
        bool pass=HousePassageCheck.Run("Tools/Environment/house-after.txt");
        pass &= HousePassageCheck.Walk("Tools/Environment/house-walk.txt");
        PrefabUtility.SaveAsPrefabAsset(buildings.transform.parent.gameObject,"Assets/Art/Environment/TestVillage/TestEnvironment.prefab");
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        Render(houses[0].gameObject);
        File.WriteAllText(Report,(pass?"PASS":"FAIL")+"\nHouses: "+houses.Count+"\nDoor clear width 2.6m, height 3.6m\n4m terrain-fitted entrance ramps, window frames, tiled roofs, porch, furnishings.\n");
        Selection.activeGameObject=houses[0].gameObject;
        if(SceneView.lastActiveSceneView)SceneView.lastActiveSceneView.LookAt(houses[0].position+Vector3.up*2,Quaternion.Euler(20,25,0),13);
    }
    static void ConfigureGlass(Material material)
    {
        material.SetColor("_BaseColor",new Color(.55f,.78f,.84f,.16f));
        material.SetColor("_Color",new Color(.55f,.78f,.84f,.16f));
        material.SetFloat("_Surface",1);material.SetFloat("_Blend",0);
        material.SetFloat("_BlendModePreserveSpecular",0);
        material.SetFloat("_SrcBlend",(float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetFloat("_DstBlend",(float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_SrcBlendAlpha",(float)UnityEngine.Rendering.BlendMode.One);
        material.SetFloat("_DstBlendAlpha",(float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetFloat("_ZWrite",0);material.SetFloat("_AlphaClip",0);
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");material.DisableKeyword("_ALPHAPREMULTIPLY_ON");material.DisableKeyword("_ALPHATEST_ON");
        material.SetOverrideTag("RenderType","Transparent");material.renderQueue=3000;
        material.SetShaderPassEnabled("ShadowCaster",false);
        material.SetShaderPassEnabled("DepthOnly",false);
    }
    static void MakeRamp(Transform h,Terrain terrain)
    {
        var a=h.TransformPoint(new Vector3(0,0,-7));float bottom=terrain.SampleHeight(a)+terrain.transform.position.y-h.position.y-.06f;
        var mesh=new Mesh{name=h.name.Replace(" ","")+"Ramp"};
        mesh.vertices=new[]{new Vector3(-1.6f,bottom,-7),new Vector3(1.6f,bottom,-7),new Vector3(-1.6f,0,-2.98f),new Vector3(1.6f,0,-2.98f),new Vector3(-1.6f,bottom-.2f,-2.98f),new Vector3(1.6f,bottom-.2f,-2.98f)};
        mesh.triangles=new[]{0,2,1,1,2,3,0,4,2,1,3,5,2,4,3,3,4,5,0,1,4,1,5,4};mesh.RecalculateNormals();
        string path=Folder+"/"+h.name.Replace(" ","")+"Ramp.asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(old){EditorUtility.CopySerialized(mesh,old);Object.DestroyImmediate(mesh);mesh=old;}else AssetDatabase.CreateAsset(mesh,path);
        var go=new GameObject("Entrance ramp");go.transform.SetParent(h,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=stone;go.AddComponent<MeshCollider>().sharedMesh=mesh;go.isStatic=true;
    }
    static void Gable(Transform house,float z)
    {
        string path=Folder+"/Gable.asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
        if(!mesh){
            mesh=new Mesh{name="Gable"};
            mesh.vertices=new[]{new Vector3(-4,4.3f,-.15f),new Vector3(4,4.3f,-.15f),new Vector3(0,5.72f,-.15f),new Vector3(-4,4.3f,.15f),new Vector3(4,4.3f,.15f),new Vector3(0,5.72f,.15f)};
            mesh.triangles=new[]{0,2,1,3,4,5,0,3,2,2,3,5,1,2,4,2,5,4,0,1,3,1,4,3};mesh.RecalculateNormals();AssetDatabase.CreateAsset(mesh,path);
        }
        var go=new GameObject("Gable wall");go.transform.SetParent(house,false);go.transform.localPosition=new Vector3(0,0,z);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=plaster;go.AddComponent<MeshCollider>().sharedMesh=mesh;go.isStatic=true;
        Box(house,"Gable king post",new Vector3(0,4.92f,z+Mathf.Sign(z)*.19f),new Vector3(.17f,1.35f,.16f),wood);
    }
    static void Render(GameObject house)
    {
        var preview=new PreviewRenderUtility();try{
            var go=Object.Instantiate(house);go.transform.position=Vector3.zero;go.transform.rotation=Quaternion.identity;preview.AddSingleGO(go);
            var target=new Vector3(0,2.1f,-.8f);preview.camera.orthographic=true;preview.camera.orthographicSize=6.8f;preview.camera.nearClipPlane=.1f;preview.camera.farClipPlane=100;
            preview.camera.transform.position=target+new Vector3(11,8,-16);preview.camera.transform.LookAt(target);preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.12f,.16f,.19f);
            preview.ambientColor=new Color(.5f,.5f,.5f);preview.lights[0].intensity=1.4f;preview.lights[0].transform.rotation=Quaternion.Euler(35,-35,0);preview.lights[1].intensity=.8f;preview.lights[1].transform.rotation=Quaternion.Euler(35,145,0);
            preview.BeginStaticPreview(new Rect(0,0,1200,950));preview.Render(true);var img=preview.EndStaticPreview();File.WriteAllBytes("Tools/Environment/house-upgraded.png",img.EncodeToPNG());Object.DestroyImmediate(img);
        }finally{preview.Cleanup();}
    }
}
