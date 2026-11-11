using UnityEngine;
using UnityEngine.InputSystem;

// Same as the original MouseLook, but reads the mouse with the new Input System
// (the project is set to "Input System Package" only, so Input.GetAxis doesn't work).
// Usual setup: Player = MouseX (turn left/right), Main Camera = MouseY (look up/down).
// Menus (backpack, shop, character select) turn this script off while they're open.
[AddComponentMenu("Control Script/Mouse Look")] // add to the Unity editor's component menu
public class MouseLook : MonoBehaviour
{
    // enum to choose rotation axis in the Unity editor
    public enum RotationAxes
    {
        MouseXAndY = 0,     // yaw and pitch
        MouseX = 1,         // yaw only
        MouseY = 2          // pitch only
    }
    // rotation axis
    public RotationAxes axes = RotationAxes.MouseX;

    // rotation sensitivity
    public float sensitivityHor = 9.0f;
    public float sensitivityVert = 9.0f;

    // min and max pitch angles
    public float minimumVert = -45.0f;
    public float maximumVert = 45.0f;

    // OFF = moving the mouse always looks around
    // ON  = only look around while holding the right mouse button (easier to click HUD buttons)
    public bool lookOnlyWhileRightMouseHeld = false;

    // old Input.GetAxis("Mouse X") was roughly mouse pixels * 0.1, keep the same feel
    const float PixelsToAxis = 0.1f;

    // for the pitch
    private float verticalRot = 0;

    void Start()
    {
        // make the rigid body not change rotation
        Rigidbody body = GetComponent<Rigidbody>();
        if (body != null)
        {
            body.freezeRotation = true;
        }
    }

    // When turned back on (e.g. after a battle), continue from where the camera is now.
    void OnEnable()
    {
        float x = transform.localEulerAngles.x;
        verticalRot = x > 180f ? x - 360f : x;
    }

    void Update()
    {
        if (Mouse.current == null) return;

        // optional: only look while the right mouse button is held
        if (lookOnlyWhileRightMouseHeld && !Mouse.current.rightButton.isPressed) return;

        Vector2 delta = Mouse.current.delta.ReadValue() * PixelsToAxis;

        // yaw only
        if (axes == RotationAxes.MouseX)
        {
            // rotate about y-axis
            transform.Rotate(0, delta.x * sensitivityHor, 0);
        }
        // pitch only
        else if (axes == RotationAxes.MouseY)
        {
            // change in pitch
            verticalRot -= delta.y * sensitivityVert;
            verticalRot = Mathf.Clamp(verticalRot, minimumVert, maximumVert);

            // get current yaw
            float horizontalRot = transform.localEulerAngles.y;

            // set the pitch, yaw remains the same
            transform.localEulerAngles = new Vector3(verticalRot, horizontalRot, 0);
        }
        // yaw and pitch
        else
        {
            // change in pitch
            verticalRot -= delta.y * sensitivityVert;
            verticalRot = Mathf.Clamp(verticalRot, minimumVert, maximumVert);

            // change in yaw
            float horizontalRot = transform.localEulerAngles.y + delta.x * sensitivityHor;

            // set pitch and yaw
            transform.localEulerAngles = new Vector3(verticalRot, horizontalRot, 0);
        }
    }
}