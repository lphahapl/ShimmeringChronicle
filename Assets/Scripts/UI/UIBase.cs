using UnityEngine;
using UnityEngine.EventSystems;

public abstract class UIBase : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{

    public bool IsOpen { get; private set; }
    /// <summary>
    /// 鼠标是否在自己上面
    /// </summary>
    public bool isPointerIn;

    public virtual bool BlocksQuickBarScroll => false;

    /// <summary>
    /// 界面的视觉根。开关界面时失活的是它，不是脚本所在的物体 ——
    /// 脚本物体一直活着，关着的时候才能继续收事件、跑 Update / 协程。
    /// 不填就退回老行为：直接失活脚本所在的物体。
    /// </summary>
    [Tooltip("界面的视觉根（拖场景里那个物体）。不填 = 直接失活脚本所在物体")]
    [SerializeField] GameObject viewRoot;

    /// <summary>
    /// 视觉根现在是不是真的显示着。
    /// 光看 IsOpen 不够 —— 父物体被关掉、或者压根没 Show 过，都算看不见。
    /// </summary>
    protected bool IsVisible =>
        viewRoot != null ? viewRoot.activeInHierarchy : gameObject.activeInHierarchy;

   
    protected virtual void Awake()
    {
        IsOpen = IsVisible;
    }

    protected virtual void OnDestroy()
    {
        // 注销，避免 UIManager 字典里留下已销毁的引用
        if (UIManager.Instance != null)
            UIManager.Instance.Unregister(this);
    }

    /// <summary>打开。封死，子类只填 OnShow。</summary>
    public void Show()
    {
        if (IsOpen) return;
        IsOpen = true;
        if (viewRoot != null) viewRoot.SetActive(true);
        OnShow();
    }

    public void Hide()
    {
        if (!IsOpen) return;
        IsOpen = false;
        isPointerIn = false;
        OnHide();
        if (viewRoot != null) viewRoot.SetActive(false);
    }

    /// <summary>
    /// 把界面上的显示重画一遍。数据没变也可以叫，幂等。
    /// 基类默认什么都不做 —— 纯静态的界面不需要重写。
    /// UIManager.RefreshAll() 会挨个调。
    /// </summary>
    public virtual void Refresh() { }

    /// <summary>初始化为隐藏状态，同时重置打开标记，不触发 OnHide。</summary>
    protected void StartHidden()
    {
        IsOpen = false;
        if (viewRoot != null) viewRoot.SetActive(false);
    }

    protected virtual void OnShow() { }
    protected virtual void OnHide() { }

    public void OnPointerEnter(PointerEventData eventData)
    {
       isPointerIn = true;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isPointerIn=false;
    }
    protected virtual void OnDisable()
    {
        isPointerIn = false;
    }

}
