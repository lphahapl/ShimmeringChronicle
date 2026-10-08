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
public static class BuildMossstoneSentinelV2
{
    public const string Folder = "Assets/Prefabs/Enemies/ReinforcedSentinel";
    public const string Fists = "Assets/Prefabs/Items/ReinforcedFists";
    public const string Blade = "Assets/Prefabs/Items/ReinforcedBlade";
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
        var hips=Joint(rig,"Pelvis",new Vector3(0,1.16f,0));
        Piece(hips,new Vector3(0,.025f,0),new Vector3(.9f,.36f,.58f),0);
        Piece(hips,new Vector3(0,.09f,.3f),new Vector3(.86f,.1f,.06f),3);
        Piece(hips,new Vector3(0,-.16f,.34f),new Vector3(.4f,.44f,.1f),0);
        Piece(hips,new Vector3(-.31f,-.13f,.31f),new Vector3(.25f,.37f,.11f),0,new Vector3(0,0,-14));
        Piece(hips,new Vector3(.31f,-.13f,.31f),new Vector3(.25f,.37f,.11f),0,new Vector3(0,0,14));
        var torso=Joint(hips,"Torso",new Vector3(0,.25f,0));
        // A broad upper chest, recessed head and fixed pauldrons create a heavy silhouette.
        Piece(torso,new Vector3(0,.41f,-.03f),new Vector3(1.12f,.85f,.73f),0);
        Piece(torso,new Vector3(-.3f,.52f,.34f),new Vector3(.58f,.4f,.18f),0,new Vector3(0,-8,-23));
        Piece(torso,new Vector3(.3f,.52f,.34f),new Vector3(.58f,.4f,.18f),0,new Vector3(0,8,23));
        Piece(torso,new Vector3(0,.35f,.405f),new Vector3(.31f,.39f,.06f),2,default(Vector3),true);
        Piece(torso,new Vector3(0,.37f,.449f),new Vector3(.145f,.225f,.055f),4,default(Vector3),true);
        Piece(torso,new Vector3(-.16f,.37f,.424f),new Vector3(.025f,.36f,.023f),3,new Vector3(0,0,-18));
        Piece(torso,new Vector3(.16f,.37f,.424f),new Vector3(.025f,.36f,.023f),3,new Vector3(0,0,18));
        Piece(torso,new Vector3(0,.07f,.365f),new Vector3(.61f,.15f,.11f),0);
        Piece(torso,new Vector3(0,.47f,-.42f),new Vector3(.76f,.73f,.2f),0);
        Piece(torso,new Vector3(0,.66f,-.55f),new Vector3(.23f,.45f,.11f),3);
        Piece(torso,new Vector3(0,.77f,0),new Vector3(1.02f,.2f,.71f),0);
        for(int side=-1;side<=1;side+=2)
        {
            Piece(torso,new Vector3(side*.88f,.76f,-.07f),new Vector3(.72f,.42f,.8f),0,new Vector3(0,side*8,-side*9));
            Piece(torso,new Vector3(side*1.03f,.98f,-.13f),new Vector3(.3f,.4f,.44f),0,new Vector3(-12,side*12,-side*32));
            Piece(torso,new Vector3(side*.82f,.99f,.015f),new Vector3(.31f,.16f,.38f),1,new Vector3(0,side*14,-side*12));
            Piece(torso,new Vector3(side*.93f,.79f,.35f),new Vector3(.39f,.07f,.04f),3,new Vector3(0,0,-side*9));
        }
        var head=Joint(torso,"Head",new Vector3(0,.95f,-.02f));
        Piece(head,new Vector3(0,.16f,-.025f),new Vector3(.52f,.5f,.49f),0);
        Piece(head,new Vector3(0,.22f,.24f),new Vector3(.47f,.18f,.075f),2);
        Piece(head,new Vector3(-.13f,.255f,.282f),new Vector3(.12f,.026f,.015f),4,new Vector3(0,0,-12));
        Piece(head,new Vector3(.13f,.255f,.282f),new Vector3(.12f,.026f,.015f),4,new Vector3(0,0,12));
        Piece(head,new Vector3(-.14f,.34f,.26f),new Vector3(.31f,.12f,.13f),0,new Vector3(0,0,-12));
        Piece(head,new Vector3(.14f,.34f,.26f),new Vector3(.31f,.12f,.13f),0,new Vector3(0,0,12));
        Piece(head,new Vector3(0,.16f,.294f),new Vector3(.07f,.2f,.12f),0);
        Piece(head,new Vector3(-.18f,.075f,.25f),new Vector3(.19f,.22f,.14f),0,new Vector3(0,0,15));
        Piece(head,new Vector3(.18f,.075f,.25f),new Vector3(.19f,.22f,.14f),0,new Vector3(0,0,-15));
        Piece(head,new Vector3(0,-.015f,.245f),new Vector3(.36f,.09f,.13f),0);
        Piece(head,new Vector3(0,.43f,-.04f),new Vector3(.26f,.32f,.28f),0,new Vector3(-8,0,0));
        Piece(head,new Vector3(-.22f,.38f,-.03f),new Vector3(.14f,.35f,.25f),0,new Vector3(0,0,-22));
        Piece(head,new Vector3(.22f,.38f,-.03f),new Vector3(.14f,.35f,.25f),0,new Vector3(0,0,22));
        Piece(head,new Vector3(-.15f,.535f,-.08f),new Vector3(.18f,.045f,.16f),1,new Vector3(0,15,0));
        for(int side=-1;side<=1;side+=2)
        {
            string prefix=side<0?"L":"R";
            var arm=Joint(torso,prefix+"Arm",new Vector3(side*.87f,.56f,-.025f));arm.localRotation=Quaternion.Euler(-8,0,side*15);
            Piece(arm,new Vector3(0,-.255f,0),new Vector3(.35f,.53f,.37f),2);
            Piece(arm,new Vector3(side*.07f,-.235f,-.01f),new Vector3(.38f,.43f,.4f),0);
            var forearm=Joint(arm,prefix+"Forearm",new Vector3(0,-.58f,0));forearm.localRotation=Quaternion.Euler(-8,0,0);
            Piece(forearm,new Vector3(0,-.22f,0),new Vector3(.49f,.49f,.51f),0);
            Piece(forearm,new Vector3(side*.12f,-.1f,-.02f),new Vector3(.2f,.32f,.43f),0,new Vector3(0,0,side*12));
            Piece(forearm,new Vector3(0,-.43f,0),new Vector3(.45f,.07f,.48f),3);
            Piece(forearm,new Vector3(side*.075f,-.04f,.17f),new Vector3(.22f,.05f,.17f),1);
            var hand=Joint(forearm,prefix+"Hand",new Vector3(0,-.485f,.025f));
            Piece(hand,new Vector3(0,-.085f,.025f),new Vector3(.4f,.29f,.38f),2);
            for(int finger=0;finger<4;finger++)Piece(hand,new Vector3((finger-1.5f)*.094f,-.12f,.215f),new Vector3(.089f,.2f,.14f),0);
            Piece(hand,new Vector3(-side*.205f,-.06f,.07f),new Vector3(.12f,.22f,.17f),0);
            Piece(hand,new Vector3(0,-.035f,-.175f),new Vector3(.34f,.15f,.055f),0);
            var leg=Joint(hips,prefix+"Leg",new Vector3(side*.335f,-.2f,0));
            Piece(leg,new Vector3(0,-.215f,0),new Vector3(.47f,.46f,.5f),0);
            Piece(leg,new Vector3(side*.1f,-.06f,-.03f),new Vector3(.21f,.25f,.4f),0,new Vector3(0,0,side*12));
            var shin=Joint(leg,prefix+"Shin",new Vector3(0,-.49f,0));
            Piece(shin,new Vector3(0,-.17f,0),new Vector3(.42f,.39f,.45f),0);
            Piece(shin,new Vector3(0,.01f,.235f),new Vector3(.4f,.2f,.12f),0);
            Piece(shin,new Vector3(0,.025f,.3f),new Vector3(.26f,.045f,.035f),3);
            var foot=Joint(shin,prefix+"Foot",new Vector3(0,-.35f,.04f));
            Piece(foot,new Vector3(0,0,.13f),new Vector3(.53f,.24f,.7f),0);
            Piece(foot,new Vector3(0,.065f,-.045f),new Vector3(.45f,.08f,.31f),3);
        }
        BakeParts(Folder+"/Models");return rig;
    }
    static void Curve(AnimationClip clip,string path,string property,float[] times,float[] values,bool linear=false)
    {
        var curve=new AnimationCurve(times.Select((t,i)=>new Keyframe(t,values[i])).ToArray());
        for(int i=0;i<curve.length;i++){AnimationUtility.SetKeyLeftTangentMode(curve,i,linear?AnimationUtility.TangentMode.Linear:AnimationUtility.TangentMode.Auto);AnimationUtility.SetKeyRightTangentMode(curve,i,linear?AnimationUtility.TangentMode.Linear:AnimationUtility.TangentMode.Auto);}
        AnimationUtility.SetEditorCurve(clip,EditorCurveBinding.FloatCurve(path,typeof(Transform),property),curve);
    }
    static AnimationClip Clip(string name,float duration,bool loop,string folder)
    {
        var c=AssetDatabase.LoadAssetAtPath<AnimationClip>(folder+"/"+name+".anim");
        bool existing=c!=null;
        if(existing)c.ClearCurves();else c=new AnimationClip();
        c.name=name;c.frameRate=60;
        // The animator object is the motion root. Horizontal root curves are NOT baked into the pose.
        Curve(c,"","m_LocalPosition.x",new[]{0f,duration},new[]{0f,0f},true);
        Curve(c,"","m_LocalPosition.y",new[]{0f,duration},new[]{0f,0f},true);
        Curve(c,"","m_LocalPosition.z",new[]{0f,duration},new[]{0f,0f},true);
        var settings=AnimationUtility.GetAnimationClipSettings(c);settings.loopTime=loop;settings.loopBlend=false;settings.keepOriginalOrientation=true;settings.keepOriginalPositionY=true;settings.keepOriginalPositionXZ=false;AnimationUtility.SetAnimationClipSettings(c,settings);
        RestPose(c,duration);
        if(!existing)AssetDatabase.CreateAsset(c,folder+"/"+name+".anim");EditorUtility.SetDirty(c);clips.Add(c);return c;
    }
    const string Hips="Rig/Pelvis";
    const string Torso=Hips+"/Torso";
    static void Angle(AnimationClip c,string path,string axis,float[] times,float[] values){Curve(c,path,"localEulerAnglesRaw."+axis,times,values);}
    static void Motion(AnimationClip c,float duration,float distance,float begin,float end){Curve(c,"","m_LocalPosition.z",new[]{0f,begin,end,duration},new[]{0f,0f,distance,distance});}
    static void Events(AnimationClip c,float hit,int index,float duration)
    {
        AnimationUtility.SetAnimationEvents(c,new[]{new AnimationEvent{time=hit,functionName="PublishEnemyCombo",intParameter=index},new AnimationEvent{time=duration-.08f,functionName="ChangeAttackStatu"}});
    }
    static void RestPose(AnimationClip c,float duration)
    {
        var t=new[]{0f,duration};
        foreach(var path in new[]{"Rig",Hips,Torso,Torso+"/Head",Torso+"/LArm",Torso+"/RArm",Torso+"/LArm/LForearm",Torso+"/RArm/RForearm",Torso+"/LArm/LForearm/LHand",Torso+"/RArm/RForearm/RHand",Hips+"/LLeg",Hips+"/RLeg",Hips+"/LLeg/LShin",Hips+"/RLeg/RShin",Hips+"/LLeg/LShin/LFoot",Hips+"/RLeg/RShin/RFoot"})
        foreach(var axis in new[]{"x","y","z"})
        {
            float value=0;
            if(path==Torso&&axis=="x")value=-4;
            if((path.EndsWith("Arm")||path.EndsWith("Forearm"))&&axis=="x")value=-8;
            if(path.EndsWith("LArm")&&axis=="z")value=-15;
            if(path.EndsWith("RArm")&&axis=="z")value=15;
            Angle(c,path,axis,t,new[]{value,value});
        }
        foreach(var axis in new[]{"x","y","z"})Curve(c,"Rig","m_LocalPosition."+axis,t,new[]{0f,0f});
        Curve(c,Hips,"m_LocalPosition.y",t,new[]{1.16f,1.16f});
    }
    static void Knees(AnimationClip c,float[] t,float[] bend)
    {
        foreach(var p in new[]{"L","R"})
        {
            Angle(c,Hips+"/"+p+"Leg","x",t,bend.Select(v=>-v*.5f).ToArray());
            Angle(c,Hips+"/"+p+"Leg/"+p+"Shin","x",t,bend);
            Angle(c,Hips+"/"+p+"Leg/"+p+"Shin/"+p+"Foot","x",t,bend.Select(v=>-v*.5f).ToArray());
        }
    }
    static AnimationClip Move(string name,float duration,float distance,float amplitude)
    {
        var c=Clip(name,duration,true,Folder+"/Animations");Curve(c,"","m_LocalPosition.z",new[]{0f,duration},new[]{0f,distance},true);
        var t=new[]{0f,duration*.25f,duration*.5f,duration*.75f,duration};
        for(int side=-1;side<=1;side+=2)
        {
            string p=side<0?"L":"R";
            Angle(c,Hips+"/"+p+"Leg","x",t,new[]{-side*amplitude,0f,side*amplitude,0f,-side*amplitude});
            Angle(c,Hips+"/"+p+"Leg/"+p+"Shin","x",t,new[]{0f,side<0?18f:0f,0f,side>0?18f:0f,0f});
            Angle(c,Torso+"/"+p+"Arm","x",t,new[]{-8+side*amplitude*.35f,-8f,-8-side*amplitude*.35f,-8f,-8+side*amplitude*.35f});
        }
        Angle(c,Torso,"z",t,new[]{-2.5f,0f,2.5f,0f,-2.5f});return c;
    }
    static AnimationClip Punch()
    {
        float d=1.8f;var c=Clip("ReinforcedFists_Attack01_Punch",d,false,Fists+"/Animations");
        var t=new[]{0f,.24f,.52f,.66f,.72f,.86f,1.12f,1.48f,1.68f,d};
        Angle(c,Torso,"y",t,new[]{0f,-16f,-32f,18f,30f,36f,32f,15f,0f,0f});
        Angle(c,Torso,"x",t,new[]{-4f,-8f,-13f,2f,11f,17f,15f,3f,-4f,-4f});
        // Left shoulder drives forward; the elbow visibly straightens at impact.
        Angle(c,Torso+"/LArm","x",t,new[]{-8f,16f,28f,-90f,-106f,-109f,-100f,-30f,-8f,-8f});
        Angle(c,Torso+"/LArm","y",t,new[]{0f,-15f,-22f,-12f,-8f,-5f,-5f,-8f,0f,0f});
        Angle(c,Torso+"/LArm","z",t,new[]{-15f,-25f,-35f,-12f,-8f,-12f,-14f,-28f,-15f,-15f});
        Angle(c,Torso+"/LArm/LForearm","x",t,new[]{-8f,-45f,-80f,-16f,-2f,-2f,-8f,-55f,-8f,-8f});
        Angle(c,Torso+"/RArm","x",t,new[]{-8f,-28f,-38f,-35f,-22f,-12f,-10f,-22f,-8f,-8f});
        Angle(c,Torso+"/RArm/RForearm","x",t,new[]{-8f,-45f,-60f,-52f,-38f,-30f,-28f,-34f,-8f,-8f});
        Knees(c,t,new[]{0f,14f,22f,12f,20f,30f,30f,14f,0f,0f});
        Curve(c,"","m_LocalPosition.z",t,new[]{0f,0f,-.01f,.08f,.11f,.13f,.13f,.12f,.12f,.12f});
        Events(c,.70f,0,d);return c;
    }
    static AnimationClip FistSlam()
    {
        float d=2.4f;var c=Clip("ReinforcedFists_Attack02_GroundSlap",d,false,Fists+"/Animations");
        var t=new[]{0f,.35f,.72f,.92f,1.02f,1.16f,1.4f,1.7f,2.05f,2.28f,d};
        foreach(var p in new[]{"L","R"})
        {
            int side=p=="L"?-1:1;
            Angle(c,Torso+"/"+p+"Arm","x",t,new[]{-8f,-65f,-158f,-130f,-80f,-80f,-80f,-95f,-28f,-8f,-8f});
            Angle(c,Torso+"/"+p+"Arm","z",t,new[]{side*15f,side*28f,side*38f,side*20f,0f,0f,0f,side*12f,side*23f,side*15f,side*15f});
            Angle(c,Torso+"/"+p+"Arm/"+p+"Forearm","x",t,new[]{-8f,-35f,-18f,-35f,-90f,-90f,-90f,-65f,-25f,-8f,-8f});
        }
        Angle(c,Torso,"x",t,new[]{-4f,-12f,-20f,30f,80f,80f,80f,52f,10f,-4f,-4f});
        Angle(c,Torso,"y",t,new float[t.Length]);
        Knees(c,t,new[]{0f,10f,4f,45f,167f,167f,167f,78f,18f,0f,0f});
        Curve(c,"","m_LocalPosition.z",t,new float[t.Length]);
        Events(c,1.02f,1,d);return c;
    }
    static AnimationClip Charge()
    {
        float d=2.15f;var c=Clip("ReinforcedBlade_Attack01_Charge",d,false,Blade+"/Animations");
        var t=new[]{0f,.35f,.7f,.9f,1.03f,1.18f,1.42f,1.7f,2.03f,d};
        Angle(c,Torso,"x",t,new[]{-4f,8f,23f,31f,38f,44f,36f,15f,-4f,-4f});
        Angle(c,Torso,"y",t,new[]{0f,18f,30f,35f,35f,41f,35f,18f,0f,0f});
        Angle(c,Torso+"/LArm","x",t,new[]{-8f,-28f,-48f,-52f,-52f,-48f,-44f,-26f,-8f,-8f});
        Angle(c,Torso+"/LArm/LForearm","x",t,new[]{-8f,-70f,-92f,-90f,-85f,-80f,-75f,-45f,-8f,-8f});
        Angle(c,Torso+"/LArm","z",t,new[]{-15f,-23f,-32f,-32f,-35f,-38f,-38f,-25f,-15f,-15f});
        Angle(c,Torso+"/RArm","x",t,new[]{-8f,0f,5f,8f,15f,20f,18f,10f,-8f,-8f});
        Angle(c,Torso+"/RArm","z",t,new[]{15f,28f,40f,42f,45f,45f,42f,30f,15f,15f});
        Angle(c,Torso+"/RArm/RForearm","x",t,new[]{-8f,-26f,-35f,-38f,-42f,-48f,-42f,-25f,-8f,-8f});
        Angle(c,Torso+"/RArm/RForearm/RHand","x",t,new[]{0f,-8f,-22f,-25f,-30f,-35f,-26f,-10f,0f,0f});
        Knees(c,t,new[]{0f,15f,28f,20f,35f,45f,42f,24f,0f,0f});
        Curve(c,"","m_LocalPosition.z",t,new[]{0f,-.035f,-.06f,.3f,.54f,.67f,.73f,.73f,.70f,.70f});
        Events(c,1.03f,0,d);return c;
    }
    static AnimationClip Sweep()
    {
        float d=2.55f;var c=Clip("ReinforcedBlade_Attack02_Sweep",d,false,Blade+"/Animations");
        var t=new[]{0f,.35f,.78f,1.04f,1.15f,1.28f,1.47f,1.7f,2.13f,2.43f,d};
        Angle(c,Torso,"y",t,new[]{0f,18f,38f,-5f,-38f,-57f,-68f,-62f,-28f,0f,0f});
        Angle(c,Torso,"x",t,new[]{-4f,-7f,-12f,2f,10f,16f,20f,18f,4f,-4f,-4f});
        Angle(c,Torso+"/RArm","x",t,new[]{-8f,-48f,-85f,-88f,-86f,-84f,-80f,-78f,-62f,-8f,-8f});
        Angle(c,Torso+"/RArm","y",t,new[]{0f,35f,62f,24f,-18f,-35f,-44f,-40f,-20f,0f,0f});
        Angle(c,Torso+"/RArm","z",t,new[]{15f,22f,25f,18f,18f,20f,24f,25f,25f,15f,15f});
        Angle(c,Torso+"/RArm/RForearm","x",t,new[]{-8f,-25f,-12f,-8f,-8f,-8f,-12f,-16f,-36f,-8f,-8f});
        Angle(c,Torso+"/RArm/RForearm/RHand","x",t,new[]{0f,35f,62f,55f,47f,40f,32f,32f,42f,0f,0f});
        Angle(c,Torso+"/LArm","x",t,new[]{-8f,-28f,-38f,-24f,8f,20f,28f,24f,5f,-8f,-8f});
        Angle(c,Torso+"/LArm","z",t,new[]{-15f,-25f,-35f,-32f,-35f,-40f,-45f,-42f,-28f,-15f,-15f});
        Knees(c,t,new[]{0f,16f,26f,20f,30f,40f,42f,38f,20f,0f,0f});
        Curve(c,"","m_LocalPosition.z",t,new float[t.Length]);
        Events(c,1.15f,1,d);return c;
    }
    static AnimationClip Cleave()
    {
        float d=3.2f;var c=Clip("ReinforcedBlade_Attack03_GroundSlap",d,false,Blade+"/Animations");
        var t=new[]{0f,.4f,1f,1.35f,1.51f,1.68f,1.92f,2.28f,2.68f,3.08f,d};
        // Stay facing forward: the bent left forearm hits the ground; right keeps the blade aside.
        Angle(c,Torso,"x",t,new[]{-4f,-10f,-19f,30f,80f,80f,80f,52f,15f,-4f,-4f});
        Angle(c,Torso,"y",t,new float[t.Length]);
        Angle(c,Torso+"/LArm","x",t,new[]{-8f,-75f,-158f,-132f,-80f,-80f,-80f,-95f,-45f,-8f,-8f});
        Angle(c,Torso+"/LArm/LForearm","x",t,new[]{-8f,-24f,-18f,-35f,-90f,-90f,-90f,-65f,-32f,-8f,-8f});
        Angle(c,Torso+"/LArm","z",t,new[]{-15f,-25f,-38f,-20f,0f,0f,0f,-12f,-25f,-15f,-15f});
        Angle(c,Torso+"/RArm","x",t,new[]{-8f,-25f,-55f,-65f,-68f,-68f,-68f,-60f,-35f,-8f,-8f});
        Angle(c,Torso+"/RArm","z",t,new[]{15f,28f,42f,48f,50f,50f,50f,45f,30f,15f,15f});
        Angle(c,Torso+"/RArm/RForearm","x",t,new[]{-8f,-35f,-60f,-100f,-110f,-110f,-110f,-90f,-45f,-8f,-8f});
        Angle(c,Torso+"/RArm/RForearm/RHand","x",t,new[]{0f,-10f,-20f,-25f,-30f,-30f,-30f,-25f,-10f,0f,0f});
        Knees(c,t,new[]{0f,12f,6f,45f,167f,167f,167f,78f,22f,0f,0f});
        Curve(c,"","m_LocalPosition.z",t,new float[t.Length]);
        Events(c,1.51f,2,d);return c;
    }
    public static void ReviseAttacks()
    {
        clips.Clear();Punch();FistSlam();Charge();Sweep();Cleave();
        var root=PrefabUtility.LoadPrefabContents(Folder+"/ReinforcedSentinel.prefab");
        try { GroundAndClearanceMossstoneV2.Process(root,clips,AssetDatabase.LoadAssetAtPath<GameObject>(Blade+"/ReinforcedBlade.prefab"));
            var idle=AssetDatabase.LoadAssetAtPath<AnimationClip>(Folder+"/Animations/Reinforced_Idle.anim");
            StanceAndStrideSentinel.Process(root,clips.Concat(new[]{idle}).ToList()); }
        finally { PrefabUtility.UnloadPrefabContents(root); }
        AssetDatabase.SaveAssets();
    }
    static AnimatorStateTransition Transition(AnimatorState from,AnimatorState to,string parameter,AnimatorConditionMode mode,bool exit=false)
    {
        var t=from.AddTransition(to);t.hasExitTime=exit;t.exitTime=exit?1.03f:0;t.duration=.035f;t.hasFixedDuration=true;t.canTransitionToSelf=false;if(parameter!=null)t.AddCondition(mode,0,parameter);return t;
    }
    static AnimatorController Controller(AnimationClip idle,AnimationClip walk,AnimationClip chase,AnimationClip hit,AnimationClip die,AnimationClip a,AnimationClip b,AnimationClip placeholder)
    {
        var c=AnimatorController.CreateAnimatorControllerAtPath(Folder+"/ReinforcedSentinel.controller");
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
        Piece(root,new Vector3(0,.02f,0),new Vector3(.105f,.4f,.105f),2);
        Piece(root,new Vector3(0,-.2f,0),new Vector3(.16f,.105f,.15f),3);
        Piece(root,new Vector3(0,.26f,0),new Vector3(.39f,.13f,.17f),3);
        Piece(root,new Vector3(.04f,1.0f,0),new Vector3(.47f,1.45f,.17f),0,new Vector3(0,0,-4));
        Piece(root,new Vector3(.115f,1.69f,0),new Vector3(.3f,.25f,.145f),0,new Vector3(0,0,-21));
        Piece(root,new Vector3(-.155f,1.0f,.095f),new Vector3(.045f,1.2f,.022f),3,new Vector3(0,0,-4));
        Piece(root,new Vector3(0,.425f,.1f),new Vector3(.1f,.18f,.035f),4,default(Vector3),true);
        Piece(root,new Vector3(.215f,1.09f,.015f),new Vector3(.04f,1.2f,.12f),2,new Vector3(0,0,-4));
        BakeParts(Blade+"/Models");
    }
    static GameObject Weapon(WeaponSO config,string folder,bool visible)
    {
        var go=new GameObject(visible?"ReinforcedBlade":"ReinforcedFists");var weapon=go.AddComponent<WeaponObj>();weapon.SO=config;weapon.showDebug=true;weapon.onlyWhenSelected=false;
        if(visible){var mesh=Joint(go.transform,"BladeMesh",Vector3.zero);BladeModel(mesh);}
        var asset=PrefabUtility.SaveAsPrefabAsset(go,folder+"/"+go.name+".prefab");Object.DestroyImmediate(go);return asset;
    }
    static void EnsureFolder(string path){if(AssetDatabase.IsValidFolder(path))return;EnsureFolder(Path.GetDirectoryName(path).Replace('\\','/'));AssetDatabase.CreateFolder(Path.GetDirectoryName(path).Replace('\\','/'),Path.GetFileName(path));}
    static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
    [MenuItem("Tools/Enemies/Build Reinforced Sentinel Resources")]
    public static void Build()
    {
        string report="v2-build-result.txt";
        try
        {
            Require(!AssetDatabase.IsValidFolder(Folder),"Output already exists; preserve existing assets instead of overwriting them.");
            foreach(var path in new[]{Folder,Folder+"/Models",Folder+"/Materials",Folder+"/Animations",Fists,Fists+"/Animations",Blade,Blade+"/Models",Blade+"/Animations"})EnsureFolder(path);
            clips.Clear();parts.Clear();stoneMesh=StoneShape();crystalMesh=CrystalShape();
            materials=new[]{MaterialAsset("SlateStone",new Color(.12f,.17f,.19f)),MaterialAsset("Moss",new Color(.18f,.24f,.1f)),MaterialAsset("DarkJoint",new Color(.035f,.05f,.055f)),MaterialAsset("AgedBrass",new Color(.37f,.28f,.14f),.48f),MaterialAsset("TurquoiseCore",new Color(.06f,.58f,.49f),.05f,.42f)};
            var idle=Clip("Reinforced_Idle",2.8f,true,Folder+"/Animations");var it=new[]{0f,1.4f,2.8f};Angle(idle,Torso,"x",it,new[]{-4f,-5.2f,-4f});Curve(idle,Hips,"m_LocalPosition.y",it,new[]{1.16f,1.172f,1.16f});
            var walk=Move("Reinforced_Walk",1.7f,1.19f,12f);var chase=Move("Reinforced_Chase",1.3f,2.08f,19f);
            var hit=Clip("Reinforced_Hit",.65f,false,Folder+"/Animations");Angle(hit,Torso,"x",new[]{0f,.18f,.65f},new[]{-4f,-25f,-4f});Motion(hit,.65f,-.13f,.06f,.22f);
            var die=Clip("Reinforced_Die",1.7f,false,Folder+"/Animations");Angle(die,"Rig","x",new[]{0f,.35f,1.1f,1.7f},new[]{0f,-5f,82f,90f});Curve(die,"Rig","m_LocalPosition.y",new[]{0f,.35f,1.1f,1.7f},new[]{0f,0f,.56f,.56f});AnimationUtility.SetAnimationEvents(die,new[]{new AnimationEvent{time=1.65f,functionName="Died"}});
            var a=Punch();var b=FistSlam();var third=Clip("Reinforced_Attack03_Placeholder",1.0f,false,Folder+"/Animations");
            var charge=Charge();var sweep=Sweep();var cleave=Cleave();
            var controller=Controller(idle,walk,chase,hit,die,a,b,third);
            var fistsController=Override(controller,Fists,"ReinforcedFists",a,b,third,new[]{a,b,third});var bladeController=Override(controller,Blade,"ReinforcedBlade",a,b,third,new[]{charge,sweep,cleave});
            var fists=ScriptableObject.CreateInstance<WeaponSO>();fists.name="ReinforcedFists";fists.id="enemy_reinforced_fists";fists.itemName="强化守卫·空手";fists.description="空物体武器，左直拳与双小臂砸地。";fists.stopDistance=2.1f;fists.attackBreak=1.3f;fists.overrideController=fistsController;
            fists.combo.Add(new AttackData{damagePerHit=12,radius=1.9f,angle=100,judgeOffset=new Vector3(0,1.25f,1f)});fists.combo.Add(new AttackData{damagePerHit=20,radius=2.2f,angle=140,judgeOffset=new Vector3(0,.45f,1.05f)});AssetDatabase.CreateAsset(fists,Fists+"/ReinforcedFists.asset");fists.prefab=Weapon(fists,Fists,false);EditorUtility.SetDirty(fists);
            var blade=ScriptableObject.CreateInstance<WeaponSO>();blade.name="ReinforcedBlade";blade.id="enemy_reinforced_blade";blade.itemName="强化守卫·长石刀";blade.description="冲撞、横斩、左小臂砸地。";blade.stopDistance=3f;blade.attackBreak=1.6f;blade.overrideController=bladeController;
            blade.combo.Add(new AttackData{damagePerHit=14,radius=1.9f,angle=95,judgeOffset=new Vector3(0,1.4f,.85f)});blade.combo.Add(new AttackData{damagePerHit=20,radius=3.15f,angle=180,judgeOffset=new Vector3(0,1.3f,1f)});blade.combo.Add(new AttackData{damagePerHit=30,radius=3.2f,angle=105,judgeOffset=new Vector3(-.55f,.45f,1.1f)});AssetDatabase.CreateAsset(blade,Blade+"/ReinforcedBlade.asset");blade.prefab=Weapon(blade,Blade,true);EditorUtility.SetDirty(blade);
            var so=ScriptableObject.CreateInstance<EnemySO>();so.name="ReinforcedSentinel";so.enemyID="Enemy_ReinforcedSentinel";so.targetName="强化守卫";so.description="慢速石质守卫，默认空手二连击。";so.maxHp=260;so.initialHp=260;so.atk=20;so.speed=.7f;so.chaseSpeed=1.6f;so.turnSpeed=180;so.chaseDistance=18;so.hitStun=.65f;so.defaultWeaponSO=fists;AssetDatabase.CreateAsset(so,Folder+"/ReinforcedSentinel.asset");
            var root=PrefabUtility.LoadPrefabContents("Assets/Prefabs/TestEnemy/TestEnemy.prefab");
            try
            {
                root.name="ReinforcedSentinel";root.layer=0;root.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);root.transform.localScale=Vector3.one;
                var old=root.transform.Find("foot");if(old)Object.DestroyImmediate(old.gameObject);
                var rig=BuildBody(root.transform);rig.name="ReinforcedSentinelModel";PrefabUtility.SaveAsPrefabAsset(rig.gameObject,Folder+"/Models/ReinforcedSentinelModel.prefab");rig.name="Rig";
                var hand=rig.Find("Pelvis/Torso/RArm/RForearm/RHand");var socket=Joint(hand,"WeaponSocket",new Vector3(0,-.035f,.075f));socket.localRotation=Quaternion.Euler(126,0,-15);
                GroundAndClearanceMossstoneV2.Process(root,clips,blade.prefab);
                var obj=root.GetComponent<EnemyObj>();var ai=root.GetComponent<EnemyAI>();var anim=root.GetComponent<Animator>();var hp=root.GetComponent<EnemyHPBar>();var agent=root.GetComponent<NavMeshAgent>();
                obj.SO=so;obj.handPos=socket;obj.animator=anim;obj.enemyAI=ai;obj.HPBar=hp;obj.isDead=false;obj.enemyName.text="强化守卫";
                ai.animator=anim;ai.enemyObj=obj;ai.self=agent;ai.triggerCollider=root.GetComponent<BoxCollider>();ai.Player=null;ai.maxComboIndex=-1;ai.comboIndex=0;ai.isAttacking=false;
                agent.height=3.05f;agent.radius=.75f;agent.baseOffset=0;agent.speed=1.6f;agent.angularSpeed=180;agent.stoppingDistance=2.1f;
                ai.triggerCollider.center=new Vector3(0,1.5f,0);ai.triggerCollider.size=Vector3.one;ai.triggerCollider.isTrigger=true;
                var collision=Joint(root.transform,"BodyCollider",Vector3.zero).gameObject.AddComponent<CapsuleCollider>();collision.gameObject.layer=LayerMask.NameToLayer("enemy");collision.height=3.02f;collision.radius=.7f;collision.center=new Vector3(0,1.5f,0);
                root.GetComponent<Rigidbody>().isKinematic=true;root.GetComponent<Rigidbody>().useGravity=false;
                hp.enemy=obj;hp.worldCanvas.localPosition=new Vector3(0,3.46f,0);hp.worldCanvas.localRotation=Quaternion.identity;
                anim.runtimeAnimatorController=controller;anim.applyRootMotion=true;anim.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                // A Generic avatar anchors extraction to the animator's own transform.
                var avatar=AvatarBuilder.BuildGenericAvatar(root,"");avatar.name="ReinforcedSentinelAvatar";AssetDatabase.CreateAsset(avatar,Folder+"/Models/ReinforcedSentinelAvatar.asset");anim.avatar=avatar;
                so.prefab=PrefabUtility.SaveAsPrefabAsset(root,Folder+"/ReinforcedSentinel.prefab");EditorUtility.SetDirty(so);
            }
            finally{PrefabUtility.UnloadPrefabContents(root);}
            var alternate=Object.Instantiate(so);alternate.name="ReinforcedSentinel_Blade";alternate.enemyID="Enemy_ReinforcedSentinel_Blade";alternate.defaultWeaponSO=blade;alternate.description="长石刀资源配置：冲撞、横斩、左小臂砸地；共用身体预制体。";AssetDatabase.CreateAsset(alternate,Folder+"/ReinforcedSentinel_Blade.asset");
            AssetDatabase.SaveAssets();AssetDatabase.Refresh();
            Validate(so,alternate);
            File.WriteAllText(report,"PASS\nBody prefab: "+Folder+"/ReinforcedSentinel.prefab\nBody height: ~3.0m\nWalk: 0.7m/s; chase: 1.6m/s\nFists: 1.8s punch / hit .70, 2.4s slam / hit 1.02; break 1.3s\nBlade: 2.15s charge / hit 1.03, 2.55s sweep / hit 1.15, 3.2s ground slap / hit 1.51; break 1.6s\nAnimations: "+clips.Count+"\nMesh renderers: "+so.prefab.GetComponentsInChildren<MeshRenderer>().Length+"\nTriangles: "+so.prefab.GetComponentsInChildren<MeshFilter>().Sum(m=>m.sharedMesh.triangles.Length/3)+"\nNo scene changes. Default fists have no mesh. Both loadouts share the body prefab. Native serialization, component references, curves and animation events checked.");
            Debug.Log("[ReinforcedSentinel] Resource build and static validation passed.");
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
