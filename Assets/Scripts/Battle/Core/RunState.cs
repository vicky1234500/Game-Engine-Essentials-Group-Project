using System.Collections.Generic;
using UnityEngine;

public class SavedBackpackItem
{
    public ItemData item;
    public Vector2Int position;
    public bool rotated;
}

// Things that last for the whole run (not just one fight). Shop / events can read and change these.
public static class RunState
{
    public static CharacterData selectedCharacter;   // chosen in the Lobby (null = use the scene's defaults)
    public static int credits;
    public static int lastCreditsEarned;   // shown on the victory panel
    public static int testExtraCredits;    // RunStarter: extra credits on top of the character's (for testing)
    public static readonly List<SavedBackpackItem> savedBackpack = new List<SavedBackpackItem>();
    public static bool hasSavedBackpack;
    public static int backpackRows;        
    public static readonly Dictionary<string, List<ShopSlot>> shopStocks = new Dictionary<string, List<ShopSlot>>();

    // Called when a new run begins (RunStarter in the Lobby).
    public static void StartNewRun()
    {
        credits = 0;
        lastCreditsEarned = 0;
        ClearBackpack();
        shopStocks.Clear();
    }

    // Start-of-run money: the character's starting credits (+ test extra).
    // fallback = used when no character was picked (e.g. pressing Play straight in the Dungeon).
    public static void SetStartingCredits(int fallback = 0)
    {
        credits = (selectedCharacter ? selectedCharacter.startingCredits : fallback) + testExtraCredits;
    }

    public static void SaveBackpack(int rows, IEnumerable<ItemInstance> items)
    {
        savedBackpack.Clear();
        foreach (var i in items)
            savedBackpack.Add(new SavedBackpackItem { item = i.data, position = i.position, rotated = i.rotated });
        backpackRows = rows;
        hasSavedBackpack = true;
    }

    public static void ClearBackpack()
    {
        savedBackpack.Clear();
        backpackRows = 0;
        hasSavedBackpack = false;
    }
}