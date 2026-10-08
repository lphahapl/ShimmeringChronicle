using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MissionReceivePanel : UIBase
{
    MissionSO SO;
    /// <summary>
    /// 任务所属地点，势力
    /// </summary>
    public TMP_Text Owner;
    public Image headIcon;
    public TMP_Text delegateOwnerName;
    public TMP_Text missionType;
    public TMP_Text missionName;
    public TMP_Text missionDescription;
    public List<GameObject> aims = new List<GameObject>();//目标
    public GameObject aimPrefab;
    public Transform aimsParent;
    public ItemReviewer reward;
    [SerializeField] List<ItemSlot> rewardSlots = new List<ItemSlot>();
    public GameObject itemSlotsPrefab;
    public Transform rewardSlotsPos;
    public Button ReceiveBtn;
    public Button closeBtn;

    protected override void Awake()
    {
        base.Awake();
        ReceiveBtn.onClick.AddListener(OnReceiveMission);
        closeBtn.onClick.AddListener(Hide);
    }

    private void OnReceiveMission()
    {
        if (SO == null) return;
        MissionManager.Instance.ReceiveMission(SO.missionID);
        Hide();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
    public void OnDrawPanel(MissionSO SO)
    {
        this.SO=SO;
        if (SO==null) { return; }
        var previewData = new MissionData(SO);
        reward = new ItemReviewer(previewData.reward);
        Owner.text = "来自" + SO.owner.ToString();
        headIcon.sprite = null;
        delegateOwnerName.text = "";
        if (SO.speakerInfo != null)
        {
            headIcon.sprite = SO.speakerInfo.head;
            delegateOwnerName.text = SO.speakerInfo.speakerName;
        }
        headIcon.enabled = headIcon.sprite != null;
        missionType.text=SO.missionType.ToString();
        missionName.text=SO.missionName;
        missionDescription.text=SO.missionDescription;
        int count = SO.requireMents.Count;
        while (aims.Count < count)
        {
            aims.Add(Instantiate(aimPrefab, aimsParent));
        }
        for (int i = 0; i < aims.Count; i++)
        {
            aims[i].SetActive(i < count);
            if (i < count) aims[i].GetComponent<MissionAim>().Bind(previewData, i);
        }
        RefreshRewardSlots(SO);
    }
    public void RefreshRewardSlots(MissionSO SO)
    {
        var items = reward.GetItemList();
        int count = items != null ? items.Count : 0;

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
    public void ShowMissionPanel(MissionSO SO)
    {
        if (SO == null) return;
        OnDrawPanel(SO);
        if (transform.parent != null) transform.parent.SetAsLastSibling();
        transform.SetAsLastSibling();
        this.Show();
    }
    
}
