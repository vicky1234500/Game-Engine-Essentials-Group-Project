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

    // ---- Effects from events that last the whole run (see RunEffects) ----
    public static readonly List<CardData> runCurses = new List<CardData>();  // Rejection / Tracker added to every fight's deck
    public static int bonusDefense;              // Ripperdoc: +10 DF
    public static int staticPerFight;            // Corrupted Terminal: Static cards added to each fight...
    public static int staticFightsLeft;          // ...for this many more fights
    public static int battleStartOverclockTurns; // AI Oracle: start every fight OVERCLOCKED for X turns
    public static bool bossIntel;                // AI Oracle: show the boss's moves before the fight
    public static bool noRewardsNextRoom;        // Nanite Leak "go around": the next room gives no rewards
    public static bool noRewardsThisRoom;        //   (moved here when that room is entered)
    public static bool noRewardsNextFight;       // Fight Ring: the elite fight gives no normal rewards
    public static bool lastFightHadNoRewards;    // read by the victory panel
    public static int lockPickUses = -1;         // Lock Pick: 3 uses (-1 = not used yet)

    // Called when a new run begins (RunStarter in the Lobby).
    public static void StartNewRun()
    {
        credits = 0;
        lastCreditsEarned = 0;
        ClearBackpack();
        shopStocks.Clear();

        runCurses.Clear();
        bonusDefense = 0;
        staticPerFight = staticFightsLeft = 0;
        battleStartOverclockTurns = 0;
        bossIntel = false;
        noRewardsNextRoom = noRewardsThisRoom = noRewardsNextFight = lastFightHadNoRewards = false;
        lockPickUses = -1;
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