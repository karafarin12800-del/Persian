using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

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
            MainMenu,
            Profile,
            Store,
            Settings,
            HeroSelect,
            WeaponSelect,
            DropMap,
            Match
        }

        private ScreenMode mode = ScreenMode.MainMenu;
        private string profileNameInput = string.Empty;
        private string playerId = string.Empty;
        private int storeCoins;
        private int selectedProfileCosmetic;
        private int selectedStoreWeaponKind = (int)WeaponController.WeaponKind.AssaultRifle;
        private bool soundEnabled = true;
        private static readonly string[] StoreCosmeticNames =
        {
            "ROYAL PROFILE FRAME",
            "GOLD NAMEPLATE",
            "AZURE BANNER",
            "VOID RIFLE",
            "ALIEN SWORD"
        };
        private static readonly int[] StoreCosmeticPrices = { 0, 250, 350, 450, 400 };
        private PlayerController player;
        private EnemySpawner enemySpawner;
        private MobileInputHub mobileInput;
        private RuntimeCombatHUD combatHud;
        private CameraFollow25D followCamera;
        private Camera activeCamera;
        private Camera androidRuntimeCamera;
        private const float AndroidCameraPitch = 37f;
        private const float AndroidCameraYaw = 32f;
        private const float AndroidCameraDistance = 16.63f;
        private const float AndroidCameraLookHeight = 0.90f;
        private const float AndroidCameraLookAhead = 4.5f;
        private EnemyChase[] minimapEnemies = new EnemyChase[0];
        private PickupItem[] minimapPickups = new PickupItem[0];
        private float nextMinimapRefresh;
        private Vector2 spawnWorld = new Vector2(0f, -4f);
        private bool spawnChosen;
        private int selectedHero;
        private int stageTwoOriginalHero;
        private bool stageTwoShopOpen;
        private float girlRescueNoticeUntil;
        private int stageNumber = 1;
        // 0 = random landing, 1 = manual map landing.
        private int stageTwoLandingMode;
        // 0 = village cluster, 1 = mirrored village, 2 = rotated village.
        private int stageTwoLayoutVariant;
        private bool startingMatch;
        private bool stageTwoLobbyRunning;
        private bool stageTwoLobbyReady;
        private int stageTwoLobbyPlayers = 1;
        private float stageTwoLobbyStartedAt;
        private bool matchInputArmed;
        private bool matchPaused;
        private bool stageThreeResultAcknowledged;
        private float resultOverlayStartTime = -1f;
        private StageTwoVictorySequence stageTwoVictorySequence;
        private StageThreeWorldBuilder stageThreeWorldBuilder;
        private StageFourWorldBuilder stageFourWorldBuilder;
        private bool forceStageOneWorldRebuildForTest;
        private bool stageTestRun;
        private bool stageThreeTransitionInProgress;
        private bool stageThreeEndingPending;
        private bool stageTransitionInProgress;
        private bool pendingSceneResume;
        private bool stageThreePreloadInProgress;
        private string stageThreePreloadStatus = string.Empty;
        private string transitionError = string.Empty;
        private int transitionRetryTargetStage = 1;
        private ScreenMode transitionRetryScreen = ScreenMode.HeroSelect;
        private bool transitionRetryStartsMatch;
        private bool stageChainTest;
        private int savedCampaignStageNumber = 1;
        private int savedCampaignStagePref = 1;
        private int savedStage3Unlocked;
        private int savedStage4Unlocked;
        private int savedStage4Completed;
        private string lobbyNotice = string.Empty;
        private float lobbyNoticeUntil;
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
        private GUIStyle menuTitleStyle;
        private GUIStyle menuHeaderStyle;
        private GUIStyle menuBodyStyle;
        private GUIStyle menuSmallStyle;
        private GUIStyle menuButtonStyle;
        private GUIStyle objectiveStyle;
        private GUIStyle stageTwoFinalLeaderboardStyle;
        private int stageTwoFinalLeaderboardStyleScreenHeight = -1;
        private int menuStyleScreenHeight = -1;
        private int uiStyleScreenHeight = -1;
        private int suppressMenuButtonClicksFrame = -1;

        // These X/Z centers mirror GameBootstrap.BuildAndroidCityPresentation exactly.
        // Keep the minimap's house marks tied to the actual Android city layout.
        private static readonly Vector2[] AndroidMinimapBuildingPoints =
        {
            new Vector2(-79f, -79f), new Vector2(-55f, -79f), new Vector2(41f, -79f), new Vector2(65f, -79f),
            new Vector2(-79f, -55f), new Vector2(-31f, -55f), new Vector2(17f, -55f), new Vector2(65f, -55f),
            new Vector2(-55f, -31f), new Vector2(-7f, -31f), new Vector2(41f, -31f),
            new Vector2(-79f, -7f), new Vector2(-31f, -7f), new Vector2(41f, -7f), new Vector2(65f, -7f),
            new Vector2(-55f, 17f), new Vector2(-7f, 17f), new Vector2(41f, 17f),
            new Vector2(-79f, 41f), new Vector2(-31f, 41f), new Vector2(17f, 41f), new Vector2(65f, 41f),
            new Vector2(-55f, 65f), new Vector2(-7f, 65f), new Vector2(41f, 65f), new Vector2(65f, 65f),
            new Vector2(-79f, 89f), new Vector2(-31f, 89f), new Vector2(17f, 89f), new Vector2(65f, 89f)
        };

        // Center lines copied from BuildAndroidRoadGrid(): start at -91m, then every 24m.
        private static readonly float[] AndroidMinimapRoadCoordinates =
            { -91f, -67f, -43f, -19f, 5f, 29f, 53f, 77f };

        // Kept aligned with the 3D beacon created in GameBootstrap.
        private static readonly Vector2 AndroidSkyGuideBeaconPoint = new Vector2(5f, 5f);

        private static readonly string[] HeroNames =
        {
            "KING ARDESHIR",
            "ANAHITA",
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
            pendingSceneResume = !string.IsNullOrEmpty(PlayerPrefs.GetString("PW.PendingScreen", string.Empty));
            if (pendingSceneResume)
            {
                startingMatch = true;
                startupStatus = "Restoring next mission...";
            }
            StartupCheckpoint.Set("PrototypeFlowAwake");
            Debug.Log("PERSIA_FLOW: PrototypeFlow Awake");
        }

        private void OnDisable()
        {
            if (stageThreeWorldBuilder != null)
            {
                stageThreeWorldBuilder.OnEndingComplete -= HandleStageThreeEndingComplete;
                stageThreeWorldBuilder.OnBuildFailed -= HandleStageThreeBuildFailed;
            }
        }

        private void Start()
        {
            // Defer all menu initialization by one frame; This lets Unity finish
            // activating the gameplay scene and gives BootLoader a chance to complete
            // its LoadSceneAsync operation before any discovery/UI work runs.
            StartCoroutine(InitializeMenuAfterActivation());
        }

        private IEnumerator InitializeMenuAfterActivation()
        {
            yield return null;

            // IMGUI APIs such as GUI.skin and GUIStyle construction are only valid
            // during OnGUI. OnGUI initializes these lazily before drawing the menu.
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
            InitializeLocalProfile();
            selectedHero = Mathf.Clamp(PlayerPrefs.GetInt("PW.SelectedHero", selectedHero), 0, HeroNames.Length - 1);
            string pendingScreen = PlayerPrefs.GetString("PW.PendingScreen", string.Empty);
            if (!string.IsNullOrEmpty(pendingScreen))
            {
                // Scene reload has already activated the new gameplay scene and its
                // roots are dormant. Unload assets only now, then open the saved screen
                // directly so the Android main menu never flashes between missions.
                StartupCheckpoint.Set("PendingScreenSceneActivated_" + pendingScreen);
                AsyncOperation pendingUnload = Resources.UnloadUnusedAssets();
                while (pendingUnload != null && !pendingUnload.isDone) yield return null;
                System.GC.Collect();
                ScreenMode requestedMode;
                mode = System.Enum.TryParse(pendingScreen, out requestedMode)
                    ? requestedMode : ScreenMode.HeroSelect;
                PlayerPrefs.DeleteKey("PW.PendingScreen");
                PlayerPrefs.Save();
                StartupMemoryMonitor.SetLoading(false);
                pendingSceneResume = false;
                startingMatch = false;
                StartupCheckpoint.Set("PendingScreenOpened_" + mode);
                yield break;
            }
#if UNITY_ANDROID
            // Android production path: keep the real front-end interactive.
            // Do NOT auto-start the match. The previous isolation build skipped the
            // hero/drop-map screens and immediately activated the heaviest gameplay
            // systems, which made the app appear to "rush through" the choices.
            selectedHero = 0;
            spawnWorld = new Vector2(0f, -4f);
            spawnChosen = false;
            mode = ScreenMode.MainMenu;
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

            if (stageNumber <= 1 || stageNumber == 2)
                mode = ScreenMode.DropMap;
            StartMatch();
        }

        private void Update()
        {
            if (pendingSceneResume) return;
            if (stageThreeEndingPending && !stageThreeTransitionInProgress && !stageTransitionInProgress)
            {
                stageThreeEndingPending = false;
                StartCoroutine(StartStageFourOnNextFrame());
            }
            // During inter-stage cleanup the loading panel owns the screen and all taps are ignored.
            if (stageThreeTransitionInProgress || stageTransitionInProgress)
                return;
            if (!string.IsNullOrEmpty(transitionError))
            {
                HandleTransitionErrorTouches();
                return;
            }

            // Use direct Touch.fingerId input for front-end controls on Android.
            // GUI.Button still draws the visual surface, but must not be the only
            // path that can advance the menu if touch-to-mouse synthesis is missing.
            if (mode == ScreenMode.Match)
            {
                GameSession session = GameSession.Instance;
                if (session != null && session.IsFinished)
                {
                    if (stageNumber == 2 && session.PlayerWon)
                    {
                        // Start evacuation automatically as soon as the final result is ready.
                        // The standings remain visible during the entire ship reveal/boarding/ascent.
                        if (stageTwoVictorySequence == null)
                            StartStageTwoVictorySequence();
                        else if (stageTwoVictorySequence.IsComplete)
                            CompleteStageTwoVictoryTransition();
                    }
                    return;
                }

                CheckStageOneGirlRescue();
                if (HandleDebugStageButtonTouches()) return;
                HandleAndroidPauseTouches();
                return;
            }
            if (mode == ScreenMode.MainMenu || mode == ScreenMode.Profile ||
                mode == ScreenMode.Store || mode == ScreenMode.Settings)
            {
                HandleLobbyTouches();
                return;
            }
            if (mode == ScreenMode.HeroSelect)
            {
                HandleHeroSelectTouches();
                return;
            }
            if (mode == ScreenMode.WeaponSelect)
            {
                HandleWeaponSelectTouches();
                return;
            }
            if (mode == ScreenMode.DropMap)
                HandleDropTouches();
        }

        private void HandleAndroidPauseTouches()
        {
            if (!Application.isMobilePlatform || Input.touchCount <= 0)
                return;

            float scale = GetUiScale();
            float size = 58f * scale;
            Rect safe = GetSafeGuiRect();
            Rect button = new Rect(
                safe.center.x - size * 0.5f,
                safe.y + 14f * scale,
                size,
                size);

            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.phase != TouchPhase.Began)
                    continue;

                Vector2 gui = new Vector2(
                    touch.position.x,
                    Screen.height - touch.position.y);
                if (!button.Contains(gui))
                    continue;

                SetMatchPaused(!matchPaused);
                return;
            }
        }

        private void OnGUI()
        {
            EnsureUiInitialized();
            if (mode == ScreenMode.Match)
            {
                if (stageTwoVictorySequence != null && stageTwoVictorySequence.IsRunning)
                {
                    DrawStageTwoCinematicOverlay();
                    return;
                }
                if (stageNumber == 3 && StageThreeWorldBuilder.IsAnyCinematicActive)
                {
                    StageThreeWorldBuilder cinematicBuilder = StageThreeWorldBuilder.Instance;
                    if (cinematicBuilder != null && cinematicBuilder.IsIntroCinematic) DrawStageThreeIntroOverlay();
                    else DrawStageThreeCinematicOverlay();
                    return;
                }
                if (stageNumber == 4 && stageFourWorldBuilder != null && stageFourWorldBuilder.IsCinematic)
                {
                    DrawStageFourIntroOverlay();
                    return;
                }
                if (stageNumber == 4 && StageFourEscapeSequence.IsFinaleActive) return;
                DrawAndroidPresentationHUD();
                if (stageNumber >= 3) DrawStageFourObjective();
                DrawAndroidPauseControl();
                if (stageTestRun || stageChainTest) DrawDebugStageButton();
                if (stageNumber != 3 && stageNumber != 4) DrawGirlRescueNotice();
                if (GameSession.Instance != null && GameSession.Instance.IsFinished)
                {
                    if (resultOverlayStartTime < 0f) resultOverlayStartTime = Time.unscaledTime;
                    DrawAndroidResultOverlay();
                }
                return;
            }
            DrawBackdrop();
            if (stageTwoLobbyRunning) { DrawStageTwoLobbyOverlay(); return; }
            if (startingMatch)
            {
                if (stageTransitionInProgress)
                    startupStatus = "Preparing next mission...";
                else if (stageThreePreloadInProgress && !string.IsNullOrEmpty(stageThreePreloadStatus))
                    startupStatus = stageThreePreloadStatus;
                else if (stageNumber == 3 && stageThreeWorldBuilder != null && !string.IsNullOrEmpty(stageThreeWorldBuilder.BuildStatus))
                    startupStatus = stageThreeWorldBuilder.BuildStatus;
                else if (gameBootstrap != null && gameBootstrap.IsWorldPreparing && !string.IsNullOrEmpty(gameBootstrap.CurrentStage))
                    startupStatus = gameBootstrap.CurrentStage;
                float scale = GetUiScale();
                float width = Mathf.Min(Screen.width - 48f * scale, 760f * scale);
                float height = 170f * scale;
                Rect panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
                Fill(panel, new Color(0.04f, 0.07f, 0.11f, 0.96f));
                Fill(new Rect(panel.x, panel.y, panel.width, 6f * scale), new Color(0.92f, 0.66f, 0.18f, 1f));
                GUI.Label(new Rect(panel.x + 18f * scale, panel.y + 30f * scale, panel.width - 36f * scale, 40f * scale), "PERSIA WAR", headerStyle);
                GUI.Label(new Rect(panel.x + 18f * scale, panel.y + 76f * scale, panel.width - 36f * scale, 30f * scale), startupStatus, bodyStyle);
                GUI.Label(new Rect(panel.x + 18f * scale, panel.y + 116f * scale, panel.width - 36f * scale, 28f * scale), "Preparing battlefield...", smallStyle);
                return;
            }
            if (!string.IsNullOrEmpty(transitionError))
            {
                DrawTransitionErrorPanel();
                return;
            }
            switch (mode)
            {
                case ScreenMode.MainMenu: DrawMainMenu(); break;
                case ScreenMode.Profile: DrawProfileScreen(); break;
                case ScreenMode.Store: DrawStoreScreen(); break;
                case ScreenMode.Settings: DrawSettingsScreen(); break;
                case ScreenMode.HeroSelect:
                    if (stageNumber >= 2 && stageTwoShopOpen) DrawCharacterShop();
                    else DrawHeroSelect();
                    break;
                case ScreenMode.WeaponSelect: DrawWeaponSelect(); break;
                default: DrawDropMap(); break;
            }
        }
        private void DrawAndroidPresentationHUD()
        {
            float scale = GetUiScale();
            Rect safe = GetSafeGuiRect();
            float margin = 14f * scale;

            // This is the Android HUD actually rendered during a match.
            // Keep live player state here instead of relying on the dormant desktop HUD.
            TargetHealth playerHealth = player != null ? player.Health : null;
            int currentHealth = playerHealth != null ? playerHealth.CurrentHealth : 0;
            int maxHealth = playerHealth != null ? playerHealth.MaxHealth : 0;
            float healthRatio = maxHealth > 0 ? Mathf.Clamp01(currentHealth / (float)maxHealth) : 0f;
            int shieldAmount = player != null ? player.Shield : 0;
            float shieldRatio = Mathf.Clamp01(shieldAmount / 100f);

            WeaponController weapon = player != null ? player.Weapon : null;
            PlayerInventory inventory = player != null ? player.Inventory : null;
            int magazine = weapon != null ? weapon.Magazine : 0;
            int reserveAmmo = weapon != null ? weapon.Reserve : 0;
            int grenades = inventory != null ? inventory.Grenades : 0;
            int wave = enemySpawner != null ? enemySpawner.CurrentWave : 0;
            GameSession session = GameSession.Instance;
            int score = session != null ? session.Score : 0;

            if (Time.unscaledTime >= nextMinimapRefresh)
            {
                nextMinimapRefresh = Time.unscaledTime + 0.40f;
                minimapEnemies = FindObjectsByType<EnemyChase>(FindObjectsSortMode.None);
                minimapPickups = FindObjectsByType<PickupItem>(FindObjectsSortMode.None);
            }

            int survivorCount = player != null && !player.IsDefeated ? 1 : 0;
            for (int i = 0; i < minimapEnemies.Length; i++)
            {
                EnemyChase enemy = minimapEnemies[i];
                if (enemy != null && enemy.gameObject.activeInHierarchy)
                    survivorCount++;
            }

            float leftW = 340f * scale;
            float leftH = 100f * scale;
            Rect left = new Rect(safe.x + margin, safe.y + margin, leftW, leftH);
            Fill(left, new Color(0.03f, 0.06f, 0.08f, 0.90f));
            Fill(new Rect(left.x, left.y, 62f * scale, 62f * scale), new Color(0.94f, 0.62f, 0.18f, 1f));
            GUI.Label(new Rect(left.x + 10f * scale, left.y + 8f * scale, 42f * scale, 42f * scale),
                "P", headerStyle);
            GUI.Label(new Rect(left.x + 72f * scale, left.y + 3f * scale, left.width - 82f * scale, 27f * scale),
                "PERSIA WAR", headerStyle);

            float statusX = left.x + 72f * scale;
            float statusWidth = left.width - 82f * scale;
            GUI.Label(new Rect(statusX, left.y + 31f * scale, statusWidth, 20f * scale),
                "HP  " + currentHealth + " / " + maxHealth, smallStyle);
            DrawStatusBar(
                new Rect(statusX, left.y + 51f * scale, statusWidth, 9f * scale),
                healthRatio,
                new Color(0.25f, 0.90f, 0.36f, 1f));

            GUI.Label(new Rect(statusX, left.y + 63f * scale, statusWidth, 20f * scale),
                "SHIELD  " + shieldAmount + " / 100", smallStyle);
            DrawStatusBar(
                new Rect(statusX, left.y + 84f * scale, statusWidth, 9f * scale),
                shieldRatio,
                new Color(0.30f, 0.66f, 1f, 1f));

            // Preserve the useful ammo/grenade counters without stacking a second
            // player-health panel over this one.
            Rect loadout = new Rect(margin, left.yMax + 6f * scale, leftW, 28f * scale);
            Fill(loadout, new Color(0.03f, 0.06f, 0.08f, 0.86f));
            string weaponSummary = weapon != null
                ? weapon.CurrentWeaponName + " " + Mathf.RoundToInt(weapon.EffectiveRange) + "m"
                : "NO WEAPON";
            GUI.Label(new Rect(loadout.x + 8f * scale, loadout.y, loadout.width - 16f * scale, loadout.height),
                weaponSummary + "  AMMO " + magazine + "/" + reserveAmmo + "  G " + grenades, smallStyle);

            float rightW = 250f * scale;
            Rect right = new Rect(safe.xMax - rightW - margin, safe.y + margin, rightW, 58f * scale);
            Fill(right, new Color(0.03f, 0.06f, 0.08f, 0.88f));
            GUI.Label(new Rect(right.x + 12f * scale, right.y + 6f * scale, right.width - 24f * scale, 22f * scale),
                "SURVIVORS", smallStyle);
            GUI.Label(new Rect(right.x + 12f * scale, right.y + 27f * scale, right.width - 24f * scale, 24f * scale),
                survivorCount.ToString(), headerStyle);

            if (stageNumber <= 2)
            {
            // Tactical minimap remains see-through so the 3D battle stays visible.
            float mapSize = Mathf.Clamp(148f * scale * 1.38f * 1.15f * 1.20f, 180f, 314f);
            Rect map = new Rect(safe.xMax - mapSize - margin, safe.y + 84f * scale, mapSize, mapSize);
            if (stageNumber == 2)
                DrawStageTwoVillageMap(map);
            else
                Fill(map, new Color(0.12f, 0.28f, 0.12f, 0.42f));

            Color frameColor = new Color(0.92f, 0.95f, 0.98f, 0.46f);
            float frame = Mathf.Max(1f, 1.5f * scale);
            Fill(new Rect(map.x, map.y, map.width, frame), frameColor);
            Fill(new Rect(map.x, map.yMax - frame, map.width, frame), frameColor);
            Fill(new Rect(map.x, map.y, frame, map.height), frameColor);
            Fill(new Rect(map.xMax - frame, map.y, frame, map.height), frameColor);

            if (stageNumber == 1)
            {
            Color roadColor = new Color(0.78f, 0.81f, 0.84f, 0.66f);
            float roadThickness = map.width * (10f / 192f);
            for (int i = 0; i < AndroidMinimapRoadCoordinates.Length; i++)
            {
                float road = AndroidMinimapRoadCoordinates[i];
                float x = map.x + Mathf.InverseLerp(-96f, 96f, road) * map.width;
                float y = map.y + Mathf.InverseLerp(96f, -96f, road) * map.height;
                Fill(new Rect(x - roadThickness * 0.5f, map.y, roadThickness, map.height), roadColor);
                Fill(new Rect(map.x, y - roadThickness * 0.5f, map.width, roadThickness), roadColor);
            }

            for (int i = 0; i < AndroidMinimapBuildingPoints.Length; i++)
            {
                // Repeat the deterministic footprint math from GameBootstrap so the
                // drawn house rectangles match each real building's center and size.
                float widthRoll = Mathf.Abs(Mathf.Sin((i + 1) * 12.9898f));
                float depthRoll = Mathf.Abs(Mathf.Sin((i + 1) * 39.425f));
                float footprint = Mathf.Lerp(7.2f, 10.2f, widthRoll);
                float depth = Mathf.Lerp(6.8f, 9.8f, depthRoll);

                if (i >= 22)
                {
                    footprint = Mathf.Lerp(6.5f, 8.2f, widthRoll);
                    depth = Mathf.Lerp(6.0f, 7.8f, depthRoll);
                }
                else if (i >= 18 && i <= 21)
                {
                    footprint = Mathf.Lerp(7.5f, 9.8f, widthRoll);
                    depth = Mathf.Lerp(6.8f, 9.2f, depthRoll);
                }

                if (i == 0 || i == 7 || i == 13)
                    footprint = Mathf.Max(footprint, 10.2f);

                if (i == 4 || i == 9 || i == 11 || i == 16)
                {
                    footprint = Mathf.Lerp(9.4f, 10.4f, widthRoll);
                    depth = Mathf.Lerp(8.2f, 10.0f, depthRoll);
                }

                Vector2 building = WorldToMap(AndroidMinimapBuildingPoints[i], map);
                float buildingWidth = map.width * (footprint / 192f);
                float buildingHeight = map.height * (depth / 192f);
                // A dark outline separates every footprint from the green ground and
                // gray streets; the inner sand color makes house locations legible
                // on a transparent map without changing their real world coordinates.
                float outline = Mathf.Max(1.5f, 2.0f * scale);
                Rect houseRect = new Rect(
                    building.x - buildingWidth * 0.5f,
                    building.y - buildingHeight * 0.5f,
                    buildingWidth,
                    buildingHeight);
                Fill(new Rect(houseRect.x - outline * 0.5f, houseRect.y - outline * 0.5f,
                    houseRect.width + outline, houseRect.height + outline),
                    new Color(0.12f, 0.075f, 0.035f, 0.98f));
                Fill(houseRect, new Color(0.96f, 0.68f, 0.30f, 1f));
            }

            }

            for (int i = 0; i < minimapEnemies.Length; i++)
            {
                EnemyChase enemy = minimapEnemies[i];
                if (enemy == null || !enemy.gameObject.activeInHierarchy)
                    continue;

                Vector3 enemyPosition = enemy.transform.position;
                if (Mathf.Abs(enemyPosition.x) > 96f || Mathf.Abs(enemyPosition.z) > 96f)
                    continue;

                Vector2 enemyPoint = WorldToMap(new Vector2(enemyPosition.x, enemyPosition.z), map);
                // The outer marker is twice the old full diameter; a navy center keeps
                // the requested enemy color while the brighter blue rim remains legible.
                enemyPoint.x = Mathf.Clamp(enemyPoint.x, map.x + 7f * scale, map.xMax - 7f * scale);
                enemyPoint.y = Mathf.Clamp(enemyPoint.y, map.y + 7f * scale, map.yMax - 7f * scale);
                DrawCircle(enemyPoint, 7f * scale, new Color(0.22f, 0.56f, 0.98f, 1f));
                DrawCircle(enemyPoint, 5f * scale, new Color(0.035f, 0.075f, 0.24f, 1f));
            }

            // Cyan squares show live collectible pickups/dropped supplies.
            for (int i = 0; i < minimapPickups.Length; i++)
            {
                PickupItem pickup = minimapPickups[i];
                if (pickup == null || !pickup.gameObject.activeInHierarchy)
                    continue;
                Vector3 pickupPosition = pickup.transform.position;
                if (Mathf.Abs(pickupPosition.x) > 96f || Mathf.Abs(pickupPosition.z) > 96f)
                    continue;

                Vector2 pickupPoint = WorldToMap(new Vector2(pickupPosition.x, pickupPosition.z), map);
                float markerSize = 5f * scale;
                Fill(new Rect(pickupPoint.x - markerSize * 0.5f,
                    pickupPoint.y - markerSize * 0.5f, markerSize, markerSize),
                    new Color(0.20f, 0.95f, 1f, 1f));
            }

            if (stageNumber < 2 && ExtractionBeacon.IsActive)
            {
                Vector3 extractionWorld = ExtractionBeacon.WorldPosition;
                Vector2 extractionPoint = WorldToMap(
                    new Vector2(extractionWorld.x, extractionWorld.z),
                    map);
                DrawCircle(extractionPoint, 12f * scale, new Color(0.08f, 0.48f, 1f, 0.62f));
                DrawCircle(extractionPoint, 5.5f * scale, new Color(0.58f, 0.90f, 1f, 1f));
            }

            Vector2 playerPoint = new Vector2(map.center.x, map.center.y);
            if (player != null)
            {
                Vector3 pos = player.transform.position;
                playerPoint = WorldToMap(new Vector2(pos.x, pos.z), map);
            }
            DrawCircle(playerPoint, 6.5f * scale, new Color(0.95f, 0.86f, 0.20f, 1f));
            GUI.Label(new Rect(map.x + 7f * scale, map.y + 5f * scale, map.width - 12f * scale, 22f * scale),
                (stageNumber == 2 ? "VILLAGE  •  YOU  •  ALIENS" : "N ↑   MAP   P:YOU   E:ENEMY"), smallStyle);

            Rect legend = new Rect(
                map.x + 4f * scale,
                map.yMax - 27f * scale,
                map.width - 8f * scale,
                22f * scale);
            Fill(legend, new Color(0.02f, 0.035f, 0.055f, 0.84f));
            DrawCircle(new Vector2(legend.x + 10f * scale, legend.center.y),
                3f * scale, new Color(0.20f, 0.95f, 1f, 1f));
            GUI.Label(new Rect(legend.x + 18f * scale, legend.y, 118f * scale, legend.height),
                "L: LOOT", smallStyle);
            DrawCircle(new Vector2(legend.x + 148f * scale, legend.center.y),
                3.5f * scale, stageNumber >= 2 ? new Color(0.22f, 0.96f, 0.32f, 1f) : new Color(0.20f, 0.58f, 1f, 1f));
            GUI.Label(new Rect(legend.x + 157f * scale, legend.y, legend.width - 162f * scale, legend.height),
                stageNumber >= 2 ? "A: ALIEN" : "B: BEACON", smallStyle);

            Rect counters = new Rect(
                Screen.width - rightW - margin,
                map.yMax + 7f * scale,
                rightW,
                30f * scale);
            Fill(counters, new Color(0.03f, 0.06f, 0.08f, 0.86f));
            StageTwoArenaManager arenaManager = stageNumber >= 2 ? GetComponent<StageTwoArenaManager>() : null;
            string counterText = stageNumber >= 3
                ? "HOSTILES  " + (stageNumber == 3
                    ? (stageThreeWorldBuilder != null ? stageThreeWorldBuilder.RemainingHostiles : 0)
                    : (stageFourWorldBuilder != null ? stageFourWorldBuilder.RemainingHostiles : 0))
                : stageNumber == 2
                    ? "WAVE " + wave + "/5   KILLS " + (arenaManager != null ? arenaManager.LocalKills : 0)
                    : "WAVE  " + wave + "     SCORE  " + score;
            GUI.Label(new Rect(counters.x + 8f * scale, counters.y, counters.width - 16f * scale, counters.height),
                counterText, smallStyle);

            if (stageNumber == 2 && arenaManager != null)
            {
                // Put the winners/leaderboard panel to the LEFT of the map, not on
                // top of the tactical map markers. Keep it responsive on narrow phones.
                float leaderboardWidth = Mathf.Min(
                    154f * scale,
                    Mathf.Max(96f * scale, map.x - margin - 12f * scale));
                float leaderboardX = Mathf.Max(margin, map.x - leaderboardWidth - 12f * scale);
                Rect leaderboard = new Rect(leaderboardX, map.y + 30f * scale,
                    leaderboardWidth, 82f * scale);
                Fill(leaderboard, new Color(0.015f, 0.035f, 0.045f, 0.78f));
                Fill(new Rect(leaderboard.x, leaderboard.y, 3f * scale, leaderboard.height),
                    new Color(0.25f, 0.95f, 0.45f, 0.95f));
                GUI.Label(new Rect(leaderboard.x + 7f * scale, leaderboard.y + 2f * scale,
                    leaderboard.width - 12f * scale, leaderboard.height - 4f * scale),
                    "TOP 3\\n" + arenaManager.GetLeaderboardText(), smallStyle);
            }

            if (stageNumber < 2 && ExtractionBeacon.IsActive)
            {
                float objectiveWidth = 360f * scale;
                Rect objective = new Rect(
                    (Screen.width - objectiveWidth) * 0.5f,
                    Screen.height - 43f * scale,
                    objectiveWidth,
                    30f * scale);
                Fill(objective, new Color(0.02f, 0.20f, 0.48f, 0.92f));
                GUI.Label(
                    new Rect(objective.x + 8f * scale, objective.y, objective.width - 16f * scale, objective.height),
                    "EXTRACTION ACTIVE  -  REACH THE BLUE BEAM",
                    smallStyle);
            }
        }

            }

        private void DrawStatusBar(Rect rect, float ratio, Color fillColor)
        {
            Fill(rect, new Color(0f, 0f, 0f, 0.82f));
            float inset = Mathf.Min(1.5f, rect.height * 0.2f);
            Rect inner = new Rect(
                rect.x + inset,
                rect.y + inset,
                Mathf.Max(0f, rect.width - inset * 2f),
                Mathf.Max(0f, rect.height - inset * 2f));
            inner.width *= Mathf.Clamp01(ratio);
            Fill(inner, fillColor);
        }

        private void DrawPreviousRunDiagnostic()
        {
#if UNITY_ANDROID
            string previous = StartupCheckpoint.Previous;
            string exception = StartupCheckpoint.LastException;
            if ((string.IsNullOrEmpty(previous) || previous == "none") &&
                (string.IsNullOrEmpty(exception) || exception == "none")) return;

            float scale = GetUiScale();
            float width = Mathf.Min(Screen.width * 0.62f, 470f * scale);
            float height = 174f * scale;
            Rect panel = new Rect(16f * scale,
                Screen.height - height - 12f * scale, width, height);
            Fill(panel, new Color(0.03f, 0.045f, 0.07f, 0.94f));
            Fill(new Rect(panel.x, panel.y, panel.width, 4f * scale), new Color(1f, 0.55f, 0.12f, 1f));
            GUI.Label(new Rect(panel.x + 8f * scale, panel.y + 5f * scale, panel.width - 16f * scale, 20f * scale),
                "LAST RUN", smallStyle);
            GUI.Label(new Rect(panel.x + 8f * scale, panel.y + 27f * scale, panel.width - 16f * scale, 24f * scale),
                "Checkpoint: " + previous, smallStyle);
            string compactException = exception.Length > 58 ? exception.Substring(0, 58) + "…" : exception;
            GUI.Label(new Rect(panel.x + 8f * scale, panel.y + 52f * scale, panel.width - 16f * scale, 24f * scale),
                "Last error: " + compactException, smallStyle);
            GUI.Label(new Rect(panel.x + 8f * scale, panel.y + 77f * scale, panel.width - 16f * scale, 22f * scale),
                "Exit: " + StartupCheckpoint.PreviousExitState, smallStyle);
            GUI.Label(new Rect(panel.x + 8f * scale, panel.y + 99f * scale, panel.width - 16f * scale, 22f * scale),
                StartupCheckpoint.PreviousMemorySummary, smallStyle);
            GUI.Label(new Rect(panel.x + 8f * scale, panel.y + 121f * scale, panel.width - 16f * scale, 22f * scale),
                StartupCheckpoint.PreviousTransitionPeakSummary, smallStyle);
            if (GUI.Button(new Rect(panel.x + 8f * scale, panel.y + 145f * scale,
                panel.width - 16f * scale, 24f * scale), "COPY REPORT"))
            {
                GUIUtility.systemCopyBuffer = StartupCheckpoint.BuildCopyReport();
                StartupCheckpoint.Set("LastRunReportCopied");
            }
#endif
        }

        private float GetUiScale()
        {
            return Mathf.Clamp(Mathf.Min(Screen.width, Screen.height) / 720f, 0.75f, 1.35f);
        }

        private Rect GetSafeGuiRect()
        {
            Rect safe = Screen.safeArea;
            return new Rect(safe.xMin, Screen.height - safe.yMax, safe.width, safe.height);
        }

        private Rect GetHeaderRect()
        {
            Rect safe = GetSafeGuiRect();
            float scale = GetUiScale();
            float margin = 14f * scale;
            return new Rect(safe.x + margin, safe.y + 6f * scale,
                Mathf.Max(0f, safe.width - margin * 2f), 72f * scale);
        }

        private void DrawBackdrop()
        {
            // Bright blue lobby palette inspired by the supplied battle-royale waiting screen.
            Color old = GUI.color;
            GUI.color = new Color(0.14f, 0.49f, 0.70f, 1f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), pixel);
            GUI.color = old;
            for (int band = 0; band < 8; band++)
            {
                float t = band / 7f;
                Fill(new Rect(0f, band * Screen.height / 8f, Screen.width, Screen.height / 8f + 1f),
                    Color.Lerp(new Color(0.10f, 0.38f, 0.61f, 0.30f),
                               new Color(0.37f, 0.72f, 0.83f, 0.22f), t));
            }
            float scale = GetUiScale();
            Rect header = GetHeaderRect();
            Fill(new Rect(0f, header.y - 6f * scale, Screen.width,
                header.height + 12f * scale), new Color(0.02f, 0.035f, 0.06f, 0.48f));
            GUI.Label(new Rect(header.x, header.y, header.width, 42f * scale), "♛  PERSIA WAR", menuTitleStyle);
            GUI.Label(new Rect(header.x + 2f * scale, header.y + 40f * scale,
                header.width, 26f * scale), "CLASSIC BATTLE ROYALE", menuSmallStyle);
        }

        private void InitializeLocalProfile()
        {
            playerId = PlayerPrefs.GetString("PW.PlayerId", string.Empty);
            if (string.IsNullOrEmpty(playerId))
            {
                playerId = System.Guid.NewGuid().ToString("N").Substring(0, 12).ToUpperInvariant();
                PlayerPrefs.SetString("PW.PlayerId", playerId);
            }

            // CampaignStage is the authoritative progression checkpoint. Unlock flags
            // only describe availability; using them to overwrite the checkpoint can
            // skip earlier stages after relaunch or after a test-stage run.
            stageNumber = Mathf.Clamp(PlayerPrefs.GetInt("PW.CampaignStage", 1), 1, 5);
            if (PlayerPrefs.GetInt("PW.Stage4Completed", 0) != 0) stageNumber = 5;
            profileNameInput = PlayerPrefs.GetString("PW.PlayerName", string.Empty);
            if (string.IsNullOrWhiteSpace(profileNameInput))
                profileNameInput = "Persian Warrior";

            storeCoins = PlayerPrefs.GetInt("PW.StoreCoins", 500);
            selectedProfileCosmetic = PlayerPrefs.GetInt("PW.ProfileCosmetic", 0);
            selectedStoreWeaponKind = PlayerPrefs.GetInt("PW.SelectedWeaponKind", (int)WeaponController.WeaponKind.AssaultRifle);
            soundEnabled = PlayerPrefs.GetInt("PW.SoundEnabled", 1) != 0;
            AudioListener.volume = soundEnabled ? 1f : 0f;
            if (!PlayerPrefs.HasKey("PW.CosmeticOwned.0"))
                PlayerPrefs.SetInt("PW.CosmeticOwned.0", 1);
            PlayerPrefs.Save();
        }

        private Rect GetLobbyButtonRect(int index)
        {
            float scale = GetUiScale();
            float width = Mathf.Min(Screen.width * 0.42f, 520f * scale);
            float heightLimit = Mathf.Max(24f, (Screen.height - 120f * scale) / 12f);
            float height = Mathf.Min(58f * scale, heightLimit);
            float gapLimit = Mathf.Max(2f, (Screen.height - 100f * scale - height * 10f) / 9f);
            float gap = Mathf.Min(10f * scale, gapLimit);
            float totalHeight = height * 10f + gap * 9f;
            float topLimit = Mathf.Max(54f * scale, Screen.height - totalHeight - 8f);
            float top = Mathf.Clamp((Screen.height - totalHeight) * 0.48f, 54f * scale, topLimit);
            float left = Mathf.Max(Screen.width * 0.52f, Screen.width - width - 28f);
            left = Mathf.Min(left, Screen.width - width - 18f);
            return new Rect(left, top + index * (height + gap), width, height);
        }

        private Rect GetLobbyBackRect()
        {
            float scale = GetUiScale();
            float width = Mathf.Min(300f * scale, Screen.width - 36f);
            return new Rect((Screen.width - width) * 0.5f, Screen.height - 86f * scale, width, 54f * scale);
        }

        private void HandleLobbyTouches()
        {
            if (!Application.isMobilePlatform || Input.touchCount <= 0)
                return;

            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.phase != TouchPhase.Began)
                    continue;

                Vector2 point = new Vector2(touch.position.x, Screen.height - touch.position.y);
                if (mode == ScreenMode.MainMenu)
                {
                    // Keep native touch hit-testing aligned with every visible menu button.
                    for (int button = 0; button < 10; button++)
                    {
                        if (!GetLobbyButtonRect(button).Contains(point)) continue;
                        suppressMenuButtonClicksFrame = Time.frameCount;
                        ActivateLobbyButton(button);
                        return;
                    }
                }
                else if (GetLobbyBackRect().Contains(point))
                {
                    suppressMenuButtonClicksFrame = Time.frameCount;
                    if (mode == ScreenMode.Profile)
                        SaveProfileAndContinue();
                    else
                        mode = ScreenMode.MainMenu;
                    return;
                }

                if (mode == ScreenMode.Store)
                {
                    for (int item = 0; item < StoreCosmeticNames.Length; item++)
                    {
                        if (!GetStoreActionRect(item).Contains(point)) continue;
                        suppressMenuButtonClicksFrame = Time.frameCount;
                        PurchaseOrEquipCosmetic(item);
                        return;
                    }
                }
                else if (mode == ScreenMode.Settings && GetSettingsSoundRect().Contains(point))
                {
                    suppressMenuButtonClicksFrame = Time.frameCount;
                    soundEnabled = !soundEnabled;
                    PlayerPrefs.SetInt("PW.SoundEnabled", soundEnabled ? 1 : 0);
                    AudioListener.volume = soundEnabled ? 1f : 0f;
                    PlayerPrefs.Save();
                    return;
                }
            }
        }

        private void DrawMainMenu()
        {
            string displayName = PlayerPrefs.GetString("PW.PlayerName", "Persian Warrior");
            int coins = PlayerPrefs.GetInt("PW.StoreCoins", 500);
            Rect card = new Rect(Screen.width * 0.07f, Screen.height * 0.19f,
                Screen.width * 0.28f, Screen.height * 0.60f);
            Fill(card, new Color(0.025f, 0.055f, 0.085f, 0.88f));
            Fill(new Rect(card.x, card.y, 6f, card.height), new Color(0.95f, 0.69f, 0.20f, 1f));
            GUI.Label(new Rect(card.x + 18f, card.y + 18f, card.width - 36f, 42f), "PLAYER", menuHeaderStyle);
            GUI.Label(new Rect(card.x + 18f, card.y + 76f, card.width - 36f, 48f), displayName, menuBodyStyle);
            GUI.Label(new Rect(card.x + 18f, card.y + 132f, card.width - 36f, 32f), "ID  " + playerId, menuSmallStyle);
            GUI.Label(new Rect(card.x + 18f, card.y + 184f, card.width - 36f, 38f), "TEST COINS  " + coins, menuBodyStyle);
            GUI.Label(new Rect(card.x + 18f, card.y + card.height - 68f, card.width - 36f, 48f),
                "OFFLINE PROFILE", menuSmallStyle);

            GUI.Label(new Rect(Screen.width * 0.40f, Screen.height * 0.055f, Screen.width * 0.54f, 46f),
                "CAMPAIGN  •  STAGE TEST ACCESS", menuBodyStyle);
            string[] labels =
            {
                "PLAY / CONTINUE CAMPAIGN",
                "PLAYER PROFILE",
                "STORE / LOADOUT",
                "SETTINGS",
                "TEST STAGE 1",
                "TEST STAGE 2",
                "TEST STAGE 3",
                "TEST STAGE 4",
                "TEST CHAIN: STAGE 2 → 3 → 4",
                "DEBUG COMPLETE THIS STAGE"
            };

            for (int i = 0; i < labels.Length; i++)
            {
                Rect button = GetLobbyButtonRect(i);
                if (DrawFantasyButton(button, labels[i], i == 0 || i >= 4))
                    ActivateLobbyButton(i);
            }

            if (!string.IsNullOrEmpty(lobbyNotice) && Time.unscaledTime < lobbyNoticeUntil)
            {
                Rect lastButton = GetLobbyButtonRect(9);
                float scale = GetUiScale();
                float noticeHeight = 34f * scale;
                float noticeY = Mathf.Min(lastButton.yMax + 6f * scale, Screen.height - 58f * scale - noticeHeight);
                Rect notice = new Rect(Screen.width * 0.52f, noticeY,
                    Screen.width * 0.42f, noticeHeight);
                Fill(notice, new Color(0.025f, 0.06f, 0.09f, 0.95f));
                GUI.Label(notice, lobbyNotice, menuSmallStyle);
            }

            GUI.Label(new Rect(Screen.width * 0.40f, Screen.height - 42f, Screen.width * 0.54f, 24f),
                "PERSIA WAR  •  UNITY EDITION", menuSmallStyle);
            DrawPreviousRunDiagnostic();
        }

        private void ActivateLobbyButton(int index)
        {
            if (index == 0)
            {
                StartCampaignFromMenu();
                return;
            }
            if (index == 1) mode = ScreenMode.Profile;
            else if (index == 2) mode = ScreenMode.Store;
            else if (index == 3) mode = ScreenMode.Settings;
            else if (index == 4) BeginStageTestEntry(1);
            else if (index == 5) BeginStageTestEntry(2);
            else if (index == 6) BeginStageTestEntry(3);
            else if (index == 7) BeginStageTestEntry(4);
            else if (index == 8) BeginStageChainTest();
            else if (index == 9) DebugCompleteCurrentStage();

            StartupCheckpoint.Set("LobbyNavigation_" + mode);
        }

        private void BeginStageChainTest()
        {
            stageChainTest = true;
            BeginStageTestEntry(2);
            ShowLobbyNotice("CHAIN TEST: use DEBUG COMPLETE THIS STAGE in Stage 2, then Stage 3.");
            StartupCheckpoint.Set("StageChainTestStarted_Stage2");
        }

        private void DebugCompleteCurrentStage()
        {
            if (stageNumber == 1)
            {
                BeginStageTwoSelection();
                return;
            }
            if (stageNumber == 2)
            {
                if (GameSession.Instance != null) GameSession.Instance.EndMission(true);
                if (stageTwoVictorySequence == null) StartStageTwoVictorySequence();
                StartupCheckpoint.Set("DebugCompleteStage2_RealEvacuationSequence");
                return;
            }
            if (stageNumber == 3)
            {
                StageThreeWorldBuilder builder = StageThreeWorldBuilder.Instance;
                if (builder != null) builder.DebugCompleteEnding();
                else ShowTransitionError(3, ScreenMode.HeroSelect, true, "Stage 3 builder is missing. Retry Stage 3 construction.");
                return;
            }
            if (stageNumber == 4)
            {
                CompleteStageFourEscapeToMainMenu();
                return;
            }
            ShowLobbyNotice("NO ACTIVE STAGE TO COMPLETE.");
        }

        private void StartCampaignFromMenu()
        {
            if (stageTestRun)
                RestoreCampaignProgressAfterTest();

            // If the app was closed during the required Stage 1 registration screen,
            // resume that screen before allowing the Stage 2 hero choice.
            if (PlayerPrefs.GetInt("PW.RegistrationPending", 0) != 0)
            {
                mode = ScreenMode.Profile;
                StartupCheckpoint.Set("StageOneRegistrationPending_OpenProfile");
                return;
            }

            if (stageNumber > 4)
            {
                ShowLobbyNotice("NO FURTHER STAGE IS AVAILABLE YET.");
                return;
            }

            // If Stage 2 was tested last, don't accidentally reuse its generated
            // village when the real campaign resumes at Stage 1.
            forceStageOneWorldRebuildForTest = stageNumber == 1;
            mode = ScreenMode.HeroSelect;
            spawnChosen = false;
            StartupCheckpoint.Set("CampaignContinue_Stage" + stageNumber);
        }

        private void CaptureCampaignProgressBeforeTest()
        {
            if (stageTestRun) return;
            savedCampaignStageNumber = stageNumber;
            savedCampaignStagePref = PlayerPrefs.GetInt("PW.CampaignStage", stageNumber);
            savedStage3Unlocked = PlayerPrefs.GetInt("PW.Stage3Unlocked", 0);
            savedStage4Unlocked = PlayerPrefs.GetInt("PW.Stage4Unlocked", 0);
            savedStage4Completed = PlayerPrefs.GetInt("PW.Stage4Completed", 0);
            stageTestRun = true;
        }

        private void RestoreCampaignProgressAfterTest()
        {
            if (!stageTestRun) return;
            stageNumber = Mathf.Clamp(savedCampaignStageNumber, 1, 5);
            PlayerPrefs.SetInt("PW.CampaignStage", savedCampaignStagePref);
            PlayerPrefs.SetInt("PW.Stage3Unlocked", savedStage3Unlocked);
            PlayerPrefs.SetInt("PW.Stage4Unlocked", savedStage4Unlocked);
            PlayerPrefs.SetInt("PW.Stage4Completed", savedStage4Completed);
            PlayerPrefs.Save();
            stageTestRun = false;
            forceStageOneWorldRebuildForTest = stageNumber == 1;
            StartupCheckpoint.Set("TestMode_CampaignProgressRestored_Stage" + stageNumber);
        }

        private void BeginStageTestEntry(int targetStage)
        {
            CaptureCampaignProgressBeforeTest();
            StageThreeWorldBuilder.ClearExistingWorld();
            StageTwoArenaManager.Instance?.ResetForStageChange();
            Time.timeScale = 1f;
            lobbyNotice = string.Empty;
            lobbyNoticeUntil = 0f;
            matchPaused = false;
            startingMatch = false;
            spawnChosen = false;
            resultOverlayStartTime = -1f;

            if (stageTwoVictorySequence != null)
            {
                stageTwoVictorySequence.FinishAndCleanup();
                Destroy(stageTwoVictorySequence);
                stageTwoVictorySequence = null;
            }

            if (targetStage == 1)
            {
                stageNumber = 1;
                selectedHero = 0;
                stageTwoOriginalHero = 0;
                stageTwoLandingMode = 0;
                stageTwoLayoutVariant = 0;
                stageTwoShopOpen = false;
                stageTwoLobbyRunning = false;
                stageTwoLobbyReady = false;
                stageTwoLobbyPlayers = 1;
                forceStageOneWorldRebuildForTest = true;
                ExtractionBeacon.ClearForStageTransition();
                if (GameSession.Instance != null) GameSession.Instance.ResetMatch();
                if (activeCamera != null) activeCamera.enabled = false;
                if (androidRuntimeCamera != null) androidRuntimeCamera.enabled = false;
                GateGameplay(false);
                mode = ScreenMode.HeroSelect;
                StartupCheckpoint.Set("DirectStageTestEntry_1");
                return;
            }

            if (targetStage == 2)
            {
                stageTwoLobbyRunning = false;
                stageTwoLobbyReady = false;
                stageTwoLobbyPlayers = 1;
                forceStageOneWorldRebuildForTest = false;
                if (GameSession.Instance != null) GameSession.Instance.ResetMatch();
                BeginStageTwoSelection();
                StartupCheckpoint.Set("DirectStageTestEntry_2");
                return;
            }

            if (targetStage == 3 || targetStage == 4)
            {
                stageNumber = targetStage;
                selectedHero = 0;
                stageTwoOriginalHero = 0;
                stageTwoLandingMode = 0;
                stageTwoLayoutVariant = 0;
                stageTwoShopOpen = false;
                stageTwoLobbyRunning = false;
                stageTwoLobbyReady = false;
                stageTwoLobbyPlayers = 1;
                forceStageOneWorldRebuildForTest = false;
                spawnWorld = targetStage == 3 ? new Vector2(0f, -97f) : Vector2.zero;
                spawnChosen = true;
                if (GameSession.Instance != null) GameSession.Instance.ResetMatch();
                if (activeCamera != null) activeCamera.enabled = false;
                if (androidRuntimeCamera != null) androidRuntimeCamera.enabled = false;
                GateGameplay(false);
                // Stages 3 and 4 have authored transport/uplink arrival sequences.
                // Their fixed entry points do not need the city/village landing map.
                mode = ScreenMode.MainMenu;
                StartCoroutine(BeginMainGameAfterMenuFrame());
                StartupCheckpoint.Set("DirectStageTestEntry_" + targetStage + "_FixedEntry");
            }
        }

        private void ShowLobbyNotice(string message)
        {
            lobbyNotice = message ?? string.Empty;
            lobbyNoticeUntil = Time.unscaledTime + 8f;
            StartupCheckpoint.Set("LobbyNoticeShown");
        }

        private void StartStageTwoVictorySequence()
        {
            if (stageTwoVictorySequence != null) return;

            // Commit campaign progress as soon as the Stage 2 victory is confirmed.
            // Android may terminate the process during world teardown; saving only
            // after teardown caused a relaunch to replay Stage 2 instead of resuming Stage 3.
            if (!stageTestRun || stageChainTest)
            {
                PlayerPrefs.SetInt("PW.Stage3Unlocked", 1);
                PlayerPrefs.SetInt("PW.CampaignStage", 3);
                PlayerPrefs.Save();
                StartupCheckpoint.Set("StageTwoVictory_Stage3ProgressSavedBeforeCleanup");
                Debug.Log("PERSIA_TRANSITION: Stage 3 checkpoint saved before Stage 2 cleanup.");
            }

            GateGameplay(false);
            if (followCamera != null) followCamera.enabled = false;
            stageTwoVictorySequence = gameObject.AddComponent<StageTwoVictorySequence>();
            Transform localRoot = playerRoot != null ? playerRoot.transform
                : (player != null ? player.transform : null);
            Camera cinematicCamera = androidRuntimeCamera != null && androidRuntimeCamera.isActiveAndEnabled
                ? androidRuntimeCamera
                : (activeCamera != null && activeCamera.isActiveAndEnabled ? activeCamera : Camera.main);
            stageTwoVictorySequence.Begin(localRoot, cinematicCamera);
        }

        private void DrawStageFourIntroOverlay()
        {
            float scale = GetUiScale();
            Rect panel = new Rect(Screen.width * 0.08f, Screen.height * 0.08f, Screen.width * 0.84f, 72f * scale);
            Fill(panel, new Color(0.005f, 0.025f, 0.035f, 0.88f));
            Fill(new Rect(panel.x, panel.y + panel.height - 4f * scale, panel.width, 4f * scale), new Color(0.10f, 1f, 0.42f, 0.95f));
            GUI.Label(new Rect(panel.x + 12f * scale, panel.y + 6f * scale, panel.width - 24f * scale, 26f * scale),
                "STAGE 4  •  ORBITAL MOTHERSHIP", menuSmallStyle);
            GUI.Label(new Rect(panel.x + 12f * scale, panel.y + 31f * scale, panel.width - 24f * scale, 34f * scale),
                stageFourWorldBuilder != null ? stageFourWorldBuilder.IntroMessage : "ENTERING THE MEGABASE", menuHeaderStyle);
        }

        private void DrawStageFourObjective()
        {
            if (stageNumber < 3) return;
            float scale = GetUiScale();
            // Shared objective row sits below the shared player/loadout panel.
            Rect panel = new Rect(14f * scale, 154f * scale, 340f * scale, 48f * scale);
            Fill(panel, new Color(0.015f, 0.035f, 0.055f, 0.82f));
            Fill(new Rect(panel.x, panel.y, 3.5f * scale, panel.height),
                new Color(0.10f, 1f, 0.42f, 0.95f));

            StageThreeWorldBuilder stageThree = StageThreeWorldBuilder.Instance;
            int remaining = stageNumber == 3
                ? (stageThree != null ? stageThree.RemainingHostiles : -1)
                : (stageFourWorldBuilder != null ? stageFourWorldBuilder.RemainingHostiles : -1);
            int total = stageNumber == 3
                ? (stageThree != null ? stageThree.TotalHostiles : 0)
                : (stageFourWorldBuilder != null ? stageFourWorldBuilder.TotalHostiles : 0);
            string objective;
            if (stageNumber == 3 && (stageThree == null || total <= 0))
                objective = "STAGE 3 INITIALIZING...";
            else if (total > 0 && remaining > 0)
                objective = "HOSTILES  " + remaining + " / " + total;
            else if (stageNumber == 3 && total > 0 && remaining == 0)
                objective = "ALIENS CLEARED — REACH THE CENTRAL LIFT";
            else if (stageNumber == 4 && stageFourWorldBuilder == null)
                objective = "STAGE 4 INITIALIZING...";
            else
                objective = "MOTHERSHIP SECURED";

            if (objectiveStyle == null)
                objectiveStyle = new GUIStyle(smallStyle) { wordWrap = true, alignment = TextAnchor.MiddleLeft };
            objectiveStyle.fontSize = Mathf.RoundToInt(16f * scale);
            GUI.Label(new Rect(panel.x + 10f * scale, panel.y + 2f * scale,
                panel.width - 16f * scale, panel.height - 4f * scale),
                objective, objectiveStyle);
        }
        
        private void DrawStageThreeIntroOverlay()
        {
            float scale = GetUiScale();
            float width = Screen.width * 0.76f;
            float height = 62f * scale;
            Rect panel = new Rect(Screen.width * 0.12f, Screen.height * 0.83f - height, width, height);
            Fill(panel, new Color(0.012f, 0.035f, 0.055f, 0.72f));
            Fill(new Rect(panel.x, panel.y, 4f * scale, panel.height), new Color(0.20f, 0.95f, 1f, 0.95f));
            GUI.Label(new Rect(panel.x + 12f * scale, panel.y + 4f * scale, panel.width - 22f * scale, 25f * scale),
                "STAGE 3  •  THE ALIEN FORTRESS", menuSmallStyle);
            string introMessage = stageThreeWorldBuilder != null &&
                !string.IsNullOrEmpty(stageThreeWorldBuilder.IntroMessage)
                    ? stageThreeWorldBuilder.IntroMessage
                    : "CLEAR ALL HOSTILES TO UNLOCK THE UPLINK";
            GUI.Label(new Rect(panel.x + 12f * scale, panel.y + 31f * scale, panel.width - 22f * scale, 24f * scale),
                introMessage, menuSmallStyle);
        }

        private void DrawStageThreeCinematicOverlay()
        {
            float scale = GetUiScale();
            Fill(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0.01f, 0.025f, 0.045f, 0.12f));
            Rect panel = new Rect(Screen.width * 0.12f, Screen.height * 0.76f, Screen.width * 0.76f, 66f * scale);
            Fill(panel, new Color(0.015f, 0.05f, 0.075f, 0.88f));
            Fill(new Rect(panel.x, panel.y, 4f * scale, panel.height), new Color(0.20f, 1f, 0.68f, 1f));
            GUI.Label(new Rect(panel.x + 14f * scale, panel.y + 7f * scale, panel.width - 24f * scale, 28f * scale),
                "CENTRAL UPLINK ACTIVATED", menuHeaderStyle);
            GUI.Label(new Rect(panel.x + 14f * scale, panel.y + 35f * scale, panel.width - 24f * scale, 22f * scale),
                "THE SIGNAL IS TAKING YOU UP...", menuSmallStyle);
        }

        private void DrawStageTwoCinematicOverlay()
        {
            if (stageTwoVictorySequence == null) return;

            Rect fullScreen = new Rect(0f, 0f, Screen.width, Screen.height);
            float flash = stageTwoVictorySequence.FlashAlpha;
            if (flash > 0f)
            {
                // A fast white hit and green energy tint signal the stealth field
                // collapse; the ship itself materializes in place behind this flash.
                Fill(fullScreen, new Color(1f, 1f, 1f, Mathf.Clamp01(flash * 0.86f)));
                Fill(fullScreen, new Color(0.48f, 1f, 0.62f, Mathf.Clamp01(flash * 0.40f)));
            }

            float decloakGlow = stageTwoVictorySequence.DecloakGlowAlpha;
            if (decloakGlow > 0.001f)
                Fill(fullScreen, new Color(0.38f, 1f, 0.54f, decloakGlow * 0.09f));

            string caption = stageTwoVictorySequence.GetCaption();
            if (!string.IsNullOrEmpty(caption) && flash < 0.05f)
            {
                float scale = GetUiScale();
                Rect captionRect = new Rect(0f, Screen.height * 0.13f, Screen.width, 56f * scale);
                Fill(new Rect(Screen.width * 0.12f, captionRect.y, Screen.width * 0.76f, captionRect.height),
                    new Color(0.015f, 0.035f, 0.055f, 0.78f));
                GUI.Label(captionRect, caption, menuHeaderStyle);
            }

            // Keep every competitor's final position visible while the ship appears,
            // boards the hero, ascends and the departure loading bar fills.
            DrawStageTwoFinalResultsOverlay();
            if (stageTwoVictorySequence.Phase == StageTwoVictorySequence.SequencePhase.DepartureWait)
                DrawStageTwoDepartureWaitProgress(stageTwoVictorySequence.DepartureWaitProgress);
        }

        private void DrawStageTwoDepartureWaitProgress(float progress)
        {
            float scale = Mathf.Clamp(GetUiScale(), 0.72f, 1.15f);
            float width = Mathf.Min(Screen.width * 0.70f, 520f * scale);
            float height = 54f * scale;
            Rect panel = new Rect((Screen.width - width) * 0.5f,
                Screen.height * 0.79f, width, height);
            Fill(panel, new Color(0.012f, 0.035f, 0.055f, 0.94f));
            GUI.Label(new Rect(panel.x + 12f * scale, panel.y + 4f * scale,
                panel.width - 24f * scale, 20f * scale),
                "PREPARING NEXT MISSION...", menuSmallStyle);
            Rect bar = new Rect(panel.x + 12f * scale, panel.y + 27f * scale,
                panel.width - 24f * scale, 13f * scale);
            Fill(bar, new Color(0.12f, 0.20f, 0.24f, 1f));
            Fill(new Rect(bar.x, bar.y, bar.width * Mathf.Clamp01(progress), bar.height),
                new Color(0.20f, 1f, 0.56f, 1f));
            GUI.Label(new Rect(panel.x, panel.y + 39f * scale, panel.width, 13f * scale),
                Mathf.RoundToInt(Mathf.Clamp01(progress) * 100f) + "%", menuSmallStyle);
        }

        private GUIStyle GetStageTwoFinalLeaderboardStyle(float scale)
        {
            if (stageTwoFinalLeaderboardStyle == null ||
                stageTwoFinalLeaderboardStyleScreenHeight != Screen.height)
            {
                stageTwoFinalLeaderboardStyle = new GUIStyle(menuSmallStyle);
                stageTwoFinalLeaderboardStyle.alignment = TextAnchor.UpperLeft;
                stageTwoFinalLeaderboardStyle.wordWrap = false;
                stageTwoFinalLeaderboardStyle.clipping = TextClipping.Clip;
                stageTwoFinalLeaderboardStyle.fontSize =
                    Mathf.Clamp(Mathf.RoundToInt(14f * scale), 12, 18);
                stageTwoFinalLeaderboardStyle.padding = new RectOffset(8, 4, 2, 2);
                stageTwoFinalLeaderboardStyle.normal.textColor = new Color(0.94f, 0.98f, 1f, 1f);
                stageTwoFinalLeaderboardStyleScreenHeight = Screen.height;
            }
            return stageTwoFinalLeaderboardStyle;
        }

        private void DrawStageTwoFinalResultsOverlay()
        {
            StageTwoArenaManager arena = StageTwoArenaManager.Instance;
            string standings = arena != null
                ? arena.GetFullLeaderboardText()
                : "1. YOU   0";

            float scale = Mathf.Clamp(GetUiScale(), 0.72f, 1.15f);
            float width = Mathf.Min(Screen.width * 0.47f, 390f * scale);
            float height = Mathf.Min(270f * scale, Screen.height * 0.66f);
            Rect panel = new Rect(8f * scale, Screen.height * 0.27f, width, height);

            Fill(panel, new Color(0.012f, 0.035f, 0.055f, 0.90f));
            Fill(new Rect(panel.x, panel.y, 4f * scale, panel.height),
                new Color(0.24f, 0.96f, 0.49f, 0.98f));
            GUI.Label(new Rect(panel.x + 10f * scale, panel.y + 5f * scale,
                panel.width - 18f * scale, 30f * scale),
                "FINAL RANKING  •  ALL PLAYERS", menuSmallStyle);
            GUI.Label(new Rect(panel.x + 8f * scale, panel.y + 34f * scale,
                panel.width - 14f * scale, panel.height - 39f * scale),
                standings, GetStageTwoFinalLeaderboardStyle(scale));
        }

        private void CompleteStageTwoVictoryTransition()
        {
            if (stageTwoVictorySequence == null) return;
            string result = GetStageTwoResultMessage();
            if (!stageTestRun || stageChainTest) stageTwoVictorySequence.PreserveShipForStageThree();
            stageTwoVictorySequence.FinishAndCleanup();
            Destroy(stageTwoVictorySequence);
            stageTwoVictorySequence = null;
            StageTwoArenaManager.Instance?.ResetForStageChange();
            GateGameplay(false);
            if (activeCamera != null) activeCamera.enabled = false;
            if (androidRuntimeCamera != null) androidRuntimeCamera.enabled = false;
            if (followCamera != null) followCamera.enabled = false;
            if (playerRoot != null) playerRoot.SetActive(false);
            if (gameRoot != null) gameRoot.SetActive(false);
            Time.timeScale = 1f;
            stageTwoLobbyRunning = false;
            stageTwoLobbyReady = false;
            stageTwoLobbyPlayers = 1;
            matchPaused = false;
            spawnChosen = false;
            resultOverlayStartTime = -1f;

            if (stageTestRun && !stageChainTest)
            {
                RestoreCampaignProgressAfterTest();
                mode = ScreenMode.MainMenu;
                ShowLobbyNotice("STAGE 2 TEST COMPLETE  •  CAMPAIGN PROGRESS PRESERVED");
                StartupCheckpoint.Set("StageTwoTestComplete_CampaignUnchanged_" + result);
                return;
            }

            // Release of the previous battlefield is deliberately deferred to the
            // transition coroutine, after its loading panel has rendered for one frame.
            // The campaign checkpoint has already been committed before any teardown.
            stageNumber = 3;
            PlayerPrefs.SetInt("PW.Stage3Unlocked", 1);
            PlayerPrefs.SetInt("PW.CampaignStage", 3);
            PlayerPrefs.Save();
            spawnWorld = new Vector2(0f, -97f);
            spawnChosen = false;
            if (GameSession.Instance != null) GameSession.Instance.ResetMatch();
            StartupCheckpoint.Set("StageTwoCutsceneComplete_AutoStartStage3Transport_" + result);
            BeginStageTransition(3, ScreenMode.HeroSelect);
        }

        private void BeginStageTransition(int targetStage, ScreenMode nextScreen)
        {
            if (stageTransitionInProgress) return;
            transitionError = string.Empty;
            transitionRetryTargetStage = Mathf.Clamp(targetStage, 1, 4);
            transitionRetryScreen = nextScreen;
            transitionRetryStartsMatch = false;
            stageTransitionInProgress = true;
            StartupMemoryMonitor.SetLoading(true, "TransitionToStage" + targetStage);
            startingMatch = true;
            startupStatus = "Preparing next mission...";
            matchInputArmed = false;
            mode = ScreenMode.MainMenu;
            stageNumber = transitionRetryTargetStage;
            PlayerPrefs.SetInt("PW.SelectedHero", Mathf.Clamp(selectedHero, 0, HeroNames.Length - 1));
            PlayerPrefs.SetInt("PW.SelectedWeaponKind", selectedStoreWeaponKind);
            if (!stageTestRun && targetStage >= 2 && targetStage <= 4)
            {
                PlayerPrefs.SetInt("PW.CampaignStage", targetStage);
                PlayerPrefs.SetString("PW.PendingScreen", nextScreen.ToString());
                PlayerPrefs.Save();
                StartupCheckpoint.Set("StageTransitionPendingScreenPersistedBeforeLoading_Stage" + targetStage);
            }
            Time.timeScale = 1f;
            matchPaused = false;
            GateGameplay(false);
            if (GameSession.Instance != null) GameSession.Instance.ResetMatch();
            if (targetStage == 4)
            {
                PlayerPrefs.SetInt("PW.Stage4Unlocked", 1);
                PlayerPrefs.SetInt("PW.CampaignStage", 4);
                PlayerPrefs.Save();
            }
            StartupCheckpoint.Set("BeginStageTransition_" + targetStage + "_" + nextScreen);
            StartCoroutine(RunStageTransitionCleanup(targetStage, nextScreen));
        }

        private IEnumerator TransitionResourceCleanupRoutine(int targetStage, ScreenMode nextScreen)
        {
            // Always render the loading panel before doing expensive scene/resource work.
            yield return null;
            if (!stageTestRun && targetStage >= 2 && targetStage <= 4)
            {
                PlayerPrefs.SetInt("PW.CampaignStage", targetStage);
                PlayerPrefs.SetInt("PW.SelectedHero", Mathf.Clamp(selectedHero, 0, HeroNames.Length - 1));
                PlayerPrefs.SetInt("PW.SelectedWeaponKind", selectedStoreWeaponKind);
                PlayerPrefs.SetString("PW.PendingScreen", nextScreen.ToString());
                PlayerPrefs.Save();
                StartupCheckpoint.Set("StageTransitionSceneReloadPersisted_Stage" + targetStage + "_" + nextScreen);

                // Clear scene-bound/static services that could otherwise keep references
                // to the outgoing arena. Scene reload itself releases scene-owned meshes
                // and materials without destroying RuntimeMaterialFactory's cached assets.
                StageTwoArenaManager.Instance?.ResetForStageChange();
                if (GameSession.Instance != null) GameSession.Instance.ResetMatch();
                StageTwoVictorySequence.DiscardRetainedShipForSceneReload();
                if (stageTwoVictorySequence != null)
                {
                    stageTwoVictorySequence.FinishAndCleanup();
                    Destroy(stageTwoVictorySequence);
                    stageTwoVictorySequence = null;
                }
                if (stageThreeWorldBuilder != null)
                {
                    stageThreeWorldBuilder.OnEndingComplete -= HandleStageThreeEndingComplete;
                    stageThreeWorldBuilder.OnBuildFailed -= HandleStageThreeBuildFailed;
                }
                GraphicsQualityProfile.ReleaseStageEffects();
                BloodFX.ClearAll();
                ModelPreview.ReleaseAll();
                StartupCheckpoint.Set("StageTransitionSceneReloadBegin_Stage" + targetStage);
                AsyncOperation sceneReload = SceneManager.LoadSceneAsync(gameObject.scene.name, LoadSceneMode.Single);
                if (sceneReload == null)
                    throw new System.InvalidOperationException("LoadSceneAsync returned null for gameplay scene.");
                while (!sceneReload.isDone) yield return null;
                yield break;
            }

            if (targetStage == 2)
            {
                ReleaseCompletedWorldBeforeStageTwo();
                // Let deferred Destroy calls be processed before requesting asset unload.
                yield return null;
            }
            else if (targetStage == 3)
            {
                // Stage 3 already has its own loading-screen asset-unload phase in
                // BeginMatchSafely. Avoid performing the same expensive global unload
                // during the short handoff to character selection.
                ReleaseCompletedWorldBeforeStageThree();
                yield return null;
                StartupCheckpoint.Set("StageThreeOldWorldReleased_AssetUnloadDeferredUntilMatchStart");
                yield break;
            }
            else if (targetStage == 4)
            {
                // Stage 3 owns a large generated world. Never destroy all of its
                // geometry synchronously inside the ending callback: render the
                // transition/loading screen first, then dispose it here.
                ReleaseCompletedWorldBeforeStageFour();
                yield return null;
                StartupCheckpoint.Set("StageFourOldWorldReleased_AfterLoadingFrame");
                // Unlike Stage 3, Stage 4 has no separate pre-build unload phase.
                // Finish reclaiming the old fortress assets here before constructing
                // the much larger mothership, reducing peak Android memory use.
            }

            AsyncOperation unload = Resources.UnloadUnusedAssets();
            while (unload != null && !unload.isDone) yield return null;
            // Do not force a full managed GC on the handoff frame; Android can stall or
            // be killed while both the previous arena and the next mission are in memory.
            yield return null;
        }

        private IEnumerator RunCoroutineSafely(System.Collections.IEnumerator routine,
            System.Action<System.Exception> onFailure, System.Action onComplete)
        {
            while (true)
            {
                bool moved = false;
                object current = null;
                System.Exception failure = null;
                try
                {
                    moved = routine.MoveNext();
                    if (moved) current = routine.Current;
                }
                catch (System.Exception ex) { failure = ex; }
                if (failure != null)
                {
                    onFailure?.Invoke(failure);
                    yield break;
                }
                if (!moved)
                {
                    onComplete?.Invoke();
                    yield break;
                }
                yield return current;
            }
        }

        private IEnumerator RunStageTransitionCleanup(int targetStage, ScreenMode nextScreen)
        {
            System.Exception cleanupError = null;
            bool cleanupComplete = false;
            Coroutine cleanupCoroutine = null;
            float startedAt = Time.realtimeSinceStartup;
            try
            {
                cleanupCoroutine = StartCoroutine(RunCoroutineSafely(
                    TransitionResourceCleanupRoutine(targetStage, nextScreen),
                    ex => cleanupError = ex,
                    () => cleanupComplete = true));
                while (!cleanupComplete && cleanupError == null &&
                    Time.realtimeSinceStartup - startedAt < 20f)
                    yield return null;
                if (!cleanupComplete && cleanupError == null)
                {
                    cleanupError = new System.TimeoutException("Stage transition cleanup exceeded 20 seconds.");
                    if (cleanupCoroutine != null) StopCoroutine(cleanupCoroutine);
                }
            }
            finally
            {
                Time.timeScale = 1f;
                startingMatch = false;
                matchInputArmed = false;
                stageTransitionInProgress = false;
                StartupMemoryMonitor.SetLoading(false);
                stageThreeTransitionInProgress = false;
                mode = nextScreen;
                if (cleanupError != null)
                {
                    string message = "Transition to Stage " + targetStage + " failed: " + cleanupError.Message;
                    Debug.LogError("PERSIA_TRANSITION: " + message + "\n" + cleanupError);
                    ShowTransitionError(targetStage, nextScreen, false, message);
                }
                else
                {
                    transitionError = string.Empty;
                    StartupCheckpoint.Set("StageTransitionCleanupComplete_Stage" + targetStage);
                }
            }
        }

        private void ShowTransitionError(int targetStage, ScreenMode retryScreen, bool retryStartsMatch, string message)
        {
            transitionRetryTargetStage = Mathf.Clamp(targetStage, 1, 4);
            transitionRetryScreen = retryScreen;
            transitionRetryStartsMatch = retryStartsMatch;
            transitionError = string.IsNullOrEmpty(message) ? "An unexpected transition error occurred." : message;
            startingMatch = false;
            stageTransitionInProgress = false;
            stageThreeTransitionInProgress = false;
            matchInputArmed = false;
            Time.timeScale = 1f;
            GateGameplay(false);
            mode = retryScreen;
            Debug.LogError("PERSIA_TRANSITION_ERROR: " + transitionError);
            StartupCheckpoint.Set("StageTransitionFailed_Stage" + targetStage);
        }

        private Rect GetTransitionErrorButtonRect()
        {
            float scale = GetUiScale();
            float width = Mathf.Min(Screen.width - 56f * scale, 380f * scale);
            float height = Mathf.Min(Screen.height - 40f * scale, 300f * scale);
            float top = (Screen.height - height) * 0.5f;
            return new Rect((Screen.width - width) * 0.5f, top + height - 58f * scale, width, 48f * scale);
        }

        private void HandleTransitionErrorTouches()
        {
            if (!Application.isMobilePlatform || Input.touchCount <= 0) return;
            Rect button = GetTransitionErrorButtonRect();
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.phase != TouchPhase.Began) continue;
                Vector2 point = new Vector2(touch.position.x, Screen.height - touch.position.y);
                if (button.Contains(point))
                {
                    RetryFailedTransition();
                    return;
                }
            }
        }

        private void DrawTransitionErrorPanel()
        {
            float scale = GetUiScale();
            float width = Mathf.Min(Screen.width - 40f * scale, 700f * scale);
            float height = Mathf.Min(Screen.height - 40f * scale, 300f * scale);
            Rect panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            Fill(panel, new Color(0.045f, 0.025f, 0.03f, 0.98f));
            Fill(new Rect(panel.x, panel.y, panel.width, 5f * scale), new Color(1f, 0.20f, 0.16f, 1f));
            GUI.Label(new Rect(panel.x + 18f * scale, panel.y + 18f * scale,
                panel.width - 36f * scale, 44f * scale), "MISSION TRANSITION FAILED", menuHeaderStyle);
            GUIStyle wrapped = new GUIStyle(menuSmallStyle)
            {
                wordWrap = true,
                alignment = TextAnchor.MiddleCenter,
                fontSize = Mathf.RoundToInt(15f * scale)
            };
            GUI.Label(new Rect(panel.x + 20f * scale, panel.y + 72f * scale,
                panel.width - 40f * scale, 88f * scale), transitionError, wrapped);
            if (GUI.Button(GetTransitionErrorButtonRect(), "RETRY", buttonStyle)) RetryFailedTransition();
        }

        private void RetryFailedTransition()
        {
            int target = transitionRetryTargetStage;
            ScreenMode next = transitionRetryScreen;
            bool retryBuild = transitionRetryStartsMatch;
            transitionError = string.Empty;
            if (retryBuild)
            {
                stageNumber = target;
                spawnChosen = true;
                mode = ScreenMode.MainMenu;
                StartMatch();
            }
            else BeginStageTransition(target, next);
        }

        private void GoToMainMenu()
        {
            StageTwoArenaManager.Instance?.ResetForStageChange();
            Time.timeScale = 1f;
            if (stageTwoVictorySequence != null)
            {
                stageTwoVictorySequence.FinishAndCleanup();
                Destroy(stageTwoVictorySequence);
                stageTwoVictorySequence = null;
            }
            if (mobileInput != null) mobileInput.DeactivateMatch();
            GateGameplay(false);
            if (activeCamera != null) activeCamera.enabled = false;
            if (androidRuntimeCamera != null) androidRuntimeCamera.enabled = false;
            if (followCamera != null) followCamera.enabled = false;
            if (playerRoot != null) playerRoot.SetActive(false);
            if (gameRoot != null) gameRoot.SetActive(false);
            startingMatch = false;
            matchPaused = false;
            resultOverlayStartTime = -1f;
            spawnChosen = false;
            if (stageTestRun) RestoreCampaignProgressAfterTest();
            mode = ScreenMode.MainMenu;
            Time.timeScale = 1f;
            StartupCheckpoint.Set("GoToMainMenu_NoProgression");
        }

        private void HandleStageThreeEndingComplete()
        {
            // This callback is raised inside StageThreeWorldBuilder's own coroutine.
            // Never deactivate/destroy that coroutine owner from inside its callback.
            if (stageNumber != 3) return;
            stageThreeEndingPending = true;
            StartupCheckpoint.Set("StageThreeEndingEventQueuedForFlowCoroutine");
        }

        private IEnumerator StartStageFourOnNextFrame()
        {
            yield return null;
            if (this == null || !isActiveAndEnabled) yield break;
            StartStageFourAfterStageThree();
        }

        private void HandleStageThreeBuildFailed(string message)
        {
            if (stageNumber == 3)
                ShowTransitionError(3, ScreenMode.HeroSelect, true, message);
        }

        private void StartStageFourAfterStageThree()
        {
            if (stageThreeTransitionInProgress || stageTransitionInProgress) return;
            stageThreeTransitionInProgress = true;
            try
            {
                StageTwoArenaManager.Instance?.ResetForStageChange();
                Time.timeScale = 1f;
                if (GameSession.Instance != null) GameSession.Instance.ResetMatch();
                if (stageTwoVictorySequence != null)
                {
                    stageTwoVictorySequence.FinishAndCleanup();
                    Destroy(stageTwoVictorySequence);
                    stageTwoVictorySequence = null;
                }
                GateGameplay(false);
                if (activeCamera != null) activeCamera.enabled = false;
                if (androidRuntimeCamera != null) androidRuntimeCamera.enabled = false;
                if (followCamera != null) followCamera.enabled = false;
                if (playerRoot != null) playerRoot.SetActive(false);
                if (gameRoot != null) gameRoot.SetActive(false);

                if (stageTestRun && !stageChainTest)
                {
                    RestoreCampaignProgressAfterTest();
                    mode = ScreenMode.MainMenu;
                    ShowLobbyNotice("STAGE 3 TEST COMPLETE  •  CAMPAIGN PROGRESS PRESERVED");
                    StartupCheckpoint.Set("StageThreeTestComplete_ReturnToMenu");
                    return;
                }

                // Persist the next stage before any deferred world teardown. If
                // Android reclaims the process during cleanup, Continue resumes Stage 4.
                stageNumber = 4;
                PlayerPrefs.SetInt("PW.Stage4Unlocked", 1);
                PlayerPrefs.SetInt("PW.CampaignStage", 4);
                PlayerPrefs.Save();
                StartupCheckpoint.Set("StageThreeVictory_Stage4ProgressSavedBeforeCleanup");
                stageTwoOriginalHero = selectedHero;
                stageTwoShopOpen = false;
                stageTwoLobbyRunning = false;
                stageTwoLobbyReady = false;
                stageTwoLobbyPlayers = 1;
                selectedStoreWeaponKind = PlayerPrefs.GetInt("PW.SelectedWeaponKind",
                    (int)WeaponController.WeaponKind.AssaultRifle);
                if (selectedStoreWeaponKind == (int)WeaponController.WeaponKind.AlienSword)
                    selectedStoreWeaponKind = (int)WeaponController.WeaponKind.AssaultRifle;
                spawnWorld = Vector2.zero;
                spawnChosen = false;
                BeginStageTransition(4, ScreenMode.HeroSelect);
            }
            catch (System.Exception ex)
            {
                ShowTransitionError(4, ScreenMode.HeroSelect, false, ex.ToString());
            }
            finally
            {
                stageThreeTransitionInProgress = false;
            }
        }

        public void CompleteStageFourEscapeToMainMenu()
        {
            Time.timeScale = 1f;
            GraphicsQualityProfile.ReleaseStageEffects();
            BloodFX.ClearAll();
            ModelPreview.ReleaseAll();
            StageTwoArenaManager.Instance?.ResetForStageChange();

            if (stageTwoVictorySequence != null)
            {
                stageTwoVictorySequence.FinishAndCleanup();
                Destroy(stageTwoVictorySequence);
                stageTwoVictorySequence = null;
            }

            if (mobileInput != null) mobileInput.DeactivateMatch();
            GateGameplay(false);
            if (activeCamera != null) activeCamera.enabled = false;
            if (androidRuntimeCamera != null) androidRuntimeCamera.enabled = false;
            if (followCamera != null) followCamera.enabled = false;

            StageFourEscapeSequence escape = gameRoot != null
                ? gameRoot.GetComponent<StageFourEscapeSequence>() : null;
            if (escape != null) Destroy(escape);

            StageFourWorldBuilder.ClearExistingWorld();
            if (stageFourWorldBuilder != null)
            {
                Destroy(stageFourWorldBuilder);
                stageFourWorldBuilder = null;
            }

            if (playerRoot != null) playerRoot.SetActive(false);
            if (gameRoot != null) gameRoot.SetActive(false);

            startingMatch = false;
            matchPaused = false;
            resultOverlayStartTime = -1f;
            stageTwoLobbyRunning = false;
            spawnChosen = false;

            if (stageTestRun)
            {
                RestoreCampaignProgressAfterTest();
                stageChainTest = false;
                mode = ScreenMode.MainMenu;
                ShowLobbyNotice("STAGE 4 TEST COMPLETE  •  CAMPAIGN PROGRESS PRESERVED");
                StartupCheckpoint.Set("StageFourTestComplete_ReturnToMainMenu");
                return;
            }

            stageNumber = 5;
            PlayerPrefs.SetInt("PW.Stage4Unlocked", 1);
            PlayerPrefs.SetInt("PW.Stage4Completed", 1);
            PlayerPrefs.SetInt("PW.CampaignStage", 5);
            PlayerPrefs.Save();
            mode = ScreenMode.MainMenu;
            ShowLobbyNotice("STAGE 4 COMPLETE  •  MOTHERSHIP DESTROYED");
            StartupCheckpoint.Set("StageFourCampaignComplete_ReturnToMainMenu");
        }

        private void SaveProfileAndContinue()
        {
            string cleanName = string.IsNullOrWhiteSpace(profileNameInput)
                ? "Persian Warrior" : profileNameInput.Trim();
            profileNameInput = cleanName.Length > 20 ? cleanName.Substring(0, 20) : cleanName;
            PlayerPrefs.SetString("PW.PlayerName", profileNameInput);
            PlayerPrefs.SetInt("PW.ProfileCreated", 1);

            bool continueToStageTwo = PlayerPrefs.GetInt("PW.RegistrationPending", 0) != 0 &&
                stageNumber == 2;
            PlayerPrefs.SetInt("PW.RegistrationPending", 0);
            PlayerPrefs.Save();

            if (continueToStageTwo)
            {
                stageTwoOriginalHero = selectedHero;
                stageTwoShopOpen = false;
                stageTwoLobbyRunning = false;
                stageTwoLobbyReady = false;
                stageTwoLobbyPlayers = 1;
                spawnChosen = false;
                mode = ScreenMode.HeroSelect;
                StartupCheckpoint.Set("PlayerProfileSaved_StageTwoHeroSelectionOpened");
            }
            else
            {
                mode = ScreenMode.MainMenu;
                StartupCheckpoint.Set("PlayerProfileSaved_ReturnToMainMenu");
            }
        }

        private void DrawProfileScreen()
        {
            bool registrationPending = PlayerPrefs.GetInt("PW.RegistrationPending", 0) != 0;
            float width = Mathf.Min(Screen.width - 64f, 760f);
            Rect panel = new Rect((Screen.width - width) * 0.5f, Screen.height * 0.19f, width, Screen.height * 0.58f);
            Fill(panel, new Color(0.025f, 0.055f, 0.085f, 0.94f));
            Fill(new Rect(panel.x, panel.y, panel.width, 6f), new Color(0.95f, 0.69f, 0.20f, 1f));
            GUI.Label(new Rect(panel.x + 20f, panel.y + 18f, panel.width - 40f, 52f),
                registrationPending ? "CREATE PLAYER PROFILE" : "PLAYER PROFILE", menuHeaderStyle);
            GUI.Label(new Rect(panel.x + 32f, panel.y + 88f, panel.width - 64f, 36f), "DISPLAY NAME (MAX 20 CHARACTERS)", menuSmallStyle);
            profileNameInput = GUI.TextField(new Rect(panel.x + 40f, panel.y + 132f, panel.width - 80f, 58f),
                profileNameInput, 20);
            GUI.Label(new Rect(panel.x + 32f, panel.y + 208f, panel.width - 64f, 34f), "PLAYER ID  " + playerId, menuSmallStyle);
            GUI.Label(new Rect(panel.x + 32f, panel.y + 250f, panel.width - 64f, 34f),
                "PROFILE COSMETIC  " + StoreCosmeticNames[Mathf.Clamp(selectedProfileCosmetic, 0, StoreCosmeticNames.Length - 1)],
                menuSmallStyle);
            if (DrawFantasyButton(GetLobbyBackRect(),
                registrationPending ? "SAVE PROFILE & CONTINUE" : "SAVE & BACK"))
                SaveProfileAndContinue();
        }

        private Rect GetStoreItemRect(int index)
        {
            const int columns = 3;
            float gap = 12f;
            float totalWidth = Mathf.Min(Screen.width - 44f, 1050f);
            float cardWidth = (totalWidth - gap * (columns - 1)) / columns;
            float cardHeight = Mathf.Min(220f, Screen.height * 0.27f);
            float left = (Screen.width - totalWidth) * 0.5f;
            int column = index % columns;
            int row = index / columns;
            float top = Screen.height * 0.25f;
            return new Rect(left + column * (cardWidth + gap), top + row * (cardHeight + gap), cardWidth, cardHeight);
        }

        private Rect GetStoreActionRect(int index)
        {
            Rect card = GetStoreItemRect(index);
            return new Rect(card.x + 10f, card.yMax - 44f, card.width - 20f, 34f);
        }

        private bool IsStoreItemOwned(int index)
        {
            if (index < 3)
                return PlayerPrefs.GetInt("PW.CosmeticOwned." + index, index == 0 ? 1 : 0) != 0;
            string rewardKey = index == 3 ? "PW.Reward.VoidRifle" : "PW.Reward.AlienSword";
            return PlayerPrefs.GetInt(rewardKey, 0) != 0;
        }

        private bool IsStoreItemEquipped(int index)
        {
            if (index < 3) return selectedProfileCosmetic == index;
            int kind = index == 3 ? (int)WeaponController.WeaponKind.VoidRifle : (int)WeaponController.WeaponKind.AlienSword;
            return selectedStoreWeaponKind == kind;
        }

        private void PurchaseOrEquipCosmetic(int index)
        {
            if (index >= 3)
            {
                string rewardKey = index == 3 ? "PW.Reward.VoidRifle" : "PW.Reward.AlienSword";
                bool ownedWeapon = PlayerPrefs.GetInt(rewardKey, 0) != 0;
                if (!ownedWeapon)
                {
                    int price = StoreCosmeticPrices[index];
                    if (storeCoins < price) return;
                    storeCoins -= price;
                    PlayerPrefs.SetInt("PW.StoreCoins", storeCoins);
                    PlayerPrefs.SetInt(rewardKey, 1);
                }
                selectedStoreWeaponKind = index == 3
                    ? (int)WeaponController.WeaponKind.VoidRifle
                    : (int)WeaponController.WeaponKind.AlienSword;
                PlayerPrefs.SetInt("PW.SelectedWeaponKind", selectedStoreWeaponKind);
                PlayerPrefs.Save();
                return;
            }

            string ownedKey = "PW.CosmeticOwned." + index;
            bool owned = IsStoreItemOwned(index);
            if (!owned)
            {
                int price = StoreCosmeticPrices[index];
                if (storeCoins < price) return;
                storeCoins -= price;
                PlayerPrefs.SetInt("PW.StoreCoins", storeCoins);
                PlayerPrefs.SetInt(ownedKey, 1);
            }
            selectedProfileCosmetic = index;
            PlayerPrefs.SetInt("PW.ProfileCosmetic", index);
            PlayerPrefs.Save();
        }

        private void DrawStoreScreen()
        {
            storeCoins = PlayerPrefs.GetInt("PW.StoreCoins", 500);
            GUI.Label(new Rect(0f, 118f, Screen.width, 54f), "PERSIA STORE", menuHeaderStyle);
            GUI.Label(new Rect(0f, 166f, Screen.width, 36f), "OFFLINE DEMO STORE  •  NO REAL-MONEY PAYMENTS", menuSmallStyle);
            GUI.Label(new Rect(Screen.width - 300f, 24f, 270f, 42f), "COINS  " + storeCoins, menuBodyStyle);

            for (int i = 0; i < StoreCosmeticNames.Length; i++)
            {
                Rect card = GetStoreItemRect(i);
                bool owned = IsStoreItemOwned(i);
                bool equipped = IsStoreItemEquipped(i);
                Fill(card, new Color(0.025f, 0.055f, 0.085f, 0.78f));
                Fill(new Rect(card.x, card.y, card.width, 4f), equipped
                    ? new Color(0.24f, 0.92f, 0.98f, 0.95f) : new Color(0.38f, 0.50f, 0.60f, 0.72f));
                Rect emblem = new Rect(card.center.x - 29f, card.y + 14f, 58f, 58f);
                Color emblemColor = i == 0 ? new Color(0.83f, 0.57f, 0.16f)
                    : i == 1 ? new Color(0.89f, 0.73f, 0.25f)
                    : i == 2 ? new Color(0.14f, 0.52f, 0.79f)
                    : i == 3 ? new Color(0.08f, 0.72f, 0.98f) : new Color(0.20f, 0.95f, 0.58f);
                if (i == 3)
                    ModelPreview.Draw(emblem, ModelKind.VoidRifle);
                else if (i == 4)
                    ModelPreview.Draw(emblem, ModelKind.AlienSword);
                else
                    Fill(emblem, emblemColor);
                GUI.Label(new Rect(card.x + 6f, card.y + 76f, card.width - 12f, 34f), StoreCosmeticNames[i], menuSmallStyle);
                GUI.Label(new Rect(card.x + 6f, card.y + 110f, card.width - 12f, 26f),
                    owned ? (equipped ? "EQUIPPED" : "OWNED") : (StoreCosmeticPrices[i] + " COINS"),
                    menuBodyStyle);
                Rect action = GetStoreActionRect(i);
                if (DrawFantasyButton(action, owned ? (equipped ? "EQUIPPED" : (i >= 3 ? "EQUIP WEAPON" : "EQUIP")) : "UNLOCK"))
                    PurchaseOrEquipCosmetic(i);
            }
            if (DrawFantasyButton(GetLobbyBackRect(), "BACK TO MENU"))
            {
                ModelPreview.ReleaseAll();
                mode = ScreenMode.MainMenu;
            }
        }

        private Rect GetSettingsSoundRect()
        {
            float width = Mathf.Min(Screen.width - 64f, 640f);
            return new Rect((Screen.width - width) * 0.5f, Screen.height * 0.42f, width, 62f);
        }

        private void DrawSettingsScreen()
        {
            float width = Mathf.Min(Screen.width - 64f, 700f);
            Rect panel = new Rect((Screen.width - width) * 0.5f, Screen.height * 0.23f, width, Screen.height * 0.48f);
            Fill(panel, new Color(0.025f, 0.055f, 0.085f, 0.94f));
            Fill(new Rect(panel.x, panel.y, panel.width, 6f), new Color(0.95f, 0.69f, 0.20f, 1f));
            GUI.Label(new Rect(panel.x + 20f, panel.y + 18f, panel.width - 40f, 52f), "SETTINGS", menuHeaderStyle);
            if (DrawFantasyButton(GetSettingsSoundRect(), soundEnabled ? "SOUND  •  ON" : "SOUND  •  OFF", soundEnabled))
            {
                soundEnabled = !soundEnabled;
                AudioListener.volume = soundEnabled ? 1f : 0f;
                PlayerPrefs.SetInt("PW.SoundEnabled", soundEnabled ? 1 : 0);
                PlayerPrefs.Save();
            }
            GUI.Label(new Rect(panel.x + 24f, panel.y + panel.height - 72f, panel.width - 48f, 42f),
                "PROFILE AND STORE DATA ARE SAVED ON THIS DEVICE", menuSmallStyle);
            if (DrawFantasyButton(GetLobbyBackRect(), "BACK TO MENU")) mode = ScreenMode.MainMenu;
        }

        private void ContinueFromHeroSelection()
        {
            stageTwoShopOpen = false;

            if (stageNumber == 3)
            {
                // Stage 3 has its own transport-arrival cinematic and authored landing.
                // Retain the canonical entry coordinate; never ask the player to pick a map point.
                spawnWorld = new Vector2(0f, -97f);
                spawnChosen = true;
                mode = ScreenMode.MainMenu;
                StartupCheckpoint.Set("StageThreeHeroSelection_FixedTransportEntry");
                StartMatch();
                return;
            }

            if (stageNumber == 4)
            {
                // Stage 4's arrival sequence positions the pilot inside the mothership.
                // Keep its authored landing; no drop-map step is needed.
                spawnWorld = Vector2.zero;
                spawnChosen = false;
                selectedStoreWeaponKind = PlayerPrefs.GetInt(
                    "PW.SelectedWeaponKind", (int)WeaponController.WeaponKind.AssaultRifle);
                if (selectedStoreWeaponKind == (int)WeaponController.WeaponKind.AlienSword)
                    selectedStoreWeaponKind = (int)WeaponController.WeaponKind.AssaultRifle;
                mode = ScreenMode.WeaponSelect;
                StartupCheckpoint.Set("StageFourHeroSelected_OpenWeaponSelection");
                return;
            }

            EnterDropMap();
        }

        private static readonly WeaponController.WeaponKind[] CampaignWeaponChoices =
        {
            WeaponController.WeaponKind.LightPistol,
            WeaponController.WeaponKind.AssaultRifle,
            WeaponController.WeaponKind.HeavyMachineGun,
            WeaponController.WeaponKind.Axe,
            WeaponController.WeaponKind.VoidRifle,
            WeaponController.WeaponKind.AlienSword
        };

        private static readonly string[] CampaignWeaponLabels =
        {
            "LIGHT PISTOL",
            "ASSAULT RIFLE",
            "HEAVY MACHINE GUN",
            "AXE",
            "VOID RIFLE",
            "ALIEN SWORD"
        };

        private bool IsCampaignWeaponAvailable(WeaponController.WeaponKind kind)
        {
            if (kind == WeaponController.WeaponKind.VoidRifle)
                return PlayerPrefs.GetInt("PW.Reward.VoidRifle", 0) != 0;
            if (kind == WeaponController.WeaponKind.AlienSword)
                return PlayerPrefs.GetInt("PW.Reward.AlienSword", 0) != 0;
            return true;
        }

        private Rect GetWeaponSelectPanelRect()
        {
            float scale = GetUiScale();
            float width = Mathf.Min(Screen.width - 28f * scale, 900f * scale);
            float height = Mathf.Min(Screen.height - 24f * scale, 580f * scale);
            return new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f,
                width, height);
        }

        private Rect GetWeaponSelectCardRect(int index)
        {
            Rect panel = GetWeaponSelectPanelRect();
            float scale = GetUiScale();
            float pad = 22f * scale;
            float gapX = 9f * scale;
            float gapY = 9f * scale;
            float cardWidth = (panel.width - pad * 2f - gapX * 2f) / 3f;
            float gridTop = panel.y + 118f * scale;
            float footerTop = panel.y + panel.height - 82f * scale;
            float cardHeight = (footerTop - gridTop - gapY) / 2f;
            return new Rect(panel.x + pad + (index % 3) * (cardWidth + gapX),
                gridTop + (index / 3) * (cardHeight + gapY), cardWidth, cardHeight);
        }

        private Rect GetWeaponSelectStartRect()
        {
            Rect panel = GetWeaponSelectPanelRect();
            float scale = GetUiScale();
            float gap = 10f * scale;
            float buttonHeight = 52f * scale;
            float contentWidth = panel.width - 44f * scale - gap;
            float buttonWidth = contentWidth * 0.62f;
            float groupWidth = contentWidth + gap;
            float left = panel.center.x - groupWidth * 0.5f;
            return new Rect(left, panel.yMax - buttonHeight - 12f * scale,
                buttonWidth, buttonHeight);
        }

        private Rect GetWeaponSelectBackRect()
        {
            Rect panel = GetWeaponSelectPanelRect();
            float scale = GetUiScale();
            float gap = 10f * scale;
            float buttonHeight = 52f * scale;
            float contentWidth = panel.width - 44f * scale - gap;
            float startWidth = contentWidth * 0.62f;
            return new Rect(GetWeaponSelectStartRect().xMax + gap,
                panel.yMax - buttonHeight - 12f * scale, contentWidth - startWidth, buttonHeight);
        }

        private void SelectCampaignWeapon(int index)
        {
            if (index < 0 || index >= CampaignWeaponChoices.Length) return;
            WeaponController.WeaponKind kind = CampaignWeaponChoices[index];
            if (!IsCampaignWeaponAvailable(kind)) return;
            // Stage 4 opens with ranged combat. The owned sword remains available in-match,
            // but is not offered as the sole weapon for the entry encounter.
            if (stageNumber == 4 && kind == WeaponController.WeaponKind.AlienSword) return;
            selectedStoreWeaponKind = (int)kind;
            StartupCheckpoint.Set("CampaignWeaponSelected_" + kind);
        }

        private void StartStageFourWithSelectedLoadout()
        {
            if (stageNumber != 4 || startingMatch) return;
            WeaponController.WeaponKind kind =
                (WeaponController.WeaponKind)selectedStoreWeaponKind;
            if (!IsCampaignWeaponAvailable(kind) || kind == WeaponController.WeaponKind.AlienSword)
                kind = WeaponController.WeaponKind.AssaultRifle;
            PlayerPrefs.SetInt("PW.SelectedWeaponKind", (int)kind);
            PlayerPrefs.Save();
            spawnWorld = Vector2.zero;
            spawnChosen = true;
            mode = ScreenMode.MainMenu;
            StartupCheckpoint.Set("StageFourLoadoutConfirmed_StartAtAuthoredEntry");
            StartMatch();
        }

        private void HandleWeaponSelectTouches()
        {
            if (!Application.isMobilePlatform || Input.touchCount <= 0) return;
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.phase != TouchPhase.Began) continue;
                Vector2 gui = new Vector2(touch.position.x, Screen.height - touch.position.y);
                if (GetWeaponSelectStartRect().Contains(gui))
                {
                    suppressMenuButtonClicksFrame = Time.frameCount;
                    StartStageFourWithSelectedLoadout();
                    return;
                }
                if (GetWeaponSelectBackRect().Contains(gui))
                {
                    suppressMenuButtonClicksFrame = Time.frameCount;
                    mode = ScreenMode.HeroSelect;
                    StartupCheckpoint.Set("StageFourWeaponSelection_BackToHero");
                    return;
                }
                for (int weapon = 0; weapon < CampaignWeaponChoices.Length; weapon++)
                {
                    if (!GetWeaponSelectCardRect(weapon).Contains(gui)) continue;
                    suppressMenuButtonClicksFrame = Time.frameCount;
                    SelectCampaignWeapon(weapon);
                    return;
                }
            }
        }

        private static ModelKind GetCampaignWeaponPreviewKind(WeaponController.WeaponKind kind)
        {
            switch (kind)
            {
                case WeaponController.WeaponKind.LightPistol: return ModelKind.Pistol;
                case WeaponController.WeaponKind.AssaultRifle: return ModelKind.AssaultRifle;
                case WeaponController.WeaponKind.HeavyMachineGun: return ModelKind.HeavyMachineGun;
                case WeaponController.WeaponKind.Axe: return ModelKind.Axe;
                case WeaponController.WeaponKind.VoidRifle: return ModelKind.VoidRifle;
                case WeaponController.WeaponKind.AlienSword: return ModelKind.AlienSword;
                default: return ModelKind.None;
            }
        }

        private void DrawWeaponSelect()
        {
            Rect panel = GetWeaponSelectPanelRect();
            float scale = GetUiScale();
            Fill(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0.005f, 0.015f, 0.03f, 0.88f));
            Fill(new Rect(panel.x + 4f * scale, panel.y + 7f * scale, panel.width, panel.height),
                new Color(0f, 0f, 0f, 0.36f));
            Fill(panel, new Color(0.025f, 0.055f, 0.085f, 0.98f));
            Fill(new Rect(panel.x, panel.y, panel.width, 5f * scale), new Color(0.25f, 0.92f, 0.45f, 1f));
            GUI.Label(new Rect(panel.x + 18f * scale, panel.y + 12f * scale,
                panel.width - 36f * scale, 42f * scale), "STAGE 4  •  SELECT YOUR WEAPON", menuHeaderStyle);
            GUI.Label(new Rect(panel.x + 18f * scale, panel.y + 54f * scale,
                panel.width - 36f * scale, 28f * scale),
                "Choose an owned weapon. The mothership arrival and landing stay unchanged.", menuSmallStyle);

            for (int i = 0; i < CampaignWeaponChoices.Length; i++)
            {
                Rect card = GetWeaponSelectCardRect(i);
                WeaponController.WeaponKind kind = CampaignWeaponChoices[i];
                bool available = IsCampaignWeaponAvailable(kind) &&
                    !(stageNumber == 4 && kind == WeaponController.WeaponKind.AlienSword);
                bool selected = selectedStoreWeaponKind == (int)kind;
                Fill(card, selected
                    ? new Color(0.08f, 0.28f, 0.24f, 0.98f)
                    : new Color(0.055f, 0.085f, 0.12f, 0.96f));
                Fill(new Rect(card.x, card.y, 4f * scale, card.height),
                    selected ? new Color(0.25f, 0.94f, 0.49f, 1f)
                    : new Color(0.36f, 0.46f, 0.52f, 0.8f));
                GUI.Label(new Rect(card.x + 8f * scale, card.y + 8f * scale,
                    card.width - 16f * scale, 36f * scale), CampaignWeaponLabels[i], menuBodyStyle);
                string status = !available ? "LOCKED / NOT AN ENTRY WEAPON"
                    : selected ? "SELECTED" : "TAP TO SELECT";
                GUI.Label(new Rect(card.x + 8f * scale, card.y + 46f * scale,
                    card.width - 16f * scale, 28f * scale), status, menuSmallStyle);

                // Render the actual 3D weapon mesh in each selection card.
                Rect imageRect = new Rect(card.x + 10f * scale, card.y + 76f * scale,
                    card.width - 20f * scale, Mathf.Max(36f * scale, card.height - 84f * scale));
                ModelPreview.Draw(imageRect, GetCampaignWeaponPreviewKind(kind));

                if (GUI.Button(card, GUIContent.none, GUIStyle.none) && available)
                    SelectCampaignWeapon(i);
            }

            GUI.Label(new Rect(panel.x + 18f * scale, panel.yMax - 88f * scale,
                panel.width - 36f * scale, 24f * scale),
                "CURRENT LOADOUT  •  " + CampaignWeaponLabels[Mathf.Clamp(
                    System.Array.IndexOf(CampaignWeaponChoices, (WeaponController.WeaponKind)selectedStoreWeaponKind),
                    0, CampaignWeaponLabels.Length - 1)], menuSmallStyle);

            if (DrawFantasyButton(GetWeaponSelectStartRect(), "START STAGE 4  ⚔", true))
                StartStageFourWithSelectedLoadout();
            if (DrawFantasyButton(GetWeaponSelectBackRect(), "BACK TO PLAYER", false))
                mode = ScreenMode.HeroSelect;
        }

        private void HandleHeroSelectTouches()
        {
            if (!Application.isMobilePlatform || Input.touchCount <= 0)
                return;

            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.phase != TouchPhase.Began)
                    continue;

                Vector2 gui = new Vector2(touch.position.x, Screen.height - touch.position.y);
                if (stageNumber >= 2 && stageTwoShopOpen)
                {
                    Rect shopBack = new Rect(Screen.width * 0.5f - 160f, Screen.height - 104f, 320f, 62f);
                    if (shopBack.Contains(gui))
                    {
                        stageTwoShopOpen = false;
                        suppressMenuButtonClicksFrame = Time.frameCount;
                    }
                    return;
                }

                float gap = 14f;
                float totalWidth = Mathf.Min(Screen.width - 44f, 1120f);
                float cardWidth = (totalWidth - gap * (HeroNames.Length - 1)) / HeroNames.Length;
                float startX = (Screen.width - totalWidth) * 0.5f;
                float top = 230f;
                float cardHeight = Mathf.Max(170f, Mathf.Min(310f * GetUiScale(), Screen.height - 360f));

                for (int hero = 0; hero < HeroNames.Length; hero++)
                {
                    Rect card = new Rect(startX + hero * (cardWidth + gap), top, cardWidth, cardHeight);
                    if (!card.Contains(gui))
                        continue;

                    if (IsHeroUnlocked(hero))
                    {
                        selectedHero = hero;
                        StartupCheckpoint.Set("HeroSelectedByTouch");
                    }
                    else
                    {
                        StartupCheckpoint.Set("LockedHeroTouchIgnored_" + hero);
                    }
                    return;
                }

                if (stageNumber >= 2)
                {
                    float gapButtons = 12f;
                    float shopWidth = Mathf.Clamp(Screen.width * 0.16f, 150f, 230f);
                    float continueWidth = Mathf.Min(420f, Screen.width - 64f - shopWidth - gapButtons);
                    float groupWidth = continueWidth + gapButtons + shopWidth;
                    float left = (Screen.width - groupWidth) * 0.5f;
                    Rect continueRect = new Rect(left, Screen.height - 112f, continueWidth, 62f);
                    Rect shopRect = new Rect(continueRect.xMax + gapButtons, continueRect.y, shopWidth, continueRect.height);
                    if (continueRect.Contains(gui))
                    {
                        suppressMenuButtonClicksFrame = Time.frameCount;
                        ContinueFromHeroSelection();
                        StartupCheckpoint.Set("HeroSelectionContinuedByTouch");
                        return;
                    }
                    if (shopRect.Contains(gui))
                    {
                        suppressMenuButtonClicksFrame = Time.frameCount;
                        stageTwoShopOpen = true;
                        StartupCheckpoint.Set("StageTwoCharacterShopOpened");
                        return;
                    }
                }
                else
                {
                    Rect continueRect = new Rect(Screen.width * 0.5f - 180f, Screen.height - 112f, 360f, 62f);
                    if (continueRect.Contains(gui))
                    {
                        suppressMenuButtonClicksFrame = Time.frameCount;
                        EnterDropMap();
                        StartupCheckpoint.Set("HeroSelectionContinuedByTouch");
                        return;
                    }
                }
            }
        }

        private void DrawHeroSelect()
        {
            GUI.Label(new Rect(0f, 121f, Screen.width, 58f), stageNumber >= 2 ? "STAGE " + stageNumber + "  •  CHOOSE YOUR HERO" : "CHOOSE YOUR HERO", menuHeaderStyle);
            string heroHint = stageNumber >= 2
                ? (IsAnahitaUnlocked()
                    ? "ARDESHIR OR RESCUED ANAHITA ARE AVAILABLE  •  OTHERS LOCKED"
                    : "RESCUE ANAHITA IN STAGE 1 TO UNLOCK HER  •  OTHERS LOCKED")
                : "ONLY ARDESHIR IS AVAILABLE  •  RESCUE ANAHITA FOR STAGE 2";
            GUI.Label(new Rect(24f, 176f, Screen.width - 48f, 36f), heroHint, menuSmallStyle);

            float gap = 14f;
            float totalWidth = Mathf.Min(Screen.width - 44f, 1120f);
            float cardWidth = (totalWidth - gap * (HeroNames.Length - 1)) / HeroNames.Length;
            float startX = (Screen.width - totalWidth) * 0.5f;
            float top = 230f;
            float cardHeight = Mathf.Min(310f, Screen.height - 360f);

            for (int i = 0; i < HeroNames.Length; i++)
            {
                Rect card = new Rect(startX + i * (cardWidth + gap), top, cardWidth, cardHeight);
                bool unlocked = IsHeroUnlocked(i);
                DrawHeroCard(card, i, i == selectedHero, unlocked);
                if (GUI.Button(card, GUIContent.none, GUIStyle.none) && unlocked)
                    selectedHero = i;
            }

            string continueLabel = stageNumber >= 2
                ? (stageNumber == 3
                    ? "CONTINUE TO TRANSPORT"
                    : stageNumber == 4
                        ? "CONTINUE TO WEAPON SELECT"
                        : (selectedHero == stageTwoOriginalHero
                            ? "KEEP CURRENT HERO  •  CONTINUE"
                            : "USE SELECTED HERO  •  CONTINUE"))
                : "SELECT SPAWN POINT";

            if (stageNumber >= 2)
            {
                float gapButtons = 12f;
                float shopWidth = Mathf.Clamp(Screen.width * 0.16f, 150f, 230f);
                float continueWidth = Mathf.Min(420f, Screen.width - 64f - shopWidth - gapButtons);
                float groupWidth = continueWidth + gapButtons + shopWidth;
                float left = (Screen.width - groupWidth) * 0.5f;
                Rect continueRect = new Rect(left, Screen.height - 112f, continueWidth, 62f);
                Rect shopRect = new Rect(continueRect.xMax + gapButtons, continueRect.y, shopWidth, continueRect.height);
                if (DrawFantasyButton(continueRect, continueLabel, true))
                    ContinueFromHeroSelection();
                if (DrawFantasyButton(shopRect, "HERO SHOP  ✦", false))
                    stageTwoShopOpen = true;
            }
            else
            {
                Rect continueRect = new Rect(Screen.width * 0.5f - 180f, Screen.height - 112f, 360f, 62f);
                if (DrawFantasyButton(continueRect, continueLabel, true))
                    EnterDropMap();
            }
        }

        private void DrawCharacterShop()
        {
            Fill(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0.01f, 0.025f, 0.05f, 0.86f));
            float scale = GetUiScale();
            float width = Mathf.Min(Screen.width - 48f, 780f);
            float height = Mathf.Min(Screen.height - 56f, 430f);
            Rect panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            Fill(panel, new Color(0.04f, 0.07f, 0.11f, 0.98f));
            Fill(new Rect(panel.x, panel.y, panel.width, 6f * scale), new Color(0.96f, 0.70f, 0.20f, 1f));
            GUI.Label(new Rect(panel.x + 20f, panel.y + 24f, panel.width - 40f, 58f),
                "✦  HERO ARMORY  ✦", menuHeaderStyle);
            GUI.Label(new Rect(panel.x + 30f, panel.y + 102f, panel.width - 60f, 68f),
                "Premium characters will appear here.", menuBodyStyle);
            GUI.Label(new Rect(panel.x + 30f, panel.y + 174f, panel.width - 60f, 94f),
                "PURCHASES ARE NOT CONNECTED IN THIS BUILD\nNo payment will be attempted and no character will be charged or unlocked.", menuSmallStyle);
            Fill(new Rect(panel.x + 28f, panel.y + height - 88f, panel.width - 56f, 1.5f * scale),
                new Color(0.86f, 0.68f, 0.30f, 0.72f));
            Rect back = new Rect(Screen.width * 0.5f - 160f, Screen.height - 104f, 320f, 62f);
            if (DrawFantasyButton(back, "BACK TO HERO SELECTION", true))
                stageTwoShopOpen = false;
        }

        private bool IsAnahitaUnlocked()
        {
            // PlayerPrefs is the durable unlock state; GameBootstrap's runtime flag is
            // retained as a same-session fallback for existing in-flight rescues.
            return PlayerPrefs.GetInt("PW.AnahitaUnlocked", 0) != 0 ||
                (gameBootstrap != null && gameBootstrap.VillageGirlRescued);
        }

        private bool IsHeroUnlocked(int index)
        {
            if (index == 0)
                return true;

            // Anahita is deliberately the second card from the left.
            return index == 1 && stageNumber >= 2 && IsAnahitaUnlocked();
        }

        private void CheckStageOneGirlRescue()
        {
            if (stageNumber != 1 || player == null || gameBootstrap == null ||
                gameBootstrap.VillageGirlRescued || player.IsDefeated)
                return;

            if (gameBootstrap.TryRescueVillageGirl(player.transform.position))
            {
                girlRescueNoticeUntil = Time.unscaledTime + 4.5f;

            }
        }

        private void DrawGirlRescueNotice()
        {
            if (Time.unscaledTime >= girlRescueNoticeUntil)
                return;

            float scale = GetUiScale();
            float width = Mathf.Min(Screen.width - 36f, 720f);
            float height = 86f * scale;
            Rect notice = new Rect((Screen.width - width) * 0.5f, Screen.height * 0.28f, width, height);
            Fill(notice, new Color(0.025f, 0.045f, 0.065f, 0.94f));
            Fill(new Rect(notice.x, notice.y, notice.width, 4f * scale), new Color(0.98f, 0.72f, 0.22f, 1f));
            GUI.Label(new Rect(notice.x + 12f, notice.y + 5f * scale, notice.width - 24f, 38f * scale),
                "ANAHITA RESCUED!", menuButtonStyle);
            GUI.Label(new Rect(notice.x + 12f, notice.y + 45f * scale, notice.width - 24f, 31f * scale),
                "SHE WILL BE AVAILABLE FOR STAGE 2", menuSmallStyle);
        }

        private void DrawHeroCard(Rect rect, int index, bool selected, bool unlocked)
        {
            Color panel = selected ? new Color(0.18f, 0.31f, 0.40f, 0.98f) : new Color(0.035f, 0.065f, 0.10f, 0.96f);
            Fill(rect, selected ? new Color(1f, 0.75f, 0.22f, 1f) : new Color(0.30f, 0.35f, 0.42f, 1f));
            Fill(new Rect(rect.x + 3f, rect.y + 3f, rect.width - 6f, rect.height - 6f), panel);
            Fill(new Rect(rect.x + 3f, rect.y + 3f, rect.width - 6f, 5f), selected ? new Color(0.98f, 0.70f, 0.16f) : new Color(0.20f, 0.28f, 0.36f));

            Rect figure = new Rect(rect.x + rect.width * 0.18f, rect.y + 34f, rect.width * 0.64f, rect.height * 0.52f);
            DrawChibiFigure(figure, index);

            GUI.Label(new Rect(rect.x + 8f, rect.yMax - 82f, rect.width - 16f, 34f), HeroNames[index], menuSmallStyle);
            string state = selected && unlocked ? "✦ SELECTED ✦" : (unlocked ? "TAP TO SELECT" : "LOCKED");
            GUI.Label(new Rect(rect.x + 8f, rect.yMax - 48f, rect.width - 16f, 30f), state, menuSmallStyle);

            if (!unlocked)
            {
                Fill(rect, new Color(0.015f, 0.025f, 0.04f, 0.58f));
                GUI.Label(new Rect(rect.x + 6f, rect.center.y - 12f, rect.width - 12f, 34f),
                    "LOCKED", menuButtonStyle);
                string hint = index == 5 ? "RESCUE IN STAGE 1" : "COMING SOON";
                GUI.Label(new Rect(rect.x + 6f, rect.center.y + 21f, rect.width - 12f, 34f),
                    hint, menuSmallStyle);
            }
        }

        private void DrawChibiFigure(Rect rect, int index)
        {
            Color[] bodyColors =
            {
                new Color(0.45f, 0.17f, 0.10f), // Ardeshir
                new Color(0.78f, 0.20f, 0.48f), // Anahita
                new Color(0.08f, 0.37f, 0.48f), // Pars Guard
                new Color(0.16f, 0.39f, 0.24f), // Royal Scout
                new Color(0.35f, 0.15f, 0.42f), // Silk Warrior
                new Color(0.39f, 0.28f, 0.08f)  // Desert Knight
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
            else if (index == 4)
            {
                Fill(new Rect(center.x - head * 0.82f, center.y - head * 1.45f, head * 1.64f, head * 0.18f), new Color(0.90f, 0.22f, 0.12f));
            }
            else if (index == 1)
            {
                // Feminine silhouette, long dark hair and pink dress for Anahita.
                Fill(new Rect(center.x - head * 0.78f, center.y - head * 1.36f, head * 1.56f, head * 0.36f), new Color(0.10f, 0.055f, 0.035f));
                Fill(new Rect(center.x - head * 0.82f, center.y - head * 0.90f, head * 0.30f, head * 1.18f), new Color(0.10f, 0.055f, 0.035f));
                Fill(new Rect(center.x + head * 0.52f, center.y - head * 0.90f, head * 0.30f, head * 1.18f), new Color(0.10f, 0.055f, 0.035f));
                Fill(new Rect(center.x - body * 0.34f, center.y + body * 0.65f, body * 0.68f, body * 0.62f), new Color(0.80f, 0.22f, 0.50f));
                Fill(new Rect(center.x - body * 0.48f, center.y + body * 0.59f, body * 0.96f, body * 0.12f), new Color(0.95f, 0.72f, 0.24f));
            }
        }
        private void DrawDropMap()
        {
            if (stageNumber >= 2)
            {
                DrawStageTwoDropMap();
                return;
            }
            GUI.Label(new Rect(0f, 121f, Screen.width, 58f), "DROP INTO THE CITY", menuHeaderStyle);
            GUI.Label(new Rect(24f, 176f, Screen.width - 48f, 36f), "TAP ANY OPEN LOCATION TO CHOOSE WHERE YOU START", menuSmallStyle);

            float size = Mathf.Min(Screen.width - 70f, Screen.height - 330f);
            Rect mapRect = new Rect((Screen.width - size) * 0.5f, 220f, size, size);
            const float mapTiltDegrees = 7f;
            GUIUtility.RotateAroundPivot(mapTiltDegrees, mapRect.center);
            DrawTacticalMap(mapRect);
            if (spawnChosen)
            {
                Vector2 point = WorldToMap(spawnWorld, mapRect);
                DrawCircle(point, 16f, new Color(1f, 0.82f, 0.18f, 0.95f));
                GUI.Label(new Rect(point.x - 65f, point.y + 18f, 130f, 28f), "YOU START HERE", smallStyle);
            }
            GUIUtility.RotateAroundPivot(-mapTiltDegrees, mapRect.center);

            // Inverse-rotate taps so the selected world point stays aligned with the tilted map.
            GUIStyle mapTouchStyle = GUIStyle.none;
            if (GUI.Button(mapRect, GUIContent.none, mapTouchStyle))
            {
                Vector2 p = InverseRotateMapPoint(Event.current.mousePosition, mapRect, mapTiltDegrees);
                spawnWorld = MapPointToWorld(p, mapRect);
                spawnChosen = true;
                Debug.Log("PERSIA_FLOW: Spawn selected on real-city map " + spawnWorld);
            }

            Rect hint = new Rect(24f, Screen.height - 100f, Screen.width - 48f, 34f);
            GUI.Label(hint, spawnChosen ? "SPAWN POINT LOCKED  •  PRESS START MATCH" : "CHOOSE AN OPEN LOCATION TO START", menuSmallStyle);

            Rect start = new Rect(Screen.width * 0.5f - 180f, Screen.height - 64f, 360f, 52f);
            GUI.enabled = spawnChosen;
            if (DrawFantasyButton(start, "START MATCH  ⚔", true))
                StartMatch();
            GUI.enabled = true;
        }

        private void DrawTacticalMap(Rect rect)
        {
            // Use the exact road centers and building centers from the Android city generator.
            Fill(rect, new Color(0.30f, 0.57f, 0.27f, 1f));
            float sx = rect.width / 192f;
            float sz = rect.height / 192f;

            Fill(new Rect(rect.x + rect.width * 0.02f, rect.y + rect.height * 0.04f, rect.width * 0.29f, rect.height * 0.28f), new Color(0.47f, 0.65f, 0.30f, 1f));
            Fill(new Rect(rect.x + rect.width * 0.66f, rect.y + rect.height * 0.04f, rect.width * 0.31f, rect.height * 0.30f), new Color(0.78f, 0.70f, 0.34f, 1f));
            Fill(new Rect(rect.x + rect.width * 0.66f, rect.y + rect.height * 0.58f, rect.width * 0.31f, rect.height * 0.37f), new Color(0.72f, 0.48f, 0.29f, 1f));

            Color sidewalk = new Color(0.73f, 0.70f, 0.60f, 1f);
            Color asphalt = new Color(0.30f, 0.33f, 0.35f, 1f);
            for (int i = 0; i < AndroidMinimapRoadCoordinates.Length; i++)
            {
                float road = AndroidMinimapRoadCoordinates[i];
                float x = rect.x + Mathf.InverseLerp(-96f, 96f, road) * rect.width;
                float y = rect.y + Mathf.InverseLerp(96f, -96f, road) * rect.height;
                Fill(new Rect(x - 5f * sx, rect.y, 10f * sx, rect.height), sidewalk);
                Fill(new Rect(x - 3.7f * sx, rect.y, 7.4f * sx, rect.height), asphalt);
                Fill(new Rect(rect.x, y - 5f * sz, rect.width, 10f * sz), sidewalk);
                Fill(new Rect(rect.x, y - 3.7f * sz, rect.width, 7.4f * sz), asphalt);
            }

            for (int i = 0; i < AndroidMinimapBuildingPoints.Length; i++)
            {
                float wr = Mathf.Abs(Mathf.Sin((i + 1) * 12.9898f));
                float dr = Mathf.Abs(Mathf.Sin((i + 1) * 39.425f));
                float footprint = Mathf.Lerp(7.2f, 10.2f, wr);
                float depth = Mathf.Lerp(6.8f, 9.8f, dr);
                if (i >= 22) { footprint = Mathf.Lerp(6.5f, 8.2f, wr); depth = Mathf.Lerp(6.0f, 7.8f, dr); }
                else if (i >= 18 && i <= 21) { footprint = Mathf.Lerp(7.5f, 9.8f, wr); depth = Mathf.Lerp(6.8f, 9.2f, dr); }
                if (i == 0 || i == 7 || i == 13) footprint = Mathf.Max(footprint, 10.2f);
                if (i == 4 || i == 9 || i == 11 || i == 16)
                {
                    footprint = Mathf.Lerp(9.4f, 10.4f, wr);
                    depth = Mathf.Lerp(8.2f, 10.0f, dr);
                }

                Vector2 center = WorldToMap(AndroidMinimapBuildingPoints[i], rect);
                float w = footprint * sx, h = depth * sz;
                Rect house = new Rect(center.x - w * 0.5f, center.y - h * 0.5f, w, h);
                Fill(new Rect(house.x - 1.2f, house.y - 1.2f, house.width + 2.4f, house.height + 2.4f), new Color(0.13f, 0.09f, 0.06f, 0.95f));
                Fill(house, (i == 0 || i == 7 || i == 13) ? new Color(0.75f, 0.69f, 0.57f, 1f) : new Color(0.89f, 0.67f, 0.36f, 1f));
            }

            GUI.Label(new Rect(rect.x + 8f, rect.y + 7f, 170f, 24f), "PERSIA WAR • CITY MAP", smallStyle);
        }

        private Vector2 InverseRotateMapPoint(Vector2 point, Rect mapRect, float degrees)
        {
            float radians = -degrees * Mathf.Deg2Rad;
            Vector2 delta = point - mapRect.center;
            float cos = Mathf.Cos(radians), sin = Mathf.Sin(radians);
            return mapRect.center + new Vector2(delta.x * cos - delta.y * sin, delta.x * sin + delta.y * cos);
        }

        private Vector2 MapPointToWorld(Vector2 point, Rect mapRect)
        {
            float u = Mathf.Clamp01((point.x - mapRect.x) / mapRect.width);
            float v = Mathf.Clamp01((point.y - mapRect.y) / mapRect.height);
            return new Vector2(Mathf.Lerp(-96f, 96f, u), Mathf.Lerp(96f, -96f, v));
        }

        private void HandleDropTouches()
        {
            if (stageNumber >= 2)
            {
                HandleStageTwoDropTouches();
                return;
            }
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

            // Handle START MATCH through real touch coordinates as well as IMGUI.
            // Some Android configurations do not synthesize MouseUp for GUI.Button.
            Rect startRect = new Rect(Screen.width * 0.5f - 210f, Screen.height - 66f, 420f, 54f);
            if (spawnChosen && startRect.Contains(screen))
            {
                StartMatch();
                return;
            }

            float size = Mathf.Min(Screen.width - 70f, Screen.height - 330f);
            Rect mapRect = new Rect((Screen.width - size) * 0.5f, 220f, size, size);
            if (!mapRect.Contains(screen)) return;

            Vector2 unrotatedScreen = InverseRotateMapPoint(screen, mapRect, 7f);
            spawnWorld = MapPointToWorld(unrotatedScreen, mapRect);
            spawnChosen = true;
        }

        private void BeginStageTwoSelection()
        {
            ExtractionBeacon.ClearForStageTransition();
            if (GameSession.Instance != null) GameSession.Instance.ResetMatch();
            Time.timeScale = 1f;
            stageNumber = 2;
            stageThreeResultAcknowledged = false;

            bool registrationRequired = !stageTestRun;
            if (registrationRequired) PlayerPrefs.SetInt("PW.RegistrationPending", 1);
            if (IsAnahitaUnlocked()) PlayerPrefs.SetInt("PW.AnahitaUnlocked", 1);
            PlayerPrefs.SetInt("PW.CampaignStage", 2);
            PlayerPrefs.Save();

            stageTwoOriginalHero = selectedHero;
            stageTwoShopOpen = false;
            stageTwoLobbyRunning = false;
            stageTwoLobbyReady = false;
            stageTwoLobbyPlayers = 1;
            stageTwoLandingMode = 1;
            stageTwoLayoutVariant = 0;
            spawnChosen = false;
            matchPaused = false;
            resultOverlayStartTime = -1f;
            if (activeCamera != null) activeCamera.enabled = false;
            if (androidRuntimeCamera != null) androidRuntimeCamera.enabled = false;
            GateGameplay(false);
            // World disposal runs after the transition loading panel has had a frame to render.
            BeginStageTransition(2, registrationRequired ? ScreenMode.Profile : ScreenMode.HeroSelect);
            StartupCheckpoint.Set(registrationRequired
                ? "StageOneComplete_OpenRegistrationBeforeStageTwoHeroSelection"
                : "StageTwoHeroSelectionOpened");
        }

        private void ReleaseCompletedWorldBeforeStageFour()
        {
            BloodFX.ClearAll();
            ModelPreview.ReleaseAll();
            // Detach callbacks first so cleanup cannot re-enter the progression flow.
            if (stageThreeWorldBuilder != null)
            {
                stageThreeWorldBuilder.OnEndingComplete -= HandleStageThreeEndingComplete;
                stageThreeWorldBuilder.OnBuildFailed -= HandleStageThreeBuildFailed;
            }

            try
            {
                StageThreeWorldBuilder.ClearExistingWorld(false);
                if (stageThreeWorldBuilder != null)
                {
                    Destroy(stageThreeWorldBuilder);
                    stageThreeWorldBuilder = null;
                }
                StartupCheckpoint.Set("CompletedStageThreeWorldReleasedBeforeStageFour");
            }
            catch (System.Exception ex)
            {
                // The transition must remain recoverable. A cleanup failure is logged
                // and surfaced rather than silently relaunching Stage 3.
                Debug.LogError("PERSIA_TRANSITION: Stage 3 world cleanup failed before Stage 4.\n" + ex);
                StartupCheckpoint.Set("CompletedStageThreeWorldReleaseFailed_" + ex.GetType().Name);
                throw;
            }
        }

        private void ReleaseCompletedWorldBeforeStageThree()
        {
            GameBootstrap completedBootstrap = gameBootstrap != null
                ? gameBootstrap
                : (gameRoot != null ? gameRoot.GetComponent<GameBootstrap>() : null);
            if (completedBootstrap == null)
            {
                StartupCheckpoint.Set("StageThreeWorldReleaseSkipped_NoBootstrap");
                Debug.LogWarning("PERSIA_TRANSITION: GameBootstrap was missing while releasing the completed world before Stage 3.");
                return;
            }

            try
            {
                completedBootstrap.ReleaseWorldForStageTransition();
                StartupCheckpoint.Set("CompletedWorldReleasedBeforeStageThree");
            }
            catch (System.Exception ex)
            {
                // Keep the campaign handoff recoverable if a device-specific cleanup
                // failure occurs. Stage 3 has its own guarded pre-build asset unload.
                Debug.LogError("PERSIA_TRANSITION: completed-world release failed before Stage 3. Continuing to player selection.\n" + ex);
                StartupCheckpoint.Set("CompletedWorldReleaseFailedBeforeStageThree_" + ex.GetType().Name);
            }
        }

        private void ReleaseCompletedWorldBeforeStageTwo()
        {
            GameBootstrap completedBootstrap = gameBootstrap != null
                ? gameBootstrap
                : (gameRoot != null ? gameRoot.GetComponent<GameBootstrap>() : null);
            if (completedBootstrap == null)
            {
                StartupCheckpoint.Set("CompletedWorldReleaseSkipped_NoBootstrap");
                Debug.LogWarning("PERSIA_TRANSITION: GameBootstrap was missing while releasing the completed world before Stage 2.");
                return;
            }

            try
            {
                completedBootstrap.ReleaseWorldForStageTransition();
                StartupCheckpoint.Set("CompletedWorldReleasedBeforeStageTwo");
            }
            catch (System.Exception ex)
            {
                // The selection flow must still recover if a device-specific cleanup error occurs.
                Debug.LogError("PERSIA_TRANSITION: completed-world release failed before Stage 2. Continuing to player selection.\n" + ex);
                StartupCheckpoint.Set("CompletedWorldReleaseFailed_" + ex.GetType().Name);
            }
        }

        private void EnterDropMap()
        {
            mode = ScreenMode.DropMap;
            if (stageNumber >= 2)
            {
                // Keep the village layout fixed. The player chooses only a landing
                // point on this one map; no random landing or alternate layouts.
                stageTwoLayoutVariant = 0;
                stageTwoLandingMode = 1;
                spawnChosen = false;
            }
            else
            {
                spawnChosen = false;
            }
        }

        private void ChooseRandomStageTwoSpawn()
        {
            // Authored safe points distributed around the village; none sit inside a
            // courtyard footprint. Randomness changes the landing each time without
            // placing the player inside geometry or directly on an enemy.
            Vector2[] safePoints =
            {
                new Vector2(-91f, -42f), new Vector2(-42f, -88f), new Vector2(4f, -86f),
                new Vector2(83f, -32f), new Vector2(87f, 48f), new Vector2(43f, 89f),
                new Vector2(-12f, 87f), new Vector2(-89f, 43f), new Vector2(4f, 42f),
                new Vector2(49f, -15f), new Vector2(-38f, 32f), new Vector2(82f, 4f)
            };
            spawnWorld = safePoints[Random.Range(0, safePoints.Length)];
            // Keep authored landing zones aligned with mirrored/rotated village layouts.
            if (stageTwoLayoutVariant == 1)
                spawnWorld.x = -spawnWorld.x;
            else if (stageTwoLayoutVariant == 2)
                spawnWorld = new Vector2(spawnWorld.y, -spawnWorld.x);
            spawnChosen = true;
            Debug.Log("PERSIA_STAGE2: random safe landing selected " + spawnWorld);
        }

        private Rect GetStageTwoMapRect()
        {
            float top = Mathf.Clamp(Screen.height * 0.23f, 130f, 165f);
            float size = Mathf.Min(Screen.height - top - 68f, Screen.width * 0.48f);
            size = Mathf.Clamp(size, 180f, 520f);
            return new Rect(18f, top, size, size);
        }

        private Rect GetStageTwoControlPanel()
        {
            Rect map = GetStageTwoMapRect();
            float x = map.xMax + 26f;
            float width = Mathf.Max(140f, Screen.width - x - 18f);
            return new Rect(x, map.y, width, Screen.height - map.y - 10f);
        }

        private Rect GetStageTwoModeButtonRect(int index)
        {
            Rect panel = GetStageTwoControlPanel();
            return new Rect(panel.x, panel.y + 42f + index * 64f, panel.width, 56f);
        }

        private Rect GetStageTwoLayoutButtonRect(int index)
        {
            Rect panel = GetStageTwoControlPanel();
            const float gap = 10f;
            float width = (panel.width - gap * 2f) / 3f;
            return new Rect(panel.x + index * (width + gap), panel.y + 202f, width, 48f);
        }

        private Rect GetStageTwoStartRect()
        {
            Rect panel = GetStageTwoControlPanel();
            return new Rect(panel.x, Screen.height - 68f, panel.width, 54f);
        }

        private void DrawStageTwoDropMap()
        {
            GUI.Label(new Rect(0f, 121f, Screen.width, 48f),
                "STAGE 2  •  OLD IRANIAN VILLAGE", menuHeaderStyle);
            GUI.Label(new Rect(24f, 168f, Screen.width - 48f, 30f),
                "CHOOSE YOUR LANDING POINT", menuSmallStyle);

            Rect mapRect = GetStageTwoMapRect();
            Rect panel = GetStageTwoControlPanel();

            Fill(panel, new Color(0.02f, 0.045f, 0.07f, 0.46f));
            Fill(new Rect(panel.x, panel.y, panel.width, 4f),
                new Color(0.98f, 0.72f, 0.22f, 0.92f));
            GUI.Label(new Rect(panel.x + 10f, panel.y + 8f, panel.width - 20f, 36f),
                "LANDING POINT", menuButtonStyle);
            GUI.Label(new Rect(panel.x + 10f, panel.y + 48f, panel.width - 20f, 74f),
                "Tap an open area on the map. The village houses, trees and paths stay in the same places.",
                menuSmallStyle);

            DrawStageTwoVillageMap(mapRect);
            if (spawnChosen)
            {
                Vector2 point = WorldToMap(spawnWorld, mapRect);
                point.x = Mathf.Clamp(point.x, mapRect.x + 44f, mapRect.xMax - 44f);
                point.y = Mathf.Clamp(point.y, mapRect.y + 45f, mapRect.yMax - 45f);
                DrawCircle(point, 15f, new Color(1f, 0.83f, 0.16f, 0.98f));
                Rect markerLabel = new Rect(
                    Mathf.Clamp(point.x - 74f, mapRect.x + 4f, mapRect.xMax - 152f),
                    Mathf.Clamp(point.y + 18f, mapRect.y + 32f, mapRect.yMax - 34f),
                    148f, 26f);
                Fill(markerLabel, new Color(0.02f, 0.035f, 0.045f, 0.88f));
                GUI.Label(markerLabel, "LANDING POINT", menuSmallStyle);
            }

            Rect hint = new Rect(panel.x + 8f, Screen.height - 119f, panel.width - 16f, 30f);
            GUI.Label(hint,
                spawnChosen ? "LANDING POINT SELECTED" : "TAP THE MAP TO CHOOSE",
                menuSmallStyle);

            GUI.enabled = spawnChosen;
            if (DrawFantasyButton(GetStageTwoStartRect(), "ENTER VILLAGE  ⚔", true))
                StartMatch();
            GUI.enabled = true;
        }

        private void HandleStageTwoDropTouches()
        {
            if (!Application.isMobilePlatform && !Input.GetMouseButtonDown(0))
                return;

            Vector2 screen;
            if (Application.isMobilePlatform)
            {
                if (Input.touchCount == 0 || Input.GetTouch(0).phase != TouchPhase.Began)
                    return;
                screen = Input.GetTouch(0).position;
            }
            else
            {
                screen = Input.mousePosition;
            }
            screen.y = Screen.height - screen.y;

            Rect startRect = GetStageTwoStartRect();
            if (spawnChosen && startRect.Contains(screen))
            {
                suppressMenuButtonClicksFrame = Time.frameCount;
                StartMatch();
                return;
            }

            Rect mapRect = GetStageTwoMapRect();
            if (mapRect.Contains(screen))
            {
                suppressMenuButtonClicksFrame = Time.frameCount;
                // Ignore taps on the decorative map title; all other map taps pick
                // the actual spawn point. Stage 2 has one immutable world layout.
                if (screen.y < mapRect.y + 36f)
                    return;
                spawnWorld = MapPointToWorld(screen, mapRect);
                spawnChosen = true;
            }
        }

        private void DrawStageTwoVillageMap(Rect rect)
        {
            Fill(rect, new Color(0.27f, 0.43f, 0.20f, 1f));
            // Gardens and cultivated patches.
            Fill(new Rect(rect.x + rect.width * 0.03f, rect.y + rect.height * 0.08f, rect.width * 0.23f, rect.height * 0.19f), new Color(0.35f, 0.53f, 0.24f, 1f));
            Fill(new Rect(rect.x + rect.width * 0.66f, rect.y + rect.height * 0.62f, rect.width * 0.29f, rect.height * 0.25f), new Color(0.37f, 0.55f, 0.25f, 1f));
            Color lane = new Color(0.49f, 0.38f, 0.26f, 1f);
            Vector2[] path = { new Vector2(-78f, -58f), new Vector2(-56f, -46f), new Vector2(-34f, -28f), new Vector2(-10f, -8f), new Vector2(14f, 12f), new Vector2(36f, 34f), new Vector2(58f, 52f), new Vector2(78f, 68f) };
            for (int i = 0; i < path.Length; i++)
            {
                Vector2 p = WorldToMap(path[i], rect);
                Fill(new Rect(p.x - rect.width * 0.018f, p.y - rect.height * 0.018f, rect.width * 0.036f, rect.height * 0.036f), lane);
            }
            Vector2[] houses =
            {
                new Vector2(-78f,-72f),new Vector2(-62f,-72f),new Vector2(-78f,-56f),new Vector2(-62f,-56f),
                new Vector2(-24f,-70f),new Vector2(-8f,-70f),new Vector2(-24f,-54f),
                new Vector2(34f,-72f),new Vector2(50f,-72f),new Vector2(66f,-56f),
                new Vector2(-72f,-8f),new Vector2(-56f,8f),new Vector2(-40f,8f),
                new Vector2(-4f,0f),new Vector2(12f,0f),new Vector2(28f,16f),
                new Vector2(62f,12f),new Vector2(78f,28f),new Vector2(-60f,58f),
                new Vector2(-44f,74f),new Vector2(12f,62f),new Vector2(28f,78f),new Vector2(62f,70f)
            };
            for (int i = 0; i < houses.Length; i++)
            {
                Vector2 center = houses[i];
                Vector2 p = WorldToMap(center, rect);
                float w = rect.width * 0.045f;
                float h = rect.height * 0.038f;
                Fill(new Rect(p.x - w * 0.5f - 2f, p.y - h * 0.5f - 2f, w + 4f, h + 4f), new Color(0.20f, 0.13f, 0.08f, 1f));
                Fill(new Rect(p.x - w * 0.5f, p.y - h * 0.5f, w, h), i == 0 || i == 13 || i == 20 ? new Color(0.84f, 0.64f, 0.40f, 1f) : new Color(0.68f, 0.48f, 0.31f, 1f));
                if (i == 0 || i == 13 || i == 20)
                    Fill(new Rect(p.x - 2f, p.y - h * 0.5f - 4f, 4f, 6f), new Color(0.85f, 0.76f, 0.54f, 1f));
            }
            GUI.Label(new Rect(rect.x + 7f, rect.y + 6f, rect.width - 14f, 30f), "VILLAGE • COURTYARDS • GARDENS", menuSmallStyle);
        }

        private void StartMatch()
        {
            if (!spawnChosen || startingMatch || stageTwoLobbyRunning)
                return;
            if (stageNumber > 4)
            {
                ShowLobbyNotice("NO FURTHER STAGE IS AVAILABLE YET.");
                mode = ScreenMode.MainMenu;
                return;
            }

            matchPaused = false;
            Time.timeScale = 1f;
            if (stageNumber == 2 && !stageTwoLobbyReady)
            {
                StartCoroutine(RunStageTwoLobby());
                return;
            }
            StartCoroutine(BeginMatchSafely());
        }

        private IEnumerator RunStageTwoLobby()
        {
            stageTwoLobbyRunning = true;
            stageTwoLobbyPlayers = 1;
            stageTwoLobbyStartedAt = Time.unscaledTime;
            StartupCheckpoint.Set("StageTwoLobbyStarted");

            // This project has no online-session backend yet. Be explicit: the first
            // slot is the local player and remaining slots are local AI competitors.
            // A real human online count requires a server/networking service.
            yield return new WaitForSecondsRealtime(7f);
            while (stageTwoLobbyPlayers < 10)
            {
                stageTwoLobbyPlayers++;
                StartupCheckpoint.Set("StageTwoLobbyRoster_" + stageTwoLobbyPlayers);
                yield return new WaitForSecondsRealtime(1f);
            }

            stageTwoLobbyReady = true;
            stageTwoLobbyRunning = false;
            StartupCheckpoint.Set("StageTwoLobbyReady");
            StartMatch();
        }

        private void DrawStageTwoLobbyOverlay()
        {
            float scale = GetUiScale();
            Rect shade = new Rect(0f, 0f, Screen.width, Screen.height);
            Fill(shade, new Color(0.015f, 0.025f, 0.045f, 0.72f));

            // Use a larger, responsive panel and keep every row inside its bounds.
            // The previous fixed pixel offsets overlapped/clipped labels on phone aspect ratios.
            float width = Mathf.Min(Screen.width - 28f * scale, 900f * scale);
            float height = Mathf.Min(Screen.height - 24f * scale, 560f * scale);
            Rect panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            Fill(new Rect(panel.x + 5f * scale, panel.y + 8f * scale, panel.width, panel.height), new Color(0f, 0f, 0f, 0.40f));
            Fill(panel, new Color(0.035f, 0.075f, 0.12f, 0.97f));
            Fill(new Rect(panel.x, panel.y, panel.width, 5f * scale), new Color(0.25f, 0.92f, 0.45f, 0.95f));

            float pad = 24f * scale;
            GUI.Label(new Rect(panel.x + pad, panel.y + 14f * scale, panel.width - pad * 2f, 48f * scale),
                "STAGE 2  •  ARENA LOBBY", menuHeaderStyle);
            GUI.Label(new Rect(panel.x + pad, panel.y + 64f * scale, panel.width - pad * 2f, 38f * scale),
                "COMPETITORS  " + stageTwoLobbyPlayers + " / 10", menuBodyStyle);

            Rect track = new Rect(panel.x + pad, panel.y + 106f * scale, panel.width - pad * 2f, 13f * scale);
            Fill(track, new Color(0.10f, 0.16f, 0.20f, 0.95f));
            Fill(new Rect(track.x, track.y, track.width * (stageTwoLobbyPlayers / 10f), track.height),
                new Color(0.20f, 0.92f, 0.42f, 0.95f));

            float elapsed = Time.unscaledTime - stageTwoLobbyStartedAt;
            string status = elapsed < 7f
                ? "WAITING FOR PLAYERS  •  " + Mathf.CeilToInt(7f - elapsed) + "s"
                : "FILLING REMAINING SLOTS WITH LOCAL AI  •  +1 / SECOND";
            GUI.Label(new Rect(panel.x + pad, panel.y + 124f * scale, panel.width - pad * 2f, 34f * scale),
                status, menuSmallStyle);

            float slotGap = 8f * scale;
            float slotWidth = (panel.width - pad * 2f - slotGap * 4f) / 5f;
            float slotTop = panel.y + 164f * scale;
            float slotHeight = 42f * scale;
            float rowGap = 8f * scale;
            for (int slot = 0; slot < 10; slot++)
            {
                int column = slot % 5;
                int row = slot / 5;
                Rect slotRect = new Rect(panel.x + pad + column * (slotWidth + slotGap),
                    slotTop + row * (slotHeight + rowGap), slotWidth, slotHeight);
                bool present = slot < stageTwoLobbyPlayers;
                Fill(slotRect, present ? new Color(0.08f, 0.32f, 0.22f, 0.92f) : new Color(0.08f, 0.12f, 0.16f, 0.82f));
                Fill(new Rect(slotRect.x, slotRect.y, 3f * scale, slotRect.height),
                    present ? new Color(0.25f, 0.92f, 0.45f, 0.95f) : new Color(0.38f, 0.45f, 0.50f, 0.55f));
                string slotName = present ? (slot == 0 ? "YOU" : "AI-" + slot.ToString("00")) : "WAITING";
                GUI.Label(new Rect(slotRect.x + 8f * scale, slotRect.y + 2f * scale,
                    slotRect.width - 14f * scale, slotRect.height - 4f * scale), slotName, menuSmallStyle);
            }

            float footerY = panel.y + panel.height - 76f * scale;
            GUI.Label(new Rect(panel.x + pad, footerY, panel.width - pad * 2f, 30f * scale),
                "100 ALIENS  •  5 WAVES  •  NEXT WAVE AT 6 REMAINING", menuSmallStyle);
            GUI.Label(new Rect(panel.x + pad, footerY + 30f * scale, panel.width - pad * 2f, 28f * scale),
                "OFFLINE TEST: AI FILLS THE LOBBY; ONLINE SERVER NOT CONNECTED", menuSmallStyle);
        }

        private void SetMatchPaused(bool paused)
        {
            matchPaused = paused;
            Time.timeScale = paused ? 0f : 1f;
            if (mobileInput != null) mobileInput.SetPaused(paused);
            StartupCheckpoint.Set(paused ? "MatchPausedByNativeTouch" : "MatchResumedByNativeTouch");
        }

        private Rect GetDebugStageButtonRect()
        {
            float scale = GetUiScale();
            float width = Mathf.Min(Screen.width - 40f * scale, 300f * scale);
            return new Rect((Screen.width - width) * 0.5f, Screen.height - 82f * scale, width, 42f * scale);
        }

        private bool HandleDebugStageButtonTouches()
        {
            if (!(stageTestRun || stageChainTest) || !Application.isMobilePlatform || Input.touchCount <= 0)
                return false;
            Rect button = GetDebugStageButtonRect();
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.phase != TouchPhase.Began) continue;
                Vector2 point = new Vector2(touch.position.x, Screen.height - touch.position.y);
                if (!button.Contains(point)) continue;
                DebugCompleteCurrentStage();
                return true;
            }
            return false;
        }

        private void DrawDebugStageButton()
        {
            Rect button = GetDebugStageButtonRect();
            if (GUI.Button(button, "DEBUG: COMPLETE THIS STAGE", buttonStyle))
                DebugCompleteCurrentStage();
        }

        private void DrawAndroidPauseControl()
        {
            EnsureUiInitialized();

            float scale = GetUiScale();
            float size = 58f * scale;

            if (matchPaused)
            {
                Fill(new Rect(0f, 0f, Screen.width, Screen.height),
                    new Color(0f, 0f, 0f, 0.28f));

                float panelW = Mathf.Min(Screen.width - 64f, 420f);
                float panelH = 170f;
                Rect panel = new Rect(
                    (Screen.width - panelW) * 0.5f,
                    (Screen.height - panelH) * 0.5f,
                    panelW,
                    panelH);

                Fill(panel, new Color(0.03f, 0.055f, 0.08f, 0.96f));
                Fill(new Rect(panel.x, panel.y, panel.width, 5f),
                    new Color(0.94f, 0.66f, 0.18f, 1f));

                GUI.Label(
                    new Rect(panel.x + 20f, panel.y + 24f, panel.width - 40f, 40f),
                    "PAUSED",
                    headerStyle);

                float gap = 8f * scale;
                float buttonY = panel.y + panel.height - 54f * scale;
                float buttonW = (panel.width - 32f * scale - gap * 2f) / 3f;
                if (GUI.Button(new Rect(panel.x + 16f * scale, buttonY, buttonW, 38f * scale), "RESUME", buttonStyle))
                    SetMatchPaused(false);
                if (GUI.Button(new Rect(panel.x + 16f * scale + buttonW + gap, buttonY, buttonW, 38f * scale), "RESTART STAGE", buttonStyle))
                    RestartCurrentStageInPlace();
                if (GUI.Button(new Rect(panel.x + 16f * scale + (buttonW + gap) * 2f, buttonY, buttonW, 38f * scale), "MAIN MENU", buttonStyle))
                    GoToMainMenu();
            }

            // Draw the control last so the pause overlay can never cover it.
            Rect safe = GetSafeGuiRect();
            Rect button = new Rect(
                safe.center.x - size * 0.5f,
                safe.y + 14f * scale,
                size,
                size);

            // Always render the pause/resume button on every platform. Android
            // activates it through HandleAndroidPauseTouches() using native finger IDs;
            // GUI.Button remains the visible control and is only the click owner on desktop.
            bool pauseButtonPressed = GUI.Button(
                button,
                matchPaused ? "▶" : "Ⅱ",
                buttonStyle);
            if (!Application.isMobilePlatform && pauseButtonPressed)
            {
                SetMatchPaused(!matchPaused);
            }
        }

        private IEnumerator BeginMatchSafely()
        {
            startingMatch = true;
            matchInputArmed = false;
            startupStatus = "starting battlefield preparation...";

            StartupCheckpoint.Set("MatchActivationStarted");

            if (stageNumber == 3)
            {
                stageThreePreloadInProgress = true;
                stageThreePreloadStatus = "Stage 3: reading saved mission data...";
                float stageThreePreloadStartedAt = Time.realtimeSinceStartup;
                const float stageThreeMinimumLoadingSeconds = 1.8f;
                const float stageThreePreloadTimeoutSeconds = 20f;
                StartupCheckpoint.Set("StageThreePreloadStarted_BeforeWorldActivation");

                // Let the loading panel render before releasing the previous arena.
                yield return null;
                stageThreePreloadStatus = "Stage 3: releasing Stage 2 memory...";
                AsyncOperation stageThreeAssetUnload = Resources.UnloadUnusedAssets();
                while (stageThreeAssetUnload != null && !stageThreeAssetUnload.isDone &&
                    Time.realtimeSinceStartup - stageThreePreloadStartedAt < stageThreePreloadTimeoutSeconds)
                {
                    startupStatus = stageThreePreloadStatus;
                    yield return null;
                }

                if (stageThreeAssetUnload != null && !stageThreeAssetUnload.isDone)
                {
                    stageThreePreloadInProgress = false;
                    stageThreePreloadStatus = string.Empty;
                    StartupCheckpoint.Set("StageThreePreloadTimedOut_20Seconds");
                    ShowTransitionError(3, ScreenMode.HeroSelect, true,
                        "Stage 3 resource loading exceeded 20 seconds. Retry after the previous arena is released.");
                    yield break;
                }

                stageThreePreloadStatus = "Stage 3: preparing graphics and mission resources...";
                // Resources.UnloadUnusedAssets already performs Unity's unused-asset
                // sweep. Avoid an immediate forced full managed GC on memory-constrained
                // Android devices; let Unity schedule collection across subsequent frames.
                yield return null;
                yield return null;
                while (Time.realtimeSinceStartup - stageThreePreloadStartedAt < stageThreeMinimumLoadingSeconds)
                {
                    yield return null;
                }

                stageThreePreloadInProgress = false;
                stageThreePreloadStatus = string.Empty;
                StartupCheckpoint.Set("StageThreePreloadCompleted_BeforeWorldActivation");
            }

            // GameBootstrap lives under the dormant GameRoot. A disabled/inactive
            // MonoBehaviour cannot own a running coroutine, so wake the root first,
            // then explicitly re-gate gameplay components before building the world.
            if (gameRoot != null && !gameRoot.activeSelf)
            {
                startupStatus = "activating battlefield systems...";
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

            StageThreeWorldBuilder.ClearExistingWorld();
            StageFourWorldBuilder.ClearExistingWorld();
            if (stageNumber == 3)
            {
                stageThreeWorldBuilder = gameRoot.GetComponent<StageThreeWorldBuilder>();
                if (stageThreeWorldBuilder == null)
                    stageThreeWorldBuilder = gameRoot.AddComponent<StageThreeWorldBuilder>();
                stageThreeWorldBuilder.OnEndingComplete -= HandleStageThreeEndingComplete;
                stageThreeWorldBuilder.OnEndingComplete += HandleStageThreeEndingComplete;
                stageThreeWorldBuilder.OnBuildFailed -= HandleStageThreeBuildFailed;
                stageThreeWorldBuilder.OnBuildFailed += HandleStageThreeBuildFailed;
                if (GameSession.Instance != null) GameSession.Instance.ResetMatch();

                startupStatus = "Stage 3: preparing the alien fortress...";
                StartupCheckpoint.Set("StageThreeWorldBuildStarted");
                bool buildCompleted = false;
                bool buildSucceeded = false;
                float buildStartedAt = Time.realtimeSinceStartup;
                const float buildTimeoutSeconds = 60f;
                Coroutine buildCoroutine = StartCoroutine(stageThreeWorldBuilder.BuildWorldRoutine(success =>
                {
                    buildSucceeded = success;
                    buildCompleted = true;
                }));
                if (buildCoroutine == null)
                {
                    buildCompleted = true;
                    buildSucceeded = false;
                    StartupCheckpoint.Set("StageThreeBuildCoroutineFailedToStart");
                }
                while (!buildCompleted)
                {
                    if (stageThreeWorldBuilder == null)
                    {
                        buildCompleted = true;
                        buildSucceeded = false;
                        startupStatus = "Stage 3 builder stopped unexpectedly.";
                        break;
                    }
                    startupStatus = stageThreeWorldBuilder.BuildStatus;
                    if (Time.realtimeSinceStartup - buildStartedAt >= buildTimeoutSeconds)
                    {
                        if (buildCoroutine != null) StopCoroutine(buildCoroutine);
                        if (stageThreeWorldBuilder != null)
                            stageThreeWorldBuilder.AbortBuild("Stage 3 world construction exceeded 60 seconds.");
                        buildCompleted = true;
                        buildSucceeded = false;
                        startupStatus = stageThreeWorldBuilder != null
                            ? stageThreeWorldBuilder.BuildStatus : "Stage 3 construction timed out.";
                        StartupCheckpoint.Set("StageThreeBuildTimedOut_60Seconds");
                        break;
                    }
                    yield return null;
                }
                if (!buildSucceeded)
                {
                    string error = stageThreeWorldBuilder != null
                        ? stageThreeWorldBuilder.BuildStatus : startupStatus;
                    ShowTransitionError(3, ScreenMode.HeroSelect, true, error);
                    yield break;
                }
                StartupCheckpoint.Set("StageThreeWorldBuildCompleted");
            }
            else if (stageNumber == 4)
            {
                stageFourWorldBuilder = gameRoot.GetComponent<StageFourWorldBuilder>();
                if (stageFourWorldBuilder == null)
                    stageFourWorldBuilder = gameRoot.AddComponent<StageFourWorldBuilder>();
                if (GameSession.Instance != null)
                    GameSession.Instance.ResetMatch();
                startupStatus = "starting orbital megabase construction...";
                StartupCheckpoint.Set("StageFourWorldBuildStarted");
                bool stageFourBuildCompleted = false;
                bool stageFourBuildSucceeded = false;
                float stageFourBuildStartedAt = Time.realtimeSinceStartup;
                const float stageFourBuildTimeoutSeconds = 60f;
                Coroutine stageFourBuildCoroutine = StartCoroutine(stageFourWorldBuilder.BuildWorldRoutine(success =>
                {
                    stageFourBuildSucceeded = success;
                    stageFourBuildCompleted = true;
                }));

                // Keep the loading presentation responsive while the megabase is
                // generated in batches. Never wait forever if Unity stops the child
                // coroutine or a device-specific runtime exception interrupts it.
                if (stageFourBuildCoroutine == null)
                {
                    stageFourBuildCompleted = true;
                    stageFourBuildSucceeded = false;
                    StartupCheckpoint.Set("StageFourBuildCoroutineFailedToStart");
                }

                while (!stageFourBuildCompleted)
                {
                    if (stageFourWorldBuilder == null)
                    {
                        stageFourBuildCompleted = true;
                        stageFourBuildSucceeded = false;
                        StartupCheckpoint.Set("StageFourBuilderLostDuringBuild");
                        break;
                    }

                    startupStatus = stageFourWorldBuilder.BuildStatus;
                    if (Time.realtimeSinceStartup - stageFourBuildStartedAt >= stageFourBuildTimeoutSeconds)
                    {
                        StopCoroutine(stageFourBuildCoroutine);
                        stageFourWorldBuilder.AbortBuild("Stage 4 world construction exceeded 60 seconds.");
                        stageFourBuildCompleted = true;
                        stageFourBuildSucceeded = false;
                        StartupCheckpoint.Set("StageFourBuildTimedOut_60Seconds");
                        break;
                    }
                    yield return null;
                }

                if (!stageFourBuildSucceeded)
                {
                    startupStatus = stageFourWorldBuilder != null
                        ? stageFourWorldBuilder.BuildStatus
                        : "Stage 4 builder stopped unexpectedly.";
                    StartupCheckpoint.Set("StageFourWorldBuildFailed");
                    string stageFourFailureCode = stageFourWorldBuilder != null &&
                        !string.IsNullOrEmpty(stageFourWorldBuilder.BuildFailureCode)
                        ? stageFourWorldBuilder.BuildFailureCode
                        : "UNKNOWN";
                    ShowTransitionError(4, ScreenMode.HeroSelect, true,
                        startupStatus + " (error " + stageFourFailureCode + ").");
                    yield break;
                }
                StartupCheckpoint.Set("StageFourWorldBuildCompleted");
            }
            else
            {
                if (gameBootstrap == null)
                    gameBootstrap = gameRoot != null ? gameRoot.GetComponent<GameBootstrap>() : FindFirstObjectByType<GameBootstrap>(FindObjectsInactive.Include);
                if (gameBootstrap == null)
                {
                    startupStatus = "Battlefield bootstrap is missing.";
                    StartupCheckpoint.Set("GameBootstrapMissing");
                    ShowTransitionError(stageNumber, ScreenMode.HeroSelect, true, startupStatus);
                    yield break;
                }

                if (stageNumber == 2)
                {
                    startupStatus = "preparing Iranian village stage...";
                    gameBootstrap.PrepareStageTwoVillage(stageTwoLayoutVariant);
                }
                else if (stageNumber == 1 && forceStageOneWorldRebuildForTest)
                {
                    startupStatus = "rebuilding a clean test world...";
                    forceStageOneWorldRebuildForTest = false;
                    gameBootstrap.PrepareStageOneWorldForTest();
                }
                else if (stageNumber == 1 && !gameBootstrap.IsWorldReady &&
                    !gameBootstrap.IsWorldPreparing && !gameBootstrap.WorldBuildFailed)
                {
                    startupStatus = "requesting battlefield build...";
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
                    ShowTransitionError(stageNumber, ScreenMode.HeroSelect, true, startupStatus);
                    yield break;
                }
            }

            startupStatus = "Activating player...";
            yield return ActivateRoot(playerRoot, "Player");

            player = playerRoot != null
                ? playerRoot.GetComponent<PlayerController>()
                : player;

            if (player == null)
            {
                startupStatus = "Player initialization failed.";
                StartupCheckpoint.Set("PlayerInitializationFailed");
                ShowTransitionError(stageNumber, ScreenMode.HeroSelect, true, startupStatus);
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
                ShowTransitionError(stageNumber, ScreenMode.HeroSelect, true, startupStatus);
                yield break;
            }

            // Stage one keeps its original three firearms. Stage two additionally
            // enables the axe, Void Rifle and Alien Sword when the local profile owns them.
            player.Weapon?.SetAxeEnabledForMatch(stageNumber >= 2);
            if (stageNumber >= 2 && player.Weapon != null)
            {
                WeaponController.WeaponKind selectedKind = (WeaponController.WeaponKind)
                    PlayerPrefs.GetInt("PW.SelectedWeaponKind", (int)WeaponController.WeaponKind.AssaultRifle);
                bool ownsVoidRifle = PlayerPrefs.GetInt("PW.Reward.VoidRifle", 0) != 0;
                bool ownsAlienSword = PlayerPrefs.GetInt("PW.Reward.AlienSword", 0) != 0;
                if (selectedKind == WeaponController.WeaponKind.VoidRifle && !ownsVoidRifle)
                    selectedKind = WeaponController.WeaponKind.AssaultRifle;
                if (selectedKind == WeaponController.WeaponKind.AlienSword && !ownsAlienSword)
                    selectedKind = WeaponController.WeaponKind.AssaultRifle;

                // Stage 4 opens with ranged combat inside the mothership. If a prior
                // store choice left the melee-only alien sword selected, guarantee a
                // firearm for the first encounter; the player can still swap later.
                if (stageNumber == 4 && selectedKind == WeaponController.WeaponKind.AlienSword)
                    selectedKind = WeaponController.WeaponKind.AssaultRifle;

                player.Weapon.SelectWeapon(selectedKind);
            }

            StartupCheckpoint.Set("PlayerPreparedForMatch");

            StartupCheckpoint.Set("MobileInputActivationDeferred");
            if (combatHud != null)
                combatHud.ConfigurePlayer(player);

            startupStatus = "deferring mobile input...";
            yield return null;

            startupStatus = "activating world bounds...";
            yield return ActivateRoot(worldBoundsRoot, "WorldBounds");
            startupStatus = "preparing main camera...";

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
            if (androidRuntimeCamera != null)
            {
                if (mobileInput != null) mobileInput.SetGameplayCamera(null);
                Destroy(androidRuntimeCamera.gameObject);
                androidRuntimeCamera = null;
            }
            androidRuntimeCamera = CreateAndroidRuntimeCamera(player.transform);
            activeCamera = androidRuntimeCamera;
            GraphicsQualityProfile.ApplyToCamera(activeCamera);
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

            if (stageNumber == 3 && stageThreeWorldBuilder != null)
                stageThreeWorldBuilder.ConfigurePlayer(player, activeCamera);
            if (stageNumber == 4 && stageFourWorldBuilder != null)
                stageFourWorldBuilder.ConfigurePlayer(player, activeCamera);

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

            startupStatus = "Preparing gameplay...";
            StartupCheckpoint.Set("MatchCoreReady");
            mode = ScreenMode.Match;
            startingMatch = false;
#if UNITY_ANDROID
            startupStatus = "Preparing camera and controls...";
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
            camera.backgroundColor = new Color(0.15f, 0.16f, 0.17f, 1f);
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

            CameraOcclusionFader occlusionFader = cameraObject.AddComponent<CameraOcclusionFader>();
            occlusionFader.SetTarget(target);
            camera.enabled = false;
            return camera;
        }

        private CameraOcclusionFader cameraOcclusionFader;

        private void LateUpdate()
        {
            // Hold the camera where the fight ended during the evacuation. If the
            // camera kept tracking the player as the ship ascends, the ship would
            // stay centred instead of visibly leaving the frame in two seconds.
            if (stageTwoVictorySequence != null && stageTwoVictorySequence.IsRunning)
                return;
            if (stageNumber == 3 && StageThreeWorldBuilder.IsAnyCinematicActive)
                return;
            if (stageNumber == 4 && stageFourWorldBuilder != null && stageFourWorldBuilder.IsCinematic)
                return;

#if UNITY_ANDROID
            if (androidRuntimeCamera == null || player == null)
                return;

            if (cameraOcclusionFader == null)
                cameraOcclusionFader = androidRuntimeCamera.GetComponent<CameraOcclusionFader>();
            if (cameraOcclusionFader != null)
            {
                cameraOcclusionFader.SetTarget(player.transform);
                cameraOcclusionFader.SetStageTwoMode(stageNumber >= 2);
            }

            Quaternion orbit = Quaternion.Euler(AndroidCameraPitch, AndroidCameraYaw, 0f);
            Vector3 desired = player.transform.position + orbit * Vector3.back * AndroidCameraDistance;
            androidRuntimeCamera.transform.position = Vector3.Lerp(
                androidRuntimeCamera.transform.position,
                desired,
                1f - Mathf.Exp(-12f * Time.deltaTime));

            Vector3 cameraForward = Quaternion.Euler(0f, AndroidCameraYaw, 0f) * Vector3.forward;
            Vector3 lookTarget = player.transform.position
                + cameraForward * AndroidCameraLookAhead
                + Vector3.up * AndroidCameraLookHeight;
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

            // Keep a short camera settling window while preserving the diagnostic checkpoint.
            for (int i = 0; i < 8; i++)
                yield return null;

            StartupCheckpoint.Set("AndroidCameraStable120Frames");

            // DIAGNOSTIC PASS 2: camera is proven stable. Now enable ONLY the mobile
            // input/UI layer. HUD, minimap rendering and enemies remain disabled.
            StartupCheckpoint.Set("AndroidPlayerActivationStarted");

            // GateGameplay(false) deliberately disabled the player during the
            // crash-isolation phase. Re-enable it before arming touch input so
            // SetMoveInput/Weapon/Aim can actually drive the live player.
            if (player != null)
            {
                player.enabled = true;
                player.SetMovementEnabled(true);
                StartupCheckpoint.Set("AndroidPlayerActivated");
            }

            StartupCheckpoint.Set("AndroidInputActivationStarted");

            // MobileInputHub is now the single Android touch owner. Movement and all
            // combat buttons share one dispatcher so multi-touch cannot overwrite movement.
            if (mobileInput != null)
            {
                mobileInput.ActivateForMatch(player, activeCamera);
                mobileInput.enabled = true;
            }

            StartupCheckpoint.Set("AndroidInputActivated");

            // Let joystick/buttons settle briefly before enabling the HUD.
            for (int i = 0; i < 8; i++)
                yield return null;

            StartupCheckpoint.Set("AndroidInputStable120Frames");
            startupStatus = "INPUT STABLE — enabling gameplay services one at a time";

            // PASS 3: enable only the lightweight HUD, then prove it is stable.
            StartupCheckpoint.Set("AndroidHudActivationStarted");
            if (combatHud == null)
                combatHud = FindFirstObjectByType<RuntimeCombatHUD>(FindObjectsInactive.Include);
            if (combatHud != null)
            {
                combatHud.ConfigurePlayer(player);
                // PrototypeFlow.DrawAndroidPresentationHUD now owns the Android HUD.
                // Keep this overlapping secondary panel off on Android.
                combatHud.enabled = false;
            }

            for (int i = 0; i < 8; i++)
                yield return null;

            StartupCheckpoint.Set("AndroidHudStable120Frames");

            // Production Android path: the core, camera, touch input and HUD have
            // each survived a clean 120-frame window. Keep the lightweight schematic
            // minimap in PrototypeFlow (no extra RenderTexture camera), then release
            // the existing enemy service in a delayed, bounded configuration.
            StartupCheckpoint.Set("AndroidGameplayServicesActivationStarted");
            startupStatus = "GAMEPLAY STABLE — preparing city activity";

            if (enemySpawner == null)
                enemySpawner = gameRoot != null
                    ? gameRoot.GetComponentInChildren<EnemySpawner>(true)
                    : FindFirstObjectByType<EnemySpawner>(FindObjectsInactive.Include);

            yield return new WaitForSecondsRealtime(0.15f);

            if (enemySpawner != null && player != null)
            {
                if (stageNumber == 3 || stageNumber == 4)
                {
                    enemySpawner.enabled = false;
                }
                else
                {
                    if (stageNumber == 2)
                    {
                        StageTwoArenaManager arena = GetComponent<StageTwoArenaManager>();
                        if (arena == null) arena = gameObject.AddComponent<StageTwoArenaManager>();
                        arena.BeginArena(player.transform, 9);
                        enemySpawner.ConfigureStageTwoArena(player.transform);
                    }
                    else
                    {
                        enemySpawner.Configure(player.transform, 24, 84f, 0f);
                    }
                    enemySpawner.enabled = true;
                }
                StartupCheckpoint.Set("AndroidEnemyServiceEnabled");
            }

            // Do not create the secondary minimap RenderTexture on Android. The
            // presentation minimap above is intentionally GPU-light and stable.
            StartupCheckpoint.Set("AndroidGameplayServicesReady");
            startupStatus = "MATCH READY";
            matchInputArmed = true;
            yield break;
#else
            yield return null;

            // The desktop/editor branch uses the same explicit activation contract as
            // Android. Enabling the MonoBehaviour alone leaves matchActive false, so
            // MobileInputHub.Update would return before reading keyboard/mouse input.
            if (mobileInput != null)
            {
                mobileInput.ActivateForMatch(player, activeCamera);
                mobileInput.EnableMinimap();
                mobileInput.enabled = true;
            }

            matchInputArmed = true;
            if (player == null) yield break;

            if (enemySpawner == null)
                enemySpawner = FindFirstObjectByType<EnemySpawner>(FindObjectsInactive.Include);

            if (enemySpawner != null)
            {
                if (stageNumber == 3 || stageNumber == 4)
                {
                    enemySpawner.enabled = false;
                }
                else
                {
                    if (stageNumber == 2)
                    {
                        StageTwoArenaManager arena = GetComponent<StageTwoArenaManager>();
                        if (arena == null) arena = gameObject.AddComponent<StageTwoArenaManager>();
                        arena.BeginArena(player.transform, 9);
                        enemySpawner.ConfigureStageTwoArena(player.transform);
                    }
                    else
                    {
                        enemySpawner.Configure(player.transform, 24, 84f, 0f);
                    }
                    enemySpawner.enabled = true;
                }
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

        
        private string GetStageTwoResultMessage()
        {
            StageTwoArenaManager arena = GetComponent<StageTwoArenaManager>();
            int rank = arena != null ? arena.LastLocalRank : 0;
            if (rank == 1) return "1ST PLACE  •  NEXT STAGE UNLOCKED  •  +300 COINS";
            if (rank == 2) return "2ND PLACE  •  VOID RIFLE REWARD IN STORE";
            if (rank == 3) return "3RD PLACE  •  ALIEN SWORD REWARD IN STORE";
            return "ARENA CLEARED  •  FINAL RANK #" + rank + "  •  CHECK THE STORE";
        }

        private void DrawPreEvacuationResultPanel(GameSession session, string resultText,
            string continueLabel, System.Action onContinue)
        {
            float scale = GetUiScale();
            float width = Mathf.Min(Screen.width - 40f * scale, 680f * scale);
            float height = Mathf.Min(Screen.height - 40f * scale, 260f * scale);
            Rect panel = new Rect((Screen.width - width) * 0.5f,
                (Screen.height - height) * 0.5f, width, height);
            Fill(panel, new Color(0.025f, 0.05f, 0.08f, 0.97f));
            Fill(new Rect(panel.x, panel.y, panel.width, 5f * scale),
                new Color(0.22f, 0.78f, 0.34f, 1f));
            GUI.Label(new Rect(panel.x + 18f * scale, panel.y + 20f * scale,
                panel.width - 36f * scale, 42f * scale), "VICTORY", headerStyle);
            GUI.Label(new Rect(panel.x + 18f * scale, panel.y + 70f * scale,
                panel.width - 36f * scale, 36f * scale), resultText, bodyStyle);
            GUI.Label(new Rect(panel.x + 18f * scale, panel.y + 112f * scale,
                panel.width - 36f * scale, 28f * scale),
                "SCORE " + session.Score + "   •   HOSTILES " + session.EnemiesDefeated, smallStyle);
            float buttonWidth = Mathf.Min(width - 36f * scale, 440f * scale);
            Rect button = new Rect(panel.center.x - buttonWidth * 0.5f,
                panel.yMax - 58f * scale, buttonWidth, 42f * scale);
            if (GUI.Button(button, continueLabel, buttonStyle))
            {
                onContinue?.Invoke();
                StartupCheckpoint.Set("VictoryResultAcknowledged_Stage" + stageNumber);
            }
        }

        private void RestartCurrentStageInPlace()
        {
            int restartStage = Mathf.Clamp(stageNumber, 1, 4);
            Time.timeScale = 1f;
            if (mobileInput != null) mobileInput.DeactivateMatch();
            GateGameplay(false);
            if (activeCamera != null) activeCamera.enabled = false;
            if (androidRuntimeCamera != null)
            {
                Destroy(androidRuntimeCamera.gameObject);
                androidRuntimeCamera = null;
            }
            activeCamera = null;
            followCamera = null;
            if (playerRoot != null) playerRoot.SetActive(false);
            if (gameRoot != null) gameRoot.SetActive(false);
            StageThreeWorldBuilder.ClearExistingWorld();
            StageFourWorldBuilder.ClearExistingWorld();
            if (stageThreeWorldBuilder != null) { Destroy(stageThreeWorldBuilder); stageThreeWorldBuilder = null; }
            if (stageFourWorldBuilder != null) { Destroy(stageFourWorldBuilder); stageFourWorldBuilder = null; }
            if (GameSession.Instance != null) GameSession.Instance.ResetMatch();
            stageNumber = restartStage;
            forceStageOneWorldRebuildForTest = restartStage == 1;
            stageTwoLobbyReady = restartStage != 2;
            StageFourEscapeSequence oldEscape = gameRoot != null ? gameRoot.GetComponent<StageFourEscapeSequence>() : null;
            if (oldEscape != null) Destroy(oldEscape);
            stageThreeResultAcknowledged = false;
            spawnChosen = true;
            spawnWorld = restartStage == 3 ? new Vector2(0f, -97f) : restartStage == 4 ? Vector2.zero : spawnWorld;
            matchPaused = false;
            resultOverlayStartTime = -1f;
            stageThreePreloadInProgress = false;
            stageThreePreloadStatus = string.Empty;
            startingMatch = false;
            mode = ScreenMode.MainMenu;
            startupStatus = "Restarting current mission...";
            StartCoroutine(BeginMainGameAfterMenuFrame());
        }

        private void DrawAndroidResultOverlay()
        {
            EnsureUiInitialized();

            GameSession session = GameSession.Instance;
            if (session == null || !session.IsFinished)
                return;
            // Stage 2 victory is a separate evacuation sequence, triggered by
            // EnemySpawner only after the final wave has spawned and every alien is dead.
            if (session.PlayerWon && stageNumber == 2)
            {
                // The final standings are informational, not a confirmation step.
                // Update() starts the evacuation automatically; until it starts,
                // keep the results on screen and do not expose PLAY AGAIN/MAIN MENU.
                DrawStageTwoFinalResultsOverlay();
                return;
            }
            if (session.PlayerWon && stageNumber == 3 && !stageThreeResultAcknowledged)
            {
                DrawPreEvacuationResultPanel(session,
                    "STAGE 3 COMPLETE  •  CAMPAIGN REWARD SECURED",
                    "CONTINUE TO STAGE 4", () => stageThreeResultAcknowledged = true);
                return;
            }

            // The blue extraction gate is reserved for Stage 1's final opponent.
            if (session.PlayerWon && stageNumber == 1)
            {
                float elapsed = Mathf.Max(0f, Time.unscaledTime - resultOverlayStartTime);
                if (elapsed < 1.65f)
                {
                    float pulse = 0.72f + 0.28f * Mathf.Sin(Time.unscaledTime * 8f);
                    float beamWidth = Mathf.Max(18f, Screen.width * 0.055f);
                    float beamX = Screen.width * 0.5f;
                    Fill(new Rect(beamX - beamWidth * 1.8f, 0f, beamWidth * 3.6f, Screen.height), new Color(0.10f, 0.44f, 1f, 0.12f * pulse));
                    Fill(new Rect(beamX - beamWidth * 0.55f, 0f, beamWidth * 1.1f, Screen.height), new Color(0.18f, 0.58f, 1f, 0.24f * pulse));
                    Fill(new Rect(beamX - beamWidth * 0.12f, 0f, beamWidth * 0.24f, Screen.height), new Color(0.72f, 0.91f, 1f, 0.52f * pulse));
                    DrawCircle(new Vector2(beamX, Screen.height * 0.82f), Screen.width * (0.08f + 0.02f * pulse), new Color(0.14f, 0.60f, 1f, 0.75f * pulse));
                    GUI.Label(new Rect(0f, Screen.height * 0.20f, Screen.width, 70f), "✦  BLUE EXTRACTION GATE  ✦", menuHeaderStyle);
                    GUI.Label(new Rect(0f, Screen.height * 0.30f, Screen.width, 42f), "PREPARING NEXT STAGE...", menuBodyStyle);
                    return;
                }
                if (stageNumber == 1)
                {
                    // The mission only becomes a win after the player enters the
                    // captive-house extraction beam, which has already rescued Anahita.
                    BeginStageTwoSelection();
                    return;
                }
            }
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
                    ? (stageNumber == 2 ? GetStageTwoResultMessage()
                        : stageNumber >= 3 ? "MISSION COMPLETE  •  CAMPAIGN REWARD SECURED"
                        : "Extraction reached. Mission complete.")
                    : "Your fighter was defeated.",
                bodyStyle);

            GUI.Label(
                new Rect(panel.x + 20f, panel.y + 146f, panel.width - 40f, 28f),
                "SCORE  " + session.Score + "    ENEMIES  " + session.EnemiesDefeated,
                smallStyle);

            float buttonGap = 14f;
            float buttonWidth = (panel.width - 64f - buttonGap) * 0.5f;
            float buttonY = panel.y + panel.height - 82f;
            if (GUI.Button(
                new Rect(panel.x + 32f, buttonY, buttonWidth, 54f),
                "PLAY AGAIN",
                buttonStyle))
            {
                if (stageTestRun)
                {
                    // Keep the saved campaign stage while restarting the requested test;
                    // reloading the scene here would lose this temporary test-mode state.
                    BeginStageTestEntry(stageNumber);
                }
                else if (stageNumber == 2)
                {
                    Time.timeScale = 1f;
                    if (GameSession.Instance != null) GameSession.Instance.ResetMatch();
                    BeginStageTwoSelection();
                }
                else
                {
                    RestartCurrentStageInPlace();
                }
            }

            if (GUI.Button(
                new Rect(panel.x + 32f + buttonWidth + buttonGap, buttonY, buttonWidth, 54f),
                "MAIN MENU",
                buttonStyle))
            {
                GoToMainMenu();
            }
        }

        // Dedicated shortcut so Stage 3 can be entered from the Stage 2 result
        // screen without replaying Stage 1. Stage 3 world construction is added
        // separately; this only establishes the independent test entry point.
        
        private void EnsureUiInitialized()
        {
            if (pixel == null)
                CreateTextures();
            if (titleStyle == null || uiStyleScreenHeight != Screen.height)
                BuildStyles();
            if (menuTitleStyle == null || menuStyleScreenHeight != Screen.height)
                BuildMenuStyles();
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
            float scale = GetUiScale();
            uiStyleScreenHeight = Screen.height;
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(34f * scale), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            headerStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(32f * scale), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(20f * scale), alignment = TextAnchor.MiddleCenter };
            smallStyle = new GUIStyle(GUI.skin.label) { fontSize = Mathf.RoundToInt(19f * scale), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            objectiveStyle = new GUIStyle(smallStyle) { fontSize = Mathf.RoundToInt(16f * scale), wordWrap = true, alignment = TextAnchor.MiddleLeft };
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = Mathf.RoundToInt(20f * scale), fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        }
        private void BuildMenuStyles()
        {
            float scale = GetUiScale();
            menuStyleScreenHeight = Screen.height;

            menuTitleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(44f * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft,
                wordWrap = false
            };
            menuTitleStyle.normal.textColor = new Color(1f, 0.84f, 0.42f, 1f);

            menuHeaderStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(42f * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            menuHeaderStyle.normal.textColor = new Color(1f, 0.88f, 0.58f, 1f);

            menuBodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(25f * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            menuBodyStyle.normal.textColor = Color.white;

            menuSmallStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(24f * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true
            };
            menuSmallStyle.normal.textColor = new Color(0.91f, 0.94f, 0.98f, 1f);

            menuButtonStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(30f * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
                wordWrap = true,
                padding = new RectOffset(8, 8, 4, 4)
            };
            menuButtonStyle.normal.textColor = new Color(1f, 0.92f, 0.72f, 1f);
        }

        private bool DrawFantasyButton(Rect rect, string label, bool selected = false)
        {
            float scale = GetUiScale();
            bool hovered = Event.current != null && rect.Contains(Event.current.mousePosition);
            Color edge = selected || hovered
                ? new Color(0.30f, 0.94f, 0.98f, 0.92f)
                : new Color(0.35f, 0.66f, 0.78f, 0.68f);

            // Layered translucent glass with a lower shadow and top bevel gives
            // menu controls a raised, readable appearance without changing layout.
            Fill(new Rect(rect.x + 3f * scale, rect.y + 5f * scale, rect.width, rect.height),
                new Color(0f, 0f, 0f, 0.36f));
            Fill(rect, edge);
            float inset = Mathf.Max(2f, 2.5f * scale);
            Rect inner = new Rect(rect.x + inset, rect.y + inset,
                Mathf.Max(0f, rect.width - inset * 2f), Mathf.Max(0f, rect.height - inset * 2f));
            Fill(inner, new Color(0.025f, 0.075f, 0.12f, 0.78f));
            Fill(new Rect(inner.x + 2f * scale, inner.y + 2f * scale,
                Mathf.Max(0f, inner.width - 4f * scale), Mathf.Max(1f, rect.height * 0.20f)),
                new Color(0.52f, 0.86f, 0.98f, selected || hovered ? 0.25f : 0.13f));
            Fill(new Rect(inner.x + 2f * scale, inner.yMax - 3f * scale,
                Mathf.Max(0f, inner.width - 4f * scale), 2f * scale),
                new Color(0f, 0f, 0f, 0.30f));
            Fill(new Rect(inner.x + 2f * scale, inner.y + 2f * scale,
                2f * scale, Mathf.Max(0f, inner.height - 4f * scale)),
                new Color(0.65f, 0.94f, 1f, 0.34f));

            bool clicked = GUI.Button(rect, GUIContent.none, GUIStyle.none);
            GUI.Label(rect, label, menuButtonStyle);
            // Native Android menu touch handling can change screens in Update before
            // the synthesized IMGUI mouse event is processed. Suppress that same-frame
            // click so the new screen cannot accidentally activate a second button.
            if (Time.frameCount == suppressMenuButtonClicksFrame)
                return false;
            return clicked;
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