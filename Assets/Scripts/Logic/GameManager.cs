using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

/// <summary>
/// 全局管理器。目前只干一件事：把 Addressables 里所有物品配置读进来，
/// 按 id 建一张表，让背包 / 掉落 / 存档这些只拿到字符串 id 的地方能反查回 ItemSO。
///
/// 用法：
///     await GameManager.Instance.Ready;                        // 想等就等
///     var so = GameManager.Instance.GetItem("weapon_001");     // 想到点再取就判 IsReady
///
/// 前提：物品配置资产（ItemSO 及其子类，比如 WeaponSO）在 Addressables 里
/// 都打了 itemLabel 那个标签。标签怎么打见 Unity 的 Addressables Groups 窗口。
/// </summary>
public class GameManager : MonoBehaviour
{
    /// <summary>没加载出来时会是 null，调用方自己判空</summary>
    public static GameManager Instance { get; private set; }

    [Tooltip("Addressables 里给所有物品配置（ItemSO 及其子类）打的标签名")]
    [SerializeField] string itemLabel = "ItemSO";
    [Tooltip("Addressables 里给所有敌人配置（EnemySO 及其子类）打的标签名")]
    [SerializeField] string enemyLabel = "EnemySO";
    [Tooltip("Addressables 里给所有敌人配置（EnemySO 及其子类）打的标签名")]
    [SerializeField] string missionLabel = "MissionSO";

    readonly Dictionary<string, ItemSO> itemConfigs = new Dictionary<string, ItemSO>();
    readonly Dictionary<string, EnemySO> enemyConfigs = new Dictionary<string, EnemySO>();
    readonly Dictionary<string, MissionSO> missionConfigs = new Dictionary<string, MissionSO>();

    AsyncOperationHandle<IList<ItemSO>> itemHandle;
    AsyncOperationHandle<IList<EnemySO>> enemyHandle;
    AsyncOperationHandle<IList<MissionSO>> missionHandle;

    /// <summary>
    /// 只读，表本身改不了。要遍历就遍历，要按 id 取就用 GetItem。
    /// </summary>
    public IReadOnlyDictionary<string, ItemSO> ItemConfigs => itemConfigs;
    public IReadOnlyDictionary<string, EnemySO> EnemyConfigs => enemyConfigs;
    public IReadOnlyDictionary<string, MissionSO> MissionConfigs => missionConfigs;

    /// <summary>
    /// 加载流程结束了（成功失败都算结束，失败时表是空的、Console 里有 error）。
    /// 在它变 true 之前，GetItem 一律返回 null。
    /// </summary>
  

    public struct  ConfigStatu
    {
        public bool ItemIsRead;
        public bool EnemyIsReady;
        public bool MissionIsReady;
    }
    /// <summary>
    /// 各种的加载状态
    /// </summary>
    public ConfigStatu LoadingStatus;
   public struct TaskStatu
    {
        public Task ItemComplete {get;  set; }
        public Task EnemyComplete {get;  set; }
        public Task MissionComplete {get; set; }

    }
    public TaskStatu LoadingTasks;


    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogError("[GameManager] 场景里有多个 GameManager，只保留一个", this);
            Destroy(gameObject);
            return;
        }

        Instance = this;

        // 不 await：让加载在后台跑，谁要用谁去等 Ready
        LoadingTasks.ItemComplete = LoadAllItemConfigsAsync();
        LoadingTasks.EnemyComplete=LoadAllEnemyConfigAsync();
        LoadingTasks.MissionComplete=LoadAllMissionConfigAsync();
        
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;

        // Addressables 是引用计数的，加载出来的东西得还回去，否则永远卸载不掉
        if (itemHandle.IsValid()) Addressables.Release(itemHandle);
    }

    async Task LoadAllItemConfigsAsync()
    {
        itemHandle = Addressables.LoadAssetsAsync<ItemSO>(itemLabel, null);
        await itemHandle.Task;

        if (itemHandle.Status != AsyncOperationStatus.Succeeded)
        {
            Debug.LogError($"[GameManager] 加载物品配置失败（标签 {itemLabel}）：" +
                           $"{itemHandle.OperationException}", this);
            LoadingStatus.ItemIsRead = true;
            return;
        }

        foreach (var so in itemHandle.Result)
        {
            if (so == null) continue;

            if (string.IsNullOrEmpty(so.id))
            {
                Debug.LogError($"[GameManager] {so.name} 没填 id，跳过", so);
                continue;
            }

            if (itemConfigs.ContainsKey(so.id))
            {
                Debug.LogError($"[GameManager] id 重复：{so.id}" +
                               $"（{itemConfigs[so.id].name} 和 {so.name}），保留先来的", so);
                continue;
            }

            itemConfigs.Add(so.id, so);
        }

        LoadingStatus.ItemIsRead = true;
        
    }
    async Task LoadAllMissionConfigAsync()
    {
        missionHandle = Addressables.LoadAssetsAsync<MissionSO>(missionLabel, (a) => { print($"加载完成{a.missionName}"); });
        await missionHandle.Task;
        if (missionHandle.Status != AsyncOperationStatus.Succeeded)
        {
            Debug.LogWarning($"[GameManager] 加载物品配置失败（标签 {missionLabel}）：" +
                           $"{missionHandle.OperationException}", this);
        }
        foreach(var so in missionHandle.Result)
        {
            if(so==null) continue;
            if (string.IsNullOrEmpty(so.missionID))
            {
                Debug.LogError($"[GameManager] {so.name} 没填 id，跳过", so);
                continue;
            }
            if (enemyConfigs.ContainsKey(so.missionID))
            {
                Debug.LogError($"[GameManager] id 重复：{so.missionID}" +
                              $"（{itemConfigs[so.missionID].name} 和 {so.name}），保留先来的", so);
                continue;
            }
            missionConfigs.Add(so.missionID, so);
        }
        LoadingStatus.MissionIsReady = true;
    }
    async Task LoadAllEnemyConfigAsync()
    {
        enemyHandle = Addressables.LoadAssetsAsync<EnemySO>(enemyLabel, (a) => { print($"加载完成{a.targetName}"); });
        await enemyHandle.Task;
        if (enemyHandle.Status != AsyncOperationStatus.Succeeded)
        {
            Debug.LogWarning($"[GameManager] 加载物品配置失败（标签 {enemyLabel}）：" +
                           $"{enemyHandle.OperationException}", this);
        }
        foreach(var so in enemyHandle.Result)
        {
            if(so==null) continue;
            if (string.IsNullOrEmpty(so.enemyID))
            {
                Debug.LogError($"[GameManager] {so.name} 没填 id，跳过", so);
                continue;
            }
            if (enemyConfigs.ContainsKey(so.enemyID))
            {
                Debug.LogError($"[GameManager] id 重复：{so.enemyID}" +
                              $"（{itemConfigs[so.enemyID].name} 和 {so.name}），保留先来的", so);
                continue;
            }
            enemyConfigs.Add(so.enemyID, so);
        }
        LoadingStatus.EnemyIsReady = true;
    }

    /// <summary>
    /// 按 id 反查配置。
    /// 没准备好、id 为空、id 不存在都返回 null，并且打日志 —— 不抛异常，
    /// 免得一个配置没填把整条调用链炸掉。
    /// </summary>
    public ItemSO GetItem(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        if (!LoadingStatus.ItemIsRead)
        {
            Debug.LogWarning($"[GameManager] 物品配置还没读完，取不到 {id}", this);
            return null;
        }

        if (itemConfigs.TryGetValue(id, out var so)) return so;

        Debug.LogWarning($"[GameManager] 没有 id 为 {id} 的物品配置", this);
        return null;
    }
    public EnemySO GetEnemy(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;

        if (!LoadingStatus.EnemyIsReady)
        {
            Debug.LogWarning($"[GameManager] 物品配置还没读完，取不到 {id}", this);
            return null;
        }

        if (EnemyConfigs.TryGetValue(id, out var so)) return so;

        Debug.LogWarning($"[GameManager] 没有 id 为 {id} 的物品配置", this);
        return null;
    }
    public MissionSO GetMission(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        if (!LoadingStatus.MissionIsReady)
        {
            Debug.LogWarning("任务配置还没读完");
            return null;
        }
        if (missionConfigs.TryGetValue(id, out var so)) return so;
        else
        {
            Debug.LogWarning($"没找到{id}的任务");
            return null;
        }
    }
}
