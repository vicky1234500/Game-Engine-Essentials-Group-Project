using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class WoundedNetrunnerEvent : MonoBehaviour
{
    [Header("Event UI")]
    [SerializeField] GameObject eventPanel;
    [SerializeField] TMP_Text titleText;
    [SerializeField] TMP_Text descriptionText;
    [SerializeField] TMP_Text statusText;
    [SerializeField] Button helpButton;
    [SerializeField] Button robButton;
    [SerializeField] Button leaveButton;

    [Header("Help her")]
    [Min(0)]
    [SerializeField] int helpHPCost = 10;
    [SerializeField] InventoryGrid inventory;             // set by the EventExplorationBridge

    [Header("Robbery reward")]
    [Min(0)]
    [SerializeField] int robberyCredits = 50;

    [Header("Exploration callback")]
    [SerializeField] UnityEvent onReturnToMap = new UnityEvent();

    enum Phase
    {
        Choice,
        Result,
        Closed
    }

    Phase phase;
    bool ready;

    // Prototype record only; not connected to the combat deck.
    int trackerCount;

    public int Credits => RunState.credits;
    public int TrackerCount => trackerCount;

    void Start()
    {
        ready = eventPanel
            && titleText
            && descriptionText
            && statusText
            && helpButton
            && robButton
            && leaveButton;

        if (!ready)
        {
            Debug.LogError(
                "WoundedNetrunnerEvent: assign all Event UI fields.",
                this
            );

            if (helpButton) helpButton.interactable = false;
            if (robButton) robButton.interactable = false;
            if (leaveButton) leaveButton.interactable = false;
            return;
        }

        // Keep the existing shared run balance.
        robberyCredits = Mathf.Max(0, robberyCredits);
        trackerCount = 0;
        phase = Phase.Choice;

        robButton.onClick.AddListener(RobNetrunner);
        helpButton.onClick.AddListener(HelpNetrunner);
        leaveButton.onClick.AddListener(LeaveOrContinue);

        eventPanel.SetActive(true);
        helpButton.gameObject.SetActive(true);
        robButton.gameObject.SetActive(true);
        leaveButton.gameObject.SetActive(true);

        var p = RunEffects.Player;
        helpButton.interactable = p && p.HP > helpHPCost && ItemDatabase.FindItem("Cyberdeck");
        robButton.interactable = true;
        leaveButton.interactable = true;

        titleText.text = "Wounded Netrunner";

        descriptionText.text =
            "Behind a dumpster, a wounded hacker clutches "
            + "a damaged terminal.\n"
            + "\"Help me get somewhere safe. "
            + "You can have my Cyberdeck.\"";

        SetLabel(
            helpButton,
            "Help her - Lose 10 HP; receive Cyberdeck"
        );

        SetLabel(
            robButton,
            $"Rob her - Gain {robberyCredits} credits + 1 Tracker"
        );

        SetLabel(leaveButton, "Leave");
        RefreshStatus();
    }

    void Update()
    {
        if (ready && eventPanel.activeInHierarchy)
            RefreshStatus();
    }

    public void HelpNetrunner()
    {
        var p = RunEffects.Player;
        if (!ready || phase != Phase.Choice || !p || p.HP <= helpHPCost)
            return;

        phase = Phase.Result;
        p.LoseHP(helpHPCost);
        string got = RunEffects.GiveItem(inventory, ItemDatabase.FindItem("Cyberdeck"));

        descriptionText.text =
            "You get her somewhere safe. It costs you, but she keeps her word.\n"
            + $"Lost {helpHPCost} HP.\n" + got;

        RefreshStatus();
        helpButton.gameObject.SetActive(false);
        robButton.gameObject.SetActive(false);
        leaveButton.gameObject.SetActive(true);
        leaveButton.interactable = true;
        SetLabel(leaveButton, "Continue");
    }

    public void RobNetrunner()
    {
        if (!ready || phase != Phase.Choice)
            return;

        // Lock the choice before granting the reward.
        phase = Phase.Result;

        RunState.credits += robberyCredits;
        trackerCount++;
        RunEffects.AddCurse("Tracker");                          // in your deck for the rest of the run

        descriptionText.text =
            "You take her credits and turn away.\n"
            + "Behind you, her terminal sends a tracking report.\n"
            + $"Received {robberyCredits} credits.\n"
            + "Added 1 Tracker curse to your deck for this run.";

        RefreshStatus();

        helpButton.gameObject.SetActive(false);
        robButton.gameObject.SetActive(false);

        leaveButton.gameObject.SetActive(true);
        leaveButton.interactable = true;

        SetLabel(leaveButton, "Continue");
    }

    void RefreshStatus()
    {
        statusText.text = $"Credits: {RunState.credits}";

        if (trackerCount > 0)
            statusText.text += $"\nCurse: Tracker x{trackerCount}";
    }

    public void LeaveOrContinue()
    {
        if (!ready || phase == Phase.Closed)
            return;

        phase = Phase.Closed;
        eventPanel.SetActive(false);
        onReturnToMap.Invoke();
    }

    static void SetLabel(Button button, string label)
    {
        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);

        if (text)
            text.text = label;
    }
}