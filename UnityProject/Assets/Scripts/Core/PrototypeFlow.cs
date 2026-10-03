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
        private enum ScreenMode { HeroSelect, DropMap, Match }
        private ScreenMode mode = ScreenMode.HeroSelect;
        private PlayerController player;
        private EnemySpawner enemySpawner;
        private MobileInputHub mobileInput;
        private RuntimeCombatHUD combatHud;
        private CameraFollow25D followCamera;
        private Camera activeCamera;
        private Vector2 spawnWorld = new Vector2(0f, -4f);
        private bool spawnChosen, startingMatch, matchInputArmed;
        private int selectedHero;
        private string startupStatus = string.Empty;
        private GameObject mainCameraRoot, playerRoot, mobileInputRoot, worldBoundsRoot, gameRoot;
        private GameBootstrap gameBootstrap;
        private Texture2D pixel, mapTexture;
        private GUIStyle titleStyle, headerStyle, bodyStyle, buttonStyle, smallStyle;
        private static readonly string[] HeroNames = { "KING ARDESHIR", "PARS GUARD", "ROYAL SCOUT", "SILK WARRIOR", "DESERT KNIGHT" };

        private void Awake()
        {
            StartupCheckpoint.Set("PrototypeFlowAwake");
            Debug.Log("PERSIA_FLOW: PrototypeFlow Awake");
        }

        private void Start() { StartCoroutine(InitializeMenuAfterActivation()); }

        private IEnumerator InitializeMenuAfterActivation()
        {
            yield return null;
            EnsureUiInitialized();
            CacheGameplayRoots();
            player = playerRoot != null ? playerRoot.GetComponent<PlayerController>() : null;
            mobileInput = mobileInputRoot != null ? mobileInputRoot.GetComponent<MobileInputHub>() : null;
            gameBootstrap = gameRoot != null ? gameRoot.GetComponent<GameBootstrap>() : null;
            enemySpawner = gameRoot != null ? gameRoot.GetComponentInChildren<EnemySpawner>(true) : null;
            combatHud = gameRoot != null ? gameRoot.GetComponentInChildren<RuntimeCombatHUD>(true) : null;
            GateGameplay(false);
            StartupCheckpoint.Set("PrototypeFlowMenuReady");
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

        private void Update()
        {
            if (mode == ScreenMode.Match) return;
            if (mode == ScreenMode.DropMap) HandleDropTouches();
        }

        private void OnGUI()
        {
            EnsureUiInitialized();
            if (mode == ScreenMode.Match)
            {
                DrawMatchDiagnosticOverlay();
                return;
            }
            DrawBackdrop();
            if (startingMatch)
            {
                if (gameBootstrap != null && gameBootstrap.IsWorldPreparing && !string.IsNullOrEmpty(gameBootstrap.CurrentStage))
                    startupStatus = gameBootstrap.CurrentStage;
                float width = Mathf.Min(Screen.width - 48f, 760f), height = 170f;
                Rect panel = new Rect((Screen.width - width) * 0.5f, Screen.height * 0.5f - height * 0.5f, width, height);
                Fill(panel, new Color(0.04f, 0.07f, 0.11f, 0.96f));
                Fill(new Rect(panel.x, panel.y, panel.width, 6f), new Color(0.92f, 0.66f, 0.18f, 1f));
                GUI.Label(new Rect(panel.x + 18f, panel.y + 30f, panel.width - 36f, 40f), "PERSIA WAR", headerStyle);
                GUI.Label(new Rect(panel.x + 18f, panel.y + 76f, panel.width - 36f, 30f), startupStatus, bodyStyle);
                GUI.Label(new Rect(panel.x + 18f, panel.y + 116f, panel.width - 36f, 28f), "Preparing battlefield...", smallStyle);
                return;
            }
            if (mode == ScreenMode.HeroSelect) DrawHeroSelect(); else DrawDropMap();
        }

        private void DrawMatchDiagnosticOverlay()
        {
            Color old = GUI.color;
            GUI.color = new Color(0.015f, 0.025f, 0.04f, 0.94f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), pixel);
            GUI.color = old;

            float width = Mathf.Min(Screen.width - 48f, 820f);
            float height = Mathf.Min(Screen.height - 80f, 330f);
            Rect panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);
            Fill(panel, new Color(0.04f, 0.07f, 0.11f, 0.98f));
            Fill(new Rect(panel.x, panel.y, panel.width, 7f), new Color(0.92f, 0.66f, 0.18f, 1f));

            GUI.Label(new Rect(panel.x + 20f, panel.y + 22f, panel.width - 40f, 42f), "PERSIA WAR", headerStyle);
            GUI.Label(new Rect(panel.x + 20f, panel.y + 68f, panel.width - 40f, 34f), "CORE MATCH REACHED", bodyStyle);

            GUI.Label(new Rect(panel.x + 28f, panel.y + 118f, panel.width - 56f, 34f), "Last checkpoint: " + StartupCheckpoint.Last, bodyStyle);
            GUI.Label(new Rect(panel.x + 28f, panel.y + 162f, panel.width - 56f, 30f), "Camera: ENABLED   •   Follow: ISOLATED", smallStyle);
            GUI.Label(new Rect(panel.x + 28f, panel.y + 196f, panel.width - 56f, 30f), "Player: PREPARED   •   HUD: ACTIVE • MobileInput: TESTING", smallStyle);
            GUI.Label(new Rect(panel.x + 28f, panel.y + 232f, panel.width - 56f, 44f), "Diagnostic mode — RuntimeCombatHUD + MobileInput only; EnemySpawner remains isolated.", smallStyle);
            GUI.Label(new Rect(panel.x + 28f, panel.y + 280f, panel.width - 56f, 30f), "MobileInput full gameplay test: movement, fire and grenade input are enabled. EnemySpawner remains isolated.", smallStyle);
        }

        private void DrawBackdrop()
        {
            Color old = GUI.color; GUI.color = new Color(0.035f, 0.08f, 0.13f, 0.96f);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), pixel); GUI.color = old;
            Rect topBar = new Rect(0f, 0f, Screen.width, Mathf.Min(110f, Screen.height * 0.16f));
            Fill(topBar, new Color(0.02f, 0.035f, 0.06f, 0.84f));
            GUI.Label(new Rect(28f, 18f, Screen.width * 0.55f, 48f), "PERSIA WAR", titleStyle);
            GUI.Label(new Rect(30f, 62f, Screen.width * 0.60f, 30f), "CLASSIC BATTLE ROYALE  •  PROTOTYPE", smallStyle);
        }

        private void DrawHeroSelect()
        {
            GUI.Label(new Rect(0f, 125f, Screen.width, 52f), "CHOOSE YOUR HERO", headerStyle);
            GUI.Label(new Rect(0f, 174f, Screen.width, 34f), "SHORT • CHIBI • PERSIAN-INSPIRED", smallStyle);
            float gap = 14f, totalWidth = Mathf.Min(Screen.width - 44f, 980f), cardWidth = (totalWidth - gap * 4f) / 5f;
            float startX = (Screen.width - totalWidth) * 0.5f, top = 230f, cardHeight = Mathf.Min(310f, Screen.height - 360f);
            for (int i = 0; i < HeroNames.Length; i++)
            {
                Rect card = new Rect(startX + i * (cardWidth + gap), top, cardWidth, cardHeight);
                DrawHeroCard(card, i, i == selectedHero);
                if (GUI.Button(card, GUIContent.none, GUIStyle.none)) selectedHero = i;
            }
            Rect continueRect = new Rect(Screen.width * 0.5f - 180f, Screen.height - 112f, 360f, 62f);
            if (GUI.Button(continueRect, "SELECT SPAWN POINT", buttonStyle)) mode = ScreenMode.DropMap;
        }

        private void DrawHeroCard(Rect rect, int index, bool selected)
        {
            Color panel = selected ? new Color(0.22f, 0.46f, 0.68f, 0.98f) : new Color(0.08f, 0.12f, 0.17f, 0.94f);
            Fill(rect, panel); Fill(new Rect(rect.x, rect.y, rect.width, 8f), selected ? new Color(0.95f, 0.68f, 0.18f) : new Color(0.20f, 0.28f, 0.36f));
            Rect figure = new Rect(rect.x + rect.width * 0.18f, rect.y + 34f, rect.width * 0.64f, rect.height * 0.52f);
            DrawChibiFigure(figure, index);
            GUI.Label(new Rect(rect.x + 8f, rect.yMax - 78f, rect.width - 16f, 30f), HeroNames[index], smallStyle);
            GUI.Label(new Rect(rect.x + 8f, rect.yMax - 48f, rect.width - 16f, 30f), selected ? "SELECTED" : "TAP TO SELECT", smallStyle);
        }

        private void DrawChibiFigure(Rect rect, int index)
        {
            Color[] bodyColors = { new Color(0.45f, 0.17f, 0.10f), new Color(0.08f, 0.37f, 0.48f), new Color(0.16f, 0.39f, 0.24f), new Color(0.35f, 0.15f, 0.42f), new Color(0.39f, 0.28f, 0.08f) };
            Vector2 center = new Vector2(rect.center.x, rect.y + rect.height * 0.55f);
            float head = rect.width * 0.33f, body = rect.width * 0.44f;
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
            else if (index == 3) Fill(new Rect(center.x - head * 0.82f, center.y - head * 1.45f, head * 1.64f, head * 0.18f), new Color(0.90f, 0.22f, 0.12f));
        }

        private void DrawDropMap()
        {
            GUI.Label(new Rect(0f, 125f, Screen.width, 52f), "DROP INTO THE CITY", headerStyle);
            GUI.Label(new Rect(0f, 174f, Screen.width, 34f), "TAP ANY OPEN LOCATION TO CHOOSE WHERE YOU START", smallStyle);
            float size = Mathf.Min(Screen.width - 70f, Screen.height - 330f);
            Rect mapRect = new Rect((Screen.width - size) * 0.5f, 220f, size, size);
            DrawTacticalMap(mapRect);
            if (GUI.Button(mapRect, GUIContent.none, GUIStyle.none))
            {
                Vector2 p = Event.current.mousePosition;
                float u = Mathf.Clamp01((p.x - mapRect.x) / mapRect.width), v = Mathf.Clamp01((p.y - mapRect.y) / mapRect.height);
                spawnWorld = new Vector2(Mathf.Lerp(-96f, 96f, u), Mathf.Lerp(96f, -96f, v)); spawnChosen = true;
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
            if (GUI.Button(start, "START MATCH", buttonStyle)) StartMatch();
            GUI.enabled = true;
        }

        private void DrawTacticalMap(Rect rect)
        {
            Fill(rect, new Color(0.39f, 0.66f, 0.27f, 1f)); float block = rect.width / 6f;
            for (int i = 1; i < 6; i++) { float road = rect.x + i * block; Fill(new Rect(road - 13f, rect.y, 26f, rect.height), new Color(0.16f, 0.18f, 0.19f)); Fill(new Rect(rect.x, rect.y + i * block - 13f, rect.width, 26f), new Color(0.16f, 0.18f, 0.19f)); }
            for (int gx = 0; gx < 6; gx++) for (int gy = 0; gy < 6; gy++)
            {
                Rect cell = new Rect(rect.x + gx * block + 7f, rect.y + gy * block + 7f, block - 14f, block - 14f);
                Color c = new Color(0.50f, 0.72f, 0.31f, 1f);
                if (gx >= 4 && gy <= 2) c = new Color(0.48f, 0.44f, 0.39f, 1f);
                if (gx == 2 && gy == 3) c = new Color(0.30f, 0.50f, 0.66f, 1f);
                Fill(cell, c);
                if (gx != 5 && gy != 5) Fill(new Rect(cell.x + 10f, cell.y + 10f, cell.width * 0.42f, cell.height * 0.34f), new Color(0.86f, 0.69f, 0.30f, 1f));
            }
            GUI.Label(new Rect(rect.x + 12f, rect.y + 10f, 190f, 30f), "PERSIA WAR • LEVEL 1", smallStyle);
            GUI.Label(new Rect(rect.x + rect.width - 160f, rect.y + 10f, 145f, 30f), "RUINED QUARTER", smallStyle);
        }

        private void HandleDropTouches()
        {
            if (!Application.isMobilePlatform && !Input.GetMouseButtonDown(0)) return;
            Vector2 screen;
            if (Application.isMobilePlatform) { if (Input.touchCount == 0 || Input.GetTouch(0).phase != TouchPhase.Began) return; screen = Input.GetTouch(0).position; }
            else screen = Input.mousePosition;
            screen.y = Screen.height - screen.y;
            float size = Mathf.Min(Screen.width - 70f, Screen.height - 330f);
            Rect mapRect = new Rect((Screen.width - size) * 0.5f, 220f, size, size);
            if (!mapRect.Contains(screen)) return;
            Vector2 uv = new Vector2(Mathf.Clamp01((screen.x - mapRect.x) / mapRect.width), Mathf.Clamp01((screen.y - mapRect.y) / mapRect.height));
            spawnWorld = new Vector2(Mathf.Lerp(-96f, 96f, uv.x), Mathf.Lerp(96f, -96f, uv.y)); spawnChosen = true;
        }

        private void StartMatch() { if (!spawnChosen || startingMatch) return; StartCoroutine(BeginMatchSafely()); }

        private IEnumerator BeginMatchSafely()
        {
            startingMatch = true; matchInputArmed = false; startupStatus = "Stage 1: starting battlefield preparation...";
            StartupCheckpoint.Set("MatchActivationStarted");
            if (gameRoot != null && !gameRoot.activeSelf)
            {
                startupStatus = "Stage 2: activating battlefield systems..."; gameRoot.SetActive(true); yield return null;
                CacheGameplayRoots(); gameBootstrap = gameRoot.GetComponent<GameBootstrap>(); player = playerRoot != null ? playerRoot.GetComponent<PlayerController>() : player;
                mobileInput = mobileInputRoot != null ? mobileInputRoot.GetComponent<MobileInputHub>() : mobileInput;
                enemySpawner = gameRoot.GetComponentInChildren<EnemySpawner>(true); combatHud = gameRoot.GetComponentInChildren<RuntimeCombatHUD>(true); GateGameplay(false);
            }
            if (gameBootstrap == null) gameBootstrap = gameRoot != null ? gameRoot.GetComponent<GameBootstrap>() : FindFirstObjectByType<GameBootstrap>(FindObjectsInactive.Include);
            if (gameBootstrap == null) { StartupCheckpoint.Set("GameBootstrapMissing"); startingMatch = false; yield break; }
            if (!gameBootstrap.IsWorldReady && !gameBootstrap.IsWorldPreparing && !gameBootstrap.WorldBuildFailed) gameBootstrap.PrepareWorld();
            while (gameBootstrap.IsWorldPreparing) { startupStatus = gameBootstrap.CurrentStage; yield return null; }
            if (gameBootstrap.WorldBuildFailed || !gameBootstrap.IsWorldReady) { StartupCheckpoint.Set("MatchActivationFailed"); startingMatch = false; yield break; }
            yield return ActivateRoot(playerRoot, "Player");
            player = playerRoot != null ? playerRoot.GetComponent<PlayerController>() : player;
            if (player == null) { StartupCheckpoint.Set("PlayerInitializationFailed"); startingMatch = false; yield break; }
            player.enabled = true; player.PrepareForMatch(selectedHero);
            for (int frame = 0; frame < 30 && !player.IsGameplayReady; frame++) yield return null;
            if (!player.IsGameplayReady) { StartupCheckpoint.Set("PlayerInitializationFailed"); startingMatch = false; yield break; }
            StartupCheckpoint.Set("PlayerPreparedForMatch");
            StartupCheckpoint.Set("MobileInputActivationDeferred");
            yield return null;
            yield return ActivateRoot(worldBoundsRoot, "WorldBounds");
            player.transform.position = new Vector3(spawnWorld.x, 0f, spawnWorld.y); ApplyHeroStyle(selectedHero);
            activeCamera = mainCameraRoot != null ? mainCameraRoot.GetComponent<Camera>() : null;
            followCamera = activeCamera != null ? activeCamera.GetComponent<CameraFollow25D>() : null;
            if (activeCamera != null) activeCamera.enabled = false;
            if (followCamera != null) followCamera.enabled = false;
            StartupCheckpoint.Set("MainCameraRootActivationStarted");
            yield return ActivateRoot(mainCameraRoot, "Main Camera");
            StartupCheckpoint.Set("MainCameraRootActivated");
            if (followCamera != null) followCamera.SetTarget(player.transform);
            StartupCheckpoint.Set("MainCameraTargetReady");
            if (activeCamera != null) { StartupCheckpoint.Set("MainCameraEnableStarted"); activeCamera.enabled = true; StartupCheckpoint.Set("MainCameraEnabled"); }
            yield return null;
            StartupCheckpoint.Set("CameraFollowIsolated");
            if (followCamera != null) followCamera.enabled = false;
            StartupCheckpoint.Set("MatchCoreReady");
            mode = ScreenMode.Match; startingMatch = false; startupStatus = string.Empty; StartupCheckpoint.Set("MatchStarted");
            // DIAGNOSTIC STEP 1: enable HUD only. MobileInput and EnemySpawner remain isolated.
            yield return null;
            if (combatHud == null) combatHud = FindFirstObjectByType<RuntimeCombatHUD>(FindObjectsInactive.Include);
            if (combatHud != null)
            {
                combatHud.ConfigurePlayer(player);
                combatHud.enabled = true;
                StartupCheckpoint.Set("CombatHudEnabled");
            }
            StartupCheckpoint.Set("HudOnlyEnabled");
            // DIAGNOSTIC STEP 2: enable MobileInput execution only. Keep EnemySpawner isolated.
            yield return null;
#if UNITY_ANDROID
            MobileInputHub.SetAndroidExecutionArmed(true);
            MobileInputHub.SetAndroidTouchProcessingArmed(true);
            MobileInputHub.SetAndroidTouchGameplayArmed(true);
            MobileInputHub.SetAndroidTouchMoveGameplayArmed(false);
#endif
            if (mobileInput == null) mobileInput = FindFirstObjectByType<MobileInputHub>(FindObjectsInactive.Include);
            if (mobileInput != null)
            {
                mobileInput.enabled = true;
                StartupCheckpoint.Set("MobileInputEnabled");
            }
            StartupCheckpoint.Set("MobileInputOnlyEnabled");
            StartupCheckpoint.Set("MobileInputTouchProcessingEnabled");
            StartupCheckpoint.Set("MobileInputGameplayEnabled");
            StartupCheckpoint.Set("EnemySpawnerIsolated");
        }

        private IEnumerator InitializeMatchServices()
        {
            yield return null; if (player == null) yield break;
            matchInputArmed = true;
#if UNITY_ANDROID
            MobileInputHub.SetAndroidExecutionArmed(true);
#endif
            if (mobileInput == null) mobileInput = FindFirstObjectByType<MobileInputHub>(FindObjectsInactive.Include);
            if (mobileInput != null) { mobileInput.enabled = true; mobileInput.EnableMinimap(); StartupCheckpoint.Set("MobileInputEnabled"); }
            if (combatHud == null) combatHud = FindFirstObjectByType<RuntimeCombatHUD>(FindObjectsInactive.Include);
            if (combatHud != null) { combatHud.ConfigurePlayer(player); combatHud.enabled = true; StartupCheckpoint.Set("CombatHudEnabled"); }
            if (enemySpawner == null) enemySpawner = FindFirstObjectByType<EnemySpawner>(FindObjectsInactive.Include);
            if (enemySpawner != null) { enemySpawner.Configure(player.transform, 8, 44f, 0f); enemySpawner.enabled = true; StartupCheckpoint.Set("EnemySpawnerEnabled"); }
            StartupCheckpoint.Set("GameplaySystemsEnabled");
        }

        private IEnumerator ActivateRoot(GameObject root, string rootName)
        {
            if (root == null) yield break;
            if (!root.activeSelf) { startupStatus = "Starting " + rootName + "..."; root.SetActive(true); yield return null; }
        }

        private void GateGameplay(bool enabled)
        {
            if (player != null) player.enabled = enabled;
#if !UNITY_ANDROID
            if (mobileInput != null) mobileInput.enabled = enabled && (matchInputArmed || !Application.isMobilePlatform);
#endif
            if (combatHud != null) combatHud.enabled = enabled;
            if (enemySpawner != null) enemySpawner.enabled = enabled;
            if (!enabled) matchInputArmed = false;
        }

        private void ApplyHeroStyle(int heroIndex)
        {
            if (player == null) return;
            StylizedCharacterVisual visual = player.GetComponentInChildren<StylizedCharacterVisual>(true);
            if (visual != null) visual.ConfigurePlayerHero(heroIndex);
        }

        private Vector2 WorldToMap(Vector2 world, Rect rect)
        {
            float x = Mathf.InverseLerp(-96f, 96f, world.x), y = Mathf.InverseLerp(96f, -96f, world.y);
            return new Vector2(rect.x + x * rect.width, rect.y + y * rect.height);
        }

        private void EnsureUiInitialized()
        {
            if (pixel == null) CreateTextures();
            if (titleStyle == null) BuildStyles();
        }

        private void CreateTextures()
        {
            pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false); pixel.SetPixel(0, 0, Color.white); pixel.Apply(); mapTexture = pixel;
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
            Color old = GUI.color; GUI.color = color; GUI.DrawTexture(rect, pixel); GUI.color = old;
        }

        private void DrawCircle(Vector2 center, float radius, Color color)
        {
            Color old = GUI.color; GUI.color = color; GUI.DrawTexture(new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f), Texture2D.whiteTexture); GUI.color = old;
        }
    }
}