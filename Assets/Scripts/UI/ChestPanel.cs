using System.Collections.Generic;
using UnityEngine;

public class ChestPanel : UIBase
{
    public bool isAnyChestPanelActive => CheckAnyActive();
    int panelNum;
    public int currentUseIndex = 0;
    public List<ChestWindow> Panels;
    protected override void Awake()
    {
        base.Awake();
        base.Refresh();
        panelNum = Panels.Count;
        foreach (var p in Panels)
            p.gameObject.SetActive(false);
        this.Subscribe(GameEvents.OnChestShow, ShowPanel);
        this.Subscribe(GameEvents.OnChestHide, HidePanel);
    }

    public void ShowPanel(IContainerOwner owner)
    {
        if (panelNum == 0) return;
        if (currentUseIndex < panelNum)
        {
            var panel = Panels[currentUseIndex];
            
            if (!ReferenceEquals(panel.ContainerOwner, owner) &&
                panel.ContainerOwner is TestChest oldChest && oldChest.IsOpen)
                oldChest.Close();
            panel.ContainerOwner = owner;
            var container = owner.GetContainer();
            Panels[currentUseIndex].Bind(container);
            Panels[currentUseIndex].gameObject.SetActive(true);
            Panels[currentUseIndex].transform.SetAsLastSibling();
            currentUseIndex++;
        }
        else
        {
            currentUseIndex = 0;
            ShowPanel(owner);
        }
    }

    public void HidePanel(IContainerOwner owner)
    {
        foreach (var panel in Panels)
            if (panel != null && ReferenceEquals(panel.ContainerOwner, owner))
                panel.gameObject.SetActive(false);
    }

    private bool CheckAnyActive()
    {
        foreach (var p in Panels)
            if (p != null && p.isActiveAndEnabled) return true;
        return false;
    }

    protected override void OnDestroy()
    {
        this.UnSubscribe(GameEvents.OnChestShow, ShowPanel);
        this.UnSubscribe(GameEvents.OnChestHide, HidePanel);
        base.OnDestroy();
    }
    public override bool BlocksQuickBarScroll
    {
        get
        {
            if (!isActiveAndEnabled || Panels == null) return false;
            foreach (var panel in Panels)
                if (panel != null && panel.BlocksQuickBarScroll) return true;
            return false;
        }
    }
    
}
