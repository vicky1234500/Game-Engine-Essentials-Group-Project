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

    [Header("Connect to exploration")]
    [SerializeField] UnityEvent onReturnToMap = new UnityEvent();

    bool ready;
    bool closed;

    void OnEnable()
    {
        ready = eventPanel
            && titleText
            && descriptionText
            && statusText
            && tradeButton
            && buyButton
            && leaveButton;

        if (!ready)
        {
            Debug.LogError(
                "BlackMarketEvent: assign all Event UI fields.",
                this
            );

            if (tradeButton) tradeButton.interactable = false;
            if (buyButton) buyButton.interactable = false;
            if (leaveButton) leaveButton.interactable = false;
            return;
        }

        closed = false;
        eventPanel.SetActive(true);

        titleText.text = "Black Market Dealer";

        descriptionText.text =
            "Behind a curtain of tangled cables, a dealer "
            + "displays unregistered equipment.\n"
            + "\"Upgrades, no questions asked.\"";

        statusText.text = "";

        tradeButton.gameObject.SetActive(true);
        buyButton.gameObject.SetActive(true);
        leaveButton.gameObject.SetActive(true);

        tradeButton.onClick.RemoveListener(TradeSelected);
        tradeButton.onClick.AddListener(TradeSelected);
        buyButton.onClick.RemoveListener(BuyCursed);
        buyButton.onClick.AddListener(BuyCursed);
        done = false;
        cursedOffer = PickCursedItem();
        RefreshButtons();

        leaveButton.interactable = true;

        // Prevent duplicate listeners when reopening the event.
        leaveButton.onClick.RemoveListener(Leave);
        leaveButton.onClick.AddListener(Leave);


        SetLabel(leaveButton, "Leave");
    }

    public void Leave()
    {
        if (!ready || closed)
            return;

        closed = true;
        eventPanel.SetActive(false);

        onReturnToMap.Invoke();
    }

    // ---------- Trading / buying ----------

    bool done;
    ItemData cursedOffer;

    void Update()
    {
        if (ready && !closed && !done) RefreshButtons();
    }

    void RefreshButtons()
    {
        ItemInstance selected = inventory ? inventory.Selected : null;
        bool canTrade = selected != null && TradeOptions(selected.data).Count > 0;
        tradeButton.interactable = canTrade;
        SetLabel(tradeButton, selected == null
            ? "Trade an item - open the backpack (0) and click one first"
            : canTrade ? $"Trade {selected.data.itemName} - same category, 1-2 tiles larger"
                       : $"No bigger {RunEffects.Category(selected.data)} item to trade {selected.data.itemName} for");

        int price = cursedOffer ? Mathf.Max(1, cursedOffer.price / 2) : 0;
        buyButton.interactable = cursedOffer && RunState.credits >= price;
        SetLabel(buyButton, cursedOffer
            ? $"Buy a random cursed item - {price} credits (half price)"
            : "No cursed items for sale");

        statusText.text = $"Credits: {RunState.credits}";
    }

    // Same category, 1-2 tiles bigger, and it must fit a 3-wide backpack.
    System.Collections.Generic.List<ItemData> TradeOptions(ItemData item)
    {
        var list = new System.Collections.Generic.List<ItemData>();
        int tiles = RunEffects.Tiles(item);
        var category = RunEffects.Category(item);
        foreach (var other in ItemDatabase.Pool())
        {
            int t = RunEffects.Tiles(other);
            if (other.itemName == item.itemName || t < tiles + 1 || t > tiles + 2) continue;
            if (RunEffects.Category(other) != category) continue;
            if (inventory && (other.size.x > inventory.Columns && other.size.y > inventory.Columns)) continue;
            list.Add(other);
        }
        return list;
    }

    ItemData PickCursedItem()
    {
        var cursed = ItemDatabase.Pool().FindAll(RunEffects.HasCurseCard);
        return cursed.Count > 0 ? cursed[Random.Range(0, cursed.Count)] : null;
    }

    public void TradeSelected()
    {
        if (!ready || closed || done || !inventory || inventory.Selected == null) return;
        ItemInstance selected = inventory.Selected;
        var options = TradeOptions(selected.data);
        if (options.Count == 0) return;

        var newItem = options[Random.Range(0, options.Count)];
        string oldName = selected.data.itemName;
        inventory.Discard(selected);
        Finish($"The dealer takes your {oldName} and slides over something bigger.\n"
               + RunEffects.GiveItem(inventory, newItem));
    }

    public void BuyCursed()
    {
        if (!ready || closed || done || !cursedOffer) return;
        int price = Mathf.Max(1, cursedOffer.price / 2);
        if (RunState.credits < price) return;

        RunState.credits -= price;
        Finish($"\"No refunds.\" Paid {price} credits.\n" + RunEffects.GiveItem(inventory, cursedOffer));
    }

    void Finish(string message)
    {
        done = true;
        if (inventoryToggle) inventoryToggle.CloseInventory();
        descriptionText.text = message;
        statusText.text = $"Credits: {RunState.credits}";
        tradeButton.gameObject.SetActive(false);
        buyButton.gameObject.SetActive(false);
        SetLabel(leaveButton, "Continue");
    }

    void OnDisable()
    {
        if (leaveButton)
            leaveButton.onClick.RemoveListener(Leave);
    }

    static void SetLabel(Button button, string label)
    {
        TMP_Text text =
            button.GetComponentInChildren<TMP_Text>(true);

        if (text)
            text.text = label;
    }
}