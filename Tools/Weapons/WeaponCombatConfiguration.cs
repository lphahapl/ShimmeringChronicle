using UnityEditor;
using UnityEngine;
public static class WeaponCombatConfiguration
{
 public static void Apply()
 {
  string[] ids={"weapon_002","weapon_003","weapon_004","EnemyWeapon_001"};
  float[][] radii={new[]{1.1f,1.2f,1.3f},new[]{1.35f,1.45f,1.6f},new[]{1.2f,1.35f,1.5f},new[]{1.7f,1.9f}};
  float[][] angles={new[]{110f,135f,80f},new[]{130f,160f,110f},new[]{110f,120f,75f},new[]{135f,90f}};
  float[][] z={new[]{.3f,.3f,.4f},new[]{.35f,.35f,.45f},new[]{.35f,.35f,.45f},new[]{.35f,.45f}};
  for(int n=0;n<ids.Length;n++)
  {
   var so=AssetDatabase.LoadAssetAtPath<WeaponSO>(WeaponAssetProduction.Root+ids[n]+"/"+ids[n]+".asset");
   for(int hit=0;hit<so.combo.Count;hit++){so.combo[hit].radius=radii[n][hit];so.combo[hit].angle=angles[n][hit];so.combo[hit].judgeOffset=new Vector3(0,n==3&&hit==0?1.05f:1,z[n][hit]);}
   so.stopDistance=n==0?1.1f:n==1?1.3f:n==2?1.2f:1.5f;so.attackBreak=n==0?.8f:n==1?.9f:n==2?1f:1.2f;
   if(n<3)so.overrideController=AssetDatabase.LoadAssetAtPath<AnimatorOverrideController>(WeaponAssetProduction.Root+ids[n]+"/Animations/Warrior/"+ids[n]+".overrideController");EditorUtility.SetDirty(so);
  }
 }
}
