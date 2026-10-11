using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object=UnityEngine.Object;

public static class EnemyWeaponProduction
{
    static void TransformCurves(AnimationClip clip,string path,List<(float,Vector3,Quaternion)> frames)
    {
        for(int axis=0;axis<3;axis++){int a=axis;Curve(clip,path,"m_LocalPosition."+"xyz"[a],frames.Select(f=>new Keyframe(f.Item1,f.Item2[a])).ToArray());}
        for(int axis=0;axis<4;axis++){int a=axis;Curve(clip,path,"m_LocalRotation."+"xyzw"[a],frames.Select(f=>new Keyframe(f.Item1,f.Item3[a])).ToArray());}
    }
    static void Curve(AnimationClip clip,string path,string property,Keyframe[] keys)
    {
        var curve=new AnimationCurve(keys);for(int i=0;i<keys.Length;i++){AnimationUtility.SetKeyLeftTangentMode(curve,i,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(curve,i,AnimationUtility.TangentMode.Linear);}AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(path,typeof(Transform),property),curve);
    }
    static float S(float x)=>Mathf.SmoothStep(0,1,Mathf.Clamp01(x));
    public static void PreserveLocomotionRoot(AnimationClip clip,AnimationClip source)
    {
        float ratio=clip.length/source.length;
        foreach(var binding in AnimationUtility.GetCurveBindings(source).Where(b=>b.path==""&&b.type==typeof(Transform)&&b.propertyName.StartsWith("m_LocalPosition.")))
        {
            var curve=AnimationUtility.GetEditorCurve(source,binding);float origin=curve.Evaluate(0);
            var keys=curve.keys;
            for(int n=0;n<keys.Length;n++){keys[n].time*=ratio;keys[n].value=origin+(keys[n].value-origin)*ratio;}
            curve.keys=keys;AnimationUtility.SetEditorCurve(clip,binding,curve);
        }
    }
    static void CubeAttacks()
    {
        var actor=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Enemies/TestEnemy/TestEnemy.prefab"));actor.transform.position=Vector3.zero;actor.transform.rotation=Quaternion.identity;
        var cube=actor.transform.Find("foot/Cube");var hand=cube.Find("hand");var rest=cube.localPosition;
        for(int attack=0;attack<2;attack++)
        {
            float duration=attack==0?.95f:1.25f,hit=attack==0?.4f:.55f;
            var body=new List<(float,Vector3,Quaternion)>();var grip=new List<(float,Vector3,Quaternion)>();Quaternion last=Quaternion.identity;
            int count=Mathf.CeilToInt(duration*60);
            for(int frame=0;frame<=count;frame++)
            {
                float t=Mathf.Min(frame/60f,duration),u=t/duration,impact=hit/duration;float wind=S(u/.23f),swing=S((u-.23f)/(impact-.23f)),recover=S((u-impact-.13f)/(1-impact-.13f));
                Vector3 start=new Vector3(.24f,1.20f,.61f),windGrip=attack==0?new Vector3(.60f,1.42f,.18f):new Vector3(.15f,1.70f,.30f),impactGrip=new Vector3(-.18f,attack==0?1.20f:1.04f,.69f),followGrip=new Vector3(-.33f,attack==0?1.16f:.98f,.61f);
                Vector3 initial=new Vector3(.4f,.72f,.56f).normalized,wd=(attack==0?new Vector3(.89f,.24f,-.40f):new Vector3(.12f,.96f,-.25f)).normalized,id=(attack==0?new Vector3(-.84f,.05f,.54f):new Vector3(.12f,-.65f,.75f)).normalized;
                Vector3 p,d;float turn,tilt;
                if(u<.23f){p=Vector3.Lerp(start,windGrip,wind);d=Vector3.Slerp(initial,wd,wind);turn=wind*25;tilt=-wind*5;}
                else if(u<impact){p=Vector3.Lerp(windGrip,impactGrip,swing);d=Vector3.Slerp(wd,id,swing);turn=Mathf.Lerp(25,-35,swing);tilt=Mathf.Lerp(-5,attack==0?3:14,swing);}
                else{float follow=S((u-impact)/.13f);p=Vector3.Lerp(Vector3.Lerp(impactGrip,followGrip,follow),start,recover);d=Vector3.Slerp(id,initial,recover);turn=Mathf.Lerp(-35,0,recover);tilt=Mathf.Lerp(attack==0?3:14,0,recover);}
                cube.localRotation=Quaternion.Euler(tilt,turn,0);cube.localPosition=rest;hand.position=p;hand.rotation=Quaternion.FromToRotation(Vector3.back,d.normalized);
                var localGrip=hand.localPosition;localGrip.z=Mathf.Max(.83f,localGrip.z);hand.localPosition=localGrip;
                var q=hand.localRotation;if(frame>0&&Quaternion.Dot(last,q)<0)q=new Quaternion(-q.x,-q.y,-q.z,-q.w);last=q;
                body.Add((t,cube.localPosition,cube.localRotation));grip.Add((t,hand.localPosition,q));
            }
            var clip=new AnimationClip{name="EnemyWeapon_001_Attack0"+(attack+1),frameRate=60};TransformCurves(clip,"foot/Cube",body);TransformCurves(clip,"foot/Cube/hand",grip);clip.EnsureQuaternionContinuity();
            AnimationUtility.SetAnimationEvents(clip,new[]{new AnimationEvent{time=attack==0?.26f:.37f,functionName="PlayHandingEffORAudioClip"},new AnimationEvent{time=hit,functionName="PublishEnemyCombo",intParameter=attack},new AnimationEvent{time=attack==0?.53f:.73f,functionName="EndEff"},new AnimationEvent{time=duration-.03f,functionName="ChangeAttackStatu"}});
            WeaponAssetProduction.Save(clip,WeaponAssetProduction.Root+"EnemyWeapon_001/Animations/EnemyWeapon_001_Attack0"+(attack+1)+".anim");
        }
        var so=AssetDatabase.LoadAssetAtPath<WeaponSO>(WeaponAssetProduction.Root+"EnemyWeapon_001/EnemyWeapon_001.asset");
        var controller=so.overrideController as AnimatorOverrideController;
        var overrides=new List<KeyValuePair<AnimationClip,AnimationClip>>();controller.GetOverrides(overrides);
        foreach(string name in new[]{"Idle","Walk","Run"})
        {
            var clip=new AnimationClip{name=name,frameRate=60};float duration=name=="Idle"?2:1;
            var body=new List<(float,Vector3,Quaternion)>();var grip=new List<(float,Vector3,Quaternion)>();
            for(int n=0;n<=60;n++)
            {
                float t=duration*n/60f;body.Add((t,rest+Vector3.up*(name=="Idle"?0:Mathf.Sin(t/duration*Mathf.PI*4)*.018f),Quaternion.identity));
                cube.localPosition=rest;cube.localRotation=Quaternion.identity;hand.position=new Vector3(.24f,1.20f,.83f);hand.rotation=Quaternion.FromToRotation(Vector3.back,new Vector3(.4f,.72f,.56f).normalized);grip.Add((t,hand.localPosition,hand.localRotation));
            }
            TransformCurves(clip,"foot/Cube",body);TransformCurves(clip,"foot/Cube/hand",grip);var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=true;AnimationUtility.SetAnimationClipSettings(clip,settings);
            if(name!="Idle")PreserveLocomotionRoot(clip,AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Prefabs/Enemies/TestEnemy/Animations/TestEnemy_"+(name=="Run"?"Chase":"Walk")+".anim"));
            var saved=WeaponAssetProduction.Save(clip,WeaponAssetProduction.Root+"EnemyWeapon_001/Animations/"+name+".anim");
            for(int n=0;n<overrides.Count;n++)
            {
                string guid=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(overrides[n].Key));
                if(guid==(name=="Idle"?"9477533924a39f547a085a8d8fe995c0":name=="Walk"?"3d8d1c7177994df48af9841676e1dd1c":"ae0ab9a506d24c79a006a35c7a2b976b"))overrides[n]=new KeyValuePair<AnimationClip,AnimationClip>(overrides[n].Key,saved);
            }
        }
        controller.ApplyOverrides(overrides);EditorUtility.SetDirty(controller);Object.DestroyImmediate(actor);
    }
    static void Windows(string id,string clipName,float start,float end)
    {
        var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(WeaponAssetProduction.Root+id+"/Animations/"+clipName+".anim");
        var events=AnimationUtility.GetAnimationEvents(clip).Where(e=>e.functionName!="PlayHandingEffORAudioClip"&&e.functionName!="EndEff").ToList();
        events.Add(new AnimationEvent{time=start,functionName="PlayHandingEffORAudioClip"});events.Add(new AnimationEvent{time=end,functionName="EndEff"});
        AnimationUtility.SetAnimationEvents(clip,events.OrderBy(e=>e.time).ToArray());EditorUtility.SetDirty(clip);
    }
    static void AudioOnly(string id)
    {
        WeaponPrefabEffects.ConfigureAudioOnly(id);
        foreach(string guid in AssetDatabase.FindAssets("t:AnimationClip",new[]{WeaponAssetProduction.Root+id+"/Animations"}))
        {
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(guid));var events=AnimationUtility.GetAnimationEvents(clip).ToList();
            if(!events.Any(e=>e.functionName=="PlayHandingEffORAudioClip")){var hit=events.FirstOrDefault(e=>e.functionName=="PublishEnemyCombo");if(hit==null)throw new Exception("Unarmed hit event missing "+clip.name);events.Add(new AnimationEvent{time=Mathf.Max(0,hit.time-.18f),functionName="PlayHandingEffORAudioClip"});AnimationUtility.SetAnimationEvents(clip,events.OrderBy(e=>e.time).ToArray());EditorUtility.SetDirty(clip);}
        }
    }
    public static void BuildAll()
    {
        CubeAttacks();
        Windows("MossstoneBlade","Blade_Attack01_Charge",.72f,1f);Windows("MossstoneBlade","Blade_Attack02_Sweep",.59f,.95f);Windows("MossstoneBlade","Blade_Attack03_Cleave",.92f,1.36f);
        Windows("ReinforcedBlade","ReinforcedBlade_Attack01_Charge",.80f,1.17f);Windows("ReinforcedBlade","ReinforcedBlade_Attack02_Sweep",.87f,1.34f);Windows("ReinforcedBlade","ReinforcedBlade_Attack03_GroundSlap",1.51f,1.80f);
        AudioOnly("MossstoneFists");AudioOnly("ReinforcedFists");
    }
    public static void Run(){try{BuildAll();AssetDatabase.SaveAssets();File.WriteAllText("enemy-result.txt","Built cube attacks, existing blade effect windows and unarmed audio events.");EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);File.WriteAllText("enemy-result.txt","FAIL: "+e);EditorApplication.Exit(1);}}
}
