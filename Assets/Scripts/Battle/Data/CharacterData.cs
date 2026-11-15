using System.Collections.Generic;
using UnityEngine;

// A playable character (picked in the Lobby). Doc: Character 1 HP 135 DF 25, starts with Knife + Bandage.
[CreateAssetMenu(fileName = "NewCharacter", menuName = "Roguelike/Character")]
public class CharacterData : ScriptableObject
{
    public string characterName = "Character 1";
    [TextArea] public string description = "";
    public Sprite portrait;                       // made by Tools > Roguelike > Make Character Portraits
    [Tooltip("3D model shown next to the character terminal in the Lobby when this character is picked. Also used for the portrait.")]
    public GameObject model;
    public Color color = new Color(0.25f, 0.55f, 0.9f);

    [Header("Stats")]
    public int maxHP = 135;
    public int baseDefense = 25;
    public int maxMana = 3;
    [Tooltip("Credits this character starts a run with.")]
    public int startingCredits = 50;

    [Header("Starting items")]
    public List<ItemData> startingItems = new List<ItemData>();

    [Tooltip("OFF = shows as locked in the Lobby (for characters that aren't made yet).")]
    public bool unlocked = true;
}
