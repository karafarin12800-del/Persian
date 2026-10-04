using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public sealed class MobileInputHub : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [SerializeField] private float joystickRadius = 120f;
        [SerializeField] private float minimapSize = 190f;
        [SerializeField] private float fireRepeatInterval = 0.155f;
        [SerializeField] private Camera gameplayCamera;
        [SerializeField] private float moveRadius = 140f;

        private int movePointerId = -1;
        private int firePointerId = -1;
        private int grenadePointerId = -1;
        private int swordPointerId = -1;
        private int reloadPointerId = -1;
        private Vector2 moveStartScreen;
        private Vector2 moveValue;
        private float nextFireTime;
        private Camera minimapCamera;
        private RenderTexture minimapTexture;
        private Texture2D circleTexture;
        private Texture2D lineTexture;
        private GrenadeController grenadeController;
        private bool minimapEnabled;
        private GUIStyle buttonTextStyle;
        private EnemyChase[] androidRadarEnemies = System.Array.Empty<EnemyChase>();
        private float nextAndroidRadarScanTime;
        private EnemySpawner radarSpawner;

        public Vector2 MoveValue => moveValue;

#if UNITY_ANDROID
        // Kept for backward compatibility with older startup code. The production
        // match uses a single authoritative execution gate; touch sub-gates no longer
        // disable movement/fire once gameplay has started.
        private static bool androidExecutionArmed;
        private static bool androidTouchProcessingArmed = true;
        private static bool androidTouchGameplayArmed = true;
        private static bool androidTouchMoveGameplayArmed = true;
        public static void SetAndroidExecutionArmed(bool armed) => androidExecutionArmed = armed;
        public static void SetAndroidTouchProcessingArmed(bool armed) => androidTouchProcessingArmed = armed;
        public static void SetAndroidTouchGameplayArmed(bool armed) => androidTouchGameplayArmed = armed;
        public static void SetAndroidTouchMoveGameplayArmed(bool armed) => androidTouchMoveGameplayArmed = armed;
#endif

        public void EnableMinimap()
        {
            minimapEnabled = true;
            StartupCheckpoint.Set("MinimapEnabled");
        }

        private void Awake()
        {
#if UNITY_ANDROID
            androidExecutionArmed = false;
            androidTouchProcessingArmed = true;
            androidTouchGameplayArmed = true;
            androidTouchMoveGameplayArmed = true;
#else
            if (player == null) player = FindFirstObjectByType<PlayerController>();
            if (gameplayCamera == null) gameplayCamera = Camera.main;
            if (player != null) grenadeController = player.Grenades;
#endif
            if (moveRadius > 0f) joystickRadius = Mathf.Clamp(moveRadius * 0.86f, 90f, 150f);
        }

        private void OnDestroy()
        {
            if (minimapTexture != null) { minimapTexture.Release(); Destroy(minimapTexture); }
            if (minimapCamera != null) Destroy(minimapCamera.gameObject);
            if (circleTexture != null) Destroy(circleTexture);
            if (lineTexture != null) Destroy(lineTexture);
        }

        private void Update()
        {
#if UNITY_ANDROID
            if (!androidExecutionArmed)
                return;
#endif
            if (player == null)
            {
                player = FindFirstObjectByType<PlayerController>();
                if (player == null) return;
                grenadeController = player.Grenades;
            }

            if (minimapEnabled) EnsureMinimap();
#if UNITY_ANDROID
            UpdateAndroidRadarCache();
#endif

#if UNITY_EDITOR || UNITY_STANDALONE
            Vector2 keyboard = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (keyboard.sqrMagnitude > 1f) keyboard.Normalize();
            player.SetMoveInput(keyboard);
            if (Input.GetKeyDown(KeyCode.R)) player.Weapon?.Reload();
            if (Input.GetKeyDown(KeyCode.Space)) player.Weapon?.TryMelee();
            if (Input.GetKeyDown(KeyCode.G)) ThrowGrenadeAtTarget();
            if (Input.GetMouseButton(0) && Time.time >= nextFireTime) FireAtNearestTarget();
 #else
            if (Input.touchCount > 0 || movePointerId >= 0 || firePointerId >= 0 || grenadePointerId >= 0)
                HandleTouches();
#endif
            UpdateMinimap();
        }

        private void HandleTouches()
        {
            // Production mobile input: the left side creates a genuinely floating
            // joystick at the initial finger position. The right side contains the
            // four action buttons used by the previous gameplay version.
            float scale = Mathf.Clamp(Mathf.Min(Screen.width, Screen.height) / 1080f, 0.75f, 1.35f);
            float radius = joystickRadius * scale;

            Vector2 fireGuiPos = new Vector2(Screen.width - 120f * scale, Screen.height - 155f * scale);
            Vector2 swordGuiPos = new Vector2(Screen.width - 270f * scale, Screen.height - 85f * scale);
            Vector2 bombGuiPos = new Vector2(Screen.width - 165f * scale, Screen.height - 85f * scale);
            Vector2 reloadGuiPos = new Vector2(Screen.width - 60f * scale, Screen.height - 85f * scale);

            float fireRadius = radius * 0.72f;
            float actionRadius = radius * 0.42f;

            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.phase != TouchPhase.Began) continue;

                bool leftSide = touch.position.x < Screen.width * 0.48f;
                Vector2 touchGuiPosition = new Vector2(touch.position.x, Screen.height - touch.position.y);

                if (leftSide && movePointerId < 0)
                {
                    movePointerId = touch.fingerId;
                    moveStartScreen = touch.position;
                    moveValue = Vector2.zero;
                    continue;
                }

                if (!leftSide && swordPointerId < 0 &&
                    Vector2.Distance(touchGuiPosition, swordGuiPos) <= actionRadius)
                {
                    swordPointerId = touch.fingerId;
                    player.Weapon?.TryMelee();
                    continue;
                }

                if (!leftSide && grenadePointerId < 0 &&
                    Vector2.Distance(touchGuiPosition, bombGuiPos) <= actionRadius)
                {
                    grenadePointerId = touch.fingerId;
                    ThrowGrenadeAtTarget();
                    continue;
                }

                if (!leftSide && reloadPointerId < 0 &&
                    Vector2.Distance(touchGuiPosition, reloadGuiPos) <= actionRadius)
                {
                    reloadPointerId = touch.fingerId;
                    player.Weapon?.Reload();
                    continue;
                }

                if (!leftSide && firePointerId < 0 &&
                    Vector2.Distance(touchGuiPosition, fireGuiPos) <= fireRadius)
                {
                    firePointerId = touch.fingerId;
                    FireAtNearestTarget();
                }
            }

            if (movePointerId >= 0 && TryGetTouch(movePointerId, out Touch moveTouch))
            {
                Vector2 delta = moveTouch.position - moveStartScreen;
                moveValue = Vector2.ClampMagnitude(delta / joystickRadius, 1f);
                player.SetMoveInput(moveValue);
                if (moveTouch.phase == TouchPhase.Ended || moveTouch.phase == TouchPhase.Canceled) { movePointerId = -1; moveValue = Vector2.zero; player.SetMoveInput(Vector2.zero); }
            }
            else if (movePointerId >= 0) { movePointerId = -1; moveValue = Vector2.zero; player.SetMoveInput(Vector2.zero); }
            if (firePointerId >= 0 && TryGetTouch(firePointerId, out Touch fireTouch))
            {
                if (Time.time >= nextFireTime) FireAtNearestTarget();
                if (fireTouch.phase == TouchPhase.Ended || fireTouch.phase == TouchPhase.Canceled) firePointerId = -1;
            }
            else if (firePointerId >= 0) firePointerId = -1;

            if (grenadePointerId >= 0 && TryGetTouch(grenadePointerId, out Touch grenadeTouch))
            {
                if (grenadeTouch.phase == TouchPhase.Ended || grenadeTouch.phase == TouchPhase.Canceled) grenadePointerId = -1;
            }
            else if (grenadePointerId >= 0) grenadePointerId = -1;

            if (swordPointerId >= 0 && TryGetTouch(swordPointerId, out Touch swordTouch))
            {
                if (swordTouch.phase == TouchPhase.Ended || swordTouch.phase == TouchPhase.Canceled) swordPointerId = -1;
            }
            else if (swordPointerId >= 0) swordPointerId = -1;

            if (reloadPointerId >= 0 && TryGetTouch(reloadPointerId, out Touch reloadTouch))
            {
                if (reloadTouch.phase == TouchPhase.Ended || reloadTouch.phase == TouchPhase.Canceled) reloadPointerId = -1;
            }
            else if (reloadPointerId >= 0) reloadPointerId = -1;
        }

        private void FireAtNearestTarget()
        {
            if (player == null || player.IsDefeated || player.Aim == null) return;
            if (Time.time < nextFireTime) return;
            TargetHealth target = player.Aim.CurrentTarget;
            if (target != null && player.Aim.FireAt(target.transform.position)) nextFireTime = Time.time + fireRepeatInterval;
            else if (player.Weapon != null && player.Weapon.Magazine <= 0) player.Weapon.Reload();
        }

        private void ThrowGrenadeAtTarget()
        {
            if (player == null || player.IsDefeated) return;
            if (grenadeController == null) grenadeController = player.Grenades;
            if (grenadeController == null || grenadeController.Grenades <= 0) return;
            Vector3 direction = player.transform.forward;
            TargetHealth target = player.Aim != null ? player.Aim.CurrentTarget : null;
            if (target != null) { Vector3 delta = target.transform.position - player.transform.position; delta.y = 0f; if (delta.sqrMagnitude > 0.001f) direction = delta.normalized; }
            grenadeController.Throw(new Vector2(direction.x, direction.z));
        }

        private void EnsureMinimap()
        {
#if UNITY_ANDROID
            return;
#else
            if (minimapCamera != null && minimapTexture != null) return;
            CreateMinimap();
#endif
        }

        private void CreateMinimap()
        {
            if (minimapCamera != null || minimapTexture != null) return;
            GameObject mapObject = new GameObject("MinimapCamera");
            mapObject.hideFlags = HideFlags.HideAndDontSave;
            minimapCamera = mapObject.AddComponent<Camera>();
            minimapCamera.orthographic = true; minimapCamera.orthographicSize = 96f; minimapCamera.nearClipPlane = 0.1f; minimapCamera.farClipPlane = 350f;
            minimapCamera.clearFlags = CameraClearFlags.SolidColor; minimapCamera.backgroundColor = new Color(0.06f, 0.07f, 0.08f, 1f); minimapCamera.enabled = true;
            minimapTexture = new RenderTexture(256, 256, 16, RenderTextureFormat.ARGB32); minimapTexture.name = "GameplayMinimap"; minimapTexture.filterMode = FilterMode.Bilinear; minimapTexture.Create(); minimapCamera.targetTexture = minimapTexture;
        }

        private void CreateGuiTextures()
        {
            circleTexture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            lineTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false); lineTexture.SetPixel(0, 0, Color.white); lineTexture.Apply();
            Vector2 center = new Vector2(31.5f, 31.5f); float radius = 31f;
            for (int y = 0; y < 64; y++) for (int x = 0; x < 64; x++) { float distance = Vector2.Distance(new Vector2(x, y), center); float alpha = Mathf.Clamp01(radius + 0.5f - distance); circleTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha)); }
            circleTexture.Apply();
        }

        private void UpdateMinimap()
        {
#if UNITY_ANDROID
            return;
#else
            if (minimapCamera == null || player == null) return;
            minimapCamera.transform.position = player.transform.position + Vector3.up * 120f; minimapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
#endif
        }

        private void OnGUI()
        {
            if (!Application.isMobilePlatform && !Application.isEditor) return;
#if UNITY_ANDROID
            if (!androidExecutionArmed) return;
            if (circleTexture == null || lineTexture == null) CreateGuiTextures();
            DrawAndroidTouchHud();
            DrawAndroidRadar();
            DrawAimGuide(Mathf.Clamp(Mathf.Min(Screen.width, Screen.height) / 1080f, 0.75f, 1.35f));
            return;
#endif
            if (circleTexture == null || lineTexture == null) CreateGuiTextures();
            float scale = Mathf.Clamp(Mathf.Min(Screen.width, Screen.height) / 1080f, 0.75f, 1.35f);
            float radius = joystickRadius * scale; Vector2 defaultBase = new Vector2(120f * scale, Screen.height - 140f * scale);
            Vector2 basePos = movePointerId >= 0 ? new Vector2(moveStartScreen.x, Screen.height - moveStartScreen.y) : defaultBase;
            Vector2 knobPos = basePos + new Vector2(moveValue.x, -moveValue.y) * radius; Vector2 firePos = new Vector2(Screen.width - 120f * scale, Screen.height - 140f * scale); Vector2 grenadePos = new Vector2(Screen.width - 245f * scale, Screen.height - 245f * scale);
            DrawCircle(basePos, radius, new Color(0f, 0f, 0f, 0.34f)); DrawCircle(knobPos, radius * 0.42f, new Color(1f, 1f, 1f, 0.72f));
            DrawCircle(firePos, radius * 0.72f, new Color(0.65f, 0.12f, 0.08f, firePointerId >= 0 ? 0.50f : 0.28f)); DrawCircle(firePos, radius * 0.38f, new Color(1f, 1f, 1f, 0.60f));
            DrawCircle(grenadePos, radius * 0.56f, new Color(0.32f, 0.24f, 0.10f, grenadePointerId >= 0 ? 0.55f : 0.32f));
            if (buttonTextStyle == null) buttonTextStyle = new GUIStyle(GUI.skin.label) { fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter };
            buttonTextStyle.fontSize = Mathf.RoundToInt(20f * scale);
            GUI.Label(new Rect(grenadePos.x - radius * 0.5f, grenadePos.y - radius * 0.5f, radius, radius), "G", buttonTextStyle); GUI.Label(new Rect(firePos.x - radius, firePos.y + radius * 0.52f, radius * 2f, 26f * scale), "AIM / FIRE", buttonTextStyle); GUI.Label(new Rect(basePos.x - radius, basePos.y + radius * 0.52f, radius * 2f, 26f * scale), "MOVE", buttonTextStyle);
            DrawAimGuide(scale);
            if (minimapTexture != null) { float size = minimapSize * scale; Rect rect = new Rect(Screen.width - size - 18f * scale, 18f * scale, size, size); GUI.DrawTexture(rect, minimapTexture, ScaleMode.StretchToFill, false); GUI.Box(rect, GUIContent.none); }
        }

        private void DrawAndroidTouchHud()
        {
            float scale = Mathf.Clamp(Mathf.Min(Screen.width, Screen.height) / 1080f, 0.75f, 1.35f);
            if (circleTexture == null)
                CreateGuiTextures();

            float radius = joystickRadius * scale;
            Vector2 moveBase = movePointerId >= 0
                ? new Vector2(moveStartScreen.x, Screen.height - moveStartScreen.y)
                : new Vector2(120f * scale, Screen.height - 140f * scale);
            Vector2 moveKnob = moveBase + new Vector2(moveValue.x, -moveValue.y) * radius;

            DrawCircle(moveBase, radius, new Color(0f, 0f, 0f, 0.38f));
            DrawCircle(moveKnob, radius * 0.42f, new Color(0.92f, 0.90f, 0.78f, 0.82f));

            Vector2 firePos = new Vector2(Screen.width - 120f * scale, Screen.height - 155f * scale);
            Vector2 swordPos = new Vector2(Screen.width - 270f * scale, Screen.height - 85f * scale);
            Vector2 bombPos = new Vector2(Screen.width - 165f * scale, Screen.height - 85f * scale);
            Vector2 reloadPos = new Vector2(Screen.width - 60f * scale, Screen.height - 85f * scale);

            DrawAndroidButton(firePos, radius * 0.72f, "FIRE", firePointerId >= 0);
            DrawAndroidButton(swordPos, radius * 0.42f, "SWORD", swordPointerId >= 0);
            DrawAndroidButton(bombPos, radius * 0.42f, "BOMB", grenadePointerId >= 0);
            DrawAndroidButton(reloadPos, radius * 0.42f, "RELOAD", reloadPointerId >= 0);
        }

        private void DrawAndroidButton(Vector2 center, float radius, string label, bool pressed)
        {
            DrawCircle(center, radius, pressed
                ? new Color(0.45f, 0.50f, 0.25f, 0.82f)
                : new Color(0.10f, 0.12f, 0.14f, 0.68f));
            GUIStyle style = buttonTextStyle ?? new GUIStyle(GUI.skin.label);
            style.fontStyle = FontStyle.Bold;
            style.fontSize = Mathf.Max(11, Mathf.RoundToInt(radius * 0.22f));
            style.alignment = TextAnchor.MiddleCenter;
            GUI.Label(new Rect(center.x - radius, center.y - radius * 0.38f,
                radius * 2f, radius * 0.76f), label, style);
        }

        private void DrawAndroidRadar()
        {
            if (player == null) return;
            float scale = Mathf.Clamp(Mathf.Min(Screen.width, Screen.height) / 1080f, 0.75f, 1.35f); float size = 150f * scale; Rect radar = new Rect(Screen.width - size - 18f * scale, 18f * scale, size, size);
            Color old = GUI.color; GUI.color = new Color(0.02f, 0.05f, 0.06f, 0.58f); GUI.DrawTexture(radar, Texture2D.whiteTexture); GUI.color = new Color(0.70f, 0.86f, 0.42f, 0.95f); GUI.Box(radar, GUIContent.none);
            Vector2 center = radar.center; float radarWorldRadius = 68f; DrawRadarBlip(center, 8f * scale, new Color(0.98f, 0.82f, 0.20f, 1f));
            EnemyChase[] enemies = androidRadarEnemies;
            for (int i = 0; i < enemies.Length; i++) { EnemyChase enemy = enemies[i]; if (enemy == null) continue; Vector3 delta = enemy.transform.position - player.transform.position; delta.y = 0f; if (delta.magnitude > radarWorldRadius) continue; Vector2 p = center + new Vector2(delta.x / radarWorldRadius, delta.z / radarWorldRadius) * (size * 0.44f); DrawRadarBlip(p, 6f * scale, new Color(0.95f, 0.18f, 0.12f, 1f)); }
            GUI.color = old;
        }

        private void UpdateAndroidRadarCache()
        {
#if UNITY_ANDROID
            if (radarSpawner == null)
                radarSpawner = Object.FindFirstObjectByType<EnemySpawner>(FindObjectsInactive.Include);
            if (radarSpawner == null || !radarSpawner.enabled)
            {
                androidRadarEnemies = System.Array.Empty<EnemyChase>();
                return;
            }
            if (Time.unscaledTime < nextAndroidRadarScanTime) return;
            nextAndroidRadarScanTime = Time.unscaledTime + 0.25f;
            androidRadarEnemies = Object.FindObjectsByType<EnemyChase>(FindObjectsSortMode.None);
#endif
        }

        private void DrawRadarBlip(Vector2 center, float radius, Color color) { Color old = GUI.color; GUI.color = color; GUI.DrawTexture(new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f), Texture2D.whiteTexture); GUI.color = old; }

        private void DrawAimGuide(float scale)
        {
            if (player == null || player.Aim == null || player.Aim.CurrentTarget == null || lineTexture == null) return;
            Camera cam = gameplayCamera != null ? gameplayCamera : Camera.main; if (cam == null) return;
            Vector3 from = cam.WorldToScreenPoint(player.transform.position + Vector3.up * 0.8f); Vector3 to = cam.WorldToScreenPoint(player.Aim.CurrentTarget.transform.position + Vector3.up * 0.75f); if (from.z <= 0f || to.z <= 0f) return;
            from.y = Screen.height - from.y; to.y = Screen.height - to.y; DrawLine(from, to, Mathf.Max(2f, 3f * scale), new Color(1f, 0.85f, 0.2f, 0.55f)); DrawCircle(new Vector2(to.x, to.y), 18f * scale, new Color(1f, 0.18f, 0.10f, 0.32f));
        }

        private void DrawLine(Vector2 start, Vector2 end, float width, Color color)
        {
            Vector2 delta = end - start; float length = delta.magnitude; if (length <= 0.01f) return;
            Matrix4x4 oldMatrix = GUI.matrix; Color oldColor = GUI.color; GUI.color = color; GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, start); GUI.DrawTexture(new Rect(start.x, start.y - width * 0.5f, length, width), lineTexture); GUI.matrix = oldMatrix; GUI.color = oldColor;
        }

        private void DrawCircle(Vector2 center, float radius, Color color)
        {
            if (circleTexture == null) return; Color old = GUI.color; GUI.color = color; GUI.DrawTexture(new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f), circleTexture, ScaleMode.StretchToFill, true); GUI.color = old;
        }

        private static bool TryGetTouch(int fingerId, out Touch touch)
        {
            for (int i = 0; i < Input.touchCount; i++) { Touch current = Input.GetTouch(i); if (current.fingerId == fingerId) { touch = current; return true; } }
            touch = default; return false;
        }
    }
}