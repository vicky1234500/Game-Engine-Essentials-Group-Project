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

    [Header("Defense and healing rewards only")]
    [SerializeField] ItemData[] rewardItems;

    [Header("Prototype starting values")]
    [Min(1)]
    [SerializeField] int startingHP = 135;

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

    [Header("Connect to the map and death flow later")]
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
    int hp;
    int lockPickUses;

    readonly List<ItemData> rewards = new List<ItemData>();

    public int CurrentHP => hp;

    // Prototype counter, not connected to the real Lock Pick item.
    public int LockPickUses => lockPickUses;

    void Start()
    {
        ready = eventPanel
            && titleText
            && descriptionText
            && statusText
            && lockPickButton
            && smashButton
            && leaveButton
            && inventory
            && inventoryToggle;

        if (!ready)
        {
            Debug.LogError(
                "SupplyCrateEvent: assign all UI and backpack fields.",
                this
            );

            if (lockPickButton) lockPickButton.interactable = false;
            if (smashButton) smashButton.interactable = false;
            if (leaveButton) leaveButton.interactable = false;
            return;
        }

        startingHP = Mathf.Max(1, startingHP);
        hp = startingHP;
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

        titleText.text = "Locked Supply Crate";

        descriptionText.text =
            "A corporate supply crate blocks the passage. "
            + "Its magnetic lock is still active.\n"
            + "Medical supplies or protective equipment may be inside.";

        bool hasRewards = rewards.Count > 0;

        lockPickButton.interactable =
            lockPickUses > 0 && hasRewards;

        smashButton.interactable = hasRewards;
        leaveButton.interactable = true;

        SetLabel(
            lockPickButton,
            lockPickUses > 0
                ? "Use Lock Pick - Consume 1 use and receive an item"
                : "Use Lock Pick - No uses remaining"
        );

        string warning = hp <= smashDamage
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

        if (!hasRewards)
        {
            Debug.LogWarning(
                "SupplyCrateEvent: assign approved Defense "
                + "or Healing items to Reward Items.",
                this
            );
        }
    }

    public void UseLockPick()
    {
        if (!ready
            || phase != Phase.Choice
            || lockPickUses <= 0
            || rewards.Count == 0)
        {
            return;
        }

        // Lock the choice before spending a use.
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
            || rewards.Count == 0)
        {
            return;
        }

        phase = Phase.Result;
        hp = Mathf.Max(0, hp - smashDamage);

        // Death is resolved before any reward is granted.
        if (hp == 0)
        {
            phase = Phase.Dead;

            titleText.text = "Game Over";

            descriptionText.text =
                "The crate's jagged casing tears into you.\n"
                + $"You lose {smashDamage} HP.";

            RefreshStatus();

            lockPickButton.gameObject.SetActive(false);
            smashButton.gameObject.SetActive(false);
            leaveButton.gameObject.SetActive(false);

            onGameOver.Invoke();
            return;
        }

        bool intact = Random.value < intactChance;

        if (Application.isEditor
            && smashTestOutcome != SmashTestOutcome.Random)
        {
            intact =
                smashTestOutcome == SmashTestOutcome.AlwaysIntact;
        }

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

    void GiveReward(string message)
    {
        ItemData reward = rewards[Random.Range(0, rewards.Count)];

        if (inventory.HasSpaceFor(reward)
            && inventory.TryAddItem(reward))
        {
            ShowResult(message + $"\nReceived: {reward.itemName}.");
            return;
        }

        // Temporary: overflow pickup will be connected later.
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
            $"HP: {hp} / {startingHP}"
            + $"     Lock Pick uses: {lockPickUses}";
    }

    public void ContinueOrLeave()
    {
        if (!ready
            || (phase != Phase.Choice && phase != Phase.Result))
        {
            return;
        }

        phase = Phase.Complete;
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