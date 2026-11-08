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

    [Header("Actual dungeon player")]
    [SerializeField] PlayerCombatant player;

    [Header("Event rules")]
    [Min(1)]
    [SerializeField] int maxHPCost = 10;

    [Min(1)]
    [SerializeField] int overclockedTurns = 2;

    [Header("Exploration and death callbacks")]
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

    // Prototype records only.
    // Combat effects and boss intel still need integration.
    bool floorOverclockEnabled;
    bool bossIntelUnlocked;

    public int CurrentHP => player ? player.HP : 0;
    public int MaxHP => player ? player.MaxHP : 0;
    public bool FloorOverclockEnabled => floorOverclockEnabled;
    public int OverclockedTurns => overclockedTurns;
    public bool BossIntelUnlocked => bossIntelUnlocked;

    void Start()
    {
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
            && acceptButton
            && askButton
            && leaveButton
            && player;

        if (!ready)
        {
            Debug.LogError(
                "AIOracleEvent: missing UI or PlayerCombatant. "
                + "Test this event in the Dungeon.",
                this
            );

            if (acceptButton) acceptButton.interactable = false;
            if (askButton) askButton.interactable = false;

            if (leaveButton)
            {
                leaveButton.gameObject.SetActive(true);
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

        // Never initialize or reset the player's HP here.
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
        leaveButton.interactable = true;

        titleText.text = "Rogue AI Oracle";

        descriptionText.text =
            "A voice crackles from a broken screen.\n"
            + "\"Your survival rate is inefficient. "
            + "Allow me to optimize you... or ask what awaits ahead.\"";

        SetLabel(
            askButton,
            "Ask about the boss - Reveal its moves before battle"
        );

        SetLabel(leaveButton, "Leave");

        if (player.IsDead)
        {
            ShowDeath();
            return;
        }

        RefreshChoices();
    }

    void Update()
    {
        if (!ready || !eventPanel.activeInHierarchy)
            return;

        if (player.IsDead && phase != Phase.Dead)
        {
            ShowDeath();
            return;
        }

        if (phase == Phase.Choice)
            RefreshChoices();
        else
            RefreshStatus();
    }

    void RefreshChoices()
    {
        bool canAccept =
            !player.IsDead && player.MaxHP > maxHPCost;

        acceptButton.interactable = canAccept;
        askButton.interactable = !player.IsDead;

        SetLabel(
            acceptButton,
            canAccept
                ? $"Accept - Max HP -{maxHPCost}; "
                    + "start battles OVERCLOCKED this floor"
                : $"Cannot accept - Max HP must exceed {maxHPCost}"
        );

        RefreshStatus();
    }

    public void AcceptOptimization()
    {
        if (!ready
            || phase != Phase.Choice
            || player.IsDead
            || player.MaxHP <= maxHPCost)
        {
            return;
        }

        // Lock before applying effects to prevent duplicate activation.
        phase = Phase.Result;

        if (!player.TryReduceMaxHP(maxHPCost))
        {
            phase = Phase.Choice;
            RefreshChoices();
            return;
        }

        floorOverclockEnabled = true;
        RunState.battleStartOverclockTurns = overclockedTurns;  // every fight this floor starts OVERCLOCKED

        ShowResult(
            "A surge of code rewrites your combat routines.\n"
            + $"Maximum HP reduced by {maxHPCost} for this run.\n"
            + "Optimization installed: start every battle on this floor "
            + $"OVERCLOCKED for {overclockedTurns} turns."
        );
    }

    public void AskAboutBoss()
    {
        if (!ready
            || phase != Phase.Choice
            || player.IsDead)
        {
            return;
        }

        phase = Phase.Result;
        bossIntelUnlocked = true;
        RunState.bossIntel = true;                               // boss moves shown in the boss room + battle log

        ShowResult(
            "The AI transfers an encrypted combat dossier.\n"
            + "Boss intel: the boss's moves will be shown when you "
            + "reach its room and at the start of the fight."
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

    void ShowDeath()
    {
        if (phase == Phase.Dead) return;

        phase = Phase.Dead;
        titleText.text = "Game Over";
        descriptionText.text = "You have no HP remaining.";

        RefreshStatus();

        acceptButton.gameObject.SetActive(false);
        askButton.gameObject.SetActive(false);
        leaveButton.gameObject.SetActive(false);

        onGameOver.Invoke();
    }

    void RefreshStatus()
    {
        statusText.text = $"HP: {player.HP} / {player.MaxHP}";

        if (floorOverclockEnabled)
        {
            statusText.text +=
                "\nOptimization: Recorded";
        }

        if (bossIntelUnlocked)
        {
            statusText.text +=
                "\nBoss intel: Recorded";
        }
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

    static void SetLabel(Button button, string label)
    {
        TMP_Text text =
            button.GetComponentInChildren<TMP_Text>(true);

        if (text)
            text.text = label;
    }
}