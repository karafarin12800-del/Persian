using System.Collections;
using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    /// <summary>
    /// Front-end flow for the mobile prototype: hero selection -> drop map -> match.
    /// It is presentation/input gating only and leaves the existing combat systems intact.
    /// </summary>
    public sealed class PrototypeFlow : MonoBehaviour
    {
        private enum ScreenMode
        {
            HeroSelect,
            DropMap,
            Match
        }

        private ScreenMode mode = ScreenMode.HeroSelect;
        private PlayerController player;
        private EnemySpawner enemySpawner;
        private MobileInputHub mobileInput;
        private RuntimeCombatHUD combatHud;
        private CameraFollow25D followCamera;
        private Camera activeCamera;
        private Camera androidRuntimeCamera;
        private const float AndroidCameraPitch = 52f;
        private const float AndroidCameraYaw = 32f;
        private const float AndroidCameraDistance = 13.2f;
        private const float AndroidCameraLookHeight = 0.90f;
        private MobileMovementJoystick mobileMovementJoystick;
        private AndroidMinimapOverlay androidMinimap;
        private Vector2 spawnWorld = new Vector2(0f, -4f);
        private bool spawnChosen;
        private int selectedHero;
        private bool startingMatch;
        private bool matchInputArmed;
        private string startupStatus = string.Empty;
        private GameObject mainCameraRoot;
        private GameObject playerRoot;
        private GameObject mobileInputRoot;
        private GameObject worldBoundsRoot;
        private GameObject gameRoot;
        private GameBootstrap gameBootstrap;
        private Texture2D pixel;
        private Texture2D mapTexture;
        private GUIStyle titleStyle;
        private GUIStyle headerStyle;
        private GUIStyle bodyStyle;
        private GUIStyle buttonStyle;
        private GUIStyle smallStyle;

        private static readonly string[] HeroNames =
        {
            "KING ARDESHIR",
            "PARS GUARD",
            "ROYAL SCOUT",
            "SILK WARRIOR",
            "DESERT KNIGHT"
        };

        private void Awake()
        {
            // Keep scene activation as cheap as possible. Do not touch GUI skin/textures
            // or scan the scene during Awake: this method runs on the critical Unity
            // scene-activation path on Android.
            StartupCheckpoint.Set("PrototypeFlowAwake");
            Debug.Log("PERSIA_FLOW: PrototypeFlow Awake");
        }

        private void Start()
        {
            // Defer all menu initialization by one frame. This lets Unity finish
            // activating the gameplay scene and gives BootLoader a chance to complete
            // its LoadSceneAsync operation before any discovery/UI work runs.
            StartCoroutine(InitializeMenuAfterActivation());
        }

        private IEnumerator InitializeMenuAfterActivation()
        {
            yield return null;

            EnsureUiInitialized();
            CacheGameplayRoots();

            // Resolve the known dormant roots directly instead of performing several
            // global FindFirstObjectByType(..., Include) scans during scene entry.
            player = playerRoot != null
                ? playerRoot.GetComponent<PlayerController>()
                : null;
            mobileInput = mobileInputRoot != null
                ? mobileInputRoot.GetComponent<MobileInputHub>()
                : null;

            gameBootstrap = gameRoot != null
                ? gameRoot.GetComponent<GameBootstrap>()
                : null;

            enemySpawner = gameRoot != null
                ? gameRoot.GetComponentInChildren<EnemySpawner>(true)
                : null;
            combatHud = gameRoot != null
                ? gameRoot.GetComponentInChildren<RuntimeCombatHUD>(true)
                : null;

            // Do not start procedural world generation while the front-end menu is opening.
            // GameRoot remains dormant until START MATCH so Android can finish scene
            // activation and render the menu without a large main-thread workload.
            GateGameplay(false);
#if UNITY_ANDROID
            // Android production path: keep the real front-end interactive.
            // Do NOT auto-start the match. The previous isolation build skipped the
            // hero/drop-map screens and immediately activated the heaviest gameplay
            // systems, which made the app appear to "rush through" the choices.
            selectedHero = 0;
            spawnWorld = new Vector2(0f, -4f);
            spawnChosen = false;
            mode = ScreenMode.HeroSelect;
            StartupCheckpoint.Set("PrototypeFlowMenuReady");
            Debug.Log("PERSIA_FLOW: Android menu ready; waiting for player selection");
            yield break;
#else
            StartupCheckpoint.Set("PrototypeFlowMenuReady");
            Debug.Log("PERSIA_FLOW: PrototypeFlow menu initialized");
#endif
        }

        private void CacheGameplayRoots()
        {
            GameObject[] roots = gameObject.scene.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                GameObject root = roots[i];
                if (root == null) continue;
                if (root.name == "Main Camera") mainCameraRoot = root;
                else if (root.name == "Player") playerRoot = root;
                else if (root.name == "MobileInput") mobileInputRoot = root;
                else if (root.name == "WorldBounds") worldBoundsRoot = root;
                else if (root.name == "GameRoot") gameRoot = root;
            }
        }

        private IEnumerator BeginMainGameAfterMenuFrame()
        {
            // One clean frame after scene activation keeps the transition deterministic
            // without showing the old green tactical-map screen on Android.
            yield return null;
            if (this == null || !isActiveAndEnabled)
                yield break;

            mode = ScreenMode.DropMap;
            StartMatch();
        }

        private void Update()
        {
            // The spawn map is part of the front-end and must remain interactive
            // even while the gameplay roots are still dormant.
            if (mode == ScreenMode.Match) return;
            if (mode == ScreenMode.DropMap)
                HandleDropTouches();
        }

        private void OnGUI()
        {
            EnsureUiInitialized();
            if (mode == ScreenMode.Match)
            {
#if UNITY_ANDROID
                if (GameSession.Instance != null && GameSession.Instance.IsFinished)
                    DrawAndroidResultOverlay();
#endif
                return;
            }
            DrawBackdrop();

            if (startingMatch)
            {
                if (gameBootstrap != null && gameBootstrap.IsWorldPreparing && !string.IsNullOrEmpty(gameBootstrap.CurrentStage))
                    startupStatus = gameBootstrap.CurrentStage;

                float width = Mathf.Min(Screen.width - 48f, 760f);
                float height = 170f;
                Rect panel = new Rect(
                    (Screen.width - width) * 0.5f,
                    Screen.height * 0.5f - height * 0.5f,
                    width,
                    height);
                Fill(panel, new Color(0.04f, 0.07f, 0.11f, 0.96f));
                Fill(new Rect(panel.x, panel.y, panel.width, 6f), new Color(0.92f, 0.66f, 0.18f, 1f));
                GUI.Label(new Rect(panel.x + 18f, panel.y + 30f, panel.width - 36f, 40f), "PERSIA WAR", headerStyle);
                GUI.Label(new Rect(panel.x + 18f, panel.y + 76f, panel.width - 36f, 30f), startupStatus, bodyStyle);
                GUI.Label(new Rect(panel.x + 18f, panel.y + 116f, panel.width - 36f, 28f), "Preparing battlefield...", smallStyle);
                return;
            }
            if (mode == ScreenMode.HeroSelect)
            {
                DrawPreviousRunDiagnostic();
                DrawHeroSelect();
            }
            else
                DrawDropMap();
        }

        private void DrawPreviousRunDiagnostic()
        {
#if UNITY_ANDROID
            string previous = StartupCheckpoint.Previous;
            if (string.IsNullOrEmpty(previous) || previous == "none")
                return;

            float width = Mathf.Min(Screen.width - 32f, 760f);
            float height = 118f;
            Rect panel = new Rect((Screen.width - width) * 0.5f, 12f, width, height);
            Fill(panel, new Color(0.03f, 0.045f, 0.07f, 0.94f));
            Fill(new Rect(panel.x, panel.y, panel.width, 4f), new Color(1f, 0.55f, 0.12f, 1f));

            GUI.Label(new Rect(panel.x + 12f, panel.y + 8f, panel.width - 24f, 22f),
                "PREVIOUS RUN DIAGNOSTIC", smallStyle);
            GUI.Label(new Rect(panel.x + 12f, panel.y + 34f, panel.width - 24f, 24f),
                "Checkpoint: " + previous, smallStyle);
            GUI.Label(new Rect(panel.x + 12f, panel.y + 60f, panel.width - 24f, 24f),
                "Exit: " + StartupCheckpoint.PreviousExitState, smallStyle);
            GUI.Label(new Rect(panel.x + 12f, panel.y + 86f, panel.width - 24f, 24f),
                "Assessment: " + StartupCheckpoint.PreviousAssessment, smallStyle);
#endif
        }

        private void DrawBackdrop()
        {
            Color old = GUI.color;
            GUI.color = new Color(0.035f, 0.08f, 0.13f, 0.96f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), pixel);
            GUI.color = old;

            Rect topBar = new Rect(0f, 0f, Screen.width, Mathf.Min(110f, Screen.height * 0.16f));
            Fill(topBar, new Color(0.02f, 0.035f, 0.06f, 0.84f));
            GUI.Label(new Rect(28f, 18f, Screen.width * 0.55f, 48f), "PERSIA WAR", titleStyle);
            GUI.Label(new Rect(30f, 62f, Screen.width * 0.60f, 30f), "CLASSIC BATTLE ROYALE  •  PROTOTYPE", smallStyle);
        }

        private void DrawHeroSelect()
        {
            GUI.Label(new Rect(0f, 125f, Screen.width, 52f), "CHOOSE YOUR HERO", headerStyle);
            GUI.Label(new Rect(0f, 174f, Screen.width, 34f), "SHORT • CHIBI • PERSIAN-INSPIRED", smallStyle);

            float gap = 14f;
            float totalWidth = Mathf.Min(Screen.width - 44f, 980f);
            float cardWidth = (totalWidth - gap * 4f) / 5f;
            float startX = (Screen.width - totalWidth) * 0.5f;
            float top = 230f;
            float cardHeight = Mathf.Min(310f, Screen.height - 360f);

            for (int i = 0; i < HeroNames.Length; i++)
            {
                Rect card = new Rect(startX + i * (cardWidth + gap), top, cardWidth, cardHeight);
                DrawHeroCard(card, i, i == selectedHero);
                if (GUI.Button(card, GUIContent.none, GUIStyle.none))
                {
                    selectedHero = i;
                }
            }

            Rect continueRect = new Rect(Screen.width * 0.5f - 180f, Screen.height - 112f, 360f, 62f);
            if (GUI.Button(continueRect, "SELECT SPAWN POINT", buttonStyle))
                mode = ScreenMode.DropMap;
        }

        private void DrawHeroCard(Rect rect, int index, bool selected)
        {
            Color panel = selected ? new Color(0.22f, 0.46f, 0.68f, 0.98f) : new Color(0.08f, 0.12f, 0.17f, 0.94f);
            Fill(rect, panel);
            Fill(new Rect(rect.x, rect.y, rect.width, 8f), selected ? new Color(0.95f, 0.68f, 0.18f) : new Color(0.20f, 0.28f, 0.36f));

            Rect figure = new Rect(rect.x + rect.width * 0.18f, rect.y + 34f, rect.width * 0.64f, rect.height * 0.52f);
            DrawChibiFigure(figure, index);

            GUI.Label(new Rect(rect.x + 8f, rect.yMax - 78f, rect.width - 16f, 30f), HeroNames[index], smallStyle);
            GUI.Label(new Rect(rect.x + 8f, rect.yMax - 48f, rect.width - 16f, 30f), selected ? "SELECTED" : "TAP TO SELECT", smallStyle);
        }

        private void DrawChibiFigure(Rect rect, int index)
        {
            Color[] bodyColors =
            {
                new Color(0.45f, 0.17f, 0.10f),
                new Color(0.08f, 0.37f, 0.48f),
                new Color(0.16f, 0.39f, 0.24f),
                new Color(0.35f, 0.15f, 0.42f),
                new Color(0.39f, 0.28f, 0.08f)
            };

            Vector2 center = new Vector2(rect.center.x, rect.y + rect.height * 0.55f);
            float head = rect.width * 0.33f;
            float body = rect.width * 0.44f;
            DrawCircle(center + new Vector2(0f, -head * 0.95f), head * 0.78f, new Color(0.76f, 0.53f, 0.34f, 1f));
            Fill(new Rect(center.x - body * 0.5f, center.y - body * 0.05f, body, body * 0.8f), bodyColors[index]);
            Fill(new Rect(center.x - body * 0.46f, center.y + body * 0.67f, body * 0.34f, body * 0.55f), new Color(0.07f, 0.09f, 0.12f));
            Fill(new Rect(center.x + body * 0.12f, center.y + body * 0.67f, body * 0.34f, body * 0.55f), new Color(0.07f, 0.09f, 0.12f));

            if (index == 0)
            {
                Fill(new Rect(center.x - head * 0.9f, center.y - head * 1.62f, head * 1.8f, head * 0.32f), new Color(0.92f, 0.66f, 0.14f));
                Fill(new Rect(center.x - 4f, center.y - head * 1.86f, 8f, head * 0.40f), new Color(0.92f, 0.66f, 0.14f));
                Fill(new Rect(center.x - head * 0.44f, center.y - head * 0.28f, head * 0.88f, head * 0.47f), new Color(0.08f, 0.055f, 0.04f));
            }
            else if (index == 3)
            {
                Fill(new Rect(center.x - head * 0.82f, center.y - head * 1.45f, head * 1.64f, head * 0.18f), new Color(0.90f, 0.22f, 0.12f));
            }
        }
        private void DrawDropMap()
        {
            GUI.Label(new Rect(0f, 125f, Screen.width, 52f), "DROP INTO THE CITY", headerStyle);
            GUI.Label(new Rect(0f, 174f, Screen.width, 34f), "TAP ANY OPEN LOCATION TO CHOOSE WHERE YOU START", smallStyle);

            float size = Mathf.Min(Screen.width - 70f, Screen.height - 330f);
            Rect mapRect = new Rect((Screen.width - size) * 0.5f, 220f, size, size);
            DrawTacticalMap(mapRect);

            // IMGUI receives Android touch events reliably even when the gameplay
            // camera/player roots are inactive. Use the map itself as a touch target
            // in addition to the Update() touch path.
            GUIStyle mapTouchStyle = GUIStyle.none;
            if (GUI.Button(mapRect, GUIContent.none, mapTouchStyle))
            {
                Vector2 p = Event.current.mousePosition;
                float u = Mathf.Clamp01((p.x - mapRect.x) / mapRect.width);
                float v = Mathf.Clamp01((p.y - mapRect.y) / mapRect.height);
                spawnWorld = new Vector2(
                    Mathf.Lerp(-96f, 96f, u),
                    Mathf.Lerp(96f, -96f, v));
                spawnChosen = true;
                Debug.Log("PERSIA_FLOW: Spawn selected " + spawnWorld);
            }

            if (spawnChosen)
            {
                Vector2 point = WorldToMap(spawnWorld, mapRect);
                DrawCircle(point, 16f, new Color(1f, 0.82f, 0.18f, 0.95f));
                GUI.Label(new Rect(point.x - 65f, point.y + 18f, 130f, 28f), "YOU START HERE", smallStyle);
            }

            Rect hint = new Rect(24f, Screen.height - 100f, Screen.width - 48f, 34f);
            GUI.Label(hint, spawnChosen ? "Spawn point locked. Press START MATCH." : "Tip: avoid the ruined quarter for the safest start.", smallStyle);

            Rect start = new Rect(Screen.width * 0.5f - 180f, Screen.height - 64f, 360f, 52f);
            GUI.enabled = spawnChosen;
            if (GUI.Button(start, "START MATCH", buttonStyle))
                StartMatch();
            GUI.enabled = true;
        }

        private void DrawTacticalMap(Rect rect)
        {
            Fill(rect, new Color(0.39f, 0.66f, 0.27f, 1f));
            float block = rect.width / 6f;

            for (int i = 1; i < 6; i++)
            {
                float road = rect.x + i * block;
                Fill(new Rect(road - 13f, rect.y, 26f, rect.height), new Color(0.16f, 0.18f, 0.19f));
                Fill(new Rect(rect.x, rect.y + i * block - 13f, rect.width, 26f), new Color(0.16f, 0.18f, 0.19f));
            }

            for (int gx = 0; gx < 6; gx++)
            {
                for (int gy = 0; gy < 6; gy++)
                {
                    Rect cell = new Rect(rect.x + gx * block + 7f, rect.y + gy * block + 7f, block - 14f, block - 14f);
                    Color c = new Color(0.50f, 0.72f, 0.31f, 1f);
                    if (gx >= 4 && gy <= 2) c = new Color(0.48f, 0.44f, 0.39f, 1f);
                    if (gx == 2 && gy == 3) c = new Color(0.30f, 0.50f, 0.66f, 1f);
                    Fill(cell, c);
                    if (gx != 5 && gy != 5)
                        Fill(new Rect(cell.x + 10f, cell.y + 10f, cell.width * 0.42f, cell.height * 0.34f), new Color(0.86f, 0.69f, 0.30f, 1f));
                }
            }

            GUI.Label(new Rect(rect.x + 12f, rect.y + 10f, 190f, 30f), "PERSIA WAR • LEVEL 1", smallStyle);
            GUI.Label(new Rect(rect.x + rect.width - 160f, rect.y + 10f, 145f, 30f), "RUINED QUARTER", smallStyle);
        }

        private void HandleDropTouches()
        {
            if (!Application.isMobilePlatform && !Input.GetMouseButtonDown(0)) return;

            Vector2 screen;
            if (Application.isMobilePlatform)
            {
                if (Input.touchCount == 0 || Input.GetTouch(0).phase != TouchPhase.Began) return;
                screen = Input.GetTouch(0).position;
            }
            else
            {
                screen = Input.mousePosition;
            }

            screen.y = Screen.height - screen.y;
            float size = Mathf.Min(Screen.width - 70f, Screen.height - 330f);
            Rect mapRect = new Rect((Screen.width - size) * 0.5f, 220f, size, size);
            if (!mapRect.Contains(screen)) return;

            Vector2 uv = new Vector2(
                Mathf.Clamp01((screen.x - mapRect.x) / mapRect.width),
                Mathf.Clamp01((screen.y - mapRect.y) / mapRect.height));

            float x = Mathf.Lerp(-96f, 96f, uv.x);
            float z = Mathf.Lerp(96f, -96f, uv.y);
            spawnWorld = new Vector2(x, z);
            spawnChosen = true;
        }

        private void StartMatch()
        {
            if (!spawnChosen || startingMatch)
                return;

            StartCoroutine(BeginMatchSafely());
        }

        private IEnumerator BeginMatchSafely()
        {
            startingMatch = true;
            matchInputArmed = false;
            startupStatus = "Stage 1: starting battlefield preparation...";

            StartupCheckpoint.Set("MatchActivationStarted");

            // GameBootstrap lives under the dormant GameRoot. A disabled/inactive
            // MonoBehaviour cannot own a running coroutine, so wake the root first,
            // then explicitly re-gate gameplay components before building the world.
            if (gameRoot != null && !gameRoot.activeSelf)
            {
                startupStatus = "Stage 2: activating battlefield systems...";
                gameRoot.SetActive(true);
                yield return null;

                EnsureRuntimeGameplayServices();
                CacheGameplayRoots();
                gameBootstrap = gameRoot.GetComponent<GameBootstrap>();
                player = playerRoot != null ? playerRoot.GetComponent<PlayerController>() : player;
                mobileInput = mobileInputRoot != null ? mobileInputRoot.GetComponent<MobileInputHub>() : mobileInput;
                enemySpawner = gameRoot.GetComponentInChildren<EnemySpawner>(true);
                combatHud = gameRoot.GetComponentInChildren<RuntimeCombatHUD>(true);
                GateGameplay(false);
            }

            if (gameBootstrap == null)
                gameBootstrap = gameRoot != null ? gameRoot.GetComponent<GameBootstrap>() : FindFirstObjectByType<GameBootstrap>(FindObjectsInactive.Include);
            if (gameBootstrap == null)
            {
                startupStatus = "Battlefield bootstrap is missing.";
                StartupCheckpoint.Set("GameBootstrapMissing");
                startingMatch = false;
                yield break;
            }

            if (!gameBootstrap.IsWorldReady && !gameBootstrap.IsWorldPreparing && !gameBootstrap.WorldBuildFailed)
            {
                startupStatus = "Stage 3: requesting battlefield build...";
                gameBootstrap.PrepareWorld();
            }

            while (gameBootstrap.IsWorldPreparing)
            {
                startupStatus = gameBootstrap.CurrentStage;
                yield return null;
            }

            if (gameBootstrap.WorldBuildFailed || !gameBootstrap.IsWorldReady)
            {
                startupStatus = "Battlefield preparation failed: " + gameBootstrap.WorldBuildError;
                StartupCheckpoint.Set("MatchActivationFailed");
                startingMatch = false;
                yield break;
            }

            startupStatus = "Stage 8: activating player...";
            yield return ActivateRoot(playerRoot, "Player");

            player = playerRoot != null
                ? playerRoot.GetComponent<PlayerController>()
                : player;

            if (player == null)
            {
                startupStatus = "Player initialization failed.";
                StartupCheckpoint.Set("PlayerInitializationFailed");
                startingMatch = false;
                yield break;
            }

            // GateGameplay(false) previously disabled PlayerController before Start could
            // build its combat dependencies. That created a lifecycle race at match entry.
            // Explicitly prepare the player, then wait until its full dependency graph exists.
            player.enabled = true;
            player.PrepareForMatch(selectedHero);
            for (int frame = 0; frame < 30 && !player.IsGameplayReady; frame++)
                yield return null;

            if (!player.IsGameplayReady)
            {
                startupStatus = "Player systems did not finish initializing.";
                StartupCheckpoint.Set("PlayerInitializationFailed");
                startingMatch = false;
                yield break;
            }

            StartupCheckpoint.Set("PlayerPreparedForMatch");

            StartupCheckpoint.Set("MobileInputActivationDeferred");
            if (combatHud != null)
                combatHud.ConfigurePlayer(player);

            startupStatus = "Stage 9: deferring mobile input...";
            yield return null;

            startupStatus = "Stage 10: activating world bounds...";
            yield return ActivateRoot(worldBoundsRoot, "WorldBounds");
            startupStatus = "Stage 11: preparing main camera...";

            // Set the player position before touching any camera lifecycle. On Android,
            // the scene camera root is intentionally left dormant: activating that
            // serialized GameObject can invoke camera-side OnEnable callbacks during
            // the same critical frame as match startup.
            player.SetGroundedPosition(new Vector3(spawnWorld.x, 0f, spawnWorld.y));
            player.LockMovementFor(0.50f);
            ApplyHeroStyle(selectedHero);

#if UNITY_ANDROID
            // Use a completely isolated runtime camera for Android. It has no
            // CameraFollow component and therefore cannot trigger the previous
            // target/lifecycle race when the scene camera is activated.
            androidRuntimeCamera = CreateAndroidRuntimeCamera(player.transform);
            activeCamera = androidRuntimeCamera;
            followCamera = null;

            if (mobileInput != null)
                mobileInput.SetGameplayCamera(activeCamera);
#else
            activeCamera = mainCameraRoot != null
                ? mainCameraRoot.GetComponent<Camera>()
                : null;
            followCamera = activeCamera != null
                ? activeCamera.GetComponent<CameraFollow25D>()
                : null;

            if (activeCamera != null)
                activeCamera.enabled = false;
            if (followCamera != null)
                followCamera.enabled = false;

            yield return ActivateRoot(mainCameraRoot, "Main Camera");

            if (followCamera != null)
                followCamera.SetTarget(player.transform);
            if (followCamera != null)
                followCamera.enabled = true;
            if (activeCamera != null)
                activeCamera.enabled = true;
#endif

            if (activeCamera != null)
                activeCamera.enabled = false;
            if (followCamera != null)
                followCamera.enabled = false;

            // Android diagnostic gate: keep every optional subsystem disabled while the
            // first post-match frames are proven stable. We then enable camera, input,
            // enemies and HUD one at a time so the exact crashing subsystem is isolated.
            GateGameplay(false);
            if (enemySpawner != null) enemySpawner.enabled = false;            if (combatHud != null) combatHud.enabled = false;
            if (mobileInput != null) mobileInput.enabled = false;
            if (activeCamera != null) activeCamera.enabled = false;
            if (followCamera != null) followCamera.enabled = false;

            startupStatus = "Stage 12: core stable — Android isolation test.";
            StartupCheckpoint.Set("MatchCoreReady");
            mode = ScreenMode.Match;
            startingMatch = false;
#if UNITY_ANDROID
            startupStatus = "STABLE TEST 1/4: core only";
            StartCoroutine(InitializeMatchServices());
#else
            startupStatus = string.Empty;
            StartupCheckpoint.Set("MatchStarted");
            StartCoroutine(InitializeMatchServices());
#endif
        }

        private Camera CreateAndroidRuntimeCamera(Transform target)
        {
            GameObject cameraObject = new GameObject("AndroidGameplayCamera");
            Camera camera = cameraObject.AddComponent<Camera>();
            camera.tag = "MainCamera";
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.08f, 0.09f, 0.10f, 1f);
            camera.fieldOfView = 50f;
            camera.nearClipPlane = 0.1f;
            camera.farClipPlane = 240f;
            camera.allowHDR = false;
            camera.allowMSAA = false;

            if (target != null)
            {
                Quaternion orbit = Quaternion.Euler(AndroidCameraPitch, AndroidCameraYaw, 0f);
                cameraObject.transform.position = target.position + orbit * Vector3.back * AndroidCameraDistance;
                cameraObject.transform.rotation = Quaternion.LookRotation(
                    (target.position + Vector3.up * AndroidCameraLookHeight) - cameraObject.transform.position,
                    Vector3.up);
            }

            camera.enabled = false;
            return camera;
        }

        private void LateUpdate()
        {
#if UNITY_ANDROID
            if (androidRuntimeCamera == null || player == null)
                return;

            Quaternion orbit = Quaternion.Euler(AndroidCameraPitch, AndroidCameraYaw, 0f);
            Vector3 desired = player.transform.position + orbit * Vector3.back * AndroidCameraDistance;
            androidRuntimeCamera.transform.position = Vector3.Lerp(
                androidRuntimeCamera.transform.position,
                desired,
                1f - Mathf.Exp(-12f * Time.deltaTime));

            Vector3 lookTarget = player.transform.position + Vector3.up * AndroidCameraLookHeight;
            Vector3 direction = lookTarget - androidRuntimeCamera.transform.position;
            if (direction.sqrMagnitude > 0.001f)
                androidRuntimeCamera.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
#endif
        }

        private IEnumerator EnableCameraAfterSafeFrames(Camera activeCamera, CameraFollow25D followCamera)
        {
#if UNITY_ANDROID
            // Deliberately hold rendering off for a long clean interval. If the process
            // exits before this point, the camera/render hand-off is not the cause.
            for (int i = 0; i < 120; i++)
                yield return null;

            if (this == null || !isActiveAndEnabled)
                yield break;

            if (followCamera != null)
            {
                followCamera.SetTarget(player != null ? player.transform : null);
                followCamera.enabled = true;
            }

            yield return null;

            if (activeCamera != null)
                activeCamera.enabled = true;

            StartupCheckpoint.Set("AndroidCameraActivatedAfterIsolation");
#else
            if (followCamera != null)
                followCamera.enabled = true;
            if (activeCamera != null)
                activeCamera.enabled = true;
#endif
        }


        private IEnumerator InitializeMatchServices()
        {
#if UNITY_ANDROID
            // DIAGNOSTIC PASS: after MatchCoreReady, enable exactly one subsystem at a time.
            // This build enables ONLY the Android camera, then waits for a long clean render
            // window. If it crashes, the last checkpoint tells us the camera/render boundary.
            Camera activeCamera = androidRuntimeCamera != null
                ? androidRuntimeCamera
                : (mainCameraRoot != null ? mainCameraRoot.GetComponent<Camera>() : null);
            CameraFollow25D follow = androidRuntimeCamera != null
                ? null
                : (activeCamera != null ? activeCamera.GetComponent<CameraFollow25D>() : null);

            StartupCheckpoint.Set("AndroidPostCoreEntered");
            StartupCheckpoint.Set("AndroidCameraActivationStarted");

            if (follow != null)
            {
                StartupCheckpoint.Set("AndroidCameraFollowAboutToEnable");
                follow.SetTarget(player != null ? player.transform : null);
                follow.enabled = true;
                StartupCheckpoint.Set("AndroidCameraFollowEnabled");
            }

            yield return null;
            StartupCheckpoint.Set("AndroidCameraEnableAboutToHappen");

            if (activeCamera != null)
                activeCamera.enabled = true;

            StartupCheckpoint.Set("AndroidCameraEnabled");

            if (player != null)
                player.LockMovementFor(0.20f);

            // 120 frames gives the render thread plenty of time to hit a native
            // graphics/mesh/material problem while all other gameplay systems remain off.
            for (int i = 0; i < 120; i++)
                yield return null;

            StartupCheckpoint.Set("AndroidCameraStable120Frames");
            startupStatus = "CAMERA STABLE — INPUT/HUD/ENEMIES DISABLED";
            yield break;
#else
            yield return null;

            matchInputArmed = true;
            if (mobileInput != null)
            {
                mobileInput.EnableMinimap();
                mobileInput.enabled = true;
            }

            if (player == null) yield break;

            if (enemySpawner == null)
                enemySpawner = FindFirstObjectByType<EnemySpawner>(FindObjectsInactive.Include);

            if (enemySpawner != null)
            {
                enemySpawner.Configure(player.transform, 8, 44f, 0f);
                enemySpawner.enabled = true;
            }

            if (combatHud == null)
                combatHud = FindFirstObjectByType<RuntimeCombatHUD>(FindObjectsInactive.Include);

            if (combatHud != null)
            {
                combatHud.ConfigurePlayer(player);
                combatHud.enabled = true;
            }

            StartupCheckpoint.Set("MatchServicesReady");
#endif
        }

        private void EnsureRuntimeGameplayServices()
        {
            if (gameRoot == null)
                return;

            if (enemySpawner == null)
            {
                enemySpawner = gameRoot.GetComponentInChildren<EnemySpawner>(true);
                if (enemySpawner == null)
                    enemySpawner = gameRoot.AddComponent<EnemySpawner>();

                enemySpawner.enabled = false;
            }

            if (combatHud == null)
            {
                combatHud = gameRoot.GetComponentInChildren<RuntimeCombatHUD>(true);
                if (combatHud == null)
                    combatHud = gameRoot.AddComponent<RuntimeCombatHUD>();

                combatHud.enabled = false;
            }

            if (combatHud != null && player != null)
                combatHud.ConfigurePlayer(player);
        }

        private IEnumerator ActivateRoot(GameObject root, string rootName)
        {
            if (root == null)
                yield break;

            if (!root.activeSelf)
            {
                startupStatus = "Starting " + rootName + "...";
                root.SetActive(true);
                yield return null;
            }
        }

        private void GateGameplay(bool enabled)
        {
            if (player != null) player.enabled = enabled;
#if !UNITY_ANDROID
            if (mobileInput != null) mobileInput.enabled = enabled && (matchInputArmed || !Application.isMobilePlatform);
#endif
            if (combatHud != null) combatHud.enabled = enabled;
            if (enemySpawner != null) enemySpawner.enabled = enabled;
            if (!enabled)
                matchInputArmed = false;
        }

        private void ApplyHeroStyle(int heroIndex)
        {
            if (player == null) return;

            Color body = heroIndex == 0 ? new Color(0.48f, 0.16f, 0.08f)
                : heroIndex == 1 ? new Color(0.07f, 0.34f, 0.44f)
                : heroIndex == 2 ? new Color(0.12f, 0.36f, 0.20f)
                : heroIndex == 3 ? new Color(0.34f, 0.12f, 0.40f)
                : new Color(0.40f, 0.28f, 0.08f);
            Color accent = heroIndex == 0 ? new Color(0.91f, 0.65f, 0.10f)
                : heroIndex == 1 ? new Color(0.78f, 0.86f, 0.88f)
                : heroIndex == 2 ? new Color(0.70f, 0.84f, 0.20f)
                : new Color(0.92f, 0.28f, 0.22f);

            StylizedCharacterVisual visual = player.GetComponentInChildren<StylizedCharacterVisual>(true);
            if (visual != null)
            {
                visual.ConfigurePlayerHero(heroIndex);
                return;
            }
        }

        private Vector2 WorldToMap(Vector2 world, Rect rect)
        {
            float x = Mathf.InverseLerp(-96f, 96f, world.x);
            float y = Mathf.InverseLerp(96f, -96f, world.y);
            return new Vector2(rect.x + x * rect.width, rect.y + y * rect.height);
        }

        private void DrawAndroidMatchHud()
        {
            EnsureUiInitialized();

            float scale = Mathf.Clamp(Mathf.Min(Screen.width, Screen.height) / 1080f, 0.75f, 1.35f);

            Rect card = new Rect(18f * scale, 18f * scale, 315f * scale, 92f * scale);
            Fill(card, new Color(0.025f, 0.055f, 0.085f, 0.88f));
            Fill(new Rect(card.x, card.y, 5f * scale, card.height), new Color(0.92f, 0.66f, 0.18f, 1f));

            GUI.Label(
                new Rect(card.x + 18f * scale, card.y + 8f * scale, card.width - 26f * scale, 28f * scale),
                "PERSIA WAR  •  BATTLEFIELD 01",
                smallStyle);

            int hp = player != null && player.Health != null ? player.Health.CurrentHealth : 100;
            int maxHp = player != null && player.Health != null ? player.Health.MaxHealth : 100;
            float hp01 = maxHp > 0 ? Mathf.Clamp01(hp / (float)maxHp) : 0f;

            Rect hpBack = new Rect(card.x + 18f * scale, card.y + 47f * scale, 205f * scale, 14f * scale);
            Fill(hpBack, new Color(0.10f, 0.12f, 0.14f, 1f));
            Fill(new Rect(hpBack.x, hpBack.y, hpBack.width * hp01, hpBack.height),
                new Color(0.18f, 0.72f, 0.32f, 1f));
            GUI.Label(
                new Rect(hpBack.x + hpBack.width + 10f * scale, hpBack.y - 5f * scale, 72f * scale, 24f * scale),
                hp + " / " + maxHp,
                smallStyle);

            GUI.Label(
                new Rect(card.x + 18f * scale, card.y + 67f * scale, card.width - 25f * scale, 22f * scale),
                "3D CITY  •  MOVE  •  MINIMAP",
                smallStyle);
        }

        private void DrawAndroidResultOverlay()
        {
            EnsureUiInitialized();

            GameSession session = GameSession.Instance;
            if (session == null || !session.IsFinished)
                return;

            float width = Mathf.Min(Screen.width - 64f, 720f);
            float height = Mathf.Min(Screen.height - 80f, 340f);
            Rect panel = new Rect(
                (Screen.width - width) * 0.5f,
                (Screen.height - height) * 0.5f,
                width,
                height);

            Fill(panel, new Color(0.025f, 0.05f, 0.08f, 0.97f));
            Fill(new Rect(panel.x, panel.y, panel.width, 6f),
                session.PlayerWon
                    ? new Color(0.22f, 0.78f, 0.34f, 1f)
                    : new Color(0.84f, 0.24f, 0.20f, 1f));

            GUI.Label(
                new Rect(panel.x + 20f, panel.y + 36f, panel.width - 40f, 60f),
                session.PlayerWon ? "VICTORY" : "GAME OVER",
                headerStyle);

            GUI.Label(
                new Rect(panel.x + 20f, panel.y + 106f, panel.width - 40f, 34f),
                session.PlayerWon
                    ? "All five enemy waves defeated."
                    : "Your fighter was defeated.",
                bodyStyle);

            GUI.Label(
                new Rect(panel.x + 20f, panel.y + 146f, panel.width - 40f, 28f),
                "SCORE  " + session.Score + "    ENEMIES  " + session.EnemiesDefeated,
                smallStyle);

            if (GUI.Button(
                new Rect(panel.x + 80f, panel.y + panel.height - 82f, panel.width - 160f, 54f),
                "PLAY AGAIN",
                buttonStyle))
            {
                session.RestartMission();
            }
        }

        private void DrawAndroidStabilityStatus()
        {
            // This is diagnostics only. The old implementation painted a full-screen
            // opaque panel over the actual match, making a healthy match look frozen
            // on "MATCH STABLE". Keep a compact status banner while isolation is active,
            // then remove the banner completely once the gameplay stack is ready.
            if (string.Equals(startupStatus, "MATCH STABLE", System.StringComparison.Ordinal))
                return;

            EnsureUiInitialized();

            float width = Mathf.Min(Screen.width - 32f, 720f);
            float height = Mathf.Min(86f, Screen.height * 0.12f);
            Rect panel = new Rect(
                (Screen.width - width) * 0.5f,
                14f,
                width,
                height);

            Fill(panel, new Color(0.035f, 0.08f, 0.13f, 0.88f));
            Fill(new Rect(panel.x, panel.y, panel.width, 4f), new Color(0.92f, 0.66f, 0.18f, 1f));

            GUI.Label(
                new Rect(panel.x + 14f, panel.y + 8f, panel.width - 28f, 28f),
                "PERSIA WAR  •  ANDROID ISOLATION",
                smallStyle);

            GUI.Label(
                new Rect(panel.x + 14f, panel.y + 36f, panel.width - 28f, 30f),
                startupStatus,
                bodyStyle);
        }

        private void EnsureUiInitialized()
        {
            if (pixel == null)
                CreateTextures();
            if (titleStyle == null)
                BuildStyles();
        }

        private void CreateTextures()
        {
            pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            pixel.SetPixel(0, 0, Color.white);
            pixel.Apply();
            mapTexture = pixel;
        }

        private void BuildStyles()
        {
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            headerStyle = new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
            smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        }

        private void Fill(Rect rect, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, pixel);
            GUI.color = old;
        }

        private void DrawCircle(Vector2 center, float radius, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;            GUI.DrawTexture(new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f), Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}