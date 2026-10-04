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
        this.Subscribe<PlayerData, RequirementType, string, int>(GameEvents.OnPushMissionProgress, OnReceivedNewProgress);
    }
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    private void OnReceivedNewProgress( PlayerData data, RequirementType type, string targetID, int count)
    {
        bool anyMissionChanged = false;
        if (data != player.Data) return;

        foreach (var missiondata in data.missions.Values)
        {
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
                            missiondata.missionStatus = MissionStatu.ÒÑÍê³É;
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
    public void ReceiveMission(string missionID, MissionData data)
    {
        if (player.Data.missions.ContainsKey(missionID)
            || player.Data.completedMissions.ContainsKey(missionID)) return;

        player.Data.missions.Add(missionID, data);
        this.Publish<PlayerData>(GameEvents.OnMissionsChanged, player.Data);
    }
}
