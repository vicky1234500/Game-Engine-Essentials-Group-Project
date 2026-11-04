using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class RipperdocEvent : MonoBehaviour
{
    [Header("Event UI")]
    [SerializeField] GameObject eventPanel;
    [SerializeField] TMP_Text titleText;
    [SerializeField] TMP_Text descriptionText;
    [SerializeField] TMP_Text statusText;
    [SerializeField] Button payButton;
    [SerializeField] Button freeButton;
    [SerializeField] Button leaveButton;

    [Header("Prototype defense - connect later")]
    [Min(0)]
    [SerializeField] int startingDF = 25;

    [Header("Event rules")]
    [Min(0)]
    [SerializeField] int implantCost = 60;

    [Min(0)]
    [SerializeField] int defenseBonus = 10;

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
    int defense;
    int rejectionCount;

    public int Credits => RunState.credits;
    public int Defense => defense;

    // Prototype record only. Not connected to the combat deck yet.
    public int RejectionCount => rejectionCount;

    void Start()
    {
        ready = eventPanel
            && titleText
            && descriptionText
            && statusText
            && payButton
            && freeButton
            && leaveButton;

        if (!ready)
        {
            Debug.LogError(
                "RipperdocEvent: assign all Event UI fields.",
                this
            );

            if (payButton) payButton.interactable = false;
            if (freeButton) freeButton.interactable = false;
            if (leaveButton) leaveButton.interactable = false;
            return;
        }

        // Never reset the shared run balance when entering an event.
        defense = Mathf.Max(0, startingDF);
        implantCost = Mathf.Max(0, implantCost);
        defenseBonus = Mathf.Max(0, defenseBonus);
        rejectionCount = 0;
        phase = Phase.Choice;

        payButton.onClick.AddListener(BuyImplant);
        freeButton.onClick.AddListener(AcceptFreeImplant);
        leaveButton.onClick.AddListener(ContinueOrLeave);

        eventPanel.SetActive(true);
        payButton.gameObject.SetActive(true);
        freeButton.gameObject.SetActive(true);
        leaveButton.gameObject.SetActive(true);

        freeButton.interactable = true;
        leaveButton.interactable = true;

        titleText.text = "Back-Alley Ripperdoc";

        descriptionText.text =
            "Behind a flickering clinic sign, a shady surgeon "
            + "offers you an armor implant.\n"
            + "\"Pay for clean hardware, or try my experimental stock "
            + "for free. Your choice.\"";

        SetLabel(
            freeButton,
            $"Free implant - Gain {defenseBonus} DF + 1 Rejection curse"
        );

        SetLabel(leaveButton, "Leave");
        RefreshChoices();
    }

    void Update()
    {
        if (!ready || !eventPanel.activeInHierarchy)
            return;

        if (phase == Phase.Choice)
            RefreshChoices();
        else
            RefreshStatus();
    }

    void RefreshChoices()
    {
        payButton.interactable = RunState.credits >= implantCost;

        SetLabel(
            payButton,
            RunState.credits >= implantCost
                ? $"Pay {implantCost} credits - Gain {defenseBonus} DF"
                : $"Need {implantCost} credits - Not enough credits"
        );

        RefreshStatus();
    }

    public void BuyImplant()
    {
        if (!ready
            || phase != Phase.Choice
            || RunState.credits < implantCost)
        {
            return;
        }

        // Lock the choice before charging.
        phase = Phase.Result;

        RunState.credits -= implantCost;
        defense += defenseBonus;

        ShowResult(
            "The surgeon installs a reinforced armor implant.\n"
            + $"Paid {implantCost} credits. Gained {defenseBonus} DF.\n"
            + "The bonus lasts for this run."
        );
    }

    public void AcceptFreeImplant()
    {
        if (!ready || phase != Phase.Choice)
            return;

        phase = Phase.Result;

        defense += defenseBonus;
        rejectionCount++;

        ShowResult(
            "The experimental implant locks into place. "
            + "A warning flashes across its diagnostic screen.\n"
            + $"Gained {defenseBonus} DF and 1 Rejection curse "
            + "for this run.\n"
            + "Rejection: unplayable; lose 4 HP if it remains "
            + "in your hand at the end of a turn."
        );
    }

    void ShowResult(string message)
    {
        descriptionText.text = message;
        RefreshStatus();

        payButton.gameObject.SetActive(false);
        freeButton.gameObject.SetActive(false);

        leaveButton.gameObject.SetActive(true);
        leaveButton.interactable = true;
        SetLabel(leaveButton, "Continue");
    }

    void RefreshStatus()
    {
        statusText.text =
            $"DF: {defense}     Credits: {RunState.credits}";

        if (rejectionCount > 0)
            statusText.text += $"\nCurse: Rejection x{rejectionCount}";
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

    [ContextMenu("TEST - Set Run Credits To 75")]
    void TestSetCreditsTo75()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying
            || !ready
            || phase != Phase.Choice)
        {
            return;
        }

        RunState.credits = 75;
        RefreshChoices();
#endif
    }

    [ContextMenu("TEST - Set Run Credits To 59")]
    void TestSetCreditsTo59()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying
            || !ready
            || phase != Phase.Choice)
        {
            return;
        }

        RunState.credits = 59;
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