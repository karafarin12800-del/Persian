using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PersiaWar.Unity2D5D
{
    /// <summary>
    /// Safe Android boot flow. The gameplay scene is loaded additively first, then
    /// its root objects are activated in small stages so a single expensive scene
    /// activation cannot take down the process.
    /// </summary>
    public sealed class BootLoader : MonoBehaviour
    {
        private const string GameplayScenePath = "Assets/Scenes/PersiaWarPrototype.unity";
        private const float MinimumVisibleSeconds = 0.65f;

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
            StartCoroutine(LoadGameplaySceneSafely());
        }

        private IEnumerator LoadGameplaySceneSafely()
        {
            yield return null;

            status = "Loading battlefield...";
            Scene bootScene = SceneManager.GetActiveScene();
            float visibleSeconds = 0f;

            try
            {
                loadOperation = SceneManager.LoadSceneAsync(GameplayScenePath, LoadSceneMode.Additive);
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

            Debug.Log("PERSIA_BOOT_STAGE: MainSceneLoadStarted");

            while (!loadOperation.isDone)
            {
                loadElapsed += Time.unscaledDeltaTime;
                visibleSeconds += Time.unscaledDeltaTime;
                progress = Mathf.Clamp01(loadOperation.progress);
                status = loadElapsed > 5f ? "Preparing battlefield..." : "Loading battlefield...";

                if (!loadWarningLogged && loadElapsed > 5f)
                {
                    loadWarningLogged = true;
                    Debug.LogWarning("PERSIA_BOOT_STAGE: MainSceneLoadTakingLongerThanExpected");
                }

                yield return null;
            }

            Scene gameplayScene = SceneManager.GetSceneByPath(GameplayScenePath);
            if (!gameplayScene.IsValid() || !gameplayScene.isLoaded)
            {
                Fail("Battlefield scene loaded but is not valid.", null);
                yield break;
            }

            Debug.Log("PERSIA_BOOT_STAGE: MainSceneLoadedAdditive");
            progress = 0.80f;

            while (visibleSeconds < MinimumVisibleSeconds)
            {
                visibleSeconds += Time.unscaledDeltaTime;
                yield return null;
            }

            GameObject[] roots = gameplayScene.GetRootGameObjects();

            // Keep the activation order deterministic. The gameplay scene currently
            // contains exactly these root objects; unknown roots remain untouched.
            yield return ActivateRoot(roots, "Main Camera", 0.84f);
            yield return ActivateRoot(roots, "Player", 0.87f);
            yield return ActivateRoot(roots, "MobileInput", 0.89f);
            yield return ActivateRoot(roots, "WorldBounds", 0.91f);
            yield return ActivateRoot(roots, "GameRoot", 0.95f);
            yield return ActivateRoot(roots, "PrototypeFlow", 0.98f);

            SceneManager.SetActiveScene(gameplayScene);
            Debug.Log("PERSIA_BOOT_STAGE: MainSceneActivationRequested");
            Debug.Log("PERSIA_BOOT_STAGE: MainSceneActivated");

            // Only now remove the lightweight boot scene.
            AsyncOperation unload = SceneManager.UnloadSceneAsync(bootScene);
            if (unload != null)
            {
                while (!unload.isDone)
                    yield return null;
            }

            progress = 1f;
        }

        private IEnumerator ActivateRoot(GameObject[] roots, string rootName, float targetProgress)
        {
            GameObject root = FindRoot(roots, rootName);
            if (root == null)
            {
                Debug.LogWarning("PERSIA_BOOT_STAGE: MissingRoot:" + rootName);
                yield return null;
                yield break;
            }

            if (!root.activeSelf)
            {
                status = "Starting " + rootName + "...";
                Debug.Log("PERSIA_BOOT_STAGE: ActivatingRoot:" + rootName);
                root.SetActive(true);
                yield return null;
            }

            progress = targetProgress;
            yield return null;
        }

        private static GameObject FindRoot(GameObject[] roots, string rootName)
        {
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i] != null && roots[i].name == rootName)
                    return roots[i];
            }
            return null;
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
                GUI.Label(new Rect(centerX + 35f, centerY + 77f, panelWidth - 70f, 48f), failureMessage, footerStyle);
            }
            else
            {
                GUI.Label(new Rect(centerX, centerY + 84f, panelWidth, 30f), "Preparing the battlefield • Please wait.", footerStyle);
            }

            GUI.color = old;
        }
    }
}
