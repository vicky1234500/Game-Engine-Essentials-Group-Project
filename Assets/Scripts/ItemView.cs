using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Put this on the root of the ItemView prefab.
[RequireComponent(typeof(CanvasGroup))]
public class ItemView : MonoBehaviour,
    IBeginDragHandler, IDragHandler, IEndDragHandler,
    IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    [SerializeField] Image background;
    [SerializeField] Image icon;
    [SerializeField] Text countBadge;
    [SerializeField] Text nameLabel;     // only shown when the item has no icon sprite yet
    [SerializeField] float iconPadding = 6f;

    [Header("Selected look")]
    [SerializeField] float normalAlpha = 0.45f;
    [SerializeField] float selectedAlpha = 0.8f;
    [SerializeField] Color selectedOutlineColor = new Color(1f, 0.9f, 0.2f, 1f);

    public ItemInstance Item { get; private set; }

    InventoryGrid grid;
    RectTransform rect;
    CanvasGroup group;
    Transform dragParent;
    Outline outline;
    Color outlineNormalColor;

    bool dragging, discarded;
    Vector2Int startPos;
    bool startRotated;
    Vector2 grabOffset;     // where you grabbed the item, measured from its top-left
    Vector2 pointerPos;
    Camera eventCam;

    public void Setup(ItemInstance item, InventoryGrid owner)
    {
        Item = item;
        grid = owner;
        item.view = this;
        rect = (RectTransform)transform;
        group = GetComponent<CanvasGroup>();

        // Topmost canvas, so the dragged item renders above everything.
        Canvas[] canvases = GetComponentsInParent<Canvas>(true);
        dragParent = canvases[canvases.Length - 1].transform;

        outline = GetComponent<Outline>();
        if (!outline && background) outline = background.GetComponent<Outline>();
        if (outline) outlineNormalColor = outline.effectColor;
        SetSelected(false);
        bool hasIcon = icon && item.data.icon;
        if (icon) { icon.sprite = item.data.icon; icon.enabled = hasIcon; }
        if (nameLabel) { nameLabel.text = item.data.itemName; nameLabel.gameObject.SetActive(!hasIcon); }
        if (countBadge) countBadge.text = item.data.TotalCards.ToString();
    }

    // Snap the visual onto its slots.
    public void Refresh()
    {
        rect.SetParent(grid.ItemLayer, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0f, 1f);
        rect.localScale = Vector3.one;
        rect.localRotation = Quaternion.identity;
        ApplySize();
        rect.anchoredPosition = grid.CellToAnchored(Item.position);
    }

    void ApplySize()
    {
        rect.sizeDelta = grid.SizeToPixels(Item.Size);
        if (!icon) return;

        // Icon keeps its original shape and just turns 90 degrees when the item is rotated.
        RectTransform ir = icon.rectTransform;
        ir.anchorMin = ir.anchorMax = ir.pivot = new Vector2(0.5f, 0.5f);
        ir.anchoredPosition = Vector2.zero;
        ir.sizeDelta = grid.SizeToPixels(Item.data.size) - Vector2.one * iconPadding * 2f;
        ir.localEulerAngles = new Vector3(0f, 0f, Item.rotated ? 90f : 0f);
    }

    public void SetSelected(bool selected)
    {
        if (background)
        {
            Color c = Item.data.tint;
            c.a = selected ? selectedAlpha : normalAlpha;
            background.color = c;
        }
        if (outline) outline.effectColor = selected ? selectedOutlineColor : outlineNormalColor;
    }

    // Click (without dragging) selects the item. Unity doesn't send a click after a drag.
    public void OnPointerClick(PointerEventData e)
    {
        if (e.button == PointerEventData.InputButton.Left) grid.Select(Item);
    }

    // ---------- Drag ----------

    public void OnBeginDrag(PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Left) return;

        dragging = true;
        discarded = false;
        startPos = Item.position;
        startRotated = Item.rotated;
        eventCam = e.pressEventCamera;
        pointerPos = e.position;
        if (grid.Tooltip) grid.Tooltip.Hide();
        grid.Deselect();

        Vector2 itemTopLeft = new Vector2(rect.anchoredPosition.x, -rect.anchoredPosition.y);
        grabOffset = grid.ScreenToTopLeft(e.position, eventCam) - itemTopLeft;

        grid.Remove(Item);                 // free its slots so it can be dropped back nearby
        group.blocksRaycasts = false;      // lets the trash bin receive the drop
        group.alpha = 0.85f;
        rect.SetParent(dragParent, true);
        rect.SetAsLastSibling();
        FollowPointer();
    }

    public void OnDrag(PointerEventData e)
    {
        if (!dragging) return;
        pointerPos = e.position;
        FollowPointer();
    }

    void Update()
    {
        if (!dragging) return;

        // Inventory closed mid-drag: put the item back.
        if (!grid.isActiveAndEnabled) { FinishDrag(cancel: true); return; }

        // R rotates while dragging.
        if (grid.AllowManualRotate && Keyboard.current != null && Keyboard.current.rKey.wasPressedThisFrame
            && Item.data.size.x != Item.data.size.y)
        {
            Item.rotated = !Item.rotated;
            ApplySize();
            grabOffset = grid.CellSize * 0.5f;
            FollowPointer();
        }
    }

    void FollowPointer()
    {
        Vector2 itemTopLeft = grid.ScreenToTopLeft(pointerPos, eventCam) - grabOffset;
        rect.position = grid.TopLeftToWorld(itemTopLeft);
        Vector2Int cell = grid.TopLeftToCell(itemTopLeft);
        grid.Highlight(cell, Item.Size, grid.CanPlace(cell, Item.Size));
    }

    public void OnEndDrag(PointerEventData e)
    {
        if (!dragging) return;
        pointerPos = e.position;
        FinishDrag(cancel: false);
    }

    void FinishDrag(bool cancel)
    {
        dragging = false;
        grid.ClearHighlight();
        group.blocksRaycasts = true;
        group.alpha = 1f;

        if (discarded && !cancel)
        {
            Debug.Log($"Discarded {Item.data.itemName} ({Item.data.TotalCards} cards removed from deck)");
            Destroy(gameObject);
            return;
        }

        Vector2 itemTopLeft = grid.ScreenToTopLeft(pointerPos, eventCam) - grabOffset;
        if (cancel || !grid.TryPlace(Item, grid.TopLeftToCell(itemTopLeft)))
        {
            Item.rotated = startRotated;   // invalid spot: go back where it came from
            grid.TryPlace(Item, startPos);
        }
    }

    // Called by TrashBin (OnDrop runs before OnEndDrag).
    public void MarkDiscarded()
    {
        if (dragging) discarded = true;
    }

    // ---------- Tooltip ----------

    public void OnPointerEnter(PointerEventData e)
    {
        if (dragging || e.dragging || !grid.Tooltip) return;
        grid.Tooltip.Show(Item, rect, e.enterEventCamera);
    }

    public void OnPointerExit(PointerEventData e)
    {
        if (grid.Tooltip) grid.Tooltip.Hide();
    }
}