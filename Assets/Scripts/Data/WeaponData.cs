using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 武器的运行时数据。combo 是从 WeaponSO 拷来的（逐个 Clone）；
/// 耐久、弹药这类「这一把自己的状态」也加在这里。
/// 有了实例状态就不能堆叠了 —— 堆叠等于 count 件共用这一份数据。
/// </summary>
public class WeaponData : ItemData
{
    public List<AttackData> combo = new List<AttackData>();

    public float stopDistance;
    public float attackBreak = 0.8f;
    /// <summary>
    /// 补上 combo 的深拷贝。
    /// base 用的是 MemberwiseClone，所以这里拿到的确实是 WeaponData；
    /// 但那次浅拷贝让 copy.combo 还指着原来那个 List，必须换成新的。
    /// 以后再加引用类型字段，也在这里补。
    /// </summary>
    public override ItemData Clone()
    {
        var copy = (WeaponData)base.Clone();

        copy.combo = new List<AttackData>();
        if (combo == null) return copy;

        for (int i = 0; i < combo.Count; i++)
            copy.combo.Add(combo[i] == null ? null : combo[i].Clone());

        return copy;
    }
}

/// <summary>
/// 单个攻击段的运行时数据。由 WeaponSO 的连招配置拷贝而来。
/// </summary>
[System.Serializable]
public class AttackData
{
    public float damagePerHit;
    public float radius;
    public float angle;
    [Header("卡肉优先级")]
    public ETimePriority priority;
    [Header("卡肉时间缩放")]
    public float scale;
    [Header("卡肉时长")]
    public float time;
    
    /// <summary>判定偏移</summary>
    public Vector3 judgeOffset;

    /// <summary>
    /// 引用类型，SO 和运行时数据不能共用同一实例，
    /// 否则改 damagePerHit 等于直接改配置资产。CreateData 时逐个 Clone。
    /// </summary>
    public AttackData Clone()
    {
        return new AttackData
        {
            damagePerHit = damagePerHit,
            radius       = radius,
            judgeOffset  = judgeOffset,
            angle = angle,
            priority = priority,
            scale = scale,
            time = time
        };
    }
}
