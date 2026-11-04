using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class FightRingEvent : MonoBehaviour
{
    [Header("Event UI")]
    [SerializeField] GameObject eventPanel;
    [SerializeField] TMP_Text titleText;
    [SerializeField] TMP_Text descriptionText;
    [SerializeField] TMP_Text statusText;
    [SerializeField] Button fightButton;
    [SerializeField] Button betButton;
    [SerializeField] Button leaveButton;

    [Header("Bet rules")]
    [Min(1)]
    [SerializeField] int betCost = 30;

    [Min(0)]
    [SerializeField] int winningPayout = 60;

    [Range(0f, 1f)]
    [SerializeField] float winChance = 0.5f;

    public enum BetTestOutcome
    {
        Random,
        AlwaysWin,
        AlwaysLose
    }

    [Header("Editor testing only")]
    [SerializeField]
    BetTestOutcome betTestOutcome =
        BetTestOutcome.Random;

    [Header("Elite fight - connect later")]
    [SerializeField] bool eliteFightConnected = false;

    [Min(0)]
    [SerializeField] int eliteVictoryCredits = 100;

    [SerializeField] UnityEvent onStartEliteFight = new UnityEvent();

    [Header("Exploration and death callbacks")]
    [SerializeField] UnityEvent onReturnToMap = new UnityEvent();
    [SerializeField] UnityEvent onGameOver = new UnityEvent();

    enum Phase
    {
        Choice,
        Result,
        Fighting,
        Complete,
        Dead
    }

    Phase phase;
    bool ready;

    public int Credits => RunState.credits;

    void Start()
    {
        ready = eventPanel
            && titleText
            && descriptionText
            && statusText
            && fightButton
            && betButton
            && leaveButton;

        if (!ready)
        {
            Debug.LogError(
                "FightRingEvent: assign all Event UI fields.",
                this
            );

            if (fightButton) fightButton.interactable = false;
            if (betButton) betButton.interactable = false;
            if (leaveButton) leaveButton.interactable = false;
            return;
        }

        // Keep the existing shared run balance.
        betCost = Mathf.Max(1, betCost);
        winningPayout = Mathf.Max(0, winningPayout);
        eliteVictoryCredits = Mathf.Max(0, eliteVictoryCredits);

        phase = Phase.Choice;

        fightButton.onClick.AddListener(StartEliteFight);
        betButton.onClick.AddListener(PlaceBet);
        leaveButton.onClick.AddListener(ContinueOrLeave);

        eventPanel.SetActive(true);
        fightButton.gameObject.SetActive(true);
        betButton.gameObject.SetActive(true);
        leaveButton.gameObject.SetActive(true);

        titleText.text = "Underground Fight Ring";

        descriptionText.text =
            "Behind a steel door, a crowd surrounds a neon-lit ring.\n"
            + "The announcer calls for a challenger. "
            + "A bookmaker offers you a bet on the house fighter.";

        fightButton.interactable = eliteFightConnected;
        leaveButton.interactable = true;

        SetLabel(
            fightButton,
            eliteFightConnected
                ? $"Fight an elite - Win {eliteVictoryCredits} credits, no item reward"
                : "Fight an elite - Combat connection pending"
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
        betButton.interactable = RunState.credits >= betCost;

        SetLabel(
            betButton,
            RunState.credits >= betCost
                ? $"Bet {betCost} credits - {winChance:P0} chance to receive {winningPayout}"
                : $"Need {betCost} credits - Not enough credits"
        );

        RefreshStatus();
    }

    public void PlaceBet()
    {
        if (!ready
            || phase != Phase.Choice
            || RunState.credits < betCost)
        {
            return;
        }

        // Lock the choice before deducting the stake.
        phase = Phase.Result;
        RunState.credits -= betCost;

        bool won = Random.value < winChance;

        if (Application.isEditor
            && betTestOutcome != BetTestOutcome.Random)
        {
            won = betTestOutcome == BetTestOutcome.AlwaysWin;
        }

        if (won)
        {
            RunState.credits += winningPayout;

            ShowResult(
                "The house fighter wins. The bookmaker pays up.\n"
                + $"Paid {betCost} credits. Received {winningPayout} credits."
            );
        }
        else
        {
            ShowResult(
                "The house fighter falls. Your bet is lost.\n"
                + $"Paid {betCost} credits. Received 0 credits."
            );
        }
    }

    public void StartEliteFight()
    {
        if (!ready
            || phase != Phase.Choice
            || !eliteFightConnected)
        {
            return;
        }

        phase = Phase.Fighting;
        eventPanel.SetActive(false);

        // The future integration must resume combat, start an elite
        // battle, suppress its normal rewards, and report the outcome.
        onStartEliteFight.Invoke();
    }

    public void ResolveEliteVictory()
    {
        if (!ready || phase != Phase.Fighting)
            return;

        phase = Phase.Result;
        RunState.credits += eliteVictoryCredits;

        ShowResult(
            "The crowd erupts as your opponent falls.\n"
            + $"Received {eliteVictoryCredits} credits. No item reward."
        );
    }

    public void ResolveEliteDefeat()
    {
        if (!ready || phase != Phase.Fighting)
            return;

        phase = Phase.Dead;
        eventPanel.SetActive(true);

        titleText.text = "Game Over";
        descriptionText.text = "You fall in the underground ring.";
        RefreshStatus();

        fightButton.gameObject.SetActive(false);
        betButton.gameObject.SetActive(false);
        leaveButton.gameObject.SetActive(false);

        onGameOver.Invoke();
    }

    void ShowResult(string message)
    {
        phase = Phase.Result;
        eventPanel.SetActive(true);

        descriptionText.text = message;
        RefreshStatus();

        fightButton.gameObject.SetActive(false);
        betButton.gameObject.SetActive(false);

        leaveButton.gameObject.SetActive(true);
        leaveButton.interactable = true;

        SetLabel(leaveButton, "Continue");
    }

    void RefreshStatus()
    {
        statusText.text = $"Credits: {RunState.credits}";
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

    [ContextMenu("TEST - Set Run Credits To 29")]
    void TestSetCreditsTo29()
    {
#if UNITY_EDITOR
        if (!Application.isPlaying
            || !ready
            || phase != Phase.Choice)
        {
            return;
        }

        RunState.credits = 29;
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