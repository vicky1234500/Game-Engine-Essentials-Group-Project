using System.Collections.Generic;
using UnityEngine;

public enum EnemyRank { Normal, Elite, Boss }

public enum IntentType { Attack, Defend, Buff, Debuff, Heal, Special }

// One move an enemy can do (Slash, Smoke Bomb, Missile Barrage...).
[System.Serializable]
public class EnemyMove
{
    public string moveName = "Slash";
    public IntentType intent = IntentType.Attack;   // which icon to show above the enemy
    public List<CardEffect> effects = new List<CardEffect>();
}

[CreateAssetMenu(fileName = "NewEnemy", menuName = "Roguelike/Enemy")]
public class EnemyData : ScriptableObject
{
    public string enemyName = "Rogue";
    public EnemyRank rank = EnemyRank.Normal;
    public int maxHP = 30;
    public GameObject modelPrefab;                  // 3D model, in the hallway and in battle
    [Tooltip("The model is scaled to this height, in metres. The room ceiling is 3 m.")]
    public float modelHeight = 1.8f;
    [Tooltip("Metres above the floor. For enemies that hover, like drones.")]
    public float modelLift = 0f;
    [Tooltip("Turns the model, in degrees. Use 180 if it has its back to the player.")]
    public float modelYaw = 0f;
    [Tooltip("OFF for robots: no \"augh\" voice when hit.")]
    public bool hasHurtVoice = true;
    [Tooltip("Used in this order, then loops: Move 1 -> Move 2 -> Move 3 -> Move 1...")]
    public List<EnemyMove> moves = new List<EnemyMove>();

    [Header("Rewards")]
    public int minCredits = 10;
    public int maxCredits = 20;
}
