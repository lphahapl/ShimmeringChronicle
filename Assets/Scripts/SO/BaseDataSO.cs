using UnityEngine;

/// <summary>
/// 配置基类（非泛型根）。
/// 为什么需要它：BaseData 要持有配置引用，而泛型类不给类型参数就没法当字段类型用，
/// 所以必须有一个非泛型的根来当那个类型。
/// 里面只放「运行时不会变」的配置。
/// </summary>
public abstract class BaseDataSO : ScriptableObject
{
    public string targetName;
    public string description;
    public float maxHp;
    public float initialHp;   // 初始值，不是当前值
    public float atk;
    public float speed;

    void OnValidate()
    {
        maxHp = Mathf.Max(0f, maxHp);
        initialHp = Mathf.Clamp(initialHp, 0f, maxHp);   // 开局血量不能超过上限
    }
}

/// <summary>
/// 泛型中间层：声明「这份配置能造出哪种运行时数据」。
///
/// 泛型参数放在类上，不放在方法上 —— 这是关键：
///   放在方法上，T 由调用方指定，调用方就能写错，而且写错只在运行时炸；
///   放在类上，T 由子类声明固定，写错编译不过。
/// </summary>
public abstract class BaseDataSO<TData> : BaseDataSO where TData : BaseData, new()
{
    /// <summary>
    /// 造一份运行时数据。封死，子类不要覆盖 —— 公共字段的复制只写在这一处，
    /// 子类要填自己新增的字段请重写 FillExtra。
    /// </summary>
    public TData CreateData()
    {
        var data = new TData
        {
            targetName  = targetName,
            description = description,
            maxHp       = maxHp,
            hp          = initialHp,   
            atk         = atk,
            speed       = speed,
        };
        FillExtra(data);
        return data;
    }

   
    protected virtual void FillExtra(TData data) { }
}
