using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "WeaponConfig", menuName = "Configs/Item/WeaponSO")]
public class WeaponSO : ItemSO<WeaponData>
{
    /// <summary>连招配置，每个攻击段是一个元素</summary>
    public List<AttackData> combo = new List<AttackData>();
    /// <summary>
    /// 敌人用
    /// </summary>
    public float stopDistance;
    public float attackBreak=0.8f;
    /// <summary>
    /// 武器独有的字段在这里赋值。
    /// id / itemName / description / icon 已经在 ItemSO&lt;TData&gt;.CreateData 里填好了。
    /// </summary>
    protected override void FillExtra(WeaponData data)
    {
        data.stopDistance = stopDistance;
        data.attackBreak = attackBreak;
        if (combo == null) return;

        // 逐个 Clone：AttackData 是引用类型，直接把 SO 上的实例塞进运行时数据，
        // 之后运行时改 damagePerHit 等于把配置资产改了（还会被存档带出去）。
        for (int i = 0; i < combo.Count; i++)
            data.combo.Add(combo[i].Clone());
    }
}
