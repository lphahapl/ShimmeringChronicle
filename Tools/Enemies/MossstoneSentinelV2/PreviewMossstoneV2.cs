using System; using System.IO; using System.Linq; using UnityEditor; using UnityEngine;
public static class PreviewMossstoneV2
{
 static PreviewRenderUtility preview;static Material floorMaterial,lineMaterial;static string Out="PreviewsV2";
 static GameObject Actor(bool old,bool weapon)
 {
  string body=old?"Assets/Prefabs/Enemies/MossstoneSentinel/MossstoneSentinel.prefab":BuildMossstoneSentinelV2.Folder+"/ReinforcedSentinel.prefab";
  var actor=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(body));actor.hideFlags=HideFlags.HideAndDontSave;
  foreach(var b in actor.GetComponentsInChildren<MonoBehaviour>(true))b.enabled=false;actor.GetComponent<Animator>().enabled=false;
  foreach(var canvas in actor.GetComponentsInChildren<Canvas>(true))canvas.gameObject.SetActive(false);
  preview.AddSingleGO(actor);
  if(weapon)
  {
   string path=old?"Assets/Prefabs/Items/MossstoneBlade/MossstoneBlade.prefab":BuildMossstoneSentinelV2.Blade+"/ReinforcedBlade.prefab";
   var weaponGo=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(path),actor.GetComponent<EnemyObj>().handPos,false);foreach(var m in weaponGo.GetComponentsInChildren<MonoBehaviour>())m.enabled=false;
  }
  return actor;
 }
 static void Init(float distance,int width=900,int height=900)
 {
  preview=new PreviewRenderUtility();preview.camera.fieldOfView=34;preview.camera.nearClipPlane=.05f;preview.camera.farClipPlane=80;preview.camera.clearFlags=CameraClearFlags.Color;preview.camera.backgroundColor=new Color(.045f,.06f,.08f);preview.ambientColor=new Color(.32f,.35f,.38f);
  preview.lights[0].intensity=2.8f;preview.lights[0].color=new Color(1,.91f,.79f);preview.lights[0].transform.rotation=Quaternion.Euler(32,-32,0);
  preview.lights[1].intensity=1.7f;preview.lights[1].color=new Color(.48f,.77f,1);preview.lights[1].transform.rotation=Quaternion.Euler(12,138,0);
  floorMaterial=new Material(Shader.Find("Standard")){color=new Color(.065f,.082f,.105f)};floorMaterial.SetFloat("_Glossiness",0);
  lineMaterial=new Material(Shader.Find("Standard")){color=new Color(.12f,.15f,.17f)};lineMaterial.SetFloat("_Glossiness",0);
  var floor=GameObject.CreatePrimitive(PrimitiveType.Plane);floor.transform.localScale=Vector3.one*3;floor.transform.position=new Vector3(0,0,0);floor.GetComponent<Renderer>().sharedMaterial=floorMaterial;preview.AddSingleGO(floor);
  for(int i=-6;i<=6;i++)foreach(bool vertical in new[]{false,true})
  {var line=GameObject.CreatePrimitive(PrimitiveType.Cube);line.transform.position=new Vector3(vertical?i*2:0,.001f,vertical?0:i*2);line.transform.localScale=vertical?new Vector3(.009f,.003f,24):new Vector3(24,.003f,.009f);line.GetComponent<Renderer>().sharedMaterial=lineMaterial;preview.AddSingleGO(line);}
 }
 static void Camera(Vector3 center,float distance){preview.camera.transform.position=center+new Vector3(.48f,.15f,.865f).normalized*distance;preview.camera.transform.LookAt(center);}
 static void Save(string file,int width=900,int height=900)
 {
  preview.BeginStaticPreview(new Rect(0,0,width,height));preview.camera.aspect=(float)width/height;preview.Render(true);var result=preview.EndStaticPreview();File.WriteAllBytes(file,result.EncodeToPNG());UnityEngine.Object.DestroyImmediate(result);
 }
 static void Cleanup(){preview.Cleanup();UnityEngine.Object.DestroyImmediate(floorMaterial);UnityEngine.Object.DestroyImmediate(lineMaterial);}
 static AnimationClip Clip(string folder,string name)=>AssetDatabase.LoadAssetAtPath<AnimationClip>(folder+"/Animations/"+name+".anim");
 static void Still(string name,AnimationClip clip,float time,bool weapon)
 {
  Init(7.5f);var a=Actor(false,weapon);clip.SampleAnimation(a,time);Camera(a.transform.position+Vector3.up*(weapon?1.95f:1.6f),weapon?10.5f:8.4f);Save(Out+"/"+name+".png");Cleanup();
 }
 static void Film(string name,AnimationClip[] clips,bool weapon)
 {
  Init(9.5f);var a=Actor(false,weapon);var idle=Clip(BuildMossstoneSentinelV2.Folder,"Reinforced_Idle");int index=0;Vector3 accumulated=Vector3.zero;Directory.CreateDirectory(Out+"/"+name+"-frames");
  foreach(var c in new[]{idle}.Concat(clips).Concat(new[]{idle}))
  {
   float length=c==idle?.4f:c.length;int count=Mathf.CeilToInt(length*18);
   for(int i=0;i<count;i++)
   {c.SampleAnimation(a,Mathf.Min(c.length,i/18f));a.transform.position+=accumulated;Camera(new Vector3(0,1.65f,.8f),11.5f);Save(Out+"/"+name+"-frames/"+(index++).ToString("D4")+".png",600,600);}
   if(c!=idle){c.SampleAnimation(a,c.length);accumulated+=a.transform.position;}
  }
  Cleanup();
 }
 public static void Run()
 {
  try
  {
   PolishMossstoneV2.Apply();Directory.CreateDirectory(Out);
   var idle=Clip(BuildMossstoneSentinelV2.Folder,"Reinforced_Idle");var punch=Clip(BuildMossstoneSentinelV2.Fists,"ReinforcedFists_Attack01_Punch");var slam=Clip(BuildMossstoneSentinelV2.Fists,"ReinforcedFists_Attack02_GroundSlap");
   var charge=Clip(BuildMossstoneSentinelV2.Blade,"ReinforcedBlade_Attack01_Charge");var sweep=Clip(BuildMossstoneSentinelV2.Blade,"ReinforcedBlade_Attack02_Sweep");var cleave=Clip(BuildMossstoneSentinelV2.Blade,"ReinforcedBlade_Attack03_GroundSlap");
   Still("ReinforcedSentinel-unarmed",idle,0,false);Still("ReinforcedSentinel-blade",idle,0,true);
   Still("punch-windup",punch,.52f,false);Still("punch-overrun",punch,.86f,false);Still("slam-windup",slam,.72f,false);Still("slam-recovery",slam,1.4f,false);
   Still("charge-stride",charge,.90f,true);Still("charge-braking",charge,1.42f,true);Still("sweep-windup",sweep,.78f,true);Still("sweep-overrun",sweep,1.47f,true);Still("ground-slap-windup",cleave,1f,true);Still("ground-slap-contact",cleave,1.92f,true);
   Init(10);var old=Actor(true,false);Clip("Assets/Prefabs/Enemies/MossstoneSentinel","Sentinel_Idle").SampleAnimation(old,0);old.transform.position=new Vector3(1.8f,0,0);var updated=Actor(false,false);idle.SampleAnimation(updated,0);updated.transform.position=new Vector3(-1.8f,0,0);
   preview.camera.transform.position=new Vector3(0,2.35f,9.9f);preview.camera.transform.LookAt(new Vector3(0,1.5f,0));Save(Out+"/Mossstone-left-Reinforced-right.png",1500,1000);Cleanup();
   Film("fists-two-hit",new[]{punch,slam},false);Film("blade-three-hit",new[]{charge,sweep,cleave},true);
   File.WriteAllText("v2-preview-result.txt","PASS: native model comparison, 11 model/pose stills and 18fps footage of both complete combos rendered.");EditorApplication.Exit(0);
  }
  catch(Exception e){File.WriteAllText("v2-preview-result.txt","FAIL: "+e);Debug.LogException(e);EditorApplication.Exit(1);}
 }
}
