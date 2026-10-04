using System;
using UnityEditor.ShaderGraph.Internal;
using UnityEngine;
using static UnityEngine.Rendering.STP;

[CreateAssetMenu(fileName = "EnemyConfig", menuName = "Configs/Enemy/EnemySO")]
public class EnemySO : BaseDataSO<EnemyData>
{
    public string enemyID;
    public EnemyType type;
    public float chaseSpeed;
    public float turnSpeed = 720f;
    public float chaseDistance;
    /// <summary>
    /// ÊÜ»÷Ó²Ö±
    /// </summary>
    public float hitStun=1f;
    public WeaponSO defaultWeaponSO;
    public GameObject prefab;
    protected override void FillExtra(EnemyData data )
    {
        data.enemyID = enemyID;
        data.type = type;
        data.chaseSpeed=chaseSpeed;
        data.turnSpeed=turnSpeed;
        data.prefab = prefab;
        data.chaseDistance=chaseDistance;
        data.WeaponSO = defaultWeaponSO;
        data.hitStun=hitStun;
       
    }
}
public enum EnemyType
{
    common,
    elite,
    BOSS
}
