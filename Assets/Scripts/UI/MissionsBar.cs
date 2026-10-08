using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MissionsBar : UIBase,IContainerOwner
{
    public List<MissionSlot> slots = new List<MissionSlot>();
    public List<MissionSlot> finishedSlots = new List<MissionSlot>();

    public TMP_Text missionType;
    public TMP_Text missionName;
    public TMP_Text missionDescription;
    public List<GameObject> aims = new List<GameObject>();//目标
    public GameObject aimPrefab;
    public Transform aimsParent;
    private MissionSlot selectedSlot;
    public PlayerObj player;
    public GameObject slotPrefab;
    public Transform slotsParent;
    //To Do
    public ItemContainer reward;
    [SerializeField] List<ItemSlot> rewardSlots = new List<ItemSlot>();
    public GameObject itemSlotsPrefab;
    public Transform rewardSlotsPos;
    public TMP_Text progressingNum;
    public TMP_Text completedNum;
    public Button closeBtn;
    public bool isUnFnishedMission = false;


    protected override void Awake()
    {
        base.Awake();
        if (player == null) player = FindFirstObjectByType<PlayerObj>();
        this.Subscribe(GameEvents.OnMissionsChanged, OnMissionsChanged);
        closeBtn.onClick.AddListener(() => { Hide(); });
    }

    protected override void OnDestroy()
    {
        this.UnSubscribe(GameEvents.OnMissionsChanged, OnMissionsChanged);
        base.OnDestroy();
    }

    private void OnMissionsChanged(PlayerData changed)
    {
        if (player == null || changed != player.Data) return;
        Refresh();
    }



    void Start()
    {
        Refresh();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void DrawMainPos(MissionSlot slot)
    {
      
        if (aims.Count == 0 && aimsParent != null)
            foreach (var aim in aimsParent.GetComponentsInChildren<MissionAim>(true))
                aims.Add(aim.gameObject);

        var displaySlots = slots;
        if (!isUnFnishedMission) displaySlots = finishedSlots;
        if (slot == null || slot.data == null)
        {
            selectedSlot = null;
            foreach (var other in displaySlots)
                if (other != null) other.SetSelected(false);
            missionType.text = "";
            missionName.text = "";
            missionDescription.text = "";
            foreach (var aim in aims) aim.SetActive(false);
            RefreshRewardSlots();
            return;
        }

        selectedSlot = slot;
        foreach (var other in displaySlots)
            if (other != null) other.SetSelected(other == slot);
        slot.SetSelected(true);

        var data = slot.data;
        missionType.text = data.SO.missionType.ToString();
        missionName.text = data.SO.missionName;
        missionDescription.text = data.SO.missionDescription;

        int count = data.SO.requireMents.Count;
        while (aims.Count < count)
            aims.Add(Instantiate(aimPrefab, aimsParent));

        for (int i = 0; i < aims.Count; i++)
        {
            aims[i].SetActive(i < count);
            if (i < count) aims[i].GetComponent<MissionAim>().Bind(data, i);
        }
        RefreshRewardSlots();
    }

    public void ShowMissions(bool showUnfinished)
    {
        isUnFnishedMission = showUnfinished;
        selectedSlot = null;
        Refresh();
    }

    public override void Refresh()
    {
        if (player == null || player.Data == null) return;
        if (slots.Count == 0 && finishedSlots.Count == 0 && slotsParent != null)
            slots.AddRange(slotsParent.GetComponentsInChildren<MissionSlot>(true));

        completedNum.text = "已完成" + player.Data.completedMissions.Count;
        progressingNum.text = "未完成" + player.Data.missions.Count;
        if (isUnFnishedMission)
        {
            HideSlots(finishedSlots);
            RefreshMissionList(slots, player.Data.missions.Values);
        }
        else
        {
            HideSlots(slots);
            RefreshMissionList(finishedSlots, GetCompletedMissions());
        }
    }

    private IEnumerable<MissionData> GetCompletedMissions()
    {
        var manager = GameManager.Instance;
        if (manager == null) yield break;
        // 已完成字典只存任务 ID，这里生成详情展示用的数据。
        foreach (string missionID in player.Data.completedMissions.Keys)
        {
            var config = manager.GetMission(missionID);
            if (config == null) continue;
            var mission = new MissionData(config);
            mission.missionStatus = MissionStatu.已完成;
            for (int i = 0; i < mission.currentNums.Length; i++)
                mission.currentNums[i] = config.requireMents[i].requireNum;
            yield return mission;
        }
    }

    private void RefreshMissionList(List<MissionSlot> displaySlots, IEnumerable<MissionData> missions)
    {
        string selectedID = null;
        if (selectedSlot != null && selectedSlot.data != null)
            selectedID = selectedSlot.data.SO.missionID;
        MissionSlot first = null;
        MissionSlot nextSelected = null;
        int index = 0;
        foreach (var mission in missions)
        {
            if (mission == null) continue;
            if (index >= displaySlots.Count)
                displaySlots.Add(Instantiate(slotPrefab, slotsParent).GetComponent<MissionSlot>());

            var slot = displaySlots[index];
            slot.SetSelected(false);
            slot.data = mission;
            slot.Init(this);
            slot.gameObject.SetActive(true);
            if (first == null) first = slot;
            if (mission.SO.missionID == selectedID) nextSelected = slot;
            index++;
        }
        for (int i = index; i < displaySlots.Count; i++)
        {
            displaySlots[i].SetSelected(false);
            displaySlots[i].data = null;
            displaySlots[i].gameObject.SetActive(false);
        }
        if (nextSelected == null) nextSelected = first;
        DrawMainPos(nextSelected);
    }

    private void HideSlots(List<MissionSlot> hiddenSlots)
    {
        foreach (var slot in hiddenSlots)
        {
            slot.SetSelected(false);
            slot.gameObject.SetActive(false);
        }
    }

    public void RefreshRewardSlots()
    {
        var items = selectedSlot != null && selectedSlot.data != null
            ? selectedSlot.data.reward : null;
        int count = items != null ? items.Count : 0;
        reward = items != null ? new ItemContainer(items) : null;

        while (rewardSlots.Count < count)
            rewardSlots.Add(Instantiate(itemSlotsPrefab, rewardSlotsPos).GetComponent<ItemSlot>());

        for (int i = 0; i < rewardSlots.Count; i++)
        {
            if (i < count)
                rewardSlots[i].Bind(items[i], i, reward);
            else
                rewardSlots[i].Bind(null, i, null);

            rewardSlots[i].gameObject.SetActive(i < count);
        }
    }
    protected override void OnShow()
    {
        Refresh();
    }

    public IItemOperator GetContainer(ContainerKind kind = ContainerKind.Default)
    {
        return reward;
    }
}
