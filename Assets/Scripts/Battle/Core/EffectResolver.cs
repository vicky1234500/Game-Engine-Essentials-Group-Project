using System.Collections.Generic;
using UnityEngine;

// Turns a CardEffect into actual damage / shield / statuses.
// Used by BOTH player cards and enemy moves, so the rules stay in one place.
public static class EffectResolver
{
    public const float GlitchedMultiplier = 0.75f;    // GLITCHED: deals 25% less damage
    public const float OverloadedMultiplier = 1.25f;  // OVERLOADED: takes 25% more damage
    public const int OverclockedBonus = 3;            // OVERCLOCKED: attacks deal +3 DAMAGE
    public const float CloakedMissChance = 0.5f;      // CLOAKED: 50% chance to miss

    public static void Resolve(CardEffect e, Combatant source, Combatant chosenTarget,
                               BattleManager battle, int bonusDamage = 0, int bonusShield = 0)
    {
        List<Combatant> targets = battle.GetTargets(source, e.target, chosenTarget);

        switch (e.type)
        {
            case EffectType.Damage:
                foreach (var t in targets)
                    for (int h = 0; h < e.hits && !t.IsDead; h++)
                        DealDamage(source, t, e.amount + bonusDamage, e.ignoresShield, battle);
                break;

            case EffectType.Shield:
                foreach (var t in targets) t.GainShield(e.amount + bonusShield);
                break;

            case EffectType.Heal:
                foreach (var t in targets) t.Heal(e.amount);
                break;

            case EffectType.ApplyStatus:
                foreach (var t in targets) t.ApplyStatus(e.status, e.amount, e.duration);
                break;

            case EffectType.RemoveStatuses:   // Bandage "Purge"
                foreach (var t in targets)
                {
                    t.RemoveStatus(StatusType.Corrode);
                    t.RemoveStatus(StatusType.Burn);
                    t.RemoveStatus(StatusType.Glitched);
                    t.RemoveStatus(StatusType.Hacked);
                }
                break;

            case EffectType.GainMana:
                if (source is PlayerCombatant p) p.GainMana(e.amount);
                break;

            case EffectType.StealMana:
                if (source is PlayerCombatant pl) pl.GainMana(e.amount);           // Energy Extractor
                else battle.Player.SpendMana(e.amount);                              // System Overload
                break;

            case EffectType.Draw:
                if (source is PlayerCombatant) battle.Deck.Draw(e.amount);
                break;

            case EffectType.ExtraTurn:
                battle.GrantExtraTurn();
                break;

            case EffectType.DamagePerStatus:
                foreach (var t in targets)
                {
                    int dmg = t.Statuses.Amount(e.status) * e.amount;
                    if (dmg > 0) DealDamage(source, t, dmg + bonusDamage, e.ignoresShield, battle);
                    else battle.Log($"{t.DisplayName} has no {e.status}.");
                }
                break;

            case EffectType.RemoveShield:
                foreach (var t in targets) t.ClearShield();
                break;

            case EffectType.DamagePerAttackPlayed:
                foreach (var t in targets)
                    DealDamage(source, t, e.amount * battle.AttackCardsPlayed + bonusDamage, e.ignoresShield, battle);
                break;

            case EffectType.Summon:
                for (int i = 0; i < Mathf.Max(1, e.amount); i++) battle.SpawnEnemy(e.summon);
                break;
        }
    }

    public static void DealDamage(Combatant source, Combatant target, int baseDamage, bool ignoresShield, BattleManager battle)
    {
        if (target.Statuses.Has(StatusType.Decoy))                  // Holo Decoy: always misses, once
        {
            target.RemoveStatus(StatusType.Decoy);
            battle.Log($"{source.DisplayName} hits the decoy instead of {target.DisplayName}!");
            return;
        }
        if (target.Statuses.Has(StatusType.Cloaked) && Random.value < CloakedMissChance)
        {
            battle.Log($"{source.DisplayName}'s attack missed {target.DisplayName}!");
            return;
        }

        int dmg = CalculateDamage(source, target, baseDamage);
        int lost = target.TakeDamage(dmg, ignoresShield);
        if (lost > 0) GameAudio.Play(GameAudio.Hit);
        battle.Log($"{source.DisplayName} hits {target.DisplayName} for {dmg} ({lost} HP lost)");
    }

    // Order: flat bonuses first, then multipliers.
    public static int CalculateDamage(Combatant source, Combatant target, int baseDamage)
    {
        float dmg = baseDamage;
        if (source && source.Statuses.Has(StatusType.Overclocked)) dmg += OverclockedBonus;
        if (source && source.Statuses.Has(StatusType.Glitched)) dmg *= GlitchedMultiplier;
        if (target.Statuses.Has(StatusType.Overloaded)) dmg *= OverloadedMultiplier;
        if (source is EnemyCombatant enemy) dmg *= enemy.DamageMultiplier;       // difficulty
        int rounded = Mathf.Max(0, Mathf.RoundToInt(dmg));
        return DefenseFormula.Apply(rounded, target.Defense);    // DF reduces damage taken (then SHIELD absorbs)
    }
}
