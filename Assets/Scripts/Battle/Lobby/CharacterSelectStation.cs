using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// The character terminal in the Lobby. Walk up to it and press E to open the character select panel.
[ExecuteAlways]   // also lines up on the model in the Scene view, not only in Play mode
public class CharacterSelectStation : MonoBehaviour
{
    [SerializeField] CharacterSelectUI selectUI;
    [SerializeField] Text promptText;                 // "Press E to choose your character"
    [SerializeField] float interactRange = 2.5f;
    [SerializeField] string playerTag = "Player";
    [Tooltip("Optional: name of a scene object this terminal should stand in front of, with its label on top (the computer model).")]
    [SerializeField] string standInFrontOf = "";

    Transform player;

    void Start()
    {
        if (!string.IsNullOrEmpty(standInFrontOf)) PlaceAtModel();
        if (!Application.isPlaying) return;

        var p = GameObject.FindGameObjectWithTag(playerTag);
        if (p) player = p.transform;
        if (promptText) promptText.gameObject.SetActive(false);
    }

    void Update()
    {
        if (!Application.isPlaying || !player || !selectUI) return;

        Vector3 a = player.position, b = transform.position;
        a.y = b.y = 0;
        bool near = Vector3.Distance(a, b) <= interactRange && !selectUI.IsOpen;
        if (promptText) promptText.gameObject.SetActive(near);

        if (near && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            selectUI.Open();
    }

    // Uses the model's real size, so the terminal lands on it wherever the model is placed.
    void PlaceAtModel()
    {
        var model = GameObject.Find(standInFrontOf);
        var renderers = model ? model.GetComponentsInChildren<Renderer>() : new Renderer[0];
        if (renderers.Length == 0)
        {
            Debug.LogWarning($"CharacterSelectStation: couldn't find a model called '{standInFrontOf}'.", this);
            return;
        }

        Bounds bounds = renderers[0].bounds;
        foreach (var r in renderers) bounds.Encapsulate(r.bounds);

        // The front is the thinner side, on the side facing the middle of the room.
        Vector3 centre = new Vector3(bounds.center.x, 0f, bounds.center.z);
        bool thinInX = bounds.size.x < bounds.size.z;
        Vector3 front = thinInX ? new Vector3(-Mathf.Sign(centre.x), 0f, 0f) : new Vector3(0f, 0f, -Mathf.Sign(centre.z));
        float depth = thinInX ? bounds.extents.x : bounds.extents.z;

        transform.SetPositionAndRotation(centre + front * (depth + 0.7f), Quaternion.LookRotation(-front));

        var label = transform.Find("Label");
        if (label) label.position = centre + front * depth + Vector3.up * (bounds.max.y + 0.3f);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactRange);
    }
}
