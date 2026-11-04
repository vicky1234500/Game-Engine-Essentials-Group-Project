using System.Collections.Generic;
using UnityEngine;

// Every item that can show up as a battle reward or in a shop.
// Lives at Assets/Resources/ItemDatabase.asset so any scene can load it without setup.
// Filled by Tools > Roguelike > Create / Update All Items. Items that don't do anything yet
// (Lock Pick, Grappling Hook, Portable Workbench, Field Repair Kit) are left out on purpose.
[CreateAssetMenu(fileName = "ItemDatabase", menuName = "Roguelike/Item Database")]
public class ItemDatabase : ScriptableObject
{
    public List<ItemData> items = new List<ItemData>();

    static ItemDatabase cached;

    public static ItemDatabase Load()
    {
        if (!cached) cached = Resources.Load<ItemDatabase>("ItemDatabase");
        return cached;
    }

    // Items in the database, minus the given ones (e.g. starting items).
    public static List<ItemData> Pool(ICollection<ItemData> exclude = null)
    {
        var list = new List<ItemData>();
        var db = Load();
        if (!db) return list;
        foreach (var item in db.items)
            if (item && (exclude == null || !exclude.Contains(item)) && !list.Contains(item))
                list.Add(item);
        return list;
    }
}
