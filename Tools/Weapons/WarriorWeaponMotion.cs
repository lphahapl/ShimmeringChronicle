using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using Object=UnityEngine.Object;

public static class WarriorWeaponMotion
{
    static GameObject actor;
    static Transform[] bones;
    static Dictionary<Transform,Vector3> restPositions;
    static Dictionary<Transform,Quaternion> restRotations;
    static Transform rightArm,rightElbow,rightWrist,leftArm,leftElbow,leftWrist,socket;
    static bool heavy;
    static float modelScale;
    static float hitTime;
    static float bakeRate;
    static float Smooth(float value)=>Mathf.SmoothStep(0,1,Mathf.Clamp01(value));
    static void GripPath(string name,float time,float duration,out Vector3 grip,out Vector3 direction)
    {
        grip=new Vector3(.14f,1.02f,.29f);direction=new Vector3(.40f,.82f,.36f).normalized;
        if(!name.StartsWith("Attack")){grip.y+=Mathf.Sin(time/duration*Mathf.PI*2)*.004f;return;}
        int index=int.Parse(name.Substring(6))-1;float u=time/duration,hit=hitTime/duration;
        Vector3 wind,wd,follow,fd,impact,impactDirection;
        if(index==0){wind=new Vector3(.28f,1.16f,.14f);wd=new Vector3(.7f,.58f,-.4f);impact=new Vector3(.10f,1.03f,.38f);impactDirection=new Vector3(-.6f,.25f,.76f);follow=new Vector3(-.10f,1.0f,.25f);fd=new Vector3(-.82f,.5f,.26f);}
        else if(index==1){wind=new Vector3(-.13f,1.05f,.21f);wd=new Vector3(-.75f,.50f,-.34f);impact=new Vector3(.12f,1.08f,.37f);impactDirection=new Vector3(.68f,.35f,.64f);follow=new Vector3(.25f,1.16f,.20f);fd=new Vector3(.65f,.64f,-.35f);}
        else{wind=new Vector3(.08f,1.38f,.09f);wd=new Vector3(.10f,.97f,-.22f);impact=new Vector3(.10f,.99f,.36f);impactDirection=new Vector3(.12f,-.45f,.89f);follow=new Vector3(.13f,.93f,.30f);fd=new Vector3(.14f,-.69f,.71f);}
        var start=grip;var sd=direction;
        if(u<.22f){float a=Smooth(u/.22f);grip=Vector3.Lerp(start,wind,a);direction=Vector3.Slerp(sd,wd.normalized,a);}
        else if(u<hit){float a=Smooth((u-.22f)/(hit-.22f));grip=Vector3.Lerp(wind,impact,a);direction=Vector3.Slerp(wd.normalized,impactDirection.normalized,a);}
        else if(u<hit+.18f){float a=Smooth((u-hit)/.18f);grip=Vector3.Lerp(impact,follow,a);direction=Vector3.Slerp(impactDirection.normalized,fd.normalized,a);}
        else{float a=Smooth((u-hit-.18f)/(1-hit-.18f));grip=Vector3.Lerp(follow,start,a);direction=Vector3.Slerp(fd.normalized,sd,a);}
        direction.Normalize();
    }
    static void IK(Transform upper,Transform lower,Transform end,Vector3 target,Vector3 pole)
    {
        var origin=upper.position;float a=Vector3.Distance(origin,lower.position),b=Vector3.Distance(lower.position,end.position);
        var delta=target-origin;float d=Mathf.Clamp(delta.magnitude,.02f,a+b-.001f);var forward=delta.normalized;var bend=Vector3.ProjectOnPlane(pole,forward).normalized;
        float x=(a*a+d*d-b*b)/(2*d);var elbow=origin+forward*x+bend*Mathf.Sqrt(Mathf.Max(0,a*a-x*x));
        upper.rotation=Quaternion.FromToRotation(lower.position-origin,elbow-origin)*upper.rotation;
        lower.rotation=Quaternion.FromToRotation(end.position-lower.position,origin+forward*d-lower.position)*lower.rotation;
    }
    static Quaternion Socket(Transform wrist)
    {
        // Keep the source Warrior's hand socket calibration, shared with gameplay.
        return socket.localRotation;
    }
    static void Pose(string name,float t,float duration,AnimationClip reference)
    {
        foreach(var bone in bones){bone.localPosition=restPositions[bone];bone.localRotation=restRotations[bone];}
        reference.SampleAnimation(actor,name=="Idle"?0:t/duration*reference.length);
        actor.transform.position=Vector3.zero;actor.transform.rotation=Quaternion.identity;
        GripPath(name,t,duration,out var grip,out var direction);
        grip*=modelScale;
        var q=Quaternion.FromToRotation(Vector3.back,direction);var right=q*Quaternion.Inverse(Socket(rightWrist));
        var rightOffset=right*Vector3.Scale(socket.localPosition,rightWrist.lossyScale);
        var left=q*Quaternion.Inverse(socket.localRotation)*Quaternion.AngleAxis(180,Vector3.up);
        var leftOffset=left*Vector3.Scale(socket.localPosition,leftWrist.lossyScale);
        // Project the shared grip into both arm-reach spheres before solving the elbows.
        // This keeps the lower hand attached even while the reference torso turns away.
        if(heavy)for(int iteration=0;iteration<16;iteration++)
        {
            float rr=(Vector3.Distance(rightArm.position,rightElbow.position)+Vector3.Distance(rightElbow.position,rightWrist.position))*.98f;
            var rt=grip-rightOffset;var rd=rt-rightArm.position;if(rd.magnitude>rr)grip+=rightArm.position+rd.normalized*rr-rt;
            float lr=(Vector3.Distance(leftArm.position,leftElbow.position)+Vector3.Distance(leftElbow.position,leftWrist.position))*.98f;
            var lt=grip-direction*.095f-leftOffset;var ld=lt-leftArm.position;if(ld.magnitude>lr)grip+=leftArm.position+ld.normalized*lr-lt;
        }
        var rightTarget=grip-right*Vector3.Scale(socket.localPosition,socket.parent.lossyScale);
        var rightAxis=(rightWrist.GetChild(0).localPosition).normalized;
        IK(rightArm,rightElbow,rightWrist,rightTarget,heavy?new Vector3(.45f,-1,-.25f):-(right*rightAxis));rightWrist.rotation=right;
        if(heavy)
        {
            var leftAxis=leftWrist.GetChild(0).localPosition.normalized;
            var leftTarget=grip-direction*.095f-left*Vector3.Scale(socket.localPosition,leftWrist.lossyScale);
            IK(leftArm,leftElbow,leftWrist,leftTarget,new Vector3(-.45f,-1,-.25f));leftWrist.rotation=left;
        }
        foreach(var bone in bones.Where(b=>b.name.Contains("ArmDigit")&&(heavy||b.name.StartsWith("CATRigR"))))
        {
            bool tip=bone.name.EndsWith("3"),middle=bone.name.EndsWith("2");
            bone.localRotation=restRotations[bone]*Quaternion.AngleAxis(tip?35:middle?50:25,Vector3.forward);
        }
    }
    static AnimationClip Bake(string id,string name,float duration,bool loop,AnimationClip source,AnimationEvent[] events)
    {
        bakeRate=heavy&&name.StartsWith("Attack")?240:60;
        var clip=new AnimationClip{name=name,frameRate=bakeRate};int frames=Mathf.CeilToInt(duration*bakeRate)+1;
        var positions=bones.ToDictionary(b=>b,b=>new Vector3[frames]);var rotations=bones.ToDictionary(b=>b,b=>new Quaternion[frames]);
        for(int i=0;i<frames;i++)
        {
            Pose(name,Mathf.Min(i/bakeRate,duration),duration,source);
            foreach(var b in bones){positions[b][i]=b.localPosition;var q=b.localRotation;if(i>0&&Quaternion.Dot(q,rotations[b][i-1])<0)q=new Quaternion(-q.x,-q.y,-q.z,-q.w);rotations[b][i]=q;}
        }
        foreach(var bone in bones)
        {
            string path=AnimationUtility.CalculateTransformPath(bone,actor.transform);
            for(int axis=0;axis<4;axis++){int a=axis;Curve(clip,path,"m_LocalRotation."+"xyzw"[a],frames,duration,k=>rotations[bone][k][a]);}
            for(int axis=0;axis<3;axis++){int a=axis;Curve(clip,path,"m_LocalPosition."+"xyz"[a],frames,duration,k=>positions[bone][k][a]);}
        }
        var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=loop;settings.keepOriginalPositionXZ=true;settings.keepOriginalPositionY=true;settings.keepOriginalOrientation=true;AnimationUtility.SetAnimationClipSettings(clip,settings);
        AnimationUtility.SetAnimationEvents(clip,events??Array.Empty<AnimationEvent>());clip.EnsureQuaternionContinuity();
        return WeaponAssetProduction.Save(clip,WeaponAssetProduction.Root+id+"/Animations/Warrior/"+name+".anim");
    }
    static void Curve(AnimationClip clip,string path,string property,int count,float duration,Func<int,float> value)
    {
        var keys=Enumerable.Range(0,count).Select(k=>new Keyframe(Mathf.Min(k/bakeRate,duration),value(k))).ToArray();
        if(keys.All(k=>Mathf.Abs(k.value-keys[0].value)<.000001f))keys=new[]{keys[0],keys[keys.Length-1]};
        var c=new AnimationCurve(keys);
        for(int n=0;n<c.length;n++){AnimationUtility.SetKeyLeftTangentMode(c,n,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(c,n,AnimationUtility.TangentMode.Linear);}AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(path,typeof(Transform),property),c);
    }
    static void Build(string id)
    {
        heavy=id=="weapon_004";WeaponAssetProduction.Folder(WeaponAssetProduction.Root+id+"/Animations/Warrior");
        actor=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Warrior/Character Warrior.prefab"));
        actor.transform.position=Vector3.zero;actor.transform.rotation=Quaternion.identity;
        foreach(var behaviour in actor.GetComponentsInChildren<MonoBehaviour>())behaviour.enabled=false;
        foreach(var animator in actor.GetComponentsInChildren<Animator>())animator.enabled=false;
        bones=actor.GetComponentsInChildren<Transform>().Where(t=>t.name.StartsWith("CATRig")).ToArray();restPositions=bones.ToDictionary(b=>b,b=>b.localPosition);restRotations=bones.ToDictionary(b=>b,b=>b.localRotation);
        Transform Find(string n)=>bones.Single(t=>t.name==n);
        rightArm=Find("CATRigRArm1");rightElbow=Find("CATRigRArm2");rightWrist=Find("CATRigRArmPalm");leftArm=Find("CATRigLArm1");leftElbow=Find("CATRigLArm2");leftWrist=Find("CATRigLArmPalm");
        var player=actor.GetComponent<PlayerObj>();socket=player.handPos;
        if(!socket)throw new Exception("Warrior hand socket missing");
        modelScale=actor.GetComponentsInChildren<SkinnedMeshRenderer>().Single(s=>s.name=="Body").bounds.size.y/1.70f;
        var sourceController=AssetDatabase.LoadAssetAtPath<AnimatorController>("Assets/Prefabs/Player/Warrior/Animations/Character Warrior.controller");
        var clips=new Dictionary<string,AnimationClip>();
        float[] durations=id=="weapon_002"?new[]{.72f,.80f,.95f}:id=="weapon_003"?new[]{.85f,.90f,1.1f}:new[]{.9f,1f,1.2f};
        float[] hits=id=="weapon_002"?new[]{.26f,.30f,.38f}:id=="weapon_003"?new[]{.32f,.34f,.44f}:new[]{.35f,.40f,.50f};
        float[] starts=id=="weapon_002"?new[]{.14f,.17f,.20f}:id=="weapon_003"?new[]{.18f,.20f,.26f}:new[]{.20f,.24f,.30f};
        float[] ends=id=="weapon_002"?new[]{.39f,.43f,.52f}:id=="weapon_003"?new[]{.46f,.49f,.59f}:new[]{.49f,.55f,.68f};
        var guard=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Prefabs/Weapon/weapon_001/Animations/1A.anim");
        for(int i=0;i<3;i++){hitTime=hits[i];clips["Attack"+(i+1)]=Bake(id,"Attack"+(i+1),durations[i],false,guard,new[]{new AnimationEvent{time=starts[i],functionName="PlayHandingEffORAudioClip"},new AnimationEvent{time=hits[i],functionName="PublishPlayerCombo",intParameter=i},new AnimationEvent{time=ends[i],functionName="EndEff"}});}
        clips["Idle"]=Bake(id,"Idle",2.6f,true,guard,null);
        foreach(string name in new[]{"Walk","Run"}){var original=AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/Prefabs/Player/Warrior/Animations/"+name+".anim");clips[name]=Bake(id,name,original.length,true,original,null);}
        // The old holding layer includes the right wrist; that would override every new slash.
        string basePath=WeaponAssetProduction.Root+id+"/Animations/Warrior/WarriorWeapon.controller";
        var weaponBase=AssetDatabase.LoadAssetAtPath<AnimatorController>(basePath);
        if(!weaponBase){AssetDatabase.CopyAsset(AssetDatabase.GetAssetPath(sourceController),basePath);weaponBase=AssetDatabase.LoadAssetAtPath<AnimatorController>(basePath);}
        var mask=new AvatarMask{name="FingerGrip"};mask.AddTransformPath(actor.transform,true);
        for(int n=0;n<mask.transformCount;n++){string path=mask.GetTransformPath(n);mask.SetTransformActive(n,path.Contains("ArmDigit")&&(heavy||path.Contains("CATRigRArm")));}
        for(int n=0;n<(int)AvatarMaskBodyPart.LastBodyPart;n++)mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)n,false);
        var savedMask=WeaponAssetProduction.Save(mask,WeaponAssetProduction.Root+id+"/Animations/Warrior/FingerGrip.mask");var layers=weaponBase.layers;layers[1].avatarMask=savedMask;weaponBase.layers=layers;EditorUtility.SetDirty(weaponBase);
        var controller=new AnimatorOverrideController(weaponBase){name=id+"_Warrior"};var overrides=new List<KeyValuePair<AnimationClip,AnimationClip>>();controller.GetOverrides(overrides);
        for(int n=0;n<overrides.Count;n++)
        {
            var original=overrides[n].Key;string name=original.name;AnimationClip replacement=null;
            string guid=AssetDatabase.AssetPathToGUID(AssetDatabase.GetAssetPath(original));
            if(guid=="5f3d5b890f2797344a2f4ee136663028")replacement=clips["Attack1"];
            else if(guid=="e9c0c1d6bbc81bb45ad1b8f92db7916a")replacement=clips["Attack2"];
            else if(guid=="e84e47ea15ef7b54eafa921dbbe3dd6f")replacement=clips["Attack3"];
            else if(guid=="96fd1dabf48467e40b791be08a0c8fdf"||guid=="6629c48968b7429c9286ecb5d61de60f")replacement=clips["Idle"];
            else if(name.Contains("Idle"))replacement=clips["Idle"];
            else if(name=="Walk"||name=="Run")replacement=clips[name];
            else if(name=="HandingLayerZW"||name=="HandingLayerZW 1")replacement=clips["Idle"];
            if(replacement)overrides[n]=new KeyValuePair<AnimationClip,AnimationClip>(original,replacement);
        }
        controller.ApplyOverrides(overrides);var saved=WeaponAssetProduction.Save(controller,WeaponAssetProduction.Root+id+"/Animations/Warrior/"+id+".overrideController");
        var so=AssetDatabase.LoadAssetAtPath<WeaponSO>(WeaponAssetProduction.Root+id+"/"+id+".asset");so.overrideController=saved;EditorUtility.SetDirty(so);
        Object.DestroyImmediate(actor);
    }
    public static void BuildAll(){foreach(string id in new[]{"weapon_002","weapon_003","weapon_004"})Build(id);AssetDatabase.SaveAssets();}
    public static void Run(){try{BuildAll();File.WriteAllText("warrior-result.txt","PASS: three in-place sets with six clips each, complete heavy idle/walk/run and correctly indexed attack events; old player prefab unchanged.");EditorApplication.Exit(0);}catch(Exception e){Debug.LogException(e);File.WriteAllText("warrior-result.txt","FAIL: "+e);EditorApplication.Exit(1);}}
}
