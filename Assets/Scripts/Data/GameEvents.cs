using System.Collections.Generic;
using System.ComponentModel;
using UnityEngine;

/// <summary>
/// 全局游戏事件的键。
///
/// 注意：EventKey 没有重写 Equals / GetHashCode，字典用的是「引用相等」，
/// 所以每个键必须是这里的 static readonly 单例 —— 新建一个 new EventKey(...) 是配不上的，
/// 而且不会报错，只是永远收不到。所有键都写在这一处。
/// </summary>
public static class GameEvents
{
    public static readonly EventKey<PlayerData> OnMissionsChanged = new EventKey<PlayerData>();
    /// <summary>血量变化：(谁, 当前血量, 血量上限)。血条订阅这个</summary>
    public static readonly EventKey<GameObject, float, float> HpChanged =
        new EventKey<GameObject, float, float>();

    /// <summary>受伤：(受害者, 伤害值, 攻击者)。攻击者可能为 null —— 毒、掉落、陷阱没有来源</summary>
    public static readonly EventKey<GameObject, float, GameObject> Damaged =
        new EventKey<GameObject, float, GameObject>();

    /// <summary>死亡：(谁)。死亡之后不再发 HpChanged / Damaged</summary>
    public static readonly EventKey<GameObject> Died =
        new EventKey<GameObject>();
    /// <summary>
    /// 任何物品容器变了。参数就是变动的那个列表 —— 谁发的不用管，
    /// 接收方拿它跟自己正在显示的那个比，比得上才刷新。
    ///
    /// 背包、快捷栏、箱子、商店共用这一个：接收方比的是列表本身，
    /// 根本不关心它是谁的，所以没必要每种容器加一个事件。
    /// </summary>
    public static readonly EventKey<List<ItemData>> OnItemsChanged =
        new EventKey<List<ItemData>>();

    public static readonly EventKey<IContainerOwner> OnChestShow = new EventKey<IContainerOwner>();
    public static readonly EventKey<IContainerOwner> OnChestHide = new EventKey<IContainerOwner>();
    public static readonly EventKey<ItemSlot> OnSlotSelectionRequested = new EventKey<ItemSlot>();
    public static readonly EventKey<ItemSlot> OnSelectedSlotChanged = new EventKey<ItemSlot>();
    public static readonly EventKey<ChestWindow> OnChestWindowHoverChanged = new EventKey<ChestWindow>();
    public static readonly EventKey<int>OnPlayerCombo=new EventKey<int>();
    public static readonly EventKey<EnemyObj,int>OnEnemyCombo=new EventKey<EnemyObj,int>();
    public static readonly EventKey<EnemyObj,ItemData>OnEnemyChangedHandingItem=new EventKey<EnemyObj, ItemData>();//敌人切换手持物品或者初始化时调用
    /// <summary>
    /// 参数分别为:
    /// 谁，要求类型，id，个数
    /// </summary>
    public static readonly EventKey<PlayerData,RequirementType,string,int>OnPushMissionProgress=new EventKey<PlayerData, RequirementType, string,int>();

}
