using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Put this on TrashBin (the object with the invisible, raycastable Image).
public class TrashBin : MonoBehaviour, IDropHandler, IPointerEnterHandler, IPointerExitHandler
{
    [SerializeField] Graphic icon;   // the visible trash icon (child)
    [SerializeField] Color hoverColor = new Color(1f, 0.35f, 0.35f, 1f);
    [SerializeField] float hoverScale = 1.15f;

    Color normalColor = Color.white;
    Vector3 normalScale = Vector3.one;
    bool cached;

    public void OnPointerEnter(PointerEventData e)
    {
        // Only react when an item is being dragged over it.
        if (e.dragging && e.pointerDrag && e.pointerDrag.GetComponent<ItemView>())
            SetHover(true);
    }

    public void OnPointerExit(PointerEventData e) => SetHover(false);

    public void OnDrop(PointerEventData e)
    {
        SetHover(false);
        ItemView view = e.pointerDrag ? e.pointerDrag.GetComponent<ItemView>() : null;
        if (view) view.MarkDiscarded();
        // Later in the real project: confirmation popup or sell refund goes here.
    }

    void OnDisable() => SetHover(false);

    void SetHover(bool on)
    {
        if (!icon) return;
        if (!cached)
        {
            cached = true;
            normalColor = icon.color;
            normalScale = icon.rectTransform.localScale;
        }
        icon.color = on ? hoverColor : normalColor;
        icon.rectTransform.localScale = on ? normalScale * hoverScale : normalScale;
    }
}
