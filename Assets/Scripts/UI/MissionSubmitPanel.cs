using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MissionSubmitPanel : UIBase, IContainerOwner
{
    public MissionData mission;
    public PlayerObj player;

    public TMP_Text missionType;
    public TMP_Text missionName;
    public TMP_Text missionDescription;
    public TMP_Text owner;
    public TMP_Text speakerName;
    public Image headIcon;
    public GameObject speakerBadge;
    public TMP_Text statusText;
    public TMP_Text emptyRewardText;
    public Transform aimsParent;
    public GameObject aimPrefab;
    public ItemContainer reward;
    public Transform rewardSlotsPos;
    public GameObject itemSlotsPrefab;
    public ScrollRect contentScroll;
    public Button submitBtn;
    public Button cancelBtn;
    public Button closeBtn;
    public bool isAllCompleted = false;

    readonly List<MissionAim> aims = new List<MissionAim>();
    readonly List<ItemSlot> rewardSlots = new List<ItemSlot>();
    bool isGrantingRewards;

    protected override void Awake()
    {
        base.Awake();
        submitBtn.onClick.AddListener(SubmitMission);
        cancelBtn.onClick.AddListener(Hide);
        closeBtn.onClick.AddListener(Hide);
        StartHidden();
    }
    protected override void OnShow()
    {
        isAllCompleted = false;
    }
    public void ShowMissionPanel(MissionData missionData, PlayerObj missionPlayer)
    {
        if (missionData == null || missionData.SO == null) return;
        mission = missionData;
        player = missionPlayer;
        isAllCompleted = false;
        gameObject.SetActive(true);
        if (transform.parent != null) transform.parent.SetAsLastSibling();
        transform.SetAsLastSibling();
        Show();
        Refresh();
        Canvas.ForceUpdateCanvases();
        contentScroll.StopMovement();
        contentScroll.verticalNormalizedPosition = 1;
    }

    public override void Refresh()
    {
        if (mission == null || mission.SO == null) return;
        var config = mission.SO;
        missionType.text = config.missionType.ToString();
        missionName.text = config.missionName;
        missionDescription.text = config.missionDescription;
        owner.text = "交付地点 · " + config.owner;
        if (config.speakerInfo != null)
        {
            speakerName.text = config.speakerInfo.speakerName;
        }
        else
            speakerName.text ="委托人";
        headIcon.sprite = config.speakerInfo != null ? config.speakerInfo.head : null;
        headIcon.enabled = headIcon.sprite != null;
        speakerBadge.SetActive(headIcon.sprite == null);

        bool ready = mission.missionStatus == MissionStatu.待交付;
        statusText.text = ready ? "目标已达成 · 可以交付委托" : "目标尚未完成 · 请继续完成委托";
        statusText.color = ready ? new Color(0.65f, 0.8f, 0.57f) : new Color(0.86f, 0.75f, 0.52f);
        submitBtn.interactable = ready;

        int aimCount = config.requireMents.Count;
        while (aims.Count < aimCount)
            aims.Add(Instantiate(aimPrefab, aimsParent).GetComponent<MissionAim>());
        for (int i = 0; i < aims.Count; i++)
        {
            aims[i].gameObject.SetActive(i < aimCount);
            if (i < aimCount) aims[i].Bind(mission, i);
        }

        RefreshRewardSlots();
    }

    public void RefreshRewardSlots()
    {
        var items = mission != null ? mission.reward : null;
        reward = items != null ? new ItemContainer(items) : null;
        int rewardCount = items != null ? items.Count : 0;
        while (rewardSlots.Count < rewardCount)
        {
            var slot = Instantiate(itemSlotsPrefab, rewardSlotsPos).GetComponent<ItemSlot>();
            slot.GetComponentInChildren<TMP_Text>(true).font = missionName.font;
            rewardSlots.Add(slot);
        }
        bool hasRewards = false;
        for (int i = 0; i < rewardSlots.Count; i++)
        {
            rewardSlots[i].Bind(i < rewardCount ? items[i] : null, i, reward);
            bool visible = i < rewardCount && items[i] != null && items[i].count > 0;
            hasRewards |= visible;
            rewardSlots[i].gameObject.SetActive(visible);
            rewardSlots[i].enabled = false;
        }
        emptyRewardText.gameObject.SetActive(!hasRewards);
    }

    public IItemOperator GetContainer(ContainerKind kind = ContainerKind.Default)
    {
        return reward;
    }

    public void SubmitMission()
    {
        var manager = MissionManager.Instance;
        if (manager == null || player == null || manager.player != player
            || mission == null || mission.SO == null || mission.missionStatus != MissionStatu.待交付) return;
        if (!player.Data.missions.TryGetValue(mission.SO.missionID, out var current)
            || current != mission) return;

        isAllCompleted = GrantRewards();
        if (isAllCompleted && manager.CompleteMission(mission.SO.missionID))
        {
            Hide();
        }
    }

    public bool GrantRewards()
    {
        if (player == null || reward == null || isGrantingRewards) return false;

        isGrantingRewards = true;
        try
        {
            var source = reward;
            var list = source.GetItemList();
            for (int i = 0; i < list.Count; i++)
            {
                var item = list[i];
                if (item == null || item.count <= 0) continue;

                int added = player.AddItem(item.id, item.count);
                if (added > 0 && !source.RemoveItemAt(i, added)) return false;

                var remaining = list[i];
                if (remaining == null || remaining.count <= 0) continue;

                Vector3 position = player.transform.position + new Vector3( Random.Range(-0.2f, 0.2f), 0f, Random.Range(-0.2f, 0.2f));
                int remainingCount = remaining.count;
                var drop = DropItem.Spawn(remaining, position);
                if (drop == null) continue;
                if (!source.RemoveItemAt(i, remainingCount))
                {
                    drop.gameObject.SetActive(false);
                    Destroy(drop.gameObject);
                    return false;
                }
            }
            if (IsOpen) RefreshRewardSlots();
            return list.TrueForAll(item => item == null || item.count <= 0);
        }
        finally
        {
            isGrantingRewards = false;
        }
    }
}
