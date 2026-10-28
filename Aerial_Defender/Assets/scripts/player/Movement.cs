using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class Movement : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float sprintSpeedMultiplier = 1.75f;
    [SerializeField] private float jumpForce = 7f;
    [SerializeField] private bool showCharacterCustomizationOnStart;

    private Rigidbody rb;
    private CameraController cameraController;
    private Vector2 moveInput;
    private bool jumpRequested;
    private bool isGrounded;
    private bool isSprinting;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
        cameraController = GetComponentInChildren<CameraController>();

        if (showCharacterCustomizationOnStart)
        {
            gameObject.AddComponent<CharacterCustomization>();
        }
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            moveInput = Vector2.zero;
            isSprinting = false;
            return;
        }

        isSprinting = keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
        moveInput = Vector2.zero;
        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
        {
            moveInput.y += 1f;
        }
        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
        {
            moveInput.y -= 1f;
        }
        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
        {
            moveInput.x += 1f;
        }
        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
        {
            moveInput.x -= 1f;
        }

        moveInput = Vector2.ClampMagnitude(moveInput, 1f);
        if (keyboard.spaceKey.wasPressedThisFrame)
        {
            jumpRequested = true;
        }
    }

    private void FixedUpdate()
    {
        if (moveInput.y > 0f && cameraController != null)
        {
            cameraController.AlignBodyToLookDirection(rb, Time.fixedDeltaTime);
        }

        Vector3 movement = transform.right * moveInput.x + transform.forward * moveInput.y;
        Vector3 velocity = rb.linearVelocity;
        float currentMoveSpeed = moveSpeed * (isSprinting ? sprintSpeedMultiplier : 1f);
        velocity.x = movement.x * currentMoveSpeed;
        velocity.z = movement.z * currentMoveSpeed;

        if (jumpRequested && isGrounded)
        {
            velocity.y = jumpForce;
        }

        rb.linearVelocity = velocity;
        jumpRequested = false;
        isGrounded = false;
    }

    private void OnCollisionStay(Collision collision)
    {
        foreach (ContactPoint contact in collision.contacts)
        {
            if (Vector3.Dot(contact.normal, Vector3.up) > 0.5f)
            {
                isGrounded = true;
                break;
            }
        }
    }
}
