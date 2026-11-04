// using System.Collections;
// using System.Collections.Generic;
// using UnityEngine;
// using UnityEngine.InputSystem;

// public class OrbitCamera : MonoBehaviour
// {
//     // the orbit target (e.g., the player GameObject)
//     [SerializeField] Transform target;

//     [SerializeField] private Vector3 offset = new Vector3(0, 1.7f, -2.0f);

//     // rotation sensitivity
//     [SerializeField] private float rotSpeed = 0.5f;

//     [SerializeField] private bool lookAtTarget = false;

//     private float rotY;     // horizontal rotation
//     [SerializeField] private InputActionAsset inputActions;
//     private InputAction lookAction;

//     void Start()
//     {
//         // get transform component's yaw
//         rotY = transform.eulerAngles.y;

//         // this assumes that target is always Player, having PlayerInput component
//         PlayerInput playerInput = target.GetComponent<PlayerInput>();
//         //     UnityEngine.InputSystem.PlayerInput playerInput =
//         // // target.GetComponent<UnityEngine.InputSystem.PlayerInput>();
//         // if (playerInput != null)
//         // {
//         //     lookAction = playerInput.actions.FindAction("Look");
//         // }

//         if (inputActions != null)
//         {
//             lookAction = inputActions.FindAction("Player/Look");
//             if (lookAction != null)
//             {
//                 lookAction.Enable();
//             }
//         }

//     }

//     void OnDisable()
//     {
//         if (lookAction != null)
//         {
//             lookAction.Disable();
//         }
//     }
//     void LateUpdate()
//     {
//         if (target == null || lookAction == null) return;
//         Vector2 lookInput = lookAction.ReadValue<Vector2>();

//         // yaw based on horizontal mouse movement
//         rotY += lookInput.x * rotSpeed;

//         // create quaternion based on rotation angle
//         Quaternion rotation = Quaternion.Euler(0, rotY, 0);

//         // set the camera's position based on the rotated offset
//         transform.position = target.position + rotation * offset;

//         if (lookAtTarget)
//             // rotate camera to look at the target
//             transform.LookAt(target);
//         else
//             transform.rotation = Quaternion.Euler(transform.eulerAngles.x, rotY, 0);
//     }
// }
using UnityEngine;
using UnityEngine.InputSystem;

public class OrbitCamera : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Vector3 offset = new Vector3(0, 1.7f, -2.0f);
    [SerializeField] private float rotSpeed = 0.5f;
    [SerializeField] private bool lookAtTarget = false;
    [SerializeField] private InputActionAsset inputActions;

    private InputAction lookAction;
    private float rotY;

    void OnEnable()
    {
        if (inputActions != null)
        {
            lookAction = inputActions.FindAction("Player/Look");
            lookAction?.Enable();
        }
    }

    void Start()
    {
        if (target == null)
        {
            Debug.LogError("OrbitCamera: Target is missing.");
            return;
        }

        rotY = transform.eulerAngles.y;
    }

    void LateUpdate()
    {
        if (target == null || lookAction == null)
            return;

        Vector2 lookInput = lookAction.ReadValue<Vector2>();

        rotY += lookInput.x * rotSpeed;

        Quaternion rotation = Quaternion.Euler(0f, rotY, 0f);

        transform.position = target.position + rotation * offset;

        if (lookAtTarget)
            transform.LookAt(target);
        else
            transform.rotation = rotation;
    }

    void OnDisable()
    {
        lookAction?.Disable();
    }
}