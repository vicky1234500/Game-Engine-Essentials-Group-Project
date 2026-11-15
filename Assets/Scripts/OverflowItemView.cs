using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[RequireComponent(typeof(CanvasGroup))]
public class OverflowItemView : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [SerializeField] Image background;
    [SerializeField] Image icon;
    [SerializeField] Text countBadge;
    [SerializeField] Text nameLabel;

    public ItemInstance Item { get; private set; }

    private RectTransform rect;
    private CanvasGroup group;

    private Transform originalParent;
    private Vector2 originalPosition;
    private Transform dragParent;
    private Camera eventCamera;

    private bool dragging;

    private void Awake()
    {
        rect = GetComponent<RectTransform>();
        group = GetComponent<CanvasGroup>();
    }

    public void Setup(
        ItemInstance item,
        bool displayRotated = false,
        Vector2 cellSize = default,
        Vector2 spacing = default)
    {
        if (item == null || item.data == null)
            return;

        Item = item;

        bool hasIcon = icon && item.data.icon;

        if (icon)
        {
            icon.sprite = item.data.icon;
            icon.enabled = hasIcon;

            RectTransform iconRect = icon.rectTransform;

            iconRect.anchorMin = new Vector2(0.5f, 0.5f);
            iconRect.anchorMax = new Vector2(0.5f, 0.5f);
            iconRect.pivot = new Vector2(0.5f, 0.5f);

            iconRect.anchoredPosition = Vector2.zero;

            Vector2 iconSize = new Vector2(
                item.data.size.x * cellSize.x +
                (item.data.size.x - 1) * spacing.x,

                item.data.size.y * cellSize.y +
                (item.data.size.y - 1) * spacing.y
            );

            iconRect.sizeDelta = iconSize - Vector2.one * 8f;

            if (item.rotated ^ displayRotated)
            {
                iconRect.localEulerAngles = new Vector3(0, 0, 90);
            }
            else
            {
                iconRect.localEulerAngles = Vector3.zero;
            }
        }

        if (nameLabel)
        {
            nameLabel.text = item.data.itemName;
            nameLabel.gameObject.SetActive(!hasIcon);
        }

        if (countBadge)
        {
            countBadge.text = item.data.TotalCards.ToString();
        }

        if (background)
        {
            Color color = item.data.tint;
            color.a = 0.45f;
            background.color = color;
        }
    }

    public void OnBeginDrag(PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Left || Item == null)
            return;

        dragging = true;

        originalParent = rect.parent;
        originalPosition = rect.anchoredPosition;
        eventCamera = e.pressEventCamera;

        Canvas[] canvases = GetComponentsInParent<Canvas>(true);
        dragParent = canvases[canvases.Length - 1].transform;

        group.blocksRaycasts = false;
        group.alpha = 0.8f;

        rect.SetParent(dragParent, true);
        rect.SetAsLastSibling();
    }

    public void OnDrag(PointerEventData e)
    {
        if (!dragging)
            return;

        rect.position = e.position;
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (!dragging)
            return;

        dragging = false;

        group.blocksRaycasts = true;
        group.alpha = 1f;

        InventoryGrid inventory =
            FindFirstObjectByType<InventoryGrid>(
                FindObjectsInactive.Include
            );

        if (inventory == null)
        {
            Debug.LogError("Cannot find InventoryGrid!");
            ReturnToOriginalPosition();
            return;
        }

        bool insideInventory =
            RectTransformUtility.RectangleContainsScreenPoint(
                inventory.DropArea,
                e.position,
                eventCamera
            );

        if (insideInventory)
        {
            bool added = inventory.TryAddExistingItem(Item);

            if (added)
            {
                OverflowStorage.RemoveItem(Item);
                Destroy(gameObject);
                return;
            }
        }

        ReturnToOriginalPosition();
    }

    private void ReturnToOriginalPosition()
    {
        rect.SetParent(originalParent, false);
        rect.anchoredPosition = originalPosition;
    }
}
