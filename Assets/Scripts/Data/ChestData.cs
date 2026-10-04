using System.Collections.Generic;

/// <summary>
/// 箱子的运行时数据。
/// items 的长度 == capacity，空格是 null 占位 —— 和玩家背包同一套规则。
/// </summary>
public class ChestData
{
    /// <summary>格子数。不是当前数量，列表长度就是它</summary>
    public int capacity = 25;

    /// <summary>目前没人读，见 ChestSO.num 的说明</summary>
    public int num;

    public List<ItemData> items;
}
