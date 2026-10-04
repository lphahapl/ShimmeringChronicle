using UnityEngine;

/// <summary>
/// 每个可以交互的物体都要继承这个接口
/// </summary>
public interface IInteractable
{
    /// <summary>
    /// 交互时发生的逻辑
    /// </summary>
    public abstract bool OnInteract(GameObject interactor);
   
    /// <summary>
    /// 该物体是否可以交互
    /// </summary>
    /// <returns></returns>
    public abstract bool CanInteract(GameObject interactor);
    /// <summary>
    /// 交互时的提示
    /// </summary>
    /// <returns></returns>
    public abstract string InteractPrompt();

}
