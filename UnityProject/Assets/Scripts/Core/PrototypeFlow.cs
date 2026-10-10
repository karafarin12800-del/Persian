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
        private bool startingMatch;
        private bool matchInputArmed;
        private bool matchPaused;
        private float resultOverlayStartTime = -1f;
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
            // Use direct Touch.fingerId input for front-end controls on Android.
            // GUI.Button still draws the visual surface, but must not be the only
            // path that can advance the menu if touch-to-mouse synthesis is missing.
            if (mode == ScreenMode.Match) return;
            if (mode == ScreenMode.HeroSelect)
            {
                HandleHeroSelectTouches();
                return;
            }
            if (mode == ScreenMode.DropMap)
                HandleDropTouches();
        }

        private void OnGUI()
        {
            EnsureUiInitialized();
            if (mode == ScreenMode.Match)
            {
#if UNITY_ANDROID
                DrawAndroidPresentationHUD();
                DrawAndroidPauseControl();

                if (GameSession.Instance != null && GameSession.Instance.IsFinished)
                {
                    if (resultOverlayStartTime < 0f)
                        resultOverlayStartTime = Time.unscaledTime;
                    DrawAndroidResultOverlay();
                }
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
                DrawCircle(enemyPoint, 3.5f * scale, new Color(1f, 0.22f, 0.16f, 1f));
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

            // Blue marker matches the permanent pillar visible in the 3D city.
            Vector2 skyGuidePoint = WorldToMap(AndroidSkyGuideBeaconPoint, map);
            DrawCircle(skyGuidePoint, 8.5f * scale, new Color(0.03f, 0.32f, 1f, 1f));
            DrawCircle(skyGuidePoint, 3.7f * scale, new Color(0.56f, 0.90f, 1f, 1f));

            if (ExtractionBeacon.IsActive)
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
                3.5f * scale, new Color(0.20f, 0.58f, 1f, 1f));
            GUI.Label(new Rect(legend.x + 157f * scale, legend.y, legend.width - 162f * scale, legend.height),
                "B: BEACON", smallStyle);

            Rect counters = new Rect(
                Screen.width - rightW - margin,
                map.yMax + 7f * scale,
                rightW,
                30f * scale);
            Fill(counters, new Color(0.03f, 0.06f, 0.08f, 0.86f));
            GUI.Label(new Rect(counters.x + 8f * scale, counters.y, counters.width - 16f * scale, counters.height),
                "WAVE  " + wave + "     SCORE  " + score, smallStyle);

            if (ExtractionBeacon.IsActive)
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
            Rect topBar = new Rect(0f, 0f, Screen.width, Mathf.Min(110f, Screen.height * 0.16f));
            Fill(topBar, new Color(0.02f, 0.035f, 0.06f, 0.48f));
            GUI.Label(new Rect(28f, 18f, Screen.width * 0.55f, 48f), "♛  PERSIA WAR", titleStyle);
            GUI.Label(new Rect(30f, 62f, Screen.width * 0.60f, 30f), "CLASSIC BATTLE ROYALE", smallStyle);
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
                float gap = 14f;
                float totalWidth = Mathf.Min(Screen.width - 44f, 980f);
                float cardWidth = (totalWidth - gap * 4f) / 5f;
                float startX = (Screen.width - totalWidth) * 0.5f;
                float top = 230f;
                float cardHeight = Mathf.Min(310f, Screen.height - 360f);

                for (int hero = 0; hero < HeroNames.Length; hero++)
                {
                    Rect card = new Rect(startX + hero * (cardWidth + gap), top, cardWidth, cardHeight);
                    if (!card.Contains(gui))
                        continue;

                    selectedHero = hero;
                    StartupCheckpoint.Set("HeroSelectedByTouch");
                    return;
                }

                Rect continueRect = new Rect(Screen.width * 0.5f - 180f, Screen.height - 112f, 360f, 62f);
                if (continueRect.Contains(gui))
                {
                    mode = ScreenMode.DropMap;
                    StartupCheckpoint.Set("HeroSelectionContinuedByTouch");
                    return;
                }
            }
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
            GUI.Label(hint, spawnChosen ? "Spawn point locked. Press START MATCH." : "Tip: avoid the ruined quarter for the safest start.", smallStyle);

            Rect start = new Rect(Screen.width * 0.5f - 180f, Screen.height - 64f, 360f, 52f);
            GUI.enabled = spawnChosen;
            if (GUI.Button(start, "START MATCH", buttonStyle))
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

            Vector2 beacon = WorldToMap(AndroidSkyGuideBeaconPoint, rect);
            DrawCircle(beacon, Mathf.Max(3f, rect.width * 0.012f), new Color(0.08f, 0.34f, 1f, 1f));
            DrawCircle(beacon, Mathf.Max(1.5f, rect.width * 0.005f), new Color(0.62f, 0.91f, 1f, 1f));
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
            Rect startRect = new Rect(Screen.width * 0.5f - 180f, Screen.height - 64f, 360f, 52f);
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

        private void StartMatch()
        {
            if (!spawnChosen || startingMatch)
                return;

            matchPaused = false;
            Time.timeScale = 1f;
            StartCoroutine(BeginMatchSafely());
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

            if (GUI.Button(button, matchPaused ? "▶" : "Ⅱ", buttonStyle))
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
#if UNITY_ANDROID
            if (androidRuntimeCamera == null || player == null)
                return;

            if (cameraOcclusionFader == null)
                cameraOcclusionFader = androidRuntimeCamera.GetComponent<CameraOcclusionFader>();
            if (cameraOcclusionFader != null)
                cameraOcclusionFader.SetTarget(player.transform);

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
                enemySpawner.Configure(player.transform, 11, 84f, 0f);
                enemySpawner.enabled = true;
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
            // The blue extraction gate is reserved for a win after the final opponent
            // is eliminated. A player defeat skips the transition and shows GAME OVER.
            if (session.PlayerWon)
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
                    GUI.Label(new Rect(0f, Screen.height * 0.22f, Screen.width, 64f), "BLUE EXTRACTION GATE", headerStyle);
                    GUI.Label(new Rect(0f, Screen.height * 0.31f, Screen.width, 38f), "PREPARING NEXT STAGE...", bodyStyle);
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
                    ? "Extraction reached. Mission complete."
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
            headerStyle = new GUIStyle(GUI.skin.label) { fontSize = 32, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            bodyStyle = new GUIStyle(GUI.skin.label) { fontSize = 20, alignment = TextAnchor.MiddleCenter };
            smallStyle = new GUIStyle(GUI.skin.label) { fontSize = 19, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
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