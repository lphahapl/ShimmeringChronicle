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
    [Tooltip("角色韧性")]
    public float poise;
    [Tooltip("角色每秒恢复韧性")]
    public float poiseRecoveryPerSecond;
    [Tooltip("角色韧性上次归零后，直接恢复满韧性时间")]
    public float poiseRecoveryTime;
    public float chaseDistance;
    /// <summary>
    /// 受击硬直
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
        data.poise=poise;
        data.poiseRecoveryTime=poiseRecoveryTime;
        data.poiseRecoveryPerSecond=poiseRecoveryPerSecond;
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
