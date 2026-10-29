using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class CharacterCustomization : MonoBehaviour
{
    private sealed class AppearanceSnapshot
    {
        public readonly float[] hues = new float[PartNames.Length];
        public readonly float[] saturations = new float[PartNames.Length];
        public readonly float[] values = new float[PartNames.Length];
        public readonly bool[] accessoryEnabled = new bool[PartNames.Length];
    }

    private sealed class Part
    {
        public string label;
        public string[] objectNames;
        public bool isAccessory;
        public Transform accessory;
        public readonly List<Transform> objects = new List<Transform>();
        public readonly List<Material> materials = new List<Material>();
        public float hue;
        public float saturation;
        public float value = 1f;
    }

    private static readonly string[] PartNames = { "Shirt", "Arms", "Head", "Hat", "Beard", "Eye White", "Pupils" };
    private static readonly string[][] ObjectNames =
    {
        new[] { "Capsule" },
        new[] { "arms", "armL", "armR", "armlL" },
        new[] { "Sphere" },
        new[] { "Cone" },
        new[] { "beard" },
        new[] { "Sphere.001", "Sphere.002" },
        new[] { "Sphere.003", "Sphere.004" }
    };
    private static readonly AppearanceSnapshot[] SavedAppearances = new AppearanceSnapshot[2];

    private const float ReferenceWidth = 1440f;
    private const float ReferenceHeight = 900f;
    private const float ControllerNavigationThreshold = 0.55f;
    private const float ControllerNavigationRepeatDelay = 0.3f;
    private const float ControllerNavigationRepeatRate = 0.12f;
    private const float ControllerColorAdjustSpeed = 0.65f;

    private Movement movement;
    private GameManager gameManager;
    private CameraController cameraController;
    private Camera playerCamera;
    private AudioListener playerAudioListener;
    private Rigidbody body;
    private Camera previewCamera;
    private Part[] parts;
    private int currentPart;
    private int controllerFocusIndex;
    private bool isEditingColor;
    private float nextControllerNavigationTime;
    private bool controllerVerticalNavigationActive;
    private bool isCustomizing;
    private bool movementWasEnabled;
    private bool controllerWasEnabled;
    private bool cameraWasEnabled;
    private bool listenerWasEnabled;
    private bool gravityWasEnabled;
    private float previousTimeScale;
    private Vector3 customizationStartPosition;
    private Quaternion customizationStartRotation;
    private float hue;
    private float saturation;
    private float brightness;
    private Vector3 defaultPreviewPosition;
    private Quaternion defaultPreviewRotation;
    private GUIStyle windowStyle;
    private GUIStyle titleStyle;
    private GUIStyle labelStyle;
    private GUIStyle valueStyle;
    private GUIStyle buttonStyle;

    public bool IsCustomizing => isCustomizing;

    private void Awake()
    {
        movement = GetComponent<Movement>();
        gameManager = movement != null && movement.PlayerIndex > 0 ? null : FindObjectOfType<GameManager>();
        cameraController = GetComponentInChildren<CameraController>(true);
        playerCamera = GetComponentInChildren<Camera>(true);
        body = GetComponent<Rigidbody>();

        if (movement == null || cameraController == null || playerCamera == null)
        {
            Debug.LogError("Character customization requires Movement and a child CameraController with a Camera.", this);
            enabled = false;
            return;
        }

        playerAudioListener = playerCamera.GetComponent<AudioListener>();
        movementWasEnabled = movement.enabled;
        controllerWasEnabled = cameraController.enabled;
        cameraWasEnabled = playerCamera.enabled;
        listenerWasEnabled = playerAudioListener != null && playerAudioListener.enabled;
        gravityWasEnabled = body != null && body.useGravity;
        previousTimeScale = Time.timeScale;
        customizationStartPosition = body != null ? body.position : transform.position;
        customizationStartRotation = body != null ? body.rotation : transform.rotation;

        movement.enabled = false;
        cameraController.enabled = false;
        playerCamera.enabled = false;
        if (playerAudioListener != null) playerAudioListener.enabled = false;
        if (body != null)
        {
            body.useGravity = false;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }

        isCustomizing = true;
        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        Physics.SyncTransforms();
        InitializeParts();
        CreatePreviewCamera();

        Vector3 previewPosition = LocalMultiplayerManager.HasCustomizationPreviewPosition
            ? LocalMultiplayerManager.CustomizationPreviewPosition
            : customizationStartPosition + Vector3.up * 100f;
        transform.SetPositionAndRotation(previewPosition, customizationStartRotation);
        if (body != null)
        {
            body.position = previewPosition;
            body.rotation = customizationStartRotation;
        }

        Vector3 previewOffset = previewPosition - customizationStartPosition;
        defaultPreviewPosition += previewOffset;
        previewCamera.transform.position += previewOffset;
        Physics.SyncTransforms();
    }

    private void Update()
    {
        if (!isCustomizing) return;

        Gamepad gamepad = movement != null ? movement.InputGamepad : Gamepad.current;
        if (gamepad == null) return;

        Vector2 navigation = gamepad.dpad.ReadValue();
        if (navigation.sqrMagnitude < 0.01f)
        {
            navigation = gamepad.leftStick.ReadValue();
        }

        float horizontal = Mathf.Abs(navigation.x) >= ControllerNavigationThreshold ? Mathf.Sign(navigation.x) : 0f;
        float vertical = Mathf.Abs(navigation.y) >= ControllerNavigationThreshold ? Mathf.Sign(navigation.y) : 0f;
        if (isEditingColor)
        {
            if (horizontal != 0f)
            {
                AdjustControllerSelection(horizontal * ControllerColorAdjustSpeed * Time.unscaledDeltaTime);
            }

            controllerVerticalNavigationActive = false;
            nextControllerNavigationTime = 0f;
        }
        else
        {
            if (vertical != 0f)
            {
                if (Time.unscaledTime >= nextControllerNavigationTime)
                {
                    MoveControllerFocus(vertical > 0f ? -1 : 1);
                    nextControllerNavigationTime = Time.unscaledTime
                        + (controllerVerticalNavigationActive ? ControllerNavigationRepeatRate : ControllerNavigationRepeatDelay);
                }

                controllerVerticalNavigationActive = true;
            }
            else
            {
                controllerVerticalNavigationActive = false;
                nextControllerNavigationTime = 0f;
            }
        }

        if (gamepad.buttonSouth.wasPressedThisFrame)
        {
            ActivateControllerSelection();
        }
        else if (gamepad.buttonEast.wasPressedThisFrame && currentPart > 0)
        {
            ChangePart(currentPart - 1);
        }
    }

    private void MoveControllerFocus(int direction)
    {
        int focusCount = GetControllerFocusCount();
        controllerFocusIndex = (controllerFocusIndex + direction + focusCount) % focusCount;
    }

    private void AdjustControllerSelection(float adjustment)
    {
        Part part = parts[currentPart];
        int hueIndex = GetHueControlIndex();
        if (controllerFocusIndex == hueIndex)
        {
            hue = Mathf.Clamp01(hue + adjustment);
        }
        else if (controllerFocusIndex == hueIndex + 1)
        {
            saturation = Mathf.Clamp01(saturation + adjustment);
        }
        else if (controllerFocusIndex == hueIndex + 2)
        {
            brightness = Mathf.Clamp01(brightness + adjustment);
        }
        else
        {
            return;
        }

        part.hue = hue;
        part.saturation = saturation;
        part.value = brightness;
        ApplyColor(part);
    }

    private void ActivateControllerSelection()
    {
        Part part = parts[currentPart];
        int hueIndex = GetHueControlIndex();
        int backIndex = hueIndex + 3;
        int confirmIndex = backIndex + (currentPart > 0 ? 1 : 0);

        if (IsColorControlSelected())
        {
            isEditingColor = !isEditingColor;
        }
        else if (part.accessory != null && controllerFocusIndex == 0)
        {
            part.accessory.gameObject.SetActive(!part.accessory.gameObject.activeSelf);
        }
        else if (currentPart > 0 && controllerFocusIndex == backIndex)
        {
            ChangePart(currentPart - 1);
        }
        else if (controllerFocusIndex == confirmIndex)
        {
            if (currentPart == parts.Length - 1)
            {
                FinishCustomization();
            }
            else
            {
                ChangePart(currentPart + 1);
            }
        }
    }

    private bool IsColorControlSelected()
    {
        int hueIndex = GetHueControlIndex();
        return controllerFocusIndex >= hueIndex && controllerFocusIndex <= hueIndex + 2;
    }

    private int GetHueControlIndex()
    {
        return parts[currentPart].accessory != null ? 1 : 0;
    }

    private int GetControllerFocusCount()
    {
        return GetHueControlIndex() + 3 + (currentPart > 0 ? 1 : 0) + 1;
    }

    private string GetControllerFocusPrefix(int index)
    {
        if (controllerFocusIndex != index) return string.Empty;
        return isEditingColor && IsColorControlSelected() ? "[EDITING] > " : "> ";
    }

    private void InitializeParts()
    {
        parts = new Part[PartNames.Length];

        for (int i = 0; i < parts.Length; i++)
        {
            Part part = new Part
            {
                label = PartNames[i],
                objectNames = ObjectNames[i],
                isAccessory = PartNames[i] == "Beard" || PartNames[i] == "Hat"
            };
            parts[i] = part;

            foreach (string objectName in part.objectNames)
            {
                Transform partTransform = FindDescendant(transform, objectName);
                if (partTransform == null) continue;

                part.objects.Add(partTransform);

                if (part.isAccessory)
                {
                    part.accessory = partTransform;
                }
            }

        }

        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        foreach (Renderer partRenderer in renderers)
        {
            Material[] sourceMaterials = partRenderer.sharedMaterials;
            Material[] instanceMaterials = partRenderer.materials;
            for (int materialIndex = 0; materialIndex < sourceMaterials.Length; materialIndex++)
            {
                Material sourceMaterial = sourceMaterials[materialIndex];
                if (sourceMaterial == null || materialIndex >= instanceMaterials.Length) continue;

                Part part = FindPartForMaterial(sourceMaterial.name);
                if (part != null && instanceMaterials[materialIndex] != null)
                {
                    part.materials.Add(instanceMaterials[materialIndex]);
                }
            }
        }

        foreach (Part part in parts)
        {
            if (part.materials.Count == 0)
            {
                Debug.LogWarning("Character customization could not find a renderer using the expected material for '" + part.label + "'.", this);
            }

            InitializeColor(part);
        }

        hue = parts[0].hue;
        saturation = parts[0].saturation;
        brightness = parts[0].value;
    }

    private Part FindPartForMaterial(string materialName)
    {
        string normalizedName = materialName.ToLowerInvariant();
        if (normalizedName.Contains("pupil")) return FindPart("Pupils");
        if (normalizedName.Contains("eye")) return FindPart("Eye White");
        if (normalizedName.Contains("beard")) return FindPart("Beard");
        if (normalizedName.Contains("hat")) return FindPart("Hat");
        if (normalizedName.Contains("head")) return FindPart("Head");
        if (normalizedName.Contains("shirt")) return FindPart("Shirt");
        if (normalizedName.Contains("arm")) return FindPart("Arms");
        return null;
    }

    private Part FindPart(string label)
    {
        foreach (Part part in parts)
        {
            if (part.label == label) return part;
        }

        return null;
    }

    private static Transform FindDescendant(Transform parent, string objectName)
    {
        foreach (Transform child in parent.GetComponentsInChildren<Transform>(true))
        {
            if (child != parent && child.name == objectName) return child;
        }

        return null;
    }

    private static void InitializeColor(Part part)
    {
        Color initialColor = Color.white;
        foreach (Material material in part.materials)
        {
            if (material.HasProperty("_BaseColor"))
            {
                initialColor = material.GetColor("_BaseColor");
                break;
            }

            if (material.HasProperty("_Color"))
            {
                initialColor = material.GetColor("_Color");
                break;
            }
        }

        Color.RGBToHSV(initialColor, out part.hue, out part.saturation, out part.value);
        if (part.saturation < 0.05f) part.saturation = 1f;
        if (part.value <= 0f) part.value = 1f;
    }

    private void CreatePreviewCamera()
    {
        Bounds characterBounds = new Bounds(transform.position, Vector3.one);
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        foreach (Renderer characterRenderer in renderers)
        {
            if (!characterRenderer.enabled) continue;
            if (!hasBounds)
            {
                characterBounds = characterRenderer.bounds;
                hasBounds = true;
            }
            else
            {
                characterBounds.Encapsulate(characterRenderer.bounds);
            }
        }

        float characterSize = Mathf.Max(characterBounds.size.x, characterBounds.size.y, characterBounds.size.z);
        Vector3 lookAt = characterBounds.center;

        GameObject cameraObject = new GameObject("Character Preview Camera");
        cameraObject.tag = "MainCamera";
        previewCamera = cameraObject.AddComponent<Camera>();
        previewCamera.fieldOfView = 35f;
        previewCamera.rect = new Rect(0f, 0f, 1f, 1f);
        previewCamera.cullingMask = playerCamera.cullingMask;
        previewCamera.nearClipPlane = 0.05f;
        float halfFieldOfView = previewCamera.fieldOfView * 0.5f * Mathf.Deg2Rad;
        float cameraDistance = Mathf.Max(3f, characterBounds.extents.magnitude / Mathf.Sin(halfFieldOfView) * 1.1f);
        Vector3 cameraPosition = lookAt + transform.forward * cameraDistance + Vector3.up * (characterSize * 0.12f);
        previewCamera.transform.position = cameraPosition;
        previewCamera.transform.LookAt(lookAt);
        defaultPreviewPosition = previewCamera.transform.position;
        defaultPreviewRotation = previewCamera.transform.rotation;
        cameraObject.AddComponent<AudioListener>();
        UpdatePreviewCameraForPart();
    }

    private void OnGUI()
    {
        if (!isCustomizing) return;

        float scale = Mathf.Min(Screen.width / ReferenceWidth, Screen.height / ReferenceHeight);
        float width = Mathf.Min(500f * scale, Screen.width - 32f * scale);
        float height = Mathf.Min(740f * scale, Screen.height - 32f * scale);
        Rect panel = new Rect(
            Screen.width - width - 16f * scale,
            (Screen.height - height) * 0.5f,
            width,
            height);

        if (windowStyle == null)
        {
            windowStyle = new GUIStyle(GUI.skin.window);
            windowStyle.normal.textColor = Color.white;

            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                normal = { textColor = Color.white }
            };

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                normal = { textColor = Color.white }
            };

            valueStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleRight,
                normal = { textColor = new Color(0.8f, 0.85f, 1f) }
            };

            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontStyle = FontStyle.Bold
            };
        }

        int bodyFontSize = Mathf.Max(18, Mathf.RoundToInt(22f * scale));
        windowStyle.fontSize = Mathf.Max(20, Mathf.RoundToInt(24f * scale));
        windowStyle.padding = new RectOffset(
            Mathf.RoundToInt(24f * scale),
            Mathf.RoundToInt(24f * scale),
            Mathf.RoundToInt(20f * scale),
            Mathf.RoundToInt(18f * scale));
        titleStyle.fontSize = Mathf.Max(26, Mathf.RoundToInt(32f * scale));
        labelStyle.fontSize = bodyFontSize;
        valueStyle.fontSize = bodyFontSize;
        buttonStyle.fontSize = Mathf.Max(20, Mathf.RoundToInt(24f * scale));

        GUILayout.BeginArea(panel, "Character customization", windowStyle);
        Part part = parts[currentPart];

        GUILayout.Space(10f * scale);
        GUILayout.Label("Step " + (currentPart + 1) + " of " + parts.Length, labelStyle, GUILayout.Height(38f * scale));
        GUILayout.Label(parts[currentPart].label, titleStyle, GUILayout.Height(54f * scale));
        GUILayout.Label(
            "D-pad up/down: choose  |  A: select/edit  |  left/right: adjust  |  B: back",
            labelStyle,
            GUILayout.Height(32f * scale));
        GUILayout.Space(10f * scale);

        if (part.accessory != null)
        {
            string accessoryButtonText = GetControllerFocusPrefix(0)
                + (part.accessory.gameObject.activeSelf ? "Wearing " : "Not wearing ")
                + part.label.ToLowerInvariant() + " (A / click to toggle)";
            Color previousBackgroundColor = GUI.backgroundColor;
            if (controllerFocusIndex == 0) GUI.backgroundColor = new Color(0.7f, 0.85f, 1f);
            if (GUILayout.Button(accessoryButtonText, buttonStyle, GUILayout.Height(70f * scale)))
            {
                part.accessory.gameObject.SetActive(!part.accessory.gameObject.activeSelf);
            }
            GUI.backgroundColor = previousBackgroundColor;
        }

        GUILayout.Space(24f * scale);
        int hueIndex = GetHueControlIndex();
        GUILayout.Label(
            GetControllerFocusPrefix(hueIndex) + "Color" + GetColorControlHint(hueIndex),
            labelStyle,
            GUILayout.Height(32f * scale));
        float updatedHue = GUILayout.HorizontalSlider(hue, 0f, 1f, GUILayout.Height(40f * scale));

        GUILayout.BeginHorizontal();
        GUILayout.Label(
            GetControllerFocusPrefix(hueIndex + 1) + "Saturation" + GetColorControlHint(hueIndex + 1),
            labelStyle,
            GUILayout.Height(36f * scale));
        GUILayout.Label(Mathf.RoundToInt(saturation * 100f) + "%", valueStyle, GUILayout.Height(36f * scale));
        GUILayout.EndHorizontal();
        float updatedSaturation = GUILayout.HorizontalSlider(saturation, 0f, 1f, GUILayout.Height(40f * scale));

        GUILayout.BeginHorizontal();
        GUILayout.Label(
            GetControllerFocusPrefix(hueIndex + 2) + "Brightness" + GetColorControlHint(hueIndex + 2),
            labelStyle,
            GUILayout.Height(36f * scale));
        GUILayout.Label(Mathf.RoundToInt(brightness * 100f) + "%", valueStyle, GUILayout.Height(36f * scale));
        GUILayout.EndHorizontal();
        float updatedBrightness = GUILayout.HorizontalSlider(brightness, 0f, 1f, GUILayout.Height(40f * scale));
        if (!Mathf.Approximately(updatedHue, hue)
            || !Mathf.Approximately(updatedSaturation, saturation)
            || !Mathf.Approximately(updatedBrightness, brightness))
        {
            hue = updatedHue;
            saturation = updatedSaturation;
            brightness = updatedBrightness;
            part.hue = hue;
            part.saturation = saturation;
            part.value = brightness;
            ApplyColor(part);
        }

        GUI.color = Color.HSVToRGB(hue, saturation, brightness);
        GUILayout.Box(" ", GUILayout.Height(38f * scale), GUILayout.ExpandWidth(true));
        GUI.color = Color.white;

        if (part.materials.Count == 0)
        {
            GUILayout.Label("No materials found for this part. Check the character hierarchy.", labelStyle);
        }

        GUILayout.FlexibleSpace();
        GUILayout.BeginHorizontal();
        bool wasGuiEnabled = GUI.enabled;
        GUI.enabled = wasGuiEnabled && currentPart > 0;
        int backIndex = hueIndex + 3;
        Color previousButtonColor = GUI.backgroundColor;
        if (currentPart > 0 && controllerFocusIndex == backIndex)
        {
            GUI.backgroundColor = new Color(0.7f, 0.85f, 1f);
        }
        string backLabel = currentPart > 0 && controllerFocusIndex == backIndex ? "> BACK (B)" : "BACK";
        if (GUILayout.Button(backLabel, buttonStyle, GUILayout.Height(56f * scale)))
        {
            ChangePart(currentPart - 1);
        }
        GUI.enabled = wasGuiEnabled;

        bool isFinalPart = currentPart == parts.Length - 1;
        bool isFirstPlayerInMultiplayer = movement != null
            && movement.PlayerIndex == 0
            && LocalMultiplayerManager.SelectedPlayerCount > 1;
        string confirmLabel = isFinalPart
            ? isFirstPlayerInMultiplayer ? "FINISH PLAYER 1 - NEXT PLAYER" : "FINISH - START GAME"
            : "NEXT PART: " + parts[currentPart + 1].label.ToUpperInvariant();
        int confirmIndex = backIndex + (currentPart > 0 ? 1 : 0);
        bool confirmSelected = controllerFocusIndex == confirmIndex;
        if (confirmSelected) GUI.backgroundColor = new Color(0.7f, 0.85f, 1f);
        if (confirmSelected) confirmLabel = "> " + confirmLabel + " (A)";
        if (GUILayout.Button(confirmLabel, buttonStyle, GUILayout.Height(56f * scale)))
        {
            if (currentPart == parts.Length - 1)
            {
                FinishCustomization();
            }
            else
            {
                ChangePart(currentPart + 1);
            }
        }
        GUI.backgroundColor = previousButtonColor;
        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }

    private void ChangePart(int nextPart)
    {
        currentPart = nextPart;
        controllerFocusIndex = 0;
        isEditingColor = false;
        hue = parts[currentPart].hue;
        saturation = parts[currentPart].saturation;
        brightness = parts[currentPart].value;
        UpdatePreviewCameraForPart();
    }

    private string GetColorControlHint(int controlIndex)
    {
        if (controllerFocusIndex != controlIndex) return string.Empty;
        return isEditingColor ? "  -  A: DONE, left/right: adjust" : "  -  Press A to edit";
    }

    private void UpdatePreviewCameraForPart()
    {
        if (previewCamera == null || parts == null || currentPart < 0 || currentPart >= parts.Length) return;

        Part part = parts[currentPart];
        if (part.label != "Eye White" && part.label != "Pupils")
        {
            previewCamera.transform.SetPositionAndRotation(defaultPreviewPosition, defaultPreviewRotation);
            return;
        }

        Bounds eyeBounds = new Bounds(transform.position, Vector3.zero);
        bool hasBounds = false;
        foreach (Transform eyeObject in part.objects)
        {
            foreach (Renderer eyeRenderer in eyeObject.GetComponentsInChildren<Renderer>(true))
            {
                if (!eyeRenderer.enabled) continue;
                if (!hasBounds)
                {
                    eyeBounds = eyeRenderer.bounds;
                    hasBounds = true;
                }
                else
                {
                    eyeBounds.Encapsulate(eyeRenderer.bounds);
                }
            }
        }

        if (!hasBounds)
        {
            previewCamera.transform.SetPositionAndRotation(defaultPreviewPosition, defaultPreviewRotation);
            return;
        }

        Vector3 eyeLookAt = eyeBounds.center;
        Vector3 directionFromFace = (defaultPreviewPosition - eyeLookAt).normalized;
        float closeDistance = Mathf.Max(1.5f, eyeBounds.size.magnitude * 3f);
        previewCamera.nearClipPlane = 0.05f;
        previewCamera.transform.position = eyeLookAt + directionFromFace * closeDistance;
        previewCamera.transform.LookAt(eyeLookAt);
    }

    private static void ApplyColor(Part part)
    {
        Color color = Color.HSVToRGB(part.hue, part.saturation, part.value);
        foreach (Material material in part.materials)
        {
            if (material.HasProperty("_BaseColor"))
            {
                Color originalColor = material.GetColor("_BaseColor");
                color.a = originalColor.a;
                material.SetColor("_BaseColor", color);
            }
            else if (material.HasProperty("_Color"))
            {
                Color originalColor = material.GetColor("_Color");
                color.a = originalColor.a;
                material.SetColor("_Color", color);
            }
        }
    }

    private void UpdateGameManagerMaterials()
    {
        if (gameManager == null || (movement != null && movement.PlayerIndex > 0)) return;

        for (int i = 0; i < parts.Length; i++)
        {
            Part part = parts[i];
            if (part.materials.Count == 0) continue;

            Material primaryMaterial = part.materials[0];
            if (primaryMaterial == null) continue;

            gameManager.SetPartMaterial(part.label, primaryMaterial);
        }
    }

    private void FinishCustomization()
    {
        SaveAppearance();
        UpdateGameManagerMaterials();
        if (gameManager != null && LocalMultiplayerManager.SelectedPlayerCount < 2)
        {
            gameManager.DisableCustomizationLight();
        }

        isCustomizing = false;
        if (previewCamera != null)
        {
            previewCamera.enabled = false;
            AudioListener previewAudioListener = previewCamera.GetComponent<AudioListener>();
            if (previewAudioListener != null) previewAudioListener.enabled = false;
            Destroy(previewCamera.gameObject);
        }

        RestoreGameplayState();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void SaveAppearance()
    {
        if (movement == null || movement.PlayerIndex < 0 || movement.PlayerIndex >= SavedAppearances.Length) return;

        AppearanceSnapshot snapshot = new AppearanceSnapshot();
        for (int i = 0; i < parts.Length; i++)
        {
            Part part = parts[i];
            snapshot.hues[i] = part.hue;
            snapshot.saturations[i] = part.saturation;
            snapshot.values[i] = part.value;
            snapshot.accessoryEnabled[i] = part.accessory == null || part.accessory.gameObject.activeSelf;
        }

        SavedAppearances[movement.PlayerIndex] = snapshot;
    }

    public static bool HasSavedAppearance(int playerIndex)
    {
        return playerIndex >= 0
            && playerIndex < SavedAppearances.Length
            && SavedAppearances[playerIndex] != null;
    }

    public static bool ApplySavedAppearance(GameObject player, int playerIndex)
    {
        if (player == null || !HasSavedAppearance(playerIndex)) return false;

        AppearanceSnapshot snapshot = SavedAppearances[playerIndex];
        foreach (Renderer partRenderer in player.GetComponentsInChildren<Renderer>(true))
        {
            Material[] materials = partRenderer.materials;
            for (int i = 0; i < materials.Length; i++)
            {
                Material material = materials[i];
                if (material == null) continue;

                int partIndex = GetPartIndexForMaterial(material.name);
                if (partIndex < 0) continue;

                Color color = Color.HSVToRGB(
                    snapshot.hues[partIndex],
                    snapshot.saturations[partIndex],
                    snapshot.values[partIndex]);
                if (material.HasProperty("_BaseColor"))
                {
                    color.a = material.GetColor("_BaseColor").a;
                    material.SetColor("_BaseColor", color);
                }
                else if (material.HasProperty("_Color"))
                {
                    color.a = material.GetColor("_Color").a;
                    material.SetColor("_Color", color);
                }
            }
        }

        int[] accessoryPartIndexes = { 3, 4 };
        foreach (int partIndex in accessoryPartIndexes)
        {
            foreach (string objectName in ObjectNames[partIndex])
            {
                Transform accessory = FindDescendant(player.transform, objectName);
                if (accessory != null)
                {
                    accessory.gameObject.SetActive(snapshot.accessoryEnabled[partIndex]);
                }
            }
        }

        return true;
    }

    private static int GetPartIndexForMaterial(string materialName)
    {
        string normalizedName = materialName.ToLowerInvariant();
        if (normalizedName.Contains("pupil")) return 6;
        if (normalizedName.Contains("eye")) return 5;
        if (normalizedName.Contains("beard")) return 4;
        if (normalizedName.Contains("hat")) return 3;
        if (normalizedName.Contains("head")) return 2;
        if (normalizedName.Contains("shirt")) return 0;
        if (normalizedName.Contains("arm")) return 1;
        return -1;
    }

    private void RestoreGameplayState()
    {
        if (body != null)
        {
            body.position = customizationStartPosition;
            body.rotation = customizationStartRotation;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
        transform.SetPositionAndRotation(customizationStartPosition, customizationStartRotation);

        Physics.SyncTransforms();
        playerCamera.enabled = cameraWasEnabled;
        if (playerAudioListener != null) playerAudioListener.enabled = listenerWasEnabled;
        cameraController.enabled = controllerWasEnabled;
        movement.enabled = movementWasEnabled;

        if (body != null) body.useGravity = gravityWasEnabled;
        Time.timeScale = previousTimeScale;
    }

    private void OnDestroy()
    {
        if (!isCustomizing) return;

        if (previewCamera != null) Destroy(previewCamera.gameObject);
        RestoreGameplayState();
    }
}
