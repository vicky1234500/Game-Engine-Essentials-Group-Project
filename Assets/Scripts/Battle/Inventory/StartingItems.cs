using System.Collections.Generic;
using UnityEngine;

// Fills the backpack when a scene starts:
// - a backpack saved from the previous scene is rebuilt (items bought in the shop stay),
// - otherwise the character's starting items are added.
// Put on any object in the scene (e.g. next to the InventoryToggle on the inventory Canvas).
public class StartingItems : MonoBehaviour
{
    [SerializeField] InventoryGrid inventory;          // found automatically if empty
    [SerializeField] List<ItemData> items = new List<ItemData>();
    [Tooltip("Lobby only: picking another character swaps the backpack to that character's starting items.")]
    [SerializeField] bool refreshWhenCharacterChanges = false;
    [Tooltip("Credits when no character was picked (pressing Play straight in the Dungeon / BattleTest).")]
    [SerializeField] int creditsWithoutCharacter = 50;

    CharacterData lastCharacter;

    void Start()
    {
        // InventoryMenu starts hidden, so include inactive objects.
        if (!inventory) inventory = FindFirstObjectByType<InventoryGrid>(FindObjectsInactive.Include);
        if (!inventory) { Debug.LogWarning("StartingItems: no InventoryGrid in the scene."); return; }

        if (RunState.hasSavedBackpack)
        {
            inventory.RestoreFromRunState();
            lastCharacter = RunState.selectedCharacter;
        }
        else
        {
            AddStartingItems();
        }
    }

    void Update()
    {
        if (!refreshWhenCharacterChanges || !inventory) return;

        if (RunState.selectedCharacter != lastCharacter)
        {
            inventory.ClearAll();
            AddStartingItems();
        }
    }

    // Start of a run (or a new character picked in the Lobby): starting items + starting credits.
    void AddStartingItems()
    {
        lastCharacter = RunState.selectedCharacter;
        RunState.SetStartingCredits(creditsWithoutCharacter);

        // Character picked in the Lobby decides the starting items (otherwise use this list).
        var list = RunState.selectedCharacter ? RunState.selectedCharacter.startingItems : items;
        foreach (var item in list)
            if (item && !inventory.TryAddItem(item))
                Debug.LogWarning($"StartingItems: no space for {item.itemName}.");
    }
}