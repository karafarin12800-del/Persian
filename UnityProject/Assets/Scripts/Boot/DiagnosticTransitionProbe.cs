using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    /// <summary>
    /// Asset-free second-stage probe. If this renders, Unity can create and activate
    /// a runtime scene successfully on the target Android device.
    /// </summary>
    public sealed class DiagnosticTransitionProbe : MonoBehaviour
    {
        private void Awake()
        {
            StartupCheckpoint.Set("DiagnosticTransitionSceneAwake");
            Debug.Log("PERSIA_BOOT_DIAGNOSTIC: Transition scene Awake");
        }

        private void Start()
        {
            StartupCheckpoint.Set("DiagnosticTransitionSceneStarted");
            Debug.Log("PERSIA_BOOT_DIAGNOSTIC: Transition scene Start");
        }

        private void OnGUI()
        {
            GUI.color = new Color(0.01f, 0.01f, 0.015f, 1f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);

            float w = Mathf.Min(Screen.width - 100f, 880f);
            float h = Mathf.Min(Screen.height - 100f, 500f);
            float x = (Screen.width - w) * 0.5f;
            float y = (Screen.height - h) * 0.5f;

            // Stage 2 has a deliberately different pattern from BootScene.
            GUI.color = new Color(1f, 0.55f, 0f, 1f);
            GUI.DrawTexture(new Rect(x, y, 36f, h), Texture2D.whiteTexture);

            GUI.color = new Color(0.25f, 0.55f, 1f, 1f);
            GUI.DrawTexture(new Rect(x + w - 36f, y, 36f, h), Texture2D.whiteTexture);

            GUI.color = Color.white;
            GUI.DrawTexture(new Rect(x + 36f, y, w - 72f, 36f), Texture2D.whiteTexture);

            GUI.color = new Color(0.2f, 0.9f, 0.35f, 1f);
            GUI.DrawTexture(new Rect(x + 36f, y + h - 36f, w - 72f, 36f), Texture2D.whiteTexture);

            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(x + 120f, y + 110f, w - 240f, h - 220f), Texture2D.whiteTexture);

            GUI.color = Color.white;
        }
    }
}
