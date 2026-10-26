using System.Collections;
using UnityEngine;
using PersiaWar.Unity2D5D.UI.Loadout;

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
            StageTestSelect,
            HeroSelect,
            Loadout,
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
        private const float AndroidCameraPitch = 31f;
        private const float AndroidCameraYaw = 32f;
        private const float AndroidCameraDistance = 18.5f;
        private const float AndroidCameraLookHeight = 0.90f;
        private const float AndroidCameraLookAhead = 4.5f;
        private EnemyChase[] minimapEnemies = new EnemyChase[0];
        private PickupItem[] minimapPickups = new PickupItem[0];
        private float nextMinimapRefresh;
        private Vector2 spawnWorld = new Vector2(0f, -4f);
        private bool spawnChosen;
        private int selectedHero;
        private bool loadoutWeaponTab;
        private int lastStageReward;
        private string loadoutNotice = string.Empty;
        private float loadoutSlideOffset;
        private float loadoutSlideVelocity;
        private readonly System.Collections.Generic.Dictionary<int, float> menuButtonScales =
            new System.Collections.Generic.Dictionary<int, float>();
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
        private float resultOverlayStartTime = -1f;
        private StageTwoVictorySequence stageTwoVictorySequence;
        private StageThreeWorldBuilder stageThreeWorldBuilder;
        private StageFourWorldBuilder stageFourWorldBuilder;
        private bool forceStageOneWorldRebuildForTest;
        private bool stageTestRun;
        private int savedCampaignStageNumber = 1;
        private int savedStage3Unlocked;
        private int savedStage4Unlocked;
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
        private int menuStyleScreenHeight = -1;
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
            InitializeLocalProfile();
            LoadoutStore.EnsureDefaults();
            stageNumber = LoadoutStore.GetNextCampaignStage();
            selectedHero = LoadoutStore.SelectedHero;
            selectedStoreWeaponKind = (int)LoadoutStore.SelectedWeapon;
#if UNITY_ANDROID
            // Android production path: keep the real front-end interactive.
            // Do NOT auto-start the match. The previous isolation build skipped the
            // hero/drop-map screens and immediately activated the heaviest gameplay
            // systems, which made the app appear to "rush through" the choices.
            // Preserve the saved hero selection; forcing Ardeshir here made Anahita appear to disappear on every Android app launch.
            selectedHero = LoadoutStore.SelectedHero;
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

            mode = ScreenMode.DropMap;
            StartMatch();
        }

        private void Update()
        {
            if (mode == ScreenMode.Loadout)
                loadoutSlideOffset = Mathf.SmoothDamp(loadoutSlideOffset, 0f, ref loadoutSlideVelocity, 0.18f, Mathf.Infinity, Time.unscaledDeltaTime);
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
                        if (stageTwoVictorySequence == null)
                            StartStageTwoVictorySequence();
                        else if (stageTwoVictorySequence.IsComplete)
                            CompleteStageTwoVictoryTransition();
                    }
                    else if (stageNumber == 3 && session.PlayerWon &&
                        stageThreeWorldBuilder != null && stageThreeWorldBuilder.IsComplete)
                    {
                        CompleteStageThreeTransition();
                    }
                    return;
                }

                CheckStageOneGirlRescue();
                HandleAndroidPauseTouches();
                return;
            }
            if (mode == ScreenMode.StageTestSelect)
            {
                HandleStageTestSelectTouches();
                return;
            }
            if (mode == ScreenMode.MainMenu || mode == ScreenMode.Profile ||
                mode == ScreenMode.Store || mode == ScreenMode.Settings)
            {
                HandleLobbyTouches();
                return;
            }
            if (mode == ScreenMode.Loadout) { HandleLoadoutTouches(); return; }
            if (mode == ScreenMode.HeroSelect)
            {
                HandleHeroSelectTouches();
                return;
            }
            if (mode == ScreenMode.DropMap)
                HandleDropTouches();
        }

        private void HandleAndroidPauseTouches()
        {
            if (!Application.isMobilePlatform || Input.touchCount <= 0)
                return;

            float scale = Mathf.Clamp(
                Mathf.Min(Screen.width, Screen.height) / 720f,
                0.75f,
                1.35f);
            float size = 58f * scale;
            Rect button = new Rect(
                Screen.width * 0.5f - size * 0.5f,
                14f * scale,
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

                matchPaused = !matchPaused;
                Time.timeScale = matchPaused ? 0f : 1f;
                if (mobileInput != null)
                    mobileInput.SetPaused(matchPaused);

                StartupCheckpoint.Set(
                    matchPaused ? "MatchPausedByNativeTouch" : "MatchResumedByNativeTouch");
                return;
            }
        }

        private void OnGUI()
        {
            EnsureUiInitialized();
            if (mode == ScreenMode.Match)
            {
#if UNITY_ANDROID
                if (stageTwoVictorySequence != null && stageTwoVictorySequence.IsRunning)
                {
                    DrawStageTwoCinematicOverlay();
                    return;
                }

                if (stageNumber == 3 && stageThreeWorldBuilder != null && stageThreeWorldBuilder.IsCinematic)
                {
                    if (stageThreeWorldBuilder.IsIntroCinematic)
                        DrawStageThreeIntroOverlay();
                    else
                        DrawStageThreeCinematicOverlay();
                    return;
                }

                if (stageNumber == 4 && stageFourWorldBuilder != null && stageFourWorldBuilder.IsCinematic)
                {
                    DrawStageFourIntroOverlay();
                    return;
                }
                // Stage 4 finale uses its own cinematic captions; keep the tactical HUD
                // and pause overlay out of the exterior separation/destruction shot.
                if (stageNumber == 4 && StageFourEscapeSequence.IsFinaleActive)
                    return;
                DrawAndroidPresentationHUD();
                if (stageNumber == 3)
                    DrawStageThreeHUD();
                if (stageNumber == 4)
                    DrawStageFourObjective();
                DrawAndroidPauseControl();
                if (stageNumber != 3 && stageNumber != 4)
                    DrawGirlRescueNotice();

                if (GameSession.Instance != null && GameSession.Instance.IsFinished)
                {
                    if (resultOverlayStartTime < 0f)
                    {
                        resultOverlayStartTime = Time.unscaledTime;
                        // Stage 1's ExtractionBeacon is already attached to Anahita's
                        // randomized house and is shown when the final wave is eliminated.
                        // Do not draw a second, unrelated beam after mission completion.
                    }
                    DrawAndroidResultOverlay();
                }
#endif
                return;
            }
            DrawBackdrop();

            if (stageTwoLobbyRunning)
            {
                DrawStageTwoLobbyOverlay();
                return;
            }

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
            switch (mode)
            {
                case ScreenMode.MainMenu:
                    DrawMainMenu();
                    break;
                case ScreenMode.Profile:
                    DrawProfileScreen();
                    break;
                case ScreenMode.Store:
                    DrawStoreScreen();
                    break;
                case ScreenMode.Settings:
                    DrawSettingsScreen();
                    break;
                case ScreenMode.StageTestSelect:
                    DrawStageTestSelect();
                    break;
                case ScreenMode.HeroSelect:
                    if (stageNumber >= 2 && stageTwoShopOpen)
                        DrawCharacterShop();
                    else
                        DrawHeroSelect();
                    break;
                case ScreenMode.Loadout:
                    DrawLoadoutScreen();
                    break;
                default:
                    DrawDropMap();
                    break;
            }
        }

        private void DrawAndroidPresentationHUD()
        {
            float scale = Mathf.Clamp(Mathf.Min(Screen.width, Screen.height) / 720f, 0.75f, 1.35f);
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
            Rect left = new Rect(margin, margin, leftW, leftH);
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
            Rect right = new Rect(Screen.width - rightW - margin, margin, rightW, 58f * scale);
            Fill(right, new Color(0.03f, 0.06f, 0.08f, 0.88f));
            GUI.Label(new Rect(right.x + 12f * scale, right.y + 6f * scale, right.width - 24f * scale, 22f * scale),
                "SURVIVORS", smallStyle);
            GUI.Label(new Rect(right.x + 12f * scale, right.y + 27f * scale, right.width - 24f * scale, 24f * scale),
                survivorCount.ToString(), headerStyle);

            // Tactical minimap remains see-through so the 3D battle stays visible.
            float mapSize = Mathf.Clamp(148f * scale * 1.38f * 1.15f * 1.20f, 180f, 314f);
            Rect map = new Rect(Screen.width - mapSize - margin, 84f * scale, mapSize, mapSize);
            Fill(map, new Color(0.12f, 0.28f, 0.12f, 0.42f));

            Color frameColor = new Color(0.92f, 0.95f, 0.98f, 0.46f);
            float frame = Mathf.Max(1f, 1.5f * scale);
            Fill(new Rect(map.x, map.y, map.width, frame), frameColor);
            Fill(new Rect(map.x, map.yMax - frame, map.width, frame), frameColor);
            Fill(new Rect(map.x, map.y, frame, map.height), frameColor);
            Fill(new Rect(map.xMax - frame, map.y, frame, map.height), frameColor);

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
                "N ↑   MAP   P:YOU   E:ENEMY", smallStyle);

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
            string counterText = stageNumber >= 2
                ? "WAVE " + wave + "/5   KILLS " + (arenaManager != null ? arenaManager.LocalKills : 0)
                : "WAVE  " + wave + "     SCORE  " + score;
            GUI.Label(new Rect(counters.x + 8f * scale, counters.y, counters.width - 16f * scale, counters.height),
                counterText, smallStyle);

            if (stageNumber >= 2 && arenaManager != null)
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
            MenuTheme.DrawBackdrop(pixel);
            Rect topBar = new Rect(0f, 0f, Screen.width, Mathf.Min(110f, Screen.height * 0.16f));
            Fill(topBar, new Color(0.02f, 0.035f, 0.06f, 0.62f));
            GUI.Label(new Rect(28f, 12f, Screen.width * 0.62f, 56f), "♛  PERSIA WAR", menuTitleStyle);
            GUI.Label(new Rect(30f, 64f, Screen.width * 0.60f, 32f), "CLASSIC BATTLE ROYALE", menuSmallStyle);
        }

        private void InitializeLocalProfile()
        {
            playerId = PlayerPrefs.GetString("PW.PlayerId", string.Empty);
            if (string.IsNullOrEmpty(playerId))
            {
                playerId = System.Guid.NewGuid().ToString("N").Substring(0, 12).ToUpperInvariant();
                PlayerPrefs.SetString("PW.PlayerId", playerId);
            }

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
            const int buttonCount = 9;
            float scale = Mathf.Clamp(Mathf.Min(Screen.width, Screen.height) / 720f, 0.55f, 1.2f);
            float width = Mathf.Min(Screen.width * 0.42f, 520f * scale);
            float heightLimit = Mathf.Max(26f, (Screen.height - 112f * scale) / (buttonCount + 1f));
            float height = Mathf.Min(58f * scale, heightLimit);
            float gapLimit = Mathf.Max(2f, (Screen.height - 92f * scale - height * buttonCount) / (buttonCount - 1f));
            float gap = Mathf.Min(12f * scale, gapLimit);
            float totalHeight = height * buttonCount + gap * (buttonCount - 1);
            float topLimit = Mathf.Max(42f * scale, Screen.height - totalHeight - 6f);
            float top = Mathf.Clamp((Screen.height - totalHeight) * 0.46f, 42f * scale, topLimit);
            float left = Mathf.Max(Screen.width * 0.52f, Screen.width - width - 28f);
            left = Mathf.Min(left, Screen.width - width - 18f);
            return new Rect(left, top + index * (height + gap), width, height);
        }

        private Rect GetLobbyBackRect()
        {
            float scale = Mathf.Clamp(Mathf.Min(Screen.width, Screen.height) / 720f, 0.72f, 1.2f);
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
                    // Keep native Android hit-testing aligned with all nine menu buttons,
                    // including the temporary direct stage-test entry.
                    for (int button = 0; button < 9; button++)
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
                    {
                        string cleanName = string.IsNullOrWhiteSpace(profileNameInput)
                            ? "Persian Warrior" : profileNameInput.Trim();
                        profileNameInput = cleanName.Length > 20 ? cleanName.Substring(0, 20) : cleanName;
                        PlayerPrefs.SetString("PW.PlayerName", profileNameInput);
                        PlayerPrefs.Save();
                    }
                    mode = ScreenMode.MainMenu;
                    return;
                }

                if (mode == ScreenMode.Store)
                {
                    for (int item = 0; item < StoreCosmeticNames.Length; item++)
                    {
                        if (item >= 3 && GetStoreSellRect(item).Contains(point))
                        {
                            suppressMenuButtonClicksFrame = Time.frameCount;
                            SellStoreWeapon(item);
                            return;
                        }
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

        private string GetCampaignStageButtonLabel(int targetStage)
        {
            if (!LoadoutStore.IsCampaignStageAvailable(targetStage)) return "STAGE " + targetStage + "  •  LOCKED";
            return "STAGE " + targetStage + "  •  " +
                (LoadoutStore.IsCampaignStageCompleted(targetStage) ? "REPLAY" : "PLAY");
        }

        private void DrawMainMenu()
        {
            string displayName=PlayerPrefs.GetString("PW.PlayerName","Persian Warrior");
            int coins=LoadoutStore.Coins,tokens=LoadoutStore.Tokens;
            Rect card=new Rect(Screen.width*.07f,Screen.height*.19f,Screen.width*.28f,Screen.height*.60f);
            Fill(card,new Color(.025f,.055f,.085f,.88f));
            Fill(new Rect(card.x,card.y,6f,card.height),new Color(.95f,.69f,.20f,1f));
            GUI.Label(new Rect(card.x+18f,card.y+18f,card.width-36f,42f),"PLAYER",menuHeaderStyle);
            GUI.Label(new Rect(card.x+18f,card.y+76f,card.width-36f,48f),displayName,menuBodyStyle);
            GUI.Label(new Rect(card.x+18f,card.y+132f,card.width-36f,32f),"ID  "+playerId,menuSmallStyle);
            GUI.Label(new Rect(card.x+18f,card.y+184f,card.width-36f,32f),"COINS  "+coins,menuBodyStyle);
            GUI.Label(new Rect(card.x+18f,card.y+220f,card.width-36f,32f),"TOKENS  ✦ "+tokens,menuBodyStyle);
            GUI.Label(new Rect(card.x+18f,card.y+card.height-68f,card.width-36f,48f),"OFFLINE PROFILE",menuSmallStyle);
            GUI.Label(new Rect(Screen.width*.40f,Screen.height*.055f,Screen.width*.54f,46f),"CAMPAIGN  •  STAGE SELECT",menuBodyStyle);
            string[] labels={"CONTINUE CAMPAIGN","PLAYER PROFILE","STORE / WEAPON ARMORY","SETTINGS",
                GetCampaignStageButtonLabel(1),GetCampaignStageButtonLabel(2),GetCampaignStageButtonLabel(3),GetCampaignStageButtonLabel(4),
                "TEST STAGES  •  NO CAMPAIGN PROGRESS"};
            for(int i=0;i<labels.Length;i++)
            {
                Rect button=GetLobbyButtonRect(i);
                bool available=i<4||i==8||LoadoutStore.IsCampaignStageAvailable(i-3);
                if(DrawFantasyButton(button,labels[i],false,available))ActivateLobbyButton(i);
            }
            if(!string.IsNullOrEmpty(lobbyNotice)&&Time.unscaledTime<lobbyNoticeUntil)
            {
                Rect lastButton=GetLobbyButtonRect(8);
                float scale=Mathf.Clamp(Mathf.Min(Screen.width,Screen.height)/720f,.55f,1.2f);
                float h=34f*scale,y=Mathf.Min(lastButton.yMax+6f*scale,Screen.height-58f*scale-34f*scale);
                Rect notice=new Rect(Screen.width*.52f,y,Screen.width*.42f,h);
                Fill(notice,new Color(.025f,.06f,.09f,.95f));GUI.Label(notice,lobbyNotice,menuSmallStyle);
            }
            GUI.Label(new Rect(Screen.width*.40f,Screen.height-42f,Screen.width*.54f,24f),"PERSIA WAR  •  UNITY EDITION",menuSmallStyle);
        }

        private void ActivateLobbyButton(int index)
        {
            if(index==0){StartCampaignFromMenu();return;}
            if(index==1)mode=ScreenMode.Profile;
            else if(index==2)mode=ScreenMode.Store;
            else if(index==3)mode=ScreenMode.Settings;
            else if(index>=4&&index<=7)BeginCampaignStageEntry(index-3);
            else if(index==8)
            {
                mode=ScreenMode.StageTestSelect;
                StartupCheckpoint.Set("DirectStageTestMenuOpened");
                return;
            }
            StartupCheckpoint.Set("LobbyNavigation_"+mode);
        }

        private void StartCampaignFromMenu()
        {
            if(stageTestRun)RestoreCampaignProgressAfterTest();
            stageNumber=LoadoutStore.GetNextCampaignStage();
            if(stageNumber>4){ShowLobbyNotice("CAMPAIGN COMPLETE  •  SELECT AN UNLOCKED STAGE TO REPLAY.");return;}
            forceStageOneWorldRebuildForTest=stageNumber==1;
            selectedHero=LoadoutStore.SelectedHero;
            selectedStoreWeaponKind=(int)LoadoutStore.SelectedWeapon;
            loadoutWeaponTab=false;mode=ScreenMode.Loadout;spawnChosen=false;
            StartupCheckpoint.Set("CampaignContinue_Stage"+stageNumber);
        }

        private void BeginCampaignStageEntry(int targetStage)
        {
            if(targetStage<1||targetStage>4)return;
            if(stageTestRun)RestoreCampaignProgressAfterTest();
            if(!LoadoutStore.IsCampaignStageAvailable(targetStage))
            {
                ShowLobbyNotice("STAGE "+targetStage+" IS LOCKED  •  COMPLETE STAGE "+Mathf.Max(1,targetStage-1)+" FIRST.");
                return;
            }
            stageTestRun=false;
            StageThreeWorldBuilder.ClearExistingWorld();
            StageFourWorldBuilder.ClearExistingWorld();
            StageTwoArenaManager.Instance?.ResetForStageChange();
            Time.timeScale=1f;lobbyNotice=string.Empty;lobbyNoticeUntil=0f;
            matchPaused=false;startingMatch=false;spawnChosen=false;resultOverlayStartTime=-1f;
            stageTwoLobbyRunning=false;stageTwoLobbyReady=false;stageTwoLobbyPlayers=1;
            if(stageTwoVictorySequence!=null)
            {stageTwoVictorySequence.FinishAndCleanup();Destroy(stageTwoVictorySequence);stageTwoVictorySequence=null;}
            StageFourEscapeSequence escape=gameRoot!=null?gameRoot.GetComponent<StageFourEscapeSequence>():null;
            if(escape!=null)Destroy(escape);
            if(stageFourWorldBuilder!=null)Destroy(stageFourWorldBuilder);
            stageThreeWorldBuilder=null;stageFourWorldBuilder=null;
            selectedHero=LoadoutStore.SelectedHero;selectedStoreWeaponKind=(int)LoadoutStore.SelectedWeapon;
            stageTwoOriginalHero=selectedHero;stageTwoLandingMode=0;stageTwoLayoutVariant=0;stageTwoShopOpen=false;
            loadoutWeaponTab=false;forceStageOneWorldRebuildForTest=targetStage==1;
            if(GameSession.Instance!=null)GameSession.Instance.ResetMatch();
            if(activeCamera!=null)activeCamera.enabled=false;
            if(androidRuntimeCamera!=null)androidRuntimeCamera.enabled=false;
            if(followCamera!=null)followCamera.enabled=false;
            GateGameplay(false);
            if(targetStage==2){BeginStageTwoSelection();StartupCheckpoint.Set("CampaignStageEntry_2");return;}
            stageNumber=targetStage;
            if(targetStage==1){stageTwoOriginalHero=0;ExtractionBeacon.ClearForStageTransition();spawnWorld=Vector2.zero;}
            else if(targetStage==3)spawnWorld=new Vector2(0f,-97f);
            else spawnWorld=Vector2.zero;
            spawnChosen=targetStage>=3;mode=ScreenMode.Loadout;
            StartupCheckpoint.Set("CampaignStageEntry_"+targetStage);
        }

        private void CaptureCampaignProgressBeforeTest()
        {
            if (stageTestRun) return;
            savedCampaignStageNumber = stageNumber;
            savedStage3Unlocked = PlayerPrefs.GetInt("PW.Stage3Unlocked", 0);
            savedStage4Unlocked = PlayerPrefs.GetInt("PW.Stage4Unlocked", 0);
            stageTestRun = true;
        }

        private void RestoreCampaignProgressAfterTest()
        {
            if (!stageTestRun) return;
            stageNumber = Mathf.Clamp(savedCampaignStageNumber, 1, 4);
            PlayerPrefs.SetInt("PW.Stage3Unlocked", savedStage3Unlocked);
            PlayerPrefs.SetInt("PW.Stage4Unlocked", savedStage4Unlocked);
            PlayerPrefs.Save();
            stageTestRun = false;
            forceStageOneWorldRebuildForTest = stageNumber == 1;
            StartupCheckpoint.Set("TestMode_CampaignProgressRestored_Stage" + stageNumber);
        }

        private Rect GetStageTestButtonRect(int index)
        {
            float scale = Mathf.Clamp(Mathf.Min(Screen.width, Screen.height) / 720f, 0.55f, 1.15f);
            float contentWidth = Mathf.Min(Screen.width - 40f, 760f * scale);
            float gap = 12f * scale;
            float buttonWidth = (contentWidth - gap) * 0.5f;
            float buttonHeight = Mathf.Min(72f * scale, (Screen.height - 190f * scale) * 0.34f);
            float top = Screen.height * 0.35f;
            int column = index % 2;
            int row = index / 2;
            return new Rect((Screen.width - contentWidth) * 0.5f + column * (buttonWidth + gap),
                top + row * (buttonHeight + gap), buttonWidth, buttonHeight);
        }

        private void DrawStageTestSelect()
        {
            float scale = Mathf.Clamp(Mathf.Min(Screen.width, Screen.height) / 720f, 0.65f, 1.15f);
            GUI.Label(new Rect(0f, 42f * scale, Screen.width, 54f * scale),
                "DIRECT STAGE TEST", menuHeaderStyle);
            GUI.Label(new Rect(Screen.width * 0.08f, 96f * scale, Screen.width * 0.84f, 42f * scale),
                "Choose a stage without replaying earlier stages. Test runs do not award rewards or unlock campaign progress.",
                menuSmallStyle);

            for (int i = 0; i < 4; i++)
            {
                string[] labels = { "TEST STAGE 1  •  CITY", "TEST STAGE 2  •  VILLAGE",
                    "TEST STAGE 3  •  ALIEN PLANET", "TEST STAGE 4  •  MOTHERSHIP" };
                if (DrawFantasyButton(GetStageTestButtonRect(i), labels[i], false, true))
                {
                    BeginStageTestEntry(i + 1);
                    StartupCheckpoint.Set("DirectStageTestSelected_" + (i + 1));
                    return;
                }
            }

            if (DrawFantasyButton(GetLobbyBackRect(), "BACK TO MAIN MENU", false, true))
                mode = ScreenMode.MainMenu;
        }

        private void HandleStageTestSelectTouches()
        {
            if (!Application.isMobilePlatform || Input.touchCount <= 0)
                return;

            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.phase != TouchPhase.Began)
                    continue;

                Vector2 point = new Vector2(touch.position.x, Screen.height - touch.position.y);
                if (GetLobbyBackRect().Contains(point))
                {
                    suppressMenuButtonClicksFrame = Time.frameCount;
                    mode = ScreenMode.MainMenu;
                    StartupCheckpoint.Set("DirectStageTestMenuBack");
                    return;
                }

                for (int stage = 1; stage <= 4; stage++)
                {
                    if (!GetStageTestButtonRect(stage - 1).Contains(point))
                        continue;
                    suppressMenuButtonClicksFrame = Time.frameCount;
                    BeginStageTestEntry(stage);
                    StartupCheckpoint.Set("DirectStageTestSelectedByTouch_" + stage);
                    return;
                }
            }
        }

        private void BeginStageTestEntry(int targetStage)
        {
            CaptureCampaignProgressBeforeTest();
            StageThreeWorldBuilder.ClearExistingWorld();
            StageFourWorldBuilder.ClearExistingWorld();
            if (stageFourWorldBuilder != null)
            {
                Destroy(stageFourWorldBuilder);
                stageFourWorldBuilder = null;
            }
            StageFourEscapeSequence existingEscape = gameRoot != null
                ? gameRoot.GetComponent<StageFourEscapeSequence>() : null;
            if (existingEscape != null)
            {
                existingEscape.enabled = false;
                Destroy(existingEscape);
            }
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
                mode = ScreenMode.Loadout;
                loadoutWeaponTab = false;
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
                mode = ScreenMode.Loadout;
                loadoutWeaponTab = false;
                StartupCheckpoint.Set("DirectStageTestEntry_" + targetStage);
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
            GateGameplay(false);
            if (followCamera != null) followCamera.enabled = false;
            stageTwoVictorySequence = gameObject.AddComponent<StageTwoVictorySequence>();
            // The playerRoot field can reference a scene/container wrapper rather than
            // the actual actor. Boarding must receive the PlayerController transform.
            Transform localRoot = player != null ? player.transform
                : (playerRoot != null && playerRoot.GetComponent<PlayerController>() != null
                    ? playerRoot.transform
                    : null);
            if (localRoot == null)
            {
                PlayerController scenePlayer = FindFirstObjectByType<PlayerController>();
                if (scenePlayer != null)
                    localRoot = scenePlayer.transform;
            }
            Camera cinematicCamera = androidRuntimeCamera != null && androidRuntimeCamera.isActiveAndEnabled
                ? androidRuntimeCamera
                : (activeCamera != null && activeCamera.isActiveAndEnabled ? activeCamera : Camera.main);
            stageTwoVictorySequence.Begin(localRoot, cinematicCamera);
        }

        private void DrawStageFourIntroOverlay()
        {
            float scale = Mathf.Clamp(Mathf.Min(Screen.width, Screen.height) / 720f, 0.75f, 1.35f);
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
            if (stageFourWorldBuilder == null)
                return;

            float scale = Mathf.Clamp(Mathf.Min(Screen.width, Screen.height) / 720f, 0.70f, 1.2f);
            // Leave the HP/shield panel and its ammo row completely unobstructed.
            // The objective belongs immediately beneath those elements, not over them.
            Rect panel = new Rect(14f * scale, 154f * scale, 340f * scale, 32f * scale);
            Fill(panel, new Color(0.015f, 0.035f, 0.055f, 0.82f));
            Fill(new Rect(panel.x, panel.y, 3.5f * scale, panel.height),
                new Color(0.10f, 1f, 0.42f, 0.95f));

            int remaining = stageFourWorldBuilder.RemainingHostiles;
            int total = stageFourWorldBuilder.TotalHostiles;
            string objective = remaining > 0
                ? "HOSTILES  " + remaining + " / " + total
                : "MOTHERSHIP SECURED";
            GUI.Label(new Rect(panel.x + 10f * scale, panel.y + 2f * scale,
                panel.width - 16f * scale, panel.height - 4f * scale),
                objective, menuSmallStyle);
        }

        private void DrawStageThreeHUD()
        {
            float scale=Mathf.Clamp(Mathf.Min(Screen.width,Screen.height)/720f,.70f,1.2f);
            float width=Mathf.Min(Screen.width-28f*scale,430f*scale);
            Rect panel=new Rect(14f*scale,154f*scale,width,32f*scale);
            Fill(panel,new Color(.015f,.035f,.055f,.82f));
            Fill(new Rect(panel.x,panel.y,3.5f*scale,panel.height),new Color(.22f,.91f,1f,.98f));
            int remaining=stageThreeWorldBuilder!=null?stageThreeWorldBuilder.RemainingHostiles:66;
            int total=stageThreeWorldBuilder!=null?stageThreeWorldBuilder.TotalHostiles:66;
            string objective=remaining>0?"HOSTILES LEFT: "+remaining+" / "+total+"  •  ELIMINATE ALL ALIENS":"ALIENS CLEARED  •  REACH THE CENTRAL LIFT";
            GUI.Label(new Rect(panel.x+10f*scale,panel.y+2f*scale,panel.width-16f*scale,panel.height-4f*scale),objective,menuSmallStyle);
        }

        private void DrawStageThreeIntroOverlay()
        {
            float scale = Mathf.Clamp(Mathf.Min(Screen.width, Screen.height) / 720f, 0.75f, 1.35f);
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
            float scale = Mathf.Clamp(Mathf.Min(Screen.width, Screen.height) / 720f, 0.75f, 1.35f);
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
                float scale = Mathf.Clamp(Mathf.Min(Screen.width, Screen.height) / 720f, 0.7f, 1.25f);
                Rect captionRect = new Rect(0f, Screen.height * 0.13f, Screen.width, 56f * scale);
                Fill(new Rect(Screen.width * 0.12f, captionRect.y, Screen.width * 0.76f, captionRect.height),
                    new Color(0.015f, 0.035f, 0.055f, 0.78f));
                GUI.Label(captionRect, caption, menuHeaderStyle);
            }
        }

        private void CompleteStageTwoVictoryTransition()
        {
            if (stageTwoVictorySequence == null) return;

            // Keep the real evacuation ship alive for the campaign transition only.
            // Stage-test runs must always clean up and must not leave a stale ship.
            string result = GetStageTwoResultMessage();
            if (!stageTestRun)
                stageTwoVictorySequence.PreserveShipForStageThree();
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
            startingMatch = false;
            matchPaused = false;
            spawnChosen = false;
            resultOverlayStartTime = -1f;

            if (stageTestRun)
            {
                RestoreCampaignProgressAfterTest();
                mode = ScreenMode.MainMenu;
                ShowLobbyNotice("STAGE 2 TEST COMPLETE  •  CAMPAIGN PROGRESS PRESERVED");
                StartupCheckpoint.Set("StageTwoTestComplete_CampaignUnchanged_" + result);
            }
            else
            {
                lastStageReward = LoadoutStore.MarkStageCompleted(2, false);
                stageNumber = 3;
                PlayerPrefs.SetInt("PW.Stage3Unlocked", 1);
                PlayerPrefs.Save();
                selectedHero = LoadoutStore.SelectedHero;
                selectedStoreWeaponKind = (int)LoadoutStore.SelectedWeapon;
                loadoutWeaponTab = false;
                spawnWorld = new Vector2(0f, -97f);
                spawnChosen = true;
                mode = ScreenMode.Loadout;
                startupStatus = "Stage 3: transport ship approaching the alien planet...";
                StartupCheckpoint.Set("StageTwoCutsceneComplete_AutoStartStage3Transport_" + result);
            }
        }

        private void ReturnToMainMenuFromResult()
        {
            StageTwoArenaManager.Instance?.ResetForStageChange();
            Time.timeScale = 1f;
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
            startingMatch = false;
            matchPaused = false;
            resultOverlayStartTime = -1f;
            spawnChosen = false;
            lastStageReward = 0;
            PlayerPrefs.SetInt("PW.LastStageReward", 0);
            PlayerPrefs.SetInt("PW.LastStageTokenReward", 0);
            if (stageTestRun)
            {
                RestoreCampaignProgressAfterTest();
            }
            else
            {
                stageNumber = LoadoutStore.GetNextCampaignStage();
            }
            mode = ScreenMode.MainMenu;
            HeroPortraitStudio.Close();
            WeaponPreviewStudio.Close();
            PlayerPrefs.Save();
            StartupCheckpoint.Set("ResultScreen_ReturnToMainMenu_WithoutStageAdvance");
        }

        private void CompleteStageThreeTransition()
        {
            StageTwoArenaManager.Instance?.ResetForStageChange();
            Time.timeScale = 1f;
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
            startingMatch = false;
            matchPaused = false;
            resultOverlayStartTime = -1f;
            if (stageTestRun)
            {
                RestoreCampaignProgressAfterTest();
                spawnChosen = false;
                mode = ScreenMode.MainMenu;
                ShowLobbyNotice("STAGE 3 TEST COMPLETE  •  CAMPAIGN PROGRESS PRESERVED");
                StartupCheckpoint.Set("StageThreeTestComplete_ReturnToMenu");
                return;
            }
            lastStageReward = LoadoutStore.MarkStageCompleted(3, false);
            stageNumber = 4;
            PlayerPrefs.SetInt("PW.Stage4Unlocked", 1);
            PlayerPrefs.Save();
            selectedHero = LoadoutStore.SelectedHero;
            selectedStoreWeaponKind = (int)LoadoutStore.SelectedWeapon;
            loadoutWeaponTab = false;
            spawnWorld = Vector2.zero;
            spawnChosen = true;
            mode = ScreenMode.Loadout;
            startupStatus = "Stage 4: green uplink to the orbital mothership...";
            StartupCheckpoint.Set("StageThreeComplete_AutoStartStageFour");
        }

        /// <summary>
        /// Called only after the final Stage 4 exterior detonation. The cinematic then
        /// returns to the existing front-end menu rather than leaving the player trapped
        /// in a finished match. Stage test mode keeps the campaign progress untouched.
        /// </summary>
        public void CompleteStageFourEscapeToMainMenu()
        {
            Time.timeScale = 1f;
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
                mode = ScreenMode.MainMenu;
                ShowLobbyNotice("STAGE 4 TEST COMPLETE  •  CAMPAIGN PROGRESS PRESERVED");
                StartupCheckpoint.Set("StageFourTestComplete_ReturnToMainMenu");
                return;
            }

            lastStageReward = LoadoutStore.MarkStageCompleted(4, false);
            stageNumber = 5;
            PlayerPrefs.SetInt("PW.Stage4Unlocked", 1);
            PlayerPrefs.SetInt("PW.Stage4Completed", 1);
            PlayerPrefs.Save();
            mode = ScreenMode.MainMenu;
            ShowLobbyNotice("STAGE 4 COMPLETE  •  +" + lastStageReward + " COINS  •  MOTHERSHIP DESTROYED");
            StartupCheckpoint.Set("StageFourCampaignComplete_ReturnToMainMenu");
        }

        private void DrawProfileScreen()
        {
            float width = Mathf.Min(Screen.width - 64f, 760f);
            Rect panel = new Rect((Screen.width - width) * 0.5f, Screen.height * 0.19f, width, Screen.height * 0.58f);
            Fill(panel, new Color(0.025f, 0.055f, 0.085f, 0.94f));
            Fill(new Rect(panel.x, panel.y, panel.width, 6f), new Color(0.95f, 0.69f, 0.20f, 1f));
            GUI.Label(new Rect(panel.x + 20f, panel.y + 18f, panel.width - 40f, 52f), "PLAYER PROFILE", menuHeaderStyle);
            GUI.Label(new Rect(panel.x + 32f, panel.y + 88f, panel.width - 64f, 36f), "DISPLAY NAME (MAX 20 CHARACTERS)", menuSmallStyle);
            profileNameInput = GUI.TextField(new Rect(panel.x + 40f, panel.y + 132f, panel.width - 80f, 58f),
                profileNameInput, 20);
            GUI.Label(new Rect(panel.x + 32f, panel.y + 208f, panel.width - 64f, 34f), "PLAYER ID  " + playerId, menuSmallStyle);
            GUI.Label(new Rect(panel.x + 32f, panel.y + 250f, panel.width - 64f, 34f),
                "PROFILE COSMETIC  " + StoreCosmeticNames[Mathf.Clamp(selectedProfileCosmetic, 0, StoreCosmeticNames.Length - 1)],
                menuSmallStyle);
            if (DrawFantasyButton(GetLobbyBackRect(), "SAVE & BACK"))
            {
                string cleanName = string.IsNullOrWhiteSpace(profileNameInput) ? "Persian Warrior" : profileNameInput.Trim();
                profileNameInput = cleanName.Length > 20 ? cleanName.Substring(0, 20) : cleanName;
                PlayerPrefs.SetString("PW.PlayerName", profileNameInput);
                PlayerPrefs.Save();
                mode = ScreenMode.MainMenu;
            }
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
            float width = card.width - 20f;
            if (index >= 3)
                width -= Mathf.Min(86f, card.width * 0.34f);
            return new Rect(card.x + 10f, card.yMax - 44f, Mathf.Max(42f, width), 34f);
        }

        private Rect GetStoreSellRect(int index)
        {
            Rect action = GetStoreActionRect(index);
            Rect card = GetStoreItemRect(index);
            float sellWidth = Mathf.Min(80f, card.width * 0.30f);
            float gap = Mathf.Min(6f, card.width * 0.025f);
            return new Rect(action.xMax + gap, action.y, sellWidth, action.height);
        }

        private bool IsStoreItemOwned(int index)
        {
            if (index < 3) return PlayerPrefs.GetInt("PW.CosmeticOwned." + index, index == 0 ? 1 : 0) != 0;
            return LoadoutStore.IsWeaponOwned(index == 3 ? WeaponController.WeaponKind.VoidRifle : WeaponController.WeaponKind.AlienSword);
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
                WeaponController.WeaponKind kind = index == 3 ? WeaponController.WeaponKind.VoidRifle : WeaponController.WeaponKind.AlienSword;
                WeaponCatalog.Entry entry = WeaponCatalog.Get(kind);
                if (!LoadoutStore.TryBuyWeapon(kind))
                {
                    loadoutNotice = !LoadoutStore.IsStageUnlocked(entry.RequiredCompletedStage)
                        ? "CLEAR STAGE " + entry.RequiredCompletedStage + " FIRST"
                        : (entry.TokenPrice > 0 ? "NOT ENOUGH TOKENS" : "NOT ENOUGH COINS");
                    return;
                }
                selectedStoreWeaponKind = (int)kind;
                LoadoutStore.SelectedWeapon = kind;
                loadoutNotice = "EQUIPPED: " + entry.Name;
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

        private void SellStoreWeapon(int index)
        {
            WeaponController.WeaponKind kind = index == 3
                ? WeaponController.WeaponKind.VoidRifle
                : WeaponController.WeaponKind.AlienSword;
            int refund;
            bool refundInTokens;
            if (!LoadoutStore.TrySellWeapon(kind, out refund, out refundInTokens))
            {
                loadoutNotice = "WEAPON NOT OWNED OR NOT AVAILABLE FOR SALE";
                return;
            }

            if (selectedStoreWeaponKind == (int)kind)
                selectedStoreWeaponKind = (int)LoadoutStore.SelectedWeapon;
            storeCoins = LoadoutStore.Coins;
            WeaponCatalog.Entry entry = WeaponCatalog.Get(kind);
            loadoutNotice = "SOLD " + entry.Name + "  •  +" + refund +
                (refundInTokens ? " TOKENS" : " COINS");
        }

        private void DrawStoreScreen()
        {
            storeCoins = LoadoutStore.Coins;
            GUI.Label(new Rect(0f, 118f, Screen.width, 54f), "PERSIA STORE & WEAPON ARMORY", menuHeaderStyle);
            GUI.Label(new Rect(0f, 166f, Screen.width, 36f), "BUY, EQUIP OR SELL WEAPONS  •  OFFLINE DEMO STORE", menuSmallStyle);
            GUI.Label(new Rect(Screen.width - 430f, 24f, 405f, 42f),
                "COINS  " + storeCoins + "     TOKENS  ✦ " + LoadoutStore.Tokens, menuBodyStyle);

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
                Fill(emblem, emblemColor);
                GUI.Label(new Rect(card.x + 6f, card.y + 76f, card.width - 12f, 34f), StoreCosmeticNames[i], menuSmallStyle);

                string priceLabel;
                WeaponCatalog.Entry weaponEntry = new WeaponCatalog.Entry();
                if (i >= 3)
                {
                    weaponEntry = WeaponCatalog.Get(i == 3
                        ? WeaponController.WeaponKind.VoidRifle
                        : WeaponController.WeaponKind.AlienSword);
                    int refund = weaponEntry.TokenPrice > 0
                        ? Mathf.Max(1, weaponEntry.TokenPrice / 2)
                        : Mathf.Max(1, weaponEntry.Price / 2);
                    if (owned)
                        priceLabel = (equipped ? "EQUIPPED" : "OWNED") + "  •  SELL " + refund +
                            (weaponEntry.TokenPrice > 0 ? " TOKENS" : " COINS");
                    else if (!LoadoutStore.IsStageUnlocked(weaponEntry.RequiredCompletedStage))
                        priceLabel = "CLEAR STAGE " + weaponEntry.RequiredCompletedStage;
                    else
                        priceLabel = weaponEntry.TokenPrice > 0
                            ? weaponEntry.TokenPrice + " TOKENS"
                            : weaponEntry.Price + " COINS";
                }
                else
                {
                    priceLabel = owned ? (equipped ? "EQUIPPED" : "OWNED")
                        : StoreCosmeticPrices[i] + " COINS";
                }

                GUI.Label(new Rect(card.x + 6f, card.y + 110f, card.width - 12f, 26f), priceLabel, menuBodyStyle);
                Rect action = GetStoreActionRect(i);
                if (i >= 3)
                {
                    WeaponCatalog.Entry entry = weaponEntry;
                    bool purchaseEnabled = owned || LoadoutStore.IsStageUnlocked(entry.RequiredCompletedStage);
                    if (purchaseEnabled)
                    {
                        if (DrawFantasyButton(action, owned ? (equipped ? "EQUIPPED" : "EQUIP WEAPON") : "UNLOCK", equipped, true))
                            PurchaseOrEquipCosmetic(i);
                    }
                    else
                    {
                        DrawFantasyButton(action, "CLEAR STAGE " + weaponEntry.RequiredCompletedStage, false, false);
                    }

                    bool sellEnabled = owned && !LoadoutTestSwitch.UnlockEverything;
                    if (sellEnabled)
                    {
                        if (DrawFantasyButton(GetStoreSellRect(i), "SELL", false, true))
                            SellStoreWeapon(i);
                    }
                    else
                    {
                        DrawFantasyButton(GetStoreSellRect(i), owned ? "TEST MODE" : "NOT OWNED", false, false);
                    }
                }
                else if (DrawFantasyButton(action, owned ? (equipped ? "EQUIPPED" : "EQUIP") : "UNLOCK"))
                {
                    PurchaseOrEquipCosmetic(i);
                }
            }

            if (!string.IsNullOrEmpty(loadoutNotice))
                GUI.Label(new Rect(0f, Screen.height - 124f, Screen.width, 28f), loadoutNotice, menuSmallStyle);
            if (DrawFantasyButton(GetLobbyBackRect(), "BACK TO MENU")) mode = ScreenMode.MainMenu;
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
                float cardHeight = Mathf.Min(310f, Screen.height - 360f);

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
                        EnterDropMap();
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


        private Rect LoadoutCard(int i)
        {
            float sc=Mathf.Clamp(Mathf.Min(Screen.width,Screen.height)/720f,.58f,1.15f);
            float gap=12f*sc;
            float total=Mathf.Min(Screen.width-28f*sc,1260f*sc);
            float w=(total-gap*2f)/3f;
            float top=Mathf.Max(210f*sc,Screen.height*.22f);
            float rowGap=12f*sc;
            float bottomReserve=130f*sc;
            float h=Mathf.Clamp((Screen.height-top-bottomReserve-rowGap)/2f,175f,280f);
            return new Rect((Screen.width-total)*.5f+(i%3)*(w+gap)+loadoutSlideOffset,
                top+(i/3)*(h+rowGap),w,h);
        }
        private Rect LoadoutNext()
        {
            float sc=Mathf.Clamp(Mathf.Min(Screen.width,Screen.height)/720f,.58f,1.15f);
            float width=Mathf.Min(Screen.width-40f*sc,460f*sc);
            return new Rect((Screen.width-width)*.5f,Screen.height-84f*sc,width,62f*sc);
        }
        private void DrawLoadoutScreen()
        {
            float sc=Mathf.Clamp(Mathf.Min(Screen.width,Screen.height)/720f,.58f,1.15f);
            Rect panel=new Rect(12f*sc,112f*sc,Screen.width-24f*sc,Screen.height-194f*sc);
            MenuTheme.DrawPanel(panel,pixel);
            GUI.Label(new Rect(panel.x+12f,panel.y+6f*sc,panel.width-24f,38f*sc),"STAGE "+stageNumber+"  •  "+(loadoutWeaponTab?"CHOOSE WEAPON":"CHOOSE HERO"),menuHeaderStyle);
            GUI.Label(new Rect(panel.x+12f,panel.y+39f*sc,panel.width-24f,24f*sc),"COINS  "+LoadoutStore.Coins+"     TOKENS  ✦ "+LoadoutStore.Tokens,menuBodyStyle);
            if(LoadoutTestSwitch.UnlockEverything){Rect b=new Rect(panel.xMax-116f*sc,panel.y+8f*sc,100f*sc,24f*sc);Fill(b,new Color(.58f,.12f,.08f,1));GUI.Label(b,"TEST MODE",menuSmallStyle);}
            if(lastStageReward>0)GUI.Label(new Rect(panel.x+12f,panel.y+63f*sc,panel.width-24f,20f*sc),"STAGE CLEAR  +"+lastStageReward+" COINS  •  +"+LoadoutStore.LastStageTokenReward+" TOKENS",menuSmallStyle);
            for(int i=0;i<6;i++)
            {
                Rect card=LoadoutCard(i);
                bool chosen=loadoutWeaponTab?(selectedStoreWeaponKind==(int)WeaponCatalog.GetAt(i).Kind):(selectedHero==i);
                Fill(card,new Color(.76f,.53f,.16f,chosen?1f:.78f));
                Fill(new Rect(card.x+3f,card.y+3f,card.width-6f,card.height-6f),new Color(.025f,.055f,.085f,.97f));
                if(!loadoutWeaponTab)
                {
                    HeroPortraitStudio.Draw(new Rect(card.x+card.width*.12f,card.y+3f*sc,card.width*.76f,card.height*.69f),i,chosen);
                    bool ok=LoadoutStore.CanUseHero(i,gameBootstrap!=null&&gameBootstrap.VillageGirlRescued);
                    GUI.Label(new Rect(card.x+3f,card.yMax-50f*sc,card.width-6f,26f*sc),HeroCatalog.Name(i),menuSmallStyle);
                    string heroState=ok?(chosen?"SELECTED":"TAP TO SELECT"):(i==1?"RESCUE IN STAGE 1":(HeroCatalog.TokenPrice(i)>0?HeroCatalog.TokenPrice(i)+" TOKENS • BUY":HeroCatalog.Price(i)+" COINS • BUY"));
                    GUI.Label(new Rect(card.x+3f,card.yMax-25f*sc,card.width-6f,24f*sc),heroState,menuSmallStyle);
                }
                else
                {
                    WeaponCatalog.Entry e=WeaponCatalog.GetAt(i);
                    Rect previewRect = new Rect(card.x + card.width * .16f, card.y + 8f * sc, card.width * .68f, card.height * .40f);
                    if (!WeaponPreviewStudio.Draw(previewRect, e.Kind, chosen))
                    {
                        Fill(new Rect(card.x+card.width*.18f,card.y+card.height*.24f,card.width*.64f,card.height*.18f),new Color(.48f,.59f,.67f,1));
                        Fill(new Rect(card.x+card.width*.42f,card.y+card.height*.1f,card.width*.16f,card.height*.46f),e.Kind==WeaponController.WeaponKind.VoidRifle?Color.cyan:(e.Kind==WeaponController.WeaponKind.AlienSword?Color.green:new Color(.8f,.62f,.25f)));
                    }
                    float rangeStat = WeaponController.GetEffectiveRangeForKind(e.Kind);
                    float damageStat = WeaponController.GetDamageForKind(e.Kind);
                    float fireStat = 1f / WeaponController.GetFireCooldownForKind(e.Kind);
                    float weightStat = WeaponController.GetWeightForKind(e.Kind);
                    GUI.Label(new Rect(card.x + 4f, card.y + card.height * .43f, card.width - 8f, 25f * sc),
                        "D" + damageStat + " R" + rangeStat.ToString("0") + " F" + fireStat.ToString("0.0") + " W" + weightStat.ToString("0.0"), menuSmallStyle);
                    Fill(new Rect(card.x + card.width * .12f, card.y + card.height * .59f, card.width * .76f, 5f * sc), new Color(.18f,.24f,.30f,1f));
                    Fill(new Rect(card.x + card.width * .12f, card.y + card.height * .68f, card.width * .76f * Mathf.Clamp01(weightStat / 1.6f), 5f * sc), new Color(.94f,.68f,.23f,1f));
                    bool ok=LoadoutStore.CanUseWeapon(e.Kind);
                    GUI.Label(new Rect(card.x+3f,card.yMax-50f*sc,card.width-6f,26f*sc),e.Name,menuSmallStyle);
                    string state=ok?(chosen?"SELECTED":"TAP TO SELECT"):(!LoadoutStore.IsStageUnlocked(e.RequiredCompletedStage)?"CLEAR STAGE "+e.RequiredCompletedStage:(e.TokenPrice>0?e.TokenPrice+" TOKENS • TAP TO BUY":e.Price+" COINS • TAP TO BUY"));
                    GUI.Label(new Rect(card.x+3f,card.yMax-25f*sc,card.width-6f,24f*sc),state,menuSmallStyle);
                }
                if(GUI.Button(card,GUIContent.none,GUIStyle.none))ChooseLoadoutItem(i);
            }
            GUI.Label(new Rect(panel.x+8f,panel.yMax-42f*sc,panel.width-16f,20f*sc),loadoutNotice,menuSmallStyle);
            if(loadoutWeaponTab)
            {
                if(DrawFantasyButton(LoadoutWeaponActionRect(0),"BACK TO HERO",false))SwitchLoadoutTab(false);
                WeaponController.WeaponKind kind=(WeaponController.WeaponKind)selectedStoreWeaponKind;
                string sellLabel=kind==WeaponController.WeaponKind.AssaultRifle?"BASE WEAPON":"SELL SELECTED";
                if(DrawFantasyButton(LoadoutWeaponActionRect(1),sellLabel,false)&&kind!=WeaponController.WeaponKind.AssaultRifle)SellSelectedWeapon();
                if(DrawFantasyButton(LoadoutWeaponActionRect(2),"START STAGE "+stageNumber,true))StartStageFromLoadout();
            }
            else
            {
                Rect backMenu = new Rect(12f*sc, Screen.height-84f*sc, 200f*sc, 60f*sc);
                if (DrawFantasyButton(backMenu, "BACK TO MENU", false))
                {
                    HeroPortraitStudio.Close();
                    WeaponPreviewStudio.Close();
                    mode = ScreenMode.MainMenu;
                }
                if (DrawFantasyButton(LoadoutNext(), "CONTINUE TO WEAPONS", true)) SwitchLoadoutTab(true);
            }
        }
        private Rect LoadoutWeaponActionRect(int index)
        {
            float sc=Mathf.Clamp(Mathf.Min(Screen.width,Screen.height)/720f,.58f,1.15f);
            float bw=160f*sc,sw=190f*sc,tw=240f*sc,gap=10f*sc;
            float total=bw+sw+tw+gap*2f,x=(Screen.width-total)*.5f,y=Screen.height-84f*sc,h=60f*sc;
            if(index==0)return new Rect(x,y,bw,h);
            x+=bw+gap;if(index==1)return new Rect(x,y,sw,h);
            x+=sw+gap;return new Rect(x,y,tw,h);
        }

        private void SellSelectedWeapon()
        {
            WeaponController.WeaponKind kind=(WeaponController.WeaponKind)selectedStoreWeaponKind;
            int refund;bool tokens;
            if(!LoadoutStore.TrySellWeapon(kind,out refund,out tokens)){loadoutNotice="THE BASE ASSAULT RIFLE CANNOT BE SOLD";return;}
            selectedStoreWeaponKind=(int)LoadoutStore.SelectedWeapon;
            loadoutNotice="SOLD WEAPON  •  +"+refund+(tokens?" TOKENS":" COINS");
        }

        private void SwitchLoadoutTab(bool weaponTab)
        {
            if (loadoutWeaponTab == weaponTab) return;
            loadoutWeaponTab = weaponTab;
            loadoutSlideOffset = weaponTab ? Screen.width * 0.12f : -Screen.width * 0.12f;
            loadoutSlideVelocity = 0f;
        }

        private void ChooseLoadoutItem(int i)
        {
            if(!loadoutWeaponTab)
            {
                bool rescued=gameBootstrap!=null&&gameBootstrap.VillageGirlRescued;
                if(LoadoutStore.CanUseHero(i,rescued)||LoadoutStore.TryBuyHero(i,rescued)){selectedHero=i;LoadoutStore.SelectedHero=i;loadoutNotice=string.Empty;}
                else loadoutNotice=i==1&&!rescued?"RESCUE ANAHITA IN STAGE 1":"NOT ENOUGH COINS OR HERO LOCKED";
            }
            else
            {
                WeaponCatalog.Entry e=WeaponCatalog.GetAt(i);
                if(LoadoutStore.CanUseWeapon(e.Kind)||LoadoutStore.TryBuyWeapon(e.Kind)){selectedStoreWeaponKind=(int)e.Kind;LoadoutStore.SelectedWeapon=e.Kind;loadoutNotice=string.Empty;}
                else loadoutNotice="WEAPON LOCKED OR NOT ENOUGH COINS";
            }
        }
        private void StartStageFromLoadout()
        {
            WeaponController.WeaponKind chosen=(WeaponController.WeaponKind)selectedStoreWeaponKind;
            if(!LoadoutStore.CanUseHero(selectedHero,gameBootstrap!=null&&gameBootstrap.VillageGirlRescued)||!LoadoutStore.CanUseWeapon(chosen)){loadoutNotice="SELECT AN AVAILABLE HERO AND WEAPON";return;}
            LoadoutStore.SelectedHero=selectedHero;LoadoutStore.SelectedWeapon=chosen;
            HeroPortraitStudio.Close();
            WeaponPreviewStudio.Close();
            if(stageNumber<=2){EnterDropMap();return;}
            spawnWorld=stageNumber==3?new Vector2(0f,-97f):Vector2.zero;spawnChosen=true;mode=ScreenMode.DropMap;StartMatch();
        }
        private void HandleLoadoutTouches()
        {
            if(!Application.isMobilePlatform||Input.touchCount<=0)return;
            for(int t=0;t<Input.touchCount;t++)
            {
                Touch touch=Input.GetTouch(t);if(touch.phase!=TouchPhase.Began)continue;
                Vector2 point=new Vector2(touch.position.x,Screen.height-touch.position.y);
                if(loadoutWeaponTab)
                {
                    for(int button=0;button<3;button++)
                    {
                        if(!LoadoutWeaponActionRect(button).Contains(point))continue;
                        suppressMenuButtonClicksFrame=Time.frameCount;
                        if(button==0)SwitchLoadoutTab(false);
                        else if(button==1)
                        {
                            if((WeaponController.WeaponKind)selectedStoreWeaponKind!=WeaponController.WeaponKind.AssaultRifle)SellSelectedWeapon();
                            else loadoutNotice="THE BASE ASSAULT RIFLE CANNOT BE SOLD";
                        }
                        else StartStageFromLoadout();
                        return;
                    }
                }
                else
                {
                    float sc=Mathf.Clamp(Mathf.Min(Screen.width,Screen.height)/720f,.58f,1.15f);
                    if(new Rect(12f*sc,Screen.height-84f*sc,200f*sc,60f*sc).Contains(point))
                    {
                        suppressMenuButtonClicksFrame=Time.frameCount;
                        HeroPortraitStudio.Close();WeaponPreviewStudio.Close();mode=ScreenMode.MainMenu;return;
                    }
                    if(LoadoutNext().Contains(point)){suppressMenuButtonClicksFrame=Time.frameCount;SwitchLoadoutTab(true);return;}
                }
                for(int i=0;i<6;i++)
                    if(LoadoutCard(i).Contains(point)){suppressMenuButtonClicksFrame=Time.frameCount;ChooseLoadoutItem(i);return;}
            }
        }

        private void DrawHeroSelect()
        {
            GUI.Label(new Rect(0f, 121f, Screen.width, 58f), stageNumber >= 2 ? "STAGE 2  •  CHOOSE YOUR HERO" : "CHOOSE YOUR HERO", menuHeaderStyle);
            string heroHint = stageNumber >= 2
                ? (gameBootstrap != null && gameBootstrap.VillageGirlRescued
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
                ? (selectedHero == stageTwoOriginalHero
                    ? "KEEP CURRENT HERO  •  CONTINUE"
                    : "USE SELECTED HERO  •  CONTINUE")
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
                    EnterDropMap();
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
            float scale = Mathf.Clamp(Mathf.Min(Screen.width, Screen.height) / 720f, 0.75f, 1.25f);
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

        private bool IsHeroUnlocked(int index)
        {
            return LoadoutStore.CanUseHero(index, gameBootstrap != null && gameBootstrap.VillageGirlRescued);
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

            float scale = Mathf.Clamp(Mathf.Min(Screen.width, Screen.height) / 720f, 0.75f, 1.25f);
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

        private void BeginStageTwoSelection(bool awardStageOne = false)
        {
            ExtractionBeacon.ClearForStageTransition();
            Time.timeScale = 1f;
            if (awardStageOne && !stageTestRun)
            {
                lastStageReward = LoadoutStore.MarkStageCompleted(1, false);
            }
            else
            {
                lastStageReward = 0;
                PlayerPrefs.SetInt("PW.LastStageReward", 0);
                PlayerPrefs.SetInt("PW.LastStageTokenReward", 0);
                PlayerPrefs.Save();
            }
            stageNumber = 2;
            stageTwoOriginalHero = selectedHero;
            stageTwoShopOpen = false;
            selectedHero = LoadoutStore.SelectedHero;
            selectedStoreWeaponKind = (int)LoadoutStore.SelectedWeapon;
            stageTwoLobbyRunning = false;
            stageTwoLobbyReady = false;
            stageTwoLobbyPlayers = 1;
            mode = ScreenMode.Loadout;
            loadoutWeaponTab = false;
            spawnChosen = false;
            startingMatch = false;
            matchPaused = false;
            resultOverlayStartTime = -1f;
            stageTwoLandingMode = 1; // Stage 2 always uses the single fixed village map.
            stageTwoLayoutVariant = 0;
            if (activeCamera != null) activeCamera.enabled = false;
            if (androidRuntimeCamera != null) androidRuntimeCamera.enabled = false;
            GateGameplay(false);
            StartupCheckpoint.Set("StageTwoHeroSelectionOpened");
            Debug.Log("PERSIA_STAGE2: extraction transition complete; opening hero selection");
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
            GUI.Label(new Rect(0f, 76f, Screen.width, 48f),
                "STAGE 2  •  OLD IRANIAN VILLAGE", menuHeaderStyle);
            GUI.Label(new Rect(24f, 120f, Screen.width - 48f, 30f),
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
            float scale = Mathf.Clamp(Mathf.Min(Screen.width, Screen.height) / 720f, 0.75f, 1.2f);
            Rect shade = new Rect(0f, 0f, Screen.width, Screen.height);
            Fill(shade, new Color(0.015f, 0.025f, 0.045f, 0.72f));

            float width = Mathf.Min(Screen.width - 44f, 660f);
            float height = Mathf.Min(Screen.height - 40f, 430f);
            Rect panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            Fill(new Rect(panel.x + 5f, panel.y + 8f, panel.width, panel.height), new Color(0f, 0f, 0f, 0.40f));
            Fill(panel, new Color(0.035f, 0.075f, 0.12f, 0.91f));
            Fill(new Rect(panel.x, panel.y, panel.width, 5f), new Color(0.25f, 0.92f, 0.45f, 0.95f));
            GUI.Label(new Rect(panel.x + 18f, panel.y + 18f, panel.width - 36f, 54f), "STAGE 2  •  ARENA LOBBY", menuHeaderStyle);
            GUI.Label(new Rect(panel.x + 18f, panel.y + 74f, panel.width - 36f, 42f),
                "COMPETITORS  " + stageTwoLobbyPlayers + " / 10", menuBodyStyle);

            Rect track = new Rect(panel.x + 42f, panel.y + 119f, panel.width - 84f, 16f);
            Fill(track, new Color(0.10f, 0.16f, 0.20f, 0.95f));
            Fill(new Rect(track.x, track.y, track.width * (stageTwoLobbyPlayers / 10f), track.height),
                new Color(0.20f, 0.92f, 0.42f, 0.95f));

            float elapsed = Time.unscaledTime - stageTwoLobbyStartedAt;
            string status = elapsed < 7f
                ? "WAITING FOR PLAYERS  •  " + Mathf.CeilToInt(7f - elapsed) + "s"
                : "FILLING REMAINING SLOTS WITH LOCAL AI  •  +1 / SECOND";
            GUI.Label(new Rect(panel.x + 18f, panel.y + 143f, panel.width - 36f, 36f), status, menuSmallStyle);

            float slotGap = 6f;
            float slotWidth = (panel.width - 48f - slotGap * 4f) / 5f;
            for (int slot = 0; slot < 10; slot++)
            {
                int column = slot % 5;
                int row = slot / 5;
                Rect slotRect = new Rect(panel.x + 24f + column * (slotWidth + slotGap),
                    panel.y + 183f + row * 42f, slotWidth, 34f);
                bool present = slot < stageTwoLobbyPlayers;
                Fill(slotRect, present ? new Color(0.08f, 0.32f, 0.22f, 0.86f) : new Color(0.08f, 0.12f, 0.16f, 0.72f));
                Fill(new Rect(slotRect.x, slotRect.y, 3f, slotRect.height),
                    present ? new Color(0.25f, 0.92f, 0.45f, 0.95f) : new Color(0.38f, 0.45f, 0.50f, 0.55f));
                string slotName = present ? (slot == 0 ? "YOU" : "AI-" + slot.ToString("00")) : "WAITING";
                GUI.Label(new Rect(slotRect.x + 4f, slotRect.y, slotRect.width - 8f, slotRect.height),
                    slotName, menuSmallStyle);
            }

            GUI.Label(new Rect(panel.x + 18f, panel.y + 274f, panel.width - 36f, 38f),
                "100 ALIENS  •  5 WAVES  •  NEXT WAVE AT 6 REMAINING", menuSmallStyle);
            GUI.Label(new Rect(panel.x + 18f, panel.y + panel.height - 42f, panel.width - 36f, 28f),
                "OFFLINE TEST: AI FILLS THE LOBBY; ONLINE SERVER NOT CONNECTED", menuSmallStyle);
        }

        private void DrawAndroidPauseControl()
        {
            EnsureUiInitialized();

            float scale = Mathf.Clamp(Mathf.Min(Screen.width, Screen.height) / 720f, 0.75f, 1.35f);
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

                GUI.Label(
                    new Rect(panel.x + 20f, panel.y + 70f, panel.width - 40f, 28f),
                    "Touch ▶ to resume",
                    bodyStyle);
            }

            // Draw the control last so the pause overlay can never cover it.
            Rect button = new Rect(
                Screen.width * 0.5f - size * 0.5f,
                14f * scale,
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
                matchPaused = !matchPaused;
                Time.timeScale = matchPaused ? 0f : 1f;

                if (mobileInput != null)
                    mobileInput.SetPaused(matchPaused);
            }
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

            StageThreeWorldBuilder.ClearExistingWorld();
            StageFourWorldBuilder.ClearExistingWorld();
            if (stageNumber == 3)
            {
                stageThreeWorldBuilder = gameRoot.GetComponent<StageThreeWorldBuilder>();
                if (stageThreeWorldBuilder == null)
                    stageThreeWorldBuilder = gameRoot.AddComponent<StageThreeWorldBuilder>();
                if (GameSession.Instance != null)
                    GameSession.Instance.ResetMatch();
                startupStatus = "Stage 3: building the alien fortress and nine routes...";
                if (!stageThreeWorldBuilder.BuildWorld())
                {
                    startupStatus = "Stage 3 world construction failed.";
                    StartupCheckpoint.Set("StageThreeWorldBuildFailed");
                    startingMatch = false;
                    mode = ScreenMode.MainMenu;
                    yield break;
                }
            }
            else if (stageNumber == 4)
            {
                stageFourWorldBuilder = gameRoot.GetComponent<StageFourWorldBuilder>();
                if (stageFourWorldBuilder == null)
                    stageFourWorldBuilder = gameRoot.AddComponent<StageFourWorldBuilder>();
                if (GameSession.Instance != null)
                    GameSession.Instance.ResetMatch();
                startupStatus = "Stage 4: starting orbital megabase construction...";
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
                    startingMatch = false;
                    mode = ScreenMode.MainMenu;
                    string stageFourFailureCode = stageFourWorldBuilder != null &&
                        !string.IsNullOrEmpty(stageFourWorldBuilder.BuildFailureCode)
                        ? stageFourWorldBuilder.BuildFailureCode
                        : "UNKNOWN";
                    ShowLobbyNotice("STAGE 4 FAILED: " + stageFourFailureCode);
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
                    startingMatch = false;
                    yield break;
                }

                if (stageNumber == 2)
                {
                    startupStatus = "Stage 2: preparing Iranian village stage...";
                    gameBootstrap.PrepareStageTwoVillage(stageTwoLayoutVariant);
                }
                else if (stageNumber == 1 && forceStageOneWorldRebuildForTest)
                {
                    startupStatus = "Stage 1: rebuilding a clean test world...";
                    forceStageOneWorldRebuildForTest = false;
                    gameBootstrap.PrepareStageOneWorldForTest();
                }
                else if (stageNumber == 1 && !gameBootstrap.IsWorldReady &&
                    !gameBootstrap.IsWorldPreparing && !gameBootstrap.WorldBuildFailed)
                {
                    startupStatus = "Stage 1: requesting battlefield build...";
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

            // Stage one keeps its original three firearms. Stage two additionally
            // enables the axe, Void Rifle and Alien Sword when the local profile owns them.
            player.Weapon?.SetAxeEnabledForMatch(stageNumber >= 2);
            if (player.Weapon != null)
            {
                WeaponController.WeaponKind selectedKind = (WeaponController.WeaponKind)
                    PlayerPrefs.GetInt("PW.SelectedWeaponKind", (int)WeaponController.WeaponKind.AssaultRifle);
                System.Collections.Generic.List<WeaponController.WeaponKind> ownedKinds =
                    new System.Collections.Generic.List<WeaponController.WeaponKind>();
                for (int i = 0; i < WeaponCatalog.Entries.Length; i++)
                {
                    WeaponController.WeaponKind kind = WeaponCatalog.Entries[i].Kind;
                    if (LoadoutStore.CanUseWeapon(kind)) ownedKinds.Add(kind);
                }
                if (System.Array.IndexOf(ownedKinds.ToArray(), selectedKind) < 0)
                    selectedKind = WeaponController.WeaponKind.AssaultRifle;
                if (stageNumber == 4 && selectedKind == WeaponController.WeaponKind.AlienSword)
                {
                    // Kept as a compatibility checkpoint; catalog weapons remain selectable.
                }
                player.Weapon.ConfigureLoadout(ownedKinds.ToArray(), selectedKind);
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

            startupStatus = "Stage 12: core stable — Android isolation test.";
            if (gameRoot != null)
                GraphicsQualityProfile.ApplyWorldLook(gameRoot.transform, stageNumber);
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
        private Vector3 androidCameraPositionVelocity;

        private void LateUpdate()
        {
            // Hold the camera where the fight ended during the evacuation. If the
            // camera kept tracking the player as the ship ascends, the ship would
            // stay centred instead of visibly leaving the frame in two seconds.
            if (stageTwoVictorySequence != null && stageTwoVictorySequence.IsRunning)
                return;
            if (stageNumber == 3 && stageThreeWorldBuilder != null && stageThreeWorldBuilder.IsCinematic)
                return;
            if (stageNumber == 4 && stageFourWorldBuilder != null && stageFourWorldBuilder.IsCinematic)
                return;
            if (stageNumber == 4 && StageFourEscapeSequence.IsFinaleActive)
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
            androidRuntimeCamera.transform.position = Vector3.SmoothDamp(
                androidRuntimeCamera.transform.position, desired, ref androidCameraPositionVelocity,
                0.12f, Mathf.Infinity, Time.deltaTime);
            androidRuntimeCamera.transform.rotation = Quaternion.Euler(AndroidCameraPitch, AndroidCameraYaw, 0f);
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

            // Let joystick/buttons execute for 120 clean frames before adding anything else.
            for (int i = 0; i < 120; i++)
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

            for (int i = 0; i < 120; i++)
                yield return null;

            StartupCheckpoint.Set("AndroidHudStable120Frames");

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

            yield return new WaitForSecondsRealtime(0.75f);

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

        private string GetStageTwoResultMessage()
        {
            StageTwoArenaManager arena = GetComponent<StageTwoArenaManager>();
            int rank = arena != null ? arena.LastLocalRank : 0;
            if (rank == 1) return "1ST PLACE  •  NEXT STAGE UNLOCKED  •  +300 COINS";
            if (rank == 2) return "2ND PLACE  •  VOID RIFLE REWARD IN STORE";
            if (rank == 3) return "3RD PLACE  •  ALIEN SWORD REWARD IN STORE";
            return "ARENA CLEARED  •  FINAL RANK #" + rank + "  •  CHECK THE STORE";
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
                if (stageTwoVictorySequence == null)
                    StartStageTwoVictorySequence();
                if (stageTwoVictorySequence != null && stageTwoVictorySequence.IsRunning)
                {
                    DrawStageTwoCinematicOverlay();
                    return;
                }
                if (stageTwoVictorySequence != null && stageTwoVictorySequence.IsComplete)
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
                    BeginStageTwoSelection(true);
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
                    ? (stageNumber >= 2 ? GetStageTwoResultMessage() : "Extraction reached. Mission complete.")
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
                    session.RestartMission();
                }
            }

            if (GUI.Button(
                new Rect(panel.x + 32f + buttonWidth + buttonGap, buttonY, buttonWidth, 54f),
                "MAIN MENU",
                buttonStyle))
            {
                ReturnToMainMenuFromResult();
            }
        }

        // Dedicated shortcut so Stage 3 can be entered from the Stage 2 result
        // screen without replaying Stage 1. Stage 3 world construction is added
        // separately; this only establishes the independent test entry point.
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
            titleStyle = new GUIStyle(GUI.skin.label) { fontSize = 34, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleLeft };
            headerStyle = new GUIStyle(GUI.skin.label) { fontSize = 32, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
            smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 19, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            buttonStyle = new GUIStyle(GUI.skin.button) { fontSize = 20, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
        }

        private void BuildMenuStyles()
        {
            float scale = Mathf.Clamp(Mathf.Min(Screen.width, Screen.height) / 720f, 0.75f, 1.25f);
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

        private bool DrawFantasyButton(Rect rect, string label, bool selected = false, bool enabled = true)
        {
            float scale = Mathf.Clamp(Mathf.Min(Screen.width, Screen.height) / 720f, 0.75f, 1.25f);
            bool hovered = enabled && Event.current != null && rect.Contains(Event.current.mousePosition);
            bool pressed = enabled && hovered && Event.current.type == EventType.MouseDown;
            int scaleKey = label.GetHashCode() ^ Mathf.RoundToInt(rect.x * 3f) ^ Mathf.RoundToInt(rect.y * 7f);
            if (!menuButtonScales.TryGetValue(scaleKey, out float currentScale)) currentScale = 1f;
            float targetScale = pressed ? 0.965f : hovered ? 1.025f : 1f;
            currentScale = Mathf.Lerp(currentScale, targetScale, 1f - Mathf.Exp(-20f * Time.unscaledDeltaTime));
            menuButtonScales[scaleKey] = currentScale;
            rect = new Rect(rect.center.x - rect.width * currentScale * 0.5f,
                rect.center.y - rect.height * currentScale * 0.5f, rect.width * currentScale, rect.height * currentScale);
            Color edge = !enabled
                ? new Color(0.28f, 0.31f, 0.35f, 0.62f)
                : (selected || hovered
                    ? new Color(0.30f, 0.94f, 0.98f, 0.92f)
                    : new Color(0.35f, 0.66f, 0.78f, 0.68f));

            // Layered translucent glass with a lower shadow and top bevel gives
            // menu controls a raised, readable appearance without changing layout.
            Fill(new Rect(rect.x + 3f * scale, rect.y + 5f * scale, rect.width, rect.height),
                new Color(0f, 0f, 0f, 0.36f));
            Fill(rect, edge);
            float inset = Mathf.Max(2f, 2.5f * scale);
            Rect inner = new Rect(rect.x + inset, rect.y + inset,
                Mathf.Max(0f, rect.width - inset * 2f), Mathf.Max(0f, rect.height - inset * 2f));
            Fill(inner, enabled ? new Color(0.025f, 0.075f, 0.12f, 0.78f) : new Color(0.035f, 0.04f, 0.045f, 0.72f));
            Fill(new Rect(inner.x + 2f * scale, inner.y + 2f * scale,
                Mathf.Max(0f, inner.width - 4f * scale), Mathf.Max(1f, rect.height * 0.20f)),
                new Color(0.52f, 0.86f, 0.98f, enabled ? (selected || hovered ? 0.25f : 0.13f) : 0.025f));
            Fill(new Rect(inner.x + 2f * scale, inner.yMax - 3f * scale,
                Mathf.Max(0f, inner.width - 4f * scale), 2f * scale),
                new Color(0f, 0f, 0f, 0.30f));
            Fill(new Rect(inner.x + 2f * scale, inner.y + 2f * scale,
                2f * scale, Mathf.Max(0f, inner.height - 4f * scale)),
                new Color(0.65f, 0.94f, 1f, enabled ? 0.34f : 0.07f));

            bool clicked = enabled && GUI.Button(rect, GUIContent.none, GUIStyle.none);
            GUI.Label(rect, label, menuButtonStyle);
            // Native Android menu touch handling can change screens in Update before
            // the synthesized IMGUI mouse event is processed. Suppress that same-frame
            // click so the new screen cannot accidentally activate a second button.
            if (!enabled || Time.frameCount == suppressMenuButtonClicksFrame)
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