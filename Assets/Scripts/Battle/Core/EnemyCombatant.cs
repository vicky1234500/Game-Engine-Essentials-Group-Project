using UnityEngine;

// One enemy in battle. BattleManager spawns these from EnemyData.
public class EnemyCombatant : Combatant
{
    [Tooltip("Capsule shown when the EnemyData has no model yet. Hidden once a real model is set.")]
    [SerializeField] GameObject placeholderModel;

    public EnemyData Data { get; private set; }
    public EnemyAI AI { get; private set; }
    public EnemyMove NextMove { get; private set; }   // the "intent" shown above its head
    public float DamageMultiplier { get; private set; } = 1f;   // difficulty (set by BattleManager)

    public override string DisplayName => Data ? Data.enemyName : name;
    public override bool IsBoss => Data && Data.rank == EnemyRank.Boss;
    protected override bool HasHurtVoice => Data && Data.hasHurtVoice;

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
        if (Statuses.Has(StatusType.Frozen))       { RemoveStatus(StatusType.Frozen);       return true; }
        return false;
    }
}
