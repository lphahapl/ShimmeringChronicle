using System.Collections.Generic;
using UnityEngine;
[CreateAssetMenu(fileName = "MissionConfig", menuName = "Configs/mission/MissionSO")]
public class MissionSO : ScriptableObject
{
    public string missionID;
    public string missionName;
    [TextArea] public string missionDescription;
    public MissionType missionType;
    public DialogSO dialog;//说话者，应该在接取任务的时候触发
    public List<MisssionRequireEntry> requireMents;
    public List<ItemSlotData> reward;
}
/// <summary>
/// 任务manager或者playerobj应该监听对应类型的事件
/// 击败敌人应该监听敌人死亡的事件（发布攻击者和受害者那个）
/// 收集物品应监听玩家背包和快捷栏变动，注意过滤这两个容器相互交换
/// 与人对话，对话时也应该发布说话的两个人
/// </summary>
 [System.Serializable]
public class MisssionRequireEntry
{
    public string missionDetail;//任务描述
    public RequirementType type;
    public string targetID;

    [Min(1)]
    public int requireNum = 1;
}
public enum MissionType
{
    主线任务,
    支线任务
}
public enum RequirementType
{
    到达地点,
    击败敌人,
    收集物品,
    与人对话
}
