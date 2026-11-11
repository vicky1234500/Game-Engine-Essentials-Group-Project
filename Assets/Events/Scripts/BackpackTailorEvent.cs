using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class BackpackTailorEvent : MonoBehaviour
{
    [Header("Event UI")]
    [SerializeField] GameObject eventPanel;
    [SerializeField] TMP_Text titleText;
    [SerializeField] TMP_Text descriptionText;
    [SerializeField] TMP_Text statusText;
    [SerializeField] Button payButton;
    [SerializeField] Button tradeButton;
    [SerializeField] Button leaveButton;

    [Header("Backpack (set by the EventExplorationBridge)")]
    [SerializeField] InventoryGrid inventory;
    [SerializeField] InventoryToggle inventoryToggle;

    [Min(0)]
    [SerializeField] int payCost = 50;

    [Header("Connect to exploration")]
    [SerializeField] UnityEvent onReturnToMap = new UnityEvent();

    bool ready;
    bool closed;
    bool done;
    bool choosingItem;

    // Closing the backpack clears Inventory.Selected.
    // Keep the selected item here until the player confirms.
    ItemInstance tradeItem;

    const string Intro =
        "A street vendor examines your gear rig.\n"
        + "\"Extra pockets, reinforced straps. "
        + "Pay in credits, or give me something worth salvaging.\"\n"
        + "Sealed delivery packages cannot be traded.";

    void OnEnable()
    {
        ready = eventPanel && titleText && descriptionText
            && statusText && payButton && tradeButton && leaveButton;

        if (!ready)
        {
            Debug.LogError(
                "BackpackTailorEvent: assign all Event UI fields.",
                this);
            return;
        }

        closed = false;
        done = false;
        choosingItem = false;
        tradeItem = null;
        payCost = Mathf.Max(0, payCost);

        payButton.onClick.RemoveListener(PayForRow);
        payButton.onClick.AddListener(PayForRow);

        tradeButton.onClick.RemoveListener(TradeForRow);
        tradeButton.onClick.AddListener(TradeForRow);

        leaveButton.onClick.RemoveListener(Leave);
        leaveButton.onClick.AddListener(Leave);

        eventPanel.SetActive(true);
        titleText.text = "Backpack Tailor";
        descriptionText.text = Intro;

        payButton.gameObject.SetActive(true);
        tradeButton.gameObject.SetActive(true);
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
        {
            tradeItem = null;
            descriptionText.text = "That item is no longer in your backpack.";
        }

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

    static bool IsSealedPackage(ItemData item)
    {
        return item
            && (NormalizeName(item.name) == "sealedpackage"
                || NormalizeName(item.itemName) == "sealedpackage");
    }

    bool CanTrade(ItemInstance item)
    {
        return StillInBackpack(item)
            && item.data
            && !IsSealedPackage(item.data)
            && RunEffects.Tiles(item.data) >= 3;
    }

    bool HasTradeableItem()
    {
        if (!inventory)
            return false;

        foreach (ItemInstance item in inventory.Items)
        {
            if (CanTrade(item))
                return true;
        }

        return false;
    }

    void RefreshButtons()
    {
        statusText.text = inventory
            ? $"Credits: {RunState.credits}     "
              + $"Backpack: {inventory.Columns} x {inventory.Rows}"
            : $"Credits: {RunState.credits}";

        if (!inventory)
        {
            payButton.interactable = false;
            tradeButton.interactable = false;
            SetLabel(payButton, "Backpack not connected");
            SetLabel(tradeButton, "Backpack not connected");
            return;
        }

        if (!inventory.CanAddRow)
        {
            payButton.interactable = false;
            tradeButton.interactable = false;
            SetLabel(payButton, "Backpack is already at maximum size");
            SetLabel(tradeButton, "Backpack is already at maximum size");
            return;
        }

        payButton.interactable = RunState.credits >= payCost;
        SetLabel(payButton, RunState.credits >= payCost
            ? $"Pay {payCost} credits - Add a row (+{inventory.Columns} tiles)"
            : $"Need {payCost} credits - Not enough credits");

        if (!inventoryToggle)
        {
            tradeButton.interactable = false;
            SetLabel(tradeButton, "Backpack controls not connected");
            return;
        }

        if (tradeItem != null && CanTrade(tradeItem))
        {
            tradeButton.interactable = true;
            SetLabel(tradeButton,
                $"Confirm trade: {tradeItem.data.itemName}"
                + $" - Add a row (+{inventory.Columns} tiles)");
            return;
        }

        tradeButton.interactable = HasTradeableItem();
        SetLabel(tradeButton, tradeButton.interactable
            ? "Trade an item of 3+ tiles - Choose from backpack"
            : "Need an eligible item occupying 3 or more tiles");
    }

    void BeginItemSelection()
    {
        if (!inventory || !inventoryToggle || !inventory.CanAddRow)
            return;

        tradeItem = null;
        inventory.Deselect();
        choosingItem = true;

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
            // Save before CloseInventory clears the selection.
            tradeItem = selected;
            ReturnFromSelection();

            if (!tradeItem.data)
            {
                tradeItem = null;
                descriptionText.text = "Invalid item. Choose another item.";
            }
            else if (IsSealedPackage(tradeItem.data))
            {
                tradeItem = null;
                descriptionText.text =
                    "Sealed delivery packages cannot be traded.\n"
                    + "Choose another item.";
            }
            else if (RunEffects.Tiles(tradeItem.data) < 3)
            {
                tradeItem = null;
                descriptionText.text =
                    "This item is too small.\n"
                    + "Choose an item occupying at least 3 tiles.";
            }
            else
            {
                descriptionText.text =
                    $"Selected: {tradeItem.data.itemName}\n"
                    + $"Confirm to consume this item and add "
                    + $"one backpack row (+{inventory.Columns} tiles).\n"
                    + "No credits will be charged.";
            }

            RefreshButtons();
            return;
        }

        // Pressing 0 to close without choosing cancels selection.
        if (!inventoryToggle.IsOpen)
        {
            tradeItem = null;
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

    public void PayForRow()
    {
        if (!ready || closed || done || choosingItem)
            return;

        if (!inventory || !inventory.CanAddRow
            || RunState.credits < payCost)
            return;

        if (!inventory.AddRow())
            return;

        RunState.credits -= payCost;

        Finish(
            $"Paid {payCost} credits.\n"
            + $"Your backpack gained a row (+{inventory.Columns} tiles).");
    }

    public void TradeForRow()
    {
        if (!ready || closed || done || choosingItem)
            return;

        if (!inventory || !inventory.CanAddRow)
            return;

        if (tradeItem == null || !CanTrade(tradeItem))
        {
            BeginItemSelection();
            return;
        }

        string itemName = tradeItem.data.itemName;

        // Only consume the item if the expansion succeeds.
        if (!inventory.AddRow())
            return;

        inventory.Discard(tradeItem);
        tradeItem = null;

        Finish(
            $"The tailor takes your {itemName} for parts.\n"
            + $"Your backpack gained a row (+{inventory.Columns} tiles).\n"
            + "No credits were charged.");
    }

    void Finish(string message)
    {
        done = true;
        choosingItem = false;

        if (inventoryToggle)
            inventoryToggle.CloseInventory();

        eventPanel.SetActive(true);
        descriptionText.text = message;
        statusText.text =
            $"Credits: {RunState.credits}     "
            + $"Backpack: {inventory.Columns} x {inventory.Rows}";

        payButton.gameObject.SetActive(false);
        tradeButton.gameObject.SetActive(false);
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
        tradeItem = null;

        if (inventoryToggle)
            inventoryToggle.CloseInventory();

        eventPanel.SetActive(false);
        onReturnToMap.Invoke();
    }

    void OnDisable()
    {
        if (payButton)
            payButton.onClick.RemoveListener(PayForRow);

        if (tradeButton)
            tradeButton.onClick.RemoveListener(TradeForRow);

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