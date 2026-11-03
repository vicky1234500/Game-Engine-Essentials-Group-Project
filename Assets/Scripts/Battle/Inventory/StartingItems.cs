using System.Collections.Generic;
using UnityEngine;

// Puts the starting items into the backpack when the game starts.
// Doc: Character 1 starts with Knife, Bandage (and Lock Pick later).
// Put on any object in the scene (e.g. next to the InventoryToggle on the inventory Canvas).
public class StartingItems : MonoBehaviour
{
    [SerializeField] InventoryGrid inventory;          // found automatically if empty
    [SerializeField] List<ItemData> items = new List<ItemData>();

    void Start()
    {
        // InventoryMenu starts hidden, so include inactive objects.
        if (!inventory) inventory = FindFirstObjectByType<InventoryGrid>(FindObjectsInactive.Include);
        if (!inventory) { Debug.LogWarning("StartingItems: no InventoryGrid in the scene."); return; }

        // Character picked in the Lobby decides the starting items (otherwise use this list).
        var list = RunState.selectedCharacter ? RunState.selectedCharacter.startingItems : items;
        foreach (var item in list)
            if (item && !inventory.TryAddItem(item))
                Debug.LogWarning($"StartingItems: no space for {item.itemName}.");
    }
}
