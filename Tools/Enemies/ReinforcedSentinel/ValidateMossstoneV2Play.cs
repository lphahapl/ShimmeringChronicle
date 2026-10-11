using System; using System.IO; using System.Linq; using System.Collections.Generic; using UnityEditor; using UnityEngine; using UnityEngine.AI;
[InitializeOnLoad]
public static class ValidateMossstoneV2Play
{
 static EnemyObj enemy;static EnemySO fists,blade;static GameObject target;static List<int> combo=new List<int>();static int stage,errors;static float started,lockedUntil;static Vector3 origin;static float attackOrigin;static bool chargeVerified;
 static ValidateMossstoneV2Play(){EditorApplication.update+=Tick;}
 public static void Run(){File.WriteAllText("v2-play-errors.txt","");SessionState.SetBool("MossstoneV2Play",true);EditorApplication.EnterPlaymode();}
 static void Require(bool ok,string text){if(!ok)throw new Exception(text);}
 static void Stage(int value){stage=value;started=Time.time;}
 static float Elapsed=>Time.time-started;
 static void Log(string text,string stack,LogType type){if((type==LogType.Error||type==LogType.Exception)&&!stack.Contains("UnityEditor.Search")&&!stack.Contains("UnityEditor.Connect")){errors++;File.AppendAllText("v2-play-errors.txt",text+"\n"+stack+"\n");}}
 static void OnCombo(EnemyObj who,int index)
 {if(who!=enemy)return;combo.Add(index);bool bare=enemy.data.WeaponSO==fists.defaultWeaponSO;float seconds=bare?(index==0?.85f:1.12f):(index==0?.88f:index==1?1.1f:1.4f);lockedUntil=Time.time+seconds;File.AppendAllText("v2-play-timing.txt",(bare?"Fists":"Blade")+" event "+index+" at "+Time.time+"s\n");}
 static void Tick()
 {
  if(!SessionState.GetBool("MossstoneV2Play",false)||!EditorApplication.isPlaying)return;
  try
  {
   if(stage==0)
   {
    Application.logMessageReceived+=Log;File.WriteAllText("v2-play-timing.txt","");
    var sources=new List<NavMeshBuildSource>{new NavMeshBuildSource{shape=NavMeshBuildSourceShape.Box,transform=Matrix4x4.TRS(new Vector3(0,-.5f,0),Quaternion.identity,Vector3.one),size=new Vector3(50,1,50),area=0}};
    var nav=NavMeshBuilder.BuildNavMeshData(NavMesh.GetSettingsByID(0),sources,new Bounds(Vector3.zero,new Vector3(50,10,50)),Vector3.zero,Quaternion.identity);Require(nav!=null,"NavMesh creation");NavMesh.AddNavMeshData(nav);
    new GameObject("EnemyManager").AddComponent<EnemyManager>();fists=AssetDatabase.LoadAssetAtPath<EnemySO>(BuildMossstoneSentinelV2.Folder+"/ReinforcedSentinel.asset");blade=AssetDatabase.LoadAssetAtPath<EnemySO>(BuildMossstoneSentinelV2.Folder+"/ReinforcedSentinel_Blade.asset");
    enemy=UnityEngine.Object.Instantiate(fists.prefab,Vector3.zero,Quaternion.identity).GetComponent<EnemyObj>();EnemyManager.Instance.Register(enemy);target=new GameObject("PursuitTarget");target.transform.position=new Vector3(0,0,14);enemy.enemyAI.Player=target;
    new object().Subscribe<EnemyObj,int>(GameEvents.OnEnemyCombo,OnCombo);Stage(10);return;
   }
   if(stage==10&&Elapsed>1.6f){origin=enemy.transform.position;Stage(1);return;}
   if(stage==1&&Elapsed>2.1f)
   {
    Require(Mathf.Abs(enemy.transform.position.z-origin.z-Elapsed*1.6f)<.25f,"Chase root-motion speed "+(enemy.transform.position.z-origin.z)/Elapsed);Require(Mathf.Abs(enemy.transform.position.y-origin.y)<.015f,"Vertical root drift: "+(enemy.transform.position.y-origin.y)+" baseline="+origin.y);
    enemy.enemyAI.Player=target;target.transform.position=enemy.transform.position+Vector3.forward*1.4f;combo.Clear();attackOrigin=enemy.transform.position.z;Stage(2);return;
   }
   if(stage==2||stage==3)
   {
    target.transform.position=enemy.transform.position+Vector3.forward*1.4f;
    if(stage==3&&!chargeVerified)
    {
     var state=enemy.animator.GetCurrentAnimatorStateInfo(0);
     if(state.IsName("Base Layer.Attack1")&&state.normalizedTime>.94f)
     {float delta=enemy.transform.position.z-attackOrigin;Require(delta>.64f&&delta<.84f,"First blade charge displacement "+delta);chargeVerified=true;File.AppendAllText("v2-play-timing.txt","First blade charge actual root displacement="+delta+"m\n");}
    }
    if(Time.time<lockedUntil)Require(enemy.enemyAI.isAttacking,"Recovery unlocked too soon");
    foreach(var name in new[]{"L","R"})
    {var mesh=enemy.transform.Find("Rig/Pelvis/"+name+"Leg/"+name+"Shin/"+name+"Foot/Mesh").GetComponent<MeshFilter>();Require(mesh.sharedMesh.vertices.Min(v=>mesh.transform.TransformPoint(v).y)>-.018f,"Sole under ground");}
   }
   if(stage==2&&Elapsed>4.8f)
   {
    Require(combo.Take(2).SequenceEqual(new[]{0,1}),"Fists combo "+string.Join(",",combo));float fistDelta=enemy.transform.position.z-attackOrigin;Require(fistDelta>.08f&&fistDelta<.20f,"Fists root motion "+fistDelta);File.AppendAllText("v2-play-timing.txt","Fists complete combo displacement="+fistDelta+"m\n");
    enemy.gameObject.SetActive(false);enemy.ResetEnemyStatu(blade);enemy.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);enemy.gameObject.SetActive(true);enemy.enemyAI.Player=target;combo.Clear();lockedUntil=0;attackOrigin=0;Stage(3);return;
   }
   if(stage==3&&Elapsed>10.2f)
   {
    Require(combo.Take(3).SequenceEqual(new[]{0,1,2}),"Blade combo "+string.Join(",",combo));Require(chargeVerified,"First charge displacement not verified");Require(enemy.transform.position.z>.64f&&enemy.transform.position.z<.84f,"Blade root motion "+enemy.transform.position.z);File.AppendAllText("v2-play-timing.txt","Blade complete combo displacement="+enemy.transform.position.z+"m\n");Require(enemy.data.weaponData==enemy.data.HandingItem,"Item data binding");Require(enemy.data.weaponData.attackBreak>0,"Full combo break missing");
    Require(enemy.GetComponentsInChildren<WeaponObj>().Length==1&&enemy.GetComponentInChildren<WeaponObj>().SO==blade.defaultWeaponSO,"Weapon replacement");
    enemy.enemyAI.Player=null;enemy.data.hp=0;enemy.isDead=true;new object().Publish<GameObject>(GameEvents.Died,enemy.gameObject);Stage(4);return;
   }
   if(stage==4&&Elapsed>2.1f)
   {
    Require(!enemy.gameObject.activeSelf&&!EnemyManager.Instance.AllEnemies.Contains(enemy),"Death recycle");enemy.ResetEnemyStatu(fists);enemy.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);enemy.gameObject.SetActive(true);Stage(5);return;
   }
   if(stage==5&&Elapsed>.35f)
   {
    Require(enemy.data.hp==260&&!enemy.isDead,"Respawn health");Require(enemy.enemyAI.maxComboIndex==1&&enemy.GetComponentsInChildren<WeaponObj>().Length==1&&enemy.GetComponentInChildren<WeaponObj>().SO==fists.defaultWeaponSO,"Respawn loadout");Require(enemy.animator.GetCurrentAnimatorStateInfo(0).IsName("Base Layer.Idle"),"Respawn state");Require(errors==0,"Runtime errors "+errors);
    File.WriteAllText("v2-play-result.txt","PASS: actual project gameplay logic in native Unity Play Mode; 1.6m/s root-motion chase; feet above ground; fists events 0,1 and blade events 0,1,2; post-hit recovery remains locked (0.85/1.12s fists; 0.88/1.1/1.4s blade checked); full-combo cooldown; horizontal attack root motion; weapon replacement/data linkage; death recycle; respawn to fists, full HP and Idle; zero gameplay errors. Isolated source copies omit four unused STP/ShaderGraph namespace imports; main scripts unchanged.");SessionState.SetBool("MossstoneV2Play",false);EditorApplication.Exit(0);
   }
  }
  catch(Exception e){File.WriteAllText("v2-play-result.txt","FAIL: "+e);Debug.LogException(e);SessionState.SetBool("MossstoneV2Play",false);EditorApplication.Exit(1);}
 }
}
