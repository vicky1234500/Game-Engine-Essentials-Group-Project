// All status effects from the design doc (STATUS EFFECTS section + older cards).
// SHIELD is not here: it's its own number on Combatant because almost everything uses it.
public enum StatusType
{
    Corrode,       // X damage per turn, X drops by 1 each turn
    Burn,          // X damage per turn for 3 turns, ignores SHIELD
    Bleed,         // older cards (Deep Stab, Impale) - damage per turn
    Poison,        // Mad Scientist / Poison Burst - damage per turn
    Glitched,      // deals 25% less damage
    Overloaded,    // takes 25% more damage
    ShortCircuit,  // skips its next action (bosses are immune)
    Cloaked,       // attacks against it have a 50% chance to miss
    Hacked,        // its next action hits a random target (can be its own side)
    Overclocked,   // player buff: +3 attack damage, lose 2 HP at end of turn, 2 turns
    Frozen,        // Cryo Gun - skips next action
    Slowed,        // Cryo Gun
    Dizzy,         // Dizzy Poison
    HealingReduced, // Parasite - healing received -50%
    Regen,          // Nano-Injector: +X HP at the end of each turn
    Decoy,          // Holo Decoy: the next attack against it misses completely
    DroneStrike     // Attack Drone: deals X damage to a random enemy at the end of each player turn
}
