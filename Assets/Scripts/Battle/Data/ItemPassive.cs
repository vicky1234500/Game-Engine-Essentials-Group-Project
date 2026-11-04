// Passive bonuses an item gives just by being in the backpack.
// e.g. Kinetic Vest: BonusDF 10. Ammo Belt: GunDamage 2. Neural Booster: BonusManaPerTurn 1.
public enum PassiveType
{
    BonusDF,             // Kinetic Vest +10, Overclocked Exo-Suit +30
    GunDamage,           // Ammo Belt: gun cards deal +X DAMAGE
    BonusManaPerTurn,    // Neural Booster +1 MANA every turn
    HealPerTurn,         // Black-Market Implant +5 HP at the start of every turn
    ExtraDrawFirstTurn,  // Tactical AI Core: draw 2 extra cards on turn 1
    CreditBonusPercent,  // Stolen Credit Chip +50% money
    ShopDiscountPercent, // Shop Spoofer: shop prices -15%
    HealAfterBattle,     // Med Pod: +15 HP after each won fight
    RevealMap,           // Map Uplink: the map screen shows the whole floor
    RestHealBonusPercent // Field Repair Kit: rest rooms heal more (needs Rest rooms, not made yet)
}

[System.Serializable]
public class ItemPassive
{
    public PassiveType type;
    public int amount;
}