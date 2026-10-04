using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 一个物品容器：包着一个 List&lt;ItemData&gt;，实现 IItemOperator。
///
/// 背包、快捷栏各持有一个；以后箱子、商店、掉落物也一样 ——
/// 容器规则只有这一份实现，不用每个持有方抄一遍。
///
/// 改完自己广播 OnItemsChanged，参数就是自己那个列表。
/// 界面收到后拿它跟自己显示的那个比（比引用），对得上才刷新。
/// </summary>
public class ItemContainer : IItemOperator
{
    readonly List<ItemData> items;

    /// <summary>包住一个已有的列表。传 null 会建一个空的，但那样外面就拿不到它了 ——
    /// 所以调用方应该先把列表建好（PlayerObj.EnsureContainers 就是这么做的）。</summary>
    public ItemContainer(List<ItemData> items)
    {
        this.items = items ?? new List<ItemData>();
    }
  
    /// <summary>容器里的格子。空格子是 null 占位，不往前挤。</summary>
    public List<ItemData> GetItemList() => items;

    public bool ContainsItem(string id) => ItemRules.FindItem(items, id) >= 0;

    /// <summary>这一格能不能被拿走（源方向）。空格不能；以后有"绑定 / 锁定"的物品也在这里挡。</summary>
    public bool CanExchange(int index)
    {
        if (index < 0 || index >= items.Count) return false;

        var item = items[index];
        return item != null && item.count > 0;
    }

    public int AddItem(string id, int count)
    {
        if (string.IsNullOrEmpty(id) || count <= 0) return 0;

        // 靠 id 反查配置走 GameManager，它还没加载完就放不进去
        var manager = GameManager.Instance;
        if (manager == null || !manager.LoadingStatus.ItemIsRead)
        {
            Debug.LogWarning("[ItemContainer] GameManager 还没准备好，加不了物品");
            return 0;
        }

        var so = manager.GetItem(id);
        if (so == null) return 0;

        int added = ItemRules.AddInto(items, so, count);
        if (added <= 0) return 0;

        this.Publish(GameEvents.OnItemsChanged, items);
        return added;
    }

    public bool RemoveItem(string id, int count)
    {
        if (!ItemRules.TakeAcross(items, id, count)) return false;

        this.Publish(GameEvents.OnItemsChanged, items);
        return true;
    }

    public bool RemoveItemAt(int index, int count)
    {
        if (!ItemRules.TakeFrom(items, index, count)) return false;

        this.Publish(GameEvents.OnItemsChanged, items);
        return true;
    }
}
