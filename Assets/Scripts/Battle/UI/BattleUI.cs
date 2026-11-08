using UnityEngine;
using UnityEngine.UI;

// Main battle HUD: mana, piles, End Turn button, log, win/lose panels.
// Put on the BattleUI object (child of the main Canvas). BattleTransition turns it on/off. Also handles "click a card, then click an enemy" targeting.
public class BattleUI : MonoBehaviour
{
    public static BattleUI Instance { get; private set; }

    [SerializeField] BattleManager battle;

    [Header("Texts")]
    [SerializeField] Text manaText;
    [SerializeField] Text drawPileText;
    [SerializeField] Text discardPileText;
    [SerializeField] Text logText;
    [SerializeField] int logLines = 6;

    [Header("Buttons / panels")]
    [SerializeField] Button endTurnButton;
    [SerializeField] GameObject victoryPanel;
    [SerializeField] Text victoryCreditsText;
    [SerializeField] GameObject defeatPanel;

    public CardViewBase SelectedCard { get; private set; }   // 2D CardView or 3D CardView3D

    readonly System.Collections.Generic.Queue<string> log = new System.Collections.Generic.Queue<string>();

    void Awake() => Instance = this;

    void OnEnable()
    {
        if (!battle) battle = FindFirstObjectByType<BattleManager>();
        battle.OnStateChanged += HandleState;
        battle.OnLog += AddLog;
        battle.Deck.OnPilesChanged += Refresh;
        battle.Player.OnChanged += Refresh;
        if (endTurnButton) endTurnButton.onClick.AddListener(OnEndTurnClicked);

        // Battle UI is turned on/off every fight, so reset here (not in Start).
        if (victoryPanel) victoryPanel.SetActive(false);
        if (defeatPanel) defeatPanel.SetActive(false);
        ClearSelection();
        Refresh();
    }

    void OnDisable()
    {
        if (!battle) return;
        battle.OnStateChanged -= HandleState;
        battle.OnLog -= AddLog;
        if (battle.Deck) battle.Deck.OnPilesChanged -= Refresh;
        if (battle.Player) battle.Player.OnChanged -= Refresh;
        if (endTurnButton) endTurnButton.onClick.RemoveListener(OnEndTurnClicked);
    }

    void Refresh()
    {
        var p = battle.Player;
        if (manaText) manaText.text = $"{p.Mana}/{p.MaxMana + p.BonusManaPerTurn}";
        if (drawPileText) drawPileText.text = battle.Deck.drawPile.Count.ToString();
        if (discardPileText) discardPileText.text = battle.Deck.discardPile.Count.ToString();
    }

    void HandleState(BattleState s)
    {
        if (endTurnButton) endTurnButton.interactable = s == BattleState.PlayerTurn;
        if (s != BattleState.PlayerTurn) ClearSelection();

        if (s == BattleState.Victory && victoryPanel)
        {
            victoryPanel.SetActive(true);
            if (victoryCreditsText) victoryCreditsText.text = $"+{RunState.lastCreditsEarned} credits";
        }
        if (s == BattleState.Defeat && defeatPanel) defeatPanel.SetActive(true);
        Refresh();
    }

    void AddLog(string msg)
    {
        log.Enqueue(msg);
        while (log.Count > logLines) log.Dequeue();
        if (logText) logText.text = string.Join("\n", log);
    }

    void OnEndTurnClicked() => battle.EndPlayerTurn();

    // ---------- Targeting ----------

    // CardView / CardView3D calls this when clicked.
    public void OnCardClicked(CardViewBase card)
    {
        if (battle.State != BattleState.PlayerTurn) return;

        if (!card.Card.data.NeedsTarget)            // self / all-enemy cards play straight away
        {
            battle.TryPlayCard(card.Card, null);
            ClearSelection();
            return;
        }

        if (SelectedCard == card) { ClearSelection(); return; }
        ClearSelection();
        SelectedCard = card;
        card.SetSelected(true);
    }

    // EnemyClickTarget calls this when an enemy is clicked.
    public void OnEnemyClicked(EnemyCombatant enemy)
    {
        if (!SelectedCard) return;
        battle.TryPlayCard(SelectedCard.Card, enemy);
        ClearSelection();
    }

    public void ClearSelection()
    {
        if (SelectedCard) SelectedCard.SetSelected(false);
        SelectedCard = null;
    }

    // ---------- Panel buttons (hook these up in the Button's On Click) ----------

    // Victory panel "Continue": fade back to exploring.
    public void Continue()
    {
        if (BattleTransition.Instance) BattleTransition.Instance.ReturnToExploration();
    }

    // Defeat panel "Try again": reload the current scene.
    public void Restart()
    {
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        UnityEngine.SceneManagement.SceneManager.LoadScene(scene.name);
    }

    // Defeat panel "Back to Lobby".
    public void BackToLobby()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene("Lobby");
    }

    public void BackToMainMenu(string menuScene)
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(menuScene);
    }
}
