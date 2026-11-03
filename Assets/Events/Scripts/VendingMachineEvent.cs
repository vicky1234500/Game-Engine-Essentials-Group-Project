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

    [Header("Approved reward assets")]
    [SerializeField] ItemData[] oneTileRewards;
    [SerializeField] ItemData[] allRewards;

    [Header("Prototype starting values")]
    [Min(1)]
    [SerializeField] int startingHP = 135;

    [Min(0)]
    [SerializeField] int startingCredits = 30;

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

    [Header("Connect to the game's map and death flow later")]
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
    int hp;
    int credits;
    bool ready;

    ItemData pendingReward;
    string resultMessage;

    readonly List<ItemData> smallPool = new List<ItemData>();
    readonly List<ItemData> fullPool = new List<ItemData>();

    public int CurrentHP => hp;
    public int Credits => credits;
    public bool IsComplete => phase == Phase.Complete;

    void Start()
    {
        ready = eventPanel
            && titleText
            && descriptionText
            && statusText
            && payButton
            && kickButton
            && leaveButton
            && inventory
            && inventoryToggle;

        if (!ready)
        {
            Debug.LogError(
                "VendingMachineEvent: assign all Event UI "
                + "and Existing backpack fields.",
                this
            );

            if (payButton) payButton.interactable = false;
            if (kickButton) kickButton.interactable = false;
            if (leaveButton) leaveButton.interactable = false;

            return;
        }

        hp = Mathf.Max(1, startingHP);
        credits = Mathf.Max(0, startingCredits);
        purchaseCost = Mathf.Max(0, purchaseCost);
        kickDamage = Mathf.Max(1, kickDamage);

        CachePool(oneTileRewards, smallPool, true);
        CachePool(allRewards, fullPool, false);

        payButton.onClick.AddListener(Buy);
        kickButton.onClick.AddListener(Kick);
        leaveButton.onClick.AddListener(ContinueOrLeave);

        inventoryToggle.CloseInventory();
        eventPanel.SetActive(true);

        phase = Phase.Choice;

        titleText.text = "Busted Vending Machine";

        descriptionText.text =
            "A battered vending machine flickers in the corridor.\n"
            + "Sealed supplies are stuck behind its cracked display.\n"
            + "Paid costs are not refunded if you decline a reward.";

        RefreshChoices();

        if (smallPool.Count == 0 || fullPool.Count == 0)
        {
            Debug.LogWarning(
                "VendingMachineEvent: configure both approved reward pools. "
                + "A one-tile reward must have size 1 x 1.",
                this
            );
        }
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

            if (oneTileOnly && item.size != Vector2Int.one)
                continue;

            if (!target.Contains(item))
                target.Add(item);
        }
    }

    void RefreshChoices()
    {
        statusText.text =
            $"HP: {hp} / {startingHP}       Credits: {credits}";

        payButton.interactable =
            credits >= purchaseCost && smallPool.Count > 0;

        kickButton.interactable = fullPool.Count > 0;

        SetLabel(
            payButton,
            credits < purchaseCost
                ? $"Need {purchaseCost} credits - Not enough credits"
                : $"Pay {purchaseCost} credits - Random 1-tile item"
        );

        string risk = hp <= kickDamage
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
            || credits < purchaseCost
            || smallPool.Count == 0)
        {
            return;
        }

        // Change phase first to prevent repeated charges.
        phase = Phase.PendingReward;
        credits -= purchaseCost;

        pendingReward =
            smallPool[Random.Range(0, smallPool.Count)];

        resultMessage =
            "The machine releases a sealed supply.\n"
            + $"Paid {purchaseCost} credits.";

        ResolveReward();
    }

    public void Kick()
    {
        if (!ready
            || phase != Phase.Choice
            || fullPool.Count == 0)
        {
            return;
        }

        phase = Phase.Result;

        bool won = Random.value < kickRewardChance;

        if (Application.isEditor
            && kickTestOutcome != KickTestOutcome.Random)
        {
            won = kickTestOutcome == KickTestOutcome.AlwaysReward;
        }

        if (won)
        {
            pendingReward =
                fullPool[Random.Range(0, fullPool.Count)];

            phase = Phase.PendingReward;
            resultMessage = "A loud thud shakes a supply loose.";

            ResolveReward();
            return;
        }

        hp = Mathf.Max(0, hp - kickDamage);

        if (hp == 0)
        {
            phase = Phase.Dead;

            titleText.text = "Game Over";

            descriptionText.text =
                "A broken panel strikes you.\n"
                + $"You lose {kickDamage} HP.";

            statusText.text =
                $"HP: 0 / {startingHP}       Credits: {credits}";

            payButton.gameObject.SetActive(false);
            kickButton.gameObject.SetActive(false);
            leaveButton.gameObject.SetActive(false);

            onGameOver.Invoke();
            return;
        }

        ShowResult(
            "A broken panel strikes you.\n"
            + $"You lose {kickDamage} HP."
        );
    }

    void ResolveReward()
    {
        if (!pendingReward) return;

        if (inventory.HasSpaceFor(pendingReward)
            && inventory.TryAddItem(pendingReward))
        {
            string itemName = pendingReward.itemName;
            pendingReward = null;

            ShowResult(
                resultMessage + $"\nReceived: {itemName}."
            );

            return;
        }

        // Temporary: overflow pickup is not connected yet.
        // No extra UI is created, and the item is not added.
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

        statusText.text =
            $"HP: {hp} / {startingHP}       Credits: {credits}";

        payButton.gameObject.SetActive(false);
        kickButton.gameObject.SetActive(false);

        leaveButton.gameObject.SetActive(true);
        leaveButton.interactable = true;

        SetLabel(leaveButton, "Continue");
    }

    public void ContinueOrLeave()
    {
        if (!ready
            || (phase != Phase.Choice && phase != Phase.Result))
        {
            return;
        }

        phase = Phase.Complete;

        inventoryToggle.CloseInventory();
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