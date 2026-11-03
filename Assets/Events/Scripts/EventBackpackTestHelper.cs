using UnityEngine;

// Temporary manual test tool. Remove this component when testing is finished.
public class EventBackpackTestHelper : MonoBehaviour
{
    [SerializeField] InventoryGrid inventory;
    [SerializeField] ItemData fillerItem;

    [ContextMenu("Fill Backpack (Play Mode Only)")]
    public void FillBackpack()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            Debug.LogWarning("Enter Play mode before filling the backpack.", this);
            return;
        }
        if (!inventory || !fillerItem || fillerItem.size != Vector2Int.one)
        {
            Debug.LogWarning("Assign Inventory and a 1 x 1 Filler Item.", this);
            return;
        }
        int added = 0;
        while (added < 512 && inventory.HasSpaceFor(fillerItem))
        {
            if (!inventory.TryAddItem(fillerItem)) break;
            added++;
        }
        Debug.Log($"Backpack test: added {added} items. Full: {!inventory.HasSpaceFor(fillerItem)}", this);
#endif
    }
}
