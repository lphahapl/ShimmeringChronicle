using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;

public static class AuditKeqingMotion
{
    public static void Run()
    {
        EditorApplication.Exit(Check()?0:1);
    }
    public static bool Check()
    {
        var actor=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(BuildKeqingRootMotion.Folder+"/KeqingRootMotionPlayer.prefab"));
        actor.GetComponent<Animator>().enabled=false;
        var bones=actor.GetComponentsInChildren<Transform>();
        var wrists=new[]{bones.Single(t=>t.name.EndsWith("joint_LeftWrist")),bones.Single(t=>t.name.EndsWith("joint_RightWrist"))};
        var palm=new[]{new Vector3(-.707f,-.707f,0),new Vector3(.707f,-.707f,0)};
        var report=new StringBuilder();bool failed=false;
        var body=actor.GetComponentsInChildren<SkinnedMeshRenderer>().Single(r=>r.name=="U_Char_2");var mesh=new Mesh();
        foreach(var guid in AssetDatabase.FindAssets("t:AnimationClip",new[]{BuildKeqingRootMotion.Folder+"/Animations"}))
        {
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(guid));
            float[] bend={0,0},speed={0,0};Quaternion[] last=new Quaternion[2];
            float minGround=0,turnSlip=0,minBlade=100;Vector3[] planted=new Vector3[2];
            var blade=bones.Single(t=>t.name=="Blade");
            var feet=new[]{bones.Single(t=>t.name.EndsWith("!joint_LeftFoot")),bones.Single(t=>t.name.EndsWith("!joint_RightFoot"))};
            for(int n=0;n<=Mathf.CeilToInt(clip.length*120);n++)
            {
                clip.SampleAnimation(actor,Mathf.Min(clip.length,n/120f));
                minBlade=Mathf.Min(minBlade,blade.TransformPoint(new Vector3(0,.84f,0)).y);
                if(n%2==0)
                {
                    body.BakeMesh(mesh);var matrix=body.transform.localToWorldMatrix;
                    minGround=Mathf.Min(minGround,mesh.vertices.Min(v=>matrix.MultiplyPoint3x4(v).y));
                }
                if(clip.name.StartsWith("Turn"))
                {
                    float u=Mathf.Min(1,n/120f/clip.length);
                    for(int k=0;k<2;k++)
                    {
                        bool support=k==0?u>.50f:u<.50f;
                        if((k==1&&n==0)||(k==0&&u>.50f&&u-1f/120f/clip.length<=.50f))planted[k]=feet[k].position;
                        if(support){var d=feet[k].position-planted[k];d.y=0;turnSlip=Mathf.Max(turnSlip,d.magnitude);}
                    }
                }
                for(int k=0;k<2;k++)
                {
                    var q=wrists[k].localRotation;
                    bend[k]=Mathf.Max(bend[k],Vector3.Angle(palm[k],q*palm[k]));
                    if(n>0)speed[k]=Mathf.Max(speed[k],Quaternion.Angle(last[k],q)*120);
                    last[k]=q;
                }
            }
            bool bad=bend.Max()>45||speed.Max()>600||minGround<-.006f||turnSlip>.01f||minBlade<-.01f;
            failed|=bad;
            report.AppendLine($"{(bad?"FAIL":"PASS")} {clip.name}: wrist bend L={bend[0]:F1} R={bend[1]:F1} degrees; wrist local speed L={speed[0]:F0} R={speed[1]:F0} deg/s; mesh floor minimum={minGround:F4}m; turn support slip={turnSlip:F4}m; sword tip minimum={minBlade:F3}m");
        }
        File.WriteAllText("keqing-naturalness-audit.txt",report.ToString());
        Object.DestroyImmediate(mesh);Object.DestroyImmediate(actor);return !failed;
    }
}
