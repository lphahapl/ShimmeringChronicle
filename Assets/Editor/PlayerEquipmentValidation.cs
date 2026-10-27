using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object = UnityEngine.Object;

// 用当前场景真实的快捷栏引用和滚轮处理方法检查装备切换。
[InitializeOnLoad]
public static class PlayerEquipmentValidation
{
    const string Request = "Tools/Players/LynetteMigration/equipment.request";
    static PlayerEquipmentValidation()
    {
        EditorApplication.update += CheckRequest;
        EditorApplication.playModeStateChanged += OnPlayModeChanged;
    }
    static void CheckRequest()
    {
        if (EditorApplication.isCompiling || !File.Exists(Request)) return;
        if (EditorApplication.isPlaying)
        {
            EditorApplication.ExitPlaymode();
            return;
        }
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        File.Delete(Request);
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.isDirty) EditorSceneManager.SaveScene(scene);
        SessionState.SetBool("PlayerEquipmentChecking", true);
        EditorApplication.EnterPlaymode();
    }
    static void OnPlayModeChanged(PlayModeStateChange change)
    {
        if (!SessionState.GetBool("PlayerEquipmentChecking", false)) return;
        if (change == PlayModeStateChange.EnteredPlayMode)
            new GameObject("PlayerEquipmentCheck").AddComponent<PlayerEquipmentCheck>();
        if (change == PlayModeStateChange.EnteredEditMode)
            SessionState.SetBool("PlayerEquipmentChecking", false);
    }
}

public class PlayerEquipmentCheck : MonoBehaviour
{
    float started;
    void Start()
    {
        started = Time.realtimeSinceStartup;
        Application.runInBackground = true;
        foreach (EnemyAI ai in Object.FindObjectsByType<EnemyAI>(FindObjectsSortMode.None)) ai.enabled = false;
        foreach (PlayerController controller in Object.FindObjectsByType<PlayerController>(FindObjectsSortMode.None)) controller.readInput = false;
    }
    void Update()
    {
        if (Time.realtimeSinceStartup - started < 2f) return;
        List<string> report = new List<string>();
        try
        {
            PlayerObj player = Object.FindFirstObjectByType<PlayerObj>();
            QuickBar bar = UIManager.Instance.Get<QuickBar>();
            report.Add("scene=" + UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);
            report.Add("activePlayer=" + player.name);
            if (bar.player == null) report.Add("quickBarPlayer=null");
            else report.Add("quickBarPlayer=" + bar.player.name + " active=" + bar.player.isActiveAndEnabled);
            Check(report, bar.player == player, "快捷栏引用当前活动玩家");
            bool bound = bar.quickSlots.Count > 0;
            foreach (ItemSlot slot in bar.quickSlots)
                if (slot.Owner != player.QuickBar) bound = false;
            Check(report, bound, "快捷栏格子绑定当前玩家容器");
            Check(report, player.EquippedItem != null && player.HandingItem != null, "滚轮前持有开局武器");
            PlayerController controller = player.GetComponent<PlayerController>();
            var flags = BindingFlags.Instance | BindingFlags.NonPublic;
            PlayerInput input = typeof(PlayerController).GetField("playerInput", flags).GetValue(controller) as PlayerInput;
            typeof(PlayerInput).GetProperty("ScrollValue").SetValue(input, -1f);
            typeof(PlayerController).GetMethod("ReadQuickBarWheel", flags).Invoke(controller, null);
            Check(report, player.SelectedSlot != null && player.SelectedSlot.Owner == player.QuickBar, "滚轮选中当前玩家快捷格");
            Check(report, player.EquippedItem == null && player.HandingItem == null, "滚轮切空格后清除手持剑");
        }
        catch (Exception error) { report.Add("FAIL: " + error); }
        File.WriteAllLines("Tools/Players/LynetteMigration/equipment-validation.txt", report);
        enabled = false;
        EditorApplication.ExitPlaymode();
    }
    void Check(List<string> report, bool passed, string message)
    {
        string prefix = "FAIL: ";
        if (passed) prefix = "PASS: ";
        report.Add(prefix + message);
    }
}
