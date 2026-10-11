using System;
using System.IO;
using UnityEditor;
using UnityEngine;
public static class FinalizeWeaponProduction
{
 public static void Run()
 {
  try
  {
   WeaponAssetProduction.BuildAll();WarriorWeaponMotion.BuildAll();EnemyWeaponProduction.BuildAll();WeaponCombatConfiguration.Apply();AttackDataBindings.BindAll();AssetDatabase.SaveAssets();
   if(!AttackDataBindings.CheckAll())throw new Exception("Native weapon validation failed; see attack-data-binding-validation.txt");
   WeaponProductionPreview.Run();
  }
  catch(Exception e){Debug.LogException(e);File.WriteAllText("final-build.txt","FAIL "+e);EditorApplication.Exit(1);}
 }
}
