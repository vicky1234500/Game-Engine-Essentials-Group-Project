using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum BattleState { None, Setup, PlayerTurn, EnemyTurn, Victory, Defeat }

// Runs the fight: turn order, playing cards, enemy turns, win/lose.
// Lives in the SAME scene as the dungeon. BattleTransition calls StartBattle when an enemy is reached.
//
// Turn loop (state machine):
//   Setup -> PlayerTurn -> (End Turn) -> EnemyTurn -> PlayerTurn -> ...
//   any time all enemies die -> Victory, player dies -> Defeat
public class BattleManager : MonoBehaviour
{
    public static BattleManager Instance { get; private set; }

    [Header("Scene references")]
    [SerializeField] PlayerCombatant player;
    [SerializeField] DeckManager deck;
    [SerializeField] InventoryGrid inventory;          // deck is built from this (found automatically if empty)
    [SerializeField] EnemyCombatant enemyPrefab;       // EnemyCombatant + EnemyClickTarget + collider + HUD
    [SerializeField] Transform[] defaultSpawnPoints;   // used when no spawn points are passed in

    [Header("Testing")]
    [Tooltip("ON: starts a fight with the test enemies as soon as you press Play.")]
    [SerializeField] bool startTestBattleOnPlay;
    [SerializeField] List<EnemyData> testEnemies = new List<EnemyData>();
    [Tooltip("Used only if the backpack is empty.")]
    [SerializeField] List<CardData> testDeck = new List<CardData>();

    [Header("Timing")]
    [SerializeField] float enemyActionDelay = 0.8f;

    public BattleState State { get; private set; } = BattleState.None;
    public bool InBattle => State != BattleState.None;
    public PlayerCombatant Player => player;
    public DeckManager Deck => deck;
    public readonly List<EnemyCombatant> enemies = new List<EnemyCombatant>();
    public int TurnNumber { get; private set; }

    public event Action<BattleState> OnStateChanged;
    public event Action<string> OnLog;
    public event Action OnEnemiesChanged;
    public event Action<bool> OnBattleEnded;           // true = won

    Transform[] spawnPoints;
    bool extraTurnPending;

    void Awake()
    {
        Instance = this;
        player.OnDied += _ => CheckBattleOver();
    }

    void Start()
    {
        if (startTestBattleOnPlay) StartBattle(testEnemies, defaultSpawnPoints);
    }

    // ---------------- Setup ----------------

    public void StartBattle(List<EnemyData> enemyList, Transform[] points)
    {
        ClearBattle();
        spawnPoints = points != null && points.Length > 0 ? points : defaultSpawnPoints;
        SetState(BattleState.Setup);
        TurnNumber = 0;
        extraTurnPending = false;

        if (!inventory) inventory = FindFirstObjectByType<InventoryGrid>(FindObjectsInactive.Include);
        player.PrepareForBattle(inventory);

        if (inventory && inventory.Items.Count > 0) deck.BuildFromInventory(inventory);
        else deck.BuildFromList(testDeck);

        foreach (var data in enemyList) SpawnEnemy(data);
        foreach (var e in enemies) e.PlanNextMove();

        StartPlayerTurn();
    }

    public EnemyCombatant SpawnEnemy(EnemyData data)
    {
        if (!data || !enemyPrefab) return null;

        int index = enemies.Count;
        Transform point = spawnPoints != null && index < spawnPoints.Length ? spawnPoints[index] : transform;
        var enemy = Instantiate(enemyPrefab, point.position, point.rotation);
        enemy.name = data.enemyName;
        enemy.Setup(data);
        enemy.OnDied += OnEnemyDied;
        enemies.Add(enemy);

        if (State == BattleState.EnemyTurn || State == BattleState.PlayerTurn) enemy.PlanNextMove();
        OnEnemiesChanged?.Invoke();
        return enemy;
    }

    // Removes the battle enemies and resets state. BattleTransition calls this when going back to exploring.
    public void ClearBattle()
    {
        StopAllCoroutines();
        foreach (var e in enemies) if (e) Destroy(e.gameObject);
        enemies.Clear();
        deck.DiscardHand();
        SetState(BattleState.None);
        OnEnemiesChanged?.Invoke();
    }

    // ---------------- Player turn ----------------

    void StartPlayerTurn()
    {
        TurnNumber++;
        player.OnTurnStart();
        if (CheckBattleOver()) return;              // died to BURN/CORRODE

        if (player.HealPerTurn > 0) player.Heal(player.HealPerTurn);    // Black-Market Implant
        player.RefillMana();
        deck.ReturnCardsToHand();                  // Throw Knife etc.
        int draw = deck.CardsPerTurn + (TurnNumber == 1 ? player.ExtraDrawFirstTurn : 0);
        deck.Draw(draw);
        SetState(BattleState.PlayerTurn);
        Log($"--- Turn {TurnNumber} ---");
    }

    // Called by BattleUI when the player plays a card. target can be null for self/AoE cards.
    public bool TryPlayCard(CardInstance card, EnemyCombatant target)
    {
        if (State != BattleState.PlayerTurn) return false;
        if (!deck.hand.Contains(card)) return false;
        if (!card.Playable) { Log($"{card.data.cardName} can't be played."); return false; }
        if (!player.CanAfford(card.ManaCost)) { Log("Not enough mana."); return false; }
        if (card.data.NeedsTarget && (!target || target.IsDead)) { Log("Pick a target."); return false; }

        player.SpendMana(card.ManaCost);
        Log($"Player plays {card.data.cardName}");

        int bonusDamage = card.bonusDamage + (card.data.isGun ? player.GunDamageBonus : 0);   // Ammo Belt
        foreach (var effect in card.data.GetEffects())
            EffectResolver.Resolve(effect, player, target, this, bonusDamage, card.bonusShield);

        deck.MoveToDiscardOrExhaust(card);
        CheckBattleOver();
        return true;
    }

    // Called by the End Turn button.
    public void EndPlayerTurn()
    {
        if (State != BattleState.PlayerTurn) return;
        StartCoroutine(EnemyPhase());
    }

    // ---------------- Enemy turn ----------------

    IEnumerator EnemyPhase()
    {
        // End of player turn
        var leftInHand = deck.DiscardHand();
        // TODO: Rejection curse - lose 4 HP for each Rejection in leftInHand
        player.OnTurnEnd();
        if (CheckBattleOver()) yield break;

        if (extraTurnPending)                         // Tactical AI Core "Override"
        {
            extraTurnPending = false;
            StartPlayerTurn();
            yield break;
        }

        SetState(BattleState.EnemyTurn);

        foreach (var enemy in new List<EnemyCombatant>(enemies))
        {
            if (!enemy || enemy.IsDead) continue;

            enemy.OnTurnStart();
            if (CheckBattleOver()) yield break;
            if (enemy.IsDead) continue;

            yield return new WaitForSeconds(enemyActionDelay);

            if (enemy.ConsumeSkip()) Log($"{enemy.DisplayName} can't act!");
            else ExecuteEnemyMove(enemy, enemy.NextMove);

            enemy.OnTurnEnd();
            if (CheckBattleOver()) yield break;
        }

        foreach (var enemy in enemies) if (!enemy.IsDead) enemy.PlanNextMove();
        StartPlayerTurn();
    }

    void ExecuteEnemyMove(EnemyCombatant enemy, EnemyMove move)
    {
        if (move == null) return;
        Log($"{enemy.DisplayName} uses {move.moveName}");
        enemy.AI.RegisterUsed(move);

        // HACKED: its next action hits a random target, which can be its own side.
        Combatant forcedTarget = null;
        if (enemy.Statuses.Has(StatusType.Hacked))
        {
            var all = new List<Combatant> { player };
            foreach (var e in enemies) if (!e.IsDead) all.Add(e);
            forcedTarget = all[UnityEngine.Random.Range(0, all.Count)];
            enemy.RemoveStatus(StatusType.Hacked);
        }

        foreach (var effect in move.effects)
        {
            if (forcedTarget && effect.target != TargetType.Self)
            {
                var copy = new CardEffect
                {
                    type = effect.type, target = TargetType.SingleEnemy, amount = effect.amount, hits = effect.hits,
                    ignoresShield = effect.ignoresShield, status = effect.status, duration = effect.duration, summon = effect.summon
                };
                EffectResolver.Resolve(copy, enemy, forcedTarget, this);
            }
            else EffectResolver.Resolve(effect, enemy, null, this);
        }
    }

    // ---------------- Targeting ----------------

    // "Enemy" means the other side: for the player that's enemies, for an enemy that's the player.
    public List<Combatant> GetTargets(Combatant source, TargetType type, Combatant chosen)
    {
        var result = new List<Combatant>();
        bool fromPlayer = source is PlayerCombatant;

        switch (type)
        {
            case TargetType.Self:
                result.Add(source);
                break;

            case TargetType.SingleEnemy:
                if (chosen) result.Add(chosen);
                else if (!fromPlayer) result.Add(player);
                break;

            case TargetType.AllEnemies:
                if (fromPlayer) { foreach (var e in enemies) if (!e.IsDead) result.Add(e); }
                else result.Add(player);
                break;

            case TargetType.RandomEnemy:
                if (fromPlayer)
                {
                    var alive = enemies.FindAll(e => !e.IsDead);
                    if (alive.Count > 0) result.Add(alive[UnityEngine.Random.Range(0, alive.Count)]);
                }
                else result.Add(player);
                break;
        }
        return result;
    }

    // ---------------- End of battle ----------------

    void OnEnemyDied(Combatant c)
    {
        Log($"{c.DisplayName} is defeated!");
        OnEnemiesChanged?.Invoke();
        CheckBattleOver();
    }

    bool CheckBattleOver()
    {
        if (State == BattleState.None || State == BattleState.Setup) return false;
        if (State == BattleState.Victory || State == BattleState.Defeat) return true;
        if (player.IsDead) { EndBattle(false); return true; }
        if (enemies.TrueForAll(e => e.IsDead)) { EndBattle(true); return true; }
        return false;
    }

    void EndBattle(bool won)
    {
        StopAllCoroutines();

        if (won)
        {
            int credits = BattleRewards.RollCredits(enemies, player.CreditBonusPercent);
            RunState.lastCreditsEarned = credits;
            RunState.credits += credits;
            Log($"Victory! +{credits} credits");
        }
        else Log("Defeat...");

        SetState(won ? BattleState.Victory : BattleState.Defeat);
        OnBattleEnded?.Invoke(won);
    }

    public void GrantExtraTurn() => extraTurnPending = true;

    void SetState(BattleState s)
    {
        State = s;
        OnStateChanged?.Invoke(s);
    }

    public void Log(string msg)
    {
        Debug.Log("[Battle] " + msg);
        OnLog?.Invoke(msg);
    }
}
