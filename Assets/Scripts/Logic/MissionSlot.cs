using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 挂在单个任务格子预制体上
/// 它会看自己是否被选中来在任务栏主要界面显示自己的信息
/// </summary>

public class MissionSlot : MonoBehaviour,IPointerClickHandler
{
    public MissionData data;
    public TMP_Text sideMissionType;
    public TMP_Text sideTitle;
    public TMP_Text sideStatu;
    public bool isSelected;
    public MissionsBar parent;
    public GameObject selectionMarker;
    

    public void Init(MissionsBar parent)
    {
        this.parent = parent;
        Refresh();
    }


    void Start()
    {
        if (parent == null) parent = GetComponentInParent<MissionsBar>();
    
        Refresh();
    }

   
    void Update()
    {
        
    }
    private void DrawSideInfo()
    {
        if (data == null) return;
        sideMissionType.text=data.SO.missionType.ToString();
        sideTitle.text=data.SO.missionName;
        sideStatu.text=data.missionStatus.ToString();
    }
    public void Refresh()
    {
        DrawSideInfo();
        if (selectionMarker != null) selectionMarker.SetActive(isSelected);
        if (isSelected) DrawMainBar();
    }

    public void Select()
    {
        if (data == null) return;
        DrawMainBar();
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;
        if (selectionMarker != null) selectionMarker.SetActive(selected);
    }

    

    private void DrawMainBar()
    {
        if (parent != null)
        {
            parent.DrawMainPos(this);
        }
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button != PointerEventData.InputButton.Left) return;
        Select();
    }
}
