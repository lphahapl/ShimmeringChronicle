using UnityEngine;
using UnityEngine.UI;

[CreateAssetMenu(fileName = "SpeakerConfig", menuName = "Configs/Speaker/SpeakerSO")]

public class SpeakerInfo:ScriptableObject
{
    public Sprite head;//Í·Ïñ
    public string speakerName;
    public string description;
    public ESpeakerType SpeakerType;
}
public enum ESpeakerType
{
    NPC,
    Player,
    Enemy
}