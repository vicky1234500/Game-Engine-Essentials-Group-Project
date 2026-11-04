using UnityEngine;

// A door in the end chamber of a room. Walk into it to go to that room.
// Made at runtime by DungeonManager (trigger collider + kinematic rigidbody).
public class DungeonDoor : MonoBehaviour
{
    [SerializeField] string playerTag = "Player";

    MapNode target;

    public void Setup(MapNode node) => target = node;

    void OnTriggerEnter(Collider other)
    {
        if (target == null || !other.CompareTag(playerTag)) return;
        if (DungeonManager.Instance) DungeonManager.Instance.TryEnter(target);
    }

    // Standing in the doorway while the enemy is still alive, then winning -> still lets you through.
    void OnTriggerStay(Collider other) => OnTriggerEnter(other);
}
