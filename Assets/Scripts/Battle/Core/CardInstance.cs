// One physical card in the deck during a fight.
// Wraps CardData so the same asset can appear many times, and so upgrades
// (Portable Workbench +3 DAMAGE/+3 SHIELD) or temporary curses (Glitch) don't change the asset.
public class CardInstance
{
    public CardData data;
    public ItemData sourceItem;     // which item gave this card (null for curses added in battle)
    public int bonusDamage;         // from upgrades / Equipment Miniaturizer
    public int bonusShield;
    public bool temporary;          // e.g. Glitch from Netrunner: removed after this fight

    public CardInstance(CardData data, ItemData sourceItem = null)
    {
        this.data = data;
        this.sourceItem = sourceItem;
    }

    public int ManaCost => data.manaCost;
    // Curses are unplayable unless they're like Jammed ("pay 1 MANA to exhaust it").
    public bool Playable => !data.unplayable;
}
