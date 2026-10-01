using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PersiaWar.Unity2D5D
{
    /// <summary>
    /// Lightweight first scene for Android startup. It deliberately contains no
    /// gameplay objects or gameplay asset loading.
    /// </summary>
    public sealed class BootLoader : MonoBehaviour
    {
        private const string GameplayScenePath = "Assets/Scenes/PersiaWarPrototype.unity";
        private const float MinimumVisibleSeconds = 12f;
        private const float ReadyTimeoutSeconds = 45f;

        private AsyncOperation loadOperation;
        private GUIStyle titleStyle;
        private GUIStyle statusStyle;
        private GUIStyle percentStyle;
        private GUIStyle footerStyle;
        private bool failed;
        private string failureMessage = string.Empty;
        private float progress;
        private float progressTarget;
        private float loadElapsed;
        private bool loadWarningLogged;
        private bool startRequested;
        private bool preparationComplete;
        private bool preparationFailed;
        private string status = "Starting Persia War...";
        private string previousCheckpoint = string.Empty;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            progress = 0f;
            progressTarget = 0f;
            previousCheckpoint = StartupCheckpoint.Last;
            StartupCheckpoint.Set("BootSceneStarted");
            Debug.Log("PERSIA_BOOT_STAGE: BootSceneStarted");
        }

        private void Start()
        {
            BuildStyles();
            status = "Preparing game resources...";
            StartCoroutine(PrepareGameResources());
        }

        private IEnumerator PrepareGameResources()
        {
            yield return null;
            StartupCheckpoint.Set("PreflightStarted");

            Exception preparationException;
            if (!TryPrepareGameResources(out preparationException))
            {
                preparationFailed = true;
                Fail("Game preparation failed before Start.", preparationException);
                yield break;
            }

            yield return null;

            status = "Preparing battlefield in background...";
            loadElapsed = 0f;

            try
            {
                loadOperation = SceneManager.LoadSceneAsync(GameplayScenePath, LoadSceneMode.Additive);
            }
            catch (Exception ex)
            {
                preparationFailed = true;
                Fail("Unity could not prepare the gameplay scene.", ex);
                yield break;
            }

            if (loadOperation == null)
            {
                preparationFailed = true;
                Fail("Unity could not prepare the gameplay scene.", null);
                yield break;
            }

            loadOperation.allowSceneActivation = false;
            StartupCheckpoint.Set("GameplayScenePreloadStarted");

            while (loadOperation.progress < 0.9f)
            {
                loadElapsed += Time.unscaledDeltaTime;
                progressTarget = Mathf.Clamp01((loadOperation.progress / 0.9f) * 0.82f);
                progress = Mathf.MoveTowards(progress, progressTarget, Time.unscaledDeltaTime * 0.30f);
                status = loadElapsed > 8f
                    ? "Still preparing battlefield..."
                    : "Preparing battlefield in background...";
                yield return null;
            }

            progressTarget = 0.88f;
            progress = Mathf.MoveTowards(progress, progressTarget, 0.30f);
            StartupCheckpoint.Set("GameplayScenePreloaded");
            preparationComplete = true;
            progressTarget = 1f;
            progress = 1f;
            status = "Ready to deploy";
            StartupCheckpoint.Set("PreflightReady");
        }

        private bool TryPrepareGameResources(out Exception exception)
        {
            exception = null;
            try
            {
                Shader.Find("Unlit/Color");
                Shader.Find("Unlit/Texture");
                Shader.Find("Sprites/Default");
                RuntimeMaterialFactory.GetShader();

                Resources.Load<Texture2D>("PersianCharacters/Hero");
                Resources.Load<Texture2D>("PersianCharacters/Enemy_01");
                Resources.Load<Texture2D>("PersianCharacters/Enemy_02");
                Resources.Load<Texture2D>("PersianCharacters/Enemy_03");
                return true;
            }
            catch (Exception ex)
            {
                exception = ex;
                return false;
            }
        }

        private void BeginGame()
        {
            if (startRequested || failed || preparationFailed || !preparationComplete || loadOperation == null)
                return;

            startRequested = true;
            status = "Starting battlefield...";
            progress = 0f;
            progressTarget = 0f;
            StartCoroutine(ActivatePreparedGameplay());
        }

        private IEnumerator ActivatePreparedGameplay()
        {
            StartupCheckpoint.Set("GameplayActivationRequested");
            StartupCheckpoint.Set("GameplaySceneActivationStarted");
            loadOperation.allowSceneActivation = true;

            while (!loadOperation.isDone)
            {
                progressTarget = 0.25f;
                progress = Mathf.MoveTowards(progress, progressTarget, Time.unscaledDeltaTime * 0.20f);
                yield return null;
            }

            StartupCheckpoint.Set("GameplaySceneActivationCompleted");

            Scene gameplayScene = SceneManager.GetSceneByPath(GameplayScenePath);
            if (!gameplayScene.IsValid() || !gameplayScene.isLoaded)
            {
                Fail("Prepared gameplay scene is not loaded after activation.", null);
                yield break;
            }

            if (!SceneManager.SetActiveScene(gameplayScene))
            {
                Fail("Unity could not make the gameplay scene active before root activation.", null);
                yield break;
            }

            StartupCheckpoint.Set("GameplaySceneSetActiveBeforeRoots");
            yield return StartCoroutine(ActivateGameplayRoots(gameplayScene));
            if (failed)
                yield break;

            yield return StartCoroutine(ContinueGameplayInitialization());
        }

        private IEnumerator ActivateGameplayRoots(Scene gameplayScene)
        {
            StartupCheckpoint.Set("GameplayRootsActivationStarted");

            GameObject cameraRoot = FindRootObject(gameplayScene, "Main Camera");
            GameObject playerRoot = FindRootObject(gameplayScene, "Player");
            GameObject gameRoot = FindRootObject(gameplayScene, "GameRoot");
            GameObject mobileInputRoot = FindRootObject(gameplayScene, "MobileInput");
            GameObject worldBoundsRoot = FindRootObject(gameplayScene, "WorldBounds");
            GameObject prototypeFlowRoot = FindRootObject(gameplayScene, "PrototypeFlow");

            if (cameraRoot == null || playerRoot == null || gameRoot == null ||
                mobileInputRoot == null || worldBoundsRoot == null || prototypeFlowRoot == null)
            {
                Fail("Gameplay scene is missing one or more required roots.", null);
                yield break;
            }

            cameraRoot.SetActive(true);
            StartupCheckpoint.Set("GameplayCameraRootActivated");
            yield return null;

            playerRoot.SetActive(true);
            StartupCheckpoint.Set("GameplayPlayerRootActivated");
            yield return null;

            gameRoot.SetActive(true);
            StartupCheckpoint.Set("GameplayBootstrapRootActivated");
            yield return null;

            mobileInputRoot.SetActive(true);
            StartupCheckpoint.Set("GameplayMobileInputRootActivated");
            yield return null;

            worldBoundsRoot.SetActive(true);
            StartupCheckpoint.Set("GameplayWorldBoundsRootActivated");
            yield return null;

            prototypeFlowRoot.SetActive(true);
            StartupCheckpoint.Set("GameplayPrototypeFlowRootActivated");
            yield return null;

            StartupCheckpoint.Set("GameplayRootsActivationComplete");
        }

        private GameObject FindRootObject(Scene scene, string objectName)
        {
            GameObject[] roots = scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i] != null && roots[i].name == objectName)
                    return roots[i];
            }

            return null;
        }

        private IEnumerator ContinueGameplayInitialization()
        {
            Scene gameplayScene = SceneManager.GetSceneByPath(GameplayScenePath);
            if (!gameplayScene.IsValid() || !gameplayScene.isLoaded)
            {
                Fail("Prepared gameplay scene is no longer loaded.", null);
                yield break;
            }

            if (SceneManager.GetActiveScene() != gameplayScene)
            {
                if (!SceneManager.SetActiveScene(gameplayScene))
                {
                    Fail("Gameplay scene is loaded but could not become the active scene.", null);
                    yield break;
                }
            }

            StartupCheckpoint.Set("GameplaySceneActivated");

            float readyWait = 0f;
            while (!GameplayIsReady() && readyWait < ReadyTimeoutSeconds)
            {
                readyWait += Time.unscaledDeltaTime;
                status = GetStageStatus(StartupCheckpoint.Last);
                progressTarget = GetGameplayProgress(StartupCheckpoint.Last);
                progress = Mathf.MoveTowards(progress, progressTarget, Time.unscaledDeltaTime * 0.18f);
                yield return null;
            }

            if (!GameplayIsReady())
            {
                Fail("Battlefield initialization did not complete in time.", null);
                yield break;
            }

            float visibleElapsed = 0f;
            while (visibleElapsed < MinimumVisibleSeconds)
            {
                visibleElapsed += Time.unscaledDeltaTime;
                status = "Finalizing battlefield...";
                float timeProgress = Mathf.Clamp01(visibleElapsed / MinimumVisibleSeconds);
                progressTarget = Mathf.Lerp(0.92f, 0.985f, timeProgress);
                progress = Mathf.MoveTowards(progress, progressTarget, Time.unscaledDeltaTime * 0.18f);
                yield return null;
            }

            progressTarget = 1f;
            progress = 1f;
            status = "Battlefield ready";
            StartupCheckpoint.Set("GameplayReady");

            yield return null;

            Scene bootScene = gameObject.scene;
            if (bootScene.IsValid() && bootScene.isLoaded)
            {
                AsyncOperation unload = SceneManager.UnloadSceneAsync(bootScene);
                if (unload != null)
                    yield return unload;
            }
        }

        private bool GameplayIsReady()
        {
            return StartupCheckpoint.Last == "WorldBuildComplete"
                || StartupCheckpoint.Last == "RuntimeShaderReady"
                || StartupCheckpoint.Last == "GameplayReady";
        }

        private float GetGameplayProgress(string checkpoint)
        {
            switch (checkpoint)
            {
                case "GameplaySceneActivated":
                case "GameBootstrapAwake":
                    return 0.70f;
                case "GameSessionReady":
                case "CameraConfigured":
                case "LightingConfigured":
                case "PlayerLocated":
                    return 0.76f;
                case "PlayerAwakeStarted":
                case "PlayerVisualBuilt":
                case "PlayerComponentsReady":
                    return 0.82f;
                case "EnemySpawnerReady":
                case "HUDReady":
                case "GameplayBootstrapAwakeComplete":
                    return 0.87f;
                case "WorldBuildStarted":
                case "WorldBaseBuilt":
                    return 0.90f;
                case "RoadMarkingsBuilt":
                case "CityBlocksBuilt":
                    return 0.93f;
                case "LandmarksBuilt":
                case "StreetPropsBuilt":
                    return 0.96f;
                case "WorldBuildComplete":
                case "RuntimeShaderReady":
                case "GameplayReady":
                    return 1f;
                default:
                    return Mathf.Clamp(progress, 0.68f, 0.89f);
            }
        }

        private string GetStageStatus(string checkpoint)
        {
            switch (checkpoint)
            {
                case "GameSessionReady":
                    return "Preparing game session...";
                case "CameraConfigured":
                case "LightingConfigured":
                    return "Configuring battlefield...";
                case "PlayerAwakeStarted":
                case "PlayerVisualBuilt":
                case "PlayerComponentsReady":
                    return "Preparing player...";
                case "EnemySpawnerReady":
                    return "Preparing enemies...";
                case "HUDReady":
                    return "Preparing controls...";
                case "WorldBuildStarted":
                case "WorldBaseBuilt":
                    return "Building battlefield...";
                case "RoadMarkingsBuilt":
                case "CityBlocksBuilt":
                    return "Building city...";
                case "LandmarksBuilt":
                case "StreetPropsBuilt":
                    return "Finishing city...";
                case "WorldBuildComplete":
                case "RuntimeShaderReady":
                    return "Finalizing battlefield...";
                default:
                    return "Preparing battlefield...";
            }
        }

        private void Fail(string message, Exception exception)
        {
            failed = true;
            failureMessage = message;
            status = "Startup failed";
            progress = 0f;
            progressTarget = 0f;
            StartupCheckpoint.Set("BootFailure");

            if (exception != null)
                Debug.LogException(exception);
            else
                Debug.LogError("PERSIA_BOOT_STAGE: " + message);
        }

        private GUIStyle CreateTextStyle(int fontSize, FontStyle fontStyle)
        {
            GUIStyle style = new GUIStyle();
            style.fontSize = fontSize;
            style.fontStyle = fontStyle;
            style.alignment = TextAnchor.MiddleCenter;
            style.wordWrap = true;

            GUIStyleState state = new GUIStyleState();
            state.textColor = Color.white;
            style.normal = state;
            style.hover = state;
            style.active = state;
            style.focused = state;
            return style;
        }

        private void BuildStyles()
        {
            titleStyle = CreateTextStyle(42, FontStyle.Bold);
            statusStyle = CreateTextStyle(19, FontStyle.Normal);
            percentStyle = CreateTextStyle(17, FontStyle.Bold);
            footerStyle = CreateTextStyle(13, FontStyle.Normal);
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

            if (!startRequested && !failed && preparationComplete)
            {
                Rect startButton = new Rect(centerX + 90f, centerY + 5f, panelWidth - 180f, 58f);
                GUI.color = new Color(0.88f, 0.65f, 0.20f, 1f);
                if (GUI.Button(startButton, "START BATTLE", percentStyle))
                    BeginGame();
                GUI.color = Color.white;
                GUI.Label(new Rect(centerX, centerY + 75f, panelWidth, 30f), "The battlefield will load after Start.", footerStyle);
                GUI.color = old;
                return;
            }


            Rect bar = new Rect(centerX + 60f, centerY + 2f, panelWidth - 120f, 30f);
            GUI.color = new Color(0.12f, 0.15f, 0.19f, 1f);
            GUI.DrawTexture(bar, Texture2D.whiteTexture);

            float fill = Mathf.Clamp01(progress);
            if (fill < 0.015f && !failed)
            {
                float pulse = 0.18f + (Mathf.Sin(Time.unscaledTime * 4f) + 1f) * 0.09f;
                float x = bar.x + (bar.width - bar.width * pulse) * Mathf.Repeat(Time.unscaledTime * 0.16f, 1f);
                GUI.color = new Color(0.88f, 0.65f, 0.20f, 1f);
                GUI.DrawTexture(new Rect(x, bar.y, bar.width * pulse, bar.height), Texture2D.whiteTexture);
            }
            else
            {
                GUI.color = failed
                    ? new Color(0.70f, 0.18f, 0.14f, 1f)
                    : new Color(0.88f, 0.65f, 0.20f, 1f);
                GUI.DrawTexture(new Rect(bar.x, bar.y, bar.width * fill, bar.height), Texture2D.whiteTexture);
            }

            GUI.color = Color.white;
            GUI.Label(new Rect(centerX, centerY + 44f, panelWidth, 30f), Mathf.RoundToInt(progress * 100f) + "%", percentStyle);

            if (failed)
            {
                GUI.Label(new Rect(centerX + 35f, centerY + 77f, panelWidth - 70f, 48f), failureMessage, footerStyle);
            }
            else if (!string.IsNullOrEmpty(previousCheckpoint) && previousCheckpoint != "none" && previousCheckpoint != "GameplaySceneLoadCompleted")
            {
                GUI.Label(new Rect(centerX + 30f, centerY + 82f, panelWidth - 60f, 42f),
                    "Last startup checkpoint: " + previousCheckpoint,
                    footerStyle);
            }
            else
            {
                GUI.Label(new Rect(centerX, centerY + 84f, panelWidth, 30f), "Preparing the battlefield • Please wait.", footerStyle);
            }

            GUI.color = old;
        }
    }
}