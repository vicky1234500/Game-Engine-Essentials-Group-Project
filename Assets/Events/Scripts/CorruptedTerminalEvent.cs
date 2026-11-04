using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class CorruptedTerminalEvent : MonoBehaviour
{
    [Header("Event UI")]
    [SerializeField] GameObject eventPanel;
    [SerializeField] TMP_Text titleText;
    [SerializeField] TMP_Text descriptionText;
    [SerializeField] TMP_Text statusText;
    [SerializeField] Button hackButton;
    [SerializeField] Button purgeButton;
    [SerializeField] Button leaveButton;

    [Header("Hack rules")]
    [Min(0)]
    [SerializeField] int creditReward = 75;

    [Min(1)]
    [SerializeField] int staticCopiesPerFight = 2;

    [Min(1)]
    [SerializeField] int affectedFights = 3;

    [Header("Exploration callback")]
    [SerializeField] UnityEvent onReturnToMap = new UnityEvent();

    enum Phase
    {
        Choice,
        Result,
        Complete
    }

    Phase phase;
    bool ready;

    // Prototype records; connect to the combat system later.
    int staticCopies;
    int remainingAffectedFights;

    public int Credits => RunState.credits;
    public int StaticCopiesPerFight => staticCopies;

    public int RemainingAffectedFights =>
        remainingAffectedFights;

    void Start()
    {
        ready = eventPanel
            && titleText
            && descriptionText
            && statusText
            && hackButton
            && purgeButton
            && leaveButton;

        if (!ready)
        {
            Debug.LogError(
                "CorruptedTerminalEvent: assign all Event UI fields.",
                this
            );

            if (hackButton) hackButton.interactable = false;
            if (purgeButton) purgeButton.interactable = false;
            if (leaveButton) leaveButton.interactable = false;
            return;
        }

        // Keep the existing shared run balance.
        creditReward = Mathf.Max(0, creditReward);
        staticCopiesPerFight = Mathf.Max(1, staticCopiesPerFight);
        affectedFights = Mathf.Max(1, affectedFights);

        staticCopies = 0;
        remainingAffectedFights = 0;
        phase = Phase.Choice;

        hackButton.onClick.AddListener(HackTerminal);
        leaveButton.onClick.AddListener(ContinueOrLeave);

        eventPanel.SetActive(true);

        hackButton.gameObject.SetActive(true);
        purgeButton.gameObject.SetActive(true);
        leaveButton.gameObject.SetActive(true);

        hackButton.interactable = true;

        // Item selection and curse removal are not connected yet.
        purgeButton.interactable = false;

        leaveButton.interactable = true;

        titleText.text = "Corrupted Terminal";

        descriptionText.text =
            "A damaged terminal is still connected to a corporate bank.\n"
            + "Credits are within reach, but corrupted code "
            + "lurks behind the login screen.";

        SetLabel(
            hackButton,
            $"Hack - Gain {creditReward} credits; "
            + $"{staticCopiesPerFight} Static cards for "
            + $"{affectedFights} fights"
        );

        SetLabel(
            purgeButton,
            "Purge - Curse removal connection pending"
        );

        SetLabel(leaveButton, "Leave");
        RefreshStatus();
    }

    void Update()
    {
        if (ready && eventPanel.activeInHierarchy)
            RefreshStatus();
    }

    public void HackTerminal()
    {
        if (!ready || phase != Phase.Choice)
            return;

        // Lock the choice before granting credits.
        phase = Phase.Result;

        RunState.credits += creditReward;
        staticCopies = staticCopiesPerFight;
        remainingAffectedFights = affectedFights;

        descriptionText.text =
            "The transfer succeeds, but corrupted code "
            + "infects your combat systems.\n"
            + $"Received {creditReward} credits.\n"
            + $"Recorded: add {staticCopies} Static curse cards "
            + $"to each of your next {remainingAffectedFights} fights.\n"
            + "Static is unplayable and takes up a hand slot.";

        RefreshStatus();

        hackButton.gameObject.SetActive(false);
        purgeButton.gameObject.SetActive(false);

        leaveButton.gameObject.SetActive(true);
        leaveButton.interactable = true;

        SetLabel(leaveButton, "Continue");
    }

    void RefreshStatus()
    {
        statusText.text = $"Credits: {RunState.credits}";

        if (remainingAffectedFights > 0)
        {
            statusText.text +=
                $"\nDebuff: {staticCopies} Static cards per fight"
                + $" - {remainingAffectedFights} fights remaining";
        }
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
        onReturnToMap.Invoke();
    }

    static void SetLabel(Button button, string label)
    {
        TMP_Text text = button.GetComponentInChildren<TMP_Text>(true);

        if (text)
            text.text = label;
    }
}