using System; using System.IO; using System.Linq; using System.Collections.Generic; using UnityEditor; using UnityEngine;
public static class GroundAndClearanceMossstoneV2
{
 const string Hips="Rig/Pelvis",Torso=Hips+"/Torso";
 static float MinY(MeshFilter mesh){var m=mesh.transform.localToWorldMatrix;float min=float.MaxValue;foreach(var v in mesh.sharedMesh.vertices)min=Mathf.Min(min,m.MultiplyPoint3x4(v).y);return min;}
 static void Curve(AnimationClip c,string path,string prop,List<float> t,List<float> v)
 {var curve=new AnimationCurve(t.Select((x,i)=>new Keyframe(x,v[i])).ToArray());for(int i=0;i<curve.length;i++){AnimationUtility.SetKeyLeftTangentMode(curve,i,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(curve,i,AnimationUtility.TangentMode.Linear);}AnimationUtility.SetEditorCurve(c,EditorCurveBinding.FloatCurve(path,typeof(Transform),prop),curve);EditorUtility.SetDirty(c);}
 static float Value(AnimationClip c,string path,string axis,float t)=>AnimationUtility.GetEditorCurve(c,EditorCurveBinding.FloatCurve(path,typeof(Transform),"localEulerAnglesRaw."+axis)).Evaluate(t);
 static MeshCollider Collider(Transform root,string path,bool convex,List<MeshCollider> cleanup)
 {var t=root.Find(path);var collider=t.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=t.GetComponent<MeshFilter>().sharedMesh;collider.convex=convex;collider.enabled=false;cleanup.Add(collider);return collider;}
 static float Penetration(MeshCollider a,MeshCollider b)
 {return Physics.ComputePenetration(a,a.transform.position,a.transform.rotation,b,b.transform.position,b.transform.rotation,out var direction,out float distance)?distance:0;}
 public static void Process(GameObject root,List<AnimationClip> clips,GameObject blade)
 {
  var saved=root.GetComponentsInChildren<Transform>(true).ToDictionary(t=>t,t=>(t.localPosition,t.localRotation,t.localScale));
  var socket=root.transform.Find(Torso+"/RArm/RForearm/RHand/WeaponSocket");var weapon=UnityEngine.Object.Instantiate(blade,socket,false);var bladeMesh=weapon.GetComponentInChildren<MeshFilter>();
  var cleanup=new List<MeshCollider>();var bodies=new[]{Collider(root.transform,Torso+"/Mesh",false,cleanup),Collider(root.transform,Torso+"/Head/Mesh",false,cleanup)};
  var right=new[]{Collider(root.transform,Torso+"/RArm/RForearm/Mesh",true,cleanup),Collider(root.transform,Torso+"/RArm/RForearm/RHand/Mesh",true,cleanup)};
  var left=new[]{Collider(root.transform,Torso+"/LArm/LForearm/Mesh",true,cleanup),Collider(root.transform,Torso+"/LArm/LForearm/LHand/Mesh",true,cleanup)};
  var bladeCollider=bladeMesh.gameObject.AddComponent<MeshCollider>();bladeCollider.sharedMesh=bladeMesh.sharedMesh;bladeCollider.convex=true;bladeCollider.enabled=false;
  var hips=root.transform.Find(Hips);var feet=new[]{root.transform.Find(Hips+"/LLeg/LShin/LFoot/Mesh").GetComponent<MeshFilter>(),root.transform.Find(Hips+"/RLeg/RShin/RFoot/Mesh").GetComponent<MeshFilter>()};
  var report=new List<string>();float worst=0,minFoot=100,minBlade=100,maxArm=0,maxWrist=0;
  try
  {
   foreach(var c in clips)
   {
    bool isDie=c.name.EndsWith("_Die");bool useBlade=c.name.StartsWith("ReinforcedBlade_")||isDie;var t=new List<float>();var y=new List<float>();var rz=new List<float>();var lz=new List<float>();var wx=new List<float>();var dy=new List<float>();var kb=new List<float>();int count=Mathf.CeilToInt(c.length*60);
    var sampleTimes=Enumerable.Range(0,count+1).Select(i=>Mathf.Min(c.length,i/60f)).Concat(AnimationUtility.GetAnimationEvents(c).Select(e=>e.time)).Concat(AnimationUtility.GetCurveBindings(c).SelectMany(b=>AnimationUtility.GetEditorCurve(c,b).keys.Select(k=>k.time))).Distinct().OrderBy(x=>x).ToArray();
    foreach(float time in sampleTimes)
    {
     foreach(var pair in saved){pair.Key.localPosition=pair.Value.Item1;pair.Key.localRotation=pair.Value.Item2;pair.Key.localScale=pair.Value.Item3;}c.SampleAnimation(root,time);float foot=Mathf.Min(MinY(feet[0]),MinY(feet[1]));if(!isDie)hips.localPosition+=Vector3.up*(-foot+.005f);else {var rig=root.transform.Find("Rig");float low=root.GetComponentsInChildren<MeshFilter>().Min(m=>MinY(m));rig.localPosition+=Vector3.up*Mathf.Max(0f,.005f-low);}
     float r=Value(c,Torso+"/RArm","z",time),l=Value(c,Torso+"/LArm","z",time),w=Value(c,Torso+"/RArm/RForearm/RHand","x",time);
     var rArm=root.transform.Find(Torso+"/RArm");var lArm=root.transform.Find(Torso+"/LArm");var hand=root.transform.Find(Torso+"/RArm/RForearm/RHand");var leftHand=root.transform.Find(Torso+"/LArm/LForearm/LHand");var re=rArm.localEulerAngles;var le=lArm.localEulerAngles;var we=hand.localEulerAngles;
     float addR=0,addL=0,addW=0;
     for(int pass=0;pass<85;pass++)
     {
      float rp=right.Max(a=>bodies.Max(b=>Penetration(a,b))),lp=left.Max(a=>bodies.Max(b=>Penetration(a,b)));if(useBlade)rp=Mathf.Max(rp,bodies.Max(b=>Penetration(bladeCollider,b)));
      bool floor=useBlade&&MinY(bladeMesh)<.04f;
      if(rp<=.015f&&lp<=.015f&&!floor)break;
      if(rp>.015f){addR+=2;rArm.localRotation=Quaternion.Euler(re.x,re.y,r+addR);}
      if(lp>.015f){addL+=2;lArm.localRotation=Quaternion.Euler(le.x,le.y,l-addL);}
      if(floor){addW-=2;hand.localRotation=Quaternion.Euler(w+addW,we.y,we.z);}
     }
     float bend=Value(c,Hips+"/LLeg/LShin","x",time);
     if(c.name.EndsWith("GroundSlap"))
     {
      // Adjust squat depth offline, leaving the bent elbows and horizontal forearms intact.
      // Replant the feet after every change instead of translating the whole enemy upward.
      for(int pass=0;pass<640&&left.Concat(right).Min(a=>MinY(a.GetComponent<MeshFilter>()))<.003f;pass++)
      {
       bend=Mathf.Max(0,bend-.25f);
       foreach(string side in new[]{"L","R"})
       {root.transform.Find(Hips+"/"+side+"Leg").localRotation=Quaternion.Euler(-bend*.5f,0,0);root.transform.Find(Hips+"/"+side+"Leg/"+side+"Shin").localRotation=Quaternion.Euler(bend,0,0);root.transform.Find(Hips+"/"+side+"Leg/"+side+"Shin/"+side+"Foot").localRotation=Quaternion.Euler(-bend*.5f,0,0);}
       hips.localPosition+=Vector3.up*(.005f-Mathf.Min(MinY(feet[0]),MinY(feet[1])));
      }
     }
     float pmax=Mathf.Max(right.Max(a=>bodies.Max(b=>Penetration(a,b))),left.Max(a=>bodies.Max(b=>Penetration(a,b))));if(useBlade)pmax=Mathf.Max(pmax,bodies.Max(b=>Penetration(bladeCollider,b)));
     worst=Mathf.Max(worst,pmax);minFoot=Mathf.Min(minFoot,Mathf.Min(MinY(feet[0]),MinY(feet[1])));if(useBlade)minBlade=Mathf.Min(minBlade,MinY(bladeMesh));maxArm=Mathf.Max(maxArm,Mathf.Max(addR,addL));maxWrist=Mathf.Max(maxWrist,-addW);
     if(pmax>.03f||(useBlade&&MinY(bladeMesh)<.015f))throw new Exception(c.name+" unresolved clearance t="+time+" depth="+pmax+" bladeMin="+MinY(bladeMesh));
     t.Add(time);dy.Add(root.transform.Find("Rig").localPosition.y);y.Add(hips.localPosition.y);rz.Add(r+addR);lz.Add(l-addL);wx.Add(w+addW);kb.Add(bend);
     if(c.name.EndsWith("GroundSlap")&&AnimationUtility.GetAnimationEvents(c).Any(e=>e.functionName=="PublishEnemyCombo"&&Mathf.Abs(e.time-time)<.00001f))report.Add(c.name+" authored hit: fore="+MinY(left[0].GetComponent<MeshFilter>())+" hand="+MinY(left[1].GetComponent<MeshFilter>())+" foot="+MinY(feet[0])+" knee="+bend+" hip="+hips.localPosition.y+" arm="+rArm.localEulerAngles+" blade="+MinY(bladeMesh));
    }
    if(isDie)Curve(c,"Rig","m_LocalPosition.y",t,dy);else Curve(c,Hips,"m_LocalPosition.y",t,y);Curve(c,Torso+"/RArm","localEulerAnglesRaw.z",t,rz);Curve(c,Torso+"/LArm","localEulerAnglesRaw.z",t,lz);if(useBlade)Curve(c,Torso+"/RArm/RForearm/RHand","localEulerAnglesRaw.x",t,wx);
    if(c.name.EndsWith("GroundSlap"))foreach(string side in new[]{"L","R"})
    {Curve(c,Hips+"/"+side+"Leg","localEulerAnglesRaw.x",t,kb.Select(v=>-v*.5f).ToList());Curve(c,Hips+"/"+side+"Leg/"+side+"Shin","localEulerAnglesRaw.x",t,kb);Curve(c,Hips+"/"+side+"Leg/"+side+"Shin/"+side+"Foot","localEulerAnglesRaw.x",t,kb.Select(v=>-v*.5f).ToList());}
    report.Add(c.name+": "+t.Count+" sampled poses; grounded soles and hands/forearms/blade clear of torso/head.");
   }
   report.Add("Worst torso/head penetration: "+worst+"m; min foot: "+minFoot+"m; min blade: "+minBlade+"m; maximum added arm clearance: "+maxArm+"deg; wrist floor correction: "+maxWrist+"deg.");
   File.WriteAllLines("v2-clearance-result.txt",report);
  }
  finally
  {foreach(var c in cleanup)UnityEngine.Object.DestroyImmediate(c);UnityEngine.Object.DestroyImmediate(weapon);foreach(var pair in saved){pair.Key.localPosition=pair.Value.Item1;pair.Key.localRotation=pair.Value.Item2;pair.Key.localScale=pair.Value.Item3;}}
 }
}
