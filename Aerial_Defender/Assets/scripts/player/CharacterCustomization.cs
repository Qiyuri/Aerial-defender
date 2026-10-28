using System.Collections.Generic;
using UnityEngine;

public class CharacterCustomization : MonoBehaviour
{
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

    private const float ReferenceWidth = 1440f;
    private const float ReferenceHeight = 900f;

    private Movement movement;
    private GameManager gameManager;
    private CameraController cameraController;
    private Camera playerCamera;
    private AudioListener playerAudioListener;
    private Rigidbody body;
    private Camera previewCamera;
    private Part[] parts;
    private int currentPart;
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

    private void Awake()
    {
        movement = GetComponent<Movement>();
        gameManager = FindObjectOfType<GameManager>();
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

        Vector3 previewOffset = Vector3.up * 100f;
        if (body != null)
        {
            body.position = customizationStartPosition + previewOffset;
        }
        else
        {
            transform.position = customizationStartPosition + previewOffset;
        }

        defaultPreviewPosition += previewOffset;
        previewCamera.transform.position += previewOffset;
        Physics.SyncTransforms();
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
        float cameraDistance = Mathf.Max(3f, characterSize * 2.2f);
        Vector3 lookAt = characterBounds.center;
        Vector3 cameraPosition = lookAt + transform.forward * cameraDistance + Vector3.up * (characterSize * 0.12f);

        GameObject cameraObject = new GameObject("Character Preview Camera");
        cameraObject.tag = "MainCamera";
        previewCamera = cameraObject.AddComponent<Camera>();
        previewCamera.fieldOfView = 35f;
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
        GUILayout.Space(24f * scale);

        if (part.accessory != null)
        {
            string accessoryButtonText = (part.accessory.gameObject.activeSelf ? "Wearing " : "Not wearing ")
                + part.label.ToLowerInvariant() + " (click to toggle)";
            if (GUILayout.Button(accessoryButtonText, buttonStyle, GUILayout.Height(70f * scale)))
            {
                part.accessory.gameObject.SetActive(!part.accessory.gameObject.activeSelf);
            }
        }

        GUILayout.Space(24f * scale);
        GUILayout.Label("Hue", labelStyle, GUILayout.Height(32f * scale));
        float updatedHue = GUILayout.HorizontalSlider(hue, 0f, 1f, GUILayout.Height(40f * scale));

        GUILayout.BeginHorizontal();
        GUILayout.Label("Saturation", labelStyle, GUILayout.Height(36f * scale));
        GUILayout.Label(Mathf.RoundToInt(saturation * 100f) + "%", valueStyle, GUILayout.Height(36f * scale));
        GUILayout.EndHorizontal();
        float updatedSaturation = GUILayout.HorizontalSlider(saturation, 0f, 1f, GUILayout.Height(40f * scale));

        GUILayout.BeginHorizontal();
        GUILayout.Label("Brightness", labelStyle, GUILayout.Height(36f * scale));
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
        if (GUILayout.Button("Back", buttonStyle, GUILayout.Height(56f * scale)))
        {
            ChangePart(currentPart - 1);
        }
        GUI.enabled = wasGuiEnabled;

        string confirmLabel = currentPart == parts.Length - 1 ? "Confirm and play" : "Confirm and next";
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
        GUILayout.EndHorizontal();
        GUILayout.EndArea();
    }

    private void ChangePart(int nextPart)
    {
        currentPart = nextPart;
        hue = parts[currentPart].hue;
        saturation = parts[currentPart].saturation;
        brightness = parts[currentPart].value;
        UpdatePreviewCameraForPart();
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
        float closeDistance = Mathf.Max(0.35f, eyeBounds.size.magnitude * 2.5f);
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
        if (gameManager == null) return;

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
        UpdateGameManagerMaterials();
        if (gameManager != null)
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

    private void RestoreGameplayState()
    {
        if (body != null)
        {
            body.position = customizationStartPosition;
            body.rotation = customizationStartRotation;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
        else
        {
            transform.SetPositionAndRotation(customizationStartPosition, customizationStartRotation);
        }

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
