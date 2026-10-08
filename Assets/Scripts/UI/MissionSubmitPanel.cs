using UnityEngine;

public class MissionSubmitPanel : UIBase
{
    public MissionData mission;
    public PlayerObj player;

    protected override void Awake()
    {
        base.Awake();
        StartHidden();
    }

    public void ShowMissionPanel(MissionData missionData, PlayerObj missionPlayer)
    {
        mission = missionData;
        player = missionPlayer;
        gameObject.SetActive(true);
        transform.SetAsLastSibling();
        Show();
        Refresh();
    }

    public override void Refresh()
    {
        // TODO: 显示任务信息和奖励预览。
    }

    public void SubmitMission()
    {
        // TODO: 确认交任务；奖励发放成功后，再更新玩家的任务记录并关闭面板。
    }

    public void GrantRewards()
    {
        // TODO: 在这里发放 mission.reward，处理背包空间不足。
    }
}
