using System; using System.IO; using System.Linq; using System.Collections.Generic; using UnityEditor; using UnityEngine;
/// <summary>Offline foot placement and two-bone solving. No runtime IK component is added.</summary>
public static class StanceAndStrideSentinel
{
 const string Hips="Rig/Pelvis";
 static AnimationCurve Shape(float[] t,float[] v)
 {var c=new AnimationCurve(t.Select((x,i)=>new Keyframe(x,v[i])).ToArray());for(int i=0;i<c.length;i++){AnimationUtility.SetKeyLeftTangentMode(c,i,AnimationUtility.TangentMode.Auto);AnimationUtility.SetKeyRightTangentMode(c,i,AnimationUtility.TangentMode.Auto);}return c;}
 static void Curve(AnimationClip clip,string path,string prop,List<float> t,List<float> v)
 {var c=new AnimationCurve(t.Select((x,i)=>new Keyframe(x,v[i])).ToArray());for(int i=0;i<c.length;i++){AnimationUtility.SetKeyLeftTangentMode(c,i,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(c,i,AnimationUtility.TangentMode.Linear);}AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(path,typeof(Transform),prop),c);}
 static float MinY(MeshFilter m)=>m.sharedMesh.vertices.Min(v=>m.transform.TransformPoint(v).y);
 static void Legs(Transform leg,Vector3 target,float squat)
 {
  string side=leg.name.Substring(0,1);var shin=leg.Find(side+"Shin");var foot=shin.Find(side+"Foot");
  Vector3 lower=foot.localPosition;float a=shin.localPosition.magnitude,b=lower.magnitude;var delta=target-leg.position;float distance=delta.magnitude;
  if(distance>a+b+.001f||distance<Mathf.Abs(a-b)+.001f)throw new Exception("Foot goal outside leg reach: "+leg.name+" "+distance);
  var dir=delta.normalized;float along=(a*a-b*b+distance*distance)/(2*distance),height=Mathf.Sqrt(Mathf.Max(0,a*a-along*along));
  var pole=Vector3.forward;
  var bend=(pole-dir*Vector3.Dot(pole,dir)).normalized;var knee=leg.position+dir*along+bend*height;
  leg.rotation=Quaternion.FromToRotation(Vector3.down,knee-leg.position);shin.rotation=Quaternion.FromToRotation(lower,target-knee);foot.rotation=Quaternion.identity;
 }
 public static void Process(GameObject root,List<AnimationClip> clips)
 {
  var saved=root.GetComponentsInChildren<Transform>(true).ToDictionary(t=>t,t=>(t.localPosition,t.localRotation,t.localScale));var report=new List<string>();
  var hips=root.transform.Find(Hips);var paths=new[]{Hips+"/LLeg",Hips+"/LLeg/LShin",Hips+"/LLeg/LShin/LFoot",Hips+"/RLeg",Hips+"/RLeg/RShin",Hips+"/RLeg/RShin/RFoot"};
  try
  {
   foreach(var clip in clips)
   {
    bool charge=clip.name.EndsWith("Charge"),punch=clip.name.EndsWith("Punch");float floor=.125f;
    var t=charge?new[]{0f,.35f,.7f,.9f,1.03f,1.18f,1.42f,1.7f,2.03f,clip.length}:punch?new[]{0f,.24f,.52f,.66f,.72f,.86f,1.12f,1.48f,1.68f,clip.length}:new[]{0f,clip.length};
    var lz=Shape(t,charge?new[]{.23f,.23f,.25f,.65f,.93f,.93f,.93f,.93f,.93f,.93f}:punch?new[]{.23f,.23f,.27f,.35f,.35f,.35f,.35f,.35f,.35f,.35f}:new[]{.23f,.23f});
    var rz=Shape(t,charge?new[]{-.23f,-.23f,-.23f,-.23f,.05f,.33f,.47f,.47f,.47f,.47f}:punch?new[]{-.23f,-.23f,-.23f,-.23f,-.23f,-.23f,-.23f,-.17f,-.11f,-.11f}:new[]{-.23f,-.23f});
    var ly=Shape(t,charge?new[]{floor,floor,.18f,.28f,floor,floor,floor,floor,floor,floor}:punch?new[]{floor,floor,.20f,floor,floor,floor,floor,floor,floor,floor}:new[]{floor,floor});
    var ry=Shape(t,charge?new[]{floor,floor,floor,floor,.27f,.30f,.18f,floor,floor,floor}:punch?new[]{floor,floor,floor,floor,floor,floor,floor,.18f,floor,floor}:new[]{floor,floor});
    var rotations=paths.ToDictionary(p=>p,p=>new List<Quaternion>());var times=new List<float>();var ys=new List<float>();var zs=new List<float>();var frames=Enumerable.Range(0,Mathf.CeilToInt(clip.length*60)+1).Select(i=>Mathf.Min(clip.length,i/60f)).Concat(t).Concat(AnimationUtility.GetAnimationEvents(clip).Select(e=>e.time)).Distinct().OrderBy(x=>x);
    float minimum=100,maxLift=0,finalSplit=0,legClearance=100;
    foreach(float time in frames)
    {
     foreach(var pair in saved){pair.Key.localPosition=pair.Value.Item1;pair.Key.localRotation=pair.Value.Item2;pair.Key.localScale=pair.Value.Item3;}clip.SampleAnimation(root,time);
     if(!charge&&!punch&&root.transform.localPosition.sqrMagnitude>0.000001f)throw new Exception("Stationary attack root moved: "+clip.name+" at "+time);
     var left=root.transform.Find(paths[0]);var right=root.transform.Find(paths[3]);var a=new Vector3(left.position.x,Mathf.Max(floor,ly.Evaluate(time)),lz.Evaluate(time));var b=new Vector3(right.position.x,Mathf.Max(floor,ry.Evaluate(time)),rz.Evaluate(time));
     hips.localPosition=new Vector3(hips.localPosition.x,hips.localPosition.y,clip.name.EndsWith("GroundSlap")?-.46f*Mathf.Clamp01((1.14f-hips.localPosition.y)/.4f):0f);
     float reach=.49f+Mathf.Sqrt(.35f*.35f+.04f*.04f)-.008f;
     foreach(var goal in new[]{a,b}){float dz=goal.z-hips.position.z;float top=goal.y+Mathf.Sqrt(Mathf.Max(.001f,reach*reach-dz*dz))+.2f;hips.localPosition=new Vector3(hips.localPosition.x,Mathf.Min(hips.localPosition.y,top),hips.localPosition.z);}
     Legs(left,a,hips.localPosition.y);Legs(right,b,hips.localPosition.y);
     var lf=root.transform.Find(paths[2]);var rf=root.transform.Find(paths[5]);minimum=Mathf.Min(minimum,Mathf.Min(MinY(lf.Find("Mesh").GetComponent<MeshFilter>()),MinY(rf.Find("Mesh").GetComponent<MeshFilter>())));maxLift=Mathf.Max(maxLift,Mathf.Max(a.y,b.y)-floor);finalSplit=Mathf.Abs(lf.position.z-rf.position.z);
     foreach(var mesh in left.GetComponentsInChildren<MeshFilter>().Concat(right.GetComponentsInChildren<MeshFilter>()))
     {float low=MinY(mesh);legClearance=Mathf.Min(legClearance,low);if(low<-.025f)throw new Exception(clip.name+" lower body enters ground: "+low+" at "+time+" mesh="+mesh.transform.parent.name+" hips="+hips.localPosition);}
     if(Vector3.Distance(lf.position,a)>.001f||Vector3.Distance(rf.position,b)>.001f)throw new Exception("Foot solve failed "+clip.name+" at "+time);
     times.Add(time);ys.Add(hips.localPosition.y);zs.Add(hips.localPosition.z);foreach(var p in paths)rotations[p].Add(root.transform.Find(p).localRotation);
    }
    Curve(clip,Hips,"m_LocalPosition.y",times,ys);
    Curve(clip,Hips,"m_LocalPosition.z",times,zs);
    foreach(var p in paths)
    {
     foreach(var axis in new[]{"x","y","z"})AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(p,typeof(Transform),"localEulerAnglesRaw."+axis),null);
     var values=rotations[p];for(int i=1;i<values.Count;i++)if(Quaternion.Dot(values[i-1],values[i])<0){var q=values[i];values[i]=new Quaternion(-q.x,-q.y,-q.z,-q.w);}
     for(int axis=0;axis<4;axis++)Curve(clip,p,"m_LocalRotation."+"xyzw"[axis],times,values.Select(q=>q[axis]).ToList());
    }
    clip.EnsureQuaternionContinuity();EditorUtility.SetDirty(clip);report.Add(clip.name+": final fore/aft foot spacing="+finalSplit+"m; minimum sole="+minimum+"m; stride lift="+maxLift+"m; leg mesh minimum="+legClearance+"m; "+(charge||punch?"stepping with root motion":"zero root displacement and fixed foot goals")+"; foot goals solved and grounded.");
    if(legClearance<-.025f)throw new Exception(clip.name+" lower body enters ground: "+legClearance);
   }
   File.WriteAllLines("reinforced-stance-result.txt",report);
  }
  finally{foreach(var pair in saved){pair.Key.localPosition=pair.Value.Item1;pair.Key.localRotation=pair.Value.Item2;pair.Key.localScale=pair.Value.Item3;}}
 }
}
