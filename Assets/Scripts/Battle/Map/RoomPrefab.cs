using UnityEngine;

// Put this on the ROOT of every room prefab (Assets/Prefab/Rooms/<Type>/).
// It tells DungeonManager where things go. Make empty child objects and drag them in:
//
//   Player Spawn - where the player appears. About 1 m above the floor.
//                  Blue arrow (forward) = the way the player looks.
//   Enemy Spawn  - where the enemy stands (fight rooms). On the floor.
//                  Blue arrow should point TOWARDS the player.
//   Door Slots   - where doors can go, left to right. Blue arrow points OUT of the room.
//                  A room leads to 1-3 next rooms, so give it up to 3 slots.
//                  Unused slots get walled off automatically.
//
// The room is placed at (0,0,0) with no rotation, so build it around the origin.
public class RoomPrefab : MonoBehaviour
{
    public Transform playerSpawn;
    public Transform enemySpawn;
    public Transform[] doorSlots;

    // Draws the markers in the Scene view so you can see them while building the room.
    void OnDrawGizmos()
    {
        if (playerSpawn) Marker(playerSpawn, Color.green, 0.4f);
        if (enemySpawn) Marker(enemySpawn, Color.red, 0.5f);
        if (doorSlots == null) return;
        foreach (var d in doorSlots)
        {
            if (!d) continue;
            Gizmos.color = Color.cyan;
            Gizmos.matrix = d.localToWorldMatrix;
            Gizmos.DrawWireCube(new Vector3(0, 1.3f, 0), new Vector3(1.6f, 2.6f, 0.1f));
            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.DrawLine(d.position + Vector3.up, d.position + Vector3.up + d.forward);
        }
    }

    static void Marker(Transform t, Color c, float r)
    {
        Gizmos.color = c;
        Gizmos.DrawWireSphere(t.position, r);
        Gizmos.DrawLine(t.position, t.position + t.forward * 1.2f);
    }
}
