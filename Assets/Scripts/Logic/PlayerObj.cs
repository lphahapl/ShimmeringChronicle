using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// 玩家。持有两个物品容器（背包 / 快捷栏），自己不再是容器 ——
/// 想动容器就 player.Bag / player.QuickBar，一眼能看出动的是哪个。
///
/// （以前它实现 IItemOperator，等于说"玩家就是一个容器"，但它明明有两个。）
/// </summary>
public class PlayerObj : MonoBehaviour, IDamageable, IContainerOwner,ICanChangeHandingItem
{
    [SerializeField] PlayerSO config;

    private PlayerData data;
    public TMP_Text prompt;
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
        if (SelectedSlot == null || !SelectedSlot.isActiveAndEnabled) return null;
        // Read the source independently of UI refresh order.
        var items = SelectedSlot.Owner?.GetItemList();
        int index = SelectedSlot.index;
        var item = items != null && index >= 0 && index < items.Count ? items[index] : null;
        return item != null && item.count > 0 ? item : null;
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

        d.Bag ??= new List<ItemData>();
        d.quickBar ??= new List<ItemData>();

        bag ??= new ItemContainer(d.Bag);
        quickBar ??= new ItemContainer(d.quickBar);
    }
    public void Awake()
    {
        interactionMask = LayerMask.GetMask("Interactable");
        Data.missions.Add("mission_001", missionData = new MissionData(t));
        
        this.Publish<PlayerData>(GameEvents.OnMissionsChanged, this.data);
    }
    void OnEnable()
    {
        this.Subscribe(GameEvents.OnSlotSelectionRequested, OnSlotSelectionRequested);
        this.Subscribe(GameEvents.OnItemsChanged, OnItemsChanged);
    }
    public void Start()
    {
        UIManager.Instance.RefreshAll();
        this.Publish(GameEvents.OnMissionsChanged, Data);
        print(Data.missions.Count);
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
        if (damage <= 0f) return;
        if (IsDead) return;

        var d = Data;
        if (d == null) return;

        d.hp = Mathf.Max(0f, d.hp - damage);    // 减伤只需要下界保底

        
        this.Publish(GameEvents.Damaged, gameObject, damage, attacker);
        this.Publish(GameEvents.HpChanged, gameObject, d.hp, d.maxHp);

        if (d.hp <= 0f)
        {
            IsDead = true;
            this.Publish(GameEvents.Died, gameObject);
        }
    }
    [Min(0.1f)] public float interactionRange = 2f;
    public LayerMask interactionMask;
    readonly Collider[] interactionHits = new Collider[64];
    IInteractable interactionTarget;

    void LateUpdate() => UpdateInteractionTarget();

    void OnDisable()
    {
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

    // Shared by mouse selection and future quickbar wheel input.
    public bool SelectSlot(ItemSlot slot)
    {
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
        displayedItem = itemData != null && itemData.count > 0 ? itemData : null;
        if (Data != null) Data.HandingItem = displayedItem;
        if (HandingItem != null)
        {
            HandingItem.SetActive(false);
            Destroy(HandingItem);
            HandingItem = null;
        }

        
        if (itemData == null || itemData.count <= 0) return true;
        if (handPos == null || itemData.prefab == null) return false;

        HandingItem = Instantiate(itemData.prefab, handPos, false);
        if(itemData is WeaponData)
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