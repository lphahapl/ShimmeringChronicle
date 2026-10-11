using UnityEngine;


public class EnemyData:BaseData
{
    public string enemyID;
    public EnemyType type;
    public float chaseSpeed;
    public float turnSpeed;
    [Tooltip("角色韧性")]
    public float poise;
    [Tooltip("角色每秒恢复韧性")]
    public float poiseRecoveryPerSecond;
    [Tooltip("角色韧性上次归零后，直接恢复满韧性时间")]
    public float poiseRecoveryTime;
    public float hitStun = 1f;
    public float chaseDistance;
    public WeaponSO WeaponSO;
    public ItemData HandingItem;
    public WeaponData weaponData;
    public GameObject prefab;
}
