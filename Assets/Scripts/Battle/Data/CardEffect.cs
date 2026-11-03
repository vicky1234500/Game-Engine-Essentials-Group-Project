using UnityEngine;

// What one effect on a card (or enemy move) does.
// A card is a list of these, e.g. Shield Bash = [Shield 12] + [Damage 8] + [ApplyStatus Glitched 1].
public enum EffectType
{
    Damage,          // amount x hits
    Shield,          // gain SHIELD
    Heal,            // restore HP
    ApplyStatus,     // apply `status` with `amount` stacks for `duration` turns
    RemoveStatuses,  // Purge: removes Corrode, Burn, Glitched, Hacked
    GainMana,
    StealMana,       // Energy Extractor / System Overload
    Draw,            // draw `amount` cards
    ExtraTurn,       // Override
    Summon,          // Warden Mech summons drones (enemy only)
    DamagePerStatus  // Dissolve: damage = amount x target's `status` stacks (3 x CORRODE)
}

public enum TargetType
{
    Self,
    SingleEnemy,   // player picks a target
    AllEnemies,
    RandomEnemy
}

[System.Serializable]
public class CardEffect
{
    public EffectType type = EffectType.Damage;
    public TargetType target = TargetType.SingleEnemy;
    public int amount = 5;
    [Min(1)] public int hits = 1;            // "-4 DAMAGE three times" -> amount 4, hits 3
    public bool ignoresShield;               // Railgun, Armor Piercing
    [Header("Only for ApplyStatus")]
    public StatusType status;
    public int duration = 1;
    [Header("Only for Summon")]
    public EnemyData summon;
}
