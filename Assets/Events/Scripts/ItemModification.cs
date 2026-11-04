using UnityEngine;

public class ItemModification : MonoBehaviour
{
    [Header("Runtime modification record")]
    [SerializeField] int damagePerHitOffset;

    ItemData sourceData;
    ItemData runtimeData;
    ItemInstance owner;

    public int DamagePerHitOffset => damagePerHitOffset;
    public ItemData SourceData => sourceData;

    // Future combat integration:
    // Call this once for each direct attack hit.
    public int CalculateHitDamage(int baseDamage)
    {
        return Mathf.Max(0, baseDamage + damagePerHitOffset);
    }

    public static ItemData GetSourceData(ItemInstance item)
    {
        if (item == null || !item.view)
            return null;

        ItemModification modification =
            item.view.GetComponent<ItemModification>();

        if (modification && modification.sourceData)
            return modification.sourceData;

        return item.data;
    }

    // Uses the item's current dimensions.
    // Long strips change only their long side.
    // Other rectangles change both sides.
    public static bool TryGetNewSize(
        Vector2Int current,
        bool enlarge,
        out Vector2Int result)
    {
        result = current;

        if (current.x < 1 || current.y < 1)
            return false;

        // A 1 x 1 item cannot use either modification.
        if (current == Vector2Int.one)
            return false;

        int change = enlarge ? 1 : -1;

        if (current.x == 1)
        {
            result.y += change;
        }
        else if (current.y == 1)
        {
            result.x += change;
        }
        else
        {
            result.x += change;
            result.y += change;
        }

        if (result.x < 1 || result.y < 1)
            return false;

        // Enlargement is limited by the longest side,
        // not by the total number of occupied tiles.
        if (enlarge && Mathf.Max(result.x, result.y) > 6)
            return false;

        return true;
    }

    public static bool IsInBackpack(
        InventoryGrid grid,
        ItemInstance item)
    {
        if (!grid || item == null || !item.view)
            return false;

        foreach (ItemInstance existing in grid.Items)
        {
            if (existing == item)
                return true;
        }

        return false;
    }

    // Preview only: does not change the item or consume money.
    public static bool TryFindPlacement(
        InventoryGrid grid,
        ItemInstance item,
        Vector2Int newDataSize,
        out Vector2Int position,
        out bool rotated)
    {
        position = item != null ? item.position : Vector2Int.zero;
        rotated = item != null && item.rotated;

        if (!IsInBackpack(grid, item))
            return false;

        if (newDataSize.x < 1 || newDataSize.y < 1)
            return false;

        Vector2Int currentOrientationSize = rotated
            ? new Vector2Int(newDataSize.y, newDataSize.x)
            : newDataSize;

        // Prefer the item's current position.
        // Ignore its own occupied cells when checking.
        if (grid.CanPlace(position, currentOrientationSize, item))
            return true;

        // Read available grid dimensions through the existing API.
        int columns = 0;
        int rows = 0;

        while (grid.IsInside(
            new Vector2Int(columns, 0),
            Vector2Int.one))
        {
            columns++;
        }

        while (grid.IsInside(
            new Vector2Int(0, rows),
            Vector2Int.one))
        {
            rows++;
        }

        // First try the current orientation.
        // Try a rotated orientation only when rotation is allowed.
        int passes = grid.AllowManualRotate ? 2 : 1;

        for (int pass = 0; pass < passes; pass++)
        {
            bool candidateRotation =
                pass == 0 ? item.rotated : !item.rotated;

            Vector2Int candidateSize = candidateRotation
                ? new Vector2Int(newDataSize.y, newDataSize.x)
                : newDataSize;

            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < columns; x++)
                {
                    Vector2Int candidate = new Vector2Int(x, y);

                    if (!grid.CanPlace(candidate, candidateSize, item))
                        continue;

                    position = candidate;
                    rotated = candidateRotation;
                    return true;
                }
            }
        }

        return false;
    }

    // Called only after the event confirms the selected item
    // is an eligible attack item and the player can pay.
    public static bool TryApply(
        InventoryGrid grid,
        ItemInstance item,
        bool enlarge,
        out string error)
    {
        error = "";

        if (!IsInBackpack(grid, item))
        {
            error = "The selected item is no longer in the backpack.";
            return false;
        }

        if (!TryGetNewSize(item.data.size, enlarge, out Vector2Int newSize))
        {
            error = "This item cannot be modified in that direction.";
            return false;
        }

        if (!TryFindPlacement(
            grid,
            item,
            newSize,
            out Vector2Int newPosition,
            out bool newRotation))
        {
            error = "Not enough space for the modified item.";
            return false;
        }

        ItemModification modification =
            item.view.GetComponent<ItemModification>();

        if (!modification)
            modification = item.view.gameObject.AddComponent<ItemModification>();

        modification.EnsurePrivateData(item);

        Vector2Int previousSize = item.data.size;
        bool previousRotation = item.rotated;

        item.data.size = newSize;
        item.rotated = newRotation;

        if (!grid.TryPlace(item, newPosition))
        {
            item.data.size = previousSize;
            item.rotated = previousRotation;
            item.view.Refresh();

            error = "Placement failed. No modification was applied.";
            return false;
        }

        modification.damagePerHitOffset += enlarge ? 2 : -1;

        // Refresh icon size and selection after resizing.
        item.view.Refresh();
        grid.Deselect();

        return true;
    }

    void EnsurePrivateData(ItemInstance item)
    {
        if (runtimeData)
            return;

        owner = item;
        sourceData = item.data;

        // Clone only this item's data in memory.
        // Never edit the shared project asset.
        runtimeData = Instantiate(sourceData);
        runtimeData.name = sourceData.name + " (Modified Instance)";

        item.data = runtimeData;
    }

    void OnDestroy()
    {
        if (!runtimeData)
            return;

        if (owner != null && owner.data == runtimeData)
            owner.data = sourceData;

        Destroy(runtimeData);
    }
}