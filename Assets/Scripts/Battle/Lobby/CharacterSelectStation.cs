using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// The character terminal in the Lobby. Walk up to it and press E to open the character select panel.
public class CharacterSelectStation : MonoBehaviour
{
    [SerializeField] CharacterSelectUI selectUI;
    [SerializeField] Text promptText;                 // "Press E to choose your character"
    [SerializeField] float interactRange = 2.5f;
    [SerializeField] string playerTag = "Player";

    Transform player;

    void Start()
    {
        var p = GameObject.FindGameObjectWithTag(playerTag);
        if (p) player = p.transform;
        if (promptText) promptText.gameObject.SetActive(false);
    }

    void Update()
    {
        if (!player || !selectUI) return;

        Vector3 a = player.position, b = transform.position;
        a.y = b.y = 0;
        bool near = Vector3.Distance(a, b) <= interactRange && !selectUI.IsOpen;
        if (promptText) promptText.gameObject.SetActive(near);

        if (near && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            selectUI.Open();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, interactRange);
    }
}
