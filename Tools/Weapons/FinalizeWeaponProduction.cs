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
   WeaponAssetProduction.BuildAll();WarriorWeaponMotion.BuildAll();EnemyWeaponProduction.BuildAll();WeaponCombatConfiguration.Apply();AssetDatabase.SaveAssets();
   if(!ValidateWeaponProduction.CheckAll())throw new Exception("Native weapon validation failed; see production-validation.txt");
   WeaponProductionPreview.Run();
  }
  catch(Exception e){Debug.LogException(e);File.WriteAllText("final-build.txt","FAIL "+e);EditorApplication.Exit(1);}
 }
}
