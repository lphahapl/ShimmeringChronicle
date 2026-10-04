using UnityEngine;

/// <summary>
/// 物品的运行时数据。每件实例一份，由 ItemSO.CreateData() 深拷贝出来，
/// 不是指向配置的引用 —— 强化、耐久这类「这一件自己的数值」写在这里。
/// 字段和 ItemSO 是镜像的：SO 上每加一个配置字段，这里都要跟着加，
/// 漏一个就是默认同源。
/// </summary>
public class ItemData
{
    public string id;
    public string itemName;
    public string description;
    public Sprite icon;
    public GameObject prefab;
    public AnimatorOverrideController overrideController;
    /// <summary>
    /// 每格最多堆几个。由 ItemSO.stackCount 填进来，1 = 不可堆叠。
    /// count 到了这个数就塞不下了，再多得开新格。
    /// </summary>
    public int stackCount;

    /// <summary>
    /// 当前堆了几个。开局从配置侧的 ItemSlotData.num 填进来，之后是纯运行时的 ——
    /// 捡东西 count++、用一个 count--，都不会回写配置。
    /// 一叠就是一份 ItemData —— 捡到同 id 的东西是找现成的堆加，不是往列表里再塞几个实例。
    /// </summary>
    public int count = 1;

    /// <summary>
    /// 复制一份独立的数据。
    /// 拆分拖拽（一叠分一半）时必须用它 —— 直接赋值两格会指向同一个对象，
    /// 改一格的数量另一格跟着变。
    ///
    /// MemberwiseClone 按实际类型 new，所以 WeaponData 出来还是 WeaponData；
    /// 子类有自己的引用类型字段（WeaponData.combo）就 override，先 base.Clone() 再补自己那层。
    /// </summary>
    public virtual ItemData Clone()
    {
        return (ItemData)MemberwiseClone();
    }
}
