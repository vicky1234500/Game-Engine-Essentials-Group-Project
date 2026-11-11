using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class BlackMarketEvent : MonoBehaviour
{
    [Header("Event UI")]
    [SerializeField] GameObject eventPanel;
    [SerializeField] TMP_Text titleText;
    [SerializeField] TMP_Text descriptionText;
    [SerializeField] TMP_Text statusText;
    [SerializeField] Button tradeButton;
    [SerializeField] Button buyButton;
    [SerializeField] Button leaveButton;

    [Header("Backpack (set by the EventExplorationBridge)")]
    [SerializeField] InventoryGrid inventory;
    [SerializeField] InventoryToggle inventoryToggle;

    [Header("Quest item - cannot be traded")]
    [SerializeField] ItemData sealedPackage;

    [Header("Connect to exploration")]
    [SerializeField] UnityEvent onReturnToMap = new UnityEvent();

    bool ready;
    bool closed;
    bool done;
    bool choosingItem;

    ItemData cursedOffer;

    // Keep our own reference: closing the backpack clears Inventory.Selected.
    ItemInstance tradeItem;

    const string Intro =
        "Behind a curtain of tangled cables, a dealer "
        + "displays unregistered equipment.\n"
        + "\"Upgrades, no questions asked.\"\n"
        + "Sealed delivery packages cannot be traded.";

    void OnEnable()
    {
        ready = eventPanel && titleText && descriptionText
            && statusText && tradeButton && buyButton && leaveButton;

        if (!ready)
        {
            Debug.LogError("BlackMarketEvent: missing Event UI references.", this);
            return;
        }

        closed = false;
        done = false;
        choosingItem = false;
        tradeItem = null;
        cursedOffer = PickCursedItem();

        tradeButton.onClick.RemoveListener(TradeSelected);
        tradeButton.onClick.AddListener(TradeSelected);
        buyButton.onClick.RemoveListener(BuyCursed);
        buyButton.onClick.AddListener(BuyCursed);
        leaveButton.onClick.RemoveListener(Leave);
        leaveButton.onClick.AddListener(Leave);

        eventPanel.SetActive(true);
        titleText.text = "Black Market Dealer";
        descriptionText.text = Intro;

        tradeButton.gameObject.SetActive(true);
        buyButton.gameObject.SetActive(true);
        leaveButton.gameObject.SetActive(true);
        leaveButton.interactable = true;
        SetLabel(leaveButton, "Leave");

        RefreshButtons();
    }

    void Update()
    {
        if (!ready || closed || done)
            return;

        if (choosingItem)
        {
            UpdateItemSelection();
            return;
        }

        if (tradeItem != null && !StillInBackpack(tradeItem))
            tradeItem = null;

        RefreshButtons();
    }

    bool StillInBackpack(ItemInstance item)
    {
        if (!inventory || item == null)
            return false;

        foreach (ItemInstance existing in inventory.Items)
        {
            if (existing == item)
                return true;
        }

        return false;
    }

    static string NormalizeName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "";

        return value.Replace("(Clone)", "")
            .Replace("(Run)", "")
            .Replace(" ", "")
            .Replace("\t", "")
            .Replace("_", "")
            .Replace("-", "")
            .Trim()
            .ToLowerInvariant();
    }

    bool IsSealedPackage(ItemData item)
    {
        if (!item)
            return false;

        if (sealedPackage && item == sealedPackage)
            return true;

        return NormalizeName(item.name) == "sealedpackage"
            || NormalizeName(item.itemName) == "sealedpackage";
    }

    int OfferPrice()
    {
        return cursedOffer ? Mathf.Max(1, cursedOffer.price / 2) : 0;
    }

    void RefreshButtons()
    {
        bool hasBackpack = inventory && inventoryToggle;

        if (tradeItem == null)
        {
            tradeButton.interactable = hasBackpack;
            SetLabel(tradeButton, hasBackpack
                ? "Trade an item - Choose from backpack"
                : "Trade unavailable - Backpack not connected");
        }
        else
        {
            bool valid = !IsSealedPackage(tradeItem.data)
                && TradeOptions(tradeItem.data).Count > 0;

            tradeButton.interactable = hasBackpack;
            SetLabel(tradeButton, valid
                ? $"Confirm trade: {tradeItem.data.itemName}"
                  + " - same category, 1-2 tiles larger"
                : "Choose another item");
        }

        int price = OfferPrice();
        buyButton.interactable =
            cursedOffer && RunState.credits >= price;

        SetLabel(buyButton, !cursedOffer
            ? "No cursed items for sale"
            : RunState.credits >= price
                ? $"Buy a random cursed item - {price} credits (half price)"
                : $"Need {price} credits - Not enough credits");

        statusText.text = $"Credits: {RunState.credits}";
    }

    void BeginItemSelection()
    {
        if (!inventory || !inventoryToggle)
            return;

        tradeItem = null;
        inventory.Deselect();
        choosingItem = true;

        // Reveal the existing backpack without adding any new UI.
        eventPanel.SetActive(false);
        inventoryToggle.OpenInventory();
    }

    void UpdateItemSelection()
    {
        if (!inventory || !inventoryToggle)
        {
            ReturnFromSelection();
            descriptionText.text = "Backpack connection is missing.";
            return;
        }

        ItemInstance selected = inventory.Selected;

        if (selected != null && StillInBackpack(selected))
        {
            // Save it BEFORE closing the backpack clears the selection.
            tradeItem = selected;
            ReturnFromSelection();

            if (IsSealedPackage(tradeItem.data))
            {
                tradeItem = null;
                descriptionText.text =
                    "This is a sealed delivery package.\n"
                    + "It cannot be traded. Choose another item.";
            }
            else if (TradeOptions(tradeItem.data).Count == 0)
            {
                string itemName = tradeItem.data.itemName;
                tradeItem = null;
                descriptionText.text =
                    $"No suitable larger item is available for {itemName}.\n"
                    + "Choose another item.";
            }
            else
            {
                descriptionText.text =
                    $"Selected: {tradeItem.data.itemName}\n"
                    + "Confirm to exchange it for a random item of the "
                    + "same category, occupying 1-2 more tiles.\n"
                    + "The selected item will be consumed.";
            }

            RefreshButtons();
            return;
        }

        // Closing the backpack without selecting an item cancels selection.
        if (!inventoryToggle.IsOpen)
        {
            ReturnFromSelection();
            descriptionText.text = Intro;
        }
    }

    void ReturnFromSelection()
    {
        choosingItem = false;

        if (inventoryToggle)
            inventoryToggle.CloseInventory();

        eventPanel.SetActive(true);
        RefreshButtons();
    }

    List<ItemData> TradeOptions(ItemData item)
    {
        var options = new List<ItemData>();

        if (!item || IsSealedPackage(item))
            return options;

        int tiles = RunEffects.Tiles(item);
        var category = RunEffects.Category(item);

        foreach (ItemData other in ItemDatabase.Pool())
        {
            if (!other || IsSealedPackage(other))
                continue;

            if (other == item || other.itemName == item.itemName)
                continue;

            int otherTiles = RunEffects.Tiles(other);

            if (otherTiles < tiles + 1 || otherTiles > tiles + 2)
                continue;

            if (RunEffects.Category(other) != category)
                continue;

            if (inventory
                && other.size.x > inventory.Columns
                && other.size.y > inventory.Columns)
            {
                continue;
            }

            if (!options.Contains(other))
                options.Add(other);
        }

        return options;
    }

    ItemData PickCursedItem()
    {
        var options = new List<ItemData>();

        foreach (ItemData item in ItemDatabase.Pool())
        {
            if (!item || IsSealedPackage(item))
                continue;

            if (RunEffects.HasCurseCard(item) && !options.Contains(item))
                options.Add(item);
        }

        return options.Count > 0
            ? options[Random.Range(0, options.Count)]
            : null;
    }

    public void TradeSelected()
    {
        if (!ready || closed || done || choosingItem)
            return;

        if (tradeItem == null || !StillInBackpack(tradeItem))
        {
            BeginItemSelection();
            return;
        }

        if (IsSealedPackage(tradeItem.data))
        {
            tradeItem = null;
            descriptionText.text = "Sealed delivery packages cannot be traded.";
            RefreshButtons();
            return;
        }

        List<ItemData> options = TradeOptions(tradeItem.data);

        if (options.Count == 0)
        {
            BeginItemSelection();
            return;
        }

        ItemData reward = options[Random.Range(0, options.Count)];
        string oldName = tradeItem.data.itemName;

        done = true;
        inventory.Discard(tradeItem);
        tradeItem = null;

        Finish(
            $"The dealer takes your {oldName}.\n"
            + RunEffects.GiveItem(inventory, reward));
    }

    public void BuyCursed()
    {
        if (!ready || closed || done || choosingItem || !cursedOffer)
            return;

        if (IsSealedPackage(cursedOffer))
            return;

        int price = OfferPrice();

        if (RunState.credits < price)
            return;

        done = true;
        RunState.credits -= price;

        Finish(
            $"\"No refunds.\" Paid {price} credits.\n"
            + RunEffects.GiveItem(inventory, cursedOffer));
    }

    void Finish(string message)
    {
        done = true;

        if (inventoryToggle)
            inventoryToggle.CloseInventory();

        eventPanel.SetActive(true);
        descriptionText.text = message;
        statusText.text = $"Credits: {RunState.credits}";

        tradeButton.gameObject.SetActive(false);
        buyButton.gameObject.SetActive(false);
        leaveButton.gameObject.SetActive(true);
        leaveButton.interactable = true;
        SetLabel(leaveButton, "Continue");
    }

    public void Leave()
    {
        if (!ready || closed)
            return;

        closed = true;
        choosingItem = false;

        if (inventoryToggle)
            inventoryToggle.CloseInventory();

        eventPanel.SetActive(false);
        onReturnToMap.Invoke();
    }

    void OnDisable()
    {
        if (tradeButton)
            tradeButton.onClick.RemoveListener(TradeSelected);
        if (buyButton)
            buyButton.onClick.RemoveListener(BuyCursed);
        if (leaveButton)
            leaveButton.onClick.RemoveListener(Leave);
    }

    static void SetLabel(Button button, string label)
    {
        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);

        if (text)
            text.text = label;
    }
}