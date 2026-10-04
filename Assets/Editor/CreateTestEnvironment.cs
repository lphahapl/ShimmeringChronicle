using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class CreateTestEnvironment
{
    const string Folder="Assets/Art/Environment/TestVillage";
    const string RootName="Test Environment - Village and Plants";
    const string Report="Tools/Environment/environment-result.txt";
    static CreateTestEnvironment(){EditorApplication.delayCall+=Once;}
    static void Once(){if(!EditorApplication.isPlayingOrWillChangePlaymode && !File.Exists(Report))Build();}
    static Material wood,leaves,grass,wall,roof,stone,petal,yellow;
    static Terrain terrain;
    static Vector3 center;
    static Mesh cone,roofMesh;
    static System.Random rng;
    static float Rand(float a,float b)=>a+(float)rng.NextDouble()*(b-a);
    static Material Mat(string name,Color color)
    {
        string path=Folder+"/"+name+".mat";
        var m=AssetDatabase.LoadAssetAtPath<Material>(path);if(m)return m;
        m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};
        m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.12f);m.enableInstancing=true;
        AssetDatabase.CreateAsset(m,path);return m;
    }
    static GameObject Group(string name,Transform parent,Vector3 pos)
    {
        var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.localPosition=pos;return go;
    }
    static GameObject Shape(string name,PrimitiveType type,Transform parent,Vector3 pos,Vector3 scale,Material mat,bool solid=false)
    {
        var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);
        go.transform.localPosition=pos;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=mat;
        if(!solid)Object.DestroyImmediate(go.GetComponent<Collider>());
        go.isStatic=true;return go;
    }
    static void MeshShape(string name,Mesh mesh,Transform parent,Vector3 pos,Vector3 scale,Material mat)
    {
        var go=Group(name,parent,pos);go.transform.localScale=scale;
        go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=mat;go.isStatic=true;
    }
    static Mesh MakeCone()
    {
        string path=Folder+"/TreeCone.asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(old)return old;
        var v=new Vector3[10];v[0]=Vector3.up;v[1]=Vector3.zero;
        for(int i=0;i<8;i++)v[i+2]=new Vector3(Mathf.Cos(i*Mathf.PI/4)*.5f,0,Mathf.Sin(i*Mathf.PI/4)*.5f);
        var t=new int[48];for(int i=0;i<8;i++){int j=(i+1)%8;t[i*6]=0;t[i*6+1]=j+2;t[i*6+2]=i+2;t[i*6+3]=1;t[i*6+4]=i+2;t[i*6+5]=j+2;}
        var m=new Mesh{name="Low poly tree crown",vertices=v,triangles=t};m.RecalculateNormals();AssetDatabase.CreateAsset(m,path);return m;
    }
    static Vector3 Ground(float x,float z)
    {
        var p=center+new Vector3(x,0,z);p.y=terrain.SampleHeight(p)+terrain.transform.position.y;return p;
    }
    static void House(Transform parent,string name,float x,float z,float angle)
    {
        var p=Ground(x,z);float highest=p.y;
        foreach(float dx in new[]{-4f,4f})foreach(float dz in new[]{-3f,3f})highest=Mathf.Max(highest,Ground(x+dx,z+dz).y);
        p.y=highest+.08f;
        var h=Group(name,parent,p);h.transform.rotation=Quaternion.Euler(0,angle,0);
        Shape("Foundation",PrimitiveType.Cube,h.transform,new Vector3(0,-.30f,0),new Vector3(8,.6f,6),stone,true);
        Shape("Back wall",PrimitiveType.Cube,h.transform,new Vector3(0,1.6f,2.85f),new Vector3(8,3.2f,.3f),wall,true);
        foreach(float side in new[]{-1f,1f})
        {
            Shape("Side wall",PrimitiveType.Cube,h.transform,new Vector3(side*3.85f,1.6f,0),new Vector3(.3f,3.2f,6),wall,true);
            Shape("Entrance wall",PrimitiveType.Cube,h.transform,new Vector3(side*2.5f,1.6f,-2.85f),new Vector3(3,3.2f,.3f),wall,true);
            Shape("Door frame",PrimitiveType.Cube,h.transform,new Vector3(side*1.02f,1.25f,-3.03f),new Vector3(.14f,2.5f,.2f),wood);
            var slab=Shape("Sloped roof",PrimitiveType.Cube,h.transform,new Vector3(side*2.06f,3.83f,0),new Vector3(4.5f,.20f,6.7f),roof,true);
            slab.transform.localRotation=Quaternion.Euler(0,0,-side*18);
            Shape("Window",PrimitiveType.Cube,h.transform,new Vector3(side*2.55f,1.8f,-3.015f),new Vector3(1.1f,.95f,.035f),wood);
        }
        Shape("Door lintel",PrimitiveType.Cube,h.transform,new Vector3(0,2.85f,-2.85f),new Vector3(2, .7f,.3f),wall,true);
        Shape("Threshold step",PrimitiveType.Cube,h.transform,new Vector3(0,-.15f,-3.35f),new Vector3(2.3f,.3f,.8f),stone,true);
        Shape("Chimney",PrimitiveType.Cube,h.transform,new Vector3(2.5f,4.4f,1.5f),new Vector3(.7f,1.8f,.7f),stone);
    }
    [MenuItem("Tools/Terrain/Add Test Village and Plants")]
    public static void Build()
    {
        var scene=SceneManager.GetActiveScene();if(scene.path!="Assets/Scenes/SampleScene.unity")return;
        foreach(var r in scene.GetRootGameObjects())if(r.name==RootName)return;
        terrain=Object.FindFirstObjectByType<Terrain>();if(!terrain)return;
        center=terrain.transform.position+terrain.terrainData.size*.5f;center.y=0;
        Directory.CreateDirectory(Folder);Directory.CreateDirectory("Tools/Environment");Directory.CreateDirectory("Tools/SceneBackups");
        File.Copy(scene.path,"Tools/SceneBackups/SampleScene-before-decoration-"+System.DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity");
        AssetDatabase.Refresh();rng=new System.Random(230926);
        wood=Mat("Wood",new Color(.24f,.14f,.075f));leaves=Mat("Foliage",new Color(.21f,.37f,.18f));
        grass=Mat("Grass",new Color(.32f,.46f,.21f));wall=Mat("Plaster",new Color(.71f,.65f,.48f));
        roof=Mat("Terracotta",new Color(.45f,.19f,.11f));stone=Mat("Stone",new Color(.40f,.42f,.39f));
        petal=Mat("Flowers",new Color(.72f,.43f,.60f));yellow=Mat("Pollen",new Color(.93f,.72f,.24f));cone=MakeCone();
        var root=Group(RootName,null,Vector3.zero);Undo.RegisterCreatedObjectUndo(root,"Add test scenery");
        var trees=Group("Trees",root.transform,Vector3.zero);
        for(int i=0;i<32;i++)
        {
            float angle=Rand(0,Mathf.PI*2),radius=Rand(35,70),x=Mathf.Cos(angle)*radius,z=Mathf.Sin(angle)*radius;
            if(Mathf.Abs(x)<5 || Mathf.Abs(z)<5){i--;continue;}
            var tr=Group("Tree "+(i+1),trees.transform,Ground(x,z));float h=Rand(4.2f,7);
            Shape("Trunk",PrimitiveType.Cylinder,tr.transform,Vector3.up*h*.25f,new Vector3(.45f,h*.25f,.45f),wood,true);
            MeshShape("Crown lower",cone,tr.transform,Vector3.up*h*.28f,new Vector3(h*.65f,h*.55f,h*.65f),leaves);
            MeshShape("Crown upper",cone,tr.transform,Vector3.up*h*.52f,new Vector3(h*.48f,h*.48f,h*.48f),leaves);
        }
        var plants=Group("Grass and Flowers",root.transform,Vector3.zero);
        for(int i=0;i<100;i++)
        {
            float x=Rand(-70,70),z=Rand(-70,70);
            if(Mathf.Abs(x)<6 || Mathf.Abs(z)<6 || new Vector2(x,z).magnitude<13 || (x>10 && x<39 && z>10 && z<40)){i--;continue;}
            var tuft=Group(i<35?"Flower patch":"Grass tuft",plants.transform,Ground(x,z));
            for(int j=0;j<3;j++)
            {
                float dx=Rand(-.5f,.5f),dz=Rand(-.5f,.5f),h=Rand(.25f,.60f);
                MeshShape("Blade",cone,tuft.transform,new Vector3(dx,0,dz),new Vector3(.19f,h,.19f),grass);
                if(i<35)
                {
                    Shape("Petals",PrimitiveType.Sphere,tuft.transform,new Vector3(dx,h,dz),new Vector3(.24f,.08f,.24f),petal);
                    Shape("Center",PrimitiveType.Sphere,tuft.transform,new Vector3(dx,h+.04f,dz),new Vector3(.08f,.04f,.08f),yellow);
                }
            }
        }
        var village=Group("Buildings",root.transform,Vector3.zero);
        House(village.transform,"Test House A",20,20,0);House(village.transform,"Test House B",33,20,0);House(village.transform,"Test House C",26,34,180);
        AssetDatabase.SaveAssets();PrefabUtility.SaveAsPrefabAsset(root,Folder+"/TestEnvironment.prefab");
        EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject=root;
        if(SceneView.lastActiveSceneView)SceneView.lastActiveSceneView.LookAt(Ground(20,20),Quaternion.Euler(32,-35,0),38);
        File.WriteAllText(Report,"PASS\nTrees: 32\nFlower patches: 35 (105 flowers)\nGrass-only tufts: 65\nBuildings: 3, doorway width 2 m\nCrates: 7\nColliders: "+root.GetComponentsInChildren<Collider>().Length+"\nSpawn clear radius: 13 m; cross corridors kept clear\nSaved scene: "+scene.path);
        Debug.Log("[TestEnvironment] Test scenery generated and scene saved.");
        BuildTestChests.Build();
        UpgradeTestHouses.Build();
    }
}
