using NUnit.Framework;
using System.Collections.Generic;
using Unity.Android.Gradle.Manifest;
using UnityEngine;
using UnityEngine.Rendering;

public class NpcObj : InteractBase
{
    public MissionSO SO;
    public DialogSO dialog;
    public string currentID;
    public bool isReceivedMission;
    public bool isCompletedMission;
    public DialogueUI LDialogBox;
    public DialogueUI RDialogBox;
    public DialogueUI nowDialogBox;
    public bool isTalking;
    public PlayerObj player;
    public List<GameObject>btnChioces=new List<GameObject>();
 
    private readonly Dictionary<string, DialogNode> nodeMap = new();
    public DialogNode nowNode= new DialogNode();


    protected override void Awake()
    {
        base.Awake();
    }

    private void OnEnable()
    {
        this.Subscribe(GameEvents.OnDialogueContinueRequested, ToStep);
        this.Subscribe(GameEvents.OnDialogueChoiceSelected, ToStep);
    }

    private void OnDisable()
    {
        this.UnSubscribe(GameEvents.OnDialogueContinueRequested, ToStep);
        this.UnSubscribe(GameEvents.OnDialogueChoiceSelected, ToStep);
        if (isTalking) EndTalk();
    }

    void Start()
    {
        BuildNodeIndex();
    }

    
    void Update()
    {
        
    }
    protected override bool InteractLogic(GameObject interactor)
    {
        PlayerObj player = interactor.GetComponent<PlayerObj>();
        if(player==null|| dialog==null) return false;
        this.player = player;
        CheckMissionStatus();
        StartTalk(isReceivedMission, isCompletedMission);
        return true;
    }
  
    private void RefreshDialogePanel(DialogNode nodeData)
    {
        if (nodeData.speaker == null || nodeData.speaker.SpeakerType != ESpeakerType.Player)
        {
            nowDialogBox = RDialogBox;
            RDialogBox.gameObject.SetActive(true);
            LDialogBox.gameObject.SetActive(false);
        }
        else
        {
            nowDialogBox = LDialogBox;
            RDialogBox.gameObject.SetActive(false);
            LDialogBox.gameObject.SetActive(true);
        }
        nowDialogBox.BindData(nodeData,player);
    }
    private void CheckMissionStatus()
    {
        isReceivedMission = false;
        isCompletedMission = false;
        if (player == null || player.Data == null || SO == null || string.IsNullOrEmpty(SO.missionID)) return;

        var data = player.Data;
        if (data.completedMissions.ContainsKey(SO.missionID))
        {
            isReceivedMission = true;
            isCompletedMission = true;
            return;
        }
        if (data.missions.TryGetValue(SO.missionID, out var mission))
        {
            isReceivedMission = true;
            isCompletedMission = mission.missionStatus == MissionStatu.已完成;
        }
    }

    /// <summary>根据接取和完成情况显示不同的对话。</summary>
    public void StartTalk(bool isReceived, bool isCompleted)
    {
        string startID = "";
        if (dialog != null)
        {
            startID = dialog.startNodeID;
            if (isCompleted)
                startID = dialog.completedStartNodeID;
            else if (isReceived)
                startID = dialog.receivedStartNodeID;
        }
        if (string.IsNullOrEmpty(startID) || !nodeMap.TryGetValue(startID, out nowNode))
        {
            Debug.LogError("找不到对话起始节点", this);
            EndTalk();
            return;
        }

        currentID = startID;
        isTalking = true;
        this.Publish(GameEvents.OnPlayerTalk, player, true);
        RefreshDialogePanel(nowNode);
    }

    public void ToStep(DialogueUI dialogueUI, string nextStep)
    {
        if (!isTalking || dialogueUI != nowDialogBox) return;
        if (string.IsNullOrEmpty(nextStep))
        {
            EndTalk();
            return;
        }
        if (!nodeMap.TryGetValue(nextStep, out nowNode))
        {
            Debug.LogError($"找不到对话节点：{nextStep}", this);
            EndTalk();
            return;
        }

        currentID = nextStep;
        RefreshDialogePanel(nowNode);
    }

    public void EndTalk()
    {
        if (LDialogBox != null) LDialogBox.gameObject.SetActive(false);
        if (RDialogBox != null) RDialogBox.gameObject.SetActive(false);
        nowDialogBox = null;
        nowNode = null;
        currentID = "";
        if (!isTalking) return;
        isTalking = false;
        this.Publish(GameEvents.OnPlayerTalk, player, false);
    }

    private void BuildNodeIndex()
    {
        nodeMap.Clear();
        if (dialog == null) return;

        foreach (DialogNode node in dialog.nodes)
        {
            // nodeID 必须非空且唯一；重复 ID 会在这里报错。
            nodeMap.Add(node.nodeID, node);
        }
    }
  
}
