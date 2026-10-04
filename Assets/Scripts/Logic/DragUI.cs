using UnityEngine;
using UnityEngine.EventSystems;

public class DragUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    public GameObject dragGameObject;
    RectTransform dragRect;
    RectTransform parentRect;
    Vector3 offset;
    bool isDragging;

    public void OnBeginDrag(PointerEventData eventData)
    {
        isDragging = false;
        if (eventData.button != PointerEventData.InputButton.Left || dragGameObject == null)
            return;

        dragRect = dragGameObject.transform as RectTransform;
        parentRect = dragRect != null ? dragRect.parent as RectTransform : null;
        if (parentRect == null) return;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect, eventData.position, eventData.pressEventCamera, out var pointerPosition))
        {
            // 在父物体的局部坐标中保存窗口与鼠标的偏移。
            offset = dragRect.localPosition - (Vector3)pointerPosition;
            isDragging = true;
        }
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!isDragging || dragRect == null || parentRect == null) return;

        if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect, eventData.position, eventData.pressEventCamera, out var pointerPosition))
            dragRect.localPosition = (Vector3)pointerPosition + offset;
    }

    public void OnEndDrag(PointerEventData eventData) => isDragging = false;

    void OnDisable() => isDragging = false;
}
