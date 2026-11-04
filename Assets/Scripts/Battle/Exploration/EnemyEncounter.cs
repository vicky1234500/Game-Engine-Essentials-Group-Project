using System.Collections.Generic;
using UnityEngine;

// Put on an enemy standing in the hallway (the 3D model you see while exploring).
// The enemy does NOT move. When the player walks forward and gets within engageRange,
// BattleTransition starts the fight in the same scene.
//
// One encounter can be a group (e.g. Rogue + Security Drone).
public class EnemyEncounter : MonoBehaviour
{
    [SerializeField] List<EnemyData> enemies = new List<EnemyData>();
    [SerializeField] float engageRange = 3f;
    [SerializeField] string playerTag = "Player";

    public List<EnemyData> Enemies => enemies;

    // Used by DungeonManager when it builds a room at runtime.
    public void Setup(List<EnemyData> group, float range)
    {
        enemies = new List<EnemyData>(group);
        engageRange = range;
    }

    Transform player;
    bool engaged;

    void Start()
    {
        var p = GameObject.FindGameObjectWithTag(playerTag);
        if (p) player = p.transform;
        else Debug.LogWarning($"{name}: no object tagged {playerTag}.");
    }

    void Update()
    {
        if (engaged || !player) return;
        if (BattleTransition.Instance && BattleTransition.Instance.Busy) return;

        if (Vector3.Distance(transform.position, player.position) <= engageRange)
        {
            engaged = true;
            if (BattleTransition.Instance) BattleTransition.Instance.BeginBattle(this);
            else Debug.LogError("EnemyEncounter: no BattleTransition in the scene.");
        }
    }

    // If the encounter is shown again after a lost fight.
    public void ResetEncounter() => engaged = false;

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, engageRange);
    }
}
