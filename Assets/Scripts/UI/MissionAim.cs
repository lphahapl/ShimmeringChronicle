using TMPro;
using UnityEngine;
using UnityEngine.UI;
public class MissionAim : MonoBehaviour
{
    public Toggle toggle;
    public TMP_Text missionDetail;
    public TMP_Text progress;

    private MissionData data;
    private int index;

    public void Bind(MissionData data, int index)
    {
        this.data = data;
        this.index = index;
        Refresh();
    }

    public void Refresh()
    {
        var require = data.SO.requireMents[index];
        int current = Mathf.Clamp(data.currentNums[index], 0, require.requireNum);
        bool completed = current >= require.requireNum;

        missionDetail.text = require.missionDetail;
        progress.text = $"{current} / {require.requireNum}";
        toggle.interactable = false;
        toggle.SetIsOnWithoutNotify(completed);

        Color color = completed ? new Color(0.65f, 0.8f, 0.57f) : new Color(0.96f, 0.94f, 0.88f);
        missionDetail.color = color;
        progress.color = color;
    }
}
