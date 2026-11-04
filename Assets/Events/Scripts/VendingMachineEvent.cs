using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class VendingMachineEvent : MonoBehaviour
{
    [Header("Event UI")]
    [SerializeField] GameObject eventPanel;
    [SerializeField] TMP_Text titleText;
    [SerializeField] TMP_Text descriptionText;
    [SerializeField] TMP_Text statusText;
    [SerializeField] Button payButton;
    [SerializeField] Button kickButton;
    [SerializeField] Button leaveButton;

    [Header("Existing backpack")]
    [SerializeField] InventoryGrid inventory;
    [SerializeField] InventoryToggle inventoryToggle;

    [Header("Actual dungeon player")]
    [SerializeField] PlayerCombatant player;

    [Header("Approved reward assets")]
    [SerializeField] ItemData[] oneTileRewards;
    [SerializeField] ItemData[] allRewards;

    [Header("Event rules")]
    [Min(0)]
    [SerializeField] int purchaseCost = 15;

    [Min(1)]
    [SerializeField] int kickDamage = 10;

    [Range(0f, 1f)]
    [SerializeField] float kickRewardChance = 0.5f;

    public enum KickTestOutcome
    {
        Random,
        AlwaysReward,
        AlwaysDamage
    }

    [Header("Editor testing only")]
    [SerializeField]
    KickTestOutcome kickTestOutcome =
        KickTestOutcome.Random;

    [Header("Exploration and death callbacks")]
    [SerializeField] UnityEvent onReturnToMap = new UnityEvent();
    [SerializeField] UnityEvent onGameOver = new UnityEvent();

    enum Phase
    {
        Choice,
        PendingReward,
        Result,
        Complete,
        Dead
    }

    Phase phase;
    bool ready;

    ItemData pendingReward;
    string resultMessage;

    readonly List<ItemData> smallPool = new List<ItemData>();
    readonly List<ItemData> fullPool = new List<ItemData>();

    public int CurrentHP => player ? player.HP : 0;
    public int Credits => RunState.credits;
    public bool IsComplete => phase == Phase.Complete;

    void Start()
    {
        // Prefer the exact player used by the battle system.
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
            && payButton
            && kickButton
            && leaveButton
            && inventory
            && inventoryToggle
            && player;

        if (!ready)
        {
            Debug.LogError(
                "VendingMachineEvent: missing UI, backpack, or "
                + "PlayerCombatant. Test this event in the Dungeon.",
                this
            );

            if (payButton) payButton.interactable = false;
            if (kickButton) kickButton.interactable = false;

            // Allow leaving if the player reference cannot be found.
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

        purchaseCost = Mathf.Max(0, purchaseCost);
        kickDamage = Mathf.Max(1, kickDamage);

        // Never reset the player's HP or run credits here.
        CachePool(oneTileRewards, smallPool, true);
        CachePool(allRewards, fullPool, false);

        payButton.onClick.AddListener(Buy);
        kickButton.onClick.AddListener(Kick);
        leaveButton.onClick.AddListener(ContinueOrLeave);

        inventoryToggle.CloseInventory();
        eventPanel.SetActive(true);

        payButton.gameObject.SetActive(true);
        kickButton.gameObject.SetActive(true);
        leaveButton.gameObject.SetActive(true);
        leaveButton.interactable = true;

        phase = Phase.Choice;
        titleText.text = "Busted Vending Machine";

        descriptionText.text =
            "A battered vending machine flickers in the corridor.\n"
            + "Sealed supplies are stuck behind its cracked display.\n"
            + "Paid costs are not refunded if you decline a reward.";

        if (player.IsDead)
        {
            ShowDeath("You have no HP remaining.");
            return;
        }

        RefreshChoices();

        if (smallPool.Count == 0 || fullPool.Count == 0)
        {
            Debug.LogWarning(
                "VendingMachineEvent: configure both reward pools. "
                + "A one-tile reward must have size 1 x 1.",
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

    static void CachePool(
        ItemData[] source,
        List<ItemData> target,
        bool oneTileOnly)
    {
        target.Clear();

        if (source == null) return;

        foreach (ItemData item in source)
        {
            if (!item) continue;
            if (item.size.x < 1 || item.size.y < 1) continue;
            if (oneTileOnly && item.size != Vector2Int.one) continue;

            if (!target.Contains(item))
                target.Add(item);
        }
    }

    void RefreshStatus()
    {
        statusText.text =
            $"HP: {player.HP} / {player.MaxHP}"
            + $"       Credits: {RunState.credits}";
    }

    void RefreshChoices()
    {
        RefreshStatus();

        payButton.interactable =
            !player.IsDead
            && RunState.credits >= purchaseCost
            && smallPool.Count > 0;

        kickButton.interactable =
            !player.IsDead && fullPool.Count > 0;

        SetLabel(
            payButton,
            RunState.credits < purchaseCost
                ? $"Need {purchaseCost} credits - Not enough credits"
                : $"Pay {purchaseCost} credits - Random 1-tile item"
        );

        string risk = player.HP <= kickDamage
            ? "\nWARNING: failure will kill you."
            : "";

        SetLabel(
            kickButton,
            $"Kick it - {kickRewardChance:P0} item / "
            + $"{1f - kickRewardChance:P0} lose {kickDamage} HP"
            + risk
        );

        SetLabel(leaveButton, "Leave");
    }

    public void Buy()
    {
        if (!ready
            || phase != Phase.Choice
            || player.IsDead
            || RunState.credits < purchaseCost
            || smallPool.Count == 0)
        {
            return;
        }

        phase = Phase.PendingReward;
        RunState.credits -= purchaseCost;

        pendingReward = smallPool[Random.Range(0, smallPool.Count)];

        resultMessage =
            "The machine releases a sealed supply.\n"
            + $"Paid {purchaseCost} credits.";

        ResolveReward();
    }

    public void Kick()
    {
        if (!ready
            || phase != Phase.Choice
            || player.IsDead
            || fullPool.Count == 0)
        {
            return;
        }

        phase = Phase.Result;
        bool won = Random.value < kickRewardChance;

#if UNITY_EDITOR
        if (kickTestOutcome != KickTestOutcome.Random)
            won = kickTestOutcome == KickTestOutcome.AlwaysReward;
#endif

        if (won)
        {
            pendingReward = fullPool[Random.Range(0, fullPool.Count)];
            phase = Phase.PendingReward;
            resultMessage = "A loud thud shakes a supply loose.";

            ResolveReward();
            return;
        }

        // Event HP costs bypass combat shields.
        player.LoseHP(kickDamage);

        string message =
            "A broken panel strikes you.\n"
            + $"You lose {kickDamage} HP.";

        if (player.IsDead)
        {
            ShowDeath(message);
            return;
        }

        ShowResult(message);
    }

    void ShowDeath(string message)
    {
        if (phase == Phase.Dead) return;

        phase = Phase.Dead;
        titleText.text = "Game Over";
        descriptionText.text = message;

        RefreshStatus();

        payButton.gameObject.SetActive(false);
        kickButton.gameObject.SetActive(false);
        leaveButton.gameObject.SetActive(false);

        onGameOver.Invoke();
    }

    void ResolveReward()
    {
        if (!pendingReward) return;

        if (inventory.HasSpaceFor(pendingReward)
            && inventory.TryAddItem(pendingReward))
        {
            string itemName = pendingReward.itemName;
            pendingReward = null;

            ShowResult(resultMessage + $"\nReceived: {itemName}.");
            return;
        }

        // Outside-backpack reward staging is not connected yet.
        ShowResult(
            resultMessage
            + $"\nBackpack full. {pendingReward.itemName} was not added."
            + "\nOverflow pickup is not implemented yet."
        );
    }

    void ShowResult(string message)
    {
        phase = Phase.Result;

        inventoryToggle.CloseInventory();
        eventPanel.SetActive(true);

        descriptionText.text = message;
        RefreshStatus();

        payButton.gameObject.SetActive(false);
        kickButton.gameObject.SetActive(false);

        leaveButton.gameObject.SetActive(true);
        leaveButton.interactable = true;

        SetLabel(leaveButton, "Continue");
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

    [ContextMenu("TEST - Set Run Credits To 30")]
    void TestSetCreditsTo30()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying
            || !ready
            || phase != Phase.Choice)
        {
            return;
        }

        RunState.credits = 30;
        RefreshChoices();
#endif
    }

    [ContextMenu("TEST - Set Run Credits To 0")]
    void TestSetCreditsTo0()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying
            || !ready
            || phase != Phase.Choice)
        {
            return;
        }

        RunState.credits = 0;
        RefreshChoices();
#endif
    }

    static void SetLabel(Button button, string label)
    {
        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);

        if (text)
            text.text = label;
    }
}