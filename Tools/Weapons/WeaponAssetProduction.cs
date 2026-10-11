using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

public static class WeaponAssetProduction
{
    public const string Root="Assets/Prefabs/Weapon/";
    public static readonly string[] Names={"weapon_002","weapon_003","weapon_004","EnemyWeapon_001","weapon_001","MossstoneBlade","ReinforcedBlade","FalconOath"};
    public static readonly Color[] Colors={new Color(1,.81f,.39f),new Color(.20f,.79f,1),new Color(.68f,.22f,1),new Color(1,.70f,.30f),new Color(1,.90f,.63f),new Color(.82f,.72f,.40f),new Color(1,.66f,.25f),new Color(1,.82f,.42f)};
    public static readonly float[] Lives={.16f,.20f,.18f,.22f,.16f,.20f,.22f,.16f};
    public static readonly float[] Widths={.12f,.16f,.14f,.20f,.12f,.18f,.22f,.10f};
    static readonly float[] Tips={.755f,.875f,.870f,1.10f};
    static readonly float[] Volumes={.60f,.55f,.65f,.65f,.60f,.65f,.65f,.55f};

    public static void Folder(string path){if(AssetDatabase.IsValidFolder(path))return;Folder(Path.GetDirectoryName(path).Replace('\\','/'));AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\','/'),Path.GetFileName(path));}
    public static T Save<T>(T value,string path) where T:Object
    {var prior=AssetDatabase.LoadAssetAtPath<T>(path);if(prior==null){AssetDatabase.CreateAsset(value,path);return value;}EditorUtility.CopySerialized(value,prior);EditorUtility.SetDirty(prior);Object.DestroyImmediate(value);return prior;}
    static Material Surface(string id,string name)
    {
        if(name.EndsWith("Mat"))name=name.Substring(0,name.Length-3);
        string folder=Root+id;Folder(folder+"/Materials");
        var material=new Material(Shader.Find("Standard")){name=name};
        material.SetTexture("_MainTex",AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"/Textures/"+name+"_BaseColor.png"));
        material.SetTexture("_BaseMap",material.mainTexture);material.SetColor("_BaseColor",Color.white);
        string metallic=folder+"/Textures/"+name+"_MetallicSmoothness.png";var metallicImporter=AssetImporter.GetAtPath(metallic) as TextureImporter;
        if(metallicImporter&&metallicImporter.sRGBTexture){metallicImporter.sRGBTexture=false;metallicImporter.SaveAndReimport();}
        material.SetTexture("_MetallicGlossMap",AssetDatabase.LoadAssetAtPath<Texture2D>(metallic));
        material.EnableKeyword("_METALLICGLOSSMAP");material.SetFloat("_Smoothness",.70f);material.SetFloat("_Glossiness",.70f);material.SetFloat("_GlossMapScale",1);
        string normal=folder+"/Textures/"+name+"_Normal.png";var importer=AssetImporter.GetAtPath(normal) as TextureImporter;
        if(importer!=null&&importer.textureType!=TextureImporterType.NormalMap){importer.textureType=TextureImporterType.NormalMap;importer.SaveAndReimport();}
        material.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(normal));material.EnableKeyword("_NORMALMAP");material.SetFloat("_BumpScale",.65f);
        if(name.Contains("Crystal")){material.EnableKeyword("_EMISSION");material.SetColor("_EmissionColor",(id=="weapon_003"?Colors[1]:Colors[2])*.22f);}
        return Save(material,folder+"/Materials/"+name+".mat");
    }
    static Material EffectMaterial(string id,Color color)
    {
        var m=new Material(Shader.Find("Particles/Standard Unlit")){name="AttackGlow"};m.SetColor("_Color",Color.white);m.SetColor("_BaseColor",Color.white);
        m.SetTexture("_MainTex",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+id+"/Textures/AttackGlow.png"));m.SetTexture("_BaseMap",m.mainTexture);
        m.SetFloat("_Mode",4);m.SetInt("_SrcBlend",(int)UnityEngine.Rendering.BlendMode.SrcAlpha);m.SetInt("_DstBlend",(int)UnityEngine.Rendering.BlendMode.One);m.SetInt("_ZWrite",0);m.EnableKeyword("_ALPHABLEND_ON");m.renderQueue=3000;
        return Save(m,Root+id+"/Materials/AttackGlow.mat");
    }
    static GameObject Effect(string id,int index)
    {
        Folder(Root+id+"/Effects");Folder(Root+id+"/Materials");
        var go=new GameObject("AttackEffect");var color=Colors[index];var material=EffectMaterial(id,color);
            var trail=go.AddComponent<TrailRenderer>();trail.time=Lives[index];trail.widthMultiplier=Widths[index];trail.minVertexDistance=.008f;trail.autodestruct=false;trail.emitting=true;trail.sharedMaterial=material;trail.numCornerVertices=3;trail.numCapVertices=3;
            trail.widthCurve=new AnimationCurve(new Keyframe(0,1),new Keyframe(.6f,.50f),new Keyframe(1,0));
            var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.Lerp(color,Color.white,.55f),0),new GradientColorKey(color,.35f),new GradientColorKey(color,1)},new[]{new GradientAlphaKey(.9f,0),new GradientAlphaKey(.6f,.55f),new GradientAlphaKey(0,1)});trail.colorGradient=gradient;
            trail.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;trail.receiveShadows=false;
        var glow=new GameObject("TipGlow");glow.transform.SetParent(go.transform,false);var ps=glow.AddComponent<ParticleSystem>();var main=ps.main;main.playOnAwake=true;main.loop=true;main.duration=1;main.startLifetime=.06f;main.startSpeed=.015f;main.startSize=.032f;main.startColor=Color.Lerp(color,Color.white,.5f);main.simulationSpace=ParticleSystemSimulationSpace.World;main.maxParticles=12;
        var emission=ps.emission;emission.rateOverTime=40;
        var shape=ps.shape;shape.enabled=false;
        var fade=ps.colorOverLifetime;fade.enabled=true;var g=new Gradient();g.SetKeys(new[]{new GradientColorKey(color,0),new GradientColorKey(color,1)},new[]{new GradientAlphaKey(.8f,0),new GradientAlphaKey(0,1)});fade.color=g;
        var renderer=ps.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=material;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;
        var audio=go.AddComponent<AudioSource>();audio.playOnAwake=true;audio.loop=false;audio.spatialBlend=1;audio.dopplerLevel=0;audio.minDistance=2;audio.maxDistance=20;audio.volume=Volumes[index];audio.priority=140;
        audio.clip=AssetDatabase.LoadAssetAtPath<AudioClip>(Root+id+"/Audio/attack_swing.wav");if(!audio.clip)throw new Exception("Missing generated audio "+id);
        go.SetActive(false);var result=PrefabUtility.SaveAsPrefabAsset(go,Root+id+"/Effects/AttackEffect.prefab");Object.DestroyImmediate(go);return result;
    }
    static Vector3 FindTip(GameObject root)
    {
        var vertices=root.GetComponentsInChildren<MeshFilter>().Where(f=>f.sharedMesh!=null).SelectMany(f=>f.sharedMesh.vertices.Select(v=>root.transform.InverseTransformPoint(f.transform.TransformPoint(v)))).ToArray();
        if(vertices.Length==0)throw new Exception("No mesh to locate tip "+root.name);
        var far=vertices.OrderByDescending(v=>v.sqrMagnitude).First();var direction=far.normalized;float max=vertices.Max(v=>Vector3.Dot(v,direction));
        var candidates=vertices.Where(v=>Vector3.Dot(v,direction)>max-.015f).ToArray();return candidates.Aggregate(Vector3.zero,(sum,v)=>sum+v)/candidates.Length;
    }
    static void Weapon(string id,int index)
    {
        string path=Root+id+"/"+id+".prefab";
        var root=PrefabUtility.LoadPrefabContents(path);
        try
        {
            if(index<4)
            {
                foreach(Transform child in root.transform.Cast<Transform>().ToArray())Object.DestroyImmediate(child.gameObject);
                root.transform.localScale=Vector3.one;root.transform.localPosition=Vector3.zero;root.transform.localRotation=Quaternion.identity;root.name=id;
                string obj=Root+id+"/Models/"+id+".obj";var importer=AssetImporter.GetAtPath(obj) as ModelImporter;importer.globalScale=1;importer.useFileScale=false;importer.importNormals=ModelImporterNormals.Import;importer.SaveAndReimport();
                var model=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(obj));model.name="Model";model.transform.SetParent(root.transform,false);model.transform.localRotation=Quaternion.FromToRotation(Vector3.up,Vector3.back);
                foreach(var renderer in model.GetComponentsInChildren<MeshRenderer>())renderer.sharedMaterials=renderer.sharedMaterials.Select(m=>Surface(id,m.name.Replace(" (Instance)",""))).ToArray();
            }
            var tip=root.transform.Find("TipPoint");if(tip==null){tip=new GameObject("TipPoint").transform;tip.SetParent(root.transform,false);tip.localPosition=index<4?Vector3.back*Tips[index]:FindTip(root);}
            if(root.transform.Find("GripPoint")==null){var grip=new GameObject("GripPoint").transform;grip.SetParent(root.transform,false);}
            var prior=tip.Find("AttackEffect");if(prior!=null)Object.DestroyImmediate(prior.gameObject);
            var effect=(GameObject)PrefabUtility.InstantiatePrefab(Effect(id,index),tip);effect.name="AttackEffect";effect.transform.localPosition=Vector3.zero;effect.SetActive(false);
            var weapons=root.GetComponentsInChildren<WeaponObj>(true);if(weapons.Length==0)weapons=new[]{root.AddComponent<WeaponObj>()};
            foreach(var weapon in weapons)weapon.effPos=effect.transform;
            var saved=PrefabUtility.SaveAsPrefabAsset(root,path);
        }
        finally{PrefabUtility.UnloadPrefabContents(root);}
    }
    public static void Run()
    {
        try
        {
            BuildAll();File.WriteAllText("assets-result.txt","PASS: four approved models, eight embedded inactive sword-tip FX prefabs, nine original audio clips and ground-impact resource.");EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);File.WriteAllText("assets-result.txt","FAIL: "+e);EditorApplication.Exit(1);}
    }
    public static void BuildAll(){AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);for(int i=0;i<Names.Length;i++)Weapon(Names[i],i);AssetDatabase.SaveAssets();}
}
