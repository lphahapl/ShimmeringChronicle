using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

public static class BuildKeqingRootMotion
{
    public const string Folder = "Assets/Prefabs/Player/KeqingRootMotion";
    const string ModelPath = "Assets/Prefabs/Player/KeqingRootMotion/Models/Source/刻晴.fbx";
    static GameObject actor;
    static Transform[] bones;
    static Dictionary<Transform, Vector3> restPosition;
    static Dictionary<Transform, Quaternion> restRotation;
    static Dictionary<string, Transform> joints;
    static List<AnimationClip> clips;
    static GameObject reference;
    static Dictionary<string, AnimationClip> referenceClips;
    static Dictionary<Transform, Transform> referenceBones;
    static Dictionary<Transform, Quaternion> referenceCalibration;
    static Quaternion referenceHipBind;
    static Transform referenceHip;
    static Vector3[][] solePoints;
    static int[][] soleIndices;
    static SkinnedMeshRenderer bodySkin;
    static Mesh soleMesh;
    static Vector3[] footGoals=new Vector3[2];
    static Transform hip, torso, chest, neck, head;

    struct Pose
    {
        public float root, yaw, hipY, sway, energy;
        public Vector3 leftFoot, rightFoot;
        public float leftPitch, rightPitch, leftYaw, rightYaw;
        public string referenceA, referenceB;
        public float referenceTimeA, referenceTimeB, referenceBlend;
    }
    static void FolderAt(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        FolderAt(parent); AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
    static Transform Bone(string name) => joints[name];
    static T Save<T>(T value,string path) where T: Object
    {
        var existing=AssetDatabase.LoadAssetAtPath<T>(path);
        if(existing==null){AssetDatabase.CreateAsset(value,path);return value;}
        EditorUtility.CopySerialized(value,existing);EditorUtility.SetDirty(existing);Object.DestroyImmediate(value);return existing;
    }
    static void InitializeReference()
    {
        const string original="Assets/Prefabs/Player/Warrior/";
        string mesh=AssetDatabase.LoadAssetAtPath<GameObject>(original+"Models/Mesh.FBX")!=null?original+"Models/Mesh.FBX":"Assets/ReferenceWarrior/Mesh.FBX";
        reference=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(mesh));
        foreach(var a in reference.GetComponentsInChildren<Animator>())a.enabled=false;
        var source=reference.GetComponentsInChildren<Transform>().ToDictionary(t=>AnimationUtility.CalculateTransformPath(t,reference.transform));
        Transform Find(string name)=>source.Values.Single(t=>t.name==name);
        referenceHip=Find("CATRigHub001");referenceHipBind=referenceHip.rotation;
        referenceClips=new Dictionary<string,AnimationClip>();
        foreach(string name in new[]{"Walk","Run","1A","2A","3A"})
            referenceClips[name]=AssetDatabase.LoadAssetAtPath<AnimationClip>((name=="Walk"||name=="Run"?original+"Animations/":"Assets/Prefabs/Weapon/weapon_001/Animations/")+name+".anim")??AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/ReferenceWarrior/"+name+".anim");
        referenceBones=new Dictionary<Transform,Transform>();referenceCalibration=new Dictionary<Transform,Quaternion>();
        foreach(string side in new[]{"Left","Right"})
        {
            string prefix=side=="Left"?"CATRigLArm":"CATRigRArm";
            foreach(var part in new[]{("Arm","1","2"),("Elbow","2","Palm"),("Wrist","Palm","Digit31")})
            {
                var target=Bone(side+part.Item1);var sourceBone=Find(prefix+part.Item2);var sourceEnd=Find(prefix+part.Item3);
                Vector3 localAxis=part.Item1=="Wrist"?new Vector3(side=="Left"?-.707f:.707f,-.707f,0):restPosition[Bone(side+(part.Item1=="Arm"?"Elbow":"Wrist"))];
                var aligned=Quaternion.FromToRotation(localAxis.normalized,(sourceEnd.position-sourceBone.position).normalized);
                referenceBones[target]=sourceBone;referenceCalibration[target]=Quaternion.Inverse(sourceBone.rotation)*aligned;
            }
        }
        foreach(var pair in new[]{("Torso","CATRigSpine1"),("Torso2","CATRigSpine2"),("Neck","CATRigSpine"),("Head","CATRigHub003")})
        {
            var target=Bone(pair.Item1);var sourceBone=Find(pair.Item2);
            referenceBones[target]=sourceBone;referenceCalibration[target]=Quaternion.Inverse(sourceBone.rotation);
        }
        referenceBones[hip]=referenceHip;referenceCalibration[hip]=Quaternion.Inverse(referenceHipBind);
    }
    static Dictionary<Transform,Quaternion> ReferencePose(string name,float time)
    {
        referenceClips[name].SampleAnimation(reference,time);
        return referenceBones.ToDictionary(p=>p.Key,p=>p.Value.rotation*referenceCalibration[p.Key]);
    }
    static void CaptureSoles()
    {
        bodySkin=actor.GetComponentsInChildren<SkinnedMeshRenderer>().Single(r=>r.name=="U_Char_2");
        soleMesh=new Mesh();bodySkin.BakeMesh(soleMesh);
        var world=soleMesh.vertices.Select(v=>bodySkin.transform.TransformPoint(v)).ToArray();
        solePoints=new Vector3[2][];soleIndices=new int[2][];
        for(int k=0;k<2;k++)
        {
            var foot=Bone(k==0?"LeftFoot":"RightFoot");
            soleIndices[k]=Enumerable.Range(0,world.Length).Where(n=>world[n].y<.15f&&(k==0?world[n].x<0:world[n].x>0)).ToArray();
            solePoints[k]=soleIndices[k].Select(n=>foot.InverseTransformPoint(world[n])).ToArray();
        }
    }
    static float SoleHeight(bool left,Quaternion rotation)
    {
        return .0015f-solePoints[left?0:1].Min(v=>(rotation*v).y);
    }
    static float Smooth(float value) { value = Mathf.Clamp01(value); return value * value * (3f - 2f * value); }
    static float Ramp(float time, float begin, float end) => Smooth((time - begin) / (end - begin));
    static float Wave(float phase) => Mathf.Sin(phase * Mathf.PI * 2);
    static Pose Guard(float time)
    {
        return new Pose {
            referenceA="1A", referenceTimeA=0,
            hipY = -.04f + Wave(time / 2.6f) * .005f, sway = Wave(time / 2.6f) * .5f,
            leftFoot = new Vector3(-.115f, .0963f, .09f), rightFoot = new Vector3(.115f, .0977f, -.07f)
        };
    }
    static Vector3 Foot(float phase, bool run, int side, out float pitch)
    {
        phase = Mathf.Repeat(phase, 1);
        float stance = run ? .34f : .56f;
        float distance = run ? 2.4f : 1.26f;
        float half = distance * stance * .5f;
        float z, height;
        if (phase < stance)
        {
            float u = phase / stance;
            z = Mathf.Lerp(half, -half, u); height = 0;
            pitch = Mathf.Lerp(-10f, 18f, Smooth(u));
        }
        else
        {
            float u = (phase - stance) / (1 - stance);
            z = Mathf.Lerp(-half, half, Smooth(u));
            height = Mathf.Sin(Mathf.PI * u) * (run ? .20f : .095f);
            pitch = Mathf.Lerp(18f, -10f, Smooth(u)) + Mathf.Sin(Mathf.PI * u) * (run ? 16f : 8f);
        }
        return new Vector3(side * (run ? .087f : .085f), (side < 0 ? .0963f : .0977f) + height, z);
    }
    static Pose Gait(float time, bool run)
    {
        float duration = run ? .68f : .96f;
        float phase = time / duration;
        var p = Guard(time); p.root = time * (run ? 2.4f / .68f : 1.26f / .96f);
        p.referenceA=run?"Run":"Walk";p.referenceTimeA=Mathf.Repeat(phase,1)*referenceClips[p.referenceA].length;
        p.hipY = (run ? -.105f : -.055f) - Mathf.Cos(phase * Mathf.PI * 4f) * (run ? .026f : .009f);
        p.sway = Wave(phase) * (run ? 2.2f : 1.7f); p.energy = run ? 1f : .4f;
        p.leftFoot = Foot(phase, run, -1, out p.leftPitch);
        p.rightFoot = Foot(phase + .5f, run, 1, out p.rightPitch);
        return p;
    }
    static Pose Blend(Pose a, Pose b, float t)
    {
        return new Pose {
            referenceA=a.referenceA,referenceTimeA=a.referenceTimeA,referenceB=b.referenceA,referenceTimeB=b.referenceTimeA,referenceBlend=t,
            root = Mathf.Lerp(a.root,b.root,t), yaw = Mathf.Lerp(a.yaw,b.yaw,t),
            hipY = Mathf.Lerp(a.hipY,b.hipY,t), sway = Mathf.Lerp(a.sway,b.sway,t), energy = Mathf.Lerp(a.energy,b.energy,t),
            leftFoot = Vector3.Lerp(a.leftFoot,b.leftFoot,t), rightFoot = Vector3.Lerp(a.rightFoot,b.rightFoot,t), leftPitch = Mathf.Lerp(a.leftPitch,b.leftPitch,t), rightPitch = Mathf.Lerp(a.rightPitch,b.rightPitch,t),leftYaw=Mathf.Lerp(a.leftYaw,b.leftYaw,t),rightYaw=Mathf.Lerp(a.rightYaw,b.rightYaw,t)
        };
    }
    static Pose Entry(float time, bool run, bool stop, bool rightLead=false)
    {
        float duration = run ? .38f : .32f;
        float u = time / duration;
        float phase = stop ? time+(rightLead?(run?.34f:.48f):0) : time + (run ? .68f : .96f) - duration;
        var gait = Gait(phase, run);
        var p = stop ? Blend(gait, Guard(0), Smooth(u)) : Blend(Guard(0), gait, Smooth(u));
        // Integral of a smooth acceleration/deceleration, rather than sliding at full speed immediately.
        float speed = run ? 2.4f / .68f : 1.26f / .96f;
        float integral = u*u*u - .5f*u*u*u*u;
        p.root = speed * duration * (stop ? u - integral : integral);
        return p;
    }
    static Pose Turn(float time, int side)
    {
        float u = time / .46f; var p = Guard(0);
        p.yaw = side * 90 * Ramp(u,.14f,.84f);
        p.hipY -= Mathf.Sin(u*Mathf.PI)*.018f;
        var fullTurn=Quaternion.Euler(0,side*90,0);var inverse=Quaternion.Euler(0,-p.yaw,0);
        float leftStep=Ramp(u,.05f,.48f),rightStep=Ramp(u,.51f,.96f);
        var leftWorld=Vector3.Lerp(p.leftFoot,fullTurn*p.leftFoot,leftStep);
        var rightWorld=Vector3.Lerp(p.rightFoot,fullTurn*p.rightFoot,rightStep);
        leftWorld.y+=Mathf.Sin(leftStep*Mathf.PI)*.065f;rightWorld.y+=Mathf.Sin(rightStep*Mathf.PI)*.065f;
        p.leftFoot=inverse*leftWorld;p.rightFoot=inverse*rightWorld;
        p.leftYaw=side*90*leftStep-p.yaw;p.rightYaw=side*90*rightStep-p.yaw;
        p.energy = .35f; return p;
    }
    static Pose Strike(float time, int index)
    {
        float duration = new[]{.82f,.90f,1.08f}[index];
        float u = time / duration; var guard = Guard(0);
        guard.referenceA=(index+1)+"A";guard.referenceTimeA=u*referenceClips[guard.referenceA].length;
        var p=guard;
        float impact = Mathf.Sin(Mathf.Clamp01((u-.10f)/.72f)*Mathf.PI);
        p.hipY-=impact*(index==2?.12f:.045f);
        float distance = new[]{.34f,.43f,.72f}[index]; p.root=distance*Ramp(u,.24f,.58f);
        bool leftLead=index!=1;
        Vector3 lead=leftLead?guard.leftFoot:guard.rightFoot, trail=leftLead?guard.rightFoot:guard.leftFoot;
        float leadStep=Ramp(u,.12f,.43f), catchup=Ramp(u,.59f,.96f);
        lead.z+=distance*leadStep-p.root; lead.y+=Mathf.Sin(leadStep*Mathf.PI)*(index==2?.13f:.08f);
        if(index==2)
        {
            float firstStep=Ramp(u,.10f,.35f);
            trail.z+=distance*.42f*firstStep+distance*.58f*catchup-p.root;
            trail.y+=Mathf.Sin(firstStep*Mathf.PI)*.07f+Mathf.Sin(catchup*Mathf.PI)*.075f;
        }
        else {trail.z+=distance*catchup-p.root;trail.y+=Mathf.Sin(catchup*Mathf.PI)*.07f;}
        if(leftLead){p.leftFoot=lead;p.rightFoot=trail;}else{p.rightFoot=lead;p.leftFoot=trail;}
        p.energy=impact; return p;
    }
    static void IK(Transform upper, Transform lower, Transform end, Vector3 target, Vector3 pole)
    {
        var origin=upper.position; float a=Vector3.Distance(origin,lower.position), b=Vector3.Distance(lower.position,end.position);
        var delta=target-origin; float distance=Mathf.Clamp(delta.magnitude,.025f,a+b-.002f);
        var direction=delta.normalized;
        var bend=Vector3.ProjectOnPlane(pole,direction).normalized;
        float along=(a*a+distance*distance-b*b)/(2*distance);
        float height=Mathf.Sqrt(Mathf.Max(.00001f,a*a-along*along));
        var knee=origin+direction*along+bend*height;
        upper.rotation=Quaternion.FromToRotation(lower.position-upper.position,knee-origin)*upper.rotation;
        lower.rotation=Quaternion.FromToRotation(end.position-lower.position,origin+direction*distance-lower.position)*lower.rotation;
    }
    static void Apply(Pose p, float time)
    {
        foreach(var t in bones){t.localPosition=restPosition[t];t.localRotation=restRotation[t];}
        hip.localPosition += new Vector3(p.sway*.002f,p.hipY,0);
        var referencePose=ReferencePose(p.referenceA,p.referenceTimeA);
        if(p.referenceB!=null)
        {
            var second=ReferencePose(p.referenceB,p.referenceTimeB);
            foreach(var target in referenceBones.Keys)referencePose[target]=Quaternion.Slerp(referencePose[target],second[target],p.referenceBlend);
        }
        hip.rotation=referencePose[hip];torso.rotation=referencePose[torso];chest.rotation=referencePose[chest];neck.rotation=referencePose[neck];head.rotation=referencePose[head];
        foreach(string side in new[]{"Left","Right"})
        {
            bool left=side=="Left"; int sign=left?-1:1;
            var upper=Bone(side+"Hip");var knee=Bone(side+"Knee");var foot=Bone(side+"Foot");
            var goal=left?p.leftFoot:p.rightFoot; float pitch=left?p.leftPitch:p.rightPitch;
            var footRotation=Quaternion.Euler(pitch,(left?p.leftYaw-3:p.rightYaw+3),0);
            goal.y+=SoleHeight(left,footRotation)-(left?.0963f:.0977f);
            footGoals[left?0:1]=goal;
            IK(upper,knee,foot,goal,new Vector3(sign*.08f,0,1));
            foot.rotation=footRotation;
            foreach(string part in new[]{"Hip","Knee","Foot"})Bone(side+part+"D").localRotation=Bone(side+part).localRotation;
            var arm=Bone(side+"Arm"); var elbow=Bone(side+"Elbow");var wrist=Bone(side+"Wrist");
            arm.rotation=referencePose[arm];elbow.rotation=referencePose[elbow];wrist.rotation=referencePose[wrist];
            // Preserve forearm roll, but bound wrist bending in the actual MMD palm basis.
            var palmAxis=new Vector3(sign*.707f,-.707f,0);
            var handDelta=wrist.localRotation;
            var palmDirection=handDelta*palmAxis;
            var swing=Quaternion.FromToRotation(palmAxis,palmDirection);
            var twist=Quaternion.Inverse(swing)*handDelta;
            float twistAngle=2*Mathf.Atan2(Vector3.Dot(new Vector3(twist.x,twist.y,twist.z),palmAxis.normalized),twist.w);
            twist=Quaternion.AngleAxis(Mathf.Sin(twistAngle)*25f,palmAxis);
            wrist.localRotation=Quaternion.RotateTowards(Quaternion.identity,swing,38f)*twist;
            foreach(var t in bones.Where(t=>t.name.Contains("joint_"+side+"HandTwist")))
            {
                float weight=t.name.EndsWith("1")?.25f:t.name.EndsWith("2")?.5f:.75f;
                t.localRotation=Quaternion.Slerp(Quaternion.identity,twist,weight);
            }
            foreach(var finger in bones.Where(t=>t.name.Contains("joint_"+side) && new[]{"Index","Middle","Ring","Little"}.Any(f=>t.name.Contains(f)) && !t.name.Contains("Tip")))
            {
                float curl=left?12:finger.name.EndsWith("1")?60:finger.name.EndsWith("2")?70:55;
                finger.localRotation=Quaternion.AngleAxis(sign*curl,Vector3.forward);
            }
            for(int n=0;n<=2;n++)
            {
                var thumb=joints.ContainsKey(side+"Thumb"+n)?Bone(side+"Thumb"+n):null;
                if(thumb!=null)thumb.localRotation=Quaternion.Euler(left?5:25,sign*(left?5:25),sign*(left?5:12));
            }
        }
        // Conservative, baked follow-through. This does not add a second runtime physics system.
        foreach(var t in bones)
        {
            if(t.name.Contains("joint_migi?")||t.name.Contains("joint_hidari?"))
            {
                int depth=t.name.Count(c=>c=='_');
                t.localRotation=Quaternion.Euler(Wave(time/1.3f-depth*.045f)*(.5f+p.energy*1.2f),Wave(time/2.6f-depth*.07f)*(.5f+p.energy*1.3f),0);
            }
            if(t.name.Contains("joint_Skirt_0_"))
            {
                float forward=Mathf.Max(0,Mathf.Max(p.leftFoot.z,p.rightFoot.z));
                t.localRotation=Quaternion.Euler(-forward*12f-Wave(time/2.6f)*p.energy*1.5f,0,0);
            }
        }
        // MMD shoes have blended knee/foot weights; validate the deformed sole as well as the ankle target.
        for(int pass=0;pass<2;pass++)
        {
            bodySkin.BakeMesh(soleMesh);var vertices=soleMesh.vertices;
            for(int side=0;side<2;side++)
            {
                float minimum=soleIndices[side].Min(n=>bodySkin.transform.TransformPoint(vertices[n]).y);
                if(minimum>=.0005f)continue;
                string name=side==0?"Left":"Right";
                footGoals[side].y+=.0015f-minimum;
                var foot=Bone(name+"Foot");var rotation=foot.rotation;
                IK(Bone(name+"Hip"),Bone(name+"Knee"),foot,footGoals[side],new Vector3(side==0?-.08f:.08f,0,1));foot.rotation=rotation;
                foreach(string part in new[]{"Hip","Knee","Foot"})Bone(name+part+"D").localRotation=Bone(name+part).localRotation;
            }
        }
    }
    static void SetCurve(AnimationClip clip,string path,Type type,string property,Keyframe[] values)
    {
        // Retain the 60fps authored shape, removing keys only within 0.12mm / 0.014deg.
        var keep=new SortedSet<int>{0,values.Length-1};
        Action<int,int> simplify=null;
        simplify=(start,end)=>{
            float error=.00012f;int worst=-1;
            for(int n=start+1;n<end;n++)
            {
                float predicted=Mathf.Lerp(values[start].value,values[end].value,(values[n].time-values[start].time)/(values[end].time-values[start].time));
                float difference=Mathf.Abs(values[n].value-predicted);if(difference>error){error=difference;worst=n;}
            }
            if(worst>=0){keep.Add(worst);simplify(start,worst);simplify(worst,end);}
        };
        simplify(0,values.Length-1);
        var curve=new AnimationCurve(keep.Select(k=>values[k]).ToArray());
        for(int k=0;k<curve.length;k++){AnimationUtility.SetKeyLeftTangentMode(curve,k,AnimationUtility.TangentMode.Linear);AnimationUtility.SetKeyRightTangentMode(curve,k,AnimationUtility.TangentMode.Linear);}
        AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(path,type,property),curve);
    }
    static AnimationClip Bake(string name,float duration,bool loop,Func<float,Pose> pose,AnimationEvent[] events=null)
    {
        var clip=new AnimationClip{name=name,frameRate=60};int count=Mathf.CeilToInt(duration*60)+1;
        var times=Enumerable.Range(0,count).Select(i=>Mathf.Min(duration,i/60f)).ToArray();
        var rotations=bones.ToDictionary(t=>t,t=>new Quaternion[count]);var hips=new Vector3[count];var roots=new Vector3[count];var yaw=new Quaternion[count];
        for(int k=0;k<count;k++)
        {
            var p=pose(times[k]);Apply(p,times[k]/duration*2.6f); hips[k]=hip.localPosition;roots[k]=new Vector3(0,0,p.root);yaw[k]=Quaternion.Euler(0,p.yaw,0);
            foreach(var t in bones)
            {
                if(!loop && k>0 && (t.name.Contains("Wrist")||t.name.Contains("HandTwist")))
                {
                    float step=times[k]-times[k-1];
                    var previous=rotations[t][k-1];
                    var desired=Quaternion.Slerp(previous,t.localRotation,1f-Mathf.Exp(-30f*step));
                    t.localRotation=Quaternion.RotateTowards(previous,desired,500f*step);
                }
                var q=t.localRotation;
                if(k>0&&Quaternion.Dot(q,rotations[t][k-1])<0)q=new Quaternion(-q.x,-q.y,-q.z,-q.w);
                rotations[t][k]=q;
            }
        }
        foreach(var entry in rotations)
        {
            var values=entry.Value;
            bool changed=values.Any(q=>Quaternion.Angle(q,values[0])>.001f);
            // Bind the same moving joints in every clip, so blending always has a defined pose.
            bool relevant=entry.Key==hip||entry.Key==torso||entry.Key==chest||entry.Key==head||entry.Key==neck||
                entry.Key.name.Contains("Hip")||entry.Key.name.Contains("Knee")||entry.Key.name.Contains("Foot")||entry.Key.name.Contains("Arm")||entry.Key.name.Contains("HandTwist")||entry.Key.name.Contains("Elbow")||entry.Key.name.Contains("Wrist")||entry.Key.name.Contains("Thumb")||
                new[]{"Index","Middle","Ring","Little","joint_migi?","joint_hidari?","joint_Skirt_0_"}.Any(f=>entry.Key.name.Contains(f));
            if(!relevant)continue;
            string path=AnimationUtility.CalculateTransformPath(entry.Key,actor.transform);
            for(int axis=0;axis<4;axis++)
            {
                int a=axis;var keys=changed?times.Select((t,k)=>new Keyframe(t,values[k][a])).ToArray():new[]{new Keyframe(0,values[0][a]),new Keyframe(duration,values[0][a])};
                SetCurve(clip,path,typeof(Transform),"m_LocalRotation."+"xyzw"[axis],keys);
            }
        }
        for(int axis=0;axis<3;axis++)
        {
            int a=axis;
            SetCurve(clip,AnimationUtility.CalculateTransformPath(hip,actor.transform),typeof(Transform),"m_LocalPosition."+"xyz"[a],times.Select((t,k)=>new Keyframe(t,hips[k][a])).ToArray());
            SetCurve(clip,"",typeof(Transform),"m_LocalPosition."+"xyz"[a],times.Select((t,k)=>new Keyframe(t,roots[k][a])).ToArray());
        }
        for(int axis=0;axis<4;axis++){int a=axis;SetCurve(clip,"",typeof(Transform),"m_LocalRotation."+"xyzw"[a],times.Select((t,k)=>new Keyframe(t,yaw[k][a])).ToArray());}
        var settings=AnimationUtility.GetAnimationClipSettings(clip);settings.loopTime=loop;settings.loopBlend=false;settings.keepOriginalOrientation=true;settings.keepOriginalPositionY=true;settings.keepOriginalPositionXZ=false;settings.heightFromFeet=false;
        AnimationUtility.SetAnimationClipSettings(clip,settings);if(events!=null)AnimationUtility.SetAnimationEvents(clip,events);clip.EnsureQuaternionContinuity();
        clip=Save(clip,Folder+"/Animations/"+name+".anim"); clips.Add(clip);return clip;
    }
    static Material Material(string name,Color color,float metallic=0)
    {
        var m=new Material(Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard")){name=name};m.color=color;m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metallic);m.SetFloat("_Glossiness",.52f);m.SetFloat("_Smoothness",.52f);
        return Save(m,Folder+"/Materials/"+name+".mat");
    }
    static void Box(Transform parent,string name,Vector3 position,Vector3 scale,Material material)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=material;Object.DestroyImmediate(go.GetComponent<Collider>());
    }
    static void Sword()
    {
        var socket=new GameObject("SwordSocket").transform;socket.SetParent(Bone("RightWrist"),false);socket.localPosition=new Vector3(.028f,-.032f,.008f);
        var bladeDirection=Vector3.RotateTowards(new Vector3(.707f,-.707f,0).normalized,Vector3.forward,60f*Mathf.Deg2Rad,0);
        socket.localRotation=Quaternion.FromToRotation(Vector3.up,bladeDirection);
        var steel=Material("SwordSteel",new Color(.75f,.82f,.94f),.65f);var gold=Material("SwordGold",new Color(.64f,.46f,.22f),.65f);var grip=Material("SwordGrip",new Color(.18f,.07f,.32f),.2f);
        Box(socket,"Grip",new Vector3(0,-.02f,0),new Vector3(.025f,.13f,.025f),grip);
        Box(socket,"Guard",new Vector3(0,.054f,0),new Vector3(.15f,.016f,.045f),gold);
        Box(socket,"Pommel",new Vector3(0,-.095f,0),new Vector3(.033f,.025f,.033f),gold);
        var mesh=new Mesh{name="KeqingSwordBlade"};
        mesh.vertices=new[]{new Vector3(-.021f,.063f,0),new Vector3(.021f,.063f,0),new Vector3(0,.063f,.009f),new Vector3(0,.063f,-.009f),new Vector3(-.013f,.75f,0),new Vector3(.013f,.75f,0),new Vector3(0,.75f,.007f),new Vector3(0,.75f,-.007f),new Vector3(0,.84f,0)};
        mesh.triangles=new[]{0,4,6,0,6,2,2,6,5,2,5,1,1,5,7,1,7,3,3,7,4,3,4,0,4,8,6,6,8,5,5,8,7,7,8,4};mesh.RecalculateNormals();mesh.RecalculateBounds();
        mesh=Save(mesh,Folder+"/Models/SwordBlade.asset");var blade=new GameObject("Blade");blade.transform.SetParent(socket,false);blade.AddComponent<MeshFilter>().sharedMesh=mesh;blade.AddComponent<MeshRenderer>().sharedMaterial=steel;
    }
    static void Scene(GameObject prefab)
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var player=(GameObject)PrefabUtility.InstantiatePrefab(prefab);player.transform.position=Vector3.zero;
        var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);ground.name="PreviewGround";ground.transform.position=new Vector3(0,-.08f,0);ground.transform.localScale=new Vector3(24,.16f,24);ground.GetComponent<Renderer>().sharedMaterial=Material("PreviewGround",new Color(.12f,.16f,.22f));
        var camera=new GameObject("PreviewCamera").AddComponent<Camera>();camera.tag="MainCamera";camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.055f,.07f,.11f);camera.nearClipPlane=.05f;camera.farClipPlane=80;
        var follow=camera.gameObject.AddComponent<KeqingPreviewCamera>();follow.target=player.transform;player.GetComponent<KeqingRootMotionController>().viewCamera=camera.transform;
        camera.transform.position=new Vector3(2,1.8f,-3.6f);camera.transform.LookAt(player.transform.position+Vector3.up*.95f);
        var light=new GameObject("KeyLight").AddComponent<Light>();light.type=LightType.Directional;light.intensity=1.25f;light.transform.rotation=Quaternion.Euler(42,-35,0);
        RenderSettings.ambientLight=new Color(.62f,.66f,.75f);
        EditorSceneManager.SaveScene(UnityEngine.SceneManagement.SceneManager.GetActiveScene(),Folder+"/KeqingRootMotionPreview.unity");
    }
    public static void Build()
    {
        try
        {
            foreach(var path in new[]{Folder,Folder+"/Animations",Folder+"/Models",Folder+"/Materials"})FolderAt(path);
            actor=new GameObject("KeqingRootMotionPlayer");
            var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath));model.name="KeqingModel";model.transform.SetParent(actor.transform,false);
            foreach(var a in model.GetComponentsInChildren<Animator>())Object.DestroyImmediate(a);
            foreach(var r in model.GetComponentsInChildren<SkinnedMeshRenderer>()){r.updateWhenOffscreen=true;r.localBounds=new Bounds(new Vector3(0,.9f,0),new Vector3(3.5f,3f,3.5f));}
            bones=model.GetComponentsInChildren<Transform>(true).Where(t=>t.name.Contains("joint_")||t.name.Contains("IK_")).ToArray();
            restPosition=bones.ToDictionary(t=>t,t=>t.localPosition);restRotation=bones.ToDictionary(t=>t,t=>t.localRotation);joints=new Dictionary<string,Transform>();
            foreach(var t in bones){string n=t.name.Substring(t.name.IndexOf('.')+1).Replace("!","");if(n.StartsWith("joint_"))joints[n.Substring(6)]=t;}
            hip=Bone("HipMaster");torso=Bone("Torso");chest=Bone("Torso2");neck=Bone("Neck");head=Bone("Head");clips=new List<AnimationClip>();CaptureSoles();InitializeReference();Apply(Guard(0),0);Sword();
            Bake("Idle",2.6f,true,t=>Guard(t));
            Bake("Walk",.96f,true,t=>Gait(t,false),new[]{new AnimationEvent{time=.001f,functionName="OnFootstep",intParameter=0},new AnimationEvent{time=.48f,functionName="OnFootstep",intParameter=1}});
            Bake("Run",.68f,true,t=>Gait(t,true),new[]{new AnimationEvent{time=.001f,functionName="OnFootstep",intParameter=0},new AnimationEvent{time=.34f,functionName="OnFootstep",intParameter=1}});
            foreach(bool run in new[]{false,true})foreach(bool stop in new[]{false,true})Bake((run?"Run":"Walk")+(stop?"Stop":"Start"),run?.38f:.32f,false,t=>Entry(t,run,stop));
            foreach(bool run in new[]{false,true})Bake((run?"Run":"Walk")+"StopRight",run?.38f:.32f,false,t=>Entry(t,run,true,true));
            Bake("TurnLeft",.46f,false,t=>Turn(t,-1));Bake("TurnRight",.46f,false,t=>Turn(t,1));
            for(int i=0;i<3;i++){int index=i;float length=new[]{.82f,.90f,1.08f}[i];Bake("Attack"+(i+1),length,false,t=>Strike(t,index),new[]{new AnimationEvent{time=length*.46f,functionName="OnAttackHit",intParameter=index},new AnimationEvent{time=length*.43f,functionName="OnFootstep",intParameter=i==1?1:0}});}
            var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(Folder+"/Animations/KeqingRootMotion.controller")??AnimatorController.CreateAnimatorControllerAtPath(Folder+"/Animations/KeqingRootMotion.controller");
            foreach(var c in clips){var state=controller.layers[0].stateMachine.states.Select(s=>s.state).FirstOrDefault(s=>s.name==c.name)??controller.layers[0].stateMachine.AddState(c.name);state.motion=c;state.writeDefaultValues=false;state.tag=c.name.StartsWith("Attack")?"Attack":"Locomotion";if(c.name=="Idle")controller.layers[0].stateMachine.defaultState=state;}
            Apply(Guard(0),0);
            var animator=actor.AddComponent<Animator>();animator.runtimeAnimatorController=controller;animator.applyRootMotion=true;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
            var avatar=AvatarBuilder.BuildGenericAvatar(actor,"");avatar.name="KeqingRootMotionAvatar";avatar=Save(avatar,Folder+"/Models/KeqingRootMotionAvatar.asset");animator.avatar=avatar;
            var capsule=actor.AddComponent<CharacterController>();capsule.height=1.65f;capsule.radius=.19f;capsule.center=new Vector3(0,.83f,0);capsule.stepOffset=.25f;capsule.skinWidth=.025f;capsule.minMoveDistance=0;
            actor.AddComponent<KeqingRootMotionController>();
            var prefab=PrefabUtility.SaveAsPrefabAsset(actor,Folder+"/KeqingRootMotionPlayer.prefab");Object.DestroyImmediate(actor);Object.DestroyImmediate(reference);Object.DestroyImmediate(soleMesh);Scene(prefab);
            AssetDatabase.SaveAssets();AssetDatabase.Refresh();
            File.WriteAllText("keqing-build-result.txt","PASS: independent Keqing prefab, Generic avatar, animation-driven controller, 14 baked 60fps clips referencing the existing Warrior motions, bounded anatomical wrist poses, actual skinned sole clearance, left/right stop variants, planted turn steps, three sword attacks, fixed strike/foot-contact events and a separate preview scene. Original model and player assets are read-only.");
            EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);File.WriteAllText("keqing-build-result.txt","FAIL: "+e);EditorApplication.Exit(1);}
    }
}
