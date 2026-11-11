using System.Collections;
using UnityEngine;

// One enemy in battle. BattleManager spawns these from EnemyData.
public class EnemyCombatant : Combatant
{
    [Tooltip("Capsule shown when the EnemyData has no model yet. Hidden once a real model is set.")]
    [SerializeField] GameObject placeholderModel;

    [Header("Animation")]
    [SerializeField] string attackBool = "Attack";      // Bool parameter
    [SerializeField] string dieTrigger = "Die";         // Trigger parameter
    [Tooltip("Seconds from the start of the attack animation until the hit lands (damage is applied).")]
    [SerializeField] float attackHitDelay = 0.4f;
    [Tooltip("Seconds after the hit before the next enemy acts (rest of the attack animation).")]
    [SerializeField] float attackRecoverTime = 0.5f;

    public EnemyData Data { get; private set; }
    public EnemyAI AI { get; private set; }
    public EnemyMove NextMove { get; private set; }   // the "intent" shown above its head
    public float DamageMultiplier { get; private set; } = 1f;   // difficulty (set by BattleManager)

    public float AttackHitDelay => attackHitDelay;
    public float AttackRecoverTime => attackRecoverTime;

    public override string DisplayName => Data ? Data.enemyName : name;
    public override bool IsBoss => Data && Data.rank == EnemyRank.Boss;
    protected override bool HasHurtVoice => Data && Data.hasHurtVoice;

    private Animator animator;          // found on the model (child object)
    private Coroutine attackRoutine;    // only our own timer, so other coroutines keep running

    // hpMultiplier / damageMultiplier come from the difficulty settings (DungeonManager).
    public void Setup(EnemyData data, float hpMultiplier = 1f, float damageMultiplier = 1f)
    {
        DamageMultiplier = damageMultiplier;
        Data = data;
        AI = new EnemyAI(data);
        InitHP(Mathf.Max(1, Mathf.RoundToInt(data.maxHP * hpMultiplier)));

        if (data.modelPrefab)
        {
            EnemyModel.Spawn(data, transform, Vector3.zero);
            if (placeholderModel) placeholderModel.SetActive(false);
        }

        animator = GetComponentInChildren<Animator>();

        // play the die animation when HP reaches 0
        OnDied += _ => PlayDie();
    }

    // Decide (and show) what this enemy will do on its next turn.
    public void PlanNextMove()
    {
        NextMove = AI.ChooseMove(this);
        NotifyChanged();
    }

    // Should this enemy skip its action this turn?
    public bool ConsumeSkip()
    {
        if (Statuses.Has(StatusType.ShortCircuit)) { RemoveStatus(StatusType.ShortCircuit); return true; }
        if (Statuses.Has(StatusType.Frozen)) { RemoveStatus(StatusType.Frozen); return true; }
        return false;
    }

    // ---------- Animations ----------

    // Finds the Animator again if it wasn't ready during Setup.
    Animator GetAnimator()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();
        if (animator == null) Debug.LogWarning($"{DisplayName}: no Animator found on the model (child).");
        return animator;
    }

    // Attack = true, then false after the whole attack (hit delay + recover time).
    public void PlayAttack()
    {
        if (IsDead || GetAnimator() == null) return;

        if (attackRoutine != null) StopCoroutine(attackRoutine);
        attackRoutine = StartCoroutine(AttackRoutine());


    }

    IEnumerator AttackRoutine()
    {
        animator.SetBool(attackBool, true);
        yield return new WaitForSeconds(attackHitDelay + attackRecoverTime);
        if (animator != null) animator.SetBool(attackBool, false);
        attackRoutine = null;
    }

    // Plays automatically when the enemy dies.
    public void PlayDie()
    {
        if (GetAnimator() == null) return;

        if (attackRoutine != null) StopCoroutine(attackRoutine);
        animator.SetBool(attackBool, false);
        animator.SetTrigger(dieTrigger);
    }
}