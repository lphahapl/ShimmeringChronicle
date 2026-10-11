using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

public static class WeaponProductionPreview
{
    static PreviewRenderUtility preview;
    static Dictionary<Material,Material> materials=new Dictionary<Material,Material>();
    static List<(SkinnedMeshRenderer,Mesh)> skins=new List<(SkinnedMeshRenderer,Mesh)>();
    static GameObject actor;
    static Material Display(Material original)
    {
        if(!original)return new Material(Shader.Find("Standard")){color=new Color(.52f,.57f,.62f)};
        if(materials.TryGetValue(original,out var result))return result;
        var serialized=new SerializedObject(original);var array=serialized.FindProperty("m_SavedProperties.m_TexEnvs");Texture texture=null;
        for(int n=0;n<array.arraySize;n++){var el=array.GetArrayElementAtIndex(n);if(el.FindPropertyRelative("first").stringValue=="_BaseMap"||el.FindPropertyRelative("first").stringValue=="_MainTex"){var found=el.FindPropertyRelative("second.m_Texture").objectReferenceValue as Texture;if(found)texture=found;}}
        bool character=original.name.Contains("diffuse");
        result=new Material(Shader.Find(character&&texture?"Unlit/Transparent Cutout":"Standard"));result.mainTexture=texture;result.color=Color.white;result.SetFloat("_Cutoff",.15f);result.SetFloat("_Metallic",character?0:.60f);result.SetFloat("_Glossiness",.6f);
        materials[original]=result;return result;
    }
    static void Setup(GameObject value,bool character)
    {
        actor=value;actor.transform.position=Vector3.zero;skins.Clear();
        foreach(var b in actor.GetComponentsInChildren<MonoBehaviour>())if(b)b.enabled=false;
        foreach(var a in actor.GetComponentsInChildren<Animator>())a.enabled=false;
        foreach(var r in actor.GetComponentsInChildren<Renderer>())r.sharedMaterials=r.sharedMaterials.Select(Display).ToArray();
        foreach(var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            var child=new GameObject("BakedPreview");child.transform.SetParent(skin.transform,false);child.transform.localScale=new Vector3(1/skin.transform.lossyScale.x,1/skin.transform.lossyScale.y,1/skin.transform.lossyScale.z);var mesh=new Mesh();child.AddComponent<MeshFilter>().sharedMesh=mesh;child.AddComponent<MeshRenderer>().sharedMaterials=skin.sharedMaterials;skin.enabled=false;skins.Add((skin,mesh));
        }
        preview=new PreviewRenderUtility();preview.ambientColor=new Color(.64f,.67f,.75f);preview.lights[0].intensity=1.6f;preview.lights[0].transform.rotation=Quaternion.Euler(32,-30,0);preview.lights[1].intensity=.6f;preview.lights[1].transform.rotation=Quaternion.Euler(20,145,0);preview.AddSingleGO(actor);
    }
    static Texture2D Render(Vector3 focus,Vector3 camera,float zoom,int w,int h)
    {
        foreach(var s in skins)s.Item1.BakeMesh(s.Item2);
        preview.BeginPreview(new Rect(0,0,w,h),GUIStyle.none);preview.camera.transform.position=focus+camera;preview.camera.transform.LookAt(focus);preview.camera.orthographic=true;preview.camera.orthographicSize=zoom;preview.camera.nearClipPlane=.01f;preview.camera.farClipPlane=100;preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.12f,.14f,.18f);preview.Render(true);
        var rt=preview.EndPreview() as RenderTexture;var prior=RenderTexture.active;RenderTexture.active=rt;var texture=new Texture2D(w,h,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,w,h),0,0);texture.Apply();RenderTexture.active=prior;return texture;
    }
    static void Finish(){preview.Cleanup();Object.DestroyImmediate(actor);foreach(var s in skins)Object.DestroyImmediate(s.Item2);}
    static void SampleWithEffect(AnimationClip clip,float time)
    {
        var weapon=actor.GetComponentInChildren<WeaponObj>(true);
        var effect=actor.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="AttackEffect");
        var hit=AnimationUtility.GetAnimationEvents(clip).LastOrDefault(e=>(e.functionName=="PublishPlayerCombo"||e.functionName=="PublishEnemyCombo")&&e.time<=time);
        if(!effect&&weapon&&weapon.effPos&&weapon.SO&&weapon.SO.combo.Count>0)
        {
            var attack=weapon.SO.combo[Mathf.Clamp(hit!=null?hit.intParameter:0,0,weapon.SO.combo.Count-1)];
            if(attack.eff){effect=Object.Instantiate(attack.eff,weapon.effPos,false).transform;effect.name="AttackEffect";}
        }
        if(!effect){clip.SampleAnimation(actor,time);return;}
        bool active=hit!=null&&time<=hit.time+.35f;effect.gameObject.SetActive(active);
        if(active)
        {
            var trail=effect.GetComponent<TrailRenderer>();trail.Clear();
            float from=Mathf.Max(hit.time,time-trail.time);
            for(int n=0;n<=20;n++){clip.SampleAnimation(actor,Mathf.Lerp(from,time,n/20f));trail.AddPosition(effect.position);}
            foreach(var particles in effect.GetComponentsInChildren<ParticleSystem>())particles.Simulate(.035f,true,true);
        }
        clip.SampleAnimation(actor,time);
    }
    static void Models()
    {
        foreach(string id in WeaponAssetProduction.Names.Take(4))
        {
            Setup(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(WeaponAssetProduction.Root+id+"/"+id+".prefab")),false);
            actor.transform.rotation=Quaternion.FromToRotation(Vector3.back,Vector3.up);
            var vertices=actor.GetComponentsInChildren<MeshFilter>().SelectMany(f=>f.sharedMesh.vertices.Select(v=>f.transform.TransformPoint(v))).ToArray();var bounds=new Bounds(vertices[0],Vector3.zero);foreach(var v in vertices)bounds.Encapsulate(v);var sheet=new Texture2D(1200,800,TextureFormat.RGB24,false);
            var cameras=new[]{new Vector3(0,0,4),new Vector3(4,0,0),new Vector3(0,0,-4)};
            File.AppendAllText("preview-bounds.txt",id+" "+bounds+"\n");
            for(int n=0;n<3;n++){var image=Render(bounds.center,cameras[n],bounds.size.y*1.15f,400,800);sheet.SetPixels(n*400,0,400,800,image.GetPixels());Object.DestroyImmediate(image);}
            sheet.Apply();File.WriteAllBytes("ProductionPreviews/"+id+"_model.png",sheet.EncodeToPNG());Object.DestroyImmediate(sheet);Finish();
        }
    }
    static void Character(string id,bool warrior)
    {
        string folder=WeaponAssetProduction.Root+id+"/Animations/"+(warrior?"Warrior":"Keqing")+"/";
        var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(warrior?"Assets/Prefabs/Player/Warrior/Character Warrior.prefab":"Assets/Prefabs/Player/KeqingRootMotion/WeaponPreviews/"+id+".prefab");
        var value=Object.Instantiate(prefab);
        if(warrior)
        {
            var hand=value.GetComponent<PlayerObj>().handPos;
            var weapon=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(WeaponAssetProduction.Root+id+"/"+id+".prefab"),hand,false);
            weapon.name=id;
        }
        Setup(value,true);var sheet=new Texture2D(1500,1800,TextureFormat.RGB24,false);
        string[] names={"Idle","Walk","Run","Attack1","Attack2","Attack3"};
        for(int row=0;row<6;row++)
        {
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(folder+names[row]+".anim");
            for(int k=0;k<5;k++)
            {
                SampleWithEffect(clip,Mathf.Min(clip.length,k*clip.length/4));
                var image=Render(actor.transform.position+Vector3.up*1.55f,new Vector3(2.1f,.5f,4),2.15f,300,300);sheet.SetPixels(k*300,(5-row)*300,300,300,image.GetPixels());Object.DestroyImmediate(image);
            }
        }
        sheet.Apply();File.WriteAllBytes("ProductionPreviews/"+id+(warrior?"_Warrior":"_Keqing")+"_motion.png",sheet.EncodeToPNG());Object.DestroyImmediate(sheet);
        if(id=="weapon_004"&&warrior)
        {
            Directory.CreateDirectory("ProductionPreviews/HeavyFrames");
            foreach(string name in new[]{"Walk","Run","Attack1","Attack2","Attack3"})
            {
                var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(folder+name+".anim");
                for(int frame=0;frame<24;frame++)
                {
                    SampleWithEffect(clip,clip.length*frame/24);
                    var image=Render(actor.transform.position+Vector3.up*1.55f,new Vector3(2.1f,.5f,4),2.15f,480,560);File.WriteAllBytes("ProductionPreviews/HeavyFrames/"+name+"_"+frame.ToString("D3")+".png",image.EncodeToPNG());Object.DestroyImmediate(image);
                }
            }
        }
        Finish();
    }
    static void Enemy()
    {
        var value=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/TestEnemy/TestEnemy.prefab"));
        Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(WeaponAssetProduction.Root+"EnemyWeapon_001/EnemyWeapon_001.prefab"),value.transform.Find("foot/Cube/hand"),false);
        Setup(value,false);var sheet=new Texture2D(1500,900,TextureFormat.RGB24,false);
        for(int row=0;row<3;row++)
        {
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(WeaponAssetProduction.Root+"EnemyWeapon_001/Animations/"+(row==0?"Idle":"EnemyWeapon_001_Attack0"+row)+".anim");
            for(int frame=0;frame<5;frame++){SampleWithEffect(clip,clip.length*frame/4);var image=Render(Vector3.up*1.1f,new Vector3(2,.5f,4),1.75f,300,300);sheet.SetPixels(frame*300,(2-row)*300,300,300,image.GetPixels());Object.DestroyImmediate(image);}
        }
        sheet.Apply();File.WriteAllBytes("ProductionPreviews/EnemyWeapon_001_motion.png",sheet.EncodeToPNG());Object.DestroyImmediate(sheet);Finish();
    }
    static void EffectSweep()
    {
        var sheet=new Texture2D(2400,1200,TextureFormat.RGB24,false);
        for(int index=0;index<WeaponAssetProduction.Names.Length;index++)
        {
            string id=WeaponAssetProduction.Names[index];Setup(Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(WeaponAssetProduction.Root+id+"/"+id+".prefab")),false);
            var effect=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(WeaponAssetProduction.Root+id+"/Effects/AttackEffect.prefab"),actor.transform.Find("TipPoint"),false).transform;effect.gameObject.SetActive(true);var trail=effect.GetComponent<TrailRenderer>();trail.Clear();
            var tip=actor.transform.Find("TipPoint");var alignment=Quaternion.FromToRotation(tip.localPosition.normalized,Vector3.up);float radius=tip.localPosition.magnitude*actor.transform.lossyScale.x;
            for(int frame=0;frame<=40;frame++){actor.transform.rotation=Quaternion.AngleAxis(Mathf.Lerp(-65,45,frame/40f),Vector3.forward)*alignment;trail.AddPosition(effect.position);}
            foreach(var particles in effect.GetComponentsInChildren<ParticleSystem>())particles.Simulate(.04f,true,true);
            var image=Render(Vector3.zero,new Vector3(0,.1f,8),Mathf.Max(1.35f,radius*1.3f),600,600);sheet.SetPixels(index%4*600,(1-index/4)*600,600,600,image.GetPixels());Object.DestroyImmediate(image);Finish();
        }
        sheet.Apply();File.WriteAllBytes("ProductionPreviews/WeaponEffects.png",sheet.EncodeToPNG());Object.DestroyImmediate(sheet);
    }
    public static void Run()
    {
        try{EnemyWeaponProduction.BuildAll();AssetDatabase.SaveAssets();Directory.CreateDirectory("ProductionPreviews");Models();foreach(string id in WeaponAssetProduction.Names.Take(3))Character(id,true);Enemy();EffectSweep();File.WriteAllText("preview-result.txt","Rendered four models, Warrior and enemy motions, eight tip-effect sweeps; Keqing deferred.");EditorApplication.Exit(0);}
        catch(Exception e){Debug.LogException(e);File.WriteAllText("preview-result.txt","FAIL: "+e);EditorApplication.Exit(1);}
    }
    public static void RunModels(){try{Models();EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}}
}
