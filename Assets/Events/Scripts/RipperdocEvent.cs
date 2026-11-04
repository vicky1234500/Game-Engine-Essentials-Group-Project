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

    [Header("Prototype starting values")]
    [Min(0)]
    [SerializeField] int startingCredits = 75;

    [Min(0)]
    [SerializeField] int startingDF = 25;

    [Header("Event rules")]
    [Min(0)]
    [SerializeField] int implantCost = 60;

    [Min(0)]
    [SerializeField] int defenseBonus = 10;

    [Header("Connect to the map later")]
    [SerializeField] UnityEvent onReturnToMap = new UnityEvent();

    enum Phase
    {
        Choice,
        Result,
        Complete
    }

    Phase phase;
    bool ready;
    int credits;
    int defense;
    int rejectionCount;

    public int Credits => credits;
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

        credits = Mathf.Max(0, startingCredits);
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

        titleText.text = "Back-Alley Ripperdoc";

        descriptionText.text =
            "Behind a flickering clinic sign, a shady surgeon "
            + "offers you an armor implant.\n"
            + "\"Pay for clean hardware, or try my experimental stock "
            + "for free. Your choice.\"";

        payButton.interactable = credits >= implantCost;
        freeButton.interactable = true;
        leaveButton.interactable = true;

        SetLabel(
            payButton,
            credits >= implantCost
                ? $"Pay {implantCost} credits - Gain {defenseBonus} DF"
                : $"Need {implantCost} credits - Not enough credits"
        );

        SetLabel(
            freeButton,
            $"Free implant - Gain {defenseBonus} DF + 1 Rejection curse"
        );

        SetLabel(leaveButton, "Leave");
        RefreshStatus();
    }

    public void BuyImplant()
    {
        if (!ready
            || phase != Phase.Choice
            || credits < implantCost)
        {
            return;
        }

        // Lock the choice before applying its effects.
        phase = Phase.Result;

        credits -= implantCost;
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
            $"DF: {defense}     Credits: {credits}"
            + $"     Rejection: {rejectionCount}";
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