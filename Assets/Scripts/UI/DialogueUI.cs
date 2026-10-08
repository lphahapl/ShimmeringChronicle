using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class DialogueUI : MonoBehaviour
{
    public Image portraitImage;
    public GameObject portraitPlaceholder;
    public TMP_Text speakerNameText;
    public TMP_Text descriptionText;
    public TMP_Text content;
    public GameObject buttonPrefab;
    public Transform btnPos;
    public ScrollRect choicesScroll;
    public bool isNoSelection = false;
    private PlayerObj playerObj;
    [Min(64)] public float maxChoicesHeight = 320f;

    private readonly List<DialogueChoiceButton> buttons = new();
    private readonly List<DialogChoice> choices = new();
    private DialogNode currentNode;
    private bool selectionSubmitted;

    private void OnEnable()
    {
        this.Subscribe(GameEvents.OnPlayerContinue, Continue);
        RefreshChoicesLayout();
    }

    private void OnDisable()
    {
        this.UnSubscribe(GameEvents.OnPlayerContinue, Continue);
    }

    public void BindData(DialogNode dialogData,PlayerObj player)
    {
        playerObj = player;
        currentNode = dialogData;
        selectionSubmitted = false;
        choices.Clear();
        if (dialogData == null)
        {
            BindSpeaker(null);
            content.text = "";
            CreateBtn(0);
            return;
        }

        BindSpeaker(dialogData.speaker);
        content.text = dialogData.content;
        if (dialogData.choices != null)
        {
            foreach (var choice in dialogData.choices)
                if (choice != null) choices.Add(choice);
        }

        CreateBtn(Mathf.Max(0, choices.Count));
      
            for (int i = 0; i < choices.Count; i++)
            {
                var choice = choices[i];
                buttons[i].Label.text = choice.text;
                buttons[i].onClick.AddListener(() => SelectChoice(choice));
            }
        
        RefreshChoicesLayout();
    }

    private void BindSpeaker(SpeakerInfo speaker)
    {
        portraitImage.sprite = null;
        speakerNameText.text = "";
        descriptionText.text = "";
        if (speaker != null)
        {
            portraitImage.sprite = speaker.head;
            speakerNameText.text = speaker.speakerName;
            descriptionText.text = speaker.description;
        }
        portraitImage.enabled = portraitImage.sprite != null;
        portraitPlaceholder.SetActive(currentNode != null && !portraitImage.enabled);
    }

    /// <summary>在 btnPos 下保持 num 个可见按钮；多余按钮隐藏并留作复用。</summary>
    public void CreateBtn(int num)
    {
        num = Mathf.Max(0, num);
        while (buttons.Count < num)
        {
            var instance = Instantiate(buttonPrefab, btnPos);
            buttons.Add(instance.GetComponent<DialogueChoiceButton>());
        }
        isNoSelection = num == 0;
        for (int i = 0; i < buttons.Count; i++)
        {
            var button = buttons[i];
            button.onClick.RemoveAllListeners();
            button.interactable = !selectionSubmitted;
            button.gameObject.SetActive(i < num);
        }
        choicesScroll.gameObject.SetActive(num > 0);
        RefreshChoicesLayout();
    }

    private void SelectChoice(DialogChoice choice)
    {
        if (currentNode == null || selectionSubmitted) return;
        LockButtons();
        if (choice.actions.Count != 0)
        {
            foreach(var action in choice.actions)
            {
                switch (action.type)
                {
                    case DialogActionType.ReceiveMission:
                        this.Publish(GameEvents.OnPlayerReceiveMission, playerObj.Data,action.targetID);
                        break;
                    case DialogActionType.ReportTalkProgressAndEnd:
                        this.Publish<PlayerData,RequirementType,string,int>(GameEvents.OnPushMissionProgress,playerObj.Data,RequirementType.与人对话,action.targetID,1);
                        break;
                    case DialogActionType.SubmitMission:
                        this.Publish(GameEvents.OnPlayerSubmitMission, action.targetID);
                        break;
                    default:
                        break;
                }
            }
        }
        this.Publish(GameEvents.OnDialogueChoiceSelected, this, choice.targetNodeID);
    }

    private void Continue()
    {
        if(!isNoSelection) return;
        if (currentNode == null || selectionSubmitted) return;
        string nextNodeID = currentNode.nextNodeID;
        if (nextNodeID == null) nextNodeID = "";
        LockButtons();
        this.Publish(GameEvents.OnDialogueContinueRequested, this, nextNodeID);
        
    }

    private void LockButtons()
    {
        selectionSubmitted = true;
        foreach (var button in buttons) button.interactable = false;
    }

    private void RefreshChoicesLayout()
    {
        if (btnPos is not RectTransform contentRect || !btnPos.gameObject.activeInHierarchy) return;
        LayoutRebuilder.ForceRebuildLayoutImmediate(contentRect);
        if (choicesScroll == null) return;

        float preferredHeight = LayoutUtility.GetPreferredHeight(contentRect);
        float limit = Mathf.Max(64, maxChoicesHeight);
        var scrollRect = (RectTransform)choicesScroll.transform;
        scrollRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, Mathf.Min(limit, preferredHeight));
        choicesScroll.vertical = preferredHeight > limit;
        LayoutRebuilder.ForceRebuildLayoutImmediate(scrollRect);
        choicesScroll.StopMovement();
        choicesScroll.verticalNormalizedPosition = 1f;
    }
    
}
