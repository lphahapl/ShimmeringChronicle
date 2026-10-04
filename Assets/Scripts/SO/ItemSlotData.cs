using UnityEngine;

/// <summary>
/// 背包 / 快捷栏里的一格（配置侧）。
/// PlayerSO 用它来逐格选物品、配数量。
///
/// 运行时那边不用这个类 —— PlayerSO.FillExtra 会拿 config.CreateItemData()
/// 现造一份 ItemData 出来，数量取自 num。所以这里的实例只在资产里躺着，
/// 不会被运行时改到。
/// </summary>
[System.Serializable]
public class  ItemSlotData
{
    [Tooltip("拖物品配置进来。武器就拖 WeaponSO")]
    public ItemSO config;

    [Tooltip("这一格给几个")]
    public int num = 1;

    /// <summary>
    /// 配置的 id，只读。
    /// 不单独存成字段 —— config 上已经有一份了，存两份就是「改了一边另一边不变」。
    /// </summary>
    public string id => config != null ? config.id : null;

    public bool IsEmpty => config == null || num <= 0;
}
