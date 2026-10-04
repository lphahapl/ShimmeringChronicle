using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class CreateTestTerrain
{
    const string Folder="Assets/Art/Terrain/TestGround";
    const string Report="Tools/WeaponConcepts/test-terrain-result.txt";
    static CreateTestTerrain(){EditorApplication.delayCall+=CreateOnce;}
    static void CreateOnce()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || File.Exists(Report))return;
        Create();
    }
    [MenuItem("Tools/Terrain/Create Gentle Test Ground")]
    public static void Create()
    {
        var scene=SceneManager.GetActiveScene();
        if(scene.path!="Assets/Scenes/SampleScene.unity")return;
        // Work around the actual character, including unsaved scene transforms.
        GameObject player=null;
        foreach(var root in scene.GetRootGameObjects())
            if(root.CompareTag("Player") || root.name=="Character Warrior"){player=root;break;}
        var anchor=player?player.transform.position:Vector3.zero;
        float ground=anchor.y-1;
        GameObject plane=null;
        foreach(var root in scene.GetRootGameObjects())if(root.name=="Plane"){plane=root;break;}
        if(plane && plane.TryGetComponent<Collider>(out var collider))ground=collider.bounds.max.y;
        Directory.CreateDirectory(Folder);AssetDatabase.Refresh();
        const int resolution=257;const float width=160,vertical=8,baseHeight=2;
        string dataPath=Folder+"/GentleGround.asset";
        if(File.Exists(dataPath))return;
        var data=new TerrainData{name="Gentle Test Ground",heightmapResolution=resolution,size=new Vector3(width,vertical,width)};
        var heights=new float[resolution,resolution];
        float min=100,max=-100;
        for(int z=0;z<resolution;z++)for(int x=0;x<resolution;x++)
        {
            float px=(float)x/(resolution-1)*width-width/2;
            float pz=(float)z/(resolution-1)*width-width/2;
            float distance=Mathf.Sqrt(px*px+pz*pz);
            float blend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(10,28,distance));
            float waves=(Mathf.PerlinNoise((px+713)/43,(pz+291)/43)-.5f)*3.2f
                +Mathf.Sin(px/19)*Mathf.Cos(pz/24)*.5f;
            float h=baseHeight+waves*blend;
            heights[z,x]=h/vertical;min=Mathf.Min(min,h);max=Mathf.Max(max,h);
        }
        data.SetHeights(0,0,heights);
        AssetDatabase.CreateAsset(data,dataPath);
        var texture=new Texture2D(64,64,TextureFormat.RGB24,false){name="Muted grass ground"};
        var pixels=new Color[64*64];
        for(int z=0;z<64;z++)for(int x=0;x<64;x++)
        {
            float n=Mathf.PerlinNoise(x*.31f,z*.31f)*.07f;
            pixels[z*64+x]=new Color(.29f+n,.34f+n,.22f+n);
        }
        texture.SetPixels(pixels);texture.Apply();
        File.WriteAllBytes(Folder+"/GroundColor.png",texture.EncodeToPNG());Object.DestroyImmediate(texture);
        AssetDatabase.ImportAsset(Folder+"/GroundColor.png",ImportAssetOptions.ForceSynchronousImport);
        var layer=new TerrainLayer{diffuseTexture=AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/GroundColor.png"),tileSize=new Vector2(8,8),smoothness=.08f,metallic=0};
        AssetDatabase.CreateAsset(layer,Folder+"/Grass.terrainlayer");data.terrainLayers=new[]{layer};
        var shader=Shader.Find("Universal Render Pipeline/Terrain/Lit");
        if(!shader)throw new System.Exception("URP terrain shader not found");
        var material=new Material(shader){name="Test Ground URP"};AssetDatabase.CreateAsset(material,Folder+"/Ground.mat");
        var go=Terrain.CreateTerrainGameObject(data);go.name="Test Terrain - Gentle Hills";
        Undo.RegisterCreatedObjectUndo(go,"Create gentle test terrain");
        go.transform.position=new Vector3(anchor.x-width/2,ground-baseHeight,anchor.z-width/2);
        var terrain=go.GetComponent<Terrain>();terrain.materialTemplate=material;terrain.heightmapPixelError=5;terrain.drawInstanced=true;
        // Remove the overlapping flat collider reversibly, without deleting it.
        if(plane){Undo.RecordObject(plane,"Disable replaced flat ground");plane.SetActive(false);}
        EditorSceneManager.MarkSceneDirty(scene);AssetDatabase.SaveAssets();
        Directory.CreateDirectory("Tools/SceneBackups");
        // Preserve the user's existing on-disk scene before saving current work.
        string backup="Tools/SceneBackups/SampleScene-before-terrain-"+System.DateTime.Now.ToString("yyyyMMdd-HHmmss")+".unity";
        File.Copy(scene.path,backup);
        EditorSceneManager.SaveScene(scene);
        Selection.activeGameObject=go;
        if(SceneView.lastActiveSceneView)SceneView.lastActiveSceneView.LookAt(anchor,Quaternion.Euler(38,35,0),85);
        bool valid=go.GetComponent<TerrainCollider>().terrainData==data && data.heightmapResolution==resolution;
        if(!valid)throw new System.Exception("Terrain collider validation failed");
        File.WriteAllText(Report,"PASS\nScene: "+scene.path+"\nArea: 160 x 160 m\nHeight range: "+(max-min).ToString("F2")+" m\nFlat spawn radius: 10 m\nCenter: "+anchor+"\nGround elevation: "+ground+"\nTerrainCollider: valid\nPrevious Plane: disabled\nBackup: "+backup);
        Debug.Log("[TestTerrain] Gentle test terrain created and saved.");
    }
}
