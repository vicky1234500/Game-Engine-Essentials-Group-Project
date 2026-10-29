using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class CardEntry
{
    public CardData card;
    [Min(1)] public int count = 1;
}

[CreateAssetMenu(fileName = "NewItem", menuName = "Roguelike/Item")]
public class ItemData : ScriptableObject
{
    public string itemName = "Knife";
    public Sprite icon;                                   // draw it in the item's shape (knife = 2:1)
    public Vector2Int size = new Vector2Int(2, 1);        // width x height in slots
    public Color tint = new Color(0.22f, 0.54f, 0.87f);   // attack blue, heal green, defense purple...
    public List<CardEntry> cards = new List<CardEntry>();

    public int TotalCards
    {
        get
        {
            int total = 0;
            foreach (var entry in cards)
                if (entry.card) total += entry.count;
            return total;
        }
    }
}
