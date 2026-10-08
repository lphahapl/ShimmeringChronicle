using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>对话选项的显示控件；点击事件使用继承的 onClick。</summary>
[AddComponentMenu("UI/Dialogue Choice Button")]
public class DialogueChoiceButton : Button
{
    [SerializeField] private TMP_Text label;

    public TMP_Text Label => label;

    protected override void DoStateTransition(SelectionState state, bool instant)
    {
        base.DoStateTransition(state, instant);
        if (label == null || !gameObject.activeInHierarchy) return;

        // Button 默认只给背景变色，同时让不可用选项的文字变暗。
        Color tint = Color.white;
        if (state == SelectionState.Disabled)
            tint = new Color(0.55f, 0.55f, 0.55f, 1f);

        float duration = colors.fadeDuration;
        if (instant) duration = 0f;
        label.CrossFadeColor(tint, duration, true, true);
    }
}
