using UnityEngine;

public class EventItemTestHelper : MonoBehaviour
{
    [SerializeField] InventoryGrid inventory;
    [SerializeField] ItemData testItem;

    [ContextMenu("TEST - Add One Item")]
    void AddOneItem()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            Debug.LogWarning("请先进入 Play 模式。");
            return;
        }

        if (!inventory)
        {
            inventory = Object.FindFirstObjectByType<InventoryGrid>(
                FindObjectsInactive.Include);
        }

        if (!inventory || !testItem)
        {
            Debug.LogWarning("请指定 Inventory 和 Test Item。");
            return;
        }

        if (!inventory.HasSpaceFor(testItem))
        {
            Debug.LogWarning("背包没有足够空间，请先腾出位置。");
            return;
        }

        bool added = inventory.TryAddItem(testItem);

        Debug.Log(added
            ? $"已添加测试物品：{testItem.itemName}"
            : "添加失败，请检查背包状态。");
#endif
    }
}