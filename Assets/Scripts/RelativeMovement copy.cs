using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))] // enforces dependency on character controller
public class RelativeMovement : MonoBehaviour
{
    [SerializeField] Transform target;  // camera

    // sensitivities
    [SerializeField] private float moveSpeed = 3.0f;
    [SerializeField] private float runSpeed = 6.0f;
    [SerializeField] private float rotSpeed = 15.0f;
    [SerializeField] private float jumpSpeed = 5.0f;
    [SerializeField] private float gravity = -9.81f;
    [SerializeField] private float terminalVelocity = -20.0f;
    private CharacterController charController;
    private Animator charAnimator;
    private Vector2 moveInput;
    private bool isRunning;
    private float vertSpeed;

    void Start()
    {
        // get character controller component
        charController = GetComponent<CharacterController>();
        charAnimator = GetComponent<Animator>();
    }

    void OnMove(InputValue value)
    {
        moveInput = value.Get<Vector2>();
    }
    void OnSprint(InputValue value)
    {
        isRunning = value.isPressed;
    }
    void OnJump(InputValue value)
    {
        if (value.isPressed && charController.isGrounded)
        {
            vertSpeed = jumpSpeed;
            charAnimator.SetTrigger("Jump");
        }
    }

    void Update()
    {
        // start with zero and add movement components progressively
        Vector3 movement = Vector3.zero;

        // changes based on horizontal and vertical input
        float horInput = moveInput.x;
        float vertInput = moveInput.y;

        // if input is not zero
        if (horInput != 0 || vertInput != 0)
        {
            // x z movement transformed relative to target
            Vector3 right = target.right;
            Vector3 forward = Vector3.Cross(right, Vector3.up);

            // calculate movement
            movement = (right * horInput) + (forward * vertInput);

            // scale by movement speed
            movement *= moveSpeed;
            movement = Vector3.ClampMagnitude(movement, moveSpeed);

            // face movement direction
            //transform.rotation = Quaternion.LookRotation(movement);
            Quaternion direction = Quaternion.LookRotation(movement);
            transform.rotation = Quaternion.Lerp(transform.rotation,
                                    direction, rotSpeed * Time.deltaTime);
        }

        // float animSpeed = Mathf.Clamp01(moveInput.magnitude) *
        //     (isRunning ? 1.0f : 0.5f);

        // charAnimator.SetFloat("Speed", animSpeed);
        float animSpeed = Mathf.Clamp01(moveInput.magnitude) *
    (isRunning ? 1.0f : 0.5f);

        charAnimator.SetFloat("Speed", animSpeed);

        // Update Animator movement parameters
        charAnimator.SetFloat("MoveX", moveInput.x);
        charAnimator.SetFloat("MoveY", moveInput.y);

        if (vertSpeed < terminalVelocity)
        {
            vertSpeed = terminalVelocity;
        }
        vertSpeed += gravity * Time.deltaTime;
        movement *= isRunning ? runSpeed : moveSpeed;
        movement.y = vertSpeed;

        movement *= Time.deltaTime;
        charController.Move(movement);
    }
}
