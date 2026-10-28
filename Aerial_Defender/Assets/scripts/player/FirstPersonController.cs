using UnityEngine;

public class CameraController : MonoBehaviour
{
    [Header("Look")]
    [SerializeField] private float mouseSensitivity = 2f;
    [SerializeField] private float verticalLookLimit = 50f;
    [SerializeField] private float horizontalLookLimit = 90f;
    [SerializeField] private float bodyTurnSpeed = 240f;

    [Header("Aim FOV")]
    [SerializeField] private float aimFieldOfView = 30f;
    [SerializeField] private float fovTransitionSpeed = 15f;

    private Camera playerCamera;
    private Quaternion initialLocalRotation;
    private float yaw;
    private float pitch;
    private float defaultFieldOfView;

    void Start()
    {
        playerCamera = GetComponentInChildren<Camera>();
        if (playerCamera == null)
        {
            Debug.LogError("Missing Camera on this object or one of its children.", this);
            enabled = false;
            return;
        }

        initialLocalRotation = transform.localRotation;
        defaultFieldOfView = playerCamera.fieldOfView;
        LockCursor();
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            return;
        }

        if (Cursor.lockState != CursorLockMode.Locked && Input.GetMouseButtonDown(0))
        {
            LockCursor();
        }

        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity;

        yaw = Mathf.Clamp(yaw + mouseX, -horizontalLookLimit, horizontalLookLimit);
        pitch = Mathf.Clamp(pitch - mouseY, -verticalLookLimit, verticalLookLimit);
        transform.localRotation = initialLocalRotation * Quaternion.Euler(pitch, yaw, 0f);

        float targetFieldOfView = Input.GetMouseButton(1)
            ? aimFieldOfView
            : defaultFieldOfView;
        float interpolation = 1f - Mathf.Exp(-fovTransitionSpeed * Time.deltaTime);
        playerCamera.fieldOfView = Mathf.Lerp(
            playerCamera.fieldOfView,
            targetFieldOfView,
            interpolation);
    }

    public void AlignBodyToLookDirection(Rigidbody body, float deltaTime)
    {
        if (playerCamera == null || body == null) return;

        Vector3 lookDirection = Vector3.ProjectOnPlane(playerCamera.transform.forward, Vector3.up).normalized;
        if (lookDirection.sqrMagnitude < 0.001f) return;

        Vector3 bodyForward = Vector3.ProjectOnPlane(body.rotation * Vector3.forward, Vector3.up).normalized;
        float yawToLookDirection = Vector3.SignedAngle(bodyForward, lookDirection, Vector3.up);
        float appliedYaw = Mathf.MoveTowards(0f, yawToLookDirection, bodyTurnSpeed * deltaTime);
        if (Mathf.Approximately(appliedYaw, 0f)) return;

        body.MoveRotation(Quaternion.AngleAxis(appliedYaw, Vector3.up) * body.rotation);
        yaw = Mathf.Clamp(yaw - appliedYaw, -horizontalLookLimit, horizontalLookLimit);
        transform.localRotation = initialLocalRotation * Quaternion.Euler(pitch, yaw, 0f);
    }

    public Vector3 GetLookRelativeMovement(Vector2 input)
    {
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 right = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
        return right * input.x + forward * input.y;
    }

    public void ResetLookDirection()
    {
        yaw = 0f;
        pitch = 0f;
        transform.localRotation = initialLocalRotation;
    }

    void OnApplicationFocus(bool hasFocus)
    {
        if (hasFocus && enabled) LockCursor();
    }

    void OnDisable()
    {
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    private void LockCursor()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }
}
