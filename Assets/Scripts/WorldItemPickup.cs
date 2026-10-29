using UnityEngine;

// Put this on the item object in the 3D world (e.g. Knife).
// Needs: a Collider with Is Trigger checked + a Rigidbody with Is Kinematic checked.
[RequireComponent(typeof(Collider))]
public class WorldItemPickup : MonoBehaviour
{
    [SerializeField] ItemData item;              // e.g. the Knife asset from Assets/Data/Items
    [SerializeField] InventoryGrid inventory;    // optional: found automatically if left empty
    [SerializeField] string playerTag = "Player";
    [Tooltip("ON: items are only picked up while the inventory is closed.")]
    [SerializeField] bool onlyWhenInventoryClosed = true;

    [Header("Pickup message")]
    [Tooltip("Shown on screen when picked up. {item} = item name, {cards} = number of cards.")]
    [SerializeField] string pickupMessage = "Picked up {item}";

    [Header("Storage full message")]
    [SerializeField] string fullMessage = "Storage full";
    [SerializeField] Color messageColor = new Color(1f, 0.3f, 0.35f);
    [SerializeField] float messageHeight = 0.8f;     // how far above the item
    [SerializeField] float messageSize = 0.03f;      // bigger = larger text
    [SerializeField] float messageDuration = 3f;     // seconds the message stays visible
    [SerializeField] float retryInterval = 0.5f;     // while standing on the item, re-check for space this often

    [Header("Idle animation (optional)")]
    [SerializeField] bool animate = true;
    [SerializeField] float spinSpeed = 90f;
    [SerializeField] float bobHeight = 0.15f;
    [SerializeField] float bobSpeed = 2f;

    Vector3 startPos;
    bool pickedUp;
    float nextRetry;
    float hideAt;
    TextMesh message;
    Camera cam;

    void Start()
    {
        startPos = transform.position;
        cam = Camera.main;

        // InventoryMenu starts hidden, so include inactive objects in the search.
        if (!inventory)
            inventory = FindFirstObjectByType<InventoryGrid>(FindObjectsInactive.Include);

        if (!inventory) Debug.LogWarning($"{name}: no InventoryGrid found in the scene.");
        if (!item) Debug.LogWarning($"{name}: no ItemData assigned.");


        CreateMessage();
    }

    void Update()
    {
        if (!animate) return;
        transform.Rotate(0f, spinSpeed * Time.deltaTime, 0f, Space.World);
        transform.position = startPos + Vector3.up * (Mathf.Sin(Time.time * bobSpeed) * bobHeight);
    }

    void LateUpdate()
    {
        if (!message || !message.gameObject.activeSelf) return;

        if (Time.time >= hideAt) { ShowMessage(false); return; }

        // Float above the item and face the same way as the camera, so it's always readable.
        message.transform.position = transform.position + Vector3.up * messageHeight;
        if (cam) message.transform.rotation = cam.transform.rotation;
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag(playerTag)) TryPickup(showMessageIfFull: true);
    }

    // If the player frees space (e.g. trashes an item) while still standing here, pick it up.
    void OnTriggerStay(Collider other)
    {
        if (Time.time < nextRetry || !other.CompareTag(playerTag)) return;
        nextRetry = Time.time + retryInterval;
        TryPickup(showMessageIfFull: false);   // silent re-check, the message isn't restarted
    }

    void TryPickup(bool showMessageIfFull)
    {
        if (pickedUp || !inventory || !item) return;

        // InventoryMenu (where InventoryGrid lives) is only active while the inventory is open.
        // Skip for now; the retry in OnTriggerStay picks it up right after the inventory closes.
        if (onlyWhenInventoryClosed && inventory.gameObject.activeInHierarchy) return;

        // Only pick up if the item actually fits in the backpack.
        if (!inventory.HasSpaceFor(item))
        {
            if (showMessageIfFull) ShowMessage(true);
            return;
        }

        if (inventory.TryAddItem(item))
        {
            pickedUp = true;
            PickupNotifier.Show(pickupMessage
                .Replace("{item}", item.itemName)
                .Replace("{cards}", item.TotalCards.ToString()));
            Destroy(gameObject);
        }
    }

    // ---------- Floating 3D text (legacy TextMesh, created in code, no setup needed) ----------

    void CreateMessage()
    {
        var go = new GameObject($"{name}_FullMessage");
        message = go.AddComponent<TextMesh>();

        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        message.font = font;
        go.GetComponent<MeshRenderer>().sharedMaterial = font.material;

        message.text = fullMessage;
        message.color = messageColor;
        message.fontSize = 64;                  // high font size + small character size = sharp text
        message.characterSize = messageSize;
        message.anchor = TextAnchor.MiddleCenter;
        message.alignment = TextAlignment.Center;
        message.fontStyle = FontStyle.Bold;

        go.SetActive(false);
    }

    void ShowMessage(bool show)
    {
        if (!message) return;
        if (show)
        {
            hideAt = Time.time + messageDuration;   // (re)start the timer
            message.transform.position = transform.position + Vector3.up * messageHeight;
            if (cam) message.transform.rotation = cam.transform.rotation;
        }
        message.gameObject.SetActive(show);
    }

    void OnDestroy()
    {
        if (message) Destroy(message.gameObject);
    }
}