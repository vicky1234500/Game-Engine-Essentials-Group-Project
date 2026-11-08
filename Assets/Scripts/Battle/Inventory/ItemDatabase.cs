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
    [Tooltip("Every card (incl. curses) so events can add cards by name. Filled by Tools > Roguelike > Set Up Event Rooms.")]
    public List<CardData> cards = new List<CardData>();
    [Tooltip("Items that exist but aren't in rewards/shops (Lock Pick...). Events can still give them by name.")]
    public List<ItemData> otherItems = new List<ItemData>();

    static ItemDatabase cached;

    public static ItemDatabase Load()
    {
        if (!cached) cached = Resources.Load<ItemDatabase>("ItemDatabase");
        return cached;
    }

    // Card by asset name or card name, e.g. "Rejection", "Tracker", "Static".
    public static CardData FindCard(string name)
    {
        var db = Load();
        if (!db) return null;
        foreach (var c in db.cards)
            if (c && (c.name == name || c.cardName == name)) return c;
        return null;
    }

    // Item by asset name or item name, e.g. "Cyberdeck", "LockPick".
    public static ItemData FindItem(string name)
    {
        var db = Load();
        if (!db) return null;
        foreach (var list in new[] { db.items, db.otherItems })
            foreach (var i in list)
                if (i && (i.name == name || i.itemName == name)) return i;
        return null;
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
