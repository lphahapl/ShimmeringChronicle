using UnityEngine;
/// <summary>
/// 所有可交互物体的基类
/// </summary>
public class InteractBase : MonoBehaviour,IInteractable
{
    public string interactString;
    protected virtual void Awake()
    {
        int interactionLayer = LayerMask.NameToLayer("Interactable");
        gameObject.layer = interactionLayer;
        // Physics filters the collider object's layer, not its parent's layer.
        foreach (var collider in GetComponentsInChildren<Collider>(true))
        {
            if (collider.GetComponentInParent<InteractBase>() == this)
                collider.gameObject.layer = interactionLayer;
        }


    }
    public InteractType interactType;
    /// <summary>交互后是否已被消耗掉。由 OnInteract 按 interactType 维护。</summary>
    bool consumed;

    /// <summary>
    /// 能不能交互。封死 —— 子类不要 override 这个方法，
    /// 否则漏掉 consumed 判断，OneShot / DestroyAfterInteract 就失效了。
    /// 子类要加条件请填 SelfCanInteract。
    /// </summary>
    public bool CanInteract(GameObject interactor)
    {
        if (consumed) return false;
        return SelfCanInteract(interactor);
    }

    /// <summary>子类填这里：除「已被消耗」之外，我还有没有别的条件（钥匙、好感度……）</summary>
    protected virtual bool SelfCanInteract(GameObject interactor)
    {
        return true;
    }

    public virtual string InteractPrompt()
    {
        return interactString;
    }


    /// <summary>
    /// 外部直接调用这个函数
    /// </summary>
    /// <param name="interactor"></param>
    /// <returns></returns>
    public bool OnInteract(GameObject interactor)
    {
        //不能交互直接返回false
        if(!CanInteract(interactor))
            return false;

        //交互逻辑
        if (!InteractLogic(interactor))
            return false;

        // 交互成功，按 interactType 决定归宿
        switch (interactType)
        {
            case InteractType.OneShot:
                consumed = true;
                break;

            case InteractType.DestroyAfterInteract:
                // 先置位再销毁：Destroy 要等帧末才真正生效，
                // 中间这一帧若不置位，还能被再交互一次。
                // 另外销毁的是整个 GameObject —— 若本组件挂在共享物体上
                // （比如门上的锁），会把别人一起删掉，那种情况子类要自己重写。
                consumed = true;
                Destroy(gameObject);
                break;
        }

        return true;
    }
    /// <summary>
    /// 每个物体的独特逻辑
    /// </summary>
    /// <param name="interactor"></param>
    protected virtual bool InteractLogic(GameObject interactor)
    {
        return true;
    }
}
/// <summary>
/// 交互后的归宿 —— 决定交互成功后这个东西怎么处理。
/// 三个值互斥，刻意不提供「可重复 + 又销毁」这种组合，因为它是自相矛盾的。
/// </summary>
public enum InteractType
{
    /// <summary>可反复交互（开关、拉杆）</summary>
    Repeatable,

    /// <summary>用一次后失效，但物体留在场景里（宝箱开过盖还立着）</summary>
    OneShot,

    /// <summary>用一次后销毁（拾取物）</summary>
    DestroyAfterInteract,
}
