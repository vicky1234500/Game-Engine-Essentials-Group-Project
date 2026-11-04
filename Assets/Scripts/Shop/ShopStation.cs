using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class ShopStation : MonoBehaviour
{
    [SerializeField] ShopUI shopUI;
    [SerializeField] Text promptText; // "Press E to open the shop"
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
        if (!player || !shopUI) return;

        Vector3 a = player.position, b = transform.position;
        a.y = b.y = 0;
        bool near = Vector3.Distance(a, b) <= interactRange && !shopUI.IsOpen;
        if (promptText) promptText.gameObject.SetActive(near);

        if (near && Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
            shopUI.Open();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, interactRange);
    }
}