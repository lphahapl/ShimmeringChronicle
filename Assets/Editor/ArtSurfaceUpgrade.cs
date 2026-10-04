using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

[InitializeOnLoad]
public static class ArtSurfaceUpgrade
{
    const string Weapon="Assets/Art/Weapons/FalconOath";
    const string Character="Assets/Art/33 宵宫";
    const string Marker="Tools/WeaponConcepts/surface-upgrade-v2.txt";
    static ArtSurfaceUpgrade(){EditorApplication.delayCall+=RunOnce;}
    static void RunOnce()
    {
        if(EditorApplication.isPlayingOrWillChangePlaymode || File.Exists(Marker))return;
        try{Run();}catch(Exception e){Debug.LogException(e);}
    }
    [MenuItem("Tools/Art/Upgrade Falcon and Bind Yoimiya")]
    public static void Run()
    {
        var log=new StringBuilder();
        FalconOathAssetBuilder.Build();
        foreach(string name in new[]{"Steel","DarkSteel","Brass","Leather","Cloth"})
        {
            var mat=AssetDatabase.LoadAssetAtPath<Material>(Weapon+"/"+name+".mat");
            if(!mat)throw new Exception("Missing weapon material: "+name);
            foreach(string suffix in new[]{"BaseColor","MetallicSmoothness","Normal"})
            {
                string path=Weapon+"/Textures/"+name+"_"+suffix+".png";
                AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
                var importer=(TextureImporter)AssetImporter.GetAtPath(path);
                importer.textureType=suffix=="Normal"?TextureImporterType.NormalMap:TextureImporterType.Default;
                importer.sRGBTexture=suffix=="BaseColor";
                importer.alphaSource=TextureImporterAlphaSource.FromInput;
                importer.wrapMode=TextureWrapMode.Repeat;
                importer.anisoLevel=8;
                importer.textureCompression=TextureImporterCompression.Uncompressed;
                importer.SaveAndReimport();
                var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);
                mat.SetTexture(suffix=="BaseColor"?"_BaseMap":suffix=="Normal"?"_BumpMap":"_MetallicGlossMap",texture);
            }
            mat.SetColor("_BaseColor",Color.white);
            mat.SetFloat("_Metallic",1);
            mat.SetFloat("_Smoothness",1);
            mat.SetFloat("_SmoothnessTextureChannel",0);
            mat.SetFloat("_BumpScale",name=="Steel"?.30f:.6f);
            mat.SetFloat("_SpecularHighlights",1);mat.SetFloat("_EnvironmentReflections",1);
            mat.EnableKeyword("_NORMALMAP");mat.EnableKeyword("_METALLICSPECGLOSSMAP");
            mat.DisableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");
            EditorUtility.SetDirty(mat);
            log.AppendLine("PBR OK: "+name+" (base / metallic-smoothness / normal)");
        }
        var gem=AssetDatabase.LoadAssetAtPath<Material>(Weapon+"/Garnet.mat");
        gem.SetFloat("_Smoothness",.92f);gem.SetFloat("_Metallic",.12f);EditorUtility.SetDirty(gem);
        BindYoimiya(log);
        AssetDatabase.SaveAssets();
        RenderPreview(Weapon+"/FalconOath.prefab","Tools/WeaponConcepts/FalconOath-PBR.png",true);
        RenderPreview(Character+"/Yoimiya_URP.prefab","Tools/WeaponConcepts/Yoimiya-materials.png",false);
        File.WriteAllText(Marker,log.ToString());
        Debug.Log("[ArtSurfaceUpgrade] Complete\n"+log);
    }
    static void BindYoimiya(StringBuilder log)
    {
        string fbx=Character+"/宵宫.fbx";
        Directory.CreateDirectory(Character+"/Materials");AssetDatabase.Refresh();
        var importer=(ModelImporter)AssetImporter.GetAtPath(fbx);
        var matches=Regex.Matches(File.ReadAllText(fbx),"Material: \\d+, \"Material::([^\"]+)\"");
        if(matches.Count!=27)throw new Exception("Expected 27 Yoimiya material slots; found "+matches.Count);
        foreach(Match match in matches)
        {
            string name=match.Groups[1].Value;int index=int.Parse(name.Split('.')[0]);
            string tex=index<=8 || index==26?"面":index==10?"sph+":index<=12?"髮":index==23?"肌":"衣";
            var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(Character+"/tex/"+tex+".png");
            if(!texture)throw new Exception("Missing texture: "+tex);
            string path=Character+"/Materials/"+name+".mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!mat){mat=new Material(Shader.Find(index==10?"Universal Render Pipeline/Unlit":"Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,path);}
            mat.SetTexture("_BaseMap",texture);mat.SetTexture("_MainTex",texture);
            mat.SetColor("_BaseColor",index==10?new Color(.6f,.6f,.6f,.45f):Color.white);
            mat.SetFloat("_Cull",0);
            if(index==10)
            {
                // MMD's duplicate hair mesh contains a black additive highlight map.
                mat.SetFloat("_Surface",1);mat.SetFloat("_Blend",2);
                mat.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);mat.SetFloat("_DstBlend",(float)BlendMode.One);
                mat.SetFloat("_ZWrite",0);mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                mat.SetOverrideTag("RenderType","Transparent");mat.renderQueue=3000;
                mat.SetShaderPassEnabled("ShadowCaster",false);mat.SetShaderPassEnabled("DepthOnly",false);
            }
            else
            {
                mat.SetFloat("_Metallic",0);mat.SetFloat("_Smoothness",index==12?.35f:.16f);
                mat.SetFloat("_AlphaClip",1);mat.SetFloat("_Cutoff",.35f);mat.EnableKeyword("_ALPHATEST_ON");
                mat.SetOverrideTag("RenderType","TransparentCutout");mat.renderQueue=2450;
            }
            EditorUtility.SetDirty(mat);
            importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),name),mat);
            log.AppendLine("Yoimiya "+name+" -> "+tex+".png"+(index==10?" (additive hair highlights)":""));
        }
        importer.SaveAndReimport();
        var model=AssetDatabase.LoadAssetAtPath<GameObject>(fbx);
        int slots=0;
        foreach(var renderer in model.GetComponentsInChildren<Renderer>(true))
            foreach(var mat in renderer.sharedMaterials)
            {
                if(!mat || !AssetDatabase.GetAssetPath(mat).StartsWith(Character+"/Materials/") || !mat.GetTexture("_BaseMap"))throw new Exception("Unbound Yoimiya material");
                slots++;
            }
        if(slots!=27)throw new Exception("Unexpected bound material count: "+slots);
        var instance=UnityEngine.Object.Instantiate(model);instance.name="Yoimiya_URP";
        try{PrefabUtility.SaveAsPrefabAsset(instance,Character+"/Yoimiya_URP.prefab");}
        finally{UnityEngine.Object.DestroyImmediate(instance);}
        log.AppendLine("PASS: all 27 Yoimiya renderer slots have mapped textures.");
    }
    static void RenderPreview(string path,string output,bool weapon)
    {
        var preview=new PreviewRenderUtility();
        try
        {
            var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path));
            preview.AddSingleGO(go);
            var renderers=go.GetComponentsInChildren<Renderer>();
            var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);
            var center=weapon?bounds.center+Vector3.up*bounds.size.y*.33f:bounds.center;
            float size=weapon?bounds.size.y*.22f:bounds.size.y*.59f;
            File.WriteAllText(output+".txt","Bounds: "+bounds+"; target: "+center+"; root: "+go.transform.position+"; renderers: "+renderers.Length);
            preview.camera.orthographic=true;preview.camera.orthographicSize=size;
            preview.camera.nearClipPlane=.001f;preview.camera.farClipPlane=1000;
            preview.camera.transform.position=center+new Vector3(weapon?.75f:0,weapon?.03f:0,Mathf.Max(3,bounds.size.y*2));
            preview.camera.transform.LookAt(center);
            preview.camera.clearFlags=CameraClearFlags.SolidColor;preview.camera.backgroundColor=new Color(.075f,.09f,.12f);
            preview.ambientColor=new Color(.32f,.34f,.4f);
            preview.lights[0].intensity=1.5f;preview.lights[0].color=new Color(1,.94f,.85f);
            preview.lights[0].transform.rotation=Quaternion.Euler(28,145,0);
            preview.lights[1].intensity=1.0f;preview.lights[1].color=new Color(.66f,.80f,1);
            preview.lights[1].transform.rotation=Quaternion.Euler(-15,-35,0);
            if(weapon)
            {
                var probeGO=new GameObject("Preview studio reflection");preview.AddSingleGO(probeGO);
                probeGO.transform.position=bounds.center;
                var probe=probeGO.AddComponent<ReflectionProbe>();probe.mode=ReflectionProbeMode.Custom;
                probe.size=Vector3.one*100;probe.intensity=1;probe.customBakedTexture=StudioCube();
            }
            preview.BeginStaticPreview(new Rect(0,0,1100,1400));
            preview.Render(true);
            var image=preview.EndStaticPreview();
            File.WriteAllBytes(output,image.EncodeToPNG());UnityEngine.Object.DestroyImmediate(image);
        }
        finally{preview.Cleanup();}
    }
    static Cubemap StudioCube()
    {
        string path=Weapon+"/StudioReflection.cubemap";
        var existing=AssetDatabase.LoadAssetAtPath<Cubemap>(path);if(existing)return existing;
        const int n=128;var cube=new Cubemap(n,TextureFormat.RGBAHalf,true);
        for(int face=0;face<6;face++)
        {
            var colors=new Color[n*n];
            for(int y=0;y<n;y++)for(int x=0;x<n;x++)
            {
                float u=(x+.5f)/n*2-1,v=(y+.5f)/n*2-1;
                Vector3 d=face==0?new Vector3(1,-v,-u):face==1?new Vector3(-1,-v,u):face==2?new Vector3(u,1,v):face==3?new Vector3(u,-1,-v):face==4?new Vector3(u,-v,1):new Vector3(-u,-v,-1);
                d.Normalize();
                float panel=3.0f*Mathf.Exp(-Mathf.Pow((d.x+.50f)/.14f,2)-Mathf.Pow((d.y-.15f)/.72f,4))*Mathf.Max(0,d.z);
                float rim=2.2f*Mathf.Exp(-Mathf.Pow((d.x-.65f)/.10f,2)-Mathf.Pow(d.y/.8f,4))*Mathf.Max(0,d.z);
                float sky=.10f+.20f*Mathf.Max(0,d.y);
                colors[y*n+x]=new Color(sky+panel+rim*.65f,sky+panel*.96f+rim*.8f,sky+panel*.88f+rim,1);
            }
            cube.SetPixels(colors,(CubemapFace)face);
        }
        cube.Apply(true);AssetDatabase.CreateAsset(cube,path);return cube;
    }
}
