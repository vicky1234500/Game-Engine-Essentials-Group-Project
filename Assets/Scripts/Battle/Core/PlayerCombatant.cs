using UnityEngine;

// The player in battle. Put on the Player object (same one with PlayerMovement).
// HP carries over between fights because it's the same object all run.
public class PlayerCombatant : Combatant
{
    [SerializeField] string playerName = "Character 1";
    [SerializeField] int maxMana = 3;

    public int Mana { get; private set; }
    public int MaxMana => maxMana;

    // From item passives, recalculated at the start of every fight.
    public int BonusManaPerTurn { get; private set; }
    public int GunDamageBonus { get; private set; }
    public int HealPerTurn { get; private set; }
    public int ExtraDrawFirstTurn { get; private set; }
    public int CreditBonusPercent { get; private set; }
    public int HealAfterBattle { get; private set; }

    public override string DisplayName => playerName;

    bool initialized;

    void Awake()
    {
        // Character picked in the Lobby overrides the Inspector values.
        var c = RunState.selectedCharacter;
        if (c)
        {
            playerName = c.characterName;
            maxHP = c.maxHP;
            baseDefense = c.baseDefense;
            maxMana = c.maxMana;
        }
        InitHP(maxHP);
        initialized = true;
    }

    // Called by BattleManager when a fight starts. Keeps HP, clears fight-only stuff.
    public void PrepareForBattle(InventoryGrid inventory)
    {
        if (!initialized) { InitHP(maxHP); initialized = true; }
        Shield = 0;
        Statuses.Clear();
        ApplyPassives(inventory);
        NotifyChanged();
    }

    // Add up passives from every item in the backpack.
    void ApplyPassives(InventoryGrid inventory)
    {
        BonusDefense = 0; BonusManaPerTurn = 0; GunDamageBonus = 0;
        HealPerTurn = 0; ExtraDrawFirstTurn = 0; CreditBonusPercent = 0; HealAfterBattle = 0;
        BonusDefense = RunState.bonusDefense;                 // Ripperdoc implant etc. (lasts the whole run)
        if (!inventory) return;

        foreach (var item in inventory.Items)
        {
            if (item.data.passives == null) continue;
            foreach (var p in item.data.passives)
            {
                switch (p.type)
                {
                    case PassiveType.BonusDF:            BonusDefense += p.amount; break;
                    case PassiveType.GunDamage:          GunDamageBonus += p.amount; break;
                    case PassiveType.BonusManaPerTurn:   BonusManaPerTurn += p.amount; break;
                    case PassiveType.HealPerTurn:        HealPerTurn += p.amount; break;
                    case PassiveType.ExtraDrawFirstTurn: ExtraDrawFirstTurn += p.amount; break;
                    case PassiveType.CreditBonusPercent: CreditBonusPercent += p.amount; break;
                    case PassiveType.HealAfterBattle:    HealAfterBattle += p.amount; break;
                }
            }
        }
    }

    public void RefillMana()
    {
        Mana = maxMana + BonusManaPerTurn;
        NotifyChanged();
    }

    public bool CanAfford(int cost) => Mana >= cost;

    public void SpendMana(int cost)
    {
        Mana = Mathf.Max(0, Mana - cost);
        NotifyChanged();
    }

    public void GainMana(int amount)
    {
        Mana += amount;
        NotifyChanged();
    }
}
