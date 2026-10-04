using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.AI;
using Object = UnityEngine.Object;

/// <summary>Builds only the sentinel's assets; does not modify gameplay scenes.</summary>
public static class BuildMossstoneSentinel
{
    public const string Folder = "Assets/Prefabs/Enemies/MossstoneSentinel";
    public const string Fists = "Assets/Prefabs/Items/MossstoneFists";
    public const string Blade = "Assets/Prefabs/Items/MossstoneBlade";
    static readonly List<AnimationClip> clips = new List<AnimationClip>();
    static readonly Dictionary<Transform, List<Part>> parts = new Dictionary<Transform, List<Part>>();
    static Material[] materials;
    static Mesh stoneMesh, crystalMesh;
    struct Part { public Mesh mesh; public Matrix4x4 matrix; public int material; }
    static Transform Joint(Transform parent,string name,Vector3 position)
    {
        var t = new GameObject(name).transform; t.SetParent(parent,false); t.localPosition=position; return t;
    }
    static void Piece(Transform parent,Vector3 position,Vector3 size,int material,Vector3 rotation=default(Vector3),bool crystal=false)
    {
        if(!parts.TryGetValue(parent,out var list)){list=new List<Part>();parts[parent]=list;}
        list.Add(new Part{mesh=crystal?crystalMesh:stoneMesh,matrix=Matrix4x4.TRS(position,Quaternion.Euler(rotation),size),material=material});
    }
    static Mesh FlatMesh(string name,List<Vector3> vertices,List<int> triangles)
    {
        var flat=new List<Vector3>(); var ids=new List<int>();
        foreach(int i in triangles){ids.Add(flat.Count);flat.Add(vertices[i]);}
        var mesh=new Mesh{name=name};mesh.SetVertices(flat);mesh.SetTriangles(ids,0);mesh.RecalculateNormals();mesh.RecalculateBounds();return mesh;
    }
    static Mesh StoneShape()
    {
        var vertices=new List<Vector3>();var triangles=new List<int>();
        var ring=new[]{new Vector2(-.34f,-.5f),new Vector2(.34f,-.5f),new Vector2(.5f,-.34f),new Vector2(.5f,.34f),new Vector2(.34f,.5f),new Vector2(-.34f,.5f),new Vector2(-.5f,.34f),new Vector2(-.5f,-.34f)};
        // Four bevel rings, all vertices normalized to a one-meter box.
        for(int level=0;level<4;level++)foreach(var p in ring){float scale=(level==0||level==3)?.78f:1;float y=new[]{-.5f,-.34f,.34f,.5f}[level];vertices.Add(new Vector3(p.x*scale,y,p.y*scale));}
        for(int r=0;r<3;r++)for(int i=0;i<8;i++){int a=r*8+i,b=r*8+(i+1)%8,c=b+8,d=a+8;triangles.AddRange(new[]{a,c,b,a,d,c});}
        for(int i=1;i<7;i++){triangles.AddRange(new[]{0,i,(i+1),24,24+i+1,24+i});}
        return FlatMesh("BeveledStone",vertices,triangles);
    }
    static Mesh CrystalShape()
    {
        return FlatMesh("Crystal",new List<Vector3>{new Vector3(0,.5f,0),new Vector3(0,-.5f,0),new Vector3(-.5f,0,0),new Vector3(0,0,.5f),new Vector3(.5f,0,0),new Vector3(0,0,-.5f)},new List<int>{0,3,2,0,4,3,0,5,4,0,2,5,1,2,3,1,3,4,1,4,5,1,5,2});
    }
    static Material MaterialAsset(string name,Color color,float metal=0,float emission=0)
    {
        var shader=Shader.Find("Universal Render Pipeline/Lit")??Shader.Find("Standard");var m=new Material(shader){name=name,enableInstancing=true};
        m.SetColor("_Color",color);m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metal);m.SetFloat("_Smoothness",.2f);m.SetFloat("_Glossiness",.2f);
        if(emission>0){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*emission);m.globalIlluminationFlags=MaterialGlobalIlluminationFlags.RealtimeEmissive;}
        AssetDatabase.CreateAsset(m,Folder+"/Materials/"+name+".mat");return m;
    }
    static void BakeParts(string modelFolder)
    {
        foreach(var entry in parts)
        {
            var meshes=new List<Mesh>();var slots=new List<Material>();
            foreach(int index in entry.Value.Select(p=>p.material).Distinct())
            {
                var mesh=new Mesh();mesh.CombineMeshes(entry.Value.Where(p=>p.material==index).Select(p=>new CombineInstance{mesh=p.mesh,transform=p.matrix}).ToArray(),true,true);meshes.Add(mesh);slots.Add(materials[index]);
            }
            var output=new Mesh{name=entry.Key.name+"Mesh"};output.CombineMeshes(meshes.Select(m=>new CombineInstance{mesh=m,transform=Matrix4x4.identity}).ToArray(),false,false);output.RecalculateBounds();
            string asset=modelFolder+"/"+entry.Key.name+".asset";AssetDatabase.CreateAsset(output,asset);
            var body=new GameObject("Mesh");body.transform.SetParent(entry.Key,false);body.AddComponent<MeshFilter>().sharedMesh=output;body.AddComponent<MeshRenderer>().sharedMaterials=slots.ToArray();
            foreach(var m in meshes)Object.DestroyImmediate(m);
        }
        parts.Clear();
    }
    static Transform BuildBody(Transform root)
    {
        var rig=Joint(root,"Rig",Vector3.zero);
        var hips=Joint(rig,"Pelvis",new Vector3(0,1.1f,0));
        Piece(hips,new Vector3(0,.03f,0),new Vector3(.78f,.36f,.48f),0);
        Piece(hips,new Vector3(0,.08f,.25f),new Vector3(.7f,.13f,.07f),3);
        Piece(hips,new Vector3(0,-.1f,.29f),new Vector3(.42f,.4f,.12f),0,new Vector3(0,0,0));
        var torso=Joint(hips,"Torso",new Vector3(0,.23f,0));
        Piece(torso,new Vector3(0,.34f,0),new Vector3(.94f,.72f,.59f),0);
        Piece(torso,new Vector3(-.27f,.52f,.31f),new Vector3(.46f,.33f,.12f),0,new Vector3(0,0,-25));
        Piece(torso,new Vector3(.27f,.52f,.31f),new Vector3(.46f,.33f,.12f),0,new Vector3(0,0,25));
        Piece(torso,new Vector3(0,.32f,.33f),new Vector3(.42f,.48f,.09f),3,default(Vector3),true);
        Piece(torso,new Vector3(0,.34f,.4f),new Vector3(.27f,.34f,.14f),4,default(Vector3),true);
        Piece(torso,new Vector3(0,.09f,.31f),new Vector3(.4f,.08f,.08f),3);
        Piece(torso,new Vector3(0,.4f,-.31f),new Vector3(.46f,.52f,.12f),0);
        Piece(torso,new Vector3(-.2f,.66f,-.14f),new Vector3(.42f,.075f,.33f),1,new Vector3(0,-15,0));
        var head=Joint(torso,"Head",new Vector3(0,.79f,0));
        Piece(head,new Vector3(0,.19f,0),new Vector3(.4f,.5f,.38f),0);
        Piece(head,new Vector3(0,.13f,.205f),new Vector3(.21f,.3f,.06f),0);
        Piece(head,new Vector3(-.115f,.245f,.197f),new Vector3(.105f,.045f,.035f),4,new Vector3(0,0,-10));
        Piece(head,new Vector3(.115f,.245f,.197f),new Vector3(.105f,.045f,.035f),4,new Vector3(0,0,10));
        Piece(head,new Vector3(-.1f,.44f,-.035f),new Vector3(.3f,.05f,.3f),1);
        Piece(head,new Vector3(0,.24f,.225f),new Vector3(.035f,.3f,.045f),3);
        for(int side=-1;side<=1;side+=2)
        {
            string prefix=side<0?"L":"R";
            var arm=Joint(torso,prefix+"Arm",new Vector3(side*.59f,.58f,0));
            Piece(arm,new Vector3(side*.04f,-.11f,0),new Vector3(.53f,.42f,.58f),0,new Vector3(0,0,-side*12));
            Piece(arm,new Vector3(side*.05f,.09f,0),new Vector3(.43f,.07f,.47f),1,new Vector3(0,0,-side*12));
            Piece(arm,new Vector3(side*.12f,-.03f,.305f),new Vector3(.26f,.14f,.04f),3,new Vector3(0,0,-side*23));
            Piece(arm,new Vector3(side*.04f,-.36f,0),new Vector3(.25f,.32f,.27f),2);
            var forearm=Joint(arm,prefix+"Forearm",new Vector3(side*.065f,-.48f,0));
            Piece(forearm,new Vector3(0,-.2f,0),new Vector3(.34f,.43f,.37f),0,new Vector3(0,0,side*5));
            Piece(forearm,new Vector3(0,-.41f,0),new Vector3(.38f,.085f,.4f),3);
            Piece(forearm,new Vector3(side*.06f,-.065f,.13f),new Vector3(.23f,.06f,.18f),1);
            var hand=Joint(forearm,prefix+"Hand",new Vector3(0,-.46f,.02f));
            Piece(hand,new Vector3(0,-.055f,.02f),new Vector3(.32f,.23f,.3f),2);
            for(int finger=0;finger<3;finger++)Piece(hand,new Vector3((finger-1)*.085f,-.09f,.145f),new Vector3(.075f,.16f,.13f),0);
            Piece(hand,new Vector3(-side*.16f,-.065f,.065f),new Vector3(.1f,.15f,.14f),0);
            var leg=Joint(hips,prefix+"Leg",new Vector3(side*.285f,-.18f,0));
            Piece(leg,new Vector3(side*.04f,-.21f,0),new Vector3(.39f,.48f,.43f),0,new Vector3(0,0,side*5));
            var shin=Joint(leg,prefix+"Shin",new Vector3(side*.04f,-.49f,0));
            Piece(shin,new Vector3(0,-.19f,0),new Vector3(.34f,.36f,.37f),0);
            Piece(shin,new Vector3(0,.01f,.17f),new Vector3(.36f,.22f,.13f),3);
            Piece(shin,new Vector3(0,0,.23f),new Vector3(.21f,.14f,.045f),0);
            Piece(shin,new Vector3(-side*.07f,-.11f,.14f),new Vector3(.2f,.045f,.2f),1);
            var foot=Joint(shin,prefix+"Foot",new Vector3(0,-.32f,.035f));
            Piece(foot,new Vector3(0,0,.105f),new Vector3(.43f,.22f,.58f),0);
            Piece(foot,new Vector3(0,.065f,-.05f),new Vector3(.39f,.075f,.32f),3);
        }
        BakeParts(Folder+"/Models");
        return rig;
    }
    static void Curve(AnimationClip clip,string path,string property,float[] times,float[] values,bool linear=false)
    {
        var curve=new AnimationCurve(times.Select((t,i)=>new Keyframe(t,values[i])).ToArray());
        for(int i=0;i<curve.length;i++){AnimationUtility.SetKeyLeftTangentMode(curve,i,linear?AnimationUtility.TangentMode.Linear:AnimationUtility.TangentMode.Auto);AnimationUtility.SetKeyRightTangentMode(curve,i,linear?AnimationUtility.TangentMode.Linear:AnimationUtility.TangentMode.Auto);}
        AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(path,typeof(Transform),property),curve);
    }
    static AnimationClip Clip(string name,float duration,bool loop,string folder)
    {
        var c=new AnimationClip{name=name,frameRate=60};
        // The animator object is the motion root. Horizontal root curves are NOT baked into the pose.
        Curve(c,"","m_LocalPosition.x",new[]{0f,duration},new[]{0f,0f},true);
        Curve(c,"","m_LocalPosition.y",new[]{0f,duration},new[]{0f,0f},true);
        Curve(c,"","m_LocalPosition.z",new[]{0f,duration},new[]{0f,0f},true);
        var settings=AnimationUtility.GetAnimationClipSettings(c);settings.loopTime=loop;settings.loopBlend=false;settings.keepOriginalOrientation=true;settings.keepOriginalPositionY=true;settings.keepOriginalPositionXZ=false;AnimationUtility.SetAnimationClipSettings(c,settings);
        AssetDatabase.CreateAsset(c,folder+"/"+name+".anim");clips.Add(c);return c;
    }
    const string Hips="Rig/Pelvis";
    const string Torso=Hips+"/Torso";
    static void Angle(AnimationClip c,string path,string axis,float[] times,float[] values){Curve(c,path,"localEulerAnglesRaw."+axis,times,values);}
    static void Motion(AnimationClip c,float duration,float distance,float begin,float end){Curve(c,"","m_LocalPosition.z",new[]{0f,begin,end,duration},new[]{0f,0f,distance,distance});}
    static void Events(AnimationClip c,float hit,int index,float duration)
    {
        AnimationUtility.SetAnimationEvents(c,new[]{new AnimationEvent{time=hit,functionName="PublishEnemyCombo",intParameter=index},new AnimationEvent{time=duration-.1f,functionName="ChangeAttackStatu"}});
    }
    static AnimationClip Move(string name,float duration,float distance,float amplitude)
    {
        var c=Clip(name,duration,true,Folder+"/Animations");Curve(c,"","m_LocalPosition.z",new[]{0f,duration},new[]{0f,distance},true);
        var t=new[]{0f,duration*.25f,duration*.5f,duration*.75f,duration};
        for(int s=-1;s<=1;s+=2)
        {
            string p=s<0?"L":"R";
            Angle(c,Hips+"/"+p+"Leg","x",t,new[]{-s*amplitude,0f,s*amplitude,0f,-s*amplitude});
            Angle(c,Hips+"/"+p+"Leg/"+p+"Shin","x",t,new[]{0f,s<0?13f:0f,0f,s>0?13f:0f,0f});
            Angle(c,Torso+"/"+p+"Arm","x",t,new[]{s*amplitude*.6f,0f,-s*amplitude*.6f,0f,s*amplitude*.6f});
        }
        Angle(c,Torso,"z",t,new[]{-2f,0f,2f,0f,-2f});
        Curve(c,Hips,"m_LocalPosition.y",t,new[]{1.1f,1.12f,1.1f,1.12f,1.1f});return c;
    }
    static AnimationClip Punch()
    {
        float d=1.15f;var c=Clip("Fists_Attack01_Punch",d,false,Fists+"/Animations");var t=new[]{0f,.48f,.66f,d};
        Angle(c,Torso+"/RArm","x",t,new[]{0f,30f,-90f,0f});Angle(c,Torso+"/RArm/RForearm","x",t,new[]{0f,-75f,-10f,0f});Angle(c,Torso,"y",t,new[]{0f,-16f,18f,0f});Motion(c,d,.3f,.48f,.72f);Events(c,.67f,0,d);return c;
    }
    static AnimationClip FistSlam()
    {
        float d=1.55f;var c=Clip("Fists_Attack02_Slam",d,false,Fists+"/Animations");var t=new[]{0f,.72f,.94f,d};
        foreach(var p in new[]{"L","R"}){Angle(c,Torso+"/"+p+"Arm","x",t,new[]{0f,-160f,-80f,0f});Angle(c,Torso+"/"+p+"Arm/"+p+"Forearm","x",t,new[]{0f,-20f,-55f,0f});Angle(c,Torso+"/"+p+"Arm","z",t,new[]{0f,p=="L"?-19f:19f,p=="L"?-12f:12f,0f});}
        Angle(c,Torso,"x",t,new[]{0f,-12f,38f,0f});Motion(c,d,.4f,.72f,.98f);Events(c,.97f,1,d);return c;
    }
    static AnimationClip Charge()
    {
        float d=1.35f;var c=Clip("Blade_Attack01_Charge",d,false,Blade+"/Animations");var t=new[]{0f,.65f,.9f,d};
        Angle(c,Torso,"x",t,new[]{0f,17f,27f,0f});Angle(c,Torso,"y",t,new[]{0f,-28f,-28f,0f});Angle(c,Torso+"/RArm","x",t,new[]{0f,25f,40f,0f});Angle(c,Torso+"/LArm","x",t,new[]{0f,-50f,-50f,0f});Angle(c,Hips+"/RLeg","x",t,new[]{0f,8f,-20f,0f});Angle(c,Hips+"/LLeg","x",t,new[]{0f,-8f,20f,0f});Motion(c,d,.95f,.65f,.94f);Events(c,.92f,0,d);return c;
    }
    static AnimationClip Sweep()
    {
        float d=1.35f;var c=Clip("Blade_Attack02_Sweep",d,false,Blade+"/Animations");var t=new[]{0f,.56f,.86f,d};
        Angle(c,Torso+"/RArm","x",t,new[]{0f,-74f,-77f,0f});Angle(c,Torso+"/RArm","y",t,new[]{0f,65f,-70f,0f});Angle(c,Torso+"/RArm/RForearm","x",t,new[]{0f,-18f,-5f,0f});Angle(c,Torso,"y",t,new[]{0f,-30f,35f,0f});Angle(c,Torso+"/LArm","x",t,new[]{0f,-30f,-25f,0f});Motion(c,d,.35f,.56f,.9f);Events(c,.8f,1,d);return c;
    }
    static AnimationClip Cleave()
    {
        float d=1.85f;var c=Clip("Blade_Attack03_Cleave",d,false,Blade+"/Animations");var t=new[]{0f,.94f,1.18f,d};
        foreach(var p in new[]{"L","R"}){Angle(c,Torso+"/"+p+"Arm","x",t,new[]{0f,-165f,-85f,0f});Angle(c,Torso+"/"+p+"Arm/"+p+"Forearm","x",t,new[]{0f,-12f,-24f,0f});Angle(c,Torso+"/"+p+"Arm","z",t,new[]{0f,p=="L"?-28f:15f,p=="L"?-35f:15f,0f});}
        Angle(c,Torso+"/RArm/RForearm/RHand","x",t,new[]{0f,-45f,50f,0f});Angle(c,Torso,"x",t,new[]{0f,-14f,35f,0f});Motion(c,d,.55f,.94f,1.22f);Events(c,1.19f,2,d);return c;
    }
    static AnimatorStateTransition Transition(AnimatorState from,AnimatorState to,string parameter,AnimatorConditionMode mode,bool exit=false)
    {
        var t=from.AddTransition(to);t.hasExitTime=exit;t.exitTime=exit?.99f:0;t.duration=.035f;t.hasFixedDuration=true;t.canTransitionToSelf=false;if(parameter!=null)t.AddCondition(mode,0,parameter);return t;
    }
    static AnimatorController Controller(AnimationClip idle,AnimationClip walk,AnimationClip chase,AnimationClip hit,AnimationClip die,AnimationClip a,AnimationClip b,AnimationClip placeholder)
    {
        var c=AnimatorController.CreateAnimatorControllerAtPath(Folder+"/MossstoneSentinel.controller");
        c.AddParameter("isWalking",AnimatorControllerParameterType.Bool);c.AddParameter("isRunning",AnimatorControllerParameterType.Bool);
        foreach(var p in new[]{"Hit","isDied","Attack1","Attack2","Attack3"})c.AddParameter(p,AnimatorControllerParameterType.Trigger);
        var sm=c.layers[0].stateMachine;var states=new Dictionary<string,AnimatorState>();var names=new[]{"Idle","Walk","Chase","Hit","Die","Attack1","Attack2","Attack3"};var motions=new[]{idle,walk,chase,hit,die,a,b,placeholder};
        for(int i=0;i<names.Length;i++){var st=sm.AddState(names[i],new Vector3((i%4)*240,(i/4)*120,0));st.motion=motions[i];st.writeDefaultValues=true;states[names[i]]=st;}
        sm.defaultState=states["Idle"];
        Transition(states["Idle"],states["Walk"],"isWalking",AnimatorConditionMode.If);Transition(states["Walk"],states["Chase"],"isRunning",AnimatorConditionMode.If);
        Transition(states["Walk"],states["Idle"],"isWalking",AnimatorConditionMode.IfNot);Transition(states["Chase"],states["Idle"],"isWalking",AnimatorConditionMode.IfNot);Transition(states["Chase"],states["Walk"],"isRunning",AnimatorConditionMode.IfNot);
        foreach(var p in new[]{"isDied","Hit","Attack1","Attack2","Attack3"}){string name=p=="isDied"?"Die":p;var tr=sm.AddAnyStateTransition(states[name]);tr.hasExitTime=false;tr.duration=p.StartsWith("Attack")?0f:.025f;tr.canTransitionToSelf=false;tr.AddCondition(AnimatorConditionMode.If,0,p);}
        foreach(var name in new[]{"Hit","Attack1","Attack2","Attack3"})Transition(states[name],states["Idle"],null,AnimatorConditionMode.If,true);
        return c;
    }
    static AnimatorOverrideController Override(AnimatorController controller,string folder,string name,AnimationClip a,AnimationClip b,AnimationClip third,AnimationClip[] replacements)
    {
        var c=new AnimatorOverrideController(controller){name=name};c[a.name]=replacements[0];c[b.name]=replacements[1];c[third.name]=replacements[2];AssetDatabase.CreateAsset(c,folder+"/"+name+".overrideController");return c;
    }
    static void BladeModel(Transform root)
    {
        Piece(root,new Vector3(0,.025f,0),new Vector3(.075f,.34f,.075f),2);
        Piece(root,new Vector3(0,-.17f,0),new Vector3(.11f,.08f,.105f),3);
        Piece(root,new Vector3(0,.22f,0),new Vector3(.28f,.1f,.13f),3);
        Piece(root,new Vector3(0,.79f,0),new Vector3(.36f,1.06f,.12f),0,new Vector3(0,0,-5));
        Piece(root,new Vector3(.075f,1.26f,0),new Vector3(.23f,.2f,.105f),0,new Vector3(0,0,-19));
        Piece(root,new Vector3(-.1f,.74f,.065f),new Vector3(.075f,.93f,.03f),3,new Vector3(0,0,-5));
        Piece(root,new Vector3(0,.32f,.078f),new Vector3(.15f,.18f,.05f),4,default(Vector3),true);
        BakeParts(Blade+"/Models");
    }
    static GameObject Weapon(WeaponSO config,string folder,bool visible)
    {
        var go=new GameObject(visible?"MossstoneBlade":"MossstoneFists");var weapon=go.AddComponent<WeaponObj>();weapon.SO=config;weapon.showDebug=false;weapon.onlyWhenSelected=true;
        if(visible){var mesh=Joint(go.transform,"BladeMesh",Vector3.zero);BladeModel(mesh);}
        var asset=PrefabUtility.SaveAsPrefabAsset(go,folder+"/"+go.name+".prefab");Object.DestroyImmediate(go);return asset;
    }
    static void EnsureFolder(string path){if(AssetDatabase.IsValidFolder(path))return;EnsureFolder(Path.GetDirectoryName(path).Replace('\\','/'));AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\','/'),Path.GetFileName(path));}
    static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
    [MenuItem("Tools/Enemies/Build Mossstone Sentinel Resources")]
    public static void Build()
    {
        string report="mossstone-build-result.txt";
        try
        {
            Require(!AssetDatabase.IsValidFolder(Folder),"Output already exists; preserve existing assets instead of overwriting them.");
            foreach(var path in new[]{Folder,Folder+"/Models",Folder+"/Materials",Folder+"/Animations",Fists,Fists+"/Animations",Blade,Blade+"/Models",Blade+"/Animations"})EnsureFolder(path);
            clips.Clear();parts.Clear();stoneMesh=StoneShape();crystalMesh=CrystalShape();
            materials=new[]{MaterialAsset("SlateStone",new Color(.28f,.34f,.4f)),MaterialAsset("Moss",new Color(.31f,.4f,.17f)),MaterialAsset("DarkJoint",new Color(.09f,.12f,.13f)),MaterialAsset("AgedBrass",new Color(.62f,.46f,.22f),.65f),MaterialAsset("TurquoiseCore",new Color(.12f,.77f,.68f),.1f,.65f)};
            var idle=Clip("Sentinel_Idle",2.8f,true,Folder+"/Animations");var it=new[]{0f,1.4f,2.8f};Angle(idle,Torso,"x",it,new[]{0f,-1.4f,0f});Curve(idle,Hips,"m_LocalPosition.y",it,new[]{1.1f,1.11f,1.1f});
            var walk=Move("Sentinel_Walk",1.5f,1.2f,13f);var chase=Move("Sentinel_Chase",1.1f,1.76f,21f);
            var hit=Clip("Sentinel_Hit",.65f,false,Folder+"/Animations");Angle(hit,Torso,"x",new[]{0f,.18f,.65f},new[]{0f,-19f,0f});Motion(hit,.65f,-.13f,.06f,.22f);
            var die=Clip("Sentinel_Die",1.7f,false,Folder+"/Animations");Angle(die,"Rig","x",new[]{0f,.35f,1.1f,1.7f},new[]{0f,-5f,82f,90f});Curve(die,"Rig","m_LocalPosition.y",new[]{0f,.35f,1.1f,1.7f},new[]{0f,0f,.46f,.46f});AnimationUtility.SetAnimationEvents(die,new[]{new AnimationEvent{time=1.65f,functionName="Died"}});
            var a=Punch();var b=FistSlam();var third=Clip("Sentinel_Attack03_Placeholder",1.0f,false,Folder+"/Animations");
            var charge=Charge();var sweep=Sweep();var cleave=Cleave();
            var controller=Controller(idle,walk,chase,hit,die,a,b,third);
            var fistsController=Override(controller,Fists,"MossstoneFists",a,b,third,new[]{a,b,third});var bladeController=Override(controller,Blade,"MossstoneBlade",a,b,third,new[]{charge,sweep,cleave});
            var fists=ScriptableObject.CreateInstance<WeaponSO>();fists.name="MossstoneFists";fists.id="enemy_mossstone_fists";fists.itemName="苔石守卫·空手";fists.description="空物体武器，直拳与双拳砸击。";fists.stopDistance=1.7f;fists.attackBreak=1.5f;fists.overrideController=fistsController;
            fists.combo.Add(new AttackData{damagePerHit=12,radius=1.65f,angle=95,judgeOffset=new Vector3(0,1,.65f)});fists.combo.Add(new AttackData{damagePerHit=20,radius=1.9f,angle=130,judgeOffset=new Vector3(0,.8f,.85f)});AssetDatabase.CreateAsset(fists,Fists+"/MossstoneFists.asset");fists.prefab=Weapon(fists,Fists,false);EditorUtility.SetDirty(fists);
            var blade=ScriptableObject.CreateInstance<WeaponSO>();blade.name="MossstoneBlade";blade.id="enemy_mossstone_blade";blade.itemName="苔石守卫·长石刀";blade.description="冲撞、横斩、蓄力重劈。";blade.stopDistance=2.7f;blade.attackBreak=1.8f;blade.overrideController=bladeController;
            blade.combo.Add(new AttackData{damagePerHit=14,radius=1.6f,angle=90,judgeOffset=new Vector3(0,1,.75f)});blade.combo.Add(new AttackData{damagePerHit=20,radius=2.55f,angle=170,judgeOffset=new Vector3(0,1.1f,.75f)});blade.combo.Add(new AttackData{damagePerHit=30,radius=2.7f,angle=100,judgeOffset=new Vector3(0,.8f,1.1f)});AssetDatabase.CreateAsset(blade,Blade+"/MossstoneBlade.asset");blade.prefab=Weapon(blade,Blade,true);EditorUtility.SetDirty(blade);
            var so=ScriptableObject.CreateInstance<EnemySO>();so.name="MossstoneSentinel";so.enemyID="Enemy_MossstoneSentinel";so.targetName="苔石守卫";so.description="慢速石质守卫，默认空手二连击。";so.maxHp=260;so.initialHp=260;so.atk=20;so.speed=.8f;so.chaseSpeed=1.6f;so.turnSpeed=180;so.chaseDistance=18;so.hitStun=.65f;so.defaultWeaponSO=fists;AssetDatabase.CreateAsset(so,Folder+"/MossstoneSentinel.asset");
            var root=PrefabUtility.LoadPrefabContents("Assets/Prefabs/TestEnemy/TestEnemy.prefab");
            try
            {
                root.name="MossstoneSentinel";root.layer=0;root.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);root.transform.localScale=Vector3.one;
                var old=root.transform.Find("foot");if(old)Object.DestroyImmediate(old.gameObject);
                var rig=BuildBody(root.transform);rig.name="MossstoneSentinelModel";PrefabUtility.SaveAsPrefabAsset(rig.gameObject,Folder+"/Models/MossstoneSentinelModel.prefab");rig.name="Rig";
                var hand=rig.Find("Pelvis/Torso/RArm/RForearm/RHand");var socket=Joint(hand,"WeaponSocket",new Vector3(0,-.02f,.06f));socket.localRotation=Quaternion.Euler(125,0,0);
                var obj=root.GetComponent<EnemyObj>();var ai=root.GetComponent<EnemyAI>();var anim=root.GetComponent<Animator>();var hp=root.GetComponent<EnemyHPBar>();var agent=root.GetComponent<NavMeshAgent>();
                obj.SO=so;obj.handPos=socket;obj.animator=anim;obj.enemyAI=ai;obj.HPBar=hp;obj.isDead=false;obj.enemyName.text="苔石守卫";
                ai.animator=anim;ai.enemyObj=obj;ai.self=agent;ai.triggerCollider=root.GetComponent<BoxCollider>();ai.Player=null;ai.maxComboIndex=-1;ai.comboIndex=0;ai.isAttacking=false;
                agent.height=2.6f;agent.radius=.62f;agent.baseOffset=0;agent.speed=1.6f;agent.angularSpeed=180;agent.stoppingDistance=1.7f;
                ai.triggerCollider.center=new Vector3(0,1.3f,0);ai.triggerCollider.size=Vector3.one;ai.triggerCollider.isTrigger=true;
                var collision=Joint(root.transform,"BodyCollider",Vector3.zero).gameObject.AddComponent<CapsuleCollider>();collision.gameObject.layer=LayerMask.NameToLayer("enemy");collision.height=2.58f;collision.radius=.58f;collision.center=new Vector3(0,1.3f,0);
                root.GetComponent<Rigidbody>().isKinematic=true;root.GetComponent<Rigidbody>().useGravity=false;
                hp.enemy=obj;hp.worldCanvas.localPosition=new Vector3(0,3.04f,0);hp.worldCanvas.localRotation=Quaternion.identity;
                anim.runtimeAnimatorController=controller;anim.applyRootMotion=true;anim.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                // A Generic avatar anchors extraction to the animator's own transform.
                var avatar=AvatarBuilder.BuildGenericAvatar(root,"");avatar.name="MossstoneSentinelAvatar";AssetDatabase.CreateAsset(avatar,Folder+"/Models/MossstoneSentinelAvatar.asset");anim.avatar=avatar;
                so.prefab=PrefabUtility.SaveAsPrefabAsset(root,Folder+"/MossstoneSentinel.prefab");EditorUtility.SetDirty(so);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            var alternate=Object.Instantiate(so);alternate.name="MossstoneSentinel_Blade";alternate.enemyID="Enemy_MossstoneSentinel_Blade";alternate.defaultWeaponSO=blade;alternate.description="长石刀资源配置：冲撞、横斩、重劈；共用身体预制体。";AssetDatabase.CreateAsset(alternate,Folder+"/MossstoneSentinel_Blade.asset");
            AssetDatabase.SaveAssets();AssetDatabase.Refresh();
            Validate(so,alternate);
            File.WriteAllText(report,"PASS\nBody prefab: "+Folder+"/MossstoneSentinel.prefab\nBody height: 2.6m\nWalk: 0.8m/s; chase: 1.6m/s\nFists: 1.15s punch, 1.55s slam; 1.5s break\nBlade: 1.35s shoulder charge, 1.35s sweep, 1.85s cleave; 1.8s break\nAnimations: "+clips.Count+"\nMesh renderers: "+so.prefab.GetComponentsInChildren<MeshRenderer>().Length+"\nTriangles: "+so.prefab.GetComponentsInChildren<MeshFilter>().Sum(m=>m.sharedMesh.triangles.Length/3)+"\nNo scene changes. Default fists have no mesh. Both loadouts share the body prefab. Native serialization, component references, curves and animation events checked.");
            Debug.Log("[MossstoneSentinel] Resource build and static validation passed.");
        }
        catch(Exception e){File.WriteAllText(report,"FAIL\n"+e);Debug.LogException(e);throw;}
        finally{if(stoneMesh)Object.DestroyImmediate(stoneMesh);if(crystalMesh)Object.DestroyImmediate(crystalMesh);}
        if(Application.isBatchMode)EditorApplication.Exit(0);
    }
    static void Validate(EnemySO a,EnemySO b)
    {
        Require(a.prefab==b.prefab,"Loadout prefab mismatch");var p=a.prefab;var obj=p.GetComponent<EnemyObj>();var ai=p.GetComponent<EnemyAI>();var hp=p.GetComponent<EnemyHPBar>();
        Require(obj.SO==a&&obj.handPos&&obj.enemyName&&obj.enemyAI==ai&&obj.HPBar==hp,"Enemy references");Require(ai.self&&ai.triggerCollider&&ai.animator&&ai.enemyObj==obj,"AI references");Require(hp.fill&&hp.background&&hp.buffer&&hp.hpPrompt&&hp.worldCanvas&&hp.enemy==obj,"Health references");
        Require(p.GetComponentsInChildren<Transform>(true).All(t=>GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0),"Missing scripts");
        Require(a.defaultWeaponSO.prefab.GetComponent<WeaponObj>()&&a.defaultWeaponSO.prefab.GetComponentsInChildren<Renderer>().Length==0,"Fists must be an empty weapon object");Require(a.defaultWeaponSO.combo.Count==2&&b.defaultWeaponSO.combo.Count==3,"Combo counts");
        foreach(var clip in clips)
        {
            foreach(var binding in AnimationUtility.GetCurveBindings(clip)){Require(binding.path==""||p.transform.Find(binding.path),"Unresolved animation path "+binding.path);if(binding.path==""&&binding.propertyName=="m_LocalPosition.y")Require(AnimationUtility.GetEditorCurve(clip,binding).keys.All(k=>Mathf.Abs(k.value)<.0001f),"Vertical root drift");}
            foreach(var evt in AnimationUtility.GetAnimationEvents(clip)){Require(evt.time<clip.length,"Event at/beyond clip end");if(evt.functionName=="PublishEnemyCombo")Require(evt.intParameter>=0&&evt.intParameter<=2,"Combo event index");}
        }
        foreach(var controller in new[]{a.defaultWeaponSO.overrideController,b.defaultWeaponSO.overrideController})Require(controller.runtimeAnimatorController!=null,"Missing override base");
    }
}
