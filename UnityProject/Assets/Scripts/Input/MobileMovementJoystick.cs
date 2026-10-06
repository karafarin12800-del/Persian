using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    /// <summary>
    /// Primary mobile movement control for the 3D battlefield.
    /// The joystick is genuinely floating: its base appears where the player
    /// first touches the movement area and the knob follows that touch.
    /// </summary>
    public sealed class MobileMovementJoystick : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [SerializeField] private float radius = 112f;
        [SerializeField] private float deadZone = 0.08f;

        private int pointerId = -1;
        private Vector2 start;
        private Vector2 value;
        private bool matchActive;
        private bool matchPaused;
        private Camera movementCamera;
        private Texture2D circleTexture;
        private GUIStyle labelStyle;

        public void Configure(PlayerController target)
        {
            player = target;
        }

        public void SetMovementCamera(Camera camera)
        {
            movementCamera = camera;
        }

        public void SetPaused(bool paused)
        {
            matchPaused = paused;

            if (!paused)
                return;

            ResetPointer();
        }

        public void ActivateForMatch(PlayerController target)
        {
            player = target;
            movementCamera = Camera.main;
            if (movementCamera == null)
                movementCamera = FindFirstObjectByType<Camera>();

            matchPaused = false;
            matchActive = true;
            ResetPointer();

#if UNITY_ANDROID
            Input.multiTouchEnabled = true;
#endif
            enabled = true;
        }

        public void DeactivateMatch()
        {
            matchActive = false;
            ResetPointer();
        }

        private void Awake()
        {
            circleTexture = CreateCircleTexture(64);
            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
        }

        private void OnDestroy()
        {
            if (circleTexture != null)
                Destroy(circleTexture);
        }

        private void OnDisable()
        {
            if (player != null)
                player.SetMoveInput(Vector2.zero);
            pointerId = -1;
            value = Vector2.zero;
        }

        private void Update()
        {
            if (!matchActive || matchPaused || player == null)
                return;

            if (!Application.isMobilePlatform)
            {
                Vector2 keyboard = new Vector2(
                    Input.GetAxisRaw("Horizontal"),
                    Input.GetAxisRaw("Vertical"));

                if (keyboard.sqrMagnitude > 1f)
                    keyboard.Normalize();

                player.SetMoveInput(ApplyDeadZone(keyboard));
                return;
            }

            HandleTouchInput();
        }

        private void HandleTouchInput()
        {
            // The movement side is deliberately wider than the old 52% split.
            // This keeps floating movement comfortable on tall phones while the
            // right-side combat controls remain reserved for MobileInputHub.
            if (pointerId < 0)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    Touch beganTouch = Input.GetTouch(i);
                    if (beganTouch.phase != TouchPhase.Began)
                        continue;

                    if (beganTouch.position.x > Screen.width * 0.58f)
                        continue;

                    // Keep the top HUD/pause area free from movement capture.
                    if (beganTouch.position.y > Screen.height * 0.90f)
                        continue;

                    pointerId = beganTouch.fingerId;
                    start = ClampFloatingOrigin(beganTouch.position);
                    value = Vector2.zero;
                    break;
                }
            }

            if (pointerId < 0)
                return;

            if (!TryGetTouch(pointerId, out Touch touch))
            {
                ResetPointer();
                return;
            }

            Vector2 delta = touch.position - start;
            float scaledRadius = Mathf.Max(1f, GetScaledRadius());
            value = Vector2.ClampMagnitude(delta / scaledRadius, 1f);

            Vector2 movement = ApplyDeadZone(value);
            player.SetMoveInput(ToWorldMove(movement));

            if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                ResetPointer();
        }

        private Vector2 ApplyDeadZone(Vector2 inputValue)
        {
            float magnitude = inputValue.magnitude;
            if (magnitude <= deadZone)
                return Vector2.zero;

            float normalizedMagnitude = Mathf.InverseLerp(deadZone, 1f, magnitude);
            return inputValue.normalized * normalizedMagnitude;
        }

        private float GetScaledRadius()
        {
            float scale = Mathf.Clamp(
                Mathf.Min(Screen.width, Screen.height) / 1080f,
                0.75f,
                1.35f);
            return radius * scale;
        }

        private Vector2 ClampFloatingOrigin(Vector2 screenPosition)
        {
            float r = GetScaledRadius();
            float marginX = r * 0.72f;
            float marginY = r * 0.72f;

            float x = Mathf.Clamp(
                screenPosition.x,
                marginX,
                Screen.width * 0.58f - marginX);

            float y = Mathf.Clamp(
                screenPosition.y,
                marginY,
                Screen.height * 0.90f - marginY);

            return new Vector2(x, y);
        }

        private Vector2 ToWorldMove(Vector2 inputValue)
        {
            if (movementCamera == null)
                movementCamera = Camera.main;

            if (movementCamera == null)
                return inputValue;

            Vector3 forward = movementCamera.transform.forward;
            Vector3 right = movementCamera.transform.right;
            forward.y = 0f;
            right.y = 0f;

            if (forward.sqrMagnitude < 0.0001f || right.sqrMagnitude < 0.0001f)
                return inputValue;

            forward.Normalize();
            right.Normalize();

            Vector3 world = right * inputValue.x + forward * inputValue.y;
            return Vector2.ClampMagnitude(new Vector2(world.x, world.z), 1f);
        }

        private void ResetPointer()
        {
            pointerId = -1;
            start = Vector2.zero;
            value = Vector2.zero;

            if (player != null)
                player.SetMoveInput(Vector2.zero);
        }

        private void OnGUI()
        {
            if (!matchActive || matchPaused || !Application.isMobilePlatform || player == null)
                return;

            // Do not show a fixed joystick. The control is rendered only while a
            // movement touch is active, at the actual touch origin.
            if (pointerId < 0)
                return;

            float scale = Mathf.Clamp(
                Mathf.Min(Screen.width, Screen.height) / 1080f,
                0.75f,
                1.35f);
            float r = radius * scale;

            Vector2 basePos = new Vector2(start.x, Screen.height - start.y);
            Vector2 knobPos = basePos + new Vector2(value.x, -value.y) * r;

            DrawCircle(basePos, r, new Color(0f, 0f, 0f, 0.36f));
            DrawCircle(basePos, r, new Color(0.95f, 0.72f, 0.20f, 0.40f), true);
            DrawCircle(knobPos, r * 0.40f, new Color(0.96f, 0.98f, 1f, 0.86f));

            labelStyle.fontSize = Mathf.RoundToInt(16f * scale);
            labelStyle.normal.textColor = new Color(1f, 1f, 1f, 0.82f);
            GUI.Label(
                new Rect(basePos.x - r, basePos.y - labelStyle.fontSize * 0.65f,
                    r * 2f, labelStyle.fontSize * 1.4f),
                "MOVE",
                labelStyle);
        }

        private void DrawCircle(Vector2 center, float r, Color color, bool outline = false)
        {
            if (circleTexture == null)
                return;

            Color oldColor = GUI.color;
            GUI.color = color;

            float size = r * 2f;
            if (outline)
            {
                GUI.DrawTexture(
                    new Rect(center.x - r, center.y - r, size, size),
                    circleTexture,
                    ScaleMode.StretchToFill,
                    true);
            }
            else
            {
                GUI.DrawTexture(
                    new Rect(center.x - r, center.y - r, size, size),
                    circleTexture,
                    ScaleMode.StretchToFill,
                    true);
            }

            GUI.color = oldColor;
        }

        private static Texture2D CreateCircleTexture(int size)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
            float radius = size * 0.5f - 1f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x, y), center);
                    float alpha = Mathf.Clamp01(radius + 0.7f - distance);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply();
            return texture;
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