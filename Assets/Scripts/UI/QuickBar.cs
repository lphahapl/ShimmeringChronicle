using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 快捷栏。格子数量固定（Inspector 里配好），数据从 PlayerData.quickBar 来。
///
/// 跟 BagPanel 不一样的地方：快捷栏是常驻 HUD，不靠 UIManager 开关，
/// 所以在 Awake 就订阅 —— 不然没人 Open 它，它就永远收不到变动。
/// </summary>
public class QuickBar : UIBase
{
    /// <summary>固定个数，Inspector 里配好</summary>
    public List<ItemSlot> quickSlots = new List<ItemSlot>();

    /// <summary>要显示的容器（快捷栏）。持引用不持拷贝</summary>
    IItemOperator source;

    /// <summary>当前显示的格子。就是 source.GetItemList() 那个列表本身</summary>
    IReadOnlyList<ItemData> items;

    public PlayerObj player;

    protected override void Awake()
    {
        base.Awake();
        
        this.Subscribe(GameEvents.OnItemsChanged, OnItemsChanged);
        Show();
    }

    protected override void OnDestroy()
    {
        this.UnSubscribe(GameEvents.OnItemsChanged, OnItemsChanged);
        base.OnDestroy();       // 必须调，不然 UIManager 那张表里会留着这个已销毁的界面
    }

    protected override void OnShow()
    {
        Refresh();

        // UI 上配的格子数必须和容器容量一致，不然多出来的那几格在数据里不存在，
        // 拖上去会被 CanExchange 的越界判断挡掉 —— 看着是空格，其实放不了。
        int count = items == null ? 0 : items.Count;
        if (count != quickSlots.Count)
            Debug.LogWarning(
                $"[QuickBar] 格子数对不上：UI 上配了 {quickSlots.Count} 格，" +
                $"容器里有 {count} 格。改 PlayerSO.quickBarCapacity 让它们一致", this);
    }

    /// <summary>
    /// 快捷栏变了。和 BagPanel 同理：参数是要比对的列表，items 是同一个 List 的引用，
    /// 事件到这的时候值已经是新的了，重画就行。
    /// </summary>
    void OnItemsChanged(List<ItemData> changed)
    {
        if (changed != items) return;   // 不是我们在显示的那个容器

        // 关着的时候不刷：数据是活引用，打开时 OnShow 读到的就是最新值，不会漏
        if (!IsVisible) return;

        Refresh();
    }

    /// <summary>
    /// 重画所有格子，顺手把数据源补上。幂等，数据没变也能叫。
    /// 按格子的数量走，不是按数据的数量 —— 快捷栏格子是固定的。
    /// 空位要传 null 占住，不能把后面的往前挤：位置本身是有意义的。
    /// </summary>
    public override void Refresh()
    {
        // 没人 Bind 过就自己找玩家的快捷栏容器
        if (source == null && player != null) source = player.GetContainer(ContainerKind.QuickBar);

        items = source?.GetItemList();

        int count = items == null ? 0 : items.Count;

        for (int i = 0; i < quickSlots.Count; i++)
            quickSlots[i].Bind(i < count ? items[i] : null, i, source);
    }
}
