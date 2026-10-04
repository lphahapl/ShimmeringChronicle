using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "ChestConfig", menuName = "Configs/ChestConfig")]
public class ChestSO : ScriptableObject
{
    [Tooltip("箱子格子数。列表长度就是容量，界面上固定显示这么多格，空格用 null 占位")]
    [Min(0)] public int capacity = 25;

    /// <summary>
    /// 目前没人读 —— 只在下面搬进 ChestData。要是它本来想表达什么（编号？），
    /// 补上用处或者删掉，别留着当第二个容量。
    /// </summary>
    public int num;

    public List<ItemSlotData> items;

    public void CreateChestData(ChestData data)
    {
        if (data == null) return;

        data.capacity = capacity;
        data.items = items.ToItemDataList(capacity);
        data.num = num;
    }
}
