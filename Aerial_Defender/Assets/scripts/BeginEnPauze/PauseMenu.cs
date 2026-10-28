using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Canvas), typeof(GraphicRaycaster))]
public class PauseMenu : MonoBehaviour
{
    private Canvas pauseCanvas;
    private GraphicRaycaster graphicRaycaster;
    private RectTransform canvasTransform;
    private float previousTimeScale;
    private bool isPaused;

    private void Awake()
    {
        pauseCanvas = GetComponent<Canvas>();
        graphicRaycaster = GetComponent<GraphicRaycaster>();
        canvasTransform = GetComponent<RectTransform>();
    }

    private void Start()
    {
        previousTimeScale = Time.timeScale;
        pauseCanvas.enabled = false;
        graphicRaycaster.enabled = false;
    }

    private void Update()
    {
        if (!isPaused && Input.GetKeyDown(KeyCode.Escape))
        {
            OpenPauseMenu();
        }
    }

    private void OnDestroy()
    {
        if (isPaused)
        {
            Time.timeScale = previousTimeScale;
        }
    }

    private void OpenPauseMenu()
    {
        isPaused = true;
        Time.timeScale = 0f;
        canvasTransform.localScale = Vector3.one;
        pauseCanvas.enabled = true;
        graphicRaycaster.enabled = true;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }
}
