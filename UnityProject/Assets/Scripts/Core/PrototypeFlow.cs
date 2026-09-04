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

        private static PrototypeFlow instance;

        private ScreenMode mode = ScreenMode.HeroSelect;
        private PlayerController player;
        private EnemySpawner enemySpawner;
        private MobileInputHub mobileInput;
        private RuntimeCombatHUD combatHud;
        private CameraFollow25D followCamera;
        private Vector2 spawnWorld = new Vector2(0f, -4f);
        private bool spawnChosen;
        private int selectedHero;
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

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void CreateRuntimeFlow()
        {
            if (instance != null) return;
            GameObject root = new GameObject("PrototypeFlow");
            root.hideFlags = HideFlags.DontSave;
            instance = root.AddComponent<PrototypeFlow>();
        }

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            player = FindFirstObjectByType<PlayerController>();
            enemySpawner = FindFirstObjectByType<EnemySpawner>();
            mobileInput = FindFirstObjectByType<MobileInputHub>();
            combatHud = FindFirstObjectByType<RuntimeCombatHUD>();
            Camera camera = Camera.main;
            followCamera = camera != null ? camera.GetComponent<CameraFollow25D>() : null;

            CreateTextures();
            BuildStyles();
            GateGameplay(false);
            ApplyHeroStyle(selectedHero);
        }

        private void Update()
        {
            if (mode == ScreenMode.Match || player == null) return;

            if (mode == ScreenMode.DropMap)
                HandleDropTouches();
        }

        private void OnGUI()
        {
            if (mode == ScreenMode.Match) return;

            DrawBackdrop();
            if (mode == ScreenMode.HeroSelect)
                DrawHeroSelect();
            else
                DrawDropMap();
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
                    ApplyHeroStyle(selectedHero);
                }
            }

            Rect continueRect = new Rect(Screen.width * 0.5f - 180f, Screen.height - 112f, 360f, 62f);
            if (GUI.Button(continueRect, "SELECT SPAWN POINT", buttonStyle))
            {
                mode = ScreenMode.DropMap;
            }
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

            // Neighborhoods: safe residential blocks, central services, and ruined sector.
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
                    {
                        Fill(new Rect(cell.x + 10f, cell.y + 10f, cell.width * 0.42f, cell.height * 0.34f), new Color(0.86f, 0.69f, 0.30f, 1f));
                    }
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

            // Keep the drop point inside the existing 192x192 gameplay world and away from the edge.
            float x = Mathf.Lerp(-78f, 78f, uv.x);
            float z = Mathf.Lerp(78f, -78f, uv.y);
            spawnWorld = new Vector2(x, z);
            spawnChosen = true;
        }

        private void StartMatch()
        {
            if (player == null) return;

            player.transform.position = new Vector3(spawnWorld.x, 0f, spawnWorld.y);
            ApplyHeroStyle(selectedHero);
            if (followCamera != null) followCamera.SetTarget(player.transform);

            mode = ScreenMode.Match;
            GateGameplay(true);
        }

        private void GateGameplay(bool enabled)
        {
            if (player != null) player.enabled = enabled;
            if (mobileInput != null) mobileInput.enabled = enabled;
            if (combatHud != null) combatHud.enabled = enabled;
            if (enemySpawner != null) enemySpawner.enabled = enabled;
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

            SetChildMaterial("Body", body);
            SetChildMaterial("ShoulderArmor", accent);
            SetChildMaterial("Crown", heroIndex == 0 ? new Color(0.92f, 0.66f, 0.14f) : new Color(0.64f, 0.68f, 0.72f));
        }

        private void SetChildMaterial(string childName, Color color)
        {
            Transform child = player.transform.Find("PlayerVisual/" + childName);
            if (child == null) return;
            Renderer renderer = child.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = RuntimeMaterialFactory.Create(childName + "HeroStyle", color);
        }

        private Vector2 WorldToMap(Vector2 world, Rect rect)
        {
            float x = Mathf.InverseLerp(-78f, 78f, world.x);
            float y = Mathf.InverseLerp(78f, -78f, world.y);
            return new Vector2(rect.x + x * rect.width, rect.y + y * rect.height);
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
            titleStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 34,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            headerStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 30,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            bodyStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 20,
                alignment = TextAnchor.MiddleCenter
            };
            smallStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 16,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            buttonStyle = new GUIStyle(GUI.skin.button)
            {
                fontSize = 20,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
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
            GUI.color = color;
            GUI.DrawTexture(new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f), Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}
