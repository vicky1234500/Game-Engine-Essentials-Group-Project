using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Put this on InventoryMenu.
public class InventoryGrid : MonoBehaviour, IPointerClickHandler
{
    [Header("References")]
    [SerializeField] GridLayoutGroup slotGrid;    // InventorySlots
    [SerializeField] RectTransform itemLayer;     // ItemLayer
    [SerializeField] ItemView itemViewPrefab;     // ItemView prefab
    [SerializeField] ItemTooltip tooltip;         // ItemTooltip

    [Header("Grid size")]
    [SerializeField] int columns = 6;
    [SerializeField] int rows = 3;

    [Header("Rotation")]
    [Tooltip("ON: if an item doesn't fit normally, it is turned vertical to fit. OFF: it only goes in its normal orientation.")]
    [SerializeField] bool autoRotateToFit = true;
    [Tooltip("ON: player can press R while dragging to rotate an item.")]
    [SerializeField] bool allowManualRotate = true;

    [Header("Selection")]
    [Tooltip("Legacy UI Button that appears when a rotatable item is selected. It stays where you place it. Leave its On Click empty, the script hooks it up.")]
    [SerializeField] Button rotateButton;

    [Header("Slot highlight while dragging")]
    [SerializeField] Color validColor = new Color(0.35f, 0.8f, 0.45f, 1f);
    [SerializeField] Color invalidColor = new Color(0.9f, 0.3f, 0.3f, 1f);

    ItemInstance[,] cells;
    readonly List<ItemInstance> items = new List<ItemInstance>();
    Image[] slotImages;
    Color[] slotColors;
    bool initialized;
    ItemInstance selected;

    public RectTransform ItemLayer => itemLayer;
    public ItemTooltip Tooltip => tooltip;
    public IReadOnlyList<ItemInstance> Items => items;
    public Vector2 CellSize => slotGrid.cellSize;
    public Vector2 Step => slotGrid.cellSize + slotGrid.spacing;
    public bool AllowManualRotate => allowManualRotate;
    public ItemInstance Selected => selected;

    void Awake() => EnsureInit();

    // Lazy init so other scripts can add items even while the inventory is closed (inactive).
    void EnsureInit()
    {
        if (initialized) return;
        initialized = true;

        cells = new ItemInstance[columns, rows];
        int count = slotGrid.transform.childCount;
        slotImages = new Image[count];
        slotColors = new Color[count];
        for (int i = 0; i < count; i++)
        {
            slotImages[i] = slotGrid.transform.GetChild(i).GetComponent<Image>();
            if (slotImages[i]) slotColors[i] = slotImages[i].color;
        }

        if (rotateButton)
        {
            rotateButton.onClick.AddListener(RotateSelected);
            SetRotateButtonVisible(false);
        }
    }

    // ---------- Adding / removing ----------

    // Finds the first free spot (tries normal, then rotated). Returns false if the backpack is full.
    public bool TryAddItem(ItemData data)
    {
        if (!FindSpace(data, out Vector2Int pos, out bool rotated))
        {
            Debug.Log($"Inventory full: no space for {data.itemName}");
            return false;
        }

        var item = new ItemInstance(data) { rotated = rotated };
        ItemView view = Instantiate(itemViewPrefab, itemLayer);
        view.Setup(item, this);
        TryPlace(item, pos);
        return true;
    }

    // Checks without adding anything (used by world pickups).
    public bool HasSpaceFor(ItemData data) => FindSpace(data, out _, out _);

    // First free spot: normal orientation first, then rotated (only if autoRotateToFit is on).
    bool FindSpace(ItemData data, out Vector2Int pos, out bool rotated)
    {
        EnsureInit();
        for (int pass = 0; pass < 2; pass++)
        {
            rotated = pass == 1;
            if (rotated && (!autoRotateToFit || data.size.x == data.size.y)) break;
            Vector2Int size = rotated ? new Vector2Int(data.size.y, data.size.x) : data.size;

            for (int y = 0; y < rows; y++)
            for (int x = 0; x < columns; x++)
            {
                pos = new Vector2Int(x, y);
                if (CanPlace(pos, size)) return true;
            }
        }
        pos = default;
        rotated = false;
        return false;
    }

    public bool TryPlace(ItemInstance item, Vector2Int pos)
    {
        EnsureInit();
        if (!CanPlace(pos, item.Size, item)) return false;

        Remove(item);
        item.position = pos;
        for (int dx = 0; dx < item.Size.x; dx++)
        for (int dy = 0; dy < item.Size.y; dy++)
            cells[pos.x + dx, pos.y + dy] = item;   // every covered slot points to the SAME item

        items.Add(item);
        if (item.view) item.view.Refresh();
        return true;
    }

    public void Remove(ItemInstance item)
    {
        EnsureInit();
        for (int x = 0; x < columns; x++)
        for (int y = 0; y < rows; y++)
            if (cells[x, y] == item) cells[x, y] = null;
        items.Remove(item);
    }

    public void Discard(ItemInstance item)
    {
        if (selected == item) Deselect();
        Remove(item);
        if (item.view) Destroy(item.view.gameObject);
    }

    public bool IsInside(Vector2Int pos, Vector2Int size) =>
        pos.x >= 0 && pos.y >= 0 && pos.x + size.x <= columns && pos.y + size.y <= rows;

    public bool CanPlace(Vector2Int pos, Vector2Int size, ItemInstance ignore = null)
    {
        EnsureInit();
        if (!IsInside(pos, size)) return false;
        for (int dx = 0; dx < size.x; dx++)
        for (int dy = 0; dy < size.y; dy++)
        {
            var other = cells[pos.x + dx, pos.y + dy];
            if (other != null && other != ignore) return false;
        }
        return true;
    }

    // ---------- Selection and rotate button ----------

    public void Select(ItemInstance item)
    {
        EnsureInit();
        if (selected != null && selected.view) selected.view.SetSelected(false);
        selected = item;
        if (selected != null && selected.view) selected.view.SetSelected(true);
        UpdateRotateButton();
    }

    public void Deselect() => Select(null);

    // Clicking empty space in the inventory (background or empty slots) deselects.
    public void OnPointerClick(PointerEventData e) => Deselect();

    // Rotates the selected item and places it as close as possible to where it was
    // (e.g. shifts it left if the rotated shape would stick out past the right edge).
    public void RotateSelected()
    {
        if (selected == null || !allowManualRotate) return;

        ItemInstance item = selected;
        Vector2 oldCenter = (Vector2)item.position + (Vector2)item.Size * 0.5f;

        item.rotated = !item.rotated;
        Vector2Int newSize = item.Size;

        // Check every slot and keep the valid one whose center is nearest to the old center.
        bool found = false;
        Vector2Int best = default;
        float bestDistance = float.MaxValue;
        for (int y = 0; y < rows; y++)
        for (int x = 0; x < columns; x++)
        {
            var pos = new Vector2Int(x, y);
            if (!CanPlace(pos, newSize, item)) continue;   // "item" is ignored, so its own old slots count as free

            float distance = ((Vector2)pos + (Vector2)newSize * 0.5f - oldCenter).sqrMagnitude;
            if (distance < bestDistance)
            {
                bestDistance = distance;
                best = pos;
                found = true;
            }
        }

        if (found)
        {
            TryPlace(item, best);
        }
        else
        {
            item.rotated = !item.rotated;   // undo
            Debug.Log($"No room to rotate {item.data.itemName}");
        }
        UpdateRotateButton();
    }

    void UpdateRotateButton()
    {
        if (!rotateButton) return;

        bool show = selected != null && selected.view && allowManualRotate
                    && selected.data.size.x != selected.data.size.y;   // square items never need rotating
        SetRotateButtonVisible(show);   // only shows/hides, the button stays where you placed it
    }

    // Uses a CanvasGroup instead of SetActive, so it's safe while the menu opens or closes.
    void SetRotateButtonVisible(bool visible)
    {
        var group = rotateButton.GetComponent<CanvasGroup>();
        if (!group) group = rotateButton.gameObject.AddComponent<CanvasGroup>();
        group.alpha = visible ? 1f : 0f;
        group.interactable = visible;
        group.blocksRaycasts = visible;
    }

    // ---------- Battle deck ----------

    public List<CardData> BuildDeck()
    {
        var deck = new List<CardData>();
        foreach (var item in items)
            foreach (var entry in item.data.cards)
                if (entry.card)
                    for (int i = 0; i < entry.count; i++) deck.Add(entry.card);
        return deck;
    }

    // ---------- Coordinates (everything measured from ItemLayer's top-left corner) ----------

    public Vector2 SizeToPixels(Vector2Int size) => new Vector2(
        size.x * CellSize.x + (size.x - 1) * slotGrid.spacing.x,
        size.y * CellSize.y + (size.y - 1) * slotGrid.spacing.y);

    public Vector2 CellToAnchored(Vector2Int cell) => new Vector2(cell.x * Step.x, -cell.y * Step.y);

    public Vector2 ScreenToTopLeft(Vector2 screenPos, Camera cam)
    {
        RectTransformUtility.ScreenPointToLocalPointInRectangle(itemLayer, screenPos, cam, out Vector2 local);
        Rect r = itemLayer.rect;
        return new Vector2(local.x - r.xMin, r.yMax - local.y);
    }

    public Vector3 TopLeftToWorld(Vector2 topLeft)
    {
        Rect r = itemLayer.rect;
        return itemLayer.TransformPoint(new Vector3(r.xMin + topLeft.x, r.yMax - topLeft.y, 0f));
    }

    public Vector2Int TopLeftToCell(Vector2 topLeft) => new Vector2Int(
        Mathf.RoundToInt(topLeft.x / Step.x),
        Mathf.RoundToInt(topLeft.y / Step.y));

    // ---------- Slot highlight ----------

    public void Highlight(Vector2Int pos, Vector2Int size, bool valid)
    {
        ClearHighlight();
        for (int dx = 0; dx < size.x; dx++)
        for (int dy = 0; dy < size.y; dy++)
        {
            int x = pos.x + dx, y = pos.y + dy;
            if (x < 0 || y < 0 || x >= columns || y >= rows) continue;
            int i = y * columns + x;
            if (i < slotImages.Length && slotImages[i])
                slotImages[i].color = valid ? validColor : invalidColor;
        }
    }

    public void ClearHighlight()
    {
        if (slotImages == null) return;
        for (int i = 0; i < slotImages.Length; i++)
            if (slotImages[i]) slotImages[i].color = slotColors[i];
    }
}