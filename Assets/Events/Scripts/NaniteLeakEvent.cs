using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class NaniteLeakEvent : MonoBehaviour
{
    [Header("Event UI")]
    [SerializeField] GameObject eventPanel;
    [SerializeField] TMP_Text titleText;
    [SerializeField] TMP_Text descriptionText;
    [SerializeField] TMP_Text statusText;
    [SerializeField] Button walkButton;
    [SerializeField] Button detourButton;
    [SerializeField] Button continueButton;

    [Header("Existing backpack")]
    [SerializeField] InventoryGrid inventory;
    [SerializeField] InventoryToggle inventoryToggle;

    [Header("Actual dungeon player")]
    [SerializeField] PlayerCombatant player;

    [Header("Approved reward assets")]
    [SerializeField] ItemData[] rewardItems;

    [Header("Event rules")]
    [Min(1)]
    [SerializeField] int crossingDamage = 15;

    [Header("Exploration and death callbacks")]
    [SerializeField] UnityEvent onReturnToMap = new UnityEvent();
    [SerializeField] UnityEvent onGameOver = new UnityEvent();

    enum Phase
    {
        Choice,
        Result,
        Complete,
        Dead
    }

    Phase phase;
    bool ready;

    // Prototype record only.
    // The map system must apply this to the next node's
    // credit and item rewards, then clear it.
    bool suppressNextNodeRewards;

    readonly List<ItemData> rewards = new List<ItemData>();

    public int CurrentHP => player ? player.HP : 0;

    public bool SuppressNextNodeRewards =>
        suppressNextNodeRewards;

    void Start()
    {
        // Use the same player as the battle system.
        if (!player)
        {
            BattleManager battle =
                Object.FindFirstObjectByType<BattleManager>();

            if (battle)
                player = battle.Player;
        }

        if (!player)
            player = Object.FindFirstObjectByType<PlayerCombatant>();

        ready = eventPanel
            && titleText
            && descriptionText
            && statusText
            && walkButton
            && detourButton
            && continueButton
            && inventory
            && inventoryToggle
            && player;

        if (!ready)
        {
            Debug.LogError(
                "NaniteLeakEvent: missing UI, backpack, or "
                + "PlayerCombatant. Test this event in the Dungeon.",
                this
            );

            if (walkButton) walkButton.interactable = false;
            if (detourButton) detourButton.interactable = false;

            if (continueButton)
            {
                continueButton.gameObject.SetActive(true);
                continueButton.interactable = true;
                SetLabel(continueButton, "Leave");
                continueButton.onClick.AddListener(LeaveUnavailableEvent);
            }

            if (descriptionText)
            {
                descriptionText.text =
                    "Event unavailable: a required reference is missing.";
            }

            return;
        }

        // Never initialize or reset the player's HP here.
        crossingDamage = Mathf.Max(1, crossingDamage);
        suppressNextNodeRewards = false;
        phase = Phase.Choice;

        rewards.Clear();

        if (rewardItems != null)
        {
            foreach (ItemData item in rewardItems)
            {
                if (!item) continue;
                if (item.size.x < 1 || item.size.y < 1) continue;
                if (!rewards.Contains(item)) rewards.Add(item);
            }
        }

        walkButton.onClick.AddListener(WalkThrough);
        detourButton.onClick.AddListener(TakeDetour);
        continueButton.onClick.AddListener(ContinueToMap);

        inventoryToggle.CloseInventory();
        eventPanel.SetActive(true);

        walkButton.gameObject.SetActive(true);
        detourButton.gameObject.SetActive(true);
        continueButton.gameObject.SetActive(false);
        continueButton.interactable = true;

        titleText.text = "Nanite Leak";

        descriptionText.text =
            "A burst pipe floods the corridor with corrosive nanite mist.\n"
            + "A sealed supply lies beyond the leak. "
            + "A longer route avoids the mist, but costs you time.";

        SetLabel(
            detourButton,
            "Go around - No credit or item rewards at the next node"
        );

        SetLabel(continueButton, "Continue");

        if (player.IsDead)
        {
            ShowDeath("You have no HP remaining.");
            return;
        }

        RefreshChoices();

        if (rewards.Count == 0)
        {
            Debug.LogWarning(
                "NaniteLeakEvent: assign approved items to Reward Items.",
                this
            );
        }
    }

    void Update()
    {
        if (!ready || !eventPanel.activeInHierarchy)
            return;

        if (player.IsDead && phase != Phase.Dead)
        {
            ShowDeath("You have no HP remaining.");
            return;
        }

        if (phase == Phase.Choice)
            RefreshChoices();
        else
            RefreshStatus();
    }

    void RefreshChoices()
    {
        walkButton.interactable =
            !player.IsDead && rewards.Count > 0;

        detourButton.interactable = !player.IsDead;

        string warning = player.HP <= crossingDamage
            ? "\nWARNING: this will kill you."
            : "";

        SetLabel(
            walkButton,
            $"Walk through - Lose {crossingDamage} HP and find an item"
            + warning
        );

        RefreshStatus();
    }

    public void WalkThrough()
    {
        if (!ready
            || phase != Phase.Choice
            || player.IsDead
            || rewards.Count == 0)
        {
            return;
        }

        // Lock the choice before deducting actual HP.
        phase = Phase.Result;
        player.LoseHP(crossingDamage);

        // Resolve death before granting any item.
        if (player.IsDead)
        {
            ShowDeath(
                "The corrosive mist overwhelms you.\n"
                + $"You lose {crossingDamage} HP."
            );
            return;
        }

        ItemData reward = rewards[Random.Range(0, rewards.Count)];

        string message =
            "You push through the mist and reach the supply.\n"
            + $"Lost {crossingDamage} HP.";

        if (inventory.HasSpaceFor(reward)
            && inventory.TryAddItem(reward))
        {
            ShowResult(message + $"\nReceived: {reward.itemName}.");
            return;
        }

        // Outside-backpack reward staging is not connected yet.
        ShowResult(
            message
            + $"\nBackpack full. {reward.itemName} was not added."
            + "\nOverflow pickup is not implemented yet."
        );
    }

    public void TakeDetour()
    {
        if (!ready
            || phase != Phase.Choice
            || player.IsDead)
        {
            return;
        }

        phase = Phase.Result;
        suppressNextNodeRewards = true;

        ShowResult(
            "You take a maintenance passage around the leak.\n"
            + "Penalty recorded: the next node grants no credit "
            + "or item rewards.\n"
            + "Purchases, healing, and other effects remain available."
        );
    }

    void ShowDeath(string message)
    {
        if (phase == Phase.Dead) return;

        phase = Phase.Dead;
        titleText.text = "Game Over";
        descriptionText.text = message;

        RefreshStatus();

        walkButton.gameObject.SetActive(false);
        detourButton.gameObject.SetActive(false);
        continueButton.gameObject.SetActive(false);

        onGameOver.Invoke();
    }

    void ShowResult(string message)
    {
        phase = Phase.Result;
        descriptionText.text = message;
        RefreshStatus();

        walkButton.gameObject.SetActive(false);
        detourButton.gameObject.SetActive(false);

        continueButton.gameObject.SetActive(true);
        continueButton.interactable = true;
    }

    void RefreshStatus()
    {
        statusText.text = $"HP: {player.HP} / {player.MaxHP}";

        if (suppressNextNodeRewards)
        {
            statusText.text +=
                "\nDebuff: No credit or item rewards at the next node";
        }
    }

    public void ContinueToMap()
    {
        if (!ready
            || phase != Phase.Result
            || player.IsDead)
        {
            return;
        }

        phase = Phase.Complete;
        inventoryToggle.CloseInventory();
        eventPanel.SetActive(false);

        onReturnToMap.Invoke();
    }

    void LeaveUnavailableEvent()
    {
        if (phase == Phase.Complete) return;

        phase = Phase.Complete;
        if (eventPanel) eventPanel.SetActive(false);
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