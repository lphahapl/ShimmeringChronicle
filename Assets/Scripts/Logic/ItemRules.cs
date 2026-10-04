using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 物品容器的公共规则：查找、堆叠、存取、交换。
///
/// 全是静态纯函数，只有列表进出，不碰 MonoBehaviour ——
/// 所以背包、快捷栏、以后的箱子商店共用同一套，不用各抄一遍。
///
/// 「交换」为什么不在 IItemOperator 上：
///   接口只该描述「这个容器自己能干什么」（加、减、能不能被换），
///   而交换是两个容器之间的关系，不是其中任何一方的能力。
/// </summary>
public static class ItemRules
{
    // ══════════════════ 单个容器 ══════════════════

    /// <summary>找第一个 id 对得上、而且够扣 count 个的格子。找不到返回 -1。</summary>
    public static int FindItem(List<ItemData> list, string id, int count = 1)
    {
        if (list == null || string.IsNullOrEmpty(id)) return -1;

        for (int i = 0; i < list.Count; i++)
        {
            var item = list[i];
            if (item != null && item.id == id && item.count >= count) return i;
        }
        return -1;
    }

    /// <summary>容器里这个 id 一共有多少个（所有堆加起来的）。</summary>
    public static int CountOf(List<ItemData> list, string id)
    {
        if (list == null || string.IsNullOrEmpty(id)) return 0;

        int total = 0;
        for (int i = 0; i < list.Count; i++)
        {
            var item = list[i];
            if (item != null && item.id == id) total += item.count;
        }
        return total;
    }

    public static int FindEmptySlot(List<ItemData> list)
    {
        if (list == null) return -1;

        for (int i = 0; i < list.Count; i++)
            if (list[i] == null) return i;

        return -1;
    }

    /// <summary>这一格最多堆几个。空格、或者忘了配（&lt;=0）都当 1。</summary>
    public static int MaxStackOf(ItemData item)
    {
        if (item == null) return 1;

        return item.stackCount <= 0 ? 1 : item.stackCount;
    }

    /// <summary>
    /// 往列表里塞 count 个：先塞同 id 还没满的堆，剩下的填空格。
    /// 返回实际塞进去几个 —— 比 count 小就说明容器满了。
    /// </summary>
    public static int AddInto(List<ItemData> list, ItemSO so, int count)
    {
        if (list == null || so == null || count <= 0) return 0;

        int remaining = count;

        // 1. 先塞现成的堆
        for (int i = 0; i < list.Count && remaining > 0; i++)
        {
            var item = list[i];
            if (item == null || item.id != so.id) continue;

            int space = MaxStackOf(item) - item.count;
            if (space <= 0) continue;

            int take = Mathf.Min(space, remaining);
            item.count += take;
            remaining -= take;
        }

        // 2. 剩下的填空格。
        // **列表长度就是容器容量** —— 没有空格就是满了，不往后长。
        // 会长的话界面的格子数会跟着变，"固定 N 格"就不成立了。
        for (int i = 0; i < list.Count && remaining > 0; i++)
        {
            if (list[i] != null) continue;

            var item = so.CreateItemData();

            int take = Mathf.Min(MaxStackOf(item), remaining);
            item.count = take;
            remaining -= take;

            list[i] = item;
        }

        return count - remaining;
    }

    /// <summary>
    /// 从第 index 格扣掉 count 个。
    ///
    /// 不够扣就一个都不动、返回 false —— 自己守，不指望调用方先查过。
    /// 这是全项目唯一"扣数量"的地方，漏在这里就是静默扣成负数。
    ///
    /// 扣空了置 null 而不是删掉 —— 格子有位置，删了后面的会往前串。
    /// </summary>
    public static bool TakeFrom(List<ItemData> list, int index, int count)
    {
        if (list == null || count <= 0) return false;
        if (index < 0 || index >= list.Count) return false;

        var item = list[index];
        if (item == null || item.count < count) return false;

        item.count -= count;
        if (item.count <= 0) list[index] = null;
        return true;
    }

    /// <summary>
    /// 按 id 跨堆扣 count 个。
    ///
    /// 同一个 id 可能占了好几格（堆满 stackCount 就会），所以不能只看一格 ——
    /// 一格不够，不代表整个容器不够。
    ///
    /// 先数总量，够了才动手：从前往后各扣一点，扣空的格子置 null。
    /// 总量不够就一个都不动、返回 false —— 和 TakeFrom 一个约定。
    /// </summary>
    public static bool TakeAcross(List<ItemData> list, string id, int count)
    {
        if (list == null || string.IsNullOrEmpty(id) || count <= 0) return false;

        // 先数一遍。不先数就开扣，扣到一半发现后面不够，就得回滚了
        if (CountOf(list, id) < count) return false;

        int remaining = count;
        for (int i = 0; i < list.Count && remaining > 0; i++)
        {
            var item = list[i];
            if (item == null || item.id != id) continue;

            int take = Mathf.Min(item.count, remaining);

            // 扣数量和"扣空置 null"的规则只有 TakeFrom 一处。
            // 这里的 take 一定 <= item.count，所以它不会失败
            TakeFrom(list, i, take);
            remaining -= take;
        }

        return true;
    }

    // ══════════════════ 两个容器之间：交换 ══════════════════
    //
    // 和上面分开是因为它们不是一回事：上面是"容器自己的事"，
    // 这里是"两个容器之间的关系"。
    //
    // CanExchange 是纯查询，不改任何状态 —— UI 拖拽时鼠标停在哪一格上、
    // 那一格能不能放，得实时判断（高亮 / 红叉），不能靠"试一下再回滚"。
    // Exchange 第一句就调它，保证"判断"和"执行"永远是同一套规则。

    /// <summary>
    /// 能不能把 a 的第 aIndex 格里的东西放进 b 的第 bIndex 格。
    /// count 传 0 表示整格都拿（普通拖拽），正数表示只拿这么多个（拆分拖拽）。
    /// </summary>
    public static bool CanExchange(IItemOperator a, int aIndex, IItemOperator b, int bIndex, int count = 0)
    {
        if (a == null || b == null) return false;

        // 只有源需要"这一格归我支配"（非空；以后有"绑定 / 锁定"也在这挡）。
        //
        // 落点不查 —— b.CanExchange(index) 问的是"这一格有没有东西可以被拿走"，
        // 空格当然没有，但"拖到空格"是最常见的操作。查它等于把这条路堵死，
        // 下面那句 if (to == null) return true 永远到不了。
        if (!a.CanExchange(aIndex)) return false;

        var mine = a.GetItemList();
        var theirs = b.GetItemList();
        if (mine == null || theirs == null) return false;

        if (aIndex < 0 || aIndex >= mine.Count) return false;
        if (bIndex < 0 || bIndex >= theirs.Count) return false;
        if (ReferenceEquals(mine, theirs) && aIndex == bIndex) return false;

        var from = mine[aIndex];
        if (from == null || from.count <= 0) return false;

        int move = (count <= 0 || count > from.count) ? from.count : count;
        var to = theirs[bIndex];

        // 落点空 → 放得下
        if (to == null) return true;

        // 落点同 id → 得还有堆叠空间
        if (to.id == from.id) return MaxStackOf(to) - to.count > 0;

        // 落点不同 id → 只能整格互换，拆着拖过来的不给换
        return move >= from.count;
    }

    /// <summary>
    /// 执行交换：把 a 的第 aIndex 格里的东西放进 b 的第 bIndex 格。
    ///   落点空     → 搬过去（拆分拖拽要 Clone 一份，不然两格会指向同一个对象）
    ///   落点同 id  → 合并，被 stackCount 挡住，放不下的留在原格
    ///   不同 id    → 整格互换
    ///
    /// 两边的列表都变了，所以两个都广播。
    /// </summary>
    public static bool Exchange(IItemOperator a, int aIndex, IItemOperator b, int bIndex, int count = 0)
    {
        if (!CanExchange(a, aIndex, b, bIndex, count)) return false;

        var mine = a.GetItemList();
        var theirs = b.GetItemList();
        var from = mine[aIndex];
        var to = theirs[bIndex];

        int move = (count <= 0 || count > from.count) ? from.count : count;

        if (to == null)
        {
            if (move >= from.count)
            {
                theirs[bIndex] = from;
                mine[aIndex] = null;
            }
            else
            {
                var split = from.Clone();
                split.count = move;
                from.count -= move;
                theirs[bIndex] = split;
            }
        }
        else if (to.id == from.id)
        {
            int take = Mathf.Min(MaxStackOf(to) - to.count, move);
            to.count += take;
            from.count -= take;
            if (from.count <= 0) mine[aIndex] = null;
        }
        else
        {
            theirs[bIndex] = from;
            mine[aIndex] = to;
        }

        // Publish 的 this object _ 是个摆设 —— 事件表是全局的，根本不看谁发的。
        // 所以借 a 当接收者调一下就行，重点是两个列表都要广播出去。
        a.Publish(GameEvents.OnItemsChanged, mine);
        a.Publish(GameEvents.OnItemsChanged, theirs);
        return true;
    }
}
