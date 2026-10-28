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

    [Header("Weapon slots")]
    [SerializeField] private GameObject unarmedObject;
    [SerializeField] private GameObject sniperWeapon;
    [SerializeField] private GameObject bazookaWeapon;

    private Rigidbody rb;
    private CameraController cameraController;
    private Vector3 spawnPosition;
    private Quaternion spawnRotation;
    private Vector2 moveInput;
    private bool jumpRequested;
    private bool isGrounded;
    private bool isSprinting;
    private int selectedWeaponSlot;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        spawnPosition = rb.position;
        spawnRotation = rb.rotation;
        rb.freezeRotation = true;
        cameraController = GetComponentInChildren<CameraController>();
        if (unarmedObject == null)
        {
            unarmedObject = FindChildGameObject("Unarmed", false);
        }
        if (sniperWeapon == null)
        {
            sniperWeapon = FindChildGameObject("Sniper", false);
        }
        if (bazookaWeapon == null)
        {
            bazookaWeapon = FindChildGameObject("Bazooka", false);
        }
        SelectWeaponSlot(0);

        if (showCharacterCustomizationOnStart)
        {
            gameObject.AddComponent<CharacterCustomization>();
        }
    }

    private void Update()
    {
        HandleWeaponSelection();

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

    private void HandleWeaponSelection()
    {
        Mouse mouse = Mouse.current;
        if (mouse == null) return;

        float scroll = mouse.scroll.ReadValue().y;
        if (scroll > 0f)
        {
            SelectWeaponSlot((selectedWeaponSlot + 1) % 3);
        }
        else if (scroll < 0f)
        {
            SelectWeaponSlot((selectedWeaponSlot + 2) % 3);
        }
    }

    private void SelectWeaponSlot(int slot)
    {
        selectedWeaponSlot = slot;

        if (unarmedObject != null)
        {
            unarmedObject.SetActive(slot == 0);
        }

        if (sniperWeapon != null)
        {
            sniperWeapon.SetActive(slot == 1);
        }

        if (bazookaWeapon != null)
        {
            bazookaWeapon.SetActive(slot == 2);
        }
    }

    private GameObject FindChildGameObject(string objectName, bool logIfMissing = true)
    {
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child != transform && child.name == objectName)
            {
                return child.gameObject;
            }
        }

        if (logIfMissing)
        {
            Debug.LogWarning("Weapon slot could not find a child named '" + objectName + "'. Assign it in the Movement inspector.", this);
        }

        return null;
    }

    private void FixedUpdate()
    {
        if (isGrounded && moveInput.sqrMagnitude > 0.001f && cameraController != null)
        {
            cameraController.AlignBodyToLookDirection(rb, Time.fixedDeltaTime);
        }

        Vector3 velocity = rb.linearVelocity;
        if (isGrounded)
        {
            Vector3 movement = cameraController != null
                ? cameraController.GetLookRelativeMovement(moveInput)
                : transform.right * moveInput.x + transform.forward * moveInput.y;
            movement = Vector3.ClampMagnitude(movement, 1f);
            float currentMoveSpeed = moveSpeed * (isSprinting ? sprintSpeedMultiplier : 1f);
            velocity.x = movement.x * currentMoveSpeed;
            velocity.z = movement.z * currentMoveSpeed;
        }

        if (jumpRequested && isGrounded)
        {
            velocity.y = jumpForce;
        }

        rb.linearVelocity = velocity;
        jumpRequested = false;
        isGrounded = false;
    }

    public void RespawnAtStart()
    {
        rb.position = spawnPosition;
        rb.rotation = spawnRotation;
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;
        moveInput = Vector2.zero;
        jumpRequested = false;
        isGrounded = false;

        if (cameraController != null)
        {
            cameraController.ResetLookDirection();
        }
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
