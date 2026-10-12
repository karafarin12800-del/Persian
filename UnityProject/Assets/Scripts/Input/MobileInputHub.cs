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
        private int dashPointerId = -1;
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

        private bool minimapEnabled;
        private float minimapReadyAt;
        private bool matchActive;
        private bool matchPaused;
        private bool movementTouchCheckpointWritten;
        private bool fireTouchCheckpointWritten;
        private bool simultaneousTouchCheckpointWritten;
        private bool movingShotCheckpointWritten;
        private bool lateTouchRecoveryCheckpointWritten;
        private bool movementRecoveryActive;

        // IMGUI fallback maps concurrent touchscreen contacts to mouse buttons.
        // Keep a distinct owner per simulated button so movement cannot consume fire.
        private int guiMoveMouseButton = -1;
        private int guiFireMouseButton = -1;
        private int guiMeleeMouseButton = -1;
        private int guiGrenadeMouseButton = -1;
        private int guiReloadMouseButton = -1;

        public Vector2 MoveValue => moveValue;
        public bool IsMovementTouchActive => movePointerId >= 0 || movePointerId == -1000 || movementRecoveryActive;
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
            lateTouchRecoveryCheckpointWritten = false;
#if UNITY_ANDROID
            Input.multiTouchEnabled = true;
            // Keep the IMGUI fallback alive for devices/frames where the legacy Touch
            // API reports no contacts. HandleGuiPointerFallback yields to native touch
            // whenever Input.touchCount > 0, so native finger IDs remain authoritative.
            Input.simulateMouseWithTouches = true;
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
#if UNITY_ANDROID
            // Preserve IMGUI fallback support while paused; PrototypeFlow's native
            // pause touch handler remains primary on Android.
            Input.simulateMouseWithTouches = true;
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

        // Release every captured finger when the component is disabled. Without this,
        // PlayerController keeps the last non-zero movement vector and can continue moving
        // while the input owner is inactive.
        private void OnDisable()
        {
            ResetAllPointers();
#if UNITY_ANDROID
            // Keep menu/pause IMGUI buttons usable after gameplay input is disabled.
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
            // Keep mouse synthesis enabled as a compatibility fallback. The gameplay
            // fallback dispatcher ignores synthesized events while native touches exist.
            Input.simulateMouseWithTouches = true;
#endif
        }

        private void Update()
        {
            if (!matchActive || matchPaused)
                return;

#if UNITY_ANDROID
            // Reassert multi-touch, but do not disable mouse synthesis here: the
            // fallback needs it when the old Touch API temporarily reports zero.
            // HandleGuiPointerFallback ignores mouse events whenever native contacts
            // are available, preventing a single contact from being processed twice.
            if (!Input.multiTouchEnabled)
                Input.multiTouchEnabled = true;
            if (!Input.simulateMouseWithTouches)
                Input.simulateMouseWithTouches = true;
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
            HandleAndroidTouches();
            // Some devices expose the IMGUI fallback pointer rather than a Touch ID.
            // Poll it every frame so holding FIRE repeats shots even without MouseDrag events.
            if (firePointerId == -1001)
                FireAtNearestTarget();
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
#if UNITY_ANDROID
            // Enable native multi-touch continuously. Mouse synthesis stays available
            // only as a fallback; the dispatcher below prefers native contacts.
            if (!Input.multiTouchEnabled)
                Input.multiTouchEnabled = true;
            if (!Input.simulateMouseWithTouches)
                Input.simulateMouseWithTouches = true;
#endif
            float scale = GetUiScale();
            float radius = joystickRadius * scale * controlScale;

            GetActionCenters(
                scale,
                out Vector2 fireGui,
                out Vector2 meleeGui,
                out Vector2 grenadeGui,
                out Vector2 reloadGui);

            float actionHitRadius = BaseActionHitRadius * scale * controlScale;
            float fireHit = actionHitRadius * 1.02f;
            float meleeHit = actionHitRadius * 0.76f;
            float grenadeHit = actionHitRadius * 0.76f;
            float reloadHit = actionHitRadius * 0.72f;
            Vector2 dashGui = new Vector2(Screen.width - 222f * scale, Screen.height - actionBottomMargin * scale - 154f * scale);
            float dashHit = actionHitRadius * 0.62f;

            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                Vector2 gui = new Vector2(touch.position.x, Screen.height - touch.position.y);
                // rawPosition preserves the finger's origin if Android omits its one-frame Began phase.
                Vector2 touchStartScreen = touch.rawPosition;
                Vector2 touchStartGui = new Vector2(touchStartScreen.x, Screen.height - touchStartScreen.y);
                Rect volumeTouchRect = new Rect(Screen.width * 0.5f - 116f * scale, 18f * scale, 232f * scale, 28f * scale);
                if (volumePointerId == touch.fingerId)
                {
                    if (touch.phase == TouchPhase.Moved || touch.phase == TouchPhase.Stationary)
                    {
                        musicVolume = Mathf.Clamp01((gui.x - (volumeTouchRect.x + 62f * scale)) / (104f * scale));
                        AudioListener.volume = musicVolume;
                    }
                    if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled) volumePointerId = -1;
                    continue;
                }
                if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                    continue;

                // Retain ownership of the dash contact so a missed Began phase
                // cannot make the dash action trigger repeatedly on every frame.
                if (dashPointerId == touch.fingerId)
                {
                    if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                        dashPointerId = -1;
                    continue;
                }

                // Recover unowned live contacts even if the Began frame was missed.
                // Captured contacts are handled below by their own independent finger IDs.
                if (touch.fingerId == movePointerId ||
                    touch.fingerId == firePointerId ||
                    touch.fingerId == meleePointerId ||
                    touch.fingerId == grenadePointerId ||
                    touch.fingerId == reloadPointerId ||
                    touch.fingerId == dashPointerId)
                    continue;

                if (volumeTouchRect.Contains(touchStartGui))
                {
                    MarkLateTouchRecovery(touch);
                    volumePointerId = touch.fingerId;
                    musicVolume = Mathf.Clamp01((gui.x - (volumeTouchRect.x + 62f * scale)) / (104f * scale));
                    AudioListener.volume = musicVolume;
                    continue;
                }
                if (Vector2.Distance(touchStartGui, dashGui) <= dashHit)
                {
                    dashPointerId = touch.fingerId;
                    MarkLateTouchRecovery(touch);
                    if (player != null && player.QuickDashCharges > 0)
                        player.TryQuickDash();
                    continue;
                }

                if (meleePointerId == -1 &&
                    Vector2.Distance(touchStartGui, meleeGui) <= meleeHit)
                {
                    MarkLateTouchRecovery(touch);
                    meleePointerId = touch.fingerId;
                    player.Weapon?.TryMelee();
                    continue;
                }

                if (grenadePointerId == -1 &&
                    Vector2.Distance(touchStartGui, grenadeGui) <= grenadeHit)
                {
                    MarkLateTouchRecovery(touch);
                    grenadePointerId = touch.fingerId;
                    ThrowGrenadeAtTarget();
                    continue;
                }

                if (reloadPointerId == -1 &&
                    Vector2.Distance(touchStartGui, reloadGui) <= reloadHit)
                {
                    // Tap to reload; hold for 0.55 seconds to cycle weapon profile.
                    MarkLateTouchRecovery(touch);
                    reloadPointerId = touch.fingerId;
                    reloadPressedAt = Time.time;
                    reloadHeldToSwap = false;
                    continue;
                }

                // Capture firing after melee/grenade/reload so the widened area
                // cannot steal those controls. The lower-right backup region tolerates
                // screen-density and safe-area differences on Android.
                bool insideOtherActionZone =
                    Vector2.Distance(touchStartGui, meleeGui) <= meleeHit ||
                    Vector2.Distance(touchStartGui, grenadeGui) <= grenadeHit ||
                    Vector2.Distance(touchStartGui, reloadGui) <= reloadHit;
                bool broadFireZone =
                    touchStartGui.x >= Screen.width * 0.70f &&
                    touchStartGui.y >= Screen.height * 0.48f;

                if (firePointerId == -1 &&
                    !insideOtherActionZone &&
                    (Vector2.Distance(touchStartGui, fireGui) <= fireHit || broadFireZone))
                {
                    MarkLateTouchRecovery(touch);
                    firePointerId = touch.fingerId;
                    firePointerMissingSince = -1f;
                    if (!fireTouchCheckpointWritten)
                    {
                        fireTouchCheckpointWritten = true;
                        StartupCheckpoint.Set("MobileFireTouchAccepted");
                    }
                    continue;
                }

                // Everything else on the left side becomes the floating movement
                // stick. The origin follows the player's first touch.
                if (touchStartScreen.x <= Screen.width * 0.58f &&
                    touchStartGui.y >= Screen.height * 0.10f)
                {
                    if (movePointerId == -1)
                    {
                        MarkLateTouchRecovery(touch);
                        movePointerId = touch.fingerId;
                        moveTouchOriginScreen = touchStartScreen;
                        moveStartScreen = ClampFloatingOrigin(touchStartScreen, radius);
                        moveValue = Vector2.zero;
                        movePointerMissingSince = -1f;
                        movementRecoveryActive = false;
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
                    if (movementTouch.phase == TouchPhase.Ended)
                    {
                        // A normal lift is definitive: stop immediately.
                        ResetMovementPointer();
                    }
                    else if (movementTouch.phase == TouchPhase.Canceled)
                    {
                        // Some Android builds keep a Canceled Touch in the touch list
                        // for more than one frame. Rebind to a different active LEFT
                        // finger immediately; otherwise the joystick remains attached
                        // to a dead finger and movement+fire cannot coexist.
                        bool rebound = false;
                        for (int j = 0; j < Input.touchCount; j++)
                        {
                            Touch candidate = Input.GetTouch(j);
                            if (candidate.fingerId == movePointerId ||
                                candidate.fingerId == firePointerId ||
                                candidate.fingerId == meleePointerId ||
                                candidate.fingerId == grenadePointerId ||
                                candidate.fingerId == reloadPointerId ||
                                candidate.position.x > Screen.width * 0.58f ||
                                candidate.position.y < Screen.height * 0.10f ||
                                candidate.phase == TouchPhase.Ended ||
                                candidate.phase == TouchPhase.Canceled)
                                continue;

                            movePointerId = candidate.fingerId;
                            moveTouchOriginScreen = candidate.position - moveValue * Mathf.Max(1f, radius);
                            movePointerMissingSince = -1f;
                            movementRecoveryActive = false;
                            player.SetMoveInput(ToWorldMove(ApplyDeadZone(moveValue)));
                            rebound = true;
                            break;
                        }

                        if (!rebound)
                        {
                            // Do not keep the canceled ID, but preserve the last movement
                            // vector while a second control (commonly FIRE) is still held.
                            // movePointerId becomes free so a fresh left-side touch can
                            // immediately take over and set a new direction.
                            movePointerId = -1;
                            movePointerMissingSince = Time.unscaledTime;
                            movementRecoveryActive = true;
                            guiMoveMouseButton = -1;
                        }
                    }
                    else
                    {
                        movePointerMissingSince = -1f;
                        Vector2 delta = movementTouch.position - moveTouchOriginScreen;
                        float touchRadius = Mathf.Max(1f, radius);
                        moveValue = Vector2.ClampMagnitude(delta / touchRadius, 1f);

                        Vector2 movement = ApplyDeadZone(moveValue);
                        player.SetMoveInput(ToWorldMove(movement));
                    }
                }
                else if (Input.touchCount > 0)
                {
                    // Recover the joystick if Android changes the active finger ID.
                    // Exclude all captured action contacts so the fire finger can never
                    // be rebound as the movement thumb.
                    float touchRadius = Mathf.Max(1f, radius);
                    bool movementRebound = false;
                    for (int i = 0; i < Input.touchCount; i++)
                    {
                        Touch candidate = Input.GetTouch(i);
                        if (candidate.fingerId == firePointerId ||
                            candidate.fingerId == meleePointerId ||
                            candidate.fingerId == grenadePointerId ||
                            candidate.fingerId == reloadPointerId ||
                            candidate.position.x > Screen.width * 0.58f ||
                            candidate.position.y < Screen.height * 0.10f ||
                            candidate.phase == TouchPhase.Ended ||
                            candidate.phase == TouchPhase.Canceled)
                            continue;

                        movePointerId = candidate.fingerId;
                        moveTouchOriginScreen = candidate.position - moveValue * touchRadius;
                        movePointerMissingSince = -1f;
                        movementRecoveryActive = false;
                        movementRebound = true;
                        break;
                    }

                    // If Android reports only the still-held fire finger, keep the last
                    // joystick vector rather than replacing movement with zero. A normal
                    // Ended event still releases movement immediately above.
                    if (!movementRebound && movePointerMissingSince < 0f)
                        movePointerMissingSince = Time.unscaledTime;

                    player.SetMoveInput(ToWorldMove(ApplyDeadZone(moveValue)));
                }
                else
                {
                    // A brief empty touch list is not enough evidence that the user
                    // lifted the joystick: some devices drop the list during a second
                    // finger transition. Keep movement during the recovery window, and
                    // while the independently captured fire finger is still held.
                    if (movePointerMissingSince < 0f)
                        movePointerMissingSince = Time.unscaledTime;

                    bool fireStillHeld = firePointerId >= 0 &&
                        TryGetTouch(firePointerId, out Touch heldFireTouch) &&
                        heldFireTouch.phase != TouchPhase.Ended &&
                        heldFireTouch.phase != TouchPhase.Canceled;

                    if (fireStillHeld ||
                        Time.unscaledTime - movePointerMissingSince <= MovePointerRecoveryGraceSeconds)
                    {
                        player.SetMoveInput(ToWorldMove(ApplyDeadZone(moveValue)));
                    }
                    else
                    {
                        ResetMovementPointer();
                    }
                }
            }
            else if (movementRecoveryActive)
            {
                bool fireStillHeld = firePointerId >= 0 &&
                    TryGetTouch(firePointerId, out Touch heldFireTouch) &&
                    heldFireTouch.phase != TouchPhase.Ended &&
                    heldFireTouch.phase != TouchPhase.Canceled;

                if (fireStillHeld ||
                    Time.unscaledTime - movePointerMissingSince <= MovePointerRecoveryGraceSeconds)
                {
                    // Keep the last vector only during a genuine input hand-off.
                    player.SetMoveInput(ToWorldMove(ApplyDeadZone(moveValue)));
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
                    if (fireTouch.phase == TouchPhase.Ended)
                    {
                        // Ended means the user lifted the fire finger: release now.
                        ReleaseFirePointer();
                    }
                    else if (fireTouch.phase == TouchPhase.Canceled)
                    {
                        // Android can retain the canceled record for several frames.
                        // Look for a different live right-side contact before treating
                        // this as a release, so a transient pointer hand-off cannot
                        // permanently drop held FIRE while the movement thumb is active.
                        if (firePointerMissingSince < 0f)
                            firePointerMissingSince = Time.unscaledTime;

                        bool fireRebound = false;
                        for (int i = 0; i < Input.touchCount; i++)
                        {
                            Touch candidate = Input.GetTouch(i);
                            if (candidate.fingerId == firePointerId ||
                                candidate.fingerId == movePointerId ||
                                candidate.fingerId == meleePointerId ||
                                candidate.fingerId == grenadePointerId ||
                                candidate.fingerId == reloadPointerId ||
                                candidate.rawPosition.x < Screen.width * 0.58f ||
                                candidate.phase == TouchPhase.Ended ||
                                candidate.phase == TouchPhase.Canceled)
                                continue;

                            firePointerId = candidate.fingerId;
                            firePointerMissingSince = -1f;
                            fireRebound = true;
                            StartupCheckpoint.Set("MobileFirePointerReboundAfterCancel");
                            FireAtNearestTarget();
                            break;
                        }

                        if (!fireRebound)
                        {
                            if (Time.unscaledTime - firePointerMissingSince <= FirePointerRecoveryGraceSeconds)
                                FireAtNearestTarget();
                            else
                                ReleaseFirePointer();
                        }
                    }
                    else if (fireTouch.phase == TouchPhase.Began ||
                             fireTouch.phase == TouchPhase.Moved ||
                             fireTouch.phase == TouchPhase.Stationary)
                    {
                        firePointerMissingSince = -1f;
                        FireAtNearestTarget();
                    }
                }
                else
                {
                    bool fireRebound = false;
                    if (Input.touchCount > 0)
                    {
                        // If Unity drops/re-numbers the fire pointer, only recover it
                        // from a remaining right-side finger. Never steal the joystick,
                        // melee, grenade, or reload contacts for firing.
                        for (int i = 0; i < Input.touchCount; i++)
                        {
                            Touch candidate = Input.GetTouch(i);
                            if (candidate.fingerId == movePointerId ||
                                candidate.fingerId == meleePointerId ||
                                candidate.fingerId == grenadePointerId ||
                                candidate.fingerId == reloadPointerId ||
                                candidate.position.x < Screen.width * 0.58f ||
                                candidate.phase == TouchPhase.Ended ||
                                candidate.phase == TouchPhase.Canceled)
                                continue;

                            firePointerId = candidate.fingerId;
                            firePointerMissingSince = -1f;
                            fireRebound = true;
                            FireAtNearestTarget();
                            break;
                        }
                    }

                    if (!fireRebound)
                    {
                        if (firePointerMissingSince < 0f)
                            firePointerMissingSince = Time.unscaledTime;

                        bool movementStillHeld = movePointerId >= 0 &&
                            TryGetTouch(movePointerId, out Touch heldMoveTouch) &&
                            heldMoveTouch.phase != TouchPhase.Ended &&
                            heldMoveTouch.phase != TouchPhase.Canceled;

                        // Preserve a held-fire action through a transient input-list gap
                        // while the movement finger remains down. A normal fire Ended
                        // event above always stops firing immediately.
                        if ((Input.touchCount > 0 && movementStillHeld) ||
                            Time.unscaledTime - firePointerMissingSince <= FirePointerRecoveryGraceSeconds)
                        {
                            FireAtNearestTarget();
                        }
                        else
                        {
                            ReleaseFirePointer();
                        }
                    }
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
            ReleasePointer(ref dashPointerId);

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

        private void MarkLateTouchRecovery(Touch touch)
        {
            if (touch.phase == TouchPhase.Began || lateTouchRecoveryCheckpointWritten)
                return;

            lateTouchRecoveryCheckpointWritten = true;
            StartupCheckpoint.Set("MobileLateTouchRecoveredFromActivePhase");
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
            movementRecoveryActive = false;

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
            movementRecoveryActive = false;

            ReleaseFirePointer();
            meleePointerId = -1;
            grenadePointerId = -1;
            reloadPointerId = -1;
            dashPointerId = -1;
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
            if (movePointerId >= 0 || movePointerId == -1000 || movementRecoveryActive)
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

            if (player != null && player.QuickDashCharges > 0)
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
            Rect volumeRect = new Rect(Screen.width * 0.5f - 116f * scale, 18f * scale, 232f * scale, 28f * scale);
            GUI.Box(volumeRect, GUIContent.none);
            GUI.Label(new Rect(volumeRect.x + 4f * scale, volumeRect.y, 58f * scale, volumeRect.height), "MUSIC", smallButtonHintStyle);
            musicVolume = GUI.HorizontalSlider(new Rect(volumeRect.x + 62f * scale, volumeRect.y + 7f * scale, 104f * scale, 16f * scale), musicVolume, 0f, 1f);
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
        }

        private void HandleGuiPointerFallback()
        {
            // Native finger IDs are acquired first by HandleAndroidTouches.
            // Keep this dispatcher active as a per-control fallback even when some
            // native contacts exist: suppressing it globally whenever touchCount > 0
            // drops a second finger if the legacy Touch API misses that contact.
            // Each mouse event below can claim only a still-unowned control, and
            // Drag/Up are accepted only by the exact mouse button that claimed it.
            Event e = Event.current;
            if (e == null)
                return;

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
                if (firePointerId == -1 &&
                    guiFireMouseButton == -1 &&
                    Vector2.Distance(gui, firePos) <= actionHitRadius * 1.02f)
                {
                    firePointerId = -1001;
                    guiFireMouseButton = mouseButton;
                    FireAtNearestTarget();
                    e.Use();
                    return;
                }

                if (meleePointerId == -1 &&
                    guiMeleeMouseButton == -1 &&
                    Vector2.Distance(gui, meleePos) <= actionHitRadius * 0.76f)
                {
                    meleePointerId = -1002;
                    guiMeleeMouseButton = mouseButton;
                    player.Weapon?.TryMelee();
                    e.Use();
                    return;
                }

                if (grenadePointerId == -1 &&
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