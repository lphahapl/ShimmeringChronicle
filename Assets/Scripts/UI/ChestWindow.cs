using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ChestWindow : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    public Button exit;
    public bool isPointerIn;
    public bool BlocksQuickBarScroll => isActiveAndEnabled && isPointerIn;
    public IContainerOwner ContainerOwner { get; set; }
    public void Awake()
    {
        exit.onClick.AddListener(() =>
        {
            if (ContainerOwner is TestChest chest) chest.Close();
            gameObject.SetActive(false);
        });
    }
    /// <summary>当前显示的容器。持引用不持拷贝，所以容器里变了刷新一下就同步</summary>
    IItemOperator source;

    /// <summary>
    /// 运行时生成出来的格子，下标和 items 一一对应。
    /// 只读不给外部改 —— 要加要减走 RebuildSlots。
    /// </summary>
    [SerializeField] List<ItemSlot> slots = new List<ItemSlot>();

    /// <summary>当前显示的格子。就是 source.GetItemList() 那个列表本身</summary>
    IReadOnlyList<ItemData> items;

    /// <summary>外部把要显示的容器推进来（背包、箱子、商店都行）。传 null 就是清空。</summary>
    public void Bind(IItemOperator source)
    {
        this.source = source;
        Refresh();

        // UI 上配的格子数必须和容器容量一致，不然多出来的那几格在数据里不存在，
        // 拖上去会被 CanExchange 的越界判断挡掉 —— 看着是空格，其实放不了。
        int count = source == null ? 0 : source.GetItemList()?.Count ?? 0;
        if (count != slots.Count)
            Debug.LogWarning(
                $"[ChestWindow] 格子数对不上：UI 上配了 {slots.Count} 格，" +
                $"容器里有 {count} 格。改 ChestSO.capacity 让它们一致", this);
    }
    void OnEnable()
    {
        this.Subscribe(GameEvents.OnItemsChanged, OnItemsChanged);
        Refresh();
    }

    void OnDisable()
    {
        this.UnSubscribe(GameEvents.OnItemsChanged, OnItemsChanged);
        isPointerIn = false;
        this.Publish(GameEvents.OnChestWindowHoverChanged, this);
    }

    void OnItemsChanged(List<ItemData> changed)
    {
        if (source == null || !ReferenceEquals(changed, source.GetItemList())) return;
        Refresh();
    }

    public void Refresh()
    {
        // 没人 Bind 过就自己从玩家身上取背包容器
        if (source == null) return;

        items = source?.GetItemList();
        int count = items == null ? 0 : items.Count;
        for (int i = 0; i < slots.Count; i++)
            slots[i].Bind(i < count ? items[i] : null, i, source);
    }
    public void OnPointerEnter(PointerEventData eventData)
    {
        isPointerIn = true;
        this.Publish(GameEvents.OnChestWindowHoverChanged, this);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isPointerIn = false;
        this.Publish(GameEvents.OnChestWindowHoverChanged, this);
    }
}
   
