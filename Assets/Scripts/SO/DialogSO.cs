
using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Configs/Dialog")]
public class DialogSO : ScriptableObject
{
    public string dialogID;
    public string startNodeID;
    public string receivedStartNodeID;
    public string completedStartNodeID;
    public List<DialogNode> nodes = new();
}

[Serializable]
public class DialogNode
{
    public string nodeID;
    public SpeakerInfo speaker;
    [TextArea] public string content;

    // 没有选项时，点击继续前往这个节点。
    // 为空表示结束。
    public string nextNodeID;

    // 有选项时，等待玩家选择。
    public List<DialogChoice> choices = new List<DialogChoice>();
}

[Serializable]

public class DialogChoice
{
    public string choiceID;     // 选项 ID
    [TextArea] public string text;         // 按钮显示的文字
    public string targetNodeID; // 下一节点，空表示结束

    // 玩家选择这一项时执行
    public List<DialogActionData> actions = new();
}



[Serializable]
public class DialogActionData
{
    public DialogActionType type;

   //脚本应该根据这个值来执行相应逻辑
    public string targetID;
}
public enum DialogActionType
{
    ReceiveMission,    // 接取任务
    ReportTalkProgressAndEnd, // 上报对话任务进度
    SubmitMission // 打开交任务面板

}
