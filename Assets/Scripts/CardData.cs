using System.Collections.Generic;
using UnityEngine;

public enum CardType { Attack, Defense, Skill, Heal, Curse }

// Sound when a card (or enemy move) hits. Auto = Laser for guns, Punch for everything else.
public enum AttackSound { Auto, Punch, Slash, Laser, Explosion, LaserBurst, Railgun }

// Curse cards that do something by themselves (doc CURSE CARDS).
public enum CurseTrigger
{
    None,
    OnDrawLoseMana,          // System Error: when drawn, lose X MANA this turn
    OnDrawEnemiesGainShield, // Tracker: when drawn, all enemies gain X SHIELD
    EndOfTurnInHandLoseHP    // Rejection: still in hand at end of turn -> lose X HP
}

[CreateAssetMenu(fileName = "NewCard", menuName = "Roguelike/Card")]
public class CardData : ScriptableObject
{
    public string cardName = "Swing Knife";
    public CardType type = CardType.Attack;
    public int manaCost = 1;
    public int value = 5;                 // kept for old cards: used as a single Damage/Shield/Heal if `effects` is empty
    [TextArea] public string description = "Deal 5 damage.";

    [Header("Battle")]
    public List<CardEffect> effects = new List<CardEffect>();
    public bool exhaust;                  // removed for the rest of the fight after playing
    public bool unplayable;               // curses like Jammed, Static, Rejection
    [Tooltip("Nanite Knife Throw Knife: comes back to your hand at the start of next turn instead of the discard pile.")]
    public bool returnToHandNextTurn;
    [Tooltip("Pistol, SMG, Shotgun, Railgun cards. Ammo Belt gives these bonus damage.")]
    public bool isGun;
    [Tooltip("Sound when this card hits. Auto = Laser for guns, Punch for everything else.")]
    public AttackSound attackSound = AttackSound.Auto;

    [Header("Curse cards")]
    public CurseTrigger curseTrigger = CurseTrigger.None;
    public int curseAmount;

    // Does this card need the player to click an enemy?
    public bool NeedsTarget
    {
        get
        {
            foreach (var e in GetEffects())
                if (e.target == TargetType.SingleEnemy) return true;
            return false;
        }
    }

    // Old cards only have `value` filled in, so turn that into one effect.
    public List<CardEffect> GetEffects()
    {
        if (effects != null && effects.Count > 0) return effects;

        var fallback = new CardEffect { amount = value };
        switch (type)
        {
            case CardType.Defense: fallback.type = EffectType.Shield; fallback.target = TargetType.Self; break;
            case CardType.Heal:    fallback.type = EffectType.Heal;   fallback.target = TargetType.Self; break;
            case CardType.Curse:   return new List<CardEffect>();
            default:               fallback.type = EffectType.Damage; fallback.target = TargetType.SingleEnemy; break;
        }
        return new List<CardEffect> { fallback };
    }
}
