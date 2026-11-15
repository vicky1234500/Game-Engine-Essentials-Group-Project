using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class StorageGrid : MonoBehaviour
{
    [Header("Grid Settings")]
    [SerializeField] private GridLayoutGroup slotGrid;
    [SerializeField] private RectTransform itemLayer;
    [SerializeField] private OverflowItemView itemViewPrefab;
    [SerializeField] private GameObject slotPrefab;

    [SerializeField] private int columns = 3;
    [SerializeField] private Vector2 cellSize = new Vector2(56, 56);
    [SerializeField] private Vector2 spacing = new Vector2(5, 5);
    [SerializeField] private int padding = 10;

    private RectTransform content;

    private List<GameObject> generatedSlots = new List<GameObject>();
    private List<OverflowItemView> generatedItems = new List<OverflowItemView>();

    public IReadOnlyList<ItemInstance> Items => OverflowStorage.Items;

    private void Awake()
    {
        content = GetComponent<RectTransform>();
        SetupGrid();
    }

    private void OnEnable()
    {
        OverflowStorage.OnChanged += Refresh;
        Refresh();
    }

    private void OnDisable()
    {
        OverflowStorage.OnChanged -= Refresh;
    }

    private void SetupGrid()
    {
        if (slotGrid == null || itemLayer == null)
        {
            Debug.LogError("StorageGrid references are missing!");
            return;
        }

        slotGrid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        slotGrid.constraintCount = columns;
        slotGrid.cellSize = cellSize;
        slotGrid.spacing = spacing;
        slotGrid.startCorner = GridLayoutGroup.Corner.UpperLeft;
        slotGrid.startAxis = GridLayoutGroup.Axis.Horizontal;
        slotGrid.childAlignment = TextAnchor.UpperLeft;

        slotGrid.padding = new RectOffset(
            padding, padding, padding, padding
        );

        itemLayer.anchorMin = new Vector2(0, 1);
        itemLayer.anchorMax = new Vector2(1, 1);
        itemLayer.pivot = new Vector2(0.5f, 1);
        itemLayer.offsetMin = Vector2.zero;
        itemLayer.offsetMax = Vector2.zero;
    }

    public void Refresh()
    {
        Debug.Log("StorageGrid Refresh called!");

        ClearGeneratedObjects();

        if (OverflowStorage.Count == 0)
        {
            Debug.Log("Storage is empty.");
            return;
        }

        if (slotGrid == null || itemLayer == null ||
            itemViewPrefab == null || slotPrefab == null)
        {
            Debug.LogError("StorageGrid references are missing!");
            return;
        }

        List<ItemInstance> items = new List<ItemInstance>(
            OverflowStorage.Items
        );

        int totalCells = 0;

        foreach (ItemInstance item in items)
        {
            if (item != null && item.data != null)
            {
                totalCells += item.Size.x * item.Size.y;
            }
        }

        float stepX = cellSize.x + spacing.x;
        float stepY = cellSize.y + spacing.y;

        int maxRows = Mathf.Max(3, totalCells + 1);
        bool[,] occupied = new bool[columns, maxRows];

        int usedRows = 0;

        foreach (ItemInstance item in items)
        {
            if (item == null || item.data == null)
                continue;

            Vector2Int size = item.Size;

            bool found = false;
            Vector2Int position = Vector2Int.zero;

            // Find an empty space for the item.
            for (int y = 0; y <= maxRows - size.y; y++)
            {
                for (int x = 0; x <= columns - size.x; x++)
                {
                    bool canPlace = true;

                    for (int dx = 0; dx < size.x; dx++)
                    {
                        for (int dy = 0; dy < size.y; dy++)
                        {
                            if (occupied[x + dx, y + dy])
                            {
                                canPlace = false;
                            }
                        }
                    }

                    if (canPlace)
                    {
                        position = new Vector2Int(x, y);
                        found = true;
                        break;
                    }
                }

                if (found)
                    break;
            }

            if (!found)
            {
                Debug.LogError("Cannot place item: " + item.data.itemName);
                continue;
            }

            for (int dx = 0; dx < size.x; dx++)
            {
                for (int dy = 0; dy < size.y; dy++)
                {
                    occupied[position.x + dx, position.y + dy] = true;
                }
            }

            usedRows = Mathf.Max(usedRows, position.y + size.y);

            OverflowItemView view = Instantiate(itemViewPrefab, itemLayer);

            view.Setup(item, false, cellSize, spacing);

            RectTransform rect = view.GetComponent<RectTransform>();

            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(0, 1);
            rect.pivot = new Vector2(0, 1);

            rect.sizeDelta = new Vector2(
                size.x * cellSize.x + (size.x - 1) * spacing.x,
                size.y * cellSize.y + (size.y - 1) * spacing.y
            );

            rect.anchoredPosition = new Vector2(
                padding + position.x * stepX,
                -padding - position.y * stepY
            );

            generatedItems.Add(view);
        }

        int rows = Mathf.Max(3, usedRows);

        float height = padding * 2 +
            rows * cellSize.y +
            (rows - 1) * spacing.y;

        content.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical, height
        );

        slotGrid.GetComponent<RectTransform>().SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical, height
        );

        itemLayer.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical, height
        );

        for (int i = 0; i < rows * columns; i++)
        {
            GameObject slot = Instantiate(slotPrefab, slotGrid.transform);
            generatedSlots.Add(slot);
        }
    }

    private void ClearGeneratedObjects()
    {
        foreach (GameObject slot in generatedSlots)
        {
            if (slot != null)
            {
                Destroy(slot);
            }
        }

        generatedSlots.Clear();

        foreach (OverflowItemView item in generatedItems)
        {
            if (item != null)
            {
                Destroy(item.gameObject);
            }
        }

        generatedItems.Clear();
    }
}
