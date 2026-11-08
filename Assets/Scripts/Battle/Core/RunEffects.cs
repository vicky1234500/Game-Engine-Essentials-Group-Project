using System.Collections.Generic;
using UnityEngine;

// One place for events (and anything else) to change the run for real:
// curses in the deck, DF, items, HP. Events call these instead of keeping their own "prototype records".
public static class RunEffects
{
    // ---------- Curses ----------

    // Adds a curse card (by name: "Rejection", "Tracker", "Static"...) to every fight's deck for the rest of the run.
    public static bool AddCurse(string cardName, int count = 1)
    {
        var card = ItemDatabase.FindCard(cardName);
        if (!card)
        {
            Debug.LogWarning($"RunEffects: card '{cardName}' not found. Run Tools > Roguelike > Set Up Event Rooms.");
            return false;
        }
        for (int i = 0; i < count; i++) RunState.runCurses.Add(card);
        return true;
    }

    public static int CurseCount(string cardName)
    {
        int n = 0;
        foreach (var c in RunState.runCurses) if (c && (c.name == cardName || c.cardName == cardName)) n++;
        return n;
    }

    // Removes one curse: first one added by an event, otherwise one curse card from an item in the backpack.
    // Returns the curse's name, or null if there was nothing to remove.
    public static string RemoveOneCurse(InventoryGrid inventory)
    {
        if (RunState.runCurses.Count > 0)
        {
            var c = RunState.runCurses[RunState.runCurses.Count - 1];
            RunState.runCurses.RemoveAt(RunState.runCurses.Count - 1);
            return c ? c.cardName : "Curse";
        }

        if (!inventory) return null;
        foreach (var item in inventory.Items)
        {
            for (int i = 0; i < item.data.cards.Count; i++)
            {
                var entry = item.data.cards[i];
                if (!entry.card || entry.card.type != CardType.Curse) continue;

                // Change a private copy, never the shared item asset
                if (!item.data.name.EndsWith("(Run)"))
                {
                    item.data = Object.Instantiate(item.data);
                    item.data.name = item.data.name.Replace("(Clone)", "") + " (Run)";
                    entry = item.data.cards[i];
                }
                string removed = entry.card.cardName;
                if (entry.count > 1) entry.count--;
                else item.data.cards.RemoveAt(i);
                return $"{removed} (from {item.data.itemName})";
            }
        }
        return null;
    }

    public static bool HasAnyCurse(InventoryGrid inventory)
    {
        if (RunState.runCurses.Count > 0) return true;
        if (!inventory) return false;
        foreach (var item in inventory.Items)
            foreach (var e in item.data.cards)
                if (e.card && e.card.type == CardType.Curse) return true;
        return false;
    }

    // ---------- Stats ----------

    public static void AddDefense(int amount) => RunState.bonusDefense += amount;

    public static PlayerCombatant Player => Object.FindFirstObjectByType<PlayerCombatant>(FindObjectsInactive.Include);

    // ---------- Items ----------

    // Puts the item in the backpack. If it's full, it's dropped on the floor in front of the player
    // (same as battle rewards) and picked up automatically once there's space.
    // Returns a short line for the event text.
    public static string GiveItem(InventoryGrid inventory, ItemData item)
    {
        if (!item) return "";
        if (inventory && inventory.TryAddItem(item)) return $"Received: {item.itemName}.";

        var panel = Object.FindFirstObjectByType<ItemRewardPanel>(FindObjectsInactive.Include);
        if (panel && panel.DropNearPlayer(item))
            return $"Backpack full! {item.itemName} was dropped on the floor. Make space (press 0) and walk over it.";
        return $"Backpack full. {item.itemName} was lost.";
    }

    // The main card type of an item = its category (Attack / Defense / Heal / Skill).
    public static CardType Category(ItemData item)
    {
        var counts = new Dictionary<CardType, int>();
        foreach (var e in item.cards)
            if (e.card && e.card.type != CardType.Curse)
                counts[e.card.type] = (counts.TryGetValue(e.card.type, out var n) ? n : 0) + e.count;
        CardType best = CardType.Skill;
        int bestCount = -1;
        foreach (var kv in counts)
            if (kv.Value > bestCount) { best = kv.Key; bestCount = kv.Value; }
        return best;
    }

    public static int Tiles(ItemData item) => item.size.x * item.size.y;

    public static bool HasCurseCard(ItemData item)
    {
        foreach (var e in item.cards) if (e.card && e.card.type == CardType.Curse) return true;
        return false;
    }

    public static ItemInstance FindInBackpack(InventoryGrid inventory, string itemName)
    {
        if (!inventory) return null;
        foreach (var i in inventory.Items)
            if (i.data.name == itemName || i.data.itemName == itemName || i.data.name.StartsWith(itemName)) return i;
        return null;
    }
}
