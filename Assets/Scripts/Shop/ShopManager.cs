using System;
using System.Collections.Generic;
using UnityEngine;

public class ShopManager : MonoBehaviour
{
    public enum BuyResult { Bought, NotEnoughCredits, BackpackFull, Unavailable }

    [Tooltip("Stock is saved under this name. Lobby shop = \"Lobby\". Dungeon shops will use the map node id.")]
    [SerializeField] string shopKey = "Lobby";
    [SerializeField] InventoryGrid inventory;               

    [Header("Stock")]
    [SerializeField] List<ItemData> itemPool = new List<ItemData>();
    [Tooltip("Their starting items are never sold in the shop.")]
    [SerializeField] List<CharacterData> characters = new List<CharacterData>();
    [SerializeField] int slotCount = 3;

    [Header("Prices")]
    [SerializeField] int upgradeCost = 50;
    [SerializeField] int sellPercent = 50;                  

    public List<ShopSlot> Slots { get; private set; } = new List<ShopSlot>();
    public ShopSlot PendingSlot { get; private set; }      
    public InventoryGrid Inventory => inventory;
    public int UpgradeCost => upgradeCost;
    public int Credits => RunState.credits;

    public event Action OnChanged;                          
    public void SetShopKey(string key) => shopKey = key;

    public void EnterShop()
    {
        if (!inventory) inventory = FindFirstObjectByType<InventoryGrid>(FindObjectsInactive.Include);

        // if same shop then same stock
        if (!RunState.shopStocks.TryGetValue(shopKey, out var stock))
        {
            stock = GenerateStock();
            RunState.shopStocks[shopKey] = stock;
        }
        Slots = stock;
        Changed();
    }

    List<ShopSlot> GenerateStock()
    {
        var excluded = new HashSet<ItemData>();
        foreach (var c in characters)
            if (c) foreach (var item in c.startingItems) if (item) excluded.Add(item);
        if (RunState.selectedCharacter)
            foreach (var item in RunState.selectedCharacter.startingItems) if (item) excluded.Add(item);

        var pool = new List<ItemData>();
        foreach (var item in itemPool)
            if (item && !excluded.Contains(item) && !pool.Contains(item)) pool.Add(item);

        int discount = DiscountPercent();
        var stock = new List<ShopSlot>();
        for (int i = 0; i < slotCount && pool.Count > 0; i++)
        {
            int r = UnityEngine.Random.Range(0, pool.Count);
            ItemData item = pool[r];
            pool.RemoveAt(r);

            int price = Mathf.RoundToInt(item.price * (100 - discount) / 100f);
            stock.Add(new ShopSlot(item, price));
        }
        return stock;
    }

    int DiscountPercent()
    {
        int total = 0;
        if (!inventory) return 0;
        foreach (var item in inventory.Items)
            if (item.data.passives != null)
                foreach (var p in item.data.passives)
                    if (p.type == PassiveType.ShopDiscountPercent) total += p.amount;
        return Mathf.Clamp(total, 0, 90);
    }

    public bool CanAfford(ShopSlot slot) => slot != null && Credits >= slot.price;

    public BuyResult Buy(ShopSlot slot)
    {
        if (slot == null || slot.sold || PendingSlot != null) return BuyResult.Unavailable;
        if (!CanAfford(slot)) return BuyResult.NotEnoughCredits;

        RunState.credits -= slot.price;
        slot.MarkSold();

        if (inventory && inventory.TryAddItem(slot.item))
        {
            Changed();
            return BuyResult.Bought;
        }

        PendingSlot = slot;
        Changed();
        return BuyResult.BackpackFull;
    }

    public bool TryPlacePending()
    {
        if (PendingSlot == null) return true;
        if (!inventory || !inventory.TryAddItem(PendingSlot.item)) return false;

        PendingSlot = null;
        Changed();
        return true;
    }

    public void LeavePendingItem()
    {
        if (PendingSlot == null) return;
        Refund(PendingSlot);
        PendingSlot = null;
        Changed();
    }

    public void Refund(ShopSlot slot)
    {
        if (slot == null || !slot.sold) return;
        RunState.credits += slot.price;
        slot.ResetSlot();
        Changed();
    }

    public int GetSellPrice(ItemInstance item) => item == null ? 0 : item.data.price * sellPercent / 100;

    public int Sell(ItemInstance item)
    {
        if (item == null || !inventory) return 0;
        int price = GetSellPrice(item);
        inventory.Discard(item);
        RunState.credits += price;
        Changed();
        return price;
    }

    public bool IsBackpackMaxSize => !inventory || !inventory.CanAddRow;
    public bool CanUpgrade => !IsBackpackMaxSize && Credits >= upgradeCost;

    public bool UpgradeBackpack()
    {
        if (!CanUpgrade) return false;
        RunState.credits -= upgradeCost;
        inventory.AddRow();
        Changed();
        return true;
    }

    void Changed() => OnChanged?.Invoke();
}