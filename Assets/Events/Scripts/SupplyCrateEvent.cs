using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class SupplyCrateEvent : MonoBehaviour
{
    [Header("Event UI")]
    [SerializeField] GameObject eventPanel;
    [SerializeField] TMP_Text titleText;
    [SerializeField] TMP_Text descriptionText;
    [SerializeField] TMP_Text statusText;
    [SerializeField] Button lockPickButton;
    [SerializeField] Button smashButton;
    [SerializeField] Button leaveButton;

    [Header("Existing backpack")]
    [SerializeField] InventoryGrid inventory;
    [SerializeField] InventoryToggle inventoryToggle;

    [Header("Actual dungeon player")]
    [SerializeField] PlayerCombatant player;

    [Header("Defense and healing rewards only")]
    [SerializeField] ItemData[] rewardItems;

    [Header("Prototype Lock Pick uses - connect later")]
    [Min(0)]
    [SerializeField] int startingLockPickUses = 3;

    [Header("Event rules")]
    [Min(1)]
    [SerializeField] int smashDamage = 12;

    [Range(0f, 1f)]
    [SerializeField] float intactChance = 0.5f;

    public enum SmashTestOutcome
    {
        Random,
        AlwaysIntact,
        AlwaysBroken
    }

    [Header("Editor testing only")]
    [SerializeField]
    SmashTestOutcome smashTestOutcome =
        SmashTestOutcome.Random;

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
    int lockPickUses;

    readonly List<ItemData> rewards = new List<ItemData>();

    public int CurrentHP => player ? player.HP : 0;

    // Prototype counter, not connected to the real Lock Pick item.
    public int LockPickUses => lockPickUses;

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
            && lockPickButton
            && smashButton
            && leaveButton
            && inventory
            && inventoryToggle
            && player;

        if (!ready)
        {
            Debug.LogError(
                "SupplyCrateEvent: missing UI, backpack, or "
                + "PlayerCombatant. Test this event in the Dungeon.",
                this
            );

            if (lockPickButton) lockPickButton.interactable = false;
            if (smashButton) smashButton.interactable = false;

            if (leaveButton)
            {
                leaveButton.interactable = true;
                SetLabel(leaveButton, "Leave");
                leaveButton.onClick.AddListener(LeaveUnavailableEvent);
            }

            if (descriptionText)
            {
                descriptionText.text =
                    "Event unavailable: a required reference is missing.";
            }

            return;
        }

        // Never initialize or reset the player's HP here.
        lockPickUses = Mathf.Max(0, startingLockPickUses);
        smashDamage = Mathf.Max(1, smashDamage);

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

        lockPickButton.onClick.AddListener(UseLockPick);
        smashButton.onClick.AddListener(SmashCrate);
        leaveButton.onClick.AddListener(ContinueOrLeave);

        phase = Phase.Choice;

        inventoryToggle.CloseInventory();
        eventPanel.SetActive(true);

        lockPickButton.gameObject.SetActive(true);
        smashButton.gameObject.SetActive(true);
        leaveButton.gameObject.SetActive(true);
        leaveButton.interactable = true;

        titleText.text = "Locked Supply Crate";

        descriptionText.text =
            "A corporate supply crate blocks the passage. "
            + "Its magnetic lock is still active.\n"
            + "Medical supplies or protective equipment may be inside.";

        if (player.IsDead)
        {
            ShowDeath("You have no HP remaining.");
            return;
        }

        RefreshChoices();

        if (rewards.Count == 0)
        {
            Debug.LogWarning(
                "SupplyCrateEvent: assign approved Defense "
                + "or Healing items to Reward Items.",
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
        bool hasRewards = rewards.Count > 0;

        lockPickButton.interactable =
            !player.IsDead && lockPickUses > 0 && hasRewards;

        smashButton.interactable =
            !player.IsDead && hasRewards;

        SetLabel(
            lockPickButton,
            lockPickUses > 0
                ? "Use Lock Pick - Consume 1 use and receive an item"
                : "Use Lock Pick - No uses remaining"
        );

        string warning = player.HP <= smashDamage
            ? "\nWARNING: this will kill you."
            : "";

        SetLabel(
            smashButton,
            $"Smash open - Lose {smashDamage} HP / "
            + $"{intactChance:P0} chance of an item"
            + warning
        );

        SetLabel(leaveButton, "Leave");
        RefreshStatus();
    }

    public void UseLockPick()
    {
        if (!ready
            || phase != Phase.Choice
            || player.IsDead
            || lockPickUses <= 0
            || rewards.Count == 0)
        {
            return;
        }

        phase = Phase.Result;
        lockPickUses--;

        GiveReward(
            "The magnetic lock clicks open.\n"
            + "Used 1 Lock Pick charge."
        );
    }

    public void SmashCrate()
    {
        if (!ready
            || phase != Phase.Choice
            || player.IsDead
            || rewards.Count == 0)
        {
            return;
        }

        phase = Phase.Result;

        // Deduct actual HP, bypassing combat shields.
        player.LoseHP(smashDamage);

        // Resolve death before granting any reward.
        if (player.IsDead)
        {
            ShowDeath(
                "The crate's jagged casing tears into you.\n"
                + $"You lose {smashDamage} HP."
            );
            return;
        }

        bool intact = Random.value < intactChance;

#if UNITY_EDITOR
        if (smashTestOutcome != SmashTestOutcome.Random)
        {
            intact =
                smashTestOutcome == SmashTestOutcome.AlwaysIntact;
        }
#endif

        if (intact)
        {
            GiveReward(
                $"You force the crate open and lose {smashDamage} HP.\n"
                + "The supply inside survives."
            );
        }
        else
        {
            ShowResult(
                $"You force the crate open and lose {smashDamage} HP.\n"
                + "The supply inside is destroyed. No item received."
            );
        }
    }

    void ShowDeath(string message)
    {
        if (phase == Phase.Dead) return;

        phase = Phase.Dead;
        titleText.text = "Game Over";
        descriptionText.text = message;

        RefreshStatus();

        lockPickButton.gameObject.SetActive(false);
        smashButton.gameObject.SetActive(false);
        leaveButton.gameObject.SetActive(false);

        onGameOver.Invoke();
    }

    void GiveReward(string message)
    {
        ItemData reward = rewards[Random.Range(0, rewards.Count)];

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

    void ShowResult(string message)
    {
        phase = Phase.Result;
        descriptionText.text = message;
        RefreshStatus();

        lockPickButton.gameObject.SetActive(false);
        smashButton.gameObject.SetActive(false);

        leaveButton.gameObject.SetActive(true);
        leaveButton.interactable = true;

        SetLabel(leaveButton, "Continue");
    }

    void RefreshStatus()
    {
        statusText.text =
            $"HP: {player.HP} / {player.MaxHP}"
            + $"     Lock Pick uses: {lockPickUses}";
    }

    public void ContinueOrLeave()
    {
        if (!ready
            || player.IsDead
            || (phase != Phase.Choice && phase != Phase.Result))
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