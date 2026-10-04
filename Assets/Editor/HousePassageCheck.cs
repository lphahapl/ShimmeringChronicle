using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class HousePassageCheck
{
    static HousePassageCheck() { EditorApplication.delayCall += Once; }
    static void Once() { if (!File.Exists("Tools/Environment/house-before.txt") && !EditorApplication.isPlayingOrWillChangePlaymode) Run("Tools/Environment/house-before.txt"); }
    [MenuItem("Tools/Terrain/Check House Passages")]
    public static void Check() => Run("Tools/Environment/house-check.txt");
    public static bool Walk(string path)
    {
        var root=GameObject.Find("Test Environment - Village and Plants/Buildings");
        var player=GameObject.FindGameObjectWithTag("Player");
        var original=player.GetComponent<CharacterController>();
        var terrain=Object.FindFirstObjectByType<Terrain>();
        var report=new StringBuilder();bool pass=true;
        foreach(Transform house in root.transform)
        {
            if(!house.name.StartsWith("Test House"))continue;
            var probe=new GameObject("Temporary house passage probe");
            try {
                var controller=probe.AddComponent<CharacterController>();
                controller.height=original.height*Mathf.Abs(original.transform.lossyScale.y);
                controller.radius=original.radius*Mathf.Max(Mathf.Abs(original.transform.lossyScale.x),Mathf.Abs(original.transform.lossyScale.z));
                controller.center=Vector3.up*controller.height*.5f;controller.stepOffset=original.stepOffset;controller.slopeLimit=original.slopeLimit;controller.skinWidth=original.skinWidth;controller.minMoveDistance=0;
                var start=house.TransformPoint(new Vector3(0,0,-7.8f));start.y=terrain.SampleHeight(start)+terrain.transform.position.y+.1f;probe.transform.position=start;Physics.SyncTransforms();
                for(int i=0;i<240;i++)controller.Move(house.forward*.04f+Vector3.down*.08f);
                var inside=house.InverseTransformPoint(probe.transform.position);bool enter=inside.z>1 && Mathf.Abs(inside.x)<.15f;
                for(int i=0;i<250;i++)controller.Move(-house.forward*.04f+Vector3.down*.08f);
                var outside=house.InverseTransformPoint(probe.transform.position);bool leave=outside.z < -7;
                report.AppendLine(house.name+": enter="+enter+", exit="+leave+", inside="+inside+", outside="+outside);pass &=enter&&leave;
            }finally{Object.DestroyImmediate(probe);}
        }
        File.WriteAllText(path,(pass?"PASS\n":"FAIL\n")+report);return pass;
    }
    public static bool Run(string path)
    {
        var root=GameObject.Find("Test Environment - Village and Plants/Buildings");
        var player=GameObject.FindGameObjectWithTag("Player");
        var cc=player ? player.GetComponent<CharacterController>() : null;
        if (!root || !cc) return false;
        Physics.SyncTransforms();
        float height=cc.height*Mathf.Abs(cc.transform.lossyScale.y);
        float radius=cc.radius*Mathf.Max(Mathf.Abs(cc.transform.lossyScale.x),Mathf.Abs(cc.transform.lossyScale.z));
        var report=new StringBuilder("Player capsule height="+height+", radius="+radius+"\n");
        bool pass=true;
        foreach(Transform house in root.transform)
        {
            if(!house.name.StartsWith("Test House"))continue;
            bool clear=true;
            for(float z=-3.5f;z<=1;z+=.1f)
            {
                var foot=house.TransformPoint(new Vector3(0,.04f,z));
                foreach(var hit in Physics.OverlapCapsule(foot+Vector3.up*radius,foot+Vector3.up*(height-radius),radius,~0,QueryTriggerInteraction.Ignore))
                {
                    if(hit.transform.IsChildOf(player.transform))continue;
                    if(hit.transform.IsChildOf(house)) { report.AppendLine(house.name+": BLOCKED by "+hit.name+" at z="+z); clear=false; break; }
                }
                if(!clear)break;
            }
            report.AppendLine(house.name+": "+(clear?"PASS":"FAIL")); pass &=clear;
        }
        Directory.CreateDirectory("Tools/Environment");File.WriteAllText(path,report.ToString());return pass;
    }
}
