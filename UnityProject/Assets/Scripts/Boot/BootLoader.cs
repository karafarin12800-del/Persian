using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PersiaWar.Unity2D5D
{
    public sealed class BootLoader : MonoBehaviour
    {
        private AsyncOperation loadOperation;
        private bool loadStarted;
        private bool loadCompleted;
        private string status = "Starting...";

        private void Awake()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            StartupCheckpoint.Set("BootSceneAwake");
            Debug.Log("PERSIA_BOOT: BootScene Awake");
        }

        private void Start()
        {
            StartupCheckpoint.Set("BootSceneStarted");
            Debug.Log("PERSIA_BOOT: Starting direct gameplay scene load");
            StartCoroutine(LoadGameplayScene());
        }

        private IEnumerator LoadGameplayScene()
        {
            // Let BootScene finish one frame cleanly before replacing it.
            yield return null;

            loadStarted = true;
            status = "Loading PersiaWarPrototype...";
            StartupCheckpoint.Set("GameplayLoadStarted");

            loadOperation = SceneManager.LoadSceneAsync("PersiaWarPrototype", LoadSceneMode.Single);

            if (loadOperation == null)
            {
                status = "LoadSceneAsync returned null.";
                StartupCheckpoint.Set("GameplayLoadReturnedNull");
                yield break;
            }

            loadOperation.allowSceneActivation = true;

            while (!loadOperation.isDone)
            {
                status = "Loading PersiaWarPrototype... " +
                         Mathf.RoundToInt(Mathf.Clamp01(loadOperation.progress / 0.9f) * 100f) + "%";
                yield return null;
            }

            loadCompleted = true;
            StartupCheckpoint.Set("GameplaySceneActivated");
            Debug.Log("PERSIA_BOOT: PersiaWarPrototype activated");
        }

        private void OnGUI()
        {
            if (loadCompleted)
                return;

            GUI.color = new Color(0.02f, 0.025f, 0.04f, 1f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);

            float width = Mathf.Min(Screen.width - 80f, 900f);
            float height = Mathf.Min(Screen.height - 80f, 360f);
            float x = (Screen.width - width) * 0.5f;
            float y = (Screen.height - height) * 0.5f;

            GUI.color = new Color(0.08f, 0.11f, 0.16f, 1f);
            GUI.DrawTexture(new Rect(x, y, width, height), Texture2D.whiteTexture);

            GUI.color = new Color(0.92f, 0.66f, 0.18f, 1f);
            GUI.DrawTexture(new Rect(x, y, width, 8f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(x, y + height - 8f, width, 8f), Texture2D.whiteTexture);

            GUI.color = Color.white;
            GUI.Label(new Rect(x + 30f, y + 70f, width - 60f, 60f), "PERSIA WAR");
            GUI.Label(new Rect(x + 30f, y + 145f, width - 60f, 40f), status);
            GUI.Label(new Rect(x + 30f, y + 205f, width - 60f, 30f),
                loadStarted ? "Activating gameplay scene..." : "Starting Unity...");

#if UNITY_ANDROID
            string previous = StartupCheckpoint.Previous;
            if (!string.IsNullOrEmpty(previous) && previous != "none")
            {
                float dw = Mathf.Min(Screen.width - 48f, 760f);
                float dh = 118f;
                float dx = (Screen.width - dw) * 0.5f;
                float dy = y + height + 18f;
                GUI.color = new Color(0.04f, 0.07f, 0.11f, 0.96f);
                GUI.DrawTexture(new Rect(dx, dy, dw, dh), Texture2D.whiteTexture);
                GUI.color = new Color(0.92f, 0.66f, 0.18f, 1f);
                GUI.DrawTexture(new Rect(dx, dy, dw, 4f), Texture2D.whiteTexture);
                GUI.color = Color.white;
                GUI.Label(new Rect(dx + 14f, dy + 10f, dw - 28f, 22f), "PREVIOUS RUN DIAGNOSTIC");
                GUI.Label(new Rect(dx + 14f, dy + 36f, dw - 28f, 22f), "Checkpoint: " + previous);
                GUI.Label(new Rect(dx + 14f, dy + 60f, dw - 28f, 22f), "Exit: " + StartupCheckpoint.PreviousExitState);
                GUI.Label(new Rect(dx + 14f, dy + 84f, dw - 28f, 22f), "Assessment: " + StartupCheckpoint.PreviousAssessment);
            }
#endif
        }
    }
}