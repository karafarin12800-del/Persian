using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    /// <summary>
    /// Single owner of mobile touch input for the Android match.
    /// Movement and combat actions are intentionally kept in one touch dispatcher
    /// so a second joystick reader cannot overwrite the movement vector or steal
    /// multi-touch actions.
    /// </summary>
    public sealed class MobileInputHub : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [SerializeField] private float joystickRadius = 122f;
        [SerializeField] private float minimapSize = 190f;
        [SerializeField] private float fireRepeatInterval = 0.155f;
        [SerializeField] private Camera gameplayCamera;
        [SerializeField] private float moveRadius = 140f;
        [SerializeField] private float controlScale = 1.12f;
        [SerializeField] private float actionBottomMargin = 74f;

        private int movePointerId = -1;
        private int firePointerId = -1;
        private int meleePointerId = -1;
        private int grenadePointerId = -1;
        private int reloadPointerId = -1;
        private float reloadPressedAt;
        private bool reloadHeldToSwap;

        private Vector2 moveStartScreen;
        private Vector2 moveValue;

        private float nextFireTime;
        private GrenadeController grenadeController;

        private Camera minimapCamera;
        private RenderTexture minimapTexture;
        private Texture2D circleTexture;
        private GUIStyle buttonTextStyle;
        private GUIStyle smallButtonHintStyle;

        private bool minimapEnabled;
        private float minimapReadyAt;
        private bool matchActive;
        private bool matchPaused;
        private bool movementTouchCheckpointWritten;
        private bool fireTouchCheckpointWritten;

        // IMGUI fallback maps concurrent touchscreen contacts to mouse buttons.
        // Keep a distinct owner per simulated button so movement cannot consume fire.
        private int guiMoveMouseButton = -1;
        private int guiFireMouseButton = -1;
        private int guiMeleeMouseButton = -1;
        private int guiGrenadeMouseButton = -1;
        private int guiReloadMouseButton = -1;

        public Vector2 MoveValue => moveValue;
        public bool IsMovementTouchActive => movePointerId >= 0;
        public bool IsMatchActive => matchActive && !matchPaused;

        public void Configure(PlayerController target, Camera camera)
        {
            player = target;
            if (camera != null)
                gameplayCamera = camera;
            if (player != null)
                grenadeController = player.Grenades;
        }

        public void SetGameplayCamera(Camera camera)
        {
            if (camera != null)
                gameplayCamera = camera;
        }

        public void ActivateForMatch(PlayerController target, Camera camera)
        {
            Configure(target, camera);
            matchActive = true;
            matchPaused = false;
            ResetAllPointers();
            movementTouchCheckpointWritten = false;
            fireTouchCheckpointWritten = false;
#if UNITY_ANDROID
            Input.multiTouchEnabled = true;
            Input.simulateMouseWithTouches = true;
            // Keep synthesized mouse events available for IMGUI menu/pause controls.
            // During gameplay the fallback dispatcher defers to real touch IDs.
#endif
            enabled = true;
        }

        public void DeactivateMatch()
        {
#if UNITY_ANDROID
            // Re-enable touch-to-mouse synthesis for menu/pause IMGUI after gameplay.
            Input.simulateMouseWithTouches = true;
#endif
            matchActive = false;
            matchPaused = false;
            ResetAllPointers();
        }

        public void SetPaused(bool paused)
        {
            matchPaused = paused;
            if (paused)
                ResetAllPointers();
        }

        public void EnableMinimap()
        {
            minimapEnabled = true;
#if UNITY_ANDROID
            minimapReadyAt = Time.unscaledTime + 1.5f;
            StartupCheckpoint.Set("MinimapEnabledAndroid");
#else
            StartupCheckpoint.Set("MinimapEnabled");
#endif
        }

        private void Awake()
        {
#if UNITY_ANDROID
            // Keep IMGUI menu/pause controls clickable from the very first frame.
            // Gameplay prefers real finger IDs and guards its fallback dispatcher.
            Input.multiTouchEnabled = true;
            Input.simulateMouseWithTouches = true;
#endif
            if (moveRadius > 0f)
                joystickRadius = Mathf.Clamp(moveRadius * 0.98f, 108f, 164f);

            if (player == null)
                player = FindFirstObjectByType<PlayerController>();
            if (gameplayCamera == null)
                gameplayCamera = Camera.main;
            if (player != null)
                grenadeController = player.Grenades;
        }

        private void OnDestroy()
        {
            if (minimapTexture != null)
            {
                minimapTexture.Release();
                Destroy(minimapTexture);
            }

            if (minimapCamera != null)
                Destroy(minimapCamera.gameObject);

            if (circleTexture != null)
                Destroy(circleTexture);

        }

        private void Update()
        {
if (!matchActive || matchPaused)
                return;

            if (player == null)
            {
                player = FindFirstObjectByType<PlayerController>();
                if (player == null)
                    return;
                grenadeController = player.Grenades;
            }

#if UNITY_EDITOR || UNITY_STANDALONE
            Vector2 keyboard = new Vector2(
                Input.GetAxisRaw("Horizontal"),
                Input.GetAxisRaw("Vertical"));

            if (keyboard.sqrMagnitude > 1f)
                keyboard.Normalize();

            player.SetMoveInput(keyboard);

            if (Input.GetKeyDown(KeyCode.R))
                player.Weapon?.Reload();
            if (Input.GetKeyDown(KeyCode.Q))
                player.Weapon?.CycleWeapon();

            if (Input.GetKeyDown(KeyCode.Space))
                player.Weapon?.TryMelee();

            if (Input.GetKeyDown(KeyCode.G))
                ThrowGrenadeAtTarget();

            if (Input.GetMouseButton(0) && Time.time >= nextFireTime)
                FireAtNearestTarget();
#else
            HandleAndroidTouches();
#endif

            if (minimapEnabled)
            {
                if (minimapCamera != null || Time.unscaledTime >= minimapReadyAt)
                    EnsureMinimap();
            }

            UpdateMinimap();
        }

        private void HandleAndroidTouches()
        {
            float scale = GetUiScale();
            float radius = joystickRadius * scale * controlScale;

            GetActionCenters(
                scale,
                out Vector2 fireGui,
                out Vector2 meleeGui,
                out Vector2 grenadeGui,
                out Vector2 reloadGui);

            float fireHit = radius * 0.86f;
            float meleeHit = radius * 0.76f;
            float grenadeHit = radius * 0.76f;
            float reloadHit = radius * 0.72f;

            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.phase != TouchPhase.Began)
                    continue;

                Vector2 gui = new Vector2(
                    touch.position.x,
                    Screen.height - touch.position.y);

                // Combat buttons get first priority so an oversized touch zone can
                // never accidentally become a movement touch.
                if (firePointerId == -1 &&
                    Vector2.Distance(gui, fireGui) <= fireHit)
                {
                    firePointerId = touch.fingerId;
                    if (!fireTouchCheckpointWritten)
                    {
                        fireTouchCheckpointWritten = true;
                        StartupCheckpoint.Set("MobileFireTouchAccepted");
                    }
                    FireAtNearestTarget();
                    continue;
                }

                if (meleePointerId == -1 &&
                    Vector2.Distance(gui, meleeGui) <= meleeHit)
                {
                    meleePointerId = touch.fingerId;
                    player.Weapon?.TryMelee();
                    continue;
                }

                if (grenadePointerId == -1 &&
                    Vector2.Distance(gui, grenadeGui) <= grenadeHit)
                {
                    grenadePointerId = touch.fingerId;
                    ThrowGrenadeAtTarget();
                    continue;
                }

                if (reloadPointerId == -1 &&
                    Vector2.Distance(gui, reloadGui) <= reloadHit)
                {
                    // Tap to reload; hold for 0.55 seconds to cycle weapon profile.
                    reloadPointerId = touch.fingerId;
                    reloadPressedAt = Time.time;
                    reloadHeldToSwap = false;
                    continue;
                }

                // Everything else on the left side becomes the floating movement
                // stick. The origin follows the player's first touch.
                if (touch.position.x <= Screen.width * 0.58f &&
                    gui.y >= Screen.height * 0.10f)
                {
                    if (movePointerId == -1)
                    {
                        movePointerId = touch.fingerId;
                        moveStartScreen = ClampFloatingOrigin(touch.position, radius);
                        moveValue = Vector2.zero;
                        if (!movementTouchCheckpointWritten)
                        {
                            movementTouchCheckpointWritten = true;
                            StartupCheckpoint.Set("MobileMoveTouchAccepted");
                        }
                    }
                }
            }

            if (movePointerId >= 0)
            {
                if (TryGetTouch(movePointerId, out Touch movementTouch))
                {
                    Vector2 delta = movementTouch.position - moveStartScreen;
                    float touchRadius = Mathf.Max(1f, radius);
                    moveValue = Vector2.ClampMagnitude(delta / touchRadius, 1f);

                    Vector2 movement = ApplyDeadZone(moveValue);
                    player.SetMoveInput(ToWorldMove(movement));

                    if (movementTouch.phase == TouchPhase.Ended ||
                        movementTouch.phase == TouchPhase.Canceled)
                    {
                        ResetMovementPointer();
                    }
                }
                else
                {
                    ResetMovementPointer();
                }
            }
            else if (movePointerId != -1000)
            {
                // Do not erase movement supplied by the IMGUI fallback pointer.
                player.SetMoveInput(Vector2.zero);
            }

            if (firePointerId >= 0)
            {
                if (TryGetTouch(firePointerId, out Touch fireTouch))
                {
                    if ((fireTouch.phase == TouchPhase.Moved ||
                         fireTouch.phase == TouchPhase.Stationary) &&
                        Time.time >= nextFireTime)
                    {
                        FireAtNearestTarget();
                    }

                    if (fireTouch.phase == TouchPhase.Ended ||
                        fireTouch.phase == TouchPhase.Canceled)
                    {
                        firePointerId = -1;
                    }
                }
                else
                {
                    firePointerId = -1;
                }
            }

            if (reloadPointerId >= 0)
            {
                if (TryGetTouch(reloadPointerId, out Touch reloadTouch))
                {
                    if (!reloadHeldToSwap && Time.time - reloadPressedAt >= 0.55f)
                    {
                        player.Weapon?.CycleWeapon();
                        reloadHeldToSwap = true;
                    }

                    if (reloadTouch.phase == TouchPhase.Ended ||
                        reloadTouch.phase == TouchPhase.Canceled)
                    {
                        if (!reloadHeldToSwap)
                            player.Weapon?.Reload();
                        reloadPointerId = -1;
                        reloadHeldToSwap = false;
                        reloadPressedAt = 0f;
                    }
                }
                else
                {
                    if (!reloadHeldToSwap)
                        player.Weapon?.Reload();
                    reloadPointerId = -1;
                    reloadHeldToSwap = false;
                    reloadPressedAt = 0f;
                }
            }

            ReleasePointer(ref meleePointerId);
            ReleasePointer(ref grenadePointerId);
        }

        private Vector2 ApplyDeadZone(Vector2 inputValue)
        {
            float magnitude = inputValue.magnitude;
            if (magnitude <= 0.08f)
                return Vector2.zero;

            float normalized = Mathf.InverseLerp(0.08f, 1f, magnitude);
            return inputValue.normalized * normalized;
        }

        private Vector2 ToWorldMove(Vector2 inputValue)
        {
            Camera camera = gameplayCamera != null ? gameplayCamera : Camera.main;
            if (camera == null)
                return inputValue;

            Vector3 forward = camera.transform.forward;
            Vector3 right = camera.transform.right;
            forward.y = 0f;
            right.y = 0f;

            if (forward.sqrMagnitude < 0.0001f ||
                right.sqrMagnitude < 0.0001f)
            {
                return inputValue;
            }

            forward.Normalize();
            right.Normalize();

            Vector3 world = right * inputValue.x + forward * inputValue.y;
            return Vector2.ClampMagnitude(
                new Vector2(world.x, world.z),
                1f);
        }

        private void FireAtNearestTarget()
        {
            if (player == null ||
                player.IsDefeated ||
                player.Weapon == null)
            {
                return;
            }

            if (Time.time < nextFireTime)
                return;

            TargetHealth target = player.Aim != null
                ? player.Aim.CurrentTarget
                : null;

            bool fired = target != null
                ? player.Weapon.TryFire(target.transform.position)
                : player.Weapon.TryFireDirection(player.transform.forward);

            if (fired)
            {
                nextFireTime = Time.time + fireRepeatInterval;
            }
            else if (player.Weapon.Magazine <= 0)
            {
                player.Weapon.Reload();
            }
        }

        private void ThrowGrenadeAtTarget()
        {
            if (player == null || player.IsDefeated)
                return;

            if (grenadeController == null)
                grenadeController = player.Grenades;

            if (grenadeController == null ||
                grenadeController.Grenades <= 0)
            {
                return;
            }

            Vector3 direction = player.transform.forward;
            TargetHealth target = player.Aim != null
                ? player.Aim.CurrentTarget
                : null;

            if (target != null)
            {
                Vector3 delta = target.transform.position - player.transform.position;
                delta.y = 0f;
                if (delta.sqrMagnitude > 0.001f)
                    direction = delta.normalized;
            }

            grenadeController.Throw(
                new Vector2(direction.x, direction.z));
        }

        private void GetActionCenters(
            float scale,
            out Vector2 fire,
            out Vector2 melee,
            out Vector2 grenade,
            out Vector2 reload)
        {
            float bottom = actionBottomMargin * scale;

            // Keep combat buttons clearly separated so their touch zones
            // and visuals cannot feel like one combined control cluster.
            fire = new Vector2(
                Screen.width - 104f * scale,
                Screen.height - bottom - 58f * scale);

            melee = new Vector2(
                Screen.width - 342f * scale,
                Screen.height - bottom - 18f * scale);

            grenade = new Vector2(
                Screen.width - 342f * scale,
                Screen.height - bottom - 182f * scale);

            reload = new Vector2(
                Screen.width - 104f * scale,
                Screen.height - bottom - 210f * scale);
        }

        private float GetUiScale()
        {
            return Mathf.Clamp(
                Mathf.Min(Screen.width, Screen.height) / 1080f,
                0.75f,
                1.35f);
        }

        private static Vector2 ClampFloatingOrigin(Vector2 touchPosition, float radius)
        {
            float margin = radius * 0.70f;

            return new Vector2(
                Mathf.Clamp(
                    touchPosition.x,
                    margin,
                    Screen.width * 0.58f - margin),
                Mathf.Clamp(
                    touchPosition.y,
                    margin,
                    Screen.height - margin));
        }

        private void ResetMovementPointer()
        {
            movePointerId = -1;
            guiMoveMouseButton = -1;
            moveStartScreen = Vector2.zero;
            moveValue = Vector2.zero;

            if (player != null)
                player.SetMoveInput(Vector2.zero);
        }

        private void ResetAllPointers()
        {
            movePointerId = -1;
            firePointerId = -1;
            meleePointerId = -1;
            grenadePointerId = -1;
            reloadPointerId = -1;
            guiMoveMouseButton = -1;
            guiFireMouseButton = -1;
            guiMeleeMouseButton = -1;
            guiGrenadeMouseButton = -1;
            guiReloadMouseButton = -1;
            reloadPressedAt = 0f;
            reloadHeldToSwap = false;
            moveStartScreen = Vector2.zero;
            moveValue = Vector2.zero;

            if (player != null)
                player.SetMoveInput(Vector2.zero);
        }

        private static void ReleasePointer(ref int pointerId)
        {
            if (pointerId < 0)
                return;

            if (!TryGetTouch(pointerId, out Touch touch) ||
                touch.phase == TouchPhase.Ended ||
                touch.phase == TouchPhase.Canceled)
            {
                pointerId = -1;
            }
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

            minimapTexture = new RenderTexture(
                256,
                256,
                16,
                RenderTextureFormat.ARGB32);
            minimapTexture.name = "GameplayMinimap";
            minimapTexture.filterMode = FilterMode.Bilinear;
            minimapTexture.Create();
            minimapCamera.targetTexture = minimapTexture;
            minimapCamera.enabled = true;
        }

        private void UpdateMinimap()
        {
            if (minimapCamera == null || player == null)
                return;

            minimapCamera.transform.position =
                player.transform.position + Vector3.up * 120f;
            minimapCamera.transform.rotation =
                Quaternion.Euler(90f, 0f, 0f);
        }

        private void OnGUI()
        {
            if (!Application.isMobilePlatform && !Application.isEditor)
                return;

            if (!matchActive || matchPaused)
                return;

            HandleGuiPointerFallback();

            if (circleTexture == null)
                CreateGuiTextures();

            float scale = GetUiScale();
            float radius = joystickRadius * scale * controlScale;

            GetActionCenters(
                scale,
                out Vector2 firePos,
                out Vector2 meleePos,
                out Vector2 grenadePos,
                out Vector2 reloadPos);

            // Floating joystick: nothing is fixed on the screen. The control
            // appears at the exact touch origin and remains visible while held.
            if (movePointerId >= 0)
            {
                Vector2 basePos = new Vector2(
                    moveStartScreen.x,
                    Screen.height - moveStartScreen.y);

                Vector2 knobPos = basePos +
                    new Vector2(moveValue.x, -moveValue.y) * radius;

                // Stronger layered visuals make the floating control readable
                // even over bright city terrain.
                DrawCircle(
                    basePos,
                    radius * 1.06f,
                    new Color(0f, 0f, 0f, 0.62f));

                DrawCircle(
                    basePos,
                    radius,
                    new Color(0.95f, 0.72f, 0.20f, 0.42f));

                DrawCircle(
                    basePos,
                    radius * 0.92f,
                    new Color(0.05f, 0.07f, 0.10f, 0.38f));

                DrawCircle(
                    knobPos,
                    radius * 0.43f,
                    new Color(0.98f, 0.98f, 1f, 0.96f));

                DrawCircle(
                    knobPos,
                    radius * 0.29f,
                    new Color(0.95f, 0.72f, 0.20f, 0.78f));

                if (buttonTextStyle == null)
                {
                    buttonTextStyle = new GUIStyle(GUI.skin.label)
                    {
                        fontStyle = FontStyle.Bold,
                        alignment = TextAnchor.MiddleCenter
                    };
                }

                buttonTextStyle.fontSize = Mathf.RoundToInt(18f * scale);
                buttonTextStyle.normal.textColor = Color.white;

                GUI.Label(
                    new Rect(
                        basePos.x - radius,
                        basePos.y + radius * 0.62f,
                        radius * 2f,
                        24f * scale),
                    "MOVE",
                    buttonTextStyle);
            }
            else
            {
                // No fixed joystick is shown; only a subtle hint marks the
                // left movement area until the player touches it.
                Vector2 hint = new Vector2(
                    92f * scale,
                    Screen.height - 88f * scale);

                DrawCircle(
                    hint,
                    radius * 0.46f,
                    new Color(0f, 0f, 0f, 0.16f));

                buttonTextStyle ??= new GUIStyle(GUI.skin.label)
                {
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };

                buttonTextStyle.fontSize = Mathf.RoundToInt(13f * scale);
                buttonTextStyle.normal.textColor = new Color(1f, 1f, 1f, 0.58f);

                GUI.Label(
                    new Rect(
                        hint.x - radius,
                        hint.y - 12f * scale,
                        radius * 2f,
                        24f * scale),
                    "MOVE",
                    buttonTextStyle);
            }

            DrawCircle(
                firePos,
                radius * 0.76f,
                new Color(0.72f, 0.12f, 0.08f,
                    firePointerId >= 0 ? 0.70f : 0.48f));

            DrawCircle(
                meleePos,
                radius * 0.58f,
                new Color(0.16f, 0.18f, 0.22f,
                    meleePointerId >= 0 ? 0.78f : 0.56f));

            DrawCircle(
                grenadePos,
                radius * 0.58f,
                new Color(0.18f, 0.40f, 0.16f,
                    grenadePointerId >= 0 ? 0.72f : 0.52f));

            DrawCircle(
                reloadPos,
                radius * 0.56f,
                new Color(0.16f, 0.22f, 0.32f,
                    reloadPointerId >= 0 ? 0.76f : 0.54f));

            if (buttonTextStyle == null)
            {
                buttonTextStyle = new GUIStyle(GUI.skin.label)
                {
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                };
            }

            buttonTextStyle.fontSize =
                Mathf.RoundToInt(21f * scale);
            buttonTextStyle.normal.textColor = Color.white;

            smallButtonHintStyle ??= new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            smallButtonHintStyle.fontSize = Mathf.Clamp(Mathf.RoundToInt(10f * scale), 8, 11);
            smallButtonHintStyle.normal.textColor = new Color(1f, 0.93f, 0.62f, 0.95f);

            GUI.Label(
                new Rect(
                    firePos.x - radius,
                    firePos.y - radius * 0.30f,
                    radius * 2f,
                    radius * 0.60f),
                "FIRE",
                buttonTextStyle);

            GUI.Label(
                new Rect(
                    meleePos.x - radius * 0.60f,
                    meleePos.y - radius * 0.40f,
                    radius * 1.20f,
                    radius * 0.80f),
                "FIST",
                buttonTextStyle);

            GUI.Label(
                new Rect(
                    grenadePos.x - radius * 0.60f,
                    grenadePos.y - radius * 0.40f,
                    radius * 1.20f,
                    radius * 0.80f),
                "G",
                buttonTextStyle);

            GUI.Label(
                new Rect(
                    reloadPos.x - radius * 0.55f,
                    reloadPos.y - radius * 0.40f,
                    radius * 1.10f,
                    radius * 0.80f),
                "R",
                buttonTextStyle);
            GUI.Label(
                new Rect(
                    reloadPos.x - radius * 0.72f,
                    reloadPos.y + radius * 0.43f,
                    radius * 1.44f,
                    16f * scale),
                "HOLD SWAP",
                smallButtonHintStyle);

            if (minimapTexture != null)
            {
                float size = Mathf.Clamp(
                    minimapSize * scale,
                    170f,
                    220f);

                Rect rect = new Rect(
                    Screen.width - size - 16f * scale,
                    14f * scale,
                    size,
                    size);

                GUI.DrawTexture(
                    rect,
                    minimapTexture,
                    ScaleMode.StretchToFill,
                    false);

                Color oldMap = GUI.color;
                GUI.color = new Color(
                    0.95f, 0.72f, 0.20f, 0.95f);

                GUI.Box(rect, GUIContent.none);
                GUI.color = oldMap;

                GUI.Label(
                    new Rect(
                        rect.x + 7f,
                        rect.y + 5f,
                        70f,
                        24f),
                    "MAP",
                    buttonTextStyle);
            }
        }

        private void HandleGuiPointerFallback()
        {
            Event e = Event.current;
            if (e == null)
                return;

            // Use the real Touch.fingerId path whenever Unity exposes live touches.
            // If we already own an IMGUI fallback pointer, keep the fallback active
            // until that simulated contact has been released.
            bool guiFallbackOwnsPointer =
                movePointerId == -1000 ||
                firePointerId == -1001 ||
                meleePointerId == -1002 ||
                grenadePointerId == -1003 ||
                reloadPointerId == -1004;

            if (Application.isMobilePlatform &&
                Input.touchCount > 0 &&
                !guiFallbackOwnsPointer)
            {
                return;
            }

            if (e.type != EventType.MouseDown &&
                e.type != EventType.MouseDrag &&
                e.type != EventType.MouseUp)
            {
                return;
            }

            Vector2 gui = e.mousePosition;
            float scale = GetUiScale();
            float radius = joystickRadius * scale * controlScale;

            GetActionCenters(
                scale,
                out Vector2 firePos,
                out Vector2 meleePos,
                out Vector2 grenadePos,
                out Vector2 reloadPos);

            if (e.type == EventType.MouseDown)
            {
                int mouseButton = e.button;
                if (mouseButton < 0 || mouseButton > 2)
                    return;

                // A control is free only when its logical pointer AND simulated
                // mouse-button owner are free. Negative pointer sentinels are active.
                if (firePointerId == -1 &&
                    guiFireMouseButton == -1 &&
                    Vector2.Distance(gui, firePos) <= radius * 0.86f)
                {
                    firePointerId = -1001;
                    guiFireMouseButton = mouseButton;
                    FireAtNearestTarget();
                    e.Use();
                    return;
                }

                if (meleePointerId == -1 &&
                    guiMeleeMouseButton == -1 &&
                    Vector2.Distance(gui, meleePos) <= radius * 0.76f)
                {
                    meleePointerId = -1002;
                    guiMeleeMouseButton = mouseButton;
                    player.Weapon?.TryMelee();
                    e.Use();
                    return;
                }

                if (grenadePointerId == -1 &&
                    guiGrenadeMouseButton == -1 &&
                    Vector2.Distance(gui, grenadePos) <= radius * 0.76f)
                {
                    grenadePointerId = -1003;
                    guiGrenadeMouseButton = mouseButton;
                    ThrowGrenadeAtTarget();
                    e.Use();
                    return;
                }

                if (reloadPointerId == -1 &&
                    guiReloadMouseButton == -1 &&
                    Vector2.Distance(gui, reloadPos) <= radius * 0.72f)
                {
                    reloadPointerId = -1004;
                    guiReloadMouseButton = mouseButton;
                    reloadPressedAt = Time.time;
                    reloadHeldToSwap = false;
                    e.Use();
                    return;
                }

                if (movePointerId == -1 &&
                    guiMoveMouseButton == -1 &&
                    gui.x <= Screen.width * 0.58f &&
                    gui.y >= Screen.height * 0.10f)
                {
                    movePointerId = -1000;
                    guiMoveMouseButton = mouseButton;
                    moveStartScreen = ClampFloatingOrigin(
                        new Vector2(gui.x, Screen.height - gui.y),
                        radius);
                    moveValue = Vector2.zero;
                    e.Use();
                    return;
                }

                // This touch did not start a gameplay control. Let other IMGUI
                // controls (notably pause/menu) process it normally.
                return;
            }

            if (e.type == EventType.MouseDrag)
            {
                // Never route every drag to movement. Each simulated finger keeps
                // the mouse button captured when that finger first touched down.
                if (movePointerId == -1000 &&
                    guiMoveMouseButton == e.button)
                {
                    Vector2 current = new Vector2(
                        gui.x,
                        Screen.height - gui.y);
                    Vector2 delta = current - moveStartScreen;
                    moveValue = Vector2.ClampMagnitude(
                        delta / Mathf.Max(1f, radius),
                        1f);
                    player.SetMoveInput(ToWorldMove(ApplyDeadZone(moveValue)));
                    e.Use();
                    return;
                }

                if (firePointerId == -1001 &&
                    guiFireMouseButton == e.button)
                {
                    if (Time.time >= nextFireTime)
                        FireAtNearestTarget();
                    e.Use();
                    return;
                }

                if (reloadPointerId == -1004 &&
                    guiReloadMouseButton == e.button)
                {
                    if (!reloadHeldToSwap &&
                        Time.time - reloadPressedAt >= 0.55f)
                    {
                        player.Weapon?.CycleWeapon();
                        reloadHeldToSwap = true;
                    }

                    e.Use();
                    return;
                }

                return;
            }

            if (e.type == EventType.MouseUp)
            {
                bool handled = false;

                // Release only the control owned by this simulated finger/button.
                // A fire finger going up must not stop movement (and vice versa).
                if (movePointerId == -1000 &&
                    guiMoveMouseButton == e.button)
                {
                    ResetMovementPointer();
                    handled = true;
                }

                if (firePointerId == -1001 &&
                    guiFireMouseButton == e.button)
                {
                    firePointerId = -1;
                    guiFireMouseButton = -1;
                    handled = true;
                }

                if (meleePointerId == -1002 &&
                    guiMeleeMouseButton == e.button)
                {
                    meleePointerId = -1;
                    guiMeleeMouseButton = -1;
                    handled = true;
                }

                if (grenadePointerId == -1003 &&
                    guiGrenadeMouseButton == e.button)
                {
                    grenadePointerId = -1;
                    guiGrenadeMouseButton = -1;
                    handled = true;
                }

                if (reloadPointerId == -1004 &&
                    guiReloadMouseButton == e.button)
                {
                    if (!reloadHeldToSwap &&
                        Time.time - reloadPressedAt >= 0.55f)
                    {
                        player.Weapon?.CycleWeapon();
                    }
                    else if (!reloadHeldToSwap)
                    {
                        player.Weapon?.Reload();
                    }

                    reloadPointerId = -1;
                    guiReloadMouseButton = -1;
                    reloadHeldToSwap = false;
                    reloadPressedAt = 0f;
                    handled = true;
                }

                // Unowned MouseUp events must remain available to other IMGUI
                // controls instead of being swallowed by the gameplay fallback.
                if (handled)
                    e.Use();
            }
        }

        private void CreateGuiTextures()
        {
            circleTexture = new Texture2D(
                64,
                64,
                TextureFormat.RGBA32,
                false);

            Vector2 center = new Vector2(31.5f, 31.5f);
            float radius = 31f;

            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    float distance = Vector2.Distance(
                        new Vector2(x, y),
                        center);

                    float alpha = Mathf.Clamp01(
                        radius + 0.5f - distance);

                    circleTexture.SetPixel(
                        x,
                        y,
                        new Color(1f, 1f, 1f, alpha));
                }
            }

            circleTexture.Apply();
        }

        private void DrawCircle(
            Vector2 center,
            float radius,
            Color color,
            bool outline = false)
        {
            if (circleTexture == null)
                return;

            Color old = GUI.color;
            GUI.color = color;

            GUI.DrawTexture(
                new Rect(
                    center.x - radius,
                    center.y - radius,
                    radius * 2f,
                    radius * 2f),
                circleTexture,
                ScaleMode.StretchToFill,
                true);

            GUI.color = old;
        }

        private static bool TryGetTouch(
            int fingerId,
            out Touch touch)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch current = Input.GetTouch(i);
                if (current.fingerId != fingerId)
                    continue;

                touch = current;
                return true;
            }

            touch = default;
            return false;
        }
    }
}