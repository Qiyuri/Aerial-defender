using UnityEngine;
using UnityEngine.InputSystem;

public class LocalMultiplayerManager : MonoBehaviour
{
    public static int SelectedPlayerCount { get; set; } = 1;
    public static bool IsCustomizationFlowActive { get; private set; }
    public static bool HasCustomizationPreviewPosition { get; private set; }
    public static Vector3 CustomizationPreviewPosition { get; private set; }
    public static bool RestartWithSavedAppearance { get; private set; }

    [Header("Player 2 materials")]
    [SerializeField] private Material armMaterial;
    [SerializeField] private Material beardMaterial;
    [SerializeField] private Material hatMaterial;
    [SerializeField] private Material headMaterial;
    [SerializeField] private Material shirtMaterial;
    [SerializeField] private Material eyeWhiteMaterial;
    [SerializeField] private Material pupilsMaterial;

    private Movement firstPlayer;
    private Movement secondPlayer;
    private Vector3 secondPlayerGameplayPosition;
    private Quaternion secondPlayerGameplayRotation;
    private CharacterCustomization activeCustomization;
    private bool waitingForSecondCustomization;
    private bool waitingForGameplayStart;
    private bool restoreSavedAppearance;
    private string statusMessage;
    private Renderer[] firstPlayerRenderers;
    private bool[] firstPlayerRendererVisibility;
    private Renderer[] secondPlayerRenderers;
    private bool[] secondPlayerRendererVisibility;

    private void Awake()
    {
        IsCustomizationFlowActive = true;
        HasCustomizationPreviewPosition = false;
    }

    private void Start()
    {
        restoreSavedAppearance = RestartWithSavedAppearance;
        RestartWithSavedAppearance = false;

        Movement[] players = FindObjectsOfType<Movement>();
        if (players.Length == 0)
        {
            Debug.LogError("Local multiplayer could not find a player with a Movement component.", this);
            IsCustomizationFlowActive = false;
            enabled = false;
            return;
        }

        firstPlayer = players[0];
        CustomizationPreviewPosition = firstPlayer.transform.position + Vector3.up * 100f;
        HasCustomizationPreviewPosition = true;
        firstPlayerRenderers = firstPlayer.GetComponentsInChildren<Renderer>(true);
        firstPlayerRendererVisibility = new bool[firstPlayerRenderers.Length];
        for (int i = 0; i < firstPlayerRenderers.Length; i++)
        {
            firstPlayerRendererVisibility[i] = firstPlayerRenderers[i].enabled;
        }
        RefreshControllerAssignments();

        if (SelectedPlayerCount < 2)
        {
            SetCameraViewport(firstPlayer, new Rect(0f, 0f, 1f, 1f), true);
            if (restoreSavedAppearance)
            {
                CharacterCustomization.ApplySavedAppearance(firstPlayer.gameObject, 0);
                SetPlayerGameplayEnabled(firstPlayer, true);
                GameManager gameManager = FindObjectOfType<GameManager>();
                if (gameManager != null) gameManager.DisableCustomizationLight();
                IsCustomizationFlowActive = false;
                return;
            }

            activeCustomization = EnsureCustomization(firstPlayer);
            waitingForGameplayStart = true;
            return;
        }

        CreateSecondPlayer();
    }

    private void CreateSecondPlayer()
    {
        if (firstPlayer == null || secondPlayer != null) return;

        Vector3 secondPlayerPosition = firstPlayer.transform.position + firstPlayer.transform.right * 2.5f;
        GameObject secondPlayerObject = Instantiate(
            firstPlayer.gameObject,
            secondPlayerPosition,
            firstPlayer.transform.rotation);
        secondPlayerObject.name = "Player 2";
        secondPlayer = secondPlayerObject.GetComponent<Movement>();
        if (secondPlayer == null)
        {
            Debug.LogError("The duplicated player prefab is missing its Movement component.", secondPlayerObject);
            Destroy(secondPlayerObject);
            return;
        }

        secondPlayerGameplayPosition = secondPlayer.transform.position;
        secondPlayerGameplayRotation = secondPlayer.transform.rotation;
        SetPlayerPosition(secondPlayer, CustomizationPreviewPosition, secondPlayerGameplayRotation);
        RefreshControllerAssignments();
        ApplyPlayerTwoMaterials(secondPlayerObject);
        secondPlayerRenderers = secondPlayer.GetComponentsInChildren<Renderer>(true);
        secondPlayerRendererVisibility = new bool[secondPlayerRenderers.Length];
        for (int i = 0; i < secondPlayerRenderers.Length; i++)
        {
            secondPlayerRendererVisibility[i] = secondPlayerRenderers[i].enabled;
        }
        SetSecondPlayerRenderersVisible(false);
        SetCameraViewport(firstPlayer, new Rect(0f, 0f, 1f, 1f), true);
        SetCameraViewport(secondPlayer, new Rect(0f, 0f, 1f, 1f), false);
        if (restoreSavedAppearance)
        {
            SetCameraViewport(firstPlayer, new Rect(0f, 0f, 0.5f, 1f), true);
            SetCameraViewport(secondPlayer, new Rect(0.5f, 0f, 0.5f, 1f), false);
            SetPlayerPosition(secondPlayer, secondPlayerGameplayPosition, secondPlayerGameplayRotation);
            CharacterCustomization.ApplySavedAppearance(firstPlayer.gameObject, 0);
            CharacterCustomization.ApplySavedAppearance(secondPlayer.gameObject, 1);
            SetSecondPlayerRenderersVisible(true);
            SetPlayerGameplayEnabled(firstPlayer, true);
            SetPlayerGameplayEnabled(secondPlayer, true);
            GameManager gameManager = FindObjectOfType<GameManager>();
            if (gameManager != null) gameManager.DisableCustomizationLight();
            IsCustomizationFlowActive = false;
            return;
        }

        SetPlayerPosition(secondPlayer, CustomizationPreviewPosition, secondPlayerGameplayRotation);
        secondPlayer.gameObject.SetActive(true);
        SetPlayerGameplayEnabled(secondPlayer, false);
        secondPlayer.gameObject.SetActive(false);

        activeCustomization = EnsureCustomization(firstPlayer);
        waitingForSecondCustomization = true;
        if (Gamepad.all.Count < 2)
        {
            statusMessage = "PLAYER 2 IS SPAWNED. Waiting for the second controller to be recognized.";
            Debug.LogWarning(
                "Player 2 was spawned, but Unity currently reports only " + Gamepad.all.Count
                    + " gamepad(s). Controller input will be assigned when the second gamepad is recognized.",
                this);
        }
        else
        {
            statusMessage = null;
        }
    }

    private void Update()
    {
        RefreshControllerAssignments();

        if (activeCustomization == null || activeCustomization.IsCustomizing)
        {
            return;
        }

        if (waitingForSecondCustomization)
        {
            waitingForSecondCustomization = false;
            waitingForGameplayStart = true;
            SetPlayerGameplayEnabled(firstPlayer, false);
            SetFirstPlayerRenderersVisible(false);
            firstPlayer.gameObject.SetActive(false);
            SetPlayerPosition(secondPlayer, CustomizationPreviewPosition, secondPlayerGameplayRotation);
            secondPlayer.gameObject.SetActive(true);
            SetPlayerGameplayEnabled(secondPlayer, true);
            SetSecondPlayerRenderersVisible(true);
            activeCustomization = EnsureCustomization(secondPlayer);
        }
        else if (waitingForGameplayStart)
        {
            waitingForGameplayStart = false;
            firstPlayer.gameObject.SetActive(true);
            SetPlayerGameplayEnabled(firstPlayer, true);
            SetFirstPlayerRenderersVisible(true);

            if (secondPlayer != null)
            {
                secondPlayer.gameObject.SetActive(true);
                SetPlayerPosition(secondPlayer, secondPlayerGameplayPosition, secondPlayerGameplayRotation);
                SetCameraViewport(firstPlayer, new Rect(0f, 0f, 0.5f, 1f), true);
                SetCameraViewport(secondPlayer, new Rect(0.5f, 0f, 0.5f, 1f), false);
                SetPlayerGameplayEnabled(secondPlayer, true);
                SetSecondPlayerRenderersVisible(true);
            }

            GameManager gameManager = FindObjectOfType<GameManager>();
            if (gameManager != null) gameManager.DisableCustomizationLight();
            IsCustomizationFlowActive = false;
        }
    }

    private void SetFirstPlayerRenderersVisible(bool visible)
    {
        SetRenderersVisible(firstPlayerRenderers, firstPlayerRendererVisibility, visible);
    }

    private void SetSecondPlayerRenderersVisible(bool visible)
    {
        SetRenderersVisible(secondPlayerRenderers, secondPlayerRendererVisibility, visible);
    }

    private static void SetRenderersVisible(Renderer[] renderers, bool[] originalVisibility, bool visible)
    {
        if (renderers == null || originalVisibility == null) return;

        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null)
            {
                renderers[i].enabled = visible ? originalVisibility[i] : false;
            }
        }
    }

    private void RefreshControllerAssignments()
    {
        if (firstPlayer != null)
        {
            firstPlayer.SetPlayerInput(0, Gamepad.all.Count > 0 ? Gamepad.all[0] : null);
        }

        if (secondPlayer != null)
        {
            secondPlayer.SetPlayerInput(1, Gamepad.all.Count > 1 ? Gamepad.all[1] : null);
        }

        if (secondPlayer != null && Gamepad.all.Count > 1)
        {
            statusMessage = null;
        }
    }

    private CharacterCustomization EnsureCustomization(Movement player)
    {
        CharacterCustomization customization = player.GetComponent<CharacterCustomization>();
        if (customization == null)
        {
            customization = player.gameObject.AddComponent<CharacterCustomization>();
        }

        return customization;
    }

    private static void SetPlayerGameplayEnabled(Movement player, bool isEnabled)
    {
        if (player == null) return;

        player.enabled = isEnabled;
        CameraController cameraController = player.GetComponentInChildren<CameraController>(true);
        if (cameraController != null) cameraController.enabled = isEnabled;

        foreach (Camera playerCamera in player.GetComponentsInChildren<Camera>(true))
        {
            playerCamera.enabled = isEnabled;
        }

        foreach (AudioListener listener in player.GetComponentsInChildren<AudioListener>(true))
        {
            listener.enabled = isEnabled && player.PlayerIndex == 0;
        }
    }

    private static void SetPlayerPosition(Movement player, Vector3 position, Quaternion rotation)
    {
        player.transform.SetPositionAndRotation(position, rotation);
        Rigidbody playerBody = player.GetComponent<Rigidbody>();
        if (playerBody != null)
        {
            playerBody.position = position;
            playerBody.rotation = rotation;
            playerBody.linearVelocity = Vector3.zero;
            playerBody.angularVelocity = Vector3.zero;
        }

        Physics.SyncTransforms();
    }

    private static void SetCameraViewport(Movement player, Rect viewport, bool enableAudioListener)
    {
        CameraController cameraController = player.GetComponentInChildren<CameraController>(true);
        if (cameraController != null)
        {
            cameraController.SetPlayerInput(player.PlayerIndex, player.InputGamepad);
        }

        Camera[] playerCameras = player.GetComponentsInChildren<Camera>(true);
        if (playerCameras.Length == 0)
        {
            Debug.LogError("A player camera is required for local multiplayer.", player);
            return;
        }

        foreach (Camera playerCamera in playerCameras)
        {
            playerCamera.rect = viewport;
            AudioListener listener = playerCamera.GetComponent<AudioListener>();
            if (listener != null) listener.enabled = enableAudioListener;
        }
    }

    private void ApplyPlayerTwoMaterials(GameObject player)
    {
        Renderer[] renderers = player.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer partRenderer in renderers)
        {
            Material[] materials = partRenderer.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < materials.Length; i++)
            {
                Material replacement = GetPlayerTwoMaterial(materials[i]);
                if (replacement == null) continue;

                materials[i] = replacement;
                changed = true;
            }

            if (changed) partRenderer.sharedMaterials = materials;
        }
    }

    private Material GetPlayerTwoMaterial(Material original)
    {
        if (original == null) return null;

        string materialName = original.name.ToLowerInvariant();
        if (materialName.Contains("pupil")) return pupilsMaterial;
        if (materialName.Contains("eye")) return eyeWhiteMaterial;
        if (materialName.Contains("beard")) return beardMaterial;
        if (materialName.Contains("hat")) return hatMaterial;
        if (materialName.Contains("head")) return headMaterial;
        if (materialName.Contains("shirt")) return shirtMaterial;
        if (materialName.Contains("arm")) return armMaterial;
        return null;
    }

    private void OnGUI()
    {
        if (string.IsNullOrEmpty(statusMessage)) return;

        GUI.Box(new Rect(16f, 16f, Mathf.Min(560f, Screen.width - 32f), 56f), statusMessage);
    }

    private void OnDestroy()
    {
        IsCustomizationFlowActive = false;
    }

    public static void RequestRestartWithSavedAppearance()
    {
        RestartWithSavedAppearance = true;
    }
}
