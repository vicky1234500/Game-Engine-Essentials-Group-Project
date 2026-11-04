using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class EquipmentMiniaturizerEvent : MonoBehaviour
{
    [Header("Event UI")]
    [SerializeField] GameObject eventPanel;
    [SerializeField] TMP_Text titleText;
    [SerializeField] TMP_Text descriptionText;
    [SerializeField] TMP_Text statusText;
    [SerializeField] Button shrinkButton;
    [SerializeField] Button amplifyButton;
    [SerializeField] Button leaveButton;

    [Header("Existing backpack")]
    [SerializeField] InventoryGrid inventory;
    [SerializeField] InventoryToggle inventoryToggle;

    [Header("Approved attack item assets")]
    [SerializeField] ItemData[] attackItems;

    [Header("Prototype starting values")]
    [Min(0)]
    [SerializeField] int startingCredits = 75;

    [Header("Modification cost")]
    [Min(0)]
    [SerializeField] int modificationCost = 30;

    [Header("Connect to the map later")]
    [SerializeField] UnityEvent onReturnToMap = new UnityEvent();

    enum Phase
    {
        Choice,
        Selecting,
        Preview,
        Result,
        Closed
    }

    Phase phase;
    bool ready;
    bool enlarge;
    int credits;
    ItemInstance target;

    public int Credits => credits;

    void Start()
    {
        ready = eventPanel
            && titleText
            && descriptionText
            && statusText
            && shrinkButton
            && amplifyButton
            && leaveButton
            && inventory
            && inventoryToggle;

        if (!ready)
        {
            Debug.LogError(
                "EquipmentMiniaturizerEvent: assign all UI "
                + "and backpack fields.",
                this
            );

            if (shrinkButton) shrinkButton.interactable = false;
            if (amplifyButton) amplifyButton.interactable = false;
            if (leaveButton) leaveButton.interactable = false;
            return;
        }

        credits = Mathf.Max(0, startingCredits);
        modificationCost = Mathf.Max(0, modificationCost);

        shrinkButton.onClick.AddListener(FirstButton);
        amplifyButton.onClick.AddListener(SecondButton);
        leaveButton.onClick.AddListener(BackOrLeave);

        inventoryToggle.CloseInventory();

        titleText.text = "Equipment Miniaturizer";
        ShowChoices();

        if (attackItems == null || attackItems.Length == 0)
        {
            Debug.LogWarning(
                "Assign approved attack item assets to Attack Items.",
                this
            );
        }
    }

    void Update()
    {
        if (!ready) return;

        if (phase == Phase.Choice)
        {
            RefreshChoiceButtons();
        }
        else if (phase == Phase.Selecting)
        {
            // Closing the backpack without selecting cancels selection.
            if (!inventoryToggle.IsOpen)
            {
                ShowChoices("Selection cancelled. No credits spent.");
                return;
            }

            ItemInstance selected = inventory.Selected;

            if (selected == null)
                return;

            if (!IsApprovedAttackItem(selected)
                || !ItemModification.TryGetNewSize(
                    selected.data.size,
                    enlarge,
                    out Vector2Int ignoredSize))
            {
                inventoryToggle.CloseInventory();

                ShowChoices(
                    "That item cannot use this modification.\n"
                    + "Choose an eligible attack item."
                );

                return;
            }

            target = selected;

            // Keep our target reference before CloseInventory deselects it.
            inventoryToggle.CloseInventory();
            ShowPreview();
        }
        else if (phase == Phase.Preview)
        {
            shrinkButton.interactable = CanConfirm(out string ignored);
        }
    }

    bool IsApprovedAttackItem(ItemInstance item)
    {
        if (!ItemModification.IsInBackpack(inventory, item))
            return false;

        ItemData source = ItemModification.GetSourceData(item);

        if (!source || attackItems == null)
            return false;

        foreach (ItemData approved in attackItems)
        {
            if (approved && approved == source)
                return true;
        }

        return false;
    }

    bool HasCandidate(bool makeLarger)
    {
        foreach (ItemInstance item in inventory.Items)
        {
            if (!IsApprovedAttackItem(item))
                continue;

            if (!ItemModification.TryGetNewSize(
                item.data.size,
                makeLarger,
                out Vector2Int newSize))
            {
                continue;
            }

            if (ItemModification.TryFindPlacement(
                inventory,
                item,
                newSize,
                out Vector2Int position,
                out bool rotation))
            {
                return true;
            }
        }

        return false;
    }

    void RefreshChoiceButtons()
    {
        bool canPay = credits >= modificationCost;
        bool canShrink = HasCandidate(false);
        bool canAmplify = HasCandidate(true);

        shrinkButton.interactable = canPay && canShrink;
        amplifyButton.interactable = canPay && canAmplify;

        SetLabel(
            shrinkButton,
            !canPay
                ? $"Shrink - Need {modificationCost} credits"
                : canShrink
                    ? $"Shrink an attack item - {modificationCost} credits"
                    : "Shrink - No eligible attack item"
        );

        SetLabel(
            amplifyButton,
            !canPay
                ? $"Amplify - Need {modificationCost} credits"
                : canAmplify
                    ? $"Amplify an attack item - {modificationCost} credits"
                    : "Amplify - No eligible item or insufficient space"
        );
    }

    void ShowChoices(string message = null)
    {
        phase = Phase.Choice;
        target = null;

        eventPanel.SetActive(true);

        shrinkButton.gameObject.SetActive(true);
        amplifyButton.gameObject.SetActive(true);
        leaveButton.gameObject.SetActive(true);

        leaveButton.interactable = true;
        SetLabel(leaveButton, "Leave");

        descriptionText.text = message ??
            "Choose a modification, then click an attack item "
            + "in your backpack.\n"
            + "Shrink: reduce size and damage per hit by 1.\n"
            + "Amplify: increase size and damage per hit by 2.\n"
            + "Preview the change before paying.";

        RefreshStatus();
        RefreshChoiceButtons();
    }

    void FirstButton()
    {
        if (!ready) return;

        if (phase == Phase.Choice)
            BeginSelection(false);
        else if (phase == Phase.Preview)
            ConfirmModification();
    }

    void SecondButton()
    {
        if (!ready) return;

        if (phase == Phase.Choice)
            BeginSelection(true);
        else if (phase == Phase.Preview)
            BeginSelection(enlarge);
    }

    void BeginSelection(bool makeLarger)
    {
        if (phase != Phase.Choice && phase != Phase.Preview)
            return;

        if (credits < modificationCost)
            return;

        enlarge = makeLarger;
        target = null;

        inventory.Deselect();
        phase = Phase.Selecting;

        eventPanel.SetActive(false);
        inventoryToggle.OpenInventory();
    }

    bool CanConfirm(out string reason)
    {
        reason = "";

        if (credits < modificationCost)
        {
            reason = "Not enough credits.";
            return false;
        }

        if (!IsApprovedAttackItem(target))
        {
            reason = "The selected attack item is no longer available.";
            return false;
        }

        if (!ItemModification.TryGetNewSize(
            target.data.size,
            enlarge,
            out Vector2Int newSize))
        {
            reason = "This item has reached the size limit.";
            return false;
        }

        if (!ItemModification.TryFindPlacement(
            inventory,
            target,
            newSize,
            out Vector2Int position,
            out bool rotation))
        {
            reason = "Not enough backpack space. Choose again to rearrange.";
            return false;
        }

        return true;
    }

    void ShowPreview(string notice = null)
    {
        phase = Phase.Preview;
        eventPanel.SetActive(true);

        if (target == null || !target.view)
        {
            ShowChoices("The selected item is no longer available.");
            return;
        }

        if (!ItemModification.TryGetNewSize(
            target.data.size,
            enlarge,
            out Vector2Int newSize))
        {
            ShowChoices("This item has reached the size limit.");
            return;
        }

        ItemModification modification =
            target.view.GetComponent<ItemModification>();

        int oldOffset = modification
            ? modification.DamagePerHitOffset
            : 0;

        int newOffset = oldOffset + (enlarge ? 2 : -1);

        bool canConfirm = CanConfirm(out string reason);
        Vector2Int oldSize = target.data.size;

        descriptionText.text =
            $"{target.data.itemName}\n"
            + $"Size: {oldSize.x} x {oldSize.y}"
            + $" -> {newSize.x} x {newSize.y}\n"
            + $"Damage per hit modifier: {Signed(oldOffset)}"
            + $" -> {Signed(newOffset)} (minimum damage: 0)\n"
            + $"Cost: {modificationCost} credits.";

        if (!string.IsNullOrEmpty(notice))
            descriptionText.text += "\n" + notice;

        if (!canConfirm)
            descriptionText.text += "\n" + reason;

        shrinkButton.gameObject.SetActive(true);
        amplifyButton.gameObject.SetActive(true);
        leaveButton.gameObject.SetActive(true);

        shrinkButton.interactable = canConfirm;
        amplifyButton.interactable = true;
        leaveButton.interactable = true;

        SetLabel(shrinkButton, "Confirm modification");
        SetLabel(amplifyButton, "Choose again / Rearrange backpack");
        SetLabel(leaveButton, "Cancel - No cost");

        RefreshStatus();
    }

    void ConfirmModification()
    {
        if (phase != Phase.Preview)
            return;

        if (!CanConfirm(out string reason))
        {
            ShowPreview(reason);
            return;
        }

        if (!ItemModification.TryApply(
            inventory,
            target,
            enlarge,
            out string error))
        {
            ShowPreview(error);
            return;
        }

        // Charge only after the item was successfully modified.
        credits -= modificationCost;
        phase = Phase.Result;

        ItemModification modification =
            target.view.GetComponent<ItemModification>();

        Vector2Int size = target.data.size;

        descriptionText.text =
            $"Modified: {target.data.itemName}\n"
            + $"New size: {size.x} x {size.y}\n"
            + "Damage per hit modifier: "
            + $"{Signed(modification.DamagePerHitOffset)}\n"
            + $"Paid {modificationCost} credits.";

        shrinkButton.gameObject.SetActive(false);
        amplifyButton.gameObject.SetActive(false);

        leaveButton.gameObject.SetActive(true);
        leaveButton.interactable = true;
        SetLabel(leaveButton, "Continue");

        RefreshStatus();
    }

    void BackOrLeave()
    {
        if (!ready || phase == Phase.Closed)
            return;

        if (phase == Phase.Preview)
        {
            ShowChoices("Modification cancelled. No credits spent.");
            return;
        }

        if (phase != Phase.Choice && phase != Phase.Result)
            return;

        phase = Phase.Closed;
        inventoryToggle.CloseInventory();
        eventPanel.SetActive(false);
        onReturnToMap.Invoke();
    }

    void RefreshStatus()
    {
        statusText.text = $"Credits: {credits}";
    }

    static string Signed(int value)
    {
        return value >= 0 ? "+" + value : value.ToString();
    }

    static void SetLabel(Button button, string label)
    {
        TMP_Text text =
            button.GetComponentInChildren<TMP_Text>(true);

        if (text)
            text.text = label;
    }

    [ContextMenu("TEST - Add Two Copies Of First Attack Item")]
    void TestAddTwoItems()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying || !ready)
            return;

        if (attackItems == null
            || attackItems.Length == 0
            || !attackItems[0])
        {
            return;
        }

        int added = 0;

        for (int i = 0; i < 2; i++)
        {
            if (!inventory.HasSpaceFor(attackItems[0]))
                break;

            if (!inventory.TryAddItem(attackItems[0]))
                break;

            added++;
        }

        Debug.Log($"Added {added} attack items for testing.", this);
#endif
    }
}