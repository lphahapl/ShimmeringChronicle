using System; using System.IO; using System.Linq; using UnityEditor; using UnityEngine;
/// <summary>Adds shared stone/moss textures and planar UVs to the new sentinel only.</summary>
public static class PolishMossstone
{
 static float Rand(int x,int y){return Mathf.Repeat(Mathf.Sin(x*127.1f+y*311.7f)*43758.5453f,1);}
 static void Texture(string name,bool moss)
 {
  var t=new Texture2D(512,512,TextureFormat.RGBA32,false);var colors=new Color[512*512];
  for(int y=0;y<512;y++)for(int x=0;x<512;x++)
  {
   float u=x/512f,v=y/512f,noise=Mathf.PerlinNoise(u*17+2.1f,v*17+6.3f);float grain=Mathf.PerlinNoise(u*83,v*83);
   if(moss){float n=Mathf.PerlinNoise(u*9,v*9);float alpha=n>.39f?1:0;colors[y*512+x]=new Color(.65f+noise*.35f,.7f+noise*.3f,.48f+grain*.22f,alpha);continue;}
   float nearest=100,next=100;int cx=Mathf.FloorToInt(u*5),cy=Mathf.FloorToInt(v*5);
   for(int dy=-1;dy<=1;dy++)for(int dx=-1;dx<=1;dx++)
   {int a=cx+dx,b=cy+dy;var seed=new Vector2(a+.2f+.6f*Rand((a+20)%5,(b+20)%5),b+.2f+.6f*Rand((b+20)%5,(a+23)%5));float d=(new Vector2(u*5,v*5)-seed).sqrMagnitude;if(d<nearest){next=nearest;nearest=d;}else if(d<next)next=d;}
   float value=.77f+noise*.17f+grain*.06f;if(Mathf.Sqrt(next)-Mathf.Sqrt(nearest)<.025f)value*=.5f;
   colors[y*512+x]=new Color(value,value,value,1);
  }
  t.SetPixels(colors);t.Apply();string path=BuildMossstoneSentinel.Folder+"/Materials/"+name+".png";File.WriteAllBytes(path,t.EncodeToPNG());UnityEngine.Object.DestroyImmediate(t);AssetDatabase.ImportAsset(path);
  var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Default;importer.wrapMode=TextureWrapMode.Repeat;importer.mipmapEnabled=true;importer.maxTextureSize=512;importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.SaveAndReimport();
  string matPath=BuildMossstoneSentinel.Folder+"/Materials/"+(moss?"Moss":"SlateStone")+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(matPath);var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(path);mat.SetTexture("_MainTex",texture);mat.SetTexture("_BaseMap",texture);
  if(moss){mat.SetFloat("_Mode",1);mat.SetFloat("_Cutoff",.5f);mat.SetFloat("_AlphaClip",1);mat.EnableKeyword("_ALPHATEST_ON");mat.SetOverrideTag("RenderType","TransparentCutout");mat.renderQueue=2450;}
  EditorUtility.SetDirty(mat);
 }
 public static void Run()
 {
  foreach(var folder in new[]{BuildMossstoneSentinel.Folder+"/Models",BuildMossstoneSentinel.Blade+"/Models"})
  foreach(var guid in AssetDatabase.FindAssets("t:Mesh",new[]{folder}))
  {
   var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(AssetDatabase.GUIDToAssetPath(guid));var vertices=mesh.vertices;var normals=mesh.normals;var uv=new Vector2[vertices.Length];
   for(int i=0;i<vertices.Length;i++){var n=normals[i];var p=vertices[i];if(Mathf.Abs(n.y)>=Mathf.Abs(n.x)&&Mathf.Abs(n.y)>=Mathf.Abs(n.z))uv[i]=new Vector2(p.x,p.z)*2.5f;else if(Mathf.Abs(n.x)>Mathf.Abs(n.z))uv[i]=new Vector2(p.z,p.y)*2.5f;else uv[i]=new Vector2(p.x,p.y)*2.5f;}
   mesh.uv=uv;EditorUtility.SetDirty(mesh);
  }
  Texture("StoneCracks",false);Texture("MossPatch",true);AssetDatabase.SaveAssets();File.WriteAllText("polish-result.txt","PASS: shared 512px stone/moss textures; planar UVs; cutout moss patches.");EditorApplication.Exit(0);
 }
}
