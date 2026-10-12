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
        private const float BaseActionHitRadius = 122f;
        private const float MovePointerRecoveryGraceSeconds = 0.24f;
        private const float FirePointerRecoveryGraceSeconds = 0.24f;
        // Use the full gameplay touch dispatcher; the isolated diagnostic path hid
        // other controls and had a shorter movement-recovery path on Android.
        private const bool CoreMoveFireTestMode = false;
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
        private float musicVolume = 0.75f;
        private int volumePointerId = -1;
        private float movePointerMissingSince = -1f;
        private float firePointerMissingSince = -1f;
        private float reloadPressedAt;
        private bool reloadHeldToSwap;

        // Keep actual finger origin separate from the clamped visual joystick origin.
        private Vector2 moveStartScreen;
        private Vector2 moveTouchOriginScreen;
        private Vector2 moveValue;

        private float nextFireTime;
        private GrenadeController grenadeController;

        private Camera minimapCamera;
        private RenderTexture minimapTexture;
        private Texture2D circleTexture;
        private GUIStyle buttonTextStyle;
        private GUIStyle smallButtonHintStyle;
        // Temporary runtime telemetry for the reported missing second-touch joystick.
        private GUIStyle touchDiagnosticStyle;
        private string lastMovementTouchRoute = "not-yet-claimed";

        private bool minimapEnabled;
        private float minimapReadyAt;
        private bool matchActive;
        private bool matchPaused;
        private bool movementTouchCheckpointWritten;
        private bool fireTouchCheckpointWritten;
        private bool simultaneousTouchCheckpointWritten;
        private bool movingShotCheckpointWritten;

        // IMGUI fallback maps concurrent touchscreen contacts to mouse buttons.
        // Keep a distinct owner per simulated button so movement cannot consume fire.
        private int guiMoveMouseButton = -1;
        private int guiFireMouseButton = -1;
        private int guiMeleeMouseButton = -1;
        private int guiGrenadeMouseButton = -1;
        private int guiReloadMouseButton = -1;

        public Vector2 MoveValue => moveValue;
        public bool IsMovementTouchActive => movePointerId >= 0 || movePointerId == -1000;
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
            simultaneousTouchCheckpointWritten = false;
            movingShotCheckpointWritten = false;
#if UNITY_ANDROID
            Input.multiTouchEnabled = true;
            // During active Android gameplay, accept native Touch IDs only.
            // Touch-to-mouse synthesis can make one finger appear in two input paths.
            Input.simulateMouseWithTouches = false;
#endif
            enabled = true;
        }

        public void DeactivateMatch()
        {
#if UNITY_ANDROID
            // Mouse simulation is allowed again outside the active match for menus.
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
#if UNITY_ANDROID
            // Pause/menu UI may use IMGUI mouse events; active gameplay is native-touch-only.
            Input.simulateMouseWithTouches = paused;
#endif
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
            // Keep menu UI compatible with IMGUI before a match starts.
            // ActivateForMatch switches mouse simulation off for native touch-only gameplay.
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

        // Release every captured finger when the component is disabled. Without this,
        // PlayerController keeps the last non-zero movement vector and can continue moving
        // while the input owner is inactive.
        private void OnDisable()
        {
            ResetAllPointers();
#if UNITY_ANDROID
            // Restore menu-compatible input after this gameplay input owner is disabled.
            Input.simulateMouseWithTouches = true;
#endif
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused)
            {
                ResetAllPointers();
                return;
            }

            RestoreAndroidTouchSettings();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                ResetAllPointers();
                return;
            }

            RestoreAndroidTouchSettings();
        }

        private void RestoreAndroidTouchSettings()
        {
#if UNITY_ANDROID
            Input.multiTouchEnabled = true;
            // Respect the current screen: native touch only in active gameplay,
            // synthesized mouse events only outside gameplay or while paused.
            Input.simulateMouseWithTouches = !matchActive || matchPaused;
#endif
        }

        private void Update()
        {
            if (!matchActive || matchPaused)
                return;

#if UNITY_ANDROID
            // Reassert native multi-touch and keep simulated mouse input disabled
            // for the full duration of an active Android match.
            if (!Input.multiTouchEnabled)
                Input.multiTouchEnabled = true;
            if (Input.simulateMouseWithTouches)
                Input.simulateMouseWithTouches = false;
#endif

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

            if (Input.GetMouseButton(0))
                FireAtNearestTarget();
#else
            // Android gameplay is intentionally native-touch-only for this A/B test.
            // Do not poll any mouse/IMGUI sentinel here: the GUI fallback is disabled
            // on mobile while a match is active so a synthetic mouse cannot claim a
            // finger or overwrite move/fire ownership.
            HandleAndroidTouches();
#endif

            if (minimapEnabled)
            {
                if (minimapCamera != null || Time.unscaledTime >= minimapReadyAt)
                    EnsureMinimap();
            }

            UpdateMinimap();
        }

        private void HandleCoreMoveFireTouches()
        {
#if UNITY_ANDROID
            if (!Input.multiTouchEnabled)
                Input.multiTouchEnabled = true;
#endif
            if (player == null)
                return;

            float scale = GetUiScale();
            float radius = joystickRadius * scale * controlScale;
            GetActionCenters(scale, out Vector2 fireGui, out _, out _, out _);
            float fireHitRadius = radius * 0.90f;
            bool canFire = player.Weapon != null &&
                (player.Weapon.Magazine > 0 || player.Weapon.Reserve > 0);

            // Assign each new finger exactly one role. During this diagnostic build,
            // only LEFT movement and the dedicated FIRE button may claim a touch.
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);

                // Some Android legacy-input frames report an already-held contact as
                // Moved/Stationary when another finger is added. Permit an unowned
                // active contact to claim a free control, so a second finger can start
                // movement or fire even if its Began phase was missed by this reader.
                if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled ||
                    touch.fingerId == movePointerId || touch.fingerId == firePointerId)
                    continue;

                Vector2 gui = new Vector2(touch.position.x, Screen.height - touch.position.y);
                if (canFire && firePointerId == -1 &&
                    Vector2.Distance(gui, fireGui) <= fireHitRadius)
                {
                    firePointerId = touch.fingerId;
                    firePointerMissingSince = -1f;
                    if (!fireTouchCheckpointWritten)
                    {
                        fireTouchCheckpointWritten = true;
                        StartupCheckpoint.Set("MobileFireTouchAccepted");
                    }
                    continue;
                }

                if (movePointerId == -1 &&
                    touch.position.x <= Screen.width * 0.58f &&
                    gui.y >= Screen.height * 0.10f)
                {
                    movePointerId = touch.fingerId;
                    moveTouchOriginScreen = touch.position;
                    moveStartScreen = ClampFloatingOrigin(touch.position, radius);
                    moveValue = Vector2.zero;
                    movePointerMissingSince = -1f;

                    if (!movementTouchCheckpointWritten)
                    {
                        movementTouchCheckpointWritten = true;
                        StartupCheckpoint.Set("MobileMoveTouchAccepted");
                    }
                }
            }

            // Movement owns only its captured pointer. Fire processing below never
            // writes to PlayerController.SetMoveInput, so it cannot zero the joystick.
            bool movementWasReleased = false;
            if (movePointerId >= 0)
            {
                bool foundMovementTouch = TryGetTouch(movePointerId, out Touch movementTouch);
                if (foundMovementTouch && movementTouch.phase == TouchPhase.Ended)
                {
                    ResetMovementPointer();
                    movementWasReleased = true;
                }
                else
                {
                    if (foundMovementTouch && movementTouch.phase == TouchPhase.Canceled)
                    {
                        foundMovementTouch = TryRebindCoreMovementPointer(radius);
                        if (foundMovementTouch)
                            foundMovementTouch = TryGetTouch(movePointerId, out movementTouch);
                        else if (firePointerId >= 0)
                        {
                            // Android can cancel the joystick finger as soon as a second
                            // finger starts, before the joystick has produced a non-zero
                            // vector. Keep movement ownership while FIRE is held, including
                            // that zero-vector startup window, so the live left finger can
                            // be rebound instead of being discarded.
                            if (movePointerMissingSince < 0f)
                                movePointerMissingSince = Time.unscaledTime;
                        }
                        else
                        {
                            ResetMovementPointer();
                            movementWasReleased = true;
                        }
                    }
                    else if (!foundMovementTouch)
                    {
                        foundMovementTouch = TryRebindCoreMovementPointer(radius);
                        if (foundMovementTouch)
                            foundMovementTouch = TryGetTouch(movePointerId, out movementTouch);
                    }

                    if (foundMovementTouch &&
                        movementTouch.phase != TouchPhase.Ended &&
                        movementTouch.phase != TouchPhase.Canceled)
                    {
                        movePointerMissingSince = -1f;
                        Vector2 delta = movementTouch.position - moveTouchOriginScreen;
                        moveValue = Vector2.ClampMagnitude(delta / Mathf.Max(1f, radius), 1f);
                        player.SetMoveInput(ToWorldMove(ApplyDeadZone(moveValue)));
                    }
                    else if (movePointerId >= 0)
                    {
                        if (movePointerMissingSince < 0f)
                            movePointerMissingSince = Time.unscaledTime;

                        // Recovery must not depend on FIRE still being held. Android can
                        // briefly omit/cancel the joystick contact during a second-touch
                        // transition; if FIRE is released first, keep the movement intent
                        // alive for the same short grace window while attempting rebind.
                        bool allowBriefRecovery =
                            Time.unscaledTime - movePointerMissingSince <= MovePointerRecoveryGraceSeconds;
                        if (allowBriefRecovery)
                        {
                            player.SetMoveInput(ToWorldMove(ApplyDeadZone(moveValue)));
                        }
                        else
                        {
                            ResetMovementPointer();
                            movementWasReleased = true;
                        }
                    }
                }
            }

            if (movePointerId == -1 && !movementWasReleased)
            {
                moveValue = Vector2.zero;
                player.SetMoveInput(Vector2.zero);
            }

            // Fire has a completely separate owner. A touch ending on one control
            // cannot release the other control.
            if (firePointerId >= 0)
            {
                bool foundFireTouch = TryGetTouch(firePointerId, out Touch fireTouch);
                if (foundFireTouch && fireTouch.phase == TouchPhase.Ended)
                {
                    ReleaseFirePointer();
                }
                else
                {
                    bool activeFireTouch = foundFireTouch &&
                        fireTouch.phase != TouchPhase.Ended &&
                        fireTouch.phase != TouchPhase.Canceled;

                    if (!activeFireTouch)
                    {
                        if (firePointerMissingSince < 0f)
                            firePointerMissingSince = Time.unscaledTime;

                        bool rebound = TryRebindCoreFirePointer(scale);
                        if (rebound &&
                            TryGetTouch(firePointerId, out fireTouch) &&
                            fireTouch.phase != TouchPhase.Ended &&
                            fireTouch.phase != TouchPhase.Canceled)
                        {
                            firePointerMissingSince = -1f;
                            FireAtNearestTarget();
                        }
                        else if (Time.unscaledTime - firePointerMissingSince <= FirePointerRecoveryGraceSeconds)
                        {
                            FireAtNearestTarget();
                        }
                        else
                        {
                            ReleaseFirePointer();
                        }
                    }
                    else
                    {
                        firePointerMissingSince = -1f;
                        FireAtNearestTarget();
                    }
                }
            }

#if UNITY_ANDROID
            if (!simultaneousTouchCheckpointWritten &&
                movePointerId >= 0 &&
                firePointerId >= 0 &&
                movePointerId != firePointerId)
            {
                simultaneousTouchCheckpointWritten = true;
                StartupCheckpoint.Set("MobileMoveAndFireTouchConcurrent");
            }
#endif
        }

        private bool TryRebindCoreMovementPointer(float radius)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch candidate = Input.GetTouch(i);
                if (candidate.fingerId == movePointerId ||
                    candidate.fingerId == firePointerId ||
                    candidate.fingerId == meleePointerId ||
                    candidate.fingerId == grenadePointerId ||
                    candidate.fingerId == reloadPointerId ||
                    candidate.position.x > Screen.width * 0.58f ||
                    candidate.phase == TouchPhase.Ended ||
                    candidate.phase == TouchPhase.Canceled)
                    continue;

                Vector2 gui = new Vector2(candidate.position.x, Screen.height - candidate.position.y);
                if (gui.y < Screen.height * 0.10f)
                    continue;

                movePointerId = candidate.fingerId;
                moveTouchOriginScreen = candidate.position - moveValue * Mathf.Max(1f, radius);
                moveStartScreen = ClampFloatingOrigin(moveTouchOriginScreen, radius);
                movePointerMissingSince = -1f;
                return true;
            }

            return false;
        }

        private bool TryRebindCoreFirePointer(float scale)
        {
            float actionRadius = BaseActionHitRadius * scale * controlScale;
            GetActionCenters(
                scale,
                out _,
                out Vector2 meleeGui,
                out Vector2 grenadeGui,
                out Vector2 reloadGui);
            Vector2 dashGui = new Vector2(
                Screen.width - 222f * scale,
                Screen.height - actionBottomMargin * scale - 154f * scale);

            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch candidate = Input.GetTouch(i);
                if (candidate.fingerId == firePointerId ||
                    candidate.fingerId == movePointerId ||
                    candidate.phase == TouchPhase.Ended ||
                    candidate.phase == TouchPhase.Canceled ||
                    candidate.position.x < Screen.width * 0.58f)
                    continue;

                Vector2 gui = new Vector2(candidate.position.x, Screen.height - candidate.position.y);
                if (gui.y < Screen.height * 0.10f ||
                    Vector2.Distance(gui, meleeGui) <= actionRadius * 0.76f ||
                    Vector2.Distance(gui, grenadeGui) <= actionRadius * 0.76f ||
                    Vector2.Distance(gui, reloadGui) <= actionRadius * 0.72f ||
                    Vector2.Distance(gui, dashGui) <= actionRadius * 0.62f)
                    continue;

                firePointerId = candidate.fingerId;
                firePointerMissingSince = -1f;
                return true;
            }

            return false;
        }

        private void HandleAndroidTouches()
        {
#if UNITY_ANDROID
            if (!Input.multiTouchEnabled)
                Input.multiTouchEnabled = true;
#endif
            if (player == null)
                return;

            // Rebuilt native touch router: each finger ID has one owner, and every
            // live finger is processed independently. No active finger is reassigned
            // to another control and no IMGUI mouse event can overwrite its vector.
            float scale = GetUiScale();
            float radius = Mathf.Max(1f, joystickRadius * scale * controlScale);
            float actionRadius = BaseActionHitRadius * scale * controlScale;
            GetActionCenters(scale, out Vector2 fireGui, out Vector2 meleeGui,
                out Vector2 grenadeGui, out Vector2 reloadGui);
            Vector2 dashGui = new Vector2(
                Screen.width - 222f * scale,
                Screen.height - actionBottomMargin * scale - 154f * scale);
            Rect volumeRect = new Rect(
                Screen.width * 0.5f - 116f * scale,
                18f * scale, 232f * scale, 28f * scale);

            bool sawMove = false;
            bool sawFire = false;
            bool sawVolume = false;

            // Pass 1: update all already-owned contacts. This is deliberately separate
            // from claiming new contacts so iteration order cannot make a fire finger
            // steal movement, or movement steal a button pressed in the same frame.
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                Vector2 gui = new Vector2(touch.position.x, Screen.height - touch.position.y);
                bool ended = touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled;

                if (touch.fingerId == movePointerId)
                {
                    sawMove = !ended;
                    if (ended)
                        ResetMovementPointer();
                    else
                    {
                        Vector2 delta = touch.position - moveTouchOriginScreen;
                        moveValue = Vector2.ClampMagnitude(delta / radius, 1f);
                        player.SetMoveInput(ToWorldMove(ApplyDeadZone(moveValue)));
                        movePointerMissingSince = -1f;
                    }
                    continue;
                }

                if (touch.fingerId == firePointerId)
                {
                    sawFire = !ended;
                    if (ended)
                        ReleaseFirePointer();
                    else
                        FireAtNearestTarget();
                    continue;
                }

                if (touch.fingerId == meleePointerId)
                {
                    if (ended) meleePointerId = -1;
                    continue;
                }

                if (touch.fingerId == grenadePointerId)
                {
                    if (ended) grenadePointerId = -1;
                    continue;
                }

                if (touch.fingerId == reloadPointerId)
                {
                    if (ended)
                    {
                        if (!reloadHeldToSwap && player.Weapon != null)
                            player.Weapon.Reload();
                        reloadPointerId = -1;
                        reloadPressedAt = 0f;
                        reloadHeldToSwap = false;
                    }
                    else if (!reloadHeldToSwap && Time.time - reloadPressedAt >= 0.55f)
                    {
                        player.Weapon?.CycleWeapon();
                        reloadHeldToSwap = true;
                    }
                    continue;
                }

                if (touch.fingerId == volumePointerId)
                {
                    sawVolume = !ended;
                    if (ended)
                        volumePointerId = -1;
                    else
                    {
                        musicVolume = Mathf.Clamp01(
                            (gui.x - (volumeRect.x + 62f * scale)) / (104f * scale));
                        AudioListener.volume = musicVolume;
                    }
                }
            }

            // A missing finger ID is treated as a lost contact, not rebound to an
            // arbitrary other finger. The short grace period tolerates a transient
            // empty Touch list without allowing a stale joystick to persist forever.
            if (movePointerId >= 0 && !sawMove)
            {
                if (movePointerMissingSince < 0f)
                    movePointerMissingSince = Time.unscaledTime;
                if (Time.unscaledTime - movePointerMissingSince > MovePointerRecoveryGraceSeconds)
                    ResetMovementPointer();
            }
            if (firePointerId >= 0 && !sawFire)
            {
                if (firePointerMissingSince < 0f)
                    firePointerMissingSince = Time.unscaledTime;
                if (Time.unscaledTime - firePointerMissingSince > FirePointerRecoveryGraceSeconds)
                    ReleaseFirePointer();
            }
            if (volumePointerId >= 0 && !sawVolume && Input.touchCount == 0)
                volumePointerId = -1;

            // Pass 2: assign each newly-begun contact once. Ownership is per finger ID,
            // not per frame; existing movement continues while any other finger acts.
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.phase != TouchPhase.Began)
                    continue;

                Vector2 gui = new Vector2(touch.position.x, Screen.height - touch.position.y);
                if (volumePointerId == -1 && volumeRect.Contains(gui))
                {
                    volumePointerId = touch.fingerId;
                    musicVolume = Mathf.Clamp01(
                        (gui.x - (volumeRect.x + 62f * scale)) / (104f * scale));
                    AudioListener.volume = musicVolume;
                    continue;
                }

                if (player.QuickDashCharges > 0 &&
                    Vector2.Distance(gui, dashGui) <= actionRadius * 0.62f)
                {
                    player.TryQuickDash();
                    continue;
                }

                if (meleePointerId == -1 && player.Weapon != null &&
                    !player.IsDefeated &&
                    Vector2.Distance(gui, meleeGui) <= actionRadius * 0.76f)
                {
                    meleePointerId = touch.fingerId;
                    player.Weapon.TryMelee();
                    continue;
                }

                if (grenadePointerId == -1 && grenadeController != null &&
                    grenadeController.Grenades > 0 &&
                    Vector2.Distance(gui, grenadeGui) <= actionRadius * 0.76f)
                {
                    grenadePointerId = touch.fingerId;
                    ThrowGrenadeAtTarget();
                    continue;
                }

                if (reloadPointerId == -1 &&
                    Vector2.Distance(gui, reloadGui) <= actionRadius * 0.72f)
                {
                    reloadPointerId = touch.fingerId;
                    reloadPressedAt = Time.time;
                    reloadHeldToSwap = false;
                    continue;
                }

                bool canFire = player.Weapon != null &&
                    !player.IsDefeated &&
                    (player.Weapon.Magazine > 0 || player.Weapon.Reserve > 0);
                bool insideOtherButton =
                    Vector2.Distance(gui, meleeGui) <= actionRadius * 0.76f ||
                    Vector2.Distance(gui, grenadeGui) <= actionRadius * 0.76f ||
                    Vector2.Distance(gui, reloadGui) <= actionRadius * 0.72f;
                bool fireZone =
                    Vector2.Distance(gui, fireGui) <= actionRadius * 1.02f ||
                    (gui.x >= Screen.width * 0.70f && gui.y >= Screen.height * 0.48f);

                if (canFire && firePointerId == -1 && !insideOtherButton && fireZone)
                {
                    firePointerId = touch.fingerId;
                    firePointerMissingSince = -1f;
                    if (!fireTouchCheckpointWritten)
                    {
                        fireTouchCheckpointWritten = true;
                        StartupCheckpoint.Set("MobileFireTouchAccepted");
                    }
                    FireAtNearestTarget();
                    continue;
                }

                if (movePointerId == -1 &&
                    touch.position.x <= Screen.width * 0.58f &&
                    gui.y >= Screen.height * 0.10f)
                {
                    movePointerId = touch.fingerId;
                    lastMovementTouchRoute = "native-began id=" + touch.fingerId + " phase=" + touch.phase;
                    moveTouchOriginScreen = touch.position;
                    moveStartScreen = ClampFloatingOrigin(touch.position, radius);
                    moveValue = Vector2.zero;
                    movePointerMissingSince = -1f;
                    player.SetMoveInput(Vector2.zero);
                    if (!movementTouchCheckpointWritten)
                    {
                        movementTouchCheckpointWritten = true;
                        StartupCheckpoint.Set("MobileMoveTouchAccepted");
                    }
                }
            }

            if (movePointerId >= 0 && firePointerId >= 0 &&
                !simultaneousTouchCheckpointWritten)
            {
                simultaneousTouchCheckpointWritten = true;
                StartupCheckpoint.Set("MobileMoveAndFireTouchConcurrent");
            }
        
            // Some Android devices omit TouchPhase.Began for a second finger when
            // another finger is already held. Recover unowned contacts over the combat
            // controls as well as the joystick, otherwise move+fire silently becomes
            // move-only. Every action still claims a unique finger ID and fires once.
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.phase != TouchPhase.Moved && touch.phase != TouchPhase.Stationary)
                    continue;

                if (touch.fingerId == movePointerId ||
                    touch.fingerId == firePointerId ||
                    touch.fingerId == meleePointerId ||
                    touch.fingerId == grenadePointerId ||
                    touch.fingerId == reloadPointerId ||
                    touch.fingerId == volumePointerId)
                    continue;

                Vector2 gui = new Vector2(
                    touch.position.x,
                    Screen.height - touch.position.y);

                if (volumePointerId == -1 && volumeRect.Contains(gui))
                {
                    volumePointerId = touch.fingerId;
                    musicVolume = Mathf.Clamp01(
                        (gui.x - (volumeRect.x + 62f * scale)) / (104f * scale));
                    AudioListener.volume = musicVolume;
                    continue;
                }

                if (meleePointerId == -1 && player.Weapon != null &&
                    !player.IsDefeated &&
                    Vector2.Distance(gui, meleeGui) <= actionRadius * 0.76f)
                {
                    meleePointerId = touch.fingerId;
                    player.Weapon.TryMelee();
                    continue;
                }

                if (grenadePointerId == -1 && grenadeController != null &&
                    grenadeController.Grenades > 0 &&
                    Vector2.Distance(gui, grenadeGui) <= actionRadius * 0.76f)
                {
                    grenadePointerId = touch.fingerId;
                    ThrowGrenadeAtTarget();
                    continue;
                }

                if (reloadPointerId == -1 &&
                    Vector2.Distance(gui, reloadGui) <= actionRadius * 0.72f)
                {
                    reloadPointerId = touch.fingerId;
                    reloadPressedAt = Time.time;
                    reloadHeldToSwap = false;
                    continue;
                }

                bool canFireRecovered = player.Weapon != null &&
                    !player.IsDefeated &&
                    (player.Weapon.Magazine > 0 || player.Weapon.Reserve > 0);
                bool overOtherAction = 
                    Vector2.Distance(gui, meleeGui) <= actionRadius * 0.76f ||
                    Vector2.Distance(gui, grenadeGui) <= actionRadius * 0.76f ||
                    Vector2.Distance(gui, reloadGui) <= actionRadius * 0.72f;
                bool inFireZone =
                    Vector2.Distance(gui, fireGui) <= actionRadius * 1.02f ||
                    (gui.x >= Screen.width * 0.70f && gui.y >= Screen.height * 0.48f);

                if (canFireRecovered && firePointerId == -1 &&
                    !overOtherAction && inFireZone)
                {
                    firePointerId = touch.fingerId;
                    firePointerMissingSince = -1f;
                    if (!fireTouchCheckpointWritten)
                    {
                        fireTouchCheckpointWritten = true;
                        StartupCheckpoint.Set("MobileFireTouchRecoveredWithoutBegan");
                    }
                    FireAtNearestTarget();
                }
            }

            // Recovery for Android devices that omit TouchPhase.Began when a second
            // finger is added while another contact is already held. The left movement zone also recovers here if its Began phase was omitted.
            if (movePointerId == -1)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    Touch touch = Input.GetTouch(i);
                    if (touch.phase != TouchPhase.Moved &&
                        touch.phase != TouchPhase.Stationary)
                        continue;

                    if (touch.fingerId == firePointerId ||
                        touch.fingerId == meleePointerId ||
                        touch.fingerId == grenadePointerId ||
                        touch.fingerId == reloadPointerId ||
                        touch.fingerId == volumePointerId)
                        continue;

                    Vector2 gui = new Vector2(
                        touch.position.x,
                        Screen.height - touch.position.y);
                    if (touch.position.x > Screen.width * 0.58f ||
                        gui.y < Screen.height * 0.10f)
                        continue;

                    movePointerId = touch.fingerId;
                    lastMovementTouchRoute = "native-recovered id=" + touch.fingerId + " phase=" + touch.phase;
                    moveTouchOriginScreen = touch.position;
                    moveStartScreen = ClampFloatingOrigin(touch.position, radius);
                    moveValue = Vector2.zero;
                    movePointerMissingSince = -1f;
                    player.SetMoveInput(Vector2.zero);
                    if (!movementTouchCheckpointWritten)
                    {
                        movementTouchCheckpointWritten = true;
                        StartupCheckpoint.Set("MobileMoveTouchRecoveredWithoutBegan");
                    }
                    break;
                }
            }
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

            TargetHealth target = player.Aim != null
                ? player.Aim.CurrentTarget
                : null;

            // Aim and movement are separate: auto-aim at the nearest visible target.
            // Without a target, use the current travel vector, falling back to the
            // previous facing direction so firing does not pull movement to a stop.
            Vector3 aimDirection;
            if (target != null)
            {
                aimDirection = target.transform.position - player.transform.position;
            }
            else
            {
                Vector2 movement = player.MoveInput;
                aimDirection = movement.sqrMagnitude > 0.01f
                    ? new Vector3(movement.x, 0f, movement.y)
                    : player.transform.forward;
            }

            aimDirection.y = 0f;
            if (aimDirection.sqrMagnitude < 0.001f)
                aimDirection = player.transform.forward;
            aimDirection.Normalize();
            player.SetAimDirection(aimDirection);

            if (Time.time < nextFireTime)
                return;

            bool fired = false;
            if (target != null)
                fired = player.Weapon.TryFire(target.transform.position);

            if (!fired && player.Weapon.Magazine > 0)
                fired = player.Weapon.TryFireDirection(aimDirection);

            if (fired)
            {
                nextFireTime = Time.time + fireRepeatInterval;
#if UNITY_ANDROID
                if (!movingShotCheckpointWritten &&
                    Application.isMobilePlatform &&
                    player.MoveInput.sqrMagnitude > 0.01f)
                {
                    movingShotCheckpointWritten = true;
                    StartupCheckpoint.Set("MobileShotFiredWhileMoving");
                }
#endif
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

            // Keep every button's visual disk and touch hit-zone separate at
            // the smallest and largest supported UI scales. Overlapping fire/reload
            // zones previously made taps on the fire button reload instead.
            melee = new Vector2(
                Screen.width - 352f * scale,
                Screen.height - bottom - 18f * scale);

            grenade = new Vector2(
                Screen.width - 352f * scale,
                Screen.height - bottom - 236f * scale);

            reload = new Vector2(
                Screen.width - 104f * scale,
                Screen.height - bottom - 300f * scale);
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

        private void ReleaseFirePointer()
        {
            firePointerId = -1;
            firePointerMissingSince = -1f;
            if (player != null)
                player.ClearAimDirection();
        }

        private void ResetMovementPointer()
        {
            movePointerId = -1;
            movePointerMissingSince = -1f;

            guiMoveMouseButton = -1;
            moveStartScreen = Vector2.zero;
            moveTouchOriginScreen = Vector2.zero;
            moveValue = Vector2.zero;

            if (player != null)
                player.SetMoveInput(Vector2.zero);
        }

        private void ResetAllPointers()
        {
            movePointerId = -1;
            movePointerMissingSince = -1f;

            ReleaseFirePointer();
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
            moveTouchOriginScreen = Vector2.zero;
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

            // Native Touch IDs are preferred. The fallback dispatcher itself yields
            // while native contacts are present, but can recover input when touchCount
            // is empty on an Android device.
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
            if (movePointerId >= 0 || movePointerId == -1000)
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

            bool canFire = player != null && player.Weapon != null && (player.Weapon.Magazine > 0 || player.Weapon.Reserve > 0);
            bool canMelee = player != null && player.Weapon != null && !player.IsDefeated;
            bool canThrowGrenade = grenadeController != null && grenadeController.Grenades > 0;
            DrawCircle(
                firePos,
                radius * 0.76f,
                new Color(0.72f, 0.12f, 0.08f,
                    canFire ? (firePointerId >= 0 ? 0.70f : 0.48f) : 0.16f));

            DrawCircle(
                meleePos,
                radius * 0.58f,
                new Color(0.16f, 0.18f, 0.22f,
                    canMelee ? (meleePointerId >= 0 ? 0.78f : 0.56f) : 0.16f));

            DrawCircle(
                grenadePos,
                radius * 0.58f,
                new Color(0.18f, 0.40f, 0.16f,
                    canThrowGrenade ? (grenadePointerId >= 0 ? 0.72f : 0.52f) : 0.16f));

            if (!CoreMoveFireTestMode && player != null && player.QuickDashCharges > 0)
            {
                Vector2 dashPos = new Vector2(Screen.width - 222f * scale, Screen.height - actionBottomMargin * scale - 154f * scale);
                DrawCircle(dashPos, radius * 0.62f, new Color(0.88f, 0.12f, 0.92f, 0.88f));
                DrawCircle(dashPos, radius * 0.48f, new Color(0.18f, 0.04f, 0.24f, 0.96f));
                GUI.Label(new Rect(dashPos.x - radius * 0.65f, dashPos.y - radius * 0.38f, radius * 1.3f, radius * 0.76f), "DASH " + player.QuickDashCharges, buttonTextStyle ?? GUI.skin.label);
            }
            DrawCircle(
                reloadPos,
                radius * 0.56f,
                new Color(0.16f, 0.22f, 0.32f,
                    CoreMoveFireTestMode ? 0.10f : reloadPointerId >= 0 ? 0.76f : 0.54f));

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

            buttonTextStyle.normal.textColor = canFire ? Color.white : new Color(1f, 1f, 1f, 0.32f);
            GUI.Label(
                new Rect(
                    firePos.x - radius,
                    firePos.y - radius * 0.30f,
                    radius * 2f,
                    radius * 0.60f),
                "FIRE",
                buttonTextStyle);
            buttonTextStyle.normal.textColor = canMelee ? Color.white : new Color(1f, 1f, 1f, 0.32f);

            GUI.Label(
                new Rect(
                    meleePos.x - radius * 0.60f,
                    meleePos.y - radius * 0.40f,
                    radius * 1.20f,
                    radius * 0.80f),
                "FIST",
                buttonTextStyle);
            buttonTextStyle.normal.textColor = canThrowGrenade ? Color.white : new Color(1f, 1f, 1f, 0.32f);

            GUI.Label(
                new Rect(
                    grenadePos.x - radius * 0.60f,
                    grenadePos.y - radius * 0.40f,
                    radius * 1.20f,
                    radius * 0.80f),
                "G",
                buttonTextStyle);
            buttonTextStyle.normal.textColor = Color.white;

            buttonTextStyle.normal.textColor = CoreMoveFireTestMode
                ? new Color(1f, 1f, 1f, 0.30f)
                : Color.white;
            GUI.Label(
                new Rect(
                    reloadPos.x - radius * 0.55f,
                    reloadPos.y - radius * 0.40f,
                    radius * 1.10f,
                    radius * 0.80f),
                "R",
                buttonTextStyle);
            buttonTextStyle.normal.textColor = Color.white;
            Rect volumeRect = new Rect(Screen.width * 0.5f - 116f * scale, 18f * scale, 232f * scale, 28f * scale);
            GUI.Box(volumeRect, GUIContent.none);
            GUI.Label(new Rect(volumeRect.x + 4f * scale, volumeRect.y, 58f * scale, volumeRect.height), "MUSIC", smallButtonHintStyle);
            bool previousGuiEnabled = GUI.enabled;
            GUI.enabled = previousGuiEnabled && !CoreMoveFireTestMode;
            musicVolume = GUI.HorizontalSlider(new Rect(volumeRect.x + 62f * scale, volumeRect.y + 7f * scale, 104f * scale, 16f * scale), musicVolume, 0f, 1f);
            GUI.enabled = previousGuiEnabled;
            AudioListener.volume = musicVolume;
            GUI.Label(new Rect(volumeRect.x + 168f * scale, volumeRect.y, 60f * scale, volumeRect.height), Mathf.RoundToInt(musicVolume * 100f) + "%", smallButtonHintStyle);
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

            DrawTouchDiagnosticOverlay(scale);

            if (CoreMoveFireTestMode)
            {
                bool movementPointerActive = movePointerId >= 0 || movePointerId == -1000;
                bool movementVectorActive = player.MoveInput.sqrMagnitude > 0.02f;
                bool firePointerActive = firePointerId >= 0 || firePointerId == -1001;
                // Large, high-contrast diagnostic banner: do not scale this down
                // with the gameplay UI scale, which made it unreadable on phones.
                float debugWidth = Mathf.Min(Screen.width - 24f, 560f);
                float debugHeight = Mathf.Min(126f, Screen.height * 0.18f);
                Rect debugRect = new Rect(
                    (Screen.width - debugWidth) * 0.5f,
                    Mathf.Max(12f, Screen.height * 0.12f),
                    debugWidth,
                    debugHeight);
                Color oldBackground = GUI.backgroundColor;
                Color oldContent = GUI.contentColor;
                GUI.backgroundColor = new Color(0.02f, 0.04f, 0.08f, 0.98f);
                GUI.contentColor = Color.white;
                GUI.Box(debugRect, GUIContent.none);
                smallButtonHintStyle.fontSize = Mathf.Clamp(
                    Mathf.RoundToInt(Screen.width / 34f), 16, 23);
                smallButtonHintStyle.fontStyle = FontStyle.Bold;
                smallButtonHintStyle.alignment = TextAnchor.MiddleLeft;
                smallButtonHintStyle.normal.textColor = Color.white;
                smallButtonHintStyle.normal.background = Texture2D.whiteTexture;
                string touchSummary = "";
                for (int touchIndex = 0; touchIndex < Input.touchCount && touchIndex < 4; touchIndex++)
                {
                    Touch diagnosticTouch = Input.GetTouch(touchIndex);
                    touchSummary += (touchIndex == 0 ? "" : " | ") +
                        diagnosticTouch.fingerId + ":" + diagnosticTouch.phase;
                }
                string diagnosticText =
                    "M id:" + movePointerId + " V:" + (movementVectorActive ? "1" : "0") +
                    " mv:" + moveValue.x.ToString("F1") + "," + moveValue.y.ToString("F1") +
                    "\nF id:" + firePointerId + " F:" + (firePointerActive ? "1" : "0") +
                    " T:" + Input.touchCount + " canMove:" + (player.IsMovementEnabled ? "1" : "0") +
                    "\nTouches: " + (touchSummary.Length == 0 ? "none" : touchSummary);
                GUI.Label(new Rect(debugRect.x + 8f, debugRect.y + 4f,
                    debugRect.width - 16f, debugRect.height - 8f),
                    diagnosticText, smallButtonHintStyle);
                GUI.backgroundColor = oldBackground;
                GUI.contentColor = oldContent;
            }
        }

        // Temporary diagnostic-only overlay. It records the actual Unity touch list
        // and pointer ownership without consuming or changing any input event.
        private void DrawTouchDiagnosticOverlay(float scale)
        {
            if (!Application.isMobilePlatform || !matchActive || matchPaused)
                return;

            int count = Input.touchCount;
            string contacts = "none";
            for (int i = 0; i < count && i < 4; i++)
            {
                Touch touch = Input.GetTouch(i);
                Vector2 gui = new Vector2(touch.position.x, Screen.height - touch.position.y);
                string owner = touch.fingerId == movePointerId ? "/MOVE"
                    : touch.fingerId == firePointerId ? "/FIRE"
                    : touch.fingerId == meleePointerId ? "/MELEE"
                    : touch.fingerId == grenadePointerId ? "/GRENADE"
                    : touch.fingerId == reloadPointerId ? "/RELOAD" : string.Empty;
                string item = touch.fingerId + ":" + touch.phase + "(" +
                    Mathf.RoundToInt(gui.x) + "," + Mathf.RoundToInt(gui.y) + ")" + owner;
                contacts = i == 0 ? item : contacts + " | " + item;
            }

            if (touchDiagnosticStyle == null)
            {
                touchDiagnosticStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.UpperLeft,
                    wordWrap = true,
                    fontStyle = FontStyle.Bold,
                    padding = new RectOffset(6, 6, 4, 4)
                };
            }
            touchDiagnosticStyle.fontSize = Mathf.Clamp(Mathf.RoundToInt(14f * scale), 11, 18);
            touchDiagnosticStyle.normal.textColor = Color.white;

            float width = Mathf.Min(Screen.width - 24f, 690f * scale);
            float height = 91f * scale;
            Rect panel = new Rect((Screen.width - width) * 0.5f, 116f * scale, width, height);
            Color oldBackground = GUI.backgroundColor;
            Color oldContent = GUI.contentColor;
            GUI.backgroundColor = new Color(0.02f, 0.035f, 0.055f, 0.97f);
            GUI.contentColor = Color.white;
            GUI.Box(panel, GUIContent.none);
            string summary = "TOUCH DIAG  native=" + count +
                "  moveId=" + movePointerId + "  fireId=" + firePointerId +
                "  multi=" + Input.multiTouchEnabled +
                "  mouseSim=" + Input.simulateMouseWithTouches +
                "\nFINGERS: " + contacts +
                "\nMOVE ROUTE: " + lastMovementTouchRoute;
            GUI.Label(new Rect(panel.x + 6f, panel.y + 4f, panel.width - 12f, panel.height - 8f),
                summary, touchDiagnosticStyle);
            GUI.backgroundColor = oldBackground;
            GUI.contentColor = oldContent;
        }

        private void HandleCoreGuiPointerFallback(Event e)
        {
            if (Application.isMobilePlatform &&
                Input.touchCount > 0 &&
                movePointerId != -1000 &&
                firePointerId != -1001)
                return;

            if (e.type != EventType.MouseDown &&
                e.type != EventType.MouseDrag &&
                e.type != EventType.MouseUp)
                return;

            Vector2 gui = e.mousePosition;
            float scale = GetUiScale();
            float radius = joystickRadius * scale * controlScale;
            GetActionCenters(scale, out Vector2 fireGui, out _, out _, out _);
            float fireHitRadius = radius * 0.90f;

            if (e.type == EventType.MouseDown)
            {
                int mouseButton = e.button;
                if (mouseButton < 0 || mouseButton > 2)
                    return;

                bool canFire = player != null && player.Weapon != null &&
                    (player.Weapon.Magazine > 0 || player.Weapon.Reserve > 0);
                if (canFire && firePointerId == -1 &&
                    guiFireMouseButton == -1 &&
                    Vector2.Distance(gui, fireGui) <= fireHitRadius)
                {
                    firePointerId = -1001;
                    guiFireMouseButton = mouseButton;
                    FireAtNearestTarget();
                    e.Use();
                    return;
                }

                if (movePointerId == -1 &&
                    guiMoveMouseButton == -1 &&
                    gui.x <= Screen.width * 0.58f &&
                    gui.y >= Screen.height * 0.10f)
                {
                    movePointerId = -1000;
                    lastMovementTouchRoute = "imgui-mousedown button=" + mouseButton;
                    guiMoveMouseButton = mouseButton;
                    moveTouchOriginScreen = new Vector2(gui.x, Screen.height - gui.y);
                    moveStartScreen = ClampFloatingOrigin(moveTouchOriginScreen, radius);
                    moveValue = Vector2.zero;
                    e.Use();
                }

                // Keep pause/menu GUI events available; auxiliary combat controls
                // intentionally do not claim touches in this isolated test build.
                return;
            }

            if (e.type == EventType.MouseDrag)
            {
                if (movePointerId == -1000 && guiMoveMouseButton == e.button)
                {
                    Vector2 current = new Vector2(gui.x, Screen.height - gui.y);
                    Vector2 delta = current - moveTouchOriginScreen;
                    moveValue = Vector2.ClampMagnitude(delta / Mathf.Max(1f, radius), 1f);
                    player?.SetMoveInput(ToWorldMove(ApplyDeadZone(moveValue)));
                    e.Use();
                    return;
                }

                if (firePointerId == -1001 && guiFireMouseButton == e.button)
                {
                    FireAtNearestTarget();
                    e.Use();
                }
                return;
            }

            if (e.type == EventType.MouseUp)
            {
                bool handled = false;
                if (movePointerId == -1000 && guiMoveMouseButton == e.button)
                {
                    ResetMovementPointer();
                    handled = true;
                }

                if (firePointerId == -1001 && guiFireMouseButton == e.button)
                {
                    ReleaseFirePointer();
                    guiFireMouseButton = -1;
                    handled = true;
                }

                if (handled)
                    e.Use();
            }
        }

        private void HandleGuiPointerFallback()
        {
            // Android match gameplay is native Touch.fingerId only.
            // Do not let IMGUI mouse events claim movement/fire or overwrite touch ownership.
            // OnGUI still draws the controls; only its mouse-input fallback is disabled.
            if (Application.isMobilePlatform && matchActive && !matchPaused)
                return;

            Event e = Event.current;
            if (e == null)
                return;

            if (CoreMoveFireTestMode)
            {
                HandleCoreGuiPointerFallback(e);
                return;
            }

            // Use the real Touch.fingerId path whenever Unity exposes live touches.
            // If we already own an IMGUI fallback pointer, keep the fallback active
            // until that simulated contact has been released.
            bool guiFallbackOwnsPointer =
                movePointerId == -1000 ||
                firePointerId == -1001 ||
                meleePointerId == -1002 ||
                grenadePointerId == -1003 ||
                reloadPointerId == -1004;

            if (Application.isMobilePlatform && Input.touchCount > 0)
            {
                // Unity may expose one contact through Touch.fingerId and another
                // through touch-simulated IMGUI mouse events. If this MouseDown is
                // already represented in the native Touch list, let the native
                // dispatcher own it so one physical finger cannot be claimed twice.
                if (e.type == EventType.MouseDown &&
                    IsGuiPositionNearNativeTouch(e.mousePosition, Mathf.Max(48f, 42f * GetUiScale())))
                    return;

                // With native contacts live, only continue fallback processing for
                // a new, unrepresented MouseDown or for a fallback pointer already
                // captured by this dispatcher. This permits a second touch to use
                // either movement or an action button when Android's legacy Touch
                // list omits that contact, without double-dispatching native touches.
                if (!guiFallbackOwnsPointer && e.type != EventType.MouseDown)
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
            float actionHitRadius = BaseActionHitRadius * scale * controlScale;

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
                bool canFire = player != null && player.Weapon != null && (player.Weapon.Magazine > 0 || player.Weapon.Reserve > 0);
                bool canMelee = player != null && player.Weapon != null && !player.IsDefeated;
                bool canThrowGrenade = grenadeController != null && grenadeController.Grenades > 0;
                if (canFire && firePointerId == -1 &&
                    guiFireMouseButton == -1 &&
                    Vector2.Distance(gui, firePos) <= actionHitRadius * 1.02f)
                {
                    firePointerId = -1001;
                    guiFireMouseButton = mouseButton;
                    FireAtNearestTarget();
                    e.Use();
                    return;
                }

                if (canMelee && meleePointerId == -1 &&
                    guiMeleeMouseButton == -1 &&
                    Vector2.Distance(gui, meleePos) <= actionHitRadius * 0.76f)
                {
                    meleePointerId = -1002;
                    guiMeleeMouseButton = mouseButton;
                    player.Weapon?.TryMelee();
                    e.Use();
                    return;
                }

                if (canThrowGrenade && grenadePointerId == -1 &&
                    guiGrenadeMouseButton == -1 &&
                    Vector2.Distance(gui, grenadePos) <= actionHitRadius * 0.76f)
                {
                    grenadePointerId = -1003;
                    guiGrenadeMouseButton = mouseButton;
                    ThrowGrenadeAtTarget();
                    e.Use();
                    return;
                }

                if (reloadPointerId == -1 &&
                    guiReloadMouseButton == -1 &&
                    Vector2.Distance(gui, reloadPos) <= actionHitRadius * 0.72f)
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
                    moveTouchOriginScreen = new Vector2(gui.x, Screen.height - gui.y);
                    moveStartScreen = ClampFloatingOrigin(moveTouchOriginScreen, radius);
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
                    Vector2 delta = current - moveTouchOriginScreen;
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
                    ReleaseFirePointer();
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

        private bool IsGuiPositionNearNativeTouch(Vector2 guiPosition, float tolerance)
        {
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                    continue;

                Vector2 touchGuiPosition = new Vector2(
                    touch.position.x,
                    Screen.height - touch.position.y);
                if (Vector2.Distance(guiPosition, touchGuiPosition) <= tolerance)
                    return true;
            }

            return false;
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