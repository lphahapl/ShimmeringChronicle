/// <summary>
/// 运行时数据基类。每个实例一份，可以随便改。
/// 字段和 BaseDataSO 是「镜像」的 —— 这是已知的待办：
/// 全部抄一份的代价是每加一个配置字段都要改两处，漏一处就是静默不同步。
/// </summary>
public class BaseData
{
    public string targetName;
    public string description;
    public float maxHp;
    public float hp;          // 当前血量（会变）
    public float atk;
    public float speed;
}
