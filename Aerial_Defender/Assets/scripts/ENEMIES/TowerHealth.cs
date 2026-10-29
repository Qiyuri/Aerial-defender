using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class TowerHealth : MonoBehaviour
{
    [Header("Tower health")]
    [SerializeField, Min(1f)] private float maximumHealth = 100f;
    [SerializeField, Min(0f)] private float fullHealthAircraftDamage = 50f;
    [SerializeField, Min(0f)] private float halfHealthAircraftDamage = 25f;
    [SerializeField, Min(0f)] private float destroyedAircraftDamage = 10f;

    [Header("Game over")]
    [SerializeField] private bool showHealthOnScreen = true;

    private float currentHealth;
    private bool gameOver;
    private bool isLoadingScene;
    private int selectedGameOverOption = 1;
    private GUIStyle healthStyle;
    private GUIStyle gameOverStyle;
    private GUIStyle gameOverButtonStyle;

    public float CurrentHealth => currentHealth;
    public float MaximumHealth => maximumHealth;

    private void Awake()
    {
        currentHealth = maximumHealth;
    }

    private void Update()
    {
        if (!gameOver) return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.leftArrowKey.wasPressedThisFrame || keyboard.aKey.wasPressedThisFrame)
            {
                selectedGameOverOption = 0;
            }
            else if (keyboard.rightArrowKey.wasPressedThisFrame || keyboard.dKey.wasPressedThisFrame)
            {
                selectedGameOverOption = 1;
            }
            else if (keyboard.enterKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame)
            {
                SelectGameOverOption();
                return;
            }
            else if (keyboard.mKey.wasPressedThisFrame)
            {
                ReturnToMainMenu();
                return;
            }
            else if (keyboard.rKey.wasPressedThisFrame)
            {
                RestartWithSavedCustomization();
                return;
            }
        }

        foreach (Gamepad gamepad in Gamepad.all)
        {
            if (gamepad.dpad.left.wasPressedThisFrame || gamepad.leftStick.left.wasPressedThisFrame)
            {
                selectedGameOverOption = 0;
            }
            else if (gamepad.dpad.right.wasPressedThisFrame || gamepad.leftStick.right.wasPressedThisFrame)
            {
                selectedGameOverOption = 1;
            }

            if (gamepad.buttonSouth.wasPressedThisFrame)
            {
                SelectGameOverOption();
                return;
            }
        }
    }

    public void TakeAircraftImpact(float aircraftHealthFraction)
    {
        if (gameOver) return;

        float healthFraction = Mathf.Clamp01(aircraftHealthFraction);
        float damage;
        if (healthFraction <= 0f)
        {
            damage = destroyedAircraftDamage;
        }
        else if (healthFraction <= 0.5f)
        {
            damage = Mathf.Lerp(destroyedAircraftDamage, halfHealthAircraftDamage, healthFraction * 2f);
        }
        else
        {
            damage = Mathf.Lerp(halfHealthAircraftDamage, fullHealthAircraftDamage, (healthFraction - 0.5f) * 2f);
        }

        currentHealth = Mathf.Max(0f, currentHealth - damage);
        if (currentHealth <= 0f)
        {
            gameOver = true;
            Time.timeScale = 0f;
        }
    }

    private void OnGUI()
    {
        if (gameOver)
        {
            DrawGameOver();
            return;
        }

        if (!showHealthOnScreen) return;

        if (healthStyle == null)
        {
            healthStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleLeft,
                fontStyle = FontStyle.Bold,
                fontSize = 20,
                normal = { textColor = Color.white }
            };
        }

        const float left = 20f;
        const float top = 20f;
        const float panelWidth = 280f;
        const float panelHeight = 76f;
        Rect panelRect = new Rect(left, top, panelWidth, panelHeight);
        Rect barRect = new Rect(left + 12f, top + 46f, panelWidth - 24f, 16f);

        Color previousColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.75f);
        GUI.DrawTexture(panelRect, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(
            new Rect(left + 12f, top + 6f, panelWidth - 24f, 34f),
            $"TOWER HP: {Mathf.CeilToInt(currentHealth)} / {Mathf.CeilToInt(maximumHealth)}",
            healthStyle);

        GUI.color = new Color(0.18f, 0.18f, 0.18f, 1f);
        GUI.DrawTexture(barRect, Texture2D.whiteTexture);
        float healthFraction = maximumHealth > 0f ? Mathf.Clamp01(currentHealth / maximumHealth) : 0f;
        GUI.color = Color.Lerp(Color.red, Color.green, healthFraction);
        GUI.DrawTexture(
            new Rect(barRect.x, barRect.y, barRect.width * healthFraction, barRect.height),
            Texture2D.whiteTexture);
        GUI.color = previousColor;
    }

    private void DrawGameOver()
    {
        if (gameOverStyle == null)
        {
            gameOverStyle = new GUIStyle(GUI.skin.label)
            {
                alignment = TextAnchor.MiddleCenter,
                fontStyle = FontStyle.Bold,
                fontSize = Mathf.Clamp(Screen.height / 8, 48, 120),
                normal = { textColor = Color.red }
            };
        }

        Color previousColor = GUI.color;
        GUI.color = new Color(0f, 0f, 0f, 0.8f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(0f, Screen.height * 0.2f, Screen.width, Screen.height * 0.25f), "GAME OVER", gameOverStyle);
        float buttonWidth = Mathf.Min(360f, (Screen.width - 60f) * 0.5f);
        float buttonHeight = 72f;
        float buttonGap = 20f;
        float buttonsWidth = buttonWidth * 2f + buttonGap;
        float buttonsX = (Screen.width - buttonsWidth) * 0.5f;
        float buttonsY = Screen.height * 0.55f;
        if (gameOverButtonStyle == null)
        {
            gameOverButtonStyle = new GUIStyle(GUI.skin.button)
            {
                fontStyle = FontStyle.Bold,
                fontSize = Mathf.Clamp(Screen.height / 35, 18, 30)
            };
        }

        Color previousBackgroundColor = GUI.backgroundColor;
        if (selectedGameOverOption == 0) GUI.backgroundColor = new Color(0.7f, 0.85f, 1f);
        if (GUI.Button(
            new Rect(buttonsX, buttonsY, buttonWidth, buttonHeight),
            selectedGameOverOption == 0 ? "> MAIN MENU" : "MAIN MENU",
            gameOverButtonStyle))
        {
            ReturnToMainMenu();
        }

        GUI.backgroundColor = previousBackgroundColor;
        if (selectedGameOverOption == 1) GUI.backgroundColor = new Color(0.7f, 0.85f, 1f);
        if (GUI.Button(
            new Rect(buttonsX + buttonWidth + buttonGap, buttonsY, buttonWidth, buttonHeight),
            selectedGameOverOption == 1 ? "> RETRY" : "RETRY",
            gameOverButtonStyle))
        {
            RestartWithSavedCustomization();
        }

        GUI.backgroundColor = previousBackgroundColor;
        GUIStyle hintStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = Mathf.Clamp(Screen.height / 60, 14, 24),
            normal = { textColor = Color.white }
        };
        GUI.Label(
            new Rect(0f, buttonsY + buttonHeight + 14f, Screen.width, 48f),
            "Choose with left/right and press A / Enter  |  R: Retry  |  M: Main Menu",
            hintStyle);
        GUI.color = previousColor;
    }

    private void SelectGameOverOption()
    {
        if (selectedGameOverOption == 0)
        {
            ReturnToMainMenu();
        }
        else
        {
            RestartWithSavedCustomization();
        }
    }

    private void ReturnToMainMenu()
    {
        if (isLoadingScene) return;
        isLoadingScene = true;
        Time.timeScale = 1f;
        SceneManager.LoadScene(0);
    }

    private void RestartWithSavedCustomization()
    {
        if (isLoadingScene) return;
        isLoadingScene = true;
        LocalMultiplayerManager.RequestRestartWithSavedAppearance();
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }
}
