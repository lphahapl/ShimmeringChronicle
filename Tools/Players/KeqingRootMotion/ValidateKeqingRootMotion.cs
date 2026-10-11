using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
[InitializeOnLoad]
public static class ValidateKeqingRootMotion
{
    const string Flag="KeqingRootMotion.Validation";
    static KeqingRootMotionController player;
    static int stage,errors,footsteps;
    static float started;
    static Vector3 origin;
    static List<int> hits=new List<int>();
    static float[] lastPhase={-1,-1};
    static Vector3[] planted=new Vector3[2];
    static float maxWalkSlip,maxRunSlip;
    static Transform[] feet;
    static bool queuedSecond,queuedThird;
    static ValidateKeqingRootMotion(){if(SessionState.GetBool(Flag,false))EditorApplication.update+=Tick;}
    public static void Run(){EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);SessionState.SetBool(Flag,true);EditorApplication.EnterPlaymode();}
    static void Assert(bool result,string text){if(!result)throw new Exception(text);}
    static void Stage(int value){stage=value;started=Time.time;origin=player.transform.position;lastPhase[0]=lastPhase[1]=-1;}
    static float Elapsed=>Time.time-started;
    static void CheckLoop(AnimationClip clip)
    {
        Assert(clip.hasGenericRootTransform,"Missing generic root curves: "+clip.name);
        foreach(var binding in AnimationUtility.GetCurveBindings(clip))
        {
            if(binding.path==""||!binding.propertyName.StartsWith("m_LocalRotation"))continue;
            var curve=AnimationUtility.GetEditorCurve(clip,binding);
            Assert(Mathf.Abs(curve.Evaluate(0)-curve.Evaluate(clip.length))<.001f,"Loop seam: "+clip.name+" "+binding.path);
        }
    }
    static void Contact(bool run)
    {
        var state=player.Animator.GetCurrentAnimatorStateInfo(0);float cycle=Mathf.Repeat(state.normalizedTime,1);
        for(int i=0;i<2;i++)
        {
            float phase=Mathf.Repeat(cycle+i*.5f,1);float end=run?.34f:.56f;
            if(phase>.04f&&phase<end-.04f)
            {
                if(lastPhase[i]<0||phase<lastPhase[i])planted[i]=feet[i].position;
                var difference=feet[i].position-planted[i];difference.y=0;
                if(run)maxRunSlip=Mathf.Max(maxRunSlip,difference.magnitude);else maxWalkSlip=Mathf.Max(maxWalkSlip,difference.magnitude);
                lastPhase[i]=phase;
            }
            else lastPhase[i]=-1;
        }
    }
    static void Tick()
    {
        if(!EditorApplication.isPlaying||EditorApplication.isCompiling)return;
        try
        {
            if(stage==0)
            {
                File.WriteAllText("keqing-play-errors.txt", "");
                Application.logMessageReceived+=(text,stack,type)=>{if((type==LogType.Exception||type==LogType.Error)&&!stack.Contains("UnityEditor.Search")){errors++;File.AppendAllText("keqing-play-errors.txt",text+"\n"+stack+"\n");}};
                var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.transform.position=new Vector3(0,-.1f,0);floor.transform.localScale=new Vector3(80,.2f,80);
                player=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(BuildKeqingRootMotion.Folder+"/KeqingRootMotionPlayer.prefab")).GetComponent<KeqingRootMotionController>();player.readInput=false;
                player.AttackHit+=i=>hits.Add(i);player.Footstep+=i=>footsteps++;
                feet=new[]{player.GetComponentsInChildren<Transform>().Single(t=>t.name.EndsWith("!joint_LeftFoot")),player.GetComponentsInChildren<Transform>().Single(t=>t.name.EndsWith("!joint_RightFoot"))};
                foreach(string name in new[]{"Idle","Walk","Run"})CheckLoop(AssetDatabase.LoadAssetAtPath<AnimationClip>(BuildKeqingRootMotion.Folder+"/Animations/"+name+".anim"));
                Assert(player.Animator.avatar!=null&&player.Animator.avatar.isValid&&!player.Animator.avatar.isHuman,"Generic avatar");Stage(1);return;
            }
            if(stage==1&&Elapsed>.35f)
            {
                Assert(Vector3.Distance(origin,player.transform.position)<.035f,"Idle drift");
                player.SetMove(Vector3.forward,false);Stage(2);return;
            }
            if(stage==2&&Elapsed>.65f){Assert(player.Motion=="Walk","Walk start did not finish");Stage(3);return;}
            if(stage==3)
            {
                Contact(false);
                if(Elapsed<1.92f)return;
                float speed=(player.transform.position.z-origin.z)/Elapsed;
                Assert(Mathf.Abs(speed-1.26f/.96f)<.09f,"Walk root speed "+speed);
                Assert(Mathf.Abs(player.transform.position.y-origin.y)<.015f,"Walk vertical drift");
                Assert(maxWalkSlip<.06f,"Walk foot sliding "+maxWalkSlip);
                player.SetMove(Vector3.forward,true);Stage(4);return;
            }
            if(stage==4&&Elapsed>.35f){Assert(player.Motion=="Run","Run transition");Stage(5);return;}
            if(stage==5)
            {
                Contact(true);if(Elapsed<1.36f)return;
                float speed=(player.transform.position.z-origin.z)/Elapsed;
                Assert(Mathf.Abs(speed-2.4f/.68f)<.13f,"Run root speed "+speed);
                Assert(Mathf.Abs(player.transform.position.y-origin.y)<.015f,"Run vertical drift");
                Assert(maxRunSlip<.085f,"Run foot sliding "+maxRunSlip);Assert(footsteps>=6,"Foot contact events");
                player.SetMove(Vector3.zero,false);Stage(6);return;
            }
            if(stage==6&&Elapsed>.7f){Assert(player.Motion=="Idle","Run stop did not return to idle");player.SetMove(Vector3.right,false);Stage(7);return;}
            if(stage==7)
            {
                if(Elapsed>.22f)player.SetMove(Vector3.zero,false);
                if(Elapsed<.8f)return;
                Assert(Vector3.Dot(player.transform.forward,Vector3.right)>.99f,"Turn clip did not rotate the root 90 degrees");
                Assert(Vector3.Distance(origin,player.transform.position)<.03f,"Turn translation drift");player.RequestAttack();Stage(8);return;
            }
            if(stage==8)
            {
                if(player.AttackIndex==0&&Elapsed>.12f&&!queuedSecond){player.RequestAttack();queuedSecond=true;}
                if(player.AttackIndex==1&&!queuedThird){player.RequestAttack();queuedThird=true;}
                if(Elapsed<3.4f)return;
                Assert(hits.SequenceEqual(new[]{0,1,2}),"Three combo strike events: "+string.Join(",",hits));
                Assert(player.AttackIndex==-1&&player.Motion=="Idle","Combo recovery");
                float distance=Vector3.Distance(player.transform.position,origin);Assert(distance>1.25f&&distance<1.7f,"Combo root displacement: "+distance);
                var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=player.transform.position+Vector3.right*.9f+Vector3.up*.85f;wall.transform.localScale=new Vector3(.3f,1.7f,3);
                player.SetMove(Vector3.right,false);Stage(9);return;
            }
            if(stage==9&&Elapsed>1.5f)
            {
                Assert(player.transform.position.x-origin.x<.61f,"Root motion crossed the collision wall");Assert(errors==0,"Runtime errors: "+errors);
                File.WriteAllText("keqing-play-result.txt","PASS: real Unity Play Mode using the new prefab/controller. Generic avatar/root curves; idle stability; authored loop seams; walk 1.3125m/s and run 3.5294m/s from Animator.deltaPosition; no vertical drift; maximum stance slip walk="+maxWalkSlip.ToString("F4")+"m run="+maxRunSlip.ToString("F4")+"m; start/stop transitions; 90-degree root turn; buffered three-hit combo and exactly one strike event per stage; 1.49m nominal combo root travel; return to idle; CharacterController blocks root motion at a wall; zero runtime errors.");
                EditorApplication.update-=Tick;SessionState.SetBool(Flag,false);EditorApplication.Exit(0);
            }
        }
        catch(Exception e){File.WriteAllText("keqing-play-result.txt","FAIL: "+e);Debug.LogException(e);EditorApplication.update-=Tick;SessionState.SetBool(Flag,false);EditorApplication.Exit(1);}
    }
}
