using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 背包界面。只负责把数据画成格子，不持有真相 ——
/// 真相在 PlayerData.Bag 里，这里拿的是同一个 List 的引用。
///
/// 数据来源两种，任选：
///   1. 外部调 Bind() 推进来（快捷栏、箱子、商店都走这个入口）
///   2. Inspector 挂个 PlayerObj，没 Bind 过时自己取 player.Data.Bag
///
/// 背包一变就靠 OnItemsChanged 自动重画，不用谁记得手动刷。
/// </summary>
public class BagPanel : UIBase
{
    public override bool BlocksQuickBarScroll => isActiveAndEnabled && IsVisible && isPointerIn;

    [Tooltip("格子预制体，上面必须挂 ItemSlot")]
    [SerializeField] GameObject slotPrefab;

    [Tooltip("格子生成到这个节点下，配合自动布局组件用")]
    [SerializeField] Transform spawnPos;

    [Tooltip("测试用。不挂就只能靠外部 Bind() 推数据")]
    [SerializeField] PlayerObj player;

    /// <summary>
    /// 运行时生成出来的格子，下标和 items 一一对应。
    /// 只读不给外部改 —— 要加要减走 RebuildSlots。
    /// </summary>
    [SerializeField] List<ItemSlot> slots = new List<ItemSlot>();

    /// <summary>当前显示的容器。持引用不持拷贝，所以容器里变了刷新一下就同步</summary>
    IItemOperator source;

    /// <summary>当前显示的格子。就是 source.GetItemList() 那个列表本身</summary>
    IReadOnlyList<ItemData> items;

    /// <summary>外部把要显示的容器推进来（背包、箱子、商店都行）。传 null 就是清空。</summary>
    public void Bind(IItemOperator source)
    {
        this.source = source;
        Refresh();
    }

    protected override void Awake()
    {
        base.Awake();


        this.Subscribe(GameEvents.OnItemsChanged, OnItemsChanged);
        StartHidden();
    }

    protected override void OnShow()
    {
        Refresh();
    }

    protected override void OnDestroy()
    {
        this.UnSubscribe(GameEvents.OnItemsChanged, OnItemsChanged);
        base.OnDestroy();       // 必须调，不然 UIManager 那张表里会留着这个已销毁的界面
    }

    /// <summary>
    /// 背包变了。参数就是变动的那个列表 —— 也可能是别的容器发的，所以要比对一下。
    /// items 拿的是同一个 List 的引用，所以这时候数据已经是新的了，重画就行。
    /// </summary>
    void OnItemsChanged(List<ItemData> changed)
    {
        if (changed != items) return;   // 不是我们在显示的那个容器

        // 关着的时候不刷：数据是活引用，打开时 OnShow 读到的就是最新值，不会漏
        if (!IsVisible) return;

        Refresh();
    }

    /// <summary>
    /// 重画所有格子
    ///
    /// </summary>
    public override void Refresh()
    {
        // 没人 Bind 过就自己从玩家身上取背包容器
        if (source == null && player != null) source = player.GetContainer(ContainerKind.Bag);

        items = source?.GetItemList();

        RebuildSlots();

        int count = items == null ? 0 : items.Count;
        for (int i = 0; i < slots.Count; i++)
            slots[i].Bind(i < count ? items[i] : null, i, source);
    }

    /// <summary>
    /// 让格子数量和 items 对齐：不够就 Instantiate 补，多了就 SetActive(false) 藏起来。
    /// 多了不 Destroy 是有意的 —— 背包反复开关时反复销毁重建既费又会让自动布局抖，
    /// 藏着复用更稳。slots 里的下标永远和 items 对齐，空位传 null 给 ItemSlot。
    /// </summary>
    void RebuildSlots()
    {
        int need = items == null ? 0 : items.Count;

        while (slots.Count < need)
        {
            var go = Instantiate(slotPrefab, spawnPos);
            var slot = go.GetComponent<ItemSlot>();

            if (slot == null)
            {
                Debug.LogError("[BagPanel] 格子预制体上没挂 ItemSlot", go);
                Destroy(go);
                return;         // 别死循环
            }

            slots.Add(slot);
        }

        for (int i = 0; i < slots.Count; i++)
            slots[i].gameObject.SetActive(i < need);
    }
}
