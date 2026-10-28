using UnityEngine;
using UnityEngine.Serialization;

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

    [Header("Crosshair")]
    [SerializeField] private bool showCrosshair = true;
    [SerializeField] private float crosshairLineLength = 18f;
    [SerializeField] private float crosshairGap = 5f;
    [SerializeField] private float crosshairThickness = 2f;

    [Header("Aircraft radar")]
    [SerializeField] private bool showAircraftRadar = true;
    [SerializeField, Min(0.05f)] private float radarRefreshInterval = 0.25f;
    [SerializeField, Min(0f)] private float radarRedDistance = 100f;
    [SerializeField, Min(0.01f)] private float radarGreenDistance = 1000f;
    [SerializeField, FormerlySerializedAs("radarArrowSize"), Min(1f)] private float radarMarkerSize = 16f;

    private Camera playerCamera;
    private readonly FlyingAircraft[] nearestRadarAircraft = new FlyingAircraft[3];
    private readonly float[] nearestRadarDistances =
    {
        float.PositiveInfinity,
        float.PositiveInfinity,
        float.PositiveInfinity
    };
    private Texture2D radarFaceTexture;
    private Texture2D radarSweepTexture;
    private GUIStyle radarHeaderStyle;
    private GUIStyle radarRangeStyle;
    private float nextRadarRefreshTime;
    private Quaternion initialLocalRotation;
    private Vector3 initialCameraLocalPosition;
    private Quaternion initialCameraLocalRotation;
    private float yaw;
    private float pitch;
    private float defaultFieldOfView;
    private float defaultAimFieldOfView;
    private float aimSensitivityMultiplier = 1f;

    void Awake()
    {
        defaultAimFieldOfView = aimFieldOfView;
    }

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
        initialCameraLocalPosition = playerCamera.transform.localPosition;
        initialCameraLocalRotation = playerCamera.transform.localRotation;
        defaultFieldOfView = playerCamera.fieldOfView;
        LockCursor();
    }

    void Update()
    {
        if (showAircraftRadar && Time.unscaledTime >= nextRadarRefreshTime)
        {
            RefreshRadarTargets();
        }

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

        float currentSensitivity = mouseSensitivity
            * (Input.GetMouseButton(1) ? aimSensitivityMultiplier : 1f);
        float mouseX = Input.GetAxis("Mouse X") * currentSensitivity;
        float mouseY = Input.GetAxis("Mouse Y") * currentSensitivity;

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

    private void OnGUI()
    {
        if (Cursor.lockState != CursorLockMode.Locked) return;

        if (showAircraftRadar)
        {
            DrawAircraftRadar();
        }

        if (!showCrosshair) return;

        Color previousColor = GUI.color;
        GUI.color = Color.white;

        float centerX = Screen.width * 0.5f;
        float centerY = Screen.height * 0.5f;
        float halfLength = crosshairLineLength * 0.5f;
        GUI.DrawTexture(new Rect(
            centerX - crosshairThickness * 0.5f,
            centerY - crosshairGap - halfLength,
            crosshairThickness,
            halfLength), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(
            centerX - crosshairThickness * 0.5f,
            centerY + crosshairGap,
            crosshairThickness,
            halfLength), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(
            centerX - crosshairGap - halfLength,
            centerY - crosshairThickness * 0.5f,
            halfLength,
            crosshairThickness), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(
            centerX + crosshairGap,
            centerY - crosshairThickness * 0.5f,
            halfLength,
            crosshairThickness), Texture2D.whiteTexture);

        GUI.color = previousColor;
    }

    private void RefreshRadarTargets()
    {
        nextRadarRefreshTime = Time.unscaledTime + Mathf.Max(0.05f, radarRefreshInterval);
        for (int i = 0; i < nearestRadarAircraft.Length; i++)
        {
            nearestRadarAircraft[i] = null;
            nearestRadarDistances[i] = float.PositiveInfinity;
        }

        FlyingAircraft[] aircraft = FindObjectsOfType<FlyingAircraft>();
        foreach (FlyingAircraft candidate in aircraft)
        {
            if (candidate == null || !candidate.IsVisibleOnRadar) continue;

            float distance = Vector3.Distance(transform.position, candidate.transform.position);
            for (int i = 0; i < nearestRadarAircraft.Length; i++)
            {
                if (distance >= nearestRadarDistances[i]) continue;

                for (int j = nearestRadarAircraft.Length - 1; j > i; j--)
                {
                    nearestRadarAircraft[j] = nearestRadarAircraft[j - 1];
                    nearestRadarDistances[j] = nearestRadarDistances[j - 1];
                }

                nearestRadarAircraft[i] = candidate;
                nearestRadarDistances[i] = distance;
                break;
            }
        }
    }

    private void DrawAircraftRadar()
    {
        EnsureRadarTextures();

        Matrix4x4 previousMatrix = GUI.matrix;
        Color previousColor = GUI.color;
        float radarSize = Mathf.Clamp(Mathf.Min(Screen.width * 0.32f, Screen.height * 0.38f), 150f, 260f);
        float panelWidth = radarSize + 28f;
        float panelHeight = radarSize + 58f;
        float panelX = (Screen.width - panelWidth) * 0.5f;
        const float panelY = 12f;
        Rect panelRect = new Rect(panelX, panelY, panelWidth, panelHeight);
        Rect radarRect = new Rect(
            panelX + (panelWidth - radarSize) * 0.5f,
            panelY + 36f,
            radarSize,
            radarSize);
        Vector2 radarCenter = radarRect.center;

        GUI.color = new Color(0.015f, 0.045f, 0.04f, 0.92f);
        GUI.DrawTexture(panelRect, Texture2D.whiteTexture);
        GUI.color = new Color(0.08f, 1f, 0.48f, 1f);
        GUI.Label(new Rect(panelX + 8f, panelY + 4f, panelWidth - 16f, 24f), "AIR RADAR", GetRadarLabelStyle(true));

        GUI.color = Color.white;
        GUI.DrawTexture(radarRect, radarFaceTexture);

        GUIUtility.RotateAroundPivot(Time.unscaledTime * 90f, radarCenter);
        GUI.color = Color.white;
        GUI.DrawTexture(radarRect, radarSweepTexture);
        GUI.matrix = previousMatrix;

        float range = Mathf.Max(0.01f, radarGreenDistance);
        Vector3 viewForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        Vector3 viewRight = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;

        for (int i = 0; i < nearestRadarAircraft.Length; i++)
        {
            FlyingAircraft aircraft = nearestRadarAircraft[i];
            if (aircraft == null || !aircraft.IsVisibleOnRadar) continue;

            Vector3 direction = aircraft.transform.position - transform.position;
            direction = Vector3.ProjectOnPlane(direction, Vector3.up);
            if (direction.sqrMagnitude < 0.0001f) continue;

            float normalizedX = Vector3.Dot(direction, viewRight) / range;
            float normalizedY = Vector3.Dot(direction, viewForward) / range;
            Vector2 radarOffset = new Vector2(normalizedX, -normalizedY);
            if (radarOffset.magnitude > 0.88f) radarOffset = radarOffset.normalized * 0.88f;
            Vector2 markerCenter = radarCenter + radarOffset * (radarSize * 0.5f);
            float distance = nearestRadarDistances[i];
            float closeness = 1f - Mathf.InverseLerp(radarRedDistance, Mathf.Max(radarRedDistance + 0.01f, radarGreenDistance), distance);
            Color arrowColor = closeness < 0.5f
                ? Color.Lerp(Color.green, new Color(1f, 0.5f, 0f), closeness * 2f)
                : Color.Lerp(new Color(1f, 0.5f, 0f), Color.red, (closeness - 0.5f) * 2f);

            Vector3 aircraftForward = Vector3.ProjectOnPlane(aircraft.transform.forward, Vector3.up).normalized;
            float heading = aircraftForward.sqrMagnitude > 0.0001f
                ? Vector3.SignedAngle(viewForward, aircraftForward, Vector3.up)
                : 0f;
            Rect markerRect = new Rect(
                markerCenter.x - radarMarkerSize * 0.5f,
                markerCenter.y - radarMarkerSize * 0.5f,
                radarMarkerSize,
                radarMarkerSize);
            GUIUtility.RotateAroundPivot(heading, markerCenter);
            GUI.color = arrowColor;
            GUI.Label(markerRect, "▲", GetRadarLabelStyle(false));
            GUI.matrix = previousMatrix;
        }

        GUI.color = new Color(0.15f, 1f, 0.55f, 0.9f);
        GUI.DrawTexture(new Rect(radarCenter.x - 2f, radarCenter.y - 2f, 4f, 4f), Texture2D.whiteTexture);
        GUI.color = new Color(0.45f, 1f, 0.72f, 0.9f);
        GUI.Label(
            new Rect(panelX + 8f, panelY + panelHeight - 22f, panelWidth - 16f, 18f),
            $"RANGE {range:0} m",
            GetRadarLabelStyle(false));

        GUI.color = previousColor;
        GUI.matrix = previousMatrix;
    }

    private void EnsureRadarTextures()
    {
        if (radarFaceTexture == null)
        {
            radarFaceTexture = CreateRadarTexture(false);
        }

        if (radarSweepTexture == null)
        {
            radarSweepTexture = CreateRadarTexture(true);
        }
    }

    private static Texture2D CreateRadarTexture(bool sweep)
    {
        const int textureSize = 128;
        Texture2D texture = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            hideFlags = HideFlags.HideAndDontSave
        };
        Color[] pixels = new Color[textureSize * textureSize];

        for (int y = 0; y < textureSize; y++)
        {
            for (int x = 0; x < textureSize; x++)
            {
                float nx = (x + 0.5f - textureSize * 0.5f) / (textureSize * 0.5f);
                float ny = (y + 0.5f - textureSize * 0.5f) / (textureSize * 0.5f);
                float radius = Mathf.Sqrt(nx * nx + ny * ny);
                Color pixel = Color.clear;

                if (radius <= 1f)
                {
                    if (sweep)
                    {
                        float angle = Mathf.Abs(Mathf.Atan2(nx, ny) * Mathf.Rad2Deg);
                        if (angle < 14f)
                        {
                            float alpha = (1f - angle / 14f) * radius * 0.3f;
                            pixel = new Color(0.1f, 1f, 0.45f, alpha);
                        }
                    }
                    else
                    {
                        pixel = new Color(0.015f, 0.09f, 0.065f, 0.94f);
                        bool ring = Mathf.Abs(radius - 1f) < 0.018f
                            || Mathf.Abs(radius - 0.66f) < 0.012f
                            || Mathf.Abs(radius - 0.33f) < 0.012f;
                        bool axis = Mathf.Abs(nx) < 0.008f || Mathf.Abs(ny) < 0.008f;
                        if (ring || axis) pixel = new Color(0.08f, 0.8f, 0.4f, ring ? 0.7f : 0.45f);
                    }
                }

                pixels[y * textureSize + x] = pixel;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    private GUIStyle GetRadarLabelStyle(bool header)
    {
        if (header)
        {
            if (radarHeaderStyle == null)
            {
                radarHeaderStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold,
                    fontSize = 14
                };
            }

            return radarHeaderStyle;
        }

        if (radarRangeStyle == null)
        {
            radarRangeStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 11
            };
        }

        return radarRangeStyle;
    }

    public void SetAimFieldOfView(float fieldOfView)
    {
        aimFieldOfView = Mathf.Clamp(fieldOfView, 1f, 179f);
    }

    public void RestoreDefaultAimFieldOfView()
    {
        aimFieldOfView = defaultAimFieldOfView;
        aimSensitivityMultiplier = 1f;
    }

    public void SetAimSensitivityMultiplier(float multiplier)
    {
        aimSensitivityMultiplier = Mathf.Max(0f, multiplier);
    }

    public Ray GetAimRay()
    {
        if (playerCamera != null)
        {
            return new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        }

        return new Ray(transform.position, transform.forward);
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
        if (playerCamera != null)
        {
            playerCamera.transform.localPosition = initialCameraLocalPosition;
            playerCamera.transform.localRotation = initialCameraLocalRotation;
            playerCamera.fieldOfView = defaultFieldOfView;
        }
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
