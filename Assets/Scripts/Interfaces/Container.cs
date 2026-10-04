using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 物品容器：只读的那一半。
/// 背包、快捷栏、箱子、商店，只要能被"看"和"查"，就实现这个。
/// 读写分开是为了让只能看的地方（商店货架、展示柜）不用被迫实现 AddItem。
/// </summary>
public interface IItemProvider
{
    /// <summary>容器里的格子。空格子是 null 占位，不往前挤 —— 位置本身有意义。</summary>
    public List<ItemData> GetItemList();

    /// <summary>容器里有没有这个 id 的东西，返回值是找到的下标</summary>
    public bool ContainsItem(string id);
}

/// <summary>
/// 物品容器：能改的那一半。
/// </summary>
public interface IItemOperator : IItemProvider
{
    /// <summary>
    /// 往里加。同一个 id 先塞现成的堆，堆满了再开新格，受 stackCount 约束。
    /// 返回实际放进去几个 —— 比 count 小就说明有没放进去的。
    /// </summary>
    public int AddItem(string id, int count);

    /// <summary>
    /// 按 id 扣。同一个 id 可能占了好几格（堆满 stackCount 就会），所以跨堆扣：
    /// 先数总量够不够，够了才动手，从前往后各扣一点。
    /// 总量不够就一个都不动、返回 false —— 不做"有多少扣多少"，那样调用方很难判断结果。
    /// 想指定某一格用 RemoveItemAt。
    /// </summary>
    public bool RemoveItem(string id, int count);

    /// <summary>
    /// 按格子位置扣。位置由 ItemSlot.index 给。
    /// 同一个 id 占了好几格时只能用它 —— 那时按 id 扣是含糊的。
    /// </summary>
    public bool RemoveItemAt(int index, int count);

    /// <summary>
    /// 这一格能不能被拿走（拿去做交换、丢弃、使用）。
    /// 空格不能；以后有"绑定 / 锁定"的物品也在这里挡。
    ///
    /// 注意它问的是**源**。落点是空的完全正常，别拿它去查落点 ——
    /// 那是"能不能放进去"，由 ItemRules.CanExchange 按落点情况判。
    ///
    /// 它留在接口上是因为这是"容器自己的能力"（这一格归不归我支配）；
    /// 而"两个容器之间怎么换"是关系，在 ItemRules 里。
    /// </summary>
    public bool CanExchange(int index);
}
