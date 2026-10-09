using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class MissionManager : MonoBehaviour
{
    public static MissionManager Instance=>instance;
    private static MissionManager instance;
    public PlayerObj player;
    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        if (player == null) player = FindFirstObjectByType<PlayerObj>();
    }

    private void OnEnable()
    {
        this.Subscribe(GameEvents.OnPushMissionProgress, OnReceivedNewProgress);
        this.Subscribe(GameEvents.OnPlayerReceiveMission, OnPlayerReceiveMission);
        this.Subscribe(GameEvents.OnPlayerSubmitMission, SubmitMission);
        this.Subscribe(GameEvents.OnPlayerTalkedToNPC, OnPlayerTalkedToNPC);
    }

    private void OnDisable()
    {
        this.UnSubscribe(GameEvents.OnPushMissionProgress, OnReceivedNewProgress);
        this.UnSubscribe(GameEvents.OnPlayerReceiveMission, OnPlayerReceiveMission);
        this.UnSubscribe(GameEvents.OnPlayerSubmitMission, SubmitMission);
        this.UnSubscribe(GameEvents.OnPlayerTalkedToNPC, OnPlayerTalkedToNPC);
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private void OnPlayerReceiveMission(PlayerData data, string missionID)
    {
        if (player == null || data != player.Data || string.IsNullOrEmpty(missionID)) return;
        if (data.missions.ContainsKey(missionID) || data.completedMissions.ContainsKey(missionID)) return;
        if (GameManager.Instance == null || UIManager.Instance == null) return;
        var config = GameManager.Instance.GetMission(missionID);
        if (config == null) return;
        var panel = UIManager.Instance.Get<MissionReceivePanel>();
        if (panel == null) return;
        panel.ShowMissionPanel(config);
    }

    private void OnPlayerTalkedToNPC(string npcID)
    {
        if (player == null || string.IsNullOrEmpty(npcID)) return;
        OnReceivedNewProgress(player.Data, RequirementType.与人对话, npcID, 1);
    }

    public void SubmitMission(string missionID)
    {
        if (player == null || string.IsNullOrEmpty(missionID)) return;
        var data = player.Data;
        if (data.completedMissions.ContainsKey(missionID)) return;
        if (!data.missions.TryGetValue(missionID, out var mission)) return;
        if (mission.missionStatus != MissionStatu.待交付) return;

        if (UIManager.Instance == null) return;
        var panel = UIManager.Instance.Get<MissionSubmitPanel>();
        if (panel == null) return;
        panel.ShowMissionPanel(mission, player);
    }
    // 奖励全部发出后，才把任务移入完成记录。
    public bool CompleteMission(string missionID)
    {
        if (player == null || string.IsNullOrEmpty(missionID)) return false;
        var data = player.Data;
        if (data == null || data.completedMissions.ContainsKey(missionID)) return false;
        if (!data.missions.TryGetValue(missionID, out var mission)) return false;
        if (mission.missionStatus != MissionStatu.待交付
            || !mission.reward.TrueForAll(item => item == null || item.count <= 0)) return false;

        mission.missionStatus = MissionStatu.已完成;
        data.missions.Remove(missionID);
        data.completedMissions.Add(missionID, true);
        this.Publish(GameEvents.OnMissionsChanged, data);
        return true;
    }
    private void OnReceivedNewProgress( PlayerData data, RequirementType type, string targetID, int count)
    {
        bool anyMissionChanged = false;
        if (player == null || data == null || data != player.Data || count <= 0) return;

        foreach (var missiondata in data.missions.Values)
        {
            if (missiondata.missionStatus != MissionStatu.进行中) continue;
            var requireList = missiondata.SO.requireMents;

            for (int j = 0; j < requireList.Count; j++)
            {
                var require = requireList[j];

                if (require.type != type)
                    continue;

                if (targetID == require.targetID)
                {
                    missiondata.currentNums[j] += count;
                    anyMissionChanged = true;

                    if (missiondata.currentNums[j] >= require.requireNum)
                    {
                        missiondata.currentNums[j] = require.requireNum;

                        if (isAllAimCompleted(requireList, missiondata.currentNums))
                        {
                            missiondata.missionStatus = MissionStatu.待交付;
                            //交任务才发奖励和操作字典

                            print(missiondata.SO.missionName + "待交付");
                            anyMissionChanged = true;
                            
                        }
                    }
                }
            }
        }
        if(anyMissionChanged) this.Publish<PlayerData>(GameEvents.OnMissionsChanged, data);
    }
    private bool isAllAimCompleted(List<MisssionRequireEntry> targetList, int[] nowProgress)
    {
        for(int i = 0; i < targetList.Count; i++)
        {
            if (targetList[i].requireNum > nowProgress[i])
            {
                return false;
            }
        }
        return true;
    }
    public void ReceiveMission(string missionID)
    {
        if (player == null || string.IsNullOrEmpty(missionID) || GameManager.Instance == null) return;
        if (player.Data.missions.ContainsKey(missionID)
            || player.Data.completedMissions.ContainsKey(missionID)) return;
        var config = GameManager.Instance.GetMission(missionID);
        if (config == null) return;
        MissionData data = new MissionData(config);

        player.Data.missions.Add(missionID, data);
        this.Publish<PlayerData>(GameEvents.OnMissionsChanged, player.Data);
    }
}
