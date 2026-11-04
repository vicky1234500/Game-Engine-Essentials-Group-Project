using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class AIOracleEvent : MonoBehaviour
{
    [Header("Event UI")]
    [SerializeField] GameObject eventPanel;
    [SerializeField] TMP_Text titleText;
    [SerializeField] TMP_Text descriptionText;
    [SerializeField] TMP_Text statusText;
    [SerializeField] Button acceptButton;
    [SerializeField] Button askButton;
    [SerializeField] Button leaveButton;

    [Header("Prototype starting values")]
    [Min(1)]
    [SerializeField] int startingMaxHP = 135;

    [Min(1)]
    [SerializeField] int startingHP = 135;

    [Header("Event rules")]
    [Min(1)]
    [SerializeField] int maxHPCost = 10;

    [Min(1)]
    [SerializeField] int overclockedTurns = 2;

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

    int hp;
    int maxHP;

    // Prototype records only.
    // These must be connected to the run and combat systems later.
    bool floorOverclockEnabled;
    bool bossIntelUnlocked;

    public int CurrentHP => hp;
    public int MaxHP => maxHP;
    public bool FloorOverclockEnabled => floorOverclockEnabled;
    public int OverclockedTurns => overclockedTurns;
    public bool BossIntelUnlocked => bossIntelUnlocked;

    void Start()
    {
        ready = eventPanel
            && titleText
            && descriptionText
            && statusText
            && acceptButton
            && askButton
            && leaveButton;

        if (!ready)
        {
            Debug.LogError(
                "AIOracleEvent: assign all Event UI fields.",
                this
            );

            if (acceptButton) acceptButton.interactable = false;
            if (askButton) askButton.interactable = false;
            if (leaveButton) leaveButton.interactable = false;
            return;
        }

        maxHP = Mathf.Max(1, startingMaxHP);
        hp = Mathf.Clamp(startingHP, 1, maxHP);

        maxHPCost = Mathf.Max(1, maxHPCost);
        overclockedTurns = Mathf.Max(1, overclockedTurns);

        floorOverclockEnabled = false;
        bossIntelUnlocked = false;
        phase = Phase.Choice;

        acceptButton.onClick.AddListener(AcceptOptimization);
        askButton.onClick.AddListener(AskAboutBoss);
        leaveButton.onClick.AddListener(ContinueOrLeave);

        eventPanel.SetActive(true);
        acceptButton.gameObject.SetActive(true);
        askButton.gameObject.SetActive(true);
        leaveButton.gameObject.SetActive(true);

        titleText.text = "Rogue AI Oracle";

        descriptionText.text =
            "A voice crackles from a broken screen.\n"
            + "\"Your survival rate is inefficient. "
            + "Allow me to optimize you... or ask what awaits ahead.\"";

        acceptButton.interactable = maxHP > maxHPCost;
        askButton.interactable = true;
        leaveButton.interactable = true;

        SetLabel(
            acceptButton,
            maxHP > maxHPCost
                ? $"Accept - Max HP -{maxHPCost}; "
                    + "start battles OVERCLOCKED this floor"
                : $"Cannot accept - Max HP must exceed {maxHPCost}"
        );

        SetLabel(
            askButton,
            "Ask about the boss - Reveal its moves before battle"
        );

        SetLabel(leaveButton, "Leave");
        RefreshStatus();
    }

    public void AcceptOptimization()
    {
        if (!ready
            || phase != Phase.Choice
            || maxHP <= maxHPCost)
        {
            return;
        }

        // Lock the choice before applying its effects.
        phase = Phase.Result;

        maxHP -= maxHPCost;
        hp = Mathf.Min(hp, maxHP);
        floorOverclockEnabled = true;

        ShowResult(
            "A surge of code rewrites your combat routines.\n"
            + $"Maximum HP reduced by {maxHPCost} for this run.\n"
            + "Optimization recorded: start every battle on this floor "
            + $"OVERCLOCKED for {overclockedTurns} turns."
        );
    }

    public void AskAboutBoss()
    {
        if (!ready || phase != Phase.Choice)
            return;

        phase = Phase.Result;
        bossIntelUnlocked = true;

        ShowResult(
            "The AI transfers an encrypted combat dossier.\n"
            + "Boss intel recorded: reveal all of the boss's moves "
            + "before the fight."
        );
    }

    void ShowResult(string message)
    {
        phase = Phase.Result;
        descriptionText.text = message;
        RefreshStatus();

        acceptButton.gameObject.SetActive(false);
        askButton.gameObject.SetActive(false);

        leaveButton.gameObject.SetActive(true);
        leaveButton.interactable = true;

        SetLabel(leaveButton, "Continue");
    }

    void RefreshStatus()
    {
        string overclockState =
            floorOverclockEnabled ? "Recorded" : "None";

        string intelState =
            bossIntelUnlocked ? "Recorded" : "None";

        statusText.text =
            $"HP: {hp} / {maxHP}"
            + $"     Optimization: {overclockState}"
            + $"     Boss intel: {intelState}";
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