using UnityEngine;

public enum CardType { Attack, Defense, Skill, Heal }

[CreateAssetMenu(fileName = "NewCard", menuName = "Roguelike/Card")]
public class CardData : ScriptableObject
{
    public string cardName = "Swing Knife";
    public CardType type = CardType.Attack;
    public int manaCost = 1;
    public int value = 5;                 // damage, block or heal amount (used later by the battle system)
    [TextArea] public string description = "Deal 5 damage.";
}
