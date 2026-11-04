using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

// Put this on an object that is ALWAYS active (Canvas or Player), never on InventoryMenu itself.
public class InventoryToggle : MonoBehaviour
{
    [SerializeField] GameObject inventoryMenu;        // Canvas > InventoryMenu
    [SerializeField] GameObject hudWhenClosed;         // Canvas > InGame ("Press 0 to open inventory")
    [SerializeField] Behaviour[] disableWhileOpen;     // optional: PlayerMovement / mouse-look scripts
    [SerializeField] bool useKeyboard = true;          // key 0 / numpad 0 toggles

    [Header("Cursor")]
    [SerializeField] bool lockCursorWhenClosed = false; // OFF = cursor always visible (needed for clickable buttons)

    public bool IsOpen { get; private set; }

    void Start()
    {
        if (!inventoryMenu) Debug.LogError("InventoryToggle: Inventory Menu field is empty.");
        SetOpen(false);
    }

    void Update()
    {
        if (!useKeyboard) return;
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.digit0Key.wasPressedThisFrame || kb.numpad0Key.wasPressedThisFrame)
            ToggleInventory();
    }

    // ---------- Button-friendly functions (no parameters, so they show in a Button's On Click list) ----------

    public void OpenInventory()
    {
        //Debug.Log("InventoryToggle: OpenInventory called");
        SetOpen(true);
    }

    public void CloseInventory()
    {
        //Debug.Log("InventoryToggle: CloseInventory called");
        SetOpen(false);
    }

    public void ToggleInventory() => SetOpen(!IsOpen);

    // ---------- Core ----------

    public void SetOpen(bool open)
    {
        IsOpen = open;
        if (!open && inventoryMenu)
        {
            var grid = inventoryMenu.GetComponent<InventoryGrid>();
            if (grid) grid.Deselect();
        }
        if (inventoryMenu) inventoryMenu.SetActive(open);
        if (hudWhenClosed) hudWhenClosed.SetActive(!open);

        if (lockCursorWhenClosed)
        {
            Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = open;
        }
        else
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        foreach (var b in disableWhileOpen)
            if (b) b.enabled = !open;

        // Clear button focus, so pressing Space/Enter later doesn't click the button again.
        if (EventSystem.current) EventSystem.current.SetSelectedGameObject(null);
    }
}