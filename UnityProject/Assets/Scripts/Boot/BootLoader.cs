using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PersiaWar.Unity2D5D
{
    /// <summary>
    /// Lightweight first scene for Android startup. It deliberately contains no
    /// gameplay objects or asset loading. The gameplay scene is prepared in the
    /// background and activated after a bounded warm-up so Android cannot remain parked on the boot screen.
    /// </summary>
    public sealed class BootLoader : MonoBehaviour
    {
        private const string GameplayScenePath = "Assets/Scenes/PersiaWarPrototype.unity";
        private const float MinimumVisibleSeconds = 0.65f;
        private const float MaxPreActivationWaitSeconds = 8f;

        private AsyncOperation loadOperation;
        private GUIStyle titleStyle;
        private GUIStyle statusStyle;
        private GUIStyle percentStyle;
        private GUIStyle footerStyle;
        private bool failed;
        private string failureMessage = string.Empty;
        private float progress;
        private float loadElapsed;
        private bool loadWarningLogged;
        private string status = "Starting Persia War...";

        private void Awake()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            progress = 0f;
            Debug.Log("PERSIA_BOOT_STAGE: BootSceneStarted");
        }

        private void Start()
        {
            BuildStyles();
            StartCoroutine(LoadGameplayScene());
        }

        private IEnumerator LoadGameplayScene()
        {
            // Give the BootScene at least one rendered frame before the gameplay
            // scene starts allocating its larger runtime objects.
            yield return null;

            float visibleSeconds = 0f;
            status = "Preparing game engine...";

            try
            {
                loadOperation = SceneManager.LoadSceneAsync(GameplayScenePath, LoadSceneMode.Single);
            }
            catch (Exception ex)
            {
                Fail("Could not start the gameplay scene load.", ex);
                yield break;
            }

            if (loadOperation == null)
            {
                Fail("Unity did not return a scene load operation.", null);
                yield break;
            }

            loadOperation.allowSceneActivation = false;
            Debug.Log("PERSIA_BOOT_STAGE: MainSceneLoadStarted");

            while (loadOperation.progress < 0.9f && loadElapsed < MaxPreActivationWaitSeconds)
            {
                progress = Mathf.Clamp01(loadOperation.progress / 0.9f) * 0.92f;
                loadElapsed += Time.unscaledDeltaTime;
                if (!loadWarningLogged && loadElapsed > 5f)
                {
                    loadWarningLogged = true;
                    Debug.LogWarning("PERSIA_BOOT_STAGE: MainSceneLoadTakingLongerThanExpected");
                }
                status = loadElapsed > 5f ? "Preparing battlefield..." : "Loading battlefield...";
                visibleSeconds += Time.unscaledDeltaTime;
                yield return null;
            }

            if (loadOperation.progress < 0.9f)
            {
                Debug.LogWarning("PERSIA_BOOT_STAGE: ActivatingMainSceneAfterBoundedWarmup");
                status = "Starting Persia War...";
            }

            // Keep the lightweight boot UI visible briefly so the device gets a real
            // startup frame before the larger scene activates.
            while (visibleSeconds < MinimumVisibleSeconds)
            {
                visibleSeconds += Time.unscaledDeltaTime;
                progress = Mathf.Max(progress, 0.92f);
                status = "Finalizing resources...";
                yield return null;
            }

            progress = 0.97f;
            status = "Starting Persia War...";
            yield return null;

            Debug.Log("PERSIA_BOOT_STAGE: MainSceneActivationRequested");
            loadOperation.allowSceneActivation = true;

            while (!loadOperation.isDone)
            {
                progress = Mathf.Lerp(0.97f, 1f, Mathf.Clamp01(loadOperation.progress));
                yield return null;
            }

            progress = 1f;
            Debug.Log("PERSIA_BOOT_STAGE: MainSceneActivated");
        }

        private void Fail(string message, Exception exception)
        {
            failed = true;
            failureMessage = message;
            status = "Startup failed";
            progress = 0f;

            if (exception != null)
                Debug.LogException(exception);
            else
                Debug.LogError("PERSIA_BOOT_STAGE: " + message);
        }

        private void BuildStyles()
        {
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 42,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            statusStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 19,
                alignment = TextAnchor.MiddleCenter
            };

            percentStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 17,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };

            footerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                alignment = TextAnchor.MiddleCenter
            };
        }

        private void OnGUI()
        {
            Color old = GUI.color;

            GUI.color = new Color(0.025f, 0.045f, 0.075f, 1f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);

            float margin = Mathf.Max(24f, Screen.width * 0.07f);
            float panelWidth = Mathf.Min(Screen.width - margin * 2f, 760f);
            float centerX = (Screen.width - panelWidth) * 0.5f;
            float centerY = Screen.height * 0.5f;

            Rect panel = new Rect(centerX, centerY - 165f, panelWidth, 330f);
            GUI.color = new Color(0.055f, 0.085f, 0.125f, 0.98f);
            GUI.DrawTexture(panel, Texture2D.whiteTexture);
            GUI.color = new Color(0.88f, 0.65f, 0.20f, 0.85f);
            GUI.DrawTexture(new Rect(panel.x, panel.y, panel.width, 5f), Texture2D.whiteTexture);
            GUI.DrawTexture(new Rect(panel.x, panel.yMax - 5f, panel.width, 5f), Texture2D.whiteTexture);

            GUI.color = Color.white;
            GUI.Label(new Rect(centerX, centerY - 128f, panelWidth, 58f), "PERSIA WAR", titleStyle);
            GUI.Label(new Rect(centerX, centerY - 62f, panelWidth, 34f), status, statusStyle);

            Rect bar = new Rect(centerX + 60f, centerY + 2f, panelWidth - 120f, 30f);
            GUI.color = new Color(0.12f, 0.15f, 0.19f, 1f);
            GUI.DrawTexture(bar, Texture2D.whiteTexture);

            GUI.color = failed
                ? new Color(0.70f, 0.18f, 0.14f, 1f)
                : new Color(0.88f, 0.65f, 0.20f, 1f);
            GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(progress), bar.height), Texture2D.whiteTexture);

            GUI.color = Color.white;
            GUI.Label(new Rect(centerX, centerY + 44f, panelWidth, 30f), Mathf.RoundToInt(progress * 100f) + "%", percentStyle);

            if (failed)
            {
                GUI.Label(
                    new Rect(centerX + 35f, centerY + 77f, panelWidth - 70f, 48f),
                    failureMessage,
                    footerStyle);
            }
            else
            {
                GUI.Label(
                    new Rect(centerX, centerY + 84f, panelWidth, 30f),
                    "Preparing the battlefield • Please wait.",
                    footerStyle);
            }

            GUI.color = old;
        }
    }
}
