
using UnityEngine;
using UnityEngine.EventSystems;

public class OverflowDrop : MonoBehaviour, IDropHandler
{
    public void OnDrop(PointerEventData eventData)
    {
        Debug.Log("Overflow Drop detected!");
        if (eventData.pointerDrag == null) return;

        ItemView itemView =
            eventData.pointerDrag.GetComponent<ItemView>();

        if (itemView == null) return;

        itemView.MarkMovedToOverflow();
    }
}
