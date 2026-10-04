using UnityEngine;


public class EnemyData:BaseData
{
    public string enemyID;
    public EnemyType type;
    public float chaseSpeed;
    public float turnSpeed;
    public float hitStun = 1f;
    public float chaseDistance;
    public WeaponSO WeaponSO;
    public ItemData HandingItem;
    public WeaponData weaponData;
    public GameObject prefab;
}
