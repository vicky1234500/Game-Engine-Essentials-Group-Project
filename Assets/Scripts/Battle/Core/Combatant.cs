using System;
using UnityEngine;

public abstract class Combatant : MonoBehaviour
{
    [SerializeField] protected int maxHP = 100;

    [Tooltip("DF: % damage reduction with diminishing returns. See DefenseFormula (20 = 10%, 40 = 20%, 100 = 40%).")]
    [SerializeField] protected int baseDefense;

    public int MaxHP => maxHP;
    public int HP { get; protected set; }
    public int Shield { get; protected set; }

    public int BonusDefense { get; set; }

    public int Defense => baseDefense + BonusDefense;

    public float DamageReductionPercent =>
        DefenseFormula.ReductionPercent(Defense);

    public bool IsDead => HP <= 0;

    public StatusContainer Statuses { get; } = new StatusContainer();

    public abstract string DisplayName { get; }
    public virtual bool IsBoss => false;

    public event Action OnChanged;
    public event Action<Combatant> OnDied;

    protected void NotifyChanged() => OnChanged?.Invoke();

    public virtual void InitHP(int max, int current = -1)
    {
        maxHP = max;
        HP = current < 0 ? max : Mathf.Clamp(current, 1, max);
        Shield = 0;
        Statuses.Clear();
        NotifyChanged();
    }

    // Reduces maximum HP without healing or clearing combat state.
    // Refuses changes that would leave maximum HP below 1.
    public bool TryReduceMaxHP(int amount)
    {
        if (amount <= 0 || IsDead || maxHP <= amount)
            return false;

        maxHP -= amount;
        HP = Mathf.Min(HP, maxHP);

        NotifyChanged();
        return true;
    }

    public int TakeDamage(int amount, bool ignoresShield)
    {
        if (amount <= 0 || IsDead) return 0;

        if (!ignoresShield && Shield > 0)
        {
            int absorbed = Mathf.Min(Shield, amount);
            Shield -= absorbed;
            amount -= absorbed;
            if (absorbed > 0) GameAudio.Play(GameAudio.ShieldBlock);
        }

        return LoseHP(amount);
    }

    // Robots override this so they don't say "augh".
    protected virtual bool HasHurtVoice => true;

    void PlayHurtSound()
    {
        bool isEnemy = this is EnemyCombatant;
        if (isEnemy && HP <= 0) GameAudio.Play(GameAudio.EnemyDeath);
        else if (HasHurtVoice) GameAudio.Play(isEnemy ? GameAudio.EnemyHurt : GameAudio.PlayerHurt, 0.8f);
    }

    public int LoseHP(int amount)
    {
        if (amount <= 0 || IsDead) return 0;

        int lost = Mathf.Min(HP, amount);
        HP -= lost;
        if (lost > 0) PlayHurtSound();

        NotifyChanged();

        if (HP <= 0)
            OnDied?.Invoke(this);

        return lost;
    }

    public void Heal(int amount)
    {
        if (IsDead || amount <= 0) return;

        if (Statuses.Has(StatusType.HealingReduced))
            amount /= 2;

        HP = Mathf.Min(maxHP, HP + amount);
        NotifyChanged();
    }

    public void GainShield(int amount)
    {
        Shield += Mathf.Max(0, amount);
        NotifyChanged();
    }

    public void ClearShield()
    {
        Shield = 0;
        NotifyChanged();
    }

    public void ApplyStatus(StatusType type, int amount, int turns)
    {
        if (type == StatusType.ShortCircuit && IsBoss) return;

        Statuses.Add(type, amount, turns);
        NotifyChanged();
    }

    public void RemoveStatus(StatusType type)
    {
        Statuses.Remove(type);
        NotifyChanged();
    }

    public virtual void OnTurnStart()
    {
        ClearShield();
        Statuses.TickStartOfTurn(this);
        NotifyChanged();
    }

    public virtual void OnTurnEnd()
    {
        Statuses.TickEndOfTurn(this);
        NotifyChanged();
    }
}