using UnityEngine;

/// <summary>
/// 物品配置的基类（非泛型那一层），和玩家/敌人那棵树是平行关系。
///
/// 职责和 BaseDataSO 是一样的：
///   1. 放物品共有的序列化字段；
///   2. 提供一个不关心 TData 的类型 —— 手上、背包格、商店列表这些
///      要「什么都能装」的地方用的就是它，剑也好药也好都塞得进来。
///
/// 注意这里刻意不继承 BaseDataSO。BaseDataSO 是「能挂到物体上、有血条」的
/// 实体配置基类，只有玩家 / 敌人才走那棵树。物品不是实体，接过去会凭空多出
/// maxHp / initialHp / atk / speed 一堆用不上的字段，每个物品资产都要填一遍。
/// </summary>
public abstract class ItemSO : ScriptableObject
{
    [Tooltip("物品唯一 id，背包 / 存档 / 掉落表按它索引")]
    public string id;

    public string itemName;

    public string description;

    [Tooltip("UI 图标")]
    public Sprite icon;
    [Tooltip("最大堆叠。1 = 不可堆叠（默认，忘配了就是这），填几就是每格最多几个")]
    public int stackCount = 1;
    public GameObject prefab;
    [Tooltip("要重写的状态机，动画不同需要添加")]
    public AnimatorOverrideController overrideController;

    /// <summary>
    /// 取一份运行时数据，返回最通用的 ItemData。
    /// 手上拿着 ItemSO（可能是剑、可能是药）时走这个；
    /// 要读武器专有的字段（combo 之类），自己 is 一下 WeaponData 再取。
    ///
    /// 为什么这里叫 CreateItemData，而下面泛型层那个叫 CreateData：
    /// 两者签名只差返回类型，本来最该用「协变返回」让它们重名，
    /// 但 Unity 的运行时不吃协变返回（CS8830: Target runtime doesn't support
    /// covariant return types），只能起两个名字。
    /// 泛型层用 CreateData 保住「确定类型时不用强转」这个好处，
    /// 这个通用入口退一步叫 CreateItemData。
    /// </summary>
    public abstract ItemData CreateItemData();
}

/// <summary>
/// 物品配置的中间层：把上面那层的字段拷进运行时数据。
/// 和 BaseDataSO&lt;TData&gt; 是同一个套路，只是各管各的字段
/// （两边字段集合不一样，所以这里的 CreateData 不能复用基类那份）。
///
/// 泛型留给子类填：WeaponSO 写成 ItemSO&lt;WeaponData&gt;，拿到的就是 WeaponData，
/// 不需要 is 检查，也不需要强转。
/// </summary>
public abstract class ItemSO<TData> : ItemSO where TData : ItemData, new()
{
    /// <summary>
    /// 拷一份运行时数据出来，返回类型就是 TData。
    /// 物品这一层的字段已经在这里填好，子类不要重写这个方法，
    /// 自己的字段加到 FillExtra 里去。
    /// </summary>
    public TData CreateData()
    {
        var data = new TData
        {
            id          = id,
            itemName    = itemName,
            description = description,
            icon        = icon,
            stackCount  = stackCount,
            prefab      = prefab,
            overrideController= overrideController
        };
        FillExtra(data);
        return data;
    }

    /// <summary>
    /// 基类那个不关心 TData 的入口，转调强类型的 CreateData。
    /// TData 一定是 ItemData 的子类，这里不需要强转。
    /// </summary>
    public override ItemData CreateItemData()
    {
        return CreateData();
    }

    /// <summary>
    /// 子类独有的字段在这里赋值。
    /// 基类那几个字段上面已经填过了，这里不要再填一遍。
    /// 如果又往下继承了一层，记得先调 base.FillExtra(data)。
    /// </summary>
    protected virtual void FillExtra(TData data) { }
}
