using System.Text;
using UnityEngine;
using UnityEngine.UI;

public class ShopUI : MonoBehaviour
{
    [SerializeField] ShopManager shop;
    [SerializeField] InventoryToggle inventoryToggle;      

    [Header("Panels")]
    [SerializeField] GameObject shopPanel;                 
    [SerializeField] GameObject fullPopup;                 
    [SerializeField] GameObject sellModeBar;               
    [SerializeField] GameObject sellConfirmPanel;          

    [Header("Slots (3 of each)")]
    [SerializeField] Text[] slotTexts;                     
    [SerializeField] Text[] priceTexts;
    [SerializeField] Button[] buyButtons;
    [SerializeField] Text[] buyButtonTexts;

    [Header("Texts")]
    [SerializeField] Text creditsText;
    [SerializeField] Text messageText;
    [SerializeField] Text fullPopupText;
    [SerializeField] Text sellConfirmText;
    [SerializeField] Text upgradeButtonText;

    [Header("Buttons")]
    [SerializeField] Button sellButton;
    [SerializeField] Button upgradeButton;
    [SerializeField] Button leaveButton;
    [SerializeField] Button placeItemButton;               
    [SerializeField] Button leaveItemButton;               
    [SerializeField] Button doneSellingButton;             
    [SerializeField] Button confirmSellButton;
    [SerializeField] Button cancelSellButton;

    [Tooltip("\"Press E to open the shop\" text. Shop counters inside room prefabs use this one.")]
    [SerializeField] Text promptText;
    public Text PromptText => promptText;

    [Header("Turned off while the shop is open")]
    [SerializeField] Behaviour[] disableWhileOpen;         
    [SerializeField] bool lockCursorOnClose = true;

    public bool IsOpen { get; private set; }

    ItemInstance sellCandidate;

    void Start()
    {
        for (int i = 0; i < buyButtons.Length; i++)
        {
            int index = i;                                  
            buyButtons[i].onClick.AddListener(() => OnBuyClicked(index));
        }
        if (sellButton) sellButton.onClick.AddListener(StartSelling);
        if (upgradeButton) upgradeButton.onClick.AddListener(OnUpgradeClicked);
        if (leaveButton) leaveButton.onClick.AddListener(Leave);
        if (placeItemButton) placeItemButton.onClick.AddListener(OnPlaceItemClicked);
        if (leaveItemButton) leaveItemButton.onClick.AddListener(OnLeaveItemClicked);
        if (doneSellingButton) doneSellingButton.onClick.AddListener(StopSelling);
        if (confirmSellButton) confirmSellButton.onClick.AddListener(OnConfirmSell);
        if (cancelSellButton) cancelSellButton.onClick.AddListener(OnCancelSell);

        // The upgrade button is saved as hidden in the scene, and nothing else turns it on.
        if (upgradeButton) upgradeButton.gameObject.SetActive(true);

        shop.OnChanged += Refresh;
        HideAll();
    }

    void Update()
    {
        if (!IsOpen) return;

        // Closing the backpack locks the cursor. MouseLook normally undoes that every frame,
        // but it is turned off while the shop is open, so the shop has to keep the cursor free.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        // The backpack's own Back button closes the backpack without telling the shop.
        // If that happens while selling, finish selling so the shop window comes back.
        if (sellModeBar && sellModeBar.activeSelf && inventoryToggle && !inventoryToggle.IsOpen)
            StopSelling();
    }

    void OnDestroy()
    {
        if (shop) shop.OnChanged -= Refresh;
    }

    //press E to open shop.
    public void Open()
    {
        IsOpen = true;
        shop.EnterShop();
        HideAll();
        shopPanel.SetActive(true);
        Say("");
        SetControls(false);
        Refresh();
    }

    public void Leave()
    {
        if (shop.PendingSlot != null) shop.LeavePendingItem();   
        StopSellingQuietly();
        if (inventoryToggle) inventoryToggle.SetOpen(false);

        HideAll();
        IsOpen = false;
        SetControls(true);
        if (lockCursorOnClose)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    void HideAll()
    {
        if (shopPanel) shopPanel.SetActive(false);
        if (fullPopup) fullPopup.SetActive(false);
        if (sellModeBar) sellModeBar.SetActive(false);
        if (sellConfirmPanel) sellConfirmPanel.SetActive(false);
    }

    void SetControls(bool on)
    {
        foreach (var b in disableWhileOpen) if (b) b.enabled = on;
        if (!on)
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }

    void ShowBackpack(bool show)
    {
        if (inventoryToggle) inventoryToggle.SetOpen(show);
        SetControls(false);
    }

    void Refresh()
    {
        if (!IsOpen) return;

        if (creditsText) creditsText.text = $"Credits: {shop.Credits}";

        for (int i = 0; i < buyButtons.Length; i++)
        {
            bool used = i < shop.Slots.Count;
            buyButtons[i].gameObject.SetActive(used);
            if (i < slotTexts.Length && slotTexts[i]) slotTexts[i].gameObject.SetActive(used);
            if (i < priceTexts.Length && priceTexts[i]) priceTexts[i].gameObject.SetActive(used);
            if (!used) continue;

            ShopSlot slot = shop.Slots[i];
            if (i < slotTexts.Length && slotTexts[i]) slotTexts[i].text = Describe(slot.item);

            string price, button;
            bool canBuy;
            if (slot.sold)
            {
                price = "SOLD"; button = "SOLD"; canBuy = false;
            }
            else if (!shop.CanAfford(slot))
            {
                price = $"{slot.price} credits\nNot enough credits"; button = "Buy"; canBuy = false;
            }
            else
            {
                price = $"{slot.price} credits"; button = "Buy"; canBuy = true;
            }

            if (i < priceTexts.Length && priceTexts[i]) priceTexts[i].text = price;
            if (i < buyButtonTexts.Length && buyButtonTexts[i]) buyButtonTexts[i].text = button;
            buyButtons[i].interactable = canBuy && shop.PendingSlot == null;
        }

        if (upgradeButton) upgradeButton.interactable = shop.CanUpgrade;
        if (upgradeButtonText)
        {
            if (shop.IsBackpackMaxSize) upgradeButtonText.text = "Backpack at max size";
            else upgradeButtonText.text = $"Upgrade Backpack (+1 row)\n{shop.UpgradeCost} credits";
        }

        if (sellButton) sellButton.interactable = shop.Inventory && shop.Inventory.Items.Count > 0;
    }

    static string Describe(ItemData item)
    {
        var sb = new StringBuilder();
        sb.Append($"<b>{item.itemName}</b>\n");
        sb.Append($"Size {item.size.x} x {item.size.y}\n");
        foreach (var e in item.cards)
            if (e.card) sb.Append($"{e.count}x {e.card.cardName}\n");
        if (item.passives != null)
            foreach (var p in item.passives)
                sb.Append($"Passive: {p.type} +{p.amount}\n");
        return sb.ToString().TrimEnd();
    }

    void Say(string msg)
    {
        if (messageText) messageText.text = msg;
    }

    void OnBuyClicked(int index)
    {
        if (index >= shop.Slots.Count) return;
        ShopSlot slot = shop.Slots[index];

        switch (shop.Buy(slot))
        {
            case ShopManager.BuyResult.Bought:
                Say($"Bought {slot.item.itemName}! It's in your backpack.");
                break;

            case ShopManager.BuyResult.NotEnoughCredits:
                Say("Not enough credits");
                break;

            case ShopManager.BuyResult.BackpackFull:
                shopPanel.SetActive(false);
                ShowBackpack(true);
                if (fullPopupText)
                    fullPopupText.text = $"No space for {slot.item.itemName} ({slot.item.size.x} x {slot.item.size.y}).\n" +
                                         "Move, rotate or trash items, then press Place item.\nOr leave it and get your credits back.";
                fullPopup.SetActive(true);
                break;
        }
        Refresh();
    }

    void OnPlaceItemClicked()
    {
        string name = shop.PendingSlot != null ? shop.PendingSlot.item.itemName : "";
        if (shop.TryPlacePending())
        {
            CloseFullPopup();
            Say($"{name} added to your backpack!");
        }
        else if (fullPopupText)
        {
            fullPopupText.text = $"Still no space for {name}.\nMake more room, or leave it.";
        }
    }

    void OnLeaveItemClicked()
    {
        string name = shop.PendingSlot != null ? shop.PendingSlot.item.itemName : "";
        int price = shop.PendingSlot != null ? shop.PendingSlot.price : 0;
        shop.LeavePendingItem();
        CloseFullPopup();
        Say($"Left {name}. {price} credits refunded.");
    }

    void CloseFullPopup()
    {
        fullPopup.SetActive(false);
        ShowBackpack(false);
        shopPanel.SetActive(true);
        Refresh();
    }

    void StartSelling()
    {
        if (!shop.Inventory) return;
        shopPanel.SetActive(false);
        shop.Inventory.SellMode = true;
        shop.Inventory.Deselect();
        shop.Inventory.OnSellRequested += OnSellRequested;
        ShowBackpack(true);
        sellModeBar.SetActive(true);
    }

    void StopSelling()
    {
        StopSellingQuietly();
        ShowBackpack(false);
        shopPanel.SetActive(true);
        Refresh();
    }

    void StopSellingQuietly()
    {
        if (shop.Inventory)
        {
            shop.Inventory.SellMode = false;
            shop.Inventory.OnSellRequested -= OnSellRequested;
        }
        sellCandidate = null;
        if (sellModeBar) sellModeBar.SetActive(false);
        if (sellConfirmPanel) sellConfirmPanel.SetActive(false);
    }

    void OnSellRequested(ItemInstance item)
    {
        sellCandidate = item;
        if (sellConfirmText)
            sellConfirmText.text = $"Sell {item.data.itemName} for {shop.GetSellPrice(item)} credits?\n" +
                                   $"Its {item.data.TotalCards} cards will be removed from your deck.";
        sellConfirmPanel.SetActive(true);
    }

    void OnConfirmSell()
    {
        if (sellCandidate == null) return;
        string name = sellCandidate.data.itemName;
        int cards = sellCandidate.data.TotalCards;
        int price = shop.Sell(sellCandidate);
        sellCandidate = null;
        sellConfirmPanel.SetActive(false);
        Say($"Sold {name} for {price} credits ({cards} cards removed from your deck).");
        Debug.Log($"[Shop] Sold {name}. Deck now has {shop.Inventory.BuildDeck().Count} cards.");
    }

    void OnCancelSell()
    {
        sellCandidate = null;
        sellConfirmPanel.SetActive(false);
    }

    void OnUpgradeClicked()
    {
        if (shop.UpgradeBackpack())
            Say($"Backpack upgraded! Now {shop.Inventory.Columns} x {shop.Inventory.Rows}.");
        else if (shop.IsBackpackMaxSize)
            Say("Backpack is already at max size.");
        else
            Say("Not enough credits");
    }
}