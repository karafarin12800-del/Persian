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
        private int meleePointerId = -1;
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
        private float minimapReadyAt;
        private GUIStyle buttonTextStyle;

        public Vector2 MoveValue => moveValue;

#if UNITY_ANDROID
        // Diagnostic gate: lets PrototypeFlow enable the component without executing
        // touch/UI code, so native activation can be isolated from input processing.
        private static bool androidExecutionArmed;
        public static void SetAndroidExecutionArmed(bool armed) => androidExecutionArmed = armed;
#endif

        public void SetGameplayCamera(Camera camera)
        {
            if (camera != null)
                gameplayCamera = camera;
        }

        public void EnableMinimap()
        {
#if UNITY_ANDROID
            // Create the minimap only after the main gameplay camera is stable.
            // PrototypeFlow calls this after the Android safe-frame window.
            minimapEnabled = true;
            minimapReadyAt = Time.unscaledTime + 1.5f;
            StartupCheckpoint.Set("MinimapEnabledAndroid");
#else
            minimapEnabled = true;
#endif
        }

        private void Awake()
        {
#if UNITY_ANDROID
            // Always keep the component alive; PrototypeFlow arms its internal execution
            // gate only after the match/camera path is stable.
            androidExecutionArmed = false;
#else
            if (player == null) player = FindFirstObjectByType<PlayerController>();
            if (gameplayCamera == null) gameplayCamera = Camera.main;
            if (player != null) grenadeController = player.Grenades;
#endif
            if (moveRadius > 0f) joystickRadius = Mathf.Clamp(moveRadius * 0.86f, 90f, 150f);
        }

        private void OnDestroy()
        {
            if (minimapTexture != null)
            {
                minimapTexture.Release();
                Destroy(minimapTexture);
            }
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

            // The minimap is a secondary GPU allocation. Keep it out of the first gameplay
            // frame and let PrototypeFlow enable it only after the match is visibly entered.
            if (minimapEnabled)
            {
                if (minimapCamera != null || Time.unscaledTime >= minimapReadyAt)
                    EnsureMinimap();
            }

#if UNITY_EDITOR || UNITY_STANDALONE
            Vector2 keyboard = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (keyboard.sqrMagnitude > 1f) keyboard.Normalize();
            player.SetMoveInput(keyboard);

            if (Input.GetKeyDown(KeyCode.R)) player.Weapon?.Reload();
            if (Input.GetKeyDown(KeyCode.Space)) player.Weapon?.TryMelee();
            if (Input.GetKeyDown(KeyCode.G)) ThrowGrenadeAtTarget();
            if (Input.GetMouseButton(0) && Time.time >= nextFireTime)
                FireAtNearestTarget();
#else
            HandleTouches();
#endif

            UpdateMinimap();
        }

        private void HandleTouches()
        {
            float scale = Mathf.Clamp(Mathf.Min(Screen.width, Screen.height) / 1080f, 0.75f, 1.35f);
            float radius = joystickRadius * scale;

            Vector2 joystickBaseGui = new Vector2(128f * scale, Screen.height - 142f * scale);
            Vector2 fireGui = new Vector2(Screen.width - 105f * scale, Screen.height - 128f * scale);
            Vector2 meleeGui = new Vector2(Screen.width - 225f * scale, Screen.height - 128f * scale);
            Vector2 grenadeGui = new Vector2(Screen.width - 225f * scale, Screen.height - 272f * scale);
            Vector2 reloadGui = new Vector2(Screen.width - 105f * scale, Screen.height - 272f * scale);

            float joystickHit = radius * 1.45f;
            float fireHit = radius * 0.68f;
            float meleeHit = radius * 0.50f;
            float grenadeHit = radius * 0.50f;
            float reloadHit = radius * 0.50f;

            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.phase != TouchPhase.Began) continue;

                Vector2 gui = new Vector2(touch.position.x, Screen.height - touch.position.y);

                // Movement owns only the visible lower-left joystick zone.
                if (movePointerId < 0 &&
                    gui.x <= Screen.width * 0.58f &&
                    gui.y >= Screen.height * 0.38f)
                {
                    movePointerId = touch.fingerId;
                    moveStartScreen = touch.position;
                    moveValue = Vector2.zero;
                    continue;
                }

                if (firePointerId < 0 && Vector2.Distance(gui, fireGui) <= fireHit)
                {
                    firePointerId = touch.fingerId;
                    FireAtNearestTarget();
                    continue;
                }

                if (meleePointerId < 0 && Vector2.Distance(gui, meleeGui) <= meleeHit)
                {
                    meleePointerId = touch.fingerId;
                    player.Weapon?.TryMelee();
                    continue;
                }

                if (grenadePointerId < 0 && Vector2.Distance(gui, grenadeGui) <= grenadeHit)
                {
                    grenadePointerId = touch.fingerId;
                    ThrowGrenadeAtTarget();
                    continue;
                }

                if (reloadPointerId < 0 && Vector2.Distance(gui, reloadGui) <= reloadHit)
                {
                    reloadPointerId = touch.fingerId;
                    player.Weapon?.Reload();
                }
            }

            if (movePointerId >= 0 && TryGetTouch(movePointerId, out Touch moveTouch))
            {
                Vector2 delta = moveTouch.position - moveStartScreen;
                float touchScale = Mathf.Clamp(Mathf.Min(Screen.width, Screen.height) / 1080f, 0.75f, 1.35f);
                float touchRadius = joystickRadius * touchScale;
                moveValue = Vector2.ClampMagnitude(delta / touchRadius, 1f);
                player.SetMoveInput(moveValue);

                if (moveTouch.phase == TouchPhase.Ended || moveTouch.phase == TouchPhase.Canceled)
                {
                    movePointerId = -1;
                    moveValue = Vector2.zero;
                    player.SetMoveInput(Vector2.zero);
                }
            }
            else if (movePointerId >= 0)
            {
                movePointerId = -1;
                moveValue = Vector2.zero;
                player.SetMoveInput(Vector2.zero);
            }
            else
            {
                player.SetMoveInput(Vector2.zero);
            }

            if (firePointerId >= 0 && TryGetTouch(firePointerId, out Touch fireTouch))
            {
                if (Time.time >= nextFireTime)
                    FireAtNearestTarget();

                if (fireTouch.phase == TouchPhase.Ended || fireTouch.phase == TouchPhase.Canceled)
                    firePointerId = -1;
            }
            else if (firePointerId >= 0)
            {
                firePointerId = -1;
            }

            if (grenadePointerId >= 0 && TryGetTouch(grenadePointerId, out Touch grenadeTouch))
            {
                if (grenadeTouch.phase == TouchPhase.Ended || grenadeTouch.phase == TouchPhase.Canceled)
                    grenadePointerId = -1;
            }
            else if (grenadePointerId >= 0)
            {
                grenadePointerId = -1;
            }

            ReleasePointer(ref meleePointerId);
            ReleasePointer(ref reloadPointerId);
        }

        private static void ReleasePointer(ref int pointerId)
        {
            if (pointerId < 0) return;
            if (!TryGetTouch(pointerId, out Touch touch) ||
                touch.phase == TouchPhase.Ended ||
                touch.phase == TouchPhase.Canceled)
                pointerId = -1;
        }

        private void FireAtNearestTarget()
        {
            if (player == null || player.IsDefeated || player.Aim == null) return;
            if (Time.time < nextFireTime) return;

            TargetHealth target = player.Aim.CurrentTarget;
            if (target != null && player.Aim.FireAt(target.transform.position))
            {
                nextFireTime = Time.time + fireRepeatInterval;
            }
            else if (player.Weapon != null && player.Weapon.Magazine <= 0)
            {
                player.Weapon.Reload();
            }
        }

        private void ThrowGrenadeAtTarget()
        {
            if (player == null || player.IsDefeated) return;
            if (grenadeController == null) grenadeController = player.Grenades;
            if (grenadeController == null || grenadeController.Grenades <= 0) return;

            Vector3 direction = player.transform.forward;
            TargetHealth target = player.Aim != null ? player.Aim.CurrentTarget : null;
            if (target != null)
            {
                Vector3 delta = target.transform.position - player.transform.position;
                delta.y = 0f;
                if (delta.sqrMagnitude > 0.001f)
                    direction = delta.normalized;
            }

            grenadeController.Throw(new Vector2(direction.x, direction.z));
        }

        private void EnsureMinimap()
        {
            if (minimapCamera != null && minimapTexture != null)
                return;

            CreateMinimap();
        }

        private void CreateMinimap()
        {
            if (minimapCamera != null || minimapTexture != null)
                return;

            GameObject mapObject = new GameObject("MinimapCamera");
            mapObject.hideFlags = HideFlags.HideAndDontSave;
            minimapCamera = mapObject.AddComponent<Camera>();
            minimapCamera.orthographic = true;
            minimapCamera.orthographicSize = 96f;
            minimapCamera.nearClipPlane = 0.1f;
            minimapCamera.farClipPlane = 260f;
            minimapCamera.allowHDR = false;
            minimapCamera.allowMSAA = false;
            minimapCamera.clearFlags = CameraClearFlags.SolidColor;
            minimapCamera.backgroundColor = new Color(0.06f, 0.07f, 0.08f, 1f);
            minimapCamera.enabled = true;

            minimapTexture = new RenderTexture(256, 256, 16, RenderTextureFormat.ARGB32);
            minimapTexture.name = "GameplayMinimap";
            minimapTexture.filterMode = FilterMode.Bilinear;
            minimapTexture.Create();
            minimapCamera.targetTexture = minimapTexture;
        }

        private void CreateGuiTextures()
        {
            circleTexture = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            lineTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            lineTexture.SetPixel(0, 0, Color.white);
            lineTexture.Apply();

            Vector2 center = new Vector2(31.5f, 31.5f);
            float radius = 31f;
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    float alpha = Mathf.Clamp01(radius + 0.5f - distance);
                    circleTexture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }
            circleTexture.Apply();
        }

        private void UpdateMinimap()
        {
            if (minimapCamera == null || player == null) return;
            minimapCamera.transform.position = player.transform.position + Vector3.up * 120f;
            minimapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }

        private void OnGUI()
        {
            if (!Application.isMobilePlatform && !Application.isEditor) return;
#if UNITY_ANDROID
            if (!androidExecutionArmed) return;
#endif

            if (circleTexture == null || lineTexture == null)
                CreateGuiTextures();

            float scale = Mathf.Clamp(Mathf.Min(Screen.width, Screen.height) / 1080f, 0.75f, 1.35f);
            float radius = joystickRadius * scale;
            Vector2 defaultBase = new Vector2(120f * scale, Screen.height - 140f * scale);
            bool showFloatingJoystick = movePointerId >= 0;
            Vector2 basePos = showFloatingJoystick
                ? new Vector2(moveStartScreen.x, Screen.height - moveStartScreen.y)
                : defaultBase;
            Vector2 knobPos = basePos + new Vector2(moveValue.x, -moveValue.y) * radius;
            Vector2 firePos = new Vector2(Screen.width - 118f * scale, Screen.height - 142f * scale);
            Vector2 meleePos = new Vector2(Screen.width - 235f * scale, Screen.height - 92f * scale);
            Vector2 grenadePos = new Vector2(Screen.width - 235f * scale, Screen.height - 222f * scale);
            Vector2 reloadPos = new Vector2(Screen.width - 115f * scale, Screen.height - 260f * scale);

            if (showFloatingJoystick)
            {
                DrawCircle(basePos, radius, new Color(0f, 0f, 0f, 0.42f));
                DrawCircle(knobPos, radius * 0.42f, new Color(0.92f, 0.95f, 0.98f, 0.78f));
            }

            DrawCircle(firePos, radius * 0.64f, new Color(0.72f, 0.12f, 0.08f, firePointerId >= 0 ? 0.62f : 0.42f));
            DrawCircle(firePos, radius * 0.38f, new Color(1f, 0.92f, 0.72f, 0.72f));

            DrawCircle(meleePos, radius * 0.48f, new Color(0.16f, 0.18f, 0.22f, meleePointerId >= 0 ? 0.72f : 0.52f));
            DrawCircle(grenadePos, radius * 0.48f, new Color(0.18f, 0.40f, 0.16f, grenadePointerId >= 0 ? 0.68f : 0.48f));
            DrawCircle(reloadPos, radius * 0.46f, new Color(0.16f, 0.22f, 0.32f, reloadPointerId >= 0 ? 0.72f : 0.48f));
            if (buttonTextStyle == null)
            {
                buttonTextStyle = new GUIStyle(GUI.skin.label)
                {
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
            }
            buttonTextStyle.fontSize = Mathf.RoundToInt(20f * scale);

            GUI.Label(new Rect(grenadePos.x - radius * 0.5f, grenadePos.y - radius * 0.5f, radius, radius), "G", buttonTextStyle);
            GUI.Label(new Rect(meleePos.x - radius * 0.5f, meleePos.y - radius * 0.5f, radius, radius), "FIST", buttonTextStyle);
            GUI.Label(new Rect(reloadPos.x - radius * 0.5f, reloadPos.y - radius * 0.5f, radius, radius), "R", buttonTextStyle);
            GUI.Label(new Rect(firePos.x - radius, firePos.y + radius * 0.52f, radius * 2f, 26f * scale), "FIRE", buttonTextStyle);
            GUI.Label(new Rect(basePos.x - radius, basePos.y + radius * 0.52f, radius * 2f, 26f * scale), "MOVE", buttonTextStyle);

            DrawAimGuide(scale);

            if (minimapTexture != null)
            {
                float size = Mathf.Clamp(minimapSize * scale, 170f, 220f);
                Rect rect = new Rect(Screen.width - size - 16f * scale, 14f * scale, size, size);
                GUI.DrawTexture(rect, minimapTexture, ScaleMode.StretchToFill, false);
                Color oldMap = GUI.color;
                GUI.color = new Color(0.95f, 0.72f, 0.20f, 0.95f);
                GUI.Box(rect, GUIContent.none);
                GUI.color = oldMap;
                GUI.Label(new Rect(rect.x + 7f, rect.y + 5f, 70f, 24f), "MAP", buttonTextStyle);
            }
        }

        private void DrawAndroidControl(Rect rect, string label, Color fill)
        {
            Color old = GUI.color;
            GUI.color = fill;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = new Color(0.88f, 0.66f, 0.20f, 0.90f);
            GUI.DrawTexture(new Rect(rect.x, rect.y, rect.width, 3f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(rect, label, new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(18f * Mathf.Clamp(Mathf.Min(Screen.width, Screen.height) / 1080f, 0.75f, 1.35f)),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            });
            GUI.color = old;
        }

        private void DrawAimGuide(float scale)
        {
            if (player == null || player.Aim == null || player.Aim.CurrentTarget == null || lineTexture == null)
                return;

            Camera cam = gameplayCamera != null ? gameplayCamera : Camera.main;
            if (cam == null) return;

            Vector3 from = cam.WorldToScreenPoint(player.transform.position + Vector3.up * 0.8f);
            Vector3 to = cam.WorldToScreenPoint(player.Aim.CurrentTarget.transform.position + Vector3.up * 0.75f);
            if (from.z <= 0f || to.z <= 0f) return;
            from.y = Screen.height - from.y;
            to.y = Screen.height - to.y;

            DrawLine(from, to, Mathf.Max(2f, 3f * scale), new Color(1f, 0.85f, 0.2f, 0.55f));
            float targetSize = 18f * scale;
            DrawCircle(new Vector2(to.x, to.y), targetSize, new Color(1f, 0.18f, 0.10f, 0.32f));
        }

        private void DrawLine(Vector2 start, Vector2 end, float width, Color color)
        {
            Vector2 delta = end - start;
            float length = delta.magnitude;
            if (length <= 0.01f) return;

            Matrix4x4 oldMatrix = GUI.matrix;
            Color oldColor = GUI.color;
            GUI.color = color;
            GUIUtility.RotateAroundPivot(Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg, start);
            GUI.DrawTexture(new Rect(start.x, start.y - width * 0.5f, length, width), lineTexture);
            GUI.matrix = oldMatrix;
            GUI.color = oldColor;
        }

        private void DrawCircle(Vector2 center, float radius, Color color)
        {
            if (circleTexture == null) return;
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f), circleTexture, ScaleMode.StretchToFill, true);
            GUI.color = old;
        }

        private static bool TryGetTouch(int fingerId, out Touch touch)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch current = Input.GetTouch(i);
                if (current.fingerId == fingerId)
                {
                    touch = current;
                    return true;
                }
            }

            touch = default;
            return false;
        }
    }
}
