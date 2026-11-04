using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Put this on ItemTooltip and leave the object disabled in the scene.
public class ItemTooltip : MonoBehaviour
{
    [SerializeField] Text titleText;
    [SerializeField] Text sizeText;
    [SerializeField] Text cardsText;
    [SerializeField] Vector2 offset = new Vector2(12f, 0f);

    readonly Vector3[] corners = new Vector3[4];
    RectTransform target;
    Camera cam;

    public void Show(ItemInstance item, RectTransform targetRect, Camera eventCam)
    {
        target = targetRect;
        cam = eventCam;

        var group = GetComponent<CanvasGroup>();
        if (!group) group = gameObject.AddComponent<CanvasGroup>();
        group.blocksRaycasts = false;   // tooltip must never steal the mouse, or it flickers

        titleText.text = item.data.itemName;
        sizeText.text = $"{item.Size.x} x {item.Size.y} slots  ·  {item.data.TotalCards} cards";

        var sb = new StringBuilder();
        foreach (var entry in item.data.cards)
        {
            if (!entry.card) continue;
            CardData c = entry.card;
            sb.Append($"<b>{entry.count}x {c.cardName}</b>  <color=#9AA4B2>{c.manaCost} mana</color>\n");
            if (!string.IsNullOrEmpty(c.description))
                sb.Append($"<size=12><color=#C8CED6>{c.description}</color></size>\n");
        }
        cardsText.text = sb.ToString().TrimEnd();

        gameObject.SetActive(true);
        transform.SetAsLastSibling();

        // Place it to the right of the item.
        var rect = (RectTransform)transform;
        rect.pivot = new Vector2(0f, 1f);
        target.GetWorldCorners(corners);   // [2] = top-right corner
        rect.position = corners[2];
        rect.anchoredPosition += offset;
    }

    public void Hide() => gameObject.SetActive(false);

    // Safety net: hide if the item disappears or the mouse is no longer over it
    // (e.g. inventory was closed while hovering).
    void Update()
    {
        if (!target || !target.gameObject.activeInHierarchy || Mouse.current == null ||
            !RectTransformUtility.RectangleContainsScreenPoint(target, Mouse.current.position.ReadValue(), cam))
            Hide();
    }
}