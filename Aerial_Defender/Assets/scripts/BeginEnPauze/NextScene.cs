using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

public class NextScene : MonoBehaviour
{
    private int selectedPlayerCount = 1;
    private bool isLoadingScene;
    private GUIStyle panelStyle;
    private GUIStyle titleStyle;
    private GUIStyle hintStyle;
    private GUIStyle selectedOptionStyle;
    private GUIStyle unselectedOptionStyle;

    private void Awake()
    {
        selectedPlayerCount = LocalMultiplayerManager.SelectedPlayerCount;
    }

    private void Update()
    {
        if (SceneManager.GetActiveScene().name != "BeginScherm") return;

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.leftArrowKey.wasPressedThisFrame) selectedPlayerCount = 1;
            if (keyboard.rightArrowKey.wasPressedThisFrame) selectedPlayerCount = 2;
            if (keyboard.enterKey.wasPressedThisFrame) LoadNextScene();
        }

        foreach (Gamepad gamepad in Gamepad.all)
        {
            if (gamepad.dpad.left.wasPressedThisFrame) selectedPlayerCount = 1;
            if (gamepad.dpad.right.wasPressedThisFrame) selectedPlayerCount = 2;

            Vector2 stick = gamepad.leftStick.ReadValue();
            if (stick.x <= -0.8f) selectedPlayerCount = 1;
            if (stick.x >= 0.8f) selectedPlayerCount = 2;

            if (gamepad.buttonSouth.wasPressedThisFrame) LoadNextScene();
        }
    }

    private void OnGUI()
    {
        if (SceneManager.GetActiveScene().name != "BeginScherm") return;

        EnsureStyles();

        float width = Mathf.Min(620f, Screen.width - 32f);
        float height = 154f;
        Rect panel = new Rect((Screen.width - width) * 0.5f, Screen.height - height - 24f, width, height);
        GUI.Box(panel, GUIContent.none, panelStyle);
        GUI.Label(
            new Rect(panel.x + 12f, panel.y + 10f, panel.width - 24f, 32f),
            "CHOOSE PLAYERS",
            titleStyle);

        float gap = 12f;
        float optionWidth = (panel.width - 40f - gap) * 0.5f;
        Rect onePlayerRect = new Rect(panel.x + 20f, panel.y + 48f, optionWidth, 54f);
        Rect twoPlayerRect = new Rect(onePlayerRect.xMax + gap, onePlayerRect.y, optionWidth, 54f);

        if (GUI.Button(onePlayerRect, "1 PLAYER", selectedPlayerCount == 1 ? selectedOptionStyle : unselectedOptionStyle))
        {
            selectedPlayerCount = 1;
        }

        if (GUI.Button(twoPlayerRect, "2 PLAYERS", selectedPlayerCount == 2 ? selectedOptionStyle : unselectedOptionStyle))
        {
            selectedPlayerCount = 2;
        }

        string controlsHint = selectedPlayerCount + (selectedPlayerCount == 1 ? " player selected" : " players selected")
            + "     |     Choose with left/right, then press A or click Start"
            + "     |     Controllers: " + Gamepad.all.Count + "/2";
        GUI.Label(new Rect(panel.x + 12f, panel.y + 110f, panel.width - 24f, 30f), controlsHint, hintStyle);
    }

    private void EnsureStyles()
    {
        if (panelStyle != null) return;

        panelStyle = new GUIStyle(GUI.skin.box)
        {
            normal = { background = CreateTexture(new Color(0.025f, 0.045f, 0.075f, 0.96f)) },
            border = new RectOffset(2, 2, 2, 2)
        };
        titleStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 22,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };
        hintStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 16,
            normal = { textColor = new Color(0.85f, 0.9f, 1f) }
        };
        selectedOptionStyle = new GUIStyle(GUI.skin.button)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 22,
            fontStyle = FontStyle.Bold,
            normal =
            {
                background = CreateTexture(new Color(0.08f, 0.55f, 0.28f, 1f)),
                textColor = Color.white
            },
            hover =
            {
                background = CreateTexture(new Color(0.1f, 0.68f, 0.34f, 1f)),
                textColor = Color.white
            },
            active =
            {
                background = CreateTexture(new Color(0.05f, 0.42f, 0.2f, 1f)),
                textColor = Color.white
            }
        };
        unselectedOptionStyle = new GUIStyle(selectedOptionStyle)
        {
            normal =
            {
                background = CreateTexture(new Color(0.12f, 0.16f, 0.23f, 1f)),
                textColor = new Color(0.84f, 0.87f, 0.92f, 1f)
            },
            hover =
            {
                background = CreateTexture(new Color(0.2f, 0.27f, 0.38f, 1f)),
                textColor = Color.white
            },
            active =
            {
                background = CreateTexture(new Color(0.08f, 0.12f, 0.18f, 1f)),
                textColor = Color.white
            }
        };
    }

    private static Texture2D CreateTexture(Color color)
    {
        Texture2D texture = new Texture2D(1, 1)
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        texture.SetPixel(0, 0, color);
        texture.Apply();
        return texture;
    }

    public void LoadNextScene()
    {
        if (isLoadingScene) return;

        if (SceneManager.GetActiveScene().name != "BeginScherm")
        {
            Debug.LogWarning("The player-count selection is only available from the main menu.", this);
            return;
        }

        int nextSceneIndex = SceneManager.GetActiveScene().buildIndex + 1;
        if (nextSceneIndex >= SceneManager.sceneCountInBuildSettings)
        {
            Debug.LogError("There is no next scene in Build Settings.");
            return;
        }

        isLoadingScene = true;
        LocalMultiplayerManager.SelectedPlayerCount = selectedPlayerCount;
        SceneManager.LoadScene(nextSceneIndex);
    }
}
