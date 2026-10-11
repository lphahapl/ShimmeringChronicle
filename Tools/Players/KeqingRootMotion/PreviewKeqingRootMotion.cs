using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
public static class PreviewKeqingRootMotion
{
    static PreviewRenderUtility preview;
    static GameObject actor;
    static Vector3 cameraOffset=new Vector3(2.6f,1.1f,4.1f);
    static Transform closeUp;
    static float zoom=1.12f;
    static Dictionary<SkinnedMeshRenderer,Mesh> bakedMeshes=new Dictionary<SkinnedMeshRenderer,Mesh>();
    static Dictionary<Material,Material> displayMaterials=new Dictionary<Material,Material>();
    static Texture BaseTexture(Material material)
    {
        var serialized=new SerializedObject(material);var textures=serialized.FindProperty("m_SavedProperties.m_TexEnvs");
        for(int k=0;k<textures.arraySize;k++)
        {
            var element=textures.GetArrayElementAtIndex(k);var key=element.FindPropertyRelative("first").stringValue;
            if(key=="_BaseMap"||key=="_MainTex")
            {
                var value=element.FindPropertyRelative("second.m_Texture").objectReferenceValue as Texture;
                if(value!=null)return value;
            }
        }
        return null;
    }
    static Material Display(Material source)
    {
        if(displayMaterials.TryGetValue(source,out var result))return result;
        var texture=BaseTexture(source);
        result=new Material(Shader.Find(texture!=null?"Unlit/Transparent Cutout":"Standard"));
        if(texture!=null){result.mainTexture=texture;result.SetFloat("_Cutoff",.12f);result.color=Color.white;}
        else{result.color=source.HasProperty("_Color")?source.color:Color.white;result.SetFloat("_Metallic",source.HasProperty("_Metallic")?source.GetFloat("_Metallic"):0);result.SetFloat("_Glossiness",.4f);}
        displayMaterials.Add(source,result);return result;
    }
    static Texture2D Render(AnimationClip clip,float time,int width=600,int height=660,Vector3 offset=default(Vector3))
    {
        clip.SampleAnimation(actor,time);actor.transform.position+=offset;
        foreach(var pair in bakedMeshes)pair.Key.BakeMesh(pair.Value);
        preview.BeginPreview(new Rect(0,0,width,height),GUIStyle.none);
        var focus=closeUp!=null?closeUp.position:actor.transform.position+Vector3.up*.91f;
        preview.camera.transform.position=focus+cameraOffset;
        preview.camera.transform.LookAt(focus);preview.camera.orthographic=true;preview.camera.orthographicSize=zoom;
        preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.068f,.082f,.115f);preview.camera.nearClipPlane=.02f;preview.camera.farClipPlane=60;
        preview.Render(true);
        var texture=preview.EndPreview() as RenderTexture;var prior=RenderTexture.active;RenderTexture.active=texture;
        var output=new Texture2D(width,height,TextureFormat.RGB24,false);output.ReadPixels(new Rect(0,0,width,height),0,0);output.Apply();RenderTexture.active=prior;return output;
    }
    static void Floor()
    {
        var floor=GameObject.CreatePrimitive(PrimitiveType.Plane);Object.DestroyImmediate(floor.GetComponent<Collider>());floor.transform.position=new Vector3(0,-.004f,0);floor.transform.localScale=Vector3.one*3;
        var material=new Material(Shader.Find("Standard"));material.color=new Color(.13f,.16f,.21f);floor.GetComponent<Renderer>().sharedMaterial=material;preview.AddSingleGO(floor);
        var lineMaterial=new Material(Shader.Find("Unlit/Color"));lineMaterial.color=new Color(.24f,.30f,.39f);
        for(int axis=0;axis<2;axis++)for(int n=-12;n<=12;n++)
        {
            var go=new GameObject("Grid");var line=go.AddComponent<LineRenderer>();line.sharedMaterial=lineMaterial;line.widthMultiplier=.006f;line.positionCount=2;line.useWorldSpace=true;
            line.SetPositions(axis==0?new[]{new Vector3(n,0,-12),new Vector3(n,0,12)}:new[]{new Vector3(-12,0,n),new Vector3(12,0,n)});preview.AddSingleGO(go);
        }
    }
    public static void Run()
    {
        try
        {
            if(!AuditKeqingMotion.Check())throw new Exception("Motion pose/grounding audit failed; see keqing-naturalness-audit.txt");
            Directory.CreateDirectory("KeqingPreviews");Directory.CreateDirectory("KeqingPreviews/Frames");
            preview=new PreviewRenderUtility();preview.ambientColor=new Color(.7f,.72f,.8f);
            preview.lights[0].intensity=1.4f;preview.lights[0].transform.rotation=Quaternion.Euler(40,-25,0);preview.lights[1].intensity=.7f;preview.lights[1].transform.rotation=Quaternion.Euler(10,145,0);
            actor=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(BuildKeqingRootMotion.Folder+"/KeqingRootMotionPlayer.prefab"));
            foreach(var b in actor.GetComponentsInChildren<MonoBehaviour>())b.enabled=false;
            actor.GetComponent<Animator>().enabled=false;
            foreach(var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>())skin.updateWhenOffscreen=true;
            foreach(var r in actor.GetComponentsInChildren<Renderer>())r.sharedMaterials=r.sharedMaterials.Select(Display).ToArray();
            foreach(var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>())
            {
                var go=new GameObject("BakedPreview");go.transform.SetParent(skin.transform,false);
                var mesh=new Mesh();go.AddComponent<MeshFilter>().sharedMesh=mesh;
                go.AddComponent<MeshRenderer>().sharedMaterials=skin.sharedMaterials;
                bakedMeshes.Add(skin,mesh);skin.enabled=false;
            }
            preview.AddSingleGO(actor);Floor();
            var clips=AssetDatabase.FindAssets("t:AnimationClip",new[]{BuildKeqingRootMotion.Folder+"/Animations"}).Select(g=>AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(g))).ToDictionary(c=>c.name);
            Directory.CreateDirectory("KeqingPreviews/Inspection");
            foreach(var clip in clips.Values)
            {
                var inspection=new Texture2D(1500,660,TextureFormat.RGB24,false);
                for(int view=0;view<2;view++)for(int k=0;k<5;k++)
                {
                    cameraOffset=view==0?new Vector3(0,1,4.8f):new Vector3(4.8f,1,0);
                    var frame=Render(clip,clip.length*k/4f,300,330);
                    inspection.SetPixels(k*300,(1-view)*330,300,330,frame.GetPixels());Object.DestroyImmediate(frame);
                }
                inspection.Apply();File.WriteAllBytes("KeqingPreviews/Inspection/"+clip.name+".png",inspection.EncodeToPNG());Object.DestroyImmediate(inspection);
            }
            cameraOffset=new Vector3(2.6f,1.1f,4.1f);
            Directory.CreateDirectory("KeqingPreviews/Hands");zoom=.22f;
            closeUp=actor.GetComponentsInChildren<Transform>().Single(t=>t.name.EndsWith("joint_RightWrist"));
            foreach(string name in new[]{"Idle","Run","Attack3"})
            {
                var frame=Render(clips[name],name=="Idle"?0:clips[name].length*.5f,600,600);
                File.WriteAllBytes("KeqingPreviews/Hands/"+name+"Grip.png",frame.EncodeToPNG());Object.DestroyImmediate(frame);
            }
            closeUp=null;zoom=1.12f;
            var names=new[]{"Idle","Walk","Run","Attack1","Attack1","Attack1","Attack2","Attack2","Attack2","Attack3","Attack3","Attack3"};
            var times=new[]{0f,.15f,.15f,.22f,.38f,.56f,.24f,.42f,.61f,.29f,.50f,.74f};
            var sheet=new Texture2D(1800,2640,TextureFormat.RGB24,false);
            for(int i=0;i<names.Length;i++)
            {
                var frame=Render(clips[names[i]],times[i]);sheet.SetPixels((i%3)*600,(3-i/3)*660,600,660,frame.GetPixels());
                if(i==0)File.WriteAllBytes("KeqingPreviews/KeqingIdle.png",frame.EncodeToPNG());
                Object.DestroyImmediate(frame);
            }
            sheet.Apply();File.WriteAllBytes("KeqingPreviews/KeqingActionSheet.png",sheet.EncodeToPNG());Object.DestroyImmediate(sheet);
            int index=0;Vector3 origin=Vector3.zero;
            var sequence=new[]{"WalkStart","Walk","Walk","WalkStop","Idle","RunStart","Run","Run","RunStop","Idle","Attack1","Attack2","Attack3","Idle","TurnRight","Idle"};
            foreach(string name in sequence)
            {
                var clip=clips[name];float duration=name=="Idle"?.45f:clip.length;
                for(float t=0;t<duration;t+=1f/24f)
                {
                    var frame=Render(clip,t,800,880,origin);File.WriteAllBytes("KeqingPreviews/Frames/frame_"+(index++).ToString("D4")+".png",frame.EncodeToPNG());Object.DestroyImmediate(frame);
                }
                clip.SampleAnimation(actor,clip.length);origin+=actor.transform.position;
                if(name=="WalkStop"||name=="RunStop"||name=="Attack3"||name=="TurnRight")origin=Vector3.zero;
            }
            preview.Cleanup();File.WriteAllText("keqing-preview-result.txt","PASS: actual Keqing mesh and textured materials rendered in native Unity. Twelve-pose sheet and 24fps full locomotion/combat footage generated.");EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);File.WriteAllText("keqing-preview-result.txt","FAIL: "+e);EditorApplication.Exit(1);}
    }
}
