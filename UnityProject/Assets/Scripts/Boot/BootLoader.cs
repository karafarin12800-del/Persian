using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    /// <summary>
    /// Temporary startup isolation test.
    /// This build intentionally does NOT load the gameplay scene.
    /// If this screen is visible, Unity has reached the first scene and rendered a frame.
    /// </summary>
    public sealed class BootLoader : MonoBehaviour
    {
        private GUIStyle titleStyle;
        private GUIStyle bodyStyle;
        private GUIStyle smallStyle;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Debug.Log("PERSIA_BOOT_DIAGNOSTIC: Awake");
        }

        private void Start()
        {
            BuildStyles();
            Debug.Log("PERSIA_BOOT_DIAGNOSTIC: Start");
            StartupCheckpoint.Set("DiagnosticBootSceneStarted");
        }

        private void BuildStyles()
        {
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 34,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 22,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };

            smallStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
        }

        private void OnGUI()
        {
            Color old = GUI.color;

            GUI.color = new Color(0.015f, 0.025f, 0.045f, 1f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);

            float width = Mathf.Min(Screen.width - 48f, 820f);
            float height = Mathf.Min(Screen.height - 48f, 430f);
            float x = (Screen.width - width) * 0.5f;
            float y = (Screen.height - height) * 0.5f;

            GUI.color = new Color(0.06f, 0.10f, 0.16f, 1f);
            GUI.DrawTexture(new Rect(x, y, width, height), Texture2D.whiteTexture);

            GUI.color = new Color(0.95f, 0.65f, 0.12f, 1f);
            GUI.DrawTexture(new Rect(x, y, width, 6f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x, y + height - 6f, width, 6f), Texture2D.whiteTexture);

            GUI.color = Color.white;
            GUI.Label(new Rect(x + 20f, y + 55f, width - 40f, 55f),
                "PERSIA WAR", titleStyle);

            GUI.Label(new Rect(x + 30f, y + 145f, width - 60f, 90f),
                "STARTUP TEST — STEP 1\nFIRST SCENE IS RUNNING", bodyStyle);

            GUI.Label(new Rect(x + 35f, y + 260f, width - 70f, 70f),
                "If you can read this, Android + Unity + BootScene + first frame are working.\nBattlefield loading has NOT started in this test.",
                smallStyle);

            GUI.color = new Color(0.95f, 0.65f, 0.12f, 1f);
            GUI.Label(new Rect(x + 30f, y + 350f, width - 60f, 40f),
                "DIAGNOSTIC BUILD • NO GAMEPLAY LOAD", smallStyle);

            GUI.color = old;
        }
    }
}
