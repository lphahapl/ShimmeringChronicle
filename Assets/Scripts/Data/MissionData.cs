using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 每任务的运行时数据
/// </summary>
public class MissionData
{
    public MissionSO SO;
   
    public MissionStatu missionStatus;
    public int[] currentNums;
    public List<ItemData> reward;
    public MissionData (MissionSO SO)
    {
        this.SO = SO;
        currentNums = new int[SO.requireMents.Count];
        missionStatus = MissionStatu.进行中;
        reward=SO.reward.ToItemDataList();
        
    }
}
public enum MissionStatu
{
    待接取,
    进行中,
    待交付,
    已完成
}
