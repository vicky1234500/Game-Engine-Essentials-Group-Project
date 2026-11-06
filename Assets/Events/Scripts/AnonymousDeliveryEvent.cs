using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class AnonymousDeliveryEvent : MonoBehaviour
{
    [Header("Event UI")]
    [SerializeField] GameObject eventPanel;
    [SerializeField] TMP_Text titleText;
    [SerializeField] TMP_Text descriptionText;
    [SerializeField] TMP_Text statusText;
    [SerializeField] Button acceptButton;
    [SerializeField] Button openButton;
    [SerializeField] Button leaveButton;

    [Header("Existing backpack")]
    [SerializeField] InventoryGrid inventory;
    [SerializeField] InventoryToggle inventoryToggle;

    [Header("Package and support rewards")]
    [SerializeField] ItemData sealedPackage;
    [SerializeField] ItemData[] supportRewards;

    [Header("Delivery rules")]
    [Min(1)]
    [SerializeField] int requiredVictories = 3;

    [Min(0)]
    [SerializeField] int deliveryReward = 80;

    [Header("Return to exploration")]
    [SerializeField] UnityEvent onReturnToMap = new UnityEvent();

    enum EventPhase
    {
        Choice,
        Result,
        Closed
    }

    enum PackageState
    {
        None,
        Carrying,
        Delivered,
        Discarded,
        Opened
    }

    EventPhase phase;
    PackageState packageState;

    bool ready;
    int victories;
    int targetVictories;
    int promisedReward;
    int testBattleNumber;

    // Prototype record. Connect to the run's curse system later.
    int trackerCount;

    // Track this exact package instance.
    ItemInstance carriedPackage;

    readonly List<ItemData> rewards = new List<ItemData>();
    readonly HashSet<string> countedBattles = new HashSet<string>();

    public int Credits => RunState.credits;
    public int TrackerCount => trackerCount;
    public int Victories => victories;

    public bool HasActiveDelivery =>
        packageState == PackageState.Carrying;

    void Start()
    {
        ready = eventPanel
            && titleText
            && descriptionText
            && statusText
            && acceptButton
            && openButton
            && leaveButton
            && inventory
            && inventoryToggle;

        if (!ready)
        {
            Debug.LogError(
                "AnonymousDeliveryEvent: assign all UI and backpack fields.",
                this
            );

            if (acceptButton) acceptButton.interactable = false;
            if (openButton) openButton.interactable = false;
            if (leaveButton) leaveButton.interactable = false;
            return;
        }

        // Read the shared balance without resetting it.
        targetVictories = Mathf.Max(1, requiredVictories);
        promisedReward = Mathf.Max(0, deliveryReward);

        victories = 0;
        trackerCount = 0;
        testBattleNumber = 0;
        carriedPackage = null;

        countedBattles.Clear();
        rewards.Clear();

        if (supportRewards != null)
        {
            foreach (ItemData item in supportRewards)
            {
                if (!item || item == sealedPackage) continue;
                if (item.size.x < 1 || item.size.y < 1) continue;
                if (!rewards.Contains(item)) rewards.Add(item);
            }
        }

        phase = EventPhase.Choice;
        packageState = PackageState.None;

        acceptButton.onClick.AddListener(AcceptDelivery);
        openButton.onClick.AddListener(OpenPackage);
        leaveButton.onClick.AddListener(LeaveOrContinue);

        inventoryToggle.CloseInventory();
        eventPanel.SetActive(true);

        acceptButton.gameObject.SetActive(true);
        openButton.gameObject.SetActive(true);
        leaveButton.gameObject.SetActive(true);
        leaveButton.interactable = true;

        titleText.text = "Anonymous Delivery";

        descriptionText.text =
            "An anonymous courier offers you a sealed package.\n"
            + $"Carry it through {targetVictories} victories "
            + $"to earn {promisedReward} credits.\n"
            + "Accepting locks the package. It cannot be opened, "
            + "sold, or traded. Discarding it forfeits payment.";

        SetLabel(
            openButton,
            "Open the package - Random support item + 1 Tracker"
        );

        SetLabel(leaveButton, "Refuse");
        RefreshChoices();
        RefreshStatus();

        if (!PackageDataValid())
        {
            Debug.LogWarning(
                "Assign a Sealed Package item with exactly 2 tiles.",
                this
            );
        }

        if (rewards.Count == 0)
        {
            Debug.LogWarning(
                "Assign approved support items to Support Rewards.",
                this
            );
        }
    }

    bool PackageDataValid()
    {
        return sealedPackage
            && sealedPackage.size.x > 0
            && sealedPackage.size.y > 0
            && sealedPackage.size.x * sealedPackage.size.y == 2;
    }

    void Update()
    {
        if (!ready) return;

        if (phase == EventPhase.Choice)
            RefreshChoices();

        // Keep tracking the package after closing the event.
        CheckForDiscard();

        if (phase != EventPhase.Closed && eventPanel.activeInHierarchy)
            RefreshStatus();
    }

    void RefreshChoices()
    {
        bool valid = PackageDataValid();
        bool fits = valid && inventory.HasSpaceFor(sealedPackage);

        acceptButton.interactable = fits;
        openButton.interactable = rewards.Count > 0;

        string label;

        if (!valid)
            label = "Accept delivery - Package data missing";
        else if (!fits)
            label = "Accept delivery - No space for the 2-tile package";
        else
            label = $"Accept delivery - {targetVictories} wins / "
                + $"{promisedReward} credits";

        SetLabel(acceptButton, label);
    }

    public void AcceptDelivery()
    {
        if (!ready
            || phase != EventPhase.Choice
            || !PackageDataValid()
            || !inventory.HasSpaceFor(sealedPackage))
        {
            return;
        }

        var previousItems =
            new HashSet<ItemInstance>(inventory.Items);

        if (!inventory.TryAddItem(sealedPackage))
            return;

        foreach (ItemInstance item in inventory.Items)
        {
            if (item.data == sealedPackage
                && !previousItems.Contains(item))
            {
                carriedPackage = item;
                break;
            }
        }

        if (carriedPackage == null)
        {
            Debug.LogError(
                "Package was added but its instance could not be tracked.",
                this
            );
            return;
        }

        phase = EventPhase.Result;
        packageState = PackageState.Carrying;
        victories = 0;
        countedBattles.Clear();

        ShowResult(
            "The sealed package is now in your backpack.\n"
            + "It occupies 2 tiles and cannot be opened.\n"
            + $"Win {targetVictories} battles while carrying it "
            + $"to receive {promisedReward} credits automatically."
        );
    }

    public void OpenPackage()
    {
        // Opening is only available before accepting the delivery.
        if (!ready
            || phase != EventPhase.Choice
            || rewards.Count == 0)
        {
            return;
        }

        phase = EventPhase.Result;
        packageState = PackageState.Opened;

        trackerCount++;
        RunEffects.AddCurse("Tracker");                          // in your deck for the rest of the run

        ItemData reward = rewards[Random.Range(0, rewards.Count)];

        string message =
            "You break the seal instead of accepting the delivery.\n"
            + "Added 1 Tracker curse to your deck "
            + "for this run.\n";

        message += RunEffects.GiveItem(inventory, reward);       // backpack full -> dropped on the floor

        ShowResult(message);
    }

    // Called by EventExplorationBridge after a battle victory.
    // Each victorious battle must have a unique ID.
    public void ReportBattleVictory(string battleId)
    {
        if (!ready) return;

        CheckForDiscard();

        if (packageState != PackageState.Carrying)
            return;

        if (string.IsNullOrWhiteSpace(battleId))
            return;

        // Dragging temporarily removes the item from Inventory.Items.
        // Do not count a victory while the package is outside the grid.
        bool inBackpack = false;

        foreach (ItemInstance item in inventory.Items)
        {
            if (item == carriedPackage)
            {
                inBackpack = true;
                break;
            }
        }

        if (!inBackpack) return;
        if (!countedBattles.Add(battleId)) return;

        victories++;

        if (victories >= targetVictories)
        {
            // Mark completion before removing the package or paying.
            // Further victory reports cannot pay this delivery again.
            packageState = PackageState.Delivered;

            inventory.Discard(carriedPackage);
            carriedPackage = null;

            // Add to the current shared balance.
            RunState.credits += promisedReward;

            descriptionText.text =
                "Delivery complete. The package has been collected.\n"
                + $"Received {promisedReward} credits.";

            Debug.Log(
                $"Delivery complete. Received {promisedReward} credits. "
                + $"Shared credits: {RunState.credits}. Package removed.",
                this
            );
        }
        else
        {
            descriptionText.text =
                $"Delivery progress: {victories} / {targetVictories} "
                + "victories.\nThe package remains sealed.";

            Debug.Log(
                $"Delivery progress: {victories}/{targetVictories}",
                this
            );
        }

        RefreshStatus();
    }

    void CheckForDiscard()
    {
        if (packageState != PackageState.Carrying)
            return;

        // Temporary removal during dragging is not disposal.
        // The existing trash system destroys the item's view.
        if (carriedPackage != null && carriedPackage.view)
            return;

        packageState = PackageState.Discarded;
        carriedPackage = null;

        descriptionText.text =
            "The package was discarded.\n"
            + "Delivery cancelled. No payment received.";

        RefreshStatus();

        Debug.Log(
            $"Delivery cancelled. Shared credits remain {RunState.credits}.",
            this
        );
    }

    void ShowResult(string message)
    {
        descriptionText.text = message;

        acceptButton.gameObject.SetActive(false);
        openButton.gameObject.SetActive(false);

        leaveButton.gameObject.SetActive(true);
        leaveButton.interactable = true;

        SetLabel(leaveButton, "Continue");
        RefreshStatus();
    }

    void RefreshStatus()
    {
        statusText.text = $"Credits: {RunState.credits}";

        if (packageState == PackageState.Carrying)
        {
            statusText.text +=
                $"\nDelivery: {victories} / {targetVictories} victories";
        }
        else if (packageState == PackageState.Delivered)
        {
            statusText.text += "\nDelivery: Completed";
        }
        else if (packageState == PackageState.Discarded)
        {
            statusText.text += "\nDelivery: Cancelled";
        }

        if (trackerCount > 0)
        {
            statusText.text +=
                $"\nCurse: Tracker x{trackerCount}";
        }
    }

    public void LeaveOrContinue()
    {
        if (!ready || phase == EventPhase.Closed)
            return;

        phase = EventPhase.Closed;
        eventPanel.SetActive(false);

        onReturnToMap.Invoke();
    }

    [ContextMenu("TEST - Record One Victory")]
    void TestRecordOneVictory()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying) return;

        testBattleNumber++;
        ReportBattleVictory("test-battle-" + testBattleNumber);
#endif
    }

    [ContextMenu("TEST - Repeat Last Victory")]
    void TestRepeatLastVictory()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying || testBattleNumber == 0)
            return;

        ReportBattleVictory("test-battle-" + testBattleNumber);
#endif
    }

    static void SetLabel(Button button, string label)
    {
        TMP_Text text =
            button.GetComponentInChildren<TMP_Text>(true);

        if (text)
            text.text = label;
    }
}