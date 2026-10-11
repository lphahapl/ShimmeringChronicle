using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
public static class InspectWeaponProduction
{
 public static void Run()
 {
  var report=new StringBuilder();
  var actor=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Warrior/Character Warrior.prefab"));actor.transform.position=Vector3.zero;
  foreach(var animator in actor.GetComponentsInChildren<Animator>())animator.enabled=false;
  foreach(string phase in new[]{"rest","source","new"})
  {
   if(phase!="rest")AssetDatabase.LoadAssetAtPath<AnimationClip>(phase=="source"?"Assets/Prefabs/Weapon/weapon_001/Animations/1A.anim":"Assets/Prefabs/Weapon/weapon_004/Animations/Warrior/Idle.anim").SampleAnimation(actor,0);
   foreach(var skin in actor.GetComponentsInChildren<SkinnedMeshRenderer>())
   {
    var mesh=new Mesh();skin.BakeMesh(mesh);
    var vertices=mesh.vertices.Select(v=>skin.transform.TransformPoint(v)).ToArray();var bounds=new Bounds(vertices[0],Vector3.zero);foreach(var v in vertices)bounds.Encapsulate(v);
    report.AppendLine(phase+" "+skin.name+" scale "+skin.transform.lossyScale+" renderer "+skin.bounds+" baked "+bounds+" mesh "+skin.sharedMesh.bounds+" vertices "+vertices.Length+" materials "+string.Join(",",skin.sharedMaterials.Select(m=>m.name)));
    Object.DestroyImmediate(mesh);
   }
   foreach(var bone in actor.GetComponentsInChildren<Transform>().Where(t=>new[]{"CATRigPelvis","CATRigRArmPalm","CATRigLArmPalm","CATRigHead"}.Contains(t.name)))report.AppendLine(phase+" bone "+bone.name+" pos "+bone.position+" local "+bone.localPosition+" scale "+bone.lossyScale);
  }
  report.AppendLine("hand "+actor.GetComponent<PlayerObj>().handPos.position+" parent "+actor.GetComponent<PlayerObj>().handPos.parent.name);
  Object.DestroyImmediate(actor);File.WriteAllText("inspection.txt",report.ToString());EditorApplication.Exit(0);
 }
}
