using System; using System.IO; using System.Linq; using UnityEditor; using UnityEngine;
public static class ReviseReinforcedSentinel
{
 static string Rename(string s)=>s.Replace("MossstoneSentinelV2","ReinforcedSentinel").Replace("MossstoneFistsV2","ReinforcedFists").Replace("MossstoneBladeV2","ReinforcedBlade").Replace("SentinelV2_","Reinforced_").Replace("FistsV2_","ReinforcedFists_").Replace("BladeV2_","ReinforcedBlade_").Replace("Attack02_Slam","Attack02_GroundSlap").Replace("Attack03_Cleave","Attack03_GroundSlap");
 public static void Run()
 {
  try
  {
   if(!Application.dataPath.Replace('\\','/').EndsWith("/Temp/MossstoneV2Build/Assets"))throw new Exception("Use isolated project only");
   foreach(var old in new[]{"Assets/Prefabs/Enemies/MossstoneSentinelV2","Assets/Prefabs/Items/MossstoneFistsV2","Assets/Prefabs/Items/MossstoneBladeV2"})
   {
    string folder=Rename(old);
    if(AssetDatabase.IsValidFolder(old)){string error=AssetDatabase.MoveAsset(old,folder);if(error!="")throw new Exception(error);}
    foreach(string path in Directory.GetFiles(folder,"*",SearchOption.AllDirectories).Where(p=>!p.EndsWith(".meta")))
    {
     string next=Rename(path.Replace('\\','/'));
     if(next!=path.Replace('\\','/')){string error=AssetDatabase.MoveAsset(path.Replace('\\','/'),next);if(error!="")throw new Exception(error);}
     var asset=AssetDatabase.LoadMainAssetAtPath(next);if(asset&&!(asset is GameObject)){asset.name=Rename(asset.name);EditorUtility.SetDirty(asset);}
    }
   }
   foreach(var pair in new[]{(BuildMossstoneSentinelV2.Folder,"ReinforcedSentinel"),(BuildMossstoneSentinelV2.Fists,"ReinforcedFists"),(BuildMossstoneSentinelV2.Blade,"ReinforcedBlade")})
   {
    var root=PrefabUtility.LoadPrefabContents(pair.Item1+"/"+pair.Item2+".prefab");
    try{root.name=pair.Item2;var obj=root.GetComponent<EnemyObj>();if(obj)obj.enemyName.text="强化守卫";PrefabUtility.SaveAsPrefabAsset(root,pair.Item1+"/"+pair.Item2+".prefab");}
    finally{PrefabUtility.UnloadPrefabContents(root);}
   }
   var fists=AssetDatabase.LoadAssetAtPath<WeaponSO>(BuildMossstoneSentinelV2.Fists+"/ReinforcedFists.asset");fists.id="enemy_reinforced_fists";fists.itemName="强化守卫·空手";fists.description="左手重拳、弯腰双掌拍地。";EditorUtility.SetDirty(fists);
   fists.combo[1].judgeOffset=new Vector3(0,.45f,1.05f);
   var blade=AssetDatabase.LoadAssetAtPath<WeaponSO>(BuildMossstoneSentinelV2.Blade+"/ReinforcedBlade.asset");blade.id="enemy_reinforced_blade";blade.itemName="强化守卫·长石刀";blade.description="小幅前移冲撞、横斩、正面左掌拍地。";blade.combo[2].judgeOffset=new Vector3(-.55f,.45f,1.1f);EditorUtility.SetDirty(blade);
   foreach(string suffix in new[]{"","_Blade"})
   {var so=AssetDatabase.LoadAssetAtPath<EnemySO>(BuildMossstoneSentinelV2.Folder+"/ReinforcedSentinel"+suffix+".asset");so.enemyID="Enemy_ReinforcedSentinel"+suffix;so.targetName="强化守卫";so.description=suffix==""?"重型石质守卫，慢速移动，左拳与双掌拍地二连击。":"强化守卫长石刀配置：小幅冲撞、横斩、左掌拍地三连击。";EditorUtility.SetDirty(so);}
   BuildMossstoneSentinelV2.ReviseAttacks();
   PoseReport();AssetDatabase.SaveAssets();File.WriteAllText("reinforced-revision-result.txt","PASS: GUID-preserving rename, revised left punch, small charge, forward palm ground slaps.\n");EditorApplication.Exit(0);
  }
  catch(Exception e){File.WriteAllText("reinforced-revision-result.txt","FAIL: "+e);Debug.LogException(e);EditorApplication.Exit(1);}
 }
 static float Min(MeshFilter mesh)=>mesh.sharedMesh.vertices.Min(v=>mesh.transform.TransformPoint(v).y);
 public static void RunForearm()
 {
  try
  {
   if(!Application.dataPath.Replace('\\','/').EndsWith("/Temp/MossstoneV2Build/Assets"))throw new Exception("Use isolated project only");
   BuildMossstoneSentinelV2.ReviseAttacks();PoseReport();AssetDatabase.SaveAssets();
   File.WriteAllText("reinforced-revision-result.txt","PASS: bent elbow forearm ground impacts; small forward root-motion charge preserved.\n");EditorApplication.Exit(0);
  }
  catch(Exception e){File.WriteAllText("reinforced-revision-result.txt","FAIL: "+e);Debug.LogException(e);EditorApplication.Exit(1);}
 }
 static void PoseReport()
 {
  var root=PrefabUtility.LoadPrefabContents(BuildMossstoneSentinelV2.Folder+"/ReinforcedSentinel.prefab");
  try
  {
   var report=new System.Collections.Generic.List<string>();
   foreach(var row in new[]{(BuildMossstoneSentinelV2.Fists,"ReinforcedFists_Attack01_Punch",.70f),(BuildMossstoneSentinelV2.Fists,"ReinforcedFists_Attack02_GroundSlap",1.02f),(BuildMossstoneSentinelV2.Blade,"ReinforcedBlade_Attack03_GroundSlap",1.51f)})
   {
    var c=AssetDatabase.LoadAssetAtPath<AnimationClip>(row.Item1+"/Animations/"+row.Item2+".anim");c.SampleAnimation(root,row.Item3);
    foreach(string side in new[]{"L","R"})
    {
     var hand=root.transform.Find("Rig/Pelvis/Torso/"+side+"Arm/"+side+"Forearm/"+side+"Hand/Mesh").GetComponent<MeshFilter>();
     float low=Min(hand),forward=hand.transform.position.z-root.transform.position.z;
     report.Add(c.name+" "+side+" palm minimum="+low+" position="+hand.transform.position+" relativeZ="+forward);
     bool slap=c.name.EndsWith("GroundSlap")&&(side=="L"||c.name.StartsWith("ReinforcedFists"));
     if(slap)
     {
      var arm=root.transform.Find("Rig/Pelvis/Torso/"+side+"Arm");var forearm=arm.Find(side+"Forearm");var contact=forearm.Find("Mesh").GetComponent<MeshFilter>();
      float foreLow=Min(contact),foreZ=forearm.position.z-root.transform.Find("Rig/Pelvis").position.z;
      float elbow=Vector3.Angle(arm.position-forearm.position,hand.transform.position-forearm.position);
      float slope=Mathf.Abs(Vector3.Dot(forearm.TransformDirection(Vector3.down),Vector3.up));
      report.Add(c.name+" "+side+" forearm minimum="+foreLow+" relativeZ="+foreZ+" elbow="+elbow+" horizontal deviation="+slope);
      var foot=root.transform.Find("Rig/Pelvis/LLeg/LShin/LFoot/Mesh").GetComponent<MeshFilter>();report.Add("sampled hip="+root.transform.Find("Rig/Pelvis").localPosition.y+" foot="+Min(foot)+" knee="+root.transform.Find("Rig/Pelvis/LLeg/LShin").localEulerAngles+" arm="+arm.localEulerAngles+" fore="+forearm.localEulerAngles);File.WriteAllLines("reinforced-contact-result.txt",report);
      if(foreLow<-.002f||foreLow>.02f||foreZ<.3f||elbow<75f||elbow>115f||slope>.05f)throw new Exception("Bent forearm must contact ground in front of body: "+c.name+" "+side+" height="+foreLow+" elbow="+elbow);
     }
     if(c.name.EndsWith("Punch")&&side=="L"&&forward<1.35f)throw new Exception("Left punch must extend forward");
    }
    float floor=100;int count=Mathf.CeilToInt(c.length*60);
    for(int frame=0;frame<=count;frame++)
    {
     c.SampleAnimation(root,Mathf.Min(c.length,frame/60f));
     foreach(string side in new[]{"L","R"})
     {var fore=root.transform.Find("Rig/Pelvis/Torso/"+side+"Arm/"+side+"Forearm");floor=Mathf.Min(floor,Mathf.Min(Min(fore.Find("Mesh").GetComponent<MeshFilter>()),Min(fore.Find(side+"Hand/Mesh").GetComponent<MeshFilter>())));}
     if(c.name.EndsWith("GroundSlap")&&Mathf.Abs(Mathf.DeltaAngle(0,root.transform.Find("Rig/Pelvis/Torso").localEulerAngles.y))>.01f)throw new Exception("Ground slap must keep torso facing forward");
    }
    report.Add(c.name+" all-frame forearm/hand floor clearance="+floor);
    File.WriteAllLines("reinforced-contact-result.txt",report);
    if(floor<-.005f)throw new Exception("Arm penetrates floor "+c.name+" "+floor);
   }
   File.WriteAllLines("reinforced-contact-result.txt",report);
  }
  finally {PrefabUtility.UnloadPrefabContents(root);}
 }
}
