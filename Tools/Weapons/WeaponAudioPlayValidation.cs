using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;
[InitializeOnLoad]
public static class WeaponAudioPlayValidation
{
 const string Pending="WeaponProductionAudioValidation";
 static WeaponAudioPlayValidation(){EditorApplication.playModeStateChanged+=Changed;EditorApplication.update+=WaitForPlay;}
 static void WaitForPlay(){if(EditorApplication.isPlaying&&SessionState.GetBool(Pending,false)){SessionState.SetBool(Pending,false);Check();}}
 public static void Run(){SessionState.SetBool(Pending,true);EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);EditorApplication.EnterPlaymode();}
 static void Changed(PlayModeStateChange change)
 {
  if(change!=PlayModeStateChange.EnteredPlayMode||!SessionState.GetBool(Pending,false))return;
  WaitForPlay();
 }
 static void Check()
 {
  var report=new List<string>();int exit=0;
  try
  {
   new GameObject("AudioListener").AddComponent<AudioListener>();
   foreach(string id in WeaponAssetProduction.Names)
   {
    var effect=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(WeaponAssetProduction.Root+id+"/Effects/AttackEffect.prefab"));var source=effect.GetComponent<AudioSource>();
    for(int replay=0;replay<2;replay++)
    {
     effect.SetActive(true);if(!source.isPlaying)throw new Exception(id+" playOnAwake did not start on enable "+replay);
     effect.SetActive(false);if(source.isPlaying)throw new Exception(id+" audio did not stop on hide "+replay);
    }
    Object.Destroy(effect);report.Add("PASS "+id+" audio starts on enable, stops on hide, and starts again on reuse");
   }
   foreach(string id in new[]{"MossstoneFists","ReinforcedFists"})
   {
    var held=Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(WeaponAssetProduction.Root+id+"/"+id+".prefab"));var weapon=held.GetComponent<WeaponObj>();var source=weapon.GetComponent<AudioSource>();
    if(weapon.effPos||!source||!source.clip||source.playOnAwake||source.isPlaying)throw new Exception(id+" prefab audio configuration invalid");
    var owner=new GameObject("InactiveAudioEventOwner");owner.SetActive(false);
    var player=owner.AddComponent<PlayerObj>();player.HandingItem=held;player.PlayHandingEffORAudioClip();if(!source.isPlaying)throw new Exception(id+" player event did not play prefab AudioSource");source.Stop();
    var enemy=owner.AddComponent<EnemyObj>();typeof(EnemyObj).GetField("handingInstance",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(enemy,held);enemy.PlayHandingEffORAudioClip();if(!source.isPlaying)throw new Exception(id+" enemy event did not play prefab AudioSource");source.Stop();
    Object.Destroy(held);Object.Destroy(owner);report.Add("PASS "+id+" player/enemy events play the configured prefab AudioSource without SO audio fields");
   }
   report.Add("ALL PASSED: actual Play Mode AudioSource lifecycle.");
  }
  catch(Exception e){report.Add("FAIL "+e);exit=1;}
  File.WriteAllLines("audio-play-validation.txt",report);Debug.Log(string.Join("\n",report));SessionState.SetBool(Pending,false);EditorApplication.Exit(exit);
 }
}
