using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// ItemData 相关的扩展。
/// </summary>
public static class ItemDataExtensions
{
    /// <summary>
    /// 把配置侧的格子列表建成运行时数据，并补足到容量。
    ///
    /// 每格各自走 CreateItemData()，武器会正确 new 出 WeaponData、combo 也深拷一份，
    /// 所以拿到的跟 SO 资产、跟列表里那些 ItemSlotData 都不共享。数量取自格子的 num。
    ///
    /// **列表长度就是容器容量**：配不满的补 null 占位，配超了不截断（按配的来）。
    /// 不补的话列表长度就等于"配了几条"，界面上一个空格都没有 —— 拖拽就没地方落。
    ///
    /// 配置为空 / 数量非正的格子也建成 null 占位，保持下标对齐 ——
    /// 位置本身是有意义的（快捷栏尤其），不能把空格子挤掉。
    /// </summary>
    public static List<ItemData> ToItemDataList(this List<ItemSlotData> source, int capacity = 0)
    {
        int count = source == null ? 0 : source.Count;
        int size = Mathf.Max(count, capacity);

        var list = new List<ItemData>(size);

        for (int i = 0; i < count; i++)
        {
            var slot = source[i];
            if (slot == null || slot.config == null || slot.num <= 0)
            {
                list.Add(null);
                continue;
            }

            var item = slot.config.CreateItemData();
            item.count = slot.num;
            list.Add(item);
        }

        // 补到容量
        while (list.Count < size) list.Add(null);

        return list;
    }
}
