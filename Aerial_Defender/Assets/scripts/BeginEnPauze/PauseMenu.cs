using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Canvas), typeof(GraphicRaycaster))]
public class PauseMenu : MonoBehaviour
{
    private Canvas pauseCanvas;
    private GraphicRaycaster graphicRaycaster;
    private RectTransform canvasTransform;
    private float previousTimeScale;
    private bool isPaused;
    public static bool IsPaused { get; private set; }

    private void Awake()
    {
        pauseCanvas = GetComponent<Canvas>();
        graphicRaycaster = GetComponent<GraphicRaycaster>();
        canvasTransform = GetComponent<RectTransform>();
    }

    private void Start()
    {
        previousTimeScale = Time.timeScale;
        IsPaused = false;
        pauseCanvas.enabled = false;
        graphicRaycaster.enabled = false;
    }

    private void Update()
    {
        Gamepad gamepad = Gamepad.current;
        if (isPaused && (Input.GetKeyDown(KeyCode.Escape)
            || (gamepad != null && gamepad.startButton.wasPressedThisFrame)))
        {
            ResumeGame();
            return;
        }

        if (!isPaused && (Input.GetKeyDown(KeyCode.Escape)
            || (gamepad != null && gamepad.startButton.wasPressedThisFrame)))
        {
            OpenPauseMenu();
        }
    }

    private void OnDestroy()
    {
        if (isPaused)
        {
            Time.timeScale = previousTimeScale;
            IsPaused = false;
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }

    private void OpenPauseMenu()
    {
        isPaused = true;
        IsPaused = true;
        Time.timeScale = 0f;
        canvasTransform.localScale = Vector3.one;
        pauseCanvas.enabled = true;
        graphicRaycaster.enabled = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;

        EventSystem eventSystem = EventSystem.current;
        if (eventSystem == null) return;

        Selectable[] selectables = pauseCanvas.GetComponentsInChildren<Selectable>(true);
        foreach (Selectable selectable in selectables)
        {
            if (selectable == null || !selectable.gameObject.activeInHierarchy || !selectable.IsInteractable()) continue;

            eventSystem.SetSelectedGameObject(selectable.gameObject);
            return;
        }
    }

    public void ResumeGame()
    {
        if (!isPaused) return;

        isPaused = false;
        IsPaused = false;
        pauseCanvas.enabled = false;
        graphicRaycaster.enabled = false;
        Time.timeScale = previousTimeScale;
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        EventSystem eventSystem = EventSystem.current;
        if (eventSystem != null) eventSystem.SetSelectedGameObject(null);
    }
}
