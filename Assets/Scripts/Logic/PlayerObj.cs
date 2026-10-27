using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 玩家。持有两个物品容器（背包 / 快捷栏），自己不再是容器 ——
/// 想动容器就 player.Bag / player.QuickBar，一眼能看出动的是哪个。
///
/// （以前它实现 IItemOperator，等于说"玩家就是一个容器"，但它明明有两个。）
/// </summary>
[DefaultExecutionOrder(-30)]
public class PlayerObj : MonoBehaviour, IDamageable, IContainerOwner,ICanChangeHandingItem
{
    [SerializeField] PlayerSO config;

    private PlayerData data;
    public TMP_Text prompt;
    // 玩家事实只在这里保存，动作脚本只响应变化。
    public event Action<ItemData> EquippedItemChanged;
    public event Action<GameObject> Staggered;
    public event Action<bool> ActionLockChanged;
    public bool IsTalking { get; private set; }
    public bool CanAct => isActiveAndEnabled && !IsDead && !IsTalking;
    public ItemData EquippedItem
    {
        get
        {
            if (Data == null) return null;
            return Data.HandingItem;
        }
    }

    public IItemOperator GetContainer(ContainerKind kind = ContainerKind.Default)
    {
        return kind switch
        {
            ContainerKind.QuickBar => QuickBar,
            _ => Bag
        };
    }

    ItemContainer bag;
    ItemContainer quickBar;
    public CharacterTime hitStop;
    public float poiseRecoveryPerSecond;
    public float poise;
    public float poiseRecoveryTime;
    public float dtFix
    {
        get
        {
            if (hitStop != null) return hitStop.DeltaTime;
            return Time.deltaTime;
        }
    }

    public MissionSO t;
    public MissionData missionData;


    public GameObject HandingItem;
    ItemData displayedItem;

    public Transform handPos;
    /// <summary>
    /// 目前手持物品的引用
    /// </summary>
    public ItemSlot SelectedSlot { get; private set; }
    public ItemData SelectedItem => GetSelectedItem();
    private ItemData GetSelectedItem()
    {
        if (SelectedSlot == null)
        {
            if (displayedItem == null || displayedItem.count <= 0) return null;
            if (Data.Bag.Contains(displayedItem) || Data.quickBar.Contains(displayedItem)) return displayedItem;
            return null;
        }
        // Read the source independently of UI refresh order.
        IReadOnlyList<ItemData> items = null;
        if (SelectedSlot.Owner != null) items = SelectedSlot.Owner.GetItemList();
        int index = SelectedSlot.index;
        ItemData item = null;
        if (items != null && index >= 0 && index < items.Count) item = items[index];
        if (item != null && item.count > 0) return item;
        return null;
    }

    /// <summary>已经死了。死了之后不再受伤，也不会重复广播死亡</summary>
    public bool IsDead { get; private set; }

    /// <summary>
    /// 外部访问数据。
    /// 懒加载：谁先来读都拿得到，不依赖 Awake 顺序。
    /// </summary>
    public PlayerData Data
    {
        get
        {
            if (data == null)
            {
                if (config == null)
                {
                    Debug.LogError("[PlayerObj] 没有 PlayerSO，拿不到数据", this);
                    return null;
                }
                data = config.CreateData();
                poise = data.poise;
                poiseRecoveryPerSecond = data.poiseRecoveryPerSecond;
                poiseRecoveryTime = data.poiseRecoveryTime;
            }

            return data;
        }
    }

    /// <summary>背包容器</summary>
    public IItemOperator Bag
    {
        get { EnsureContainers(); return bag; }
    }

    /// <summary>快捷栏容器。数据和背包一样躺在 PlayerData 里，只是包成了另一个容器</summary>
    public IItemOperator QuickBar
    {
        get { EnsureContainers(); return quickBar; }
    }

    /// <summary>
    /// 懒建两个容器，并且保证它们包的是 PlayerData 里那两个真列表 ——
    /// 列表可能还没建（PlayerSO 没配），这里补一个。不补的话容器会自己新建一个空列表，
    /// 之后往容器里加的东西在 Data 里看不到。
    /// </summary>
    void EnsureContainers()
    {
        var d = Data;
        if (d == null) return;

        if (d.Bag == null) d.Bag = new List<ItemData>();
        if (d.quickBar == null) d.quickBar = new List<ItemData>();

        if (bag == null) bag = new ItemContainer(d.Bag);
        if (quickBar == null) quickBar = new ItemContainer(d.quickBar);
    }
    public void Awake()
    {
        if (!hitStop) hitStop = GetComponent<CharacterTime>();
        interactionMask = LayerMask.GetMask("Interactable");
        if (Data != null && t != null && !Data.missions.ContainsKey(t.missionID))
        {
            missionData = new MissionData(t);
            Data.missions.Add(t.missionID, missionData);
        }
        
        this.Publish<PlayerData>(GameEvents.OnMissionsChanged, this.data);
    }
    void OnEnable()
    {
        this.Subscribe(GameEvents.OnPlayerTalk, OnPlayerTalk);
        this.Subscribe(GameEvents.HitStopRequested, OnHitStopRequested);
        this.Subscribe(GameEvents.OnSlotSelectionRequested, OnSlotSelectionRequested);
        this.Subscribe(GameEvents.OnItemsChanged, OnItemsChanged);
    }
    public void Start()
    {
        EquipStartingWeapon();
        if (UIManager.Instance != null) UIManager.Instance.RefreshAll();
        this.Publish(GameEvents.OnMissionsChanged, Data);

    }

    /// <summary>
    /// 给玩家添加物品：先放背包，剩余的放快捷栏。
    /// 返回实际添加数量，并按这个数量通知收集任务进度。
    /// </summary>
    public int AddItem(string id, int count)
    {
        if (string.IsNullOrEmpty(id) || count <= 0) return 0;

        EnsureContainers();
        if (data == null) return 0;

        int added = bag.AddItem(id, count);
        if (added < count)
            added += quickBar.AddItem(id, count - added);

        if (added > 0)
            this.Publish(GameEvents.OnPushMissionProgress, data, RequirementType.收集物品, id, added);

        return added;
    }

    /// <summary>背包 或 快捷栏 里有没有 —— 也就是"玩家身上有没有这个东西"</summary>
    public bool HasItem(string id)
    {
        if (string.IsNullOrEmpty(id)) return false;

        return (Bag != null && Bag.ContainsItem(id))
            || (QuickBar != null && QuickBar.ContainsItem(id));
    }

    /// <summary>
    /// 手动喊一声"背包变了"。
    /// 直接改 Data.Bag（测试、或者一次改好几处）之后就靠它，不然界面不会知道。
    /// </summary>
    public void PublishBagChanged()
    {
        var d = Data;
        if (d != null) this.Publish(GameEvents.OnItemsChanged, d.Bag);
    }

    /// <summary>手动喊一声"快捷栏变了"。直接改 Data.quickBar 之后用它。</summary>
    public void PublishQuickBarChanged()
    {
        var d = Data;
        if (d != null) this.Publish(GameEvents.OnItemsChanged, d.quickBar);
    }

    // ───────────────────────── 受伤 ─────────────────────────

    public void TakeDamage(float damage, GameObject attacker)
    {
        TakeDamage(damage, attacker, 0f);
    }

    public void TakeDamage(float damage, GameObject attacker, float poiseDamage)
    {
        damage = Mathf.Max(0f, damage);
        poiseDamage = Mathf.Max(0f, poiseDamage);
        if (IsDead || Data == null) return;
        if (damage <= 0f && poiseDamage <= 0f) return;
        poise = Mathf.Max(0f, poise - poiseDamage);
        poiseRecoveryTime = Data.poiseRecoveryTime;
        Data.hp = Mathf.Max(0f, Data.hp - damage);
        this.Publish(GameEvents.Damaged, gameObject, damage, attacker);
        this.Publish(GameEvents.HpChanged, gameObject, Data.hp, Data.maxHp);
        if (Data.hp <= 0f) Die();
        else if (poise <= 0f && Staggered != null) Staggered(attacker);
    }

    public void Die()
    {
        if (IsDead || Data == null) return;
        IsDead = true;
        if (Data.hp > 0f)
        {
            Data.hp = 0f;
            this.Publish(GameEvents.HpChanged, gameObject, Data.hp, Data.maxHp);
        }
        if (ActionLockChanged != null) ActionLockChanged(true);
        this.Publish(GameEvents.Died, gameObject);
    }

    public void RemovePlayer()
    {
        Destroy(gameObject);
    }

    void OnPlayerTalk(PlayerObj talkingPlayer, bool talking)
    {
        if (talkingPlayer != this || IsTalking == talking) return;
        IsTalking = talking;
        if (ActionLockChanged != null) ActionLockChanged(!CanAct);
    }

    void OnHitStopRequested(GameObject victim, GameObject attacker, AttackData attack)
    {
        if (victim != gameObject && attacker != gameObject) return;
        if (hitStop == null || attack == null || attack.time <= 0f) return;
        hitStop.Apply(attack.priority, attack.scale, attack.time);
    }

    public void PublishAttackHit(WeaponData weapon, int index)
    {
        if (!CanAct || weapon == null || !ReferenceEquals(weapon, Data.GetWeaponData())) return;
        if (weapon.combo == null || index < 0 || index >= weapon.combo.Count) return;
        this.Publish(GameEvents.OnPlayerCombo, this, index);
    }

    void EquipStartingWeapon()
    {
        if (config == null || config.startingWeapon == null || EquippedItem != null) return;
        foreach (ItemData item in Data.Bag)
        {
            if (item != null && item.id == config.startingWeapon.id)
            {
                ChangeItem(item);
                return;
            }
        }
        foreach (ItemData item in Data.quickBar)
        {
            if (item != null && item.id == config.startingWeapon.id)
            {
                ChangeItem(item);
                return;
            }
        }
    }

    void Update() => RecoveryPoise(dtFix);

    private void RecoveryPoise(float deltaTime)
    {
        if (IsDead || data == null) return;
        poise = Mathf.Min(data.poise, poise + poiseRecoveryPerSecond * dtFix);
        poise = Mathf.Clamp(poise,0, data.poise);
        poiseRecoveryTime = Mathf.Max(0f, poiseRecoveryTime - dtFix);
        if (poiseRecoveryTime <= 0f) poise = data.poise;
    }

    [Min(0.1f)] public float interactionRange = 2f;
    public LayerMask interactionMask;
    readonly Collider[] interactionHits = new Collider[64];
    IInteractable interactionTarget;

    void LateUpdate() => UpdateInteractionTarget();

    void OnDisable()
    {
        this.UnSubscribe(GameEvents.OnPlayerTalk, OnPlayerTalk);
        this.UnSubscribe(GameEvents.HitStopRequested, OnHitStopRequested);
        IsTalking = false;
        this.UnSubscribe(GameEvents.OnSlotSelectionRequested, OnSlotSelectionRequested);
        this.UnSubscribe(GameEvents.OnItemsChanged, OnItemsChanged);
        SelectedSlot = null;
        ChangeItem(null);
        this.Publish(GameEvents.OnSelectedSlotChanged, (ItemSlot)null);
        interactionTarget = null;
        if (prompt) prompt.gameObject.SetActive(false);
    }

    void UpdateInteractionTarget()
    {
        interactionTarget = null;
        var origin = transform.position + Vector3.up * 0.8f;
        float nearest = float.PositiveInfinity;
        if (!IsDead)
        {
            int count = Physics.OverlapSphereNonAlloc(origin, interactionRange, interactionHits,
                interactionMask, QueryTriggerInteraction.Ignore);//忽略trriger
            for (int i = 0; i < count; i++)
            {
                var hit = interactionHits[i];
                if (hit.transform.IsChildOf(transform)) continue;//跳过自己
                var candidate = hit.GetComponent<IInteractable>();//尝试找脚本
                if (candidate == null)
                {
                    candidate = hit.GetComponentInChildren<IInteractable>();//尝试找脚本
                    if (candidate == null)
                    {
                        candidate = hit.GetComponentInParent<IInteractable>();//尝试找脚本
                    }
                }
                var component = candidate as Behaviour;
                if (component && !component.isActiveAndEnabled) continue;//如果没启用或者被禁用
                if (candidate == null || !candidate.CanInteract(gameObject)) continue;//不可交互对象
                var point = hit.ClosestPoint(origin);
                float distance = (point - origin).sqrMagnitude;
                if (distance >= nearest || !CanSeeInteraction(origin, hit.bounds.center, candidate)) continue;
                nearest = distance;
                interactionTarget = candidate;
            }
        }
        if (!prompt) return;
        prompt.raycastTarget = false;
        prompt.gameObject.SetActive(interactionTarget != null);
        if (interactionTarget != null) prompt.text = "[F] " + interactionTarget.InteractPrompt();
    }

    bool CanSeeInteraction(Vector3 origin, Vector3 point, IInteractable candidate)
    {
        var delta = point - origin;
        foreach (var hit in Physics.RaycastAll(origin, delta.normalized, delta.magnitude,
            ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.IsChildOf(transform)) continue;//是自己
            if (ReferenceEquals(hit.collider.GetComponentInParent<IInteractable>(), candidate)|| ReferenceEquals(hit.collider.GetComponent<IInteractable>(), candidate)|| ReferenceEquals(hit.collider.GetComponentInChildren<IInteractable>(), candidate)) continue;//任何一个是交互物体，就跳过
            return false;
           
            
        }
        return true;
    }
    public void TryInteract()
    {
        UpdateInteractionTarget();
        if (interactionTarget == null || IsDead) return;
        interactionTarget.OnInteract(gameObject);
        UpdateInteractionTarget();
    }
    /// <summary>
    /// 接收点击请求，并且尝试选中这个格子
    /// </summary>
    /// <param name="itemSlot"></param>
    private void OnSlotSelectionRequested(ItemSlot itemSlot)
    {
        SelectSlot(itemSlot);
    }

    /// <summary>
    /// 容器变了。
    
    /// 刻意不在这里重建模型：数量变化（捡到、用掉一个）也不该让手上的东西重来一遍。
    /// </summary>
    void OnItemsChanged(List<ItemData> changed)
    {
        var d = Data;
        if (d == null) return;

        // 只看选中那件东西所在的容器
        if (!ReferenceEquals(changed, d.Bag) && !ReferenceEquals(changed, d.quickBar)) return;

        var selected = SelectedItem;
        if (!ReferenceEquals(selected, displayedItem)) ChangeItem(selected);
    }

   
    public bool SelectSlot(ItemSlot slot)
    {
        if (!CanAct) return false;
        if (slot == null || (slot.Owner != Bag && slot.Owner != QuickBar)) return false;
        if (SelectedSlot == slot) return false;
        SelectedSlot = slot;

       
        // 关心选中变化的（高亮边框之类）订阅 OnSelectedSlotChanged。
        ChangeItem(SelectedItem);
        //格子被成功选中，发布事件更新格子ui
        this.Publish(GameEvents.OnSelectedSlotChanged, slot);
        return true;
    }

    public ItemData GetHandingItem()
    {
        return SelectedItem;
    }

    public bool ChangeItem(ItemData itemData)
    {
        displayedItem = null;
        if (itemData != null && itemData.count > 0) displayedItem = itemData;
        if (Data != null) Data.HandingItem = displayedItem;
        if (HandingItem != null)
        {
            HandingItem.SetActive(false);
            Destroy(HandingItem);
            HandingItem = null;
        }

        
        if (EquippedItemChanged != null) EquippedItemChanged(displayedItem);
        if (itemData == null || itemData.count <= 0) return true;
        if (handPos == null || itemData.prefab == null) return false;

        HandingItem = Instantiate(itemData.prefab, handPos, false);
        if(itemData is WeaponData equippedWeapon)
        {
            foreach (var weapon in HandingItem.GetComponentsInChildren<WeaponObj>())
            {
                weapon.InitWeapon(gameObject);
                weapon.weaponData = Data.GetWeaponData();
            }

        }

        return true;
    }
    
}
