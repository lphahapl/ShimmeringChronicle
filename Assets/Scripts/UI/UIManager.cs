using System;
using System.Collections.Generic;
using UnityEngine;

public class UIManager : MonoBehaviour
{
    static UIManager _instance;

    /// <summary>没准备好时返回 null，调用方判空。刻意不做懒创建 —— 那会在退出播放时凭空造出新对象。</summary>
    public static UIManager Instance => _instance;

    readonly Dictionary<Type, UIBase> _panels = new Dictionary<Type, UIBase>();

    void Awake()
    {
        if (_instance != null && _instance != this)
        {
            Debug.LogError("[UIManager] 场景里有多个 UIManager，只保留一个", this);
            Destroy(gameObject);
            return;
        }
        _instance = this;
        CollectAll();
    }

    void OnDestroy() { if (_instance == this) _instance = null; }

    void CollectAll()
    {
        var all = FindObjectsByType<UIBase>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var ui in all) Register(ui);
    }

    public void Register(UIBase ui)
    {
        if (ui == null) return;

        Type t = ui.GetType();
        if (_panels.TryGetValue(t, out var old) && old != null && old != ui)
            Debug.LogError($"[UIManager] {t.Name} 注册了两次，后来的覆盖先前的", ui);

        
        _panels[t] = ui;
    }

    public void Unregister(UIBase ui)
    {
        if (ui == null) return;
        Type t = ui.GetType();
        if (_panels.TryGetValue(t, out var cur) && cur == ui)
            _panels.Remove(t);
    }

    public T Get<T>() where T : UIBase
    {
        if (_panels.TryGetValue(typeof(T), out var ui) && ui != null) return ui as T;
        Debug.LogWarning($"[UIManager] 没找到 {typeof(T).Name}，检查它是否挂在场景里");
        return null;
    }

    public bool IsOpen<T>() where T : UIBase
    {
        if (!_panels.TryGetValue(typeof(T), out var ui) || ui == null) return false;
        return ui.IsOpen;
    }

    // 背包、任务和箱子面板；常驻血条和快捷栏不计入。
    public bool HasOpenPanel
    {
        get
        {
            if (IsOpen<BagPanel>()) return true;
            if (IsOpen<MissionReceivePanel>()) return true;
            if (IsOpen<MissionSubmitPanel>()) return true;
            if (IsOpen<MissionsBar>()) return true;
            if (_panels.TryGetValue(typeof(ChestPanel), out var ui) && ui != null && ui is ChestPanel chest)
                return chest.isAnyChestPanelActive;
            return false;
        }
    }

    public void Open<T>() where T : UIBase
    {
        var ui = Get<T>();
        if (ui == null) return;

        ui.transform.SetAsLastSibling();   // 后开的显示在最上 —— 这就是层级管理
        ui.Show();
    }

    public void Close<T>() where T : UIBase { var ui = Get<T>(); if (ui != null) ui.Hide(); }

    /// <summary>
    /// 开着就关，关着就开。
    /// 开关键（Tab 开背包那种）直接调它，调用方不用自己记状态。
    /// </summary>
    public bool Toggle<T>() where T : UIBase
    {
        var ui = Get<T>();
        if (ui == null) return false;

        if (ui.IsOpen) ui.Hide();
        else Open<T>();     // 走 Open 才会 SetAsLastSibling，直接把面板提到最上层
        return ui.IsOpen;
    }

    /// <summary>
    /// 把所有注册过的界面挨个重画一遍，用来做开局初始化。
    ///
    /// 为什么需要它：常驻的栏位（快捷栏那种）不走 Open，没人触发过它的刷新，
    /// 数据虽然在 PlayerData 里躺着，界面也还是空的。开局调一次就都出来了。
    ///
    /// 幂等，随时可以再调。
    /// </summary>
    public void RefreshAll()
    {
        foreach (var ui in _panels.Values)
        {
            // 界面被销毁过的话这里会是 null（Unity 的 == 重载），跳过
            if (ui == null) continue;
            ui.Refresh();
        }
    }
    public bool BlocksQuickBarScroll
    {
        get
        {
            if (ItemSlot.IsAnyDragging) return true;
            foreach (var ui in _panels.Values)
                if (ui != null && ui.isActiveAndEnabled && ui.BlocksQuickBarScroll)
                    return true;
            return false;
        }
    }
}
