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

    [Header("Approved reward assets")]
    [SerializeField] ItemData[] rewardItems;

    [Header("Prototype starting values")]
    [Min(1)]
    [SerializeField] int startingHP = 135;

    [Header("Event rules")]
    [Min(1)]
    [SerializeField] int crossingDamage = 15;

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

    // Prototype record only.
    // The map system must apply this to the next node's
    // credit and item rewards, then clear it.
    bool suppressNextNodeRewards;

    readonly List<ItemData> rewards = new List<ItemData>();

    public int CurrentHP => hp;

    public bool SuppressNextNodeRewards =>
        suppressNextNodeRewards;

    void Start()
    {
        ready = eventPanel
            && titleText
            && descriptionText
            && statusText
            && walkButton
            && detourButton
            && continueButton
            && inventory
            && inventoryToggle;

        if (!ready)
        {
            Debug.LogError(
                "NaniteLeakEvent: assign all UI and backpack fields.",
                this
            );

            if (walkButton) walkButton.interactable = false;
            if (detourButton) detourButton.interactable = false;
            if (continueButton) continueButton.interactable = false;
            return;
        }

        startingHP = Mathf.Max(1, startingHP);
        hp = startingHP;
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

        titleText.text = "Nanite Leak";

        descriptionText.text =
            "A burst pipe floods the corridor with corrosive nanite mist.\n"
            + "A sealed supply lies beyond the leak. "
            + "A longer route avoids the mist, but costs you time.";

        walkButton.interactable = rewards.Count > 0;
        detourButton.interactable = true;
        continueButton.interactable = true;

        string warning = hp <= crossingDamage
            ? "\nWARNING: this will kill you."
            : "";

        SetLabel(
            walkButton,
            $"Walk through - Lose {crossingDamage} HP and find an item"
            + warning
        );

        SetLabel(
            detourButton,
            "Go around - No credit or item rewards at the next node"
        );

        SetLabel(continueButton, "Continue");
        RefreshStatus();

        if (rewards.Count == 0)
        {
            Debug.LogWarning(
                "NaniteLeakEvent: assign approved items to Reward Items.",
                this
            );
        }
    }

    public void WalkThrough()
    {
        if (!ready
            || phase != Phase.Choice
            || rewards.Count == 0)
        {
            return;
        }

        // Lock the choice before applying damage.
        phase = Phase.Result;
        hp = Mathf.Max(0, hp - crossingDamage);

        if (hp == 0)
        {
            phase = Phase.Dead;

            titleText.text = "Game Over";

            descriptionText.text =
                "The corrosive mist overwhelms you.\n"
                + $"You lose {crossingDamage} HP.";

            RefreshStatus();

            walkButton.gameObject.SetActive(false);
            detourButton.gameObject.SetActive(false);
            continueButton.gameObject.SetActive(false);

            onGameOver.Invoke();
            return;
        }

        ItemData reward = rewards[Random.Range(0, rewards.Count)];

        string message =
            "You push through the mist and reach the supply.\n"
            + $"Lost {crossingDamage} HP.";

        if (inventory.HasSpaceFor(reward)
            && inventory.TryAddItem(reward))
        {
            ShowResult(
                message + $"\nReceived: {reward.itemName}."
            );
            return;
        }

        // Temporary: overflow pickup is not connected yet.
        ShowResult(
            message
            + $"\nBackpack full. {reward.itemName} was not added."
            + "\nOverflow pickup is not implemented yet."
        );
    }

    public void TakeDetour()
    {
        if (!ready || phase != Phase.Choice)
            return;

        phase = Phase.Result;
        suppressNextNodeRewards = true;

        ShowResult(
            "You take a maintenance passage around the leak.\n"
            + "Penalty recorded: the next node grants no credit "
            + "or item rewards.\n"
            + "Purchases, healing, and other effects remain available."
        );
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
        statusText.text = $"HP: {hp} / {startingHP}";

        if (suppressNextNodeRewards)
        {
            statusText.text +=
                "\nDebuff: No credit or item rewards at the next node";
        }
    }

    public void ContinueToMap()
    {
        if (!ready || phase != Phase.Result)
            return;

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