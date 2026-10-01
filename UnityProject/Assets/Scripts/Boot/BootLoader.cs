using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    /// <summary>
    /// Minimal Android startup probe.
    /// This scene deliberately does not load gameplay and does not depend on fonts,
    /// TextMeshPro, UI Text, or any gameplay asset.
    /// </summary>
    public sealed class BootLoader : MonoBehaviour
    {
        private void Awake()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            StartupCheckpoint.Set("DiagnosticBootSceneAwake");
            Debug.Log("PERSIA_BOOT_DIAGNOSTIC: Awake");
        }

        private void Start()
        {
            StartupCheckpoint.Set("DiagnosticBootSceneStarted");
            Debug.Log("PERSIA_BOOT_DIAGNOSTIC: Start");
        }

        private void OnGUI()
        {
            // Pure texture/rectangle output: no fonts, no TMP, no UI system.
            GUI.color = new Color(0.02f, 0.02f, 0.04f, 1f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);

            float w = Mathf.Min(Screen.width - 80f, 900f);
            float h = Mathf.Min(Screen.height - 80f, 520f);
            float x = (Screen.width - w) * 0.5f;
            float y = (Screen.height - h) * 0.5f;

            // Bright, unmistakable startup probe.
            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(x, y, w, h), Texture2D.whiteTexture);

            GUI.color = Color.green;
            GUI.DrawTexture(new Rect(x, y, w, 32f), Texture2D.whiteTexture);

            GUI.color = Color.magenta;
            GUI.DrawTexture(new Rect(x, y + h - 32f, w, 32f), Texture2D.whiteTexture);

            GUI.color = Color.cyan;
            GUI.DrawTexture(new Rect(x + 32f, y + 80f, w - 64f, h - 160f), Texture2D.whiteTexture);

            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(x + 80f, y + 140f, w - 160f, h - 280f), Texture2D.whiteTexture);

            GUI.color = Color.white;
        }
    }
}
