using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 背包格子（UI）。只负责显示，真相在 ItemData 里。
/// 数据由外部调 Bind() 推进来，格子不自己去背包里找。
/// </summary>
public class ItemSlot : MonoBehaviour,IPointerEnterHandler,IPointerExitHandler,IBeginDragHandler,IEndDragHandler,IDragHandler,IDropHandler,IPointerClickHandler
{
    [SerializeField] Image iconImage;
    [SerializeField] TMP_Text countText;
    /// <summary>
    /// 拖动时的图片提示
    /// </summary>
    [SerializeField] private Image dragImage;
    [SerializeField] private GameObject selectionBorder;
    private bool isDragging;
    static readonly HashSet<ItemSlot> draggingSlots = new HashSet<ItemSlot>();
    static readonly List<RaycastResult> pointerHits = new List<RaycastResult>();
    public static bool IsAnyDragging => draggingSlots.Count > 0;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetDragState()
    {
        draggingSlots.Clear();
        pointerHits.Clear();
    }

    public static bool IsPointerOverSlot(Vector2 screenPosition)
    {
        var system = EventSystem.current;
        if (system == null) return false;
        var pointer = new PointerEventData(system) { position = screenPosition };
        pointerHits.Clear();
        system.RaycastAll(pointer, pointerHits);
        bool overSlot = pointerHits.Count > 0 &&
            pointerHits[0].gameObject.GetComponentInParent<ItemSlot>() != null;
        pointerHits.Clear();
        return overSlot;
    }

    public ItemData Item => item;

    /// <summary>
    /// 当前显示的那一叠。持引用不持拷贝，所以背包里 count 变了这里就是新值，
    /// 不需要谁来同步。刷新时机只影响「什么时候重画」，不影响「值对不对」。
    /// </summary>
    ItemData item;

    /// <summary>
    /// 自己属于哪个容器。拖拽必须知道 —— 交换是两个"位置"之间的操作，
    /// 位置 = (容器, 下标)。光知道自己显示的是哪件物品，是推不出位置的。
    /// </summary>
    IItemOperator owner;

    public IItemOperator Owner => owner;

    /// <summary>选中高亮。纯 UI 状态，跟数据无关。</summary>
    public bool isSelected;
    /// <summary>
    /// 高亮遮罩
    /// </summary>
    public Image mask;
    /// <summary>
    /// 自己是第几格。由 Bind 填 —— 面板会复用格子，不能只在实例化时设一次。
    /// "按位置操作"（RemoveItemAt / ItemRules.Exchange）靠它。
    /// 点击之前先判 item == null：空格子的 index 虽然是真的，但没有意义。
    /// </summary>
    public int index;


    /// <summary>
    /// 把要显示的那一叠、它在容器里的位置、和它属于哪个容器推进来。
    /// data 传 null 表示空格子；owner 传 null 表示这一格不参与拖拽。
    /// </summary>
    public void Bind(ItemData data, int index, IItemOperator owner)
    {
        // 显示的东西换了位置，正在进行的拖拽就作废
        if (item != data || this.index != index) StopDrag();

        item = data;
        this.index = index;
        this.owner = owner;
        RefreshSlot();
    }

    public void RefreshSlot()
    {
        bool empty = item == null || item.count <= 0;

        iconImage.gameObject.SetActive(!empty);
        countText.gameObject.SetActive(!empty && item.count > 1);   // 只有 1 个就不显数字

        if (empty)
        {
            StopDrag();
            OnNotSelected();
            iconImage.sprite = null;
            SyncDragImage();
            return;
        }

        iconImage.sprite = item.icon;        // 图标从数据读，格子上不再存一份
        countText.text = item.count.ToString();
        SyncDragImage();
    }
    /// <summary>
    /// 鼠标进入发生什么
    /// </summary>
    /// <param name="eventData"></param>
    public void OnPointerEnter(PointerEventData eventData)
    {
        if (item == null || item.count <= 0) return;
        OnSelected();
    }
    /// <summary>
    /// 鼠标退出发生什么
    /// </summary>
    /// <param name="eventData"></param>
    /// <exception cref="System.NotImplementedException"></exception>
    public void OnPointerExit(PointerEventData eventData)
    {
        OnNotSelected();
    }
    /// <summary>
    /// 开始拖动时发生什么
    /// </summary>
    /// <param name="eventData"></param>
   
    public void OnBeginDrag(PointerEventData eventData)
    {
        if (isDragging || item == null || item.count <= 0 ||
            iconImage == null || dragImage == null || dragImage == iconImage) return;

        SyncDragImage();
        isDragging = true;
        draggingSlots.Add(this);
        UpdatePosition(eventData);
        dragImage.gameObject.SetActive(true);
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        StopDrag();
    }
    /// <summary>
    /// 拖动时每帧触发
    /// </summary>
    /// <param name="eventData"></param>
    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging) return;
        UpdatePosition(eventData);
    }

    private void UpdatePosition(PointerEventData eventData)
    {
        if (dragImage == null) return;
        // 当前 Overlay UI：Vector2 自动转成 (x, y, 0)。
        dragImage.transform.position = eventData.position;
    }
    /// <summary>
    /// 同步拖动图标
    /// </summary>
    private void SyncDragImage()
    {
        if (iconImage == null || dragImage == null || dragImage == iconImage) return;
        // 两个独立 Image，只同步显示属性，不把 dragImage 指向 iconImage。
        dragImage.sprite = iconImage.sprite;
        dragImage.color = iconImage.color;
        dragImage.preserveAspect = iconImage.preserveAspect;
        dragImage.raycastTarget = false;
    }
    /// <summary>
    /// 别的物品拖拽松开，落在本格子上面时触发。
    ///
    /// 直接换 —— 两个格子各自知道自己属于哪个容器、在第几格，位置信息是齐的，
    ///
    /// 越界、空格、堆叠上限、拆着拖拽落到别的物品上全部由 Exchange 自己判，
    /// 
    /// </summary>
    public void OnDrop(PointerEventData eventData)
    {
        if (eventData.pointerDrag == null) return;

        var source = eventData.pointerDrag.GetComponent<ItemSlot>();
        if (source == null || source == this || !source.isDragging) return;

        source.StopDrag();
        ItemRules.Exchange(source.Owner, source.index, Owner, index);
    }

    private void StopDrag()
    {
        if (dragImage != null && dragImage != iconImage)
            dragImage.gameObject.SetActive(false);
        isDragging = false;
        draggingSlots.Remove(this);
    }

    void OnEnable()
    {
        this.Subscribe(GameEvents.OnSelectedSlotChanged, OnSelectedSlotChanged);
        if (selectionBorder != null) selectionBorder.SetActive(false);
    }

    private void OnSelectedSlotChanged(ItemSlot selectedSlot)
    {
        if (selectionBorder != null) selectionBorder.SetActive(selectedSlot == this);
    }

    public void OnDisable()
    {
        this.UnSubscribe(GameEvents.OnSelectedSlotChanged, OnSelectedSlotChanged);
        if (selectionBorder != null) selectionBorder.SetActive(false);
        StopDrag();
        OnNotSelected();
    }

    private void OnSelected()
    {
        isSelected = true;
        if (mask == null) return;
        var tmp = mask.color;
        tmp.a = 0.3f;
        mask.color = tmp;
    }
    private void OnNotSelected()
    {
        isSelected = false;
        if (mask == null) return;
        var tmp = mask.color;
        tmp.a = 0f;
        mask.color = tmp;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (eventData.button == PointerEventData.InputButton.Left)
            //发起点击请求
            this.Publish(GameEvents.OnSlotSelectionRequested, this);
    }
}
