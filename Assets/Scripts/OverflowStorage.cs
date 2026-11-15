using System.Collections.Generic;
using UnityEngine;

// Store items that cannot fit in the Inventory.
public static class OverflowStorage
{
    private static List<ItemInstance> items = new List<ItemInstance>();

    public static System.Action OnChanged;

    // Add a new item.
    public static void AddItem(ItemData item)
    {
        if (item == null)
            return;

        ItemInstance newItem = new ItemInstance(item);

        items.Add(newItem);

        OnChanged?.Invoke();

        Debug.Log("Added to Overflow Storage: " + item.itemName);
    }

    // Add an existing item.
    public static void AddItem(ItemInstance item)
    {
        if (item == null)
            return;

        items.Add(item);

        OnChanged?.Invoke();

        Debug.Log("Added to Overflow Storage: " + item.data.itemName);
    }

    // Remove an item.
    public static void RemoveItem(ItemInstance item)
    {
        if (items.Remove(item))
        {
            OnChanged?.Invoke();
        }
    }

    // Get all items.
    public static IReadOnlyList<ItemInstance> Items => items;

    public static int Count => items.Count;

    // Remove all items.
    public static void Clear()
    {
        items.Clear();
        OnChanged?.Invoke();
    }
}
