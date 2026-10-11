using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Object=UnityEngine.Object;
public static class ValidateWeaponProduction
{
    static readonly List<string> report=new List<string>();
    const BindingFlags Private=BindingFlags.Instance|BindingFlags.NonPublic;
    static void Check(bool condition,string message){if(!condition)throw new Exception(message);report.Add("PASS "+message);}
    static void Set(object target,string field,object value)=>target.GetType().GetField(field,Private).SetValue(target,value);
    static PlayerObj Player(string name)
    {
        var root=new GameObject(name);var player=root.AddComponent<PlayerObj>();var config=ScriptableObject.CreateInstance<PlayerSO>();config.initialHp=config.maxHp=100;config.poise=100;config.Bag=new List<ItemSlotData>();config.quickBar=new List<ItemSlotData>();Set(player,"config",config);
        player.handPos=new GameObject("Hand").transform;player.handPos.SetParent(root.transform,false);var ignored=player.Data;return player;
    }
    static void Owners()
    {
        var so=AssetDatabase.LoadAssetAtPath<WeaponSO>(WeaponAssetProduction.Root+"weapon_004/weapon_004.asset");var a=Player("FirstOwner");var b=Player("SecondOwner");
        var da=so.CreateData();var db=so.CreateData();a.ChangeItem(da);b.ChangeItem(db);
        var ea=a.HandingItem.GetComponentInChildren<WeaponObj>(true).effPos;var eb=b.HandingItem.GetComponentInChildren<WeaponObj>(true).effPos;
        Check(ea&&eb&&ea!=eb&&ea.IsChildOf(a.HandingItem.transform)&&!EditorUtility.IsPersistent(ea),"two players read separate prefab instance effect positions");
        var trail=ea.GetComponent<TrailRenderer>();trail.AddPosition(Vector3.one*20);a.PlayHandingEffORAudioClip();Check(ea.gameObject.activeSelf&&!eb.gameObject.activeSelf&&trail.positionCount==0,"opening one effect clears old trail positions and leaves the other owner off");
        a.EndEff();Check(!ea.gameObject.activeSelf,"player animation end hides its effect");
        var control=a.gameObject.AddComponent<PlayerController>();Set(control,"player",a);a.PlayHandingEffORAudioClip();typeof(PlayerController).GetMethod("ResetCombo",Private).Invoke(control,null);Check(!ea.gameObject.activeSelf,"interrupting the player combo hides an active effect");
        var c=Player("NoEffectPositionOwner");c.ChangeItem(so.CreateData());c.HandingItem.GetComponentInChildren<WeaponObj>(true).effPos=null;c.PlayHandingEffORAudioClip();Check(!c.HandingItem.transform.Find("TipPoint/AttackEffect").gameObject.activeSelf,"an unassigned WeaponObj.effPos does not auto-find or enable a child effect");
        foreach(var player in new[]{a,b,c}){Object.DestroyImmediate(player.HandingItem);player.HandingItem=null;Object.DestroyImmediate(player.gameObject);}
        var enemies=new List<EnemyObj>();
        for(int n=0;n<2;n++)
        {
            var root=new GameObject("EnemyOwner"+n);var enemy=root.AddComponent<EnemyObj>();enemy.data=new EnemyData();enemy.SO=ScriptableObject.CreateInstance<EnemySO>();enemy.SO.defaultWeaponSO=so;enemy.animator=root.AddComponent<Animator>();enemy.handPos=new GameObject("Hand").transform;enemy.handPos.SetParent(root.transform,false);typeof(EnemyObj).GetMethod("SpawnDefaultWeapon",Private).Invoke(enemy,null);enemies.Add(enemy);
        }
        var enemyEffects=enemies.Select(e=>((GameObject)typeof(EnemyObj).GetField("handingInstance",Private).GetValue(e)).GetComponentInChildren<WeaponObj>(true).effPos).ToArray();
        enemies[0].PlayHandingEffORAudioClip();Check(enemyEffects[0]!=enemyEffects[1]&&enemyEffects[0].gameObject.activeSelf&&!enemyEffects[1].gameObject.activeSelf,"enemy animation event uses its held prefab component, separate from other owners");
        typeof(EnemyObj).GetMethod("OnDisable",Private).Invoke(enemies[0],null);Check(!enemyEffects[0].gameObject.activeSelf,"enemy recycling closes its effect");foreach(var enemy in enemies)Object.DestroyImmediate(enemy.gameObject);
    }
    static void Assets()
    {
        foreach(string id in WeaponAssetProduction.Names)
        {
            string path=WeaponAssetProduction.Root+id+"/";var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path+id+".prefab");Check(prefab!=null,id+" prefab imports");
            var tip=prefab.transform.Find("TipPoint/AttackEffect");Check(tip&&!tip.gameObject.activeSelf,id+" has a default-off embedded tip effect");var audio=tip.GetComponent<AudioSource>();Check(audio&&audio.clip&&audio.playOnAwake&&!audio.loop&&audio.spatialBlend==1,id+" has one-shot spatial audio");Check(tip.GetComponent<TrailRenderer>().time<=.22f,id+" has a short trail lifetime");
            var weapon=prefab.GetComponentInChildren<WeaponObj>(true);Check(weapon&&weapon.effPos==tip,id+" WeaponObj.effPos references its embedded effect transform");
            var so=AssetDatabase.LoadAssetAtPath<WeaponSO>(path+id+".asset");if(so){Check(so.prefab,id+" config points to its weapon prefab");var data=so.CreateData();Check(data.combo.Count==so.combo.Count&&!ReferenceEquals(data.combo[0],so.combo[0]),id+" creates independent attack data");}
            foreach(var renderer in prefab.GetComponentsInChildren<MeshRenderer>())foreach(var material in renderer.sharedMaterials)Check(material!=null,id+" mesh material reference resolves");
        }
        foreach(string id in new[]{"MossstoneFists","ReinforcedFists"})
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(WeaponAssetProduction.Root+id+"/"+id+".prefab");var weapon=prefab.GetComponentInChildren<WeaponObj>(true);var audio=weapon.GetComponent<AudioSource>();Check(!weapon.effPos&&audio&&audio.clip&&!audio.playOnAwake&&!audio.loop&&audio.spatialBlend==1,id+" uses its prefab AudioSource without an effect");
        }
    }
    static void Motion()
    {
        foreach(string id in WeaponAssetProduction.Names.Take(3))
        {
            var so=AssetDatabase.LoadAssetAtPath<WeaponSO>(WeaponAssetProduction.Root+id+"/"+id+".asset");Check(so.overrideController!=null,id+" has a Warrior controller");
            var actor=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player/Warrior/Character Warrior.prefab"));actor.transform.position=Vector3.zero;actor.transform.rotation=Quaternion.identity;
            var socket=actor.GetComponent<PlayerObj>().handPos;var weapon=Object.Instantiate(so.prefab,socket,false);var tip=weapon.transform.Find("TipPoint");var animator=actor.GetComponent<Animator>();animator.enabled=false;
            foreach(string name in new[]{"Idle","Walk","Run","Attack1","Attack2","Attack3"})
            {
                var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(WeaponAssetProduction.Root+id+"/Animations/Warrior/"+name+".anim");Check(clip!=null,id+" "+name+" exists");
                if(name.StartsWith("Attack")){var events=AnimationUtility.GetAnimationEvents(clip);int index=int.Parse(name.Substring(6))-1;var hit=events.Single(e=>e.functionName=="PublishPlayerCombo");Check(hit.intParameter==index&&events.First(e=>e.functionName=="PlayHandingEffORAudioClip").time<hit.time&&events.First(e=>e.functionName=="EndEff").time>hit.time,id+" "+name+" effect brackets the indexed hit");}
                float minTip=100,maxGap=0;
                for(int frame=0;frame<=60;frame++)
                {
                    clip.SampleAnimation(actor,clip.length*frame/60);minTip=Mathf.Min(minTip,tip.position.y);
                    if(id=="weapon_004")
                    {
                        var wrist=actor.GetComponentsInChildren<Transform>().Single(t=>t.name=="CATRigLArmPalm");var leftGrip=wrist.position+wrist.rotation*Vector3.Scale(socket.localPosition,wrist.lossyScale);var wanted=socket.position-(-weapon.transform.forward)*.095f;maxGap=Mathf.Max(maxGap,Vector3.Distance(leftGrip,wanted));
                    }
                }
                Check(minTip>-.03f,id+" "+name+" blade stays above the floor (minimum "+minTip.ToString("F3")+" m)");
                if(id=="weapon_004")Check(maxGap<.035f,id+" "+name+" left hand stays at the lower grip (maximum gap "+maxGap.ToString("F3")+" m)");
            }
            // Evaluate the actual layered controller rather than only sampling a raw clip.
            animator.enabled=true;animator.runtimeAnimatorController=so.overrideController;animator.Rebind();animator.Play("Base Layer.Attack_01",0,.35f);animator.Update(0);
            Check(animator.GetCurrentAnimatorClipInfo(0).Any(c=>c.clip.name=="Attack1"),id+" actual Attack_01 state uses the new first attack");
            var baseController=so.overrideController.runtimeAnimatorController as UnityEditor.Animations.AnimatorController;var mask=baseController.layers[1].avatarMask;
            Check(Enumerable.Range(0,mask.transformCount).Where(n=>mask.GetTransformPath(n).EndsWith("CATRigRArmPalm")).All(n=>!mask.GetTransformActive(n)),id+" holding layer leaves the attacking wrist free");Object.DestroyImmediate(actor);
        }
        foreach(string id in new[]{"EnemyWeapon_001","MossstoneBlade","ReinforcedBlade","MossstoneFists","ReinforcedFists"})foreach(string guid in AssetDatabase.FindAssets("t:AnimationClip",new[]{WeaponAssetProduction.Root+id+"/Animations"}))
        {
            var clip=AssetDatabase.LoadAssetAtPath<AnimationClip>(AssetDatabase.GUIDToAssetPath(guid));var events=AnimationUtility.GetAnimationEvents(clip);
            if(!events.Any(e=>e.functionName=="PublishEnemyCombo"))continue;
            Check(events.Any(e=>e.functionName=="PlayHandingEffORAudioClip"),clip.name+" has an effect or audio event");
            Check(events.All(e=>e.time>=0&&e.time<=clip.length+.005f),clip.name+" events are inside the clip");
        }
        var cube=AssetDatabase.LoadAssetAtPath<AnimationClip>(WeaponAssetProduction.Root+"EnemyWeapon_001/Animations/EnemyWeapon_001_Attack01.anim");Check(AnimationUtility.GetCurveBindings(cube).All(b=>b.path!=""),"cube enemy attack animates its visual body and hand without moving the navigation root");
    }
    public static bool CheckAll(){report.Clear();bool passed=true;try{Assets();Owners();Motion();report.Add("ALL PASSED");}catch(Exception e){report.Add("FAIL "+e);passed=false;}File.WriteAllLines("production-validation.txt",report);Debug.Log(string.Join("\n",report));return passed;}
    public static void Run(){EditorApplication.Exit(CheckAll()?0:1);}
}
