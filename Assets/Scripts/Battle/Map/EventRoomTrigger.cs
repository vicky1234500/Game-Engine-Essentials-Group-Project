using UnityEngine;

// Put on the root of an Event room prefab (Assets/Prefab/Rooms/Event/).
// Like an enemy: walk up to the event object (the pillar) and the team's EventExplorationBridge
// opens a random event popup (12 events, no repeats until all have been seen). Once per room.
public class EventRoomTrigger : MonoBehaviour
{
    [Tooltip("What you walk up to. Empty = the child called 'EventPillar...', otherwise this object.")]
    [SerializeField] Transform eventPoint;
    [SerializeField] float triggerRange = 3f;
    [SerializeField] string playerTag = "Player";

    Transform player;
    EventExplorationBridge bridge;
    bool used;

    void Start()
    {
        if (!eventPoint)
            foreach (var t in GetComponentsInChildren<Transform>(true))
                if (t.name.StartsWith("EventPillar")) { eventPoint = t; break; }
        if (!eventPoint) eventPoint = transform;

        var p = GameObject.FindGameObjectWithTag(playerTag);
        if (p) player = p.transform;

        bridge = FindFirstObjectByType<EventExplorationBridge>();
        if (!bridge)
            Debug.LogWarning("EventRoomTrigger: no EventExplorationBridge in the scene. " +
                             "Run Tools > Roguelike > Set Up Event Rooms.", this);
    }

    void Update()
    {
        if (used || !player || !bridge) return;

        Vector3 a = player.position, b = eventPoint.position;
        a.y = b.y = 0;
        if (Vector3.Distance(a, b) > triggerRange) return;

        // Only counts as used if it really opened (blocked while the backpack is open, during a fade...)
        if (bridge.TryOpenRandomEvent()) used = true;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(eventPoint ? eventPoint.position : transform.position, triggerRange);
    }
}
