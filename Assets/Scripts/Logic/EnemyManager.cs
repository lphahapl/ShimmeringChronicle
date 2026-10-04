using System;
using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;
using static Unity.Burst.Intrinsics.X86.Avx;
using static UnityEngine.Rendering.STP;

public class EnemyManager : MonoBehaviour
{
    public static EnemyManager Instance => instance;
    private static EnemyManager instance;
    /// <summary>
    /// 所有敌人
    /// </summary>
    private readonly List<EnemyObj> allEnemies = new List<EnemyObj>();
    /// <summary>
    /// 按敌人类型存储敌人
    /// </summary>
    private readonly Dictionary<Type, List<EnemyObj>>
        enemyLists = new();
    /// <summary>
    /// 外部获取当前的所有敌人
    /// </summary>
    public IReadOnlyList<EnemyObj> AllEnemies => allEnemies;
    /// <summary>
    /// 对象池，key:预制体，value:空闲的队列
    /// </summary>
    private Dictionary<GameObject,Queue<GameObject>> _pools =new Dictionary<GameObject, Queue<GameObject>>();
    public GameObject SpawnEnemy(string enemyID,Vector3 position,Quaternion rotation)
    {
        EnemySO so = GameManager.Instance.GetEnemy(enemyID);
        if (so == null || so.prefab == null)
        {
            Debug.LogError($"敌人配置或预制体不存在：{enemyID}");
            return null;
        }
        GameObject prefab= so.prefab;
        //没有对应池子
        if (!_pools.TryGetValue(prefab, out var freeGameObjects))
        {
            freeGameObjects=new Queue<GameObject>();
            _pools[prefab] =freeGameObjects;
        }
        GameObject enemy;
        if (freeGameObjects.Count > 0)
        {
            enemy= freeGameObjects.Dequeue();
            var tmp1 = enemy. GetComponent<EnemyObj>();
            tmp1.ResetEnemyStatu(so);
            
            enemy.transform.position = position;
            enemy.transform.rotation = rotation;
            enemy.SetActive(true);

        }
        else
        {
            enemy=Instantiate(prefab,position,rotation);
            var tmp2 = enemy.GetComponent<EnemyObj>();
            tmp2.ResetEnemyStatu(so);

        }
        var tmp = enemy.GetComponent<EnemyObj>();
        Register(tmp);
        return enemy;
        
    }
    public void RecycleEnemy(GameObject enemy,GameObject prefab)
    {
        var tmp = enemy.GetComponent<EnemyObj>();
        enemy.SetActive(false);
        if(!_pools.TryGetValue(prefab, out var freeGameObjects))
        {
            freeGameObjects=new Queue<GameObject>();
            _pools[prefab] = freeGameObjects;
        }
        if (freeGameObjects.Contains(enemy)) { Unregister(tmp); return; }
        freeGameObjects.Enqueue(enemy);
        Unregister(tmp);


    }
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            instance = null;
    }

    public void Register(EnemyObj enemy)
    {
        if (enemy == null || allEnemies.Contains(enemy))
            return;

        Type type = enemy.GetType();

        if (!enemyLists.TryGetValue(type, out var list))
        {
            list = new List<EnemyObj>();
            enemyLists.Add(type, list);
        }

        list.Add(enemy);
        allEnemies.Add(enemy);
    }

    public void Unregister(EnemyObj enemy)
    {
        if (enemy == null) return;

        allEnemies.Remove(enemy);

        Type type = enemy.GetType();

        if (enemyLists.TryGetValue(type, out var list))
        {
            list.Remove(enemy);

            if (list.Count == 0)
                enemyLists.Remove(type);
        }
    }

    // 按实际类型精确查询，不包含该类型的子类。
    public IReadOnlyList<EnemyObj> GetEnemies<T>()
        where T : EnemyObj
    {
        if (enemyLists.TryGetValue(typeof(T), out var list))
            return list;

        return null;
    }
}