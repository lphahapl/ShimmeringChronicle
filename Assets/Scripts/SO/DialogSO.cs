using UnityEngine;

public class DialogSO : ScriptableObject
{
    public string dialogID;
    public SingleDialog[] dialogs;
   
   
}
public struct SingleDialog
{
    public SpeakerInfo speaker;
    public string content;
}