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

    [Header("Connect to the map later")]
    [SerializeField] UnityEvent onReturnToMap = new UnityEvent();

    bool ready;
    bool closed;

    void Start()
    {
        ready = eventPanel
            && titleText
            && descriptionText
            && statusText
            && payButton
            && tradeButton
            && leaveButton;

        if (!ready)
        {
            Debug.LogError(
                "BackpackTailorEvent: assign all Event UI fields.",
                this
            );

            if (payButton) payButton.interactable = false;
            if (tradeButton) tradeButton.interactable = false;
            if (leaveButton) leaveButton.interactable = false;
            return;
        }

        closed = false;
        eventPanel.SetActive(true);

        titleText.text = "Backpack Tailor";

        descriptionText.text =
            "A street vendor examines your gear rig.\n"
            + "\"Extra pockets, reinforced straps. "
            + "Pay in credits, or give me something worth salvaging.\"";

        statusText.text = "";

        payButton.gameObject.SetActive(true);
        tradeButton.gameObject.SetActive(true);
        leaveButton.gameObject.SetActive(true);

        payButton.onClick.AddListener(PayForRow);
        tradeButton.onClick.AddListener(TradeForRow);
        done = false;
        RefreshButtons();

        leaveButton.interactable = true;
        leaveButton.onClick.AddListener(Leave);


        SetLabel(leaveButton, "Leave");
    }

    // ---------- Upgrades ----------

    bool done;

    void Update()
    {
        if (ready && !closed && !done) RefreshButtons();
    }

    void RefreshButtons()
    {
        bool roomToGrow = inventory && inventory.CanAddRow;
        ItemInstance selected = inventory ? inventory.Selected : null;
        bool bigEnough = selected != null && RunEffects.Tiles(selected.data) >= 3;

        payButton.interactable = roomToGrow && RunState.credits >= payCost;
        SetLabel(payButton, !roomToGrow ? "Backpack is already the biggest it can be"
                          : $"Pay {payCost} credits - Add a backpack row (+{inventory.Columns} tiles)");

        tradeButton.interactable = roomToGrow && bigEnough;
        SetLabel(tradeButton, selected == null
            ? "Trade an item of 3+ tiles - open the backpack (0) and click one first"
            : bigEnough ? $"Trade {selected.data.itemName} - Add a backpack row (+{inventory.Columns} tiles)"
                        : $"{selected.data.itemName} is too small (needs 3+ tiles)");

        statusText.text = inventory ? $"Credits: {RunState.credits}     Backpack: {inventory.Columns} x {inventory.Rows}" : "";
    }

    public void PayForRow()
    {
        if (!ready || closed || done || !inventory || !inventory.CanAddRow || RunState.credits < payCost) return;
        RunState.credits -= payCost;
        inventory.AddRow();
        Finish($"Paid {payCost} credits. Your backpack gained a row.");
    }

    public void TradeForRow()
    {
        if (!ready || closed || done || !inventory || !inventory.CanAddRow) return;
        ItemInstance selected = inventory.Selected;
        if (selected == null || RunEffects.Tiles(selected.data) < 3) return;

        string name = selected.data.itemName;
        inventory.Discard(selected);
        inventory.AddRow();
        Finish($"The tailor strips your {name} for parts. Your backpack gained a row.");
    }

    void Finish(string message)
    {
        done = true;
        if (inventoryToggle) inventoryToggle.CloseInventory();
        descriptionText.text = message;
        statusText.text = $"Credits: {RunState.credits}     Backpack: {inventory.Columns} x {inventory.Rows}";
        payButton.gameObject.SetActive(false);
        tradeButton.gameObject.SetActive(false);
        SetLabel(leaveButton, "Continue");
    }

    public void Leave()
    {
        if (!ready || closed)
            return;

        closed = true;
        eventPanel.SetActive(false);

        // Without a map callback, this reveals the test scene.
        onReturnToMap.Invoke();
    }

    static void SetLabel(Button button, string label)
    {
        TMP_Text text =
            button.GetComponentInChildren<TMP_Text>(true);

        if (text)
            text.text = label;
    }
}