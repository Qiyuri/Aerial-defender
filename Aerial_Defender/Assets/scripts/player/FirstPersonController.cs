using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
public class FirstPersonController : MonoBehaviour
{
    [Header("Movement Settings")]
    public float moveSpeed = 5f;
    public float jumpForce = 10f;

    [Header("Camera & FOV Settings")]
    public int minFov = 60;
    public int maxFov = 90;
    public float fovSensitivity = 1f;

    [Header("References")]
    public Camera playerCamera;

    private Rigidbody rb;
    private float yaw;
    private float pitch;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            Debug.LogError("Missing Rigidbody! Please add a Rigidbody component to this object.");
            return;
        }

        rb.freezeRotation = true;

        if (playerCamera == null)
        {
            playerCamera = GetComponentInChildren<Camera>();
        }

        if (playerCamera != null)
        {
            playerCamera.fieldOfView = minFov;
        }
    }

    private void Update()
    {
        if (rb == null)
        {
            return;
        }

        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");

        yaw += mouseX;
        pitch -= mouseY;
        pitch = Mathf.Clamp(pitch, -89f, 89f);

        transform.rotation = Quaternion.Euler(0f, yaw, 0f);

        if (playerCamera != null)
        {
            playerCamera.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.01f && playerCamera != null)
        {
            float targetFov = playerCamera.fieldOfView - (scroll * fovSensitivity);
            playerCamera.fieldOfView = Mathf.Clamp(targetFov, minFov, maxFov);
        }

        float x = Input.GetAxisRaw("Horizontal");
        float z = Input.GetAxisRaw("Vertical");

        Vector3 move = transform.right * x + transform.forward * z;
        if (move.sqrMagnitude > 1f)
        {
            move.Normalize();
        }

        rb.linearVelocity = new Vector3(move.x * moveSpeed, rb.linearVelocity.y, move.z * moveSpeed);

        if (Input.GetButtonDown("Jump") && Mathf.Abs(rb.linearVelocity.y) < 0.01f)
        {
            rb.linearVelocity = new Vector3(rb.linearVelocity.x, jumpForce, rb.linearVelocity.z);
        }
    }
}
