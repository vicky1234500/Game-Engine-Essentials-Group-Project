using System.Collections.Generic;

// One active status on a combatant.
public class StatusInstance
{
    public StatusType type;
    public int amount;     // stacks (CORRODE 2, BURN 3...)
    public int turnsLeft;  // how many more turns it lasts

    public StatusInstance(StatusType type, int amount, int turns)
    {
        this.type = type; this.amount = amount; turnsLeft = turns;
    }
}

// Holds all statuses on one combatant and ticks them each turn.
public class StatusContainer
{
    readonly Dictionary<StatusType, StatusInstance> statuses = new Dictionary<StatusType, StatusInstance>();

    public IEnumerable<StatusInstance> All => statuses.Values;
    public bool Has(StatusType t) => statuses.ContainsKey(t);
    public int Amount(StatusType t) => statuses.TryGetValue(t, out var s) ? s.amount : 0;

    // Stacking rule (team can change this): amounts add up, duration takes the longer one.
    public void Add(StatusType t, int amount, int turns)
    {
        if (statuses.TryGetValue(t, out var s))
        {
            s.amount += amount;
            if (turns > s.turnsLeft) s.turnsLeft = turns;
        }
        else statuses[t] = new StatusInstance(t, amount, turns);
    }

    public void Remove(StatusType t) => statuses.Remove(t);
    public void Clear() => statuses.Clear();

    // Called at the START of the owner's turn: damage-over-time.
    public void TickStartOfTurn(Combatant owner)
    {
        if (statuses.TryGetValue(StatusType.Corrode, out var corrode))
        {
            owner.TakeDamage(corrode.amount, ignoresShield: false);
            corrode.amount -= 1;                         // X drops by 1 each turn
            if (corrode.amount <= 0) Remove(StatusType.Corrode);
        }
        if (statuses.TryGetValue(StatusType.Burn, out var burn))
            owner.TakeDamage(burn.amount, ignoresShield: true);   // BURN ignores SHIELD
        if (statuses.TryGetValue(StatusType.Bleed, out var bleed))
            owner.TakeDamage(bleed.amount, ignoresShield: true);
        if (statuses.TryGetValue(StatusType.Poison, out var poison))
            owner.TakeDamage(poison.amount, ignoresShield: true);
    }

    // Called at the END of the owner's turn: count down durations.
    public void TickEndOfTurn(Combatant owner)
    {
        if (Has(StatusType.Overclocked)) owner.LoseHP(2);   // OVERCLOCKED: lose 2 HP at end of each turn
        if (statuses.TryGetValue(StatusType.Regen, out var regen)) owner.Heal(regen.amount);   // heals before counting down

        var expired = new List<StatusType>();
        foreach (var s in statuses.Values)
        {
            if (s.type == StatusType.Corrode) continue;     // Corrode counts down by amount instead
            s.turnsLeft--;
            if (s.turnsLeft <= 0) expired.Add(s.type);
        }
        foreach (var t in expired) Remove(t);
    }
}
