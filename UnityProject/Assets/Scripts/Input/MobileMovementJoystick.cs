using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    /// <summary>
    /// Presentation-safe mobile movement control for the 3D battlefield.
    /// It only forwards a virtual joystick vector to PlayerController.
    /// </summary>
    public sealed class MobileMovementJoystick : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [SerializeField] private float radius = 110f;

        private int pointerId = -1;
        private Vector2 start;
        private Vector2 value;

        private bool matchActive;
        private Camera movementCamera;

        public void Configure(PlayerController target)
        {
            player = target;
        }

        public void ActivateForMatch(PlayerController target)
        {
            player = target;
            movementCamera = Camera.main;
            if (movementCamera == null)
                movementCamera = FindFirstObjectByType<Camera>();
            pointerId = -1;
            start = Vector2.zero;
            value = Vector2.zero;
#if UNITY_ANDROID
            Input.multiTouchEnabled = true;
#endif
            matchActive = true;
            enabled = true;
        }

        public void DeactivateMatch()
        {
            matchActive = false;
            pointerId = -1;
            value = Vector2.zero;
            if (player != null)
                player.SetMoveInput(Vector2.zero);
        }

        private void Update()
        {
            if (!matchActive || player == null)
                return;

            if (!Application.isMobilePlatform)
            {
                Vector2 keyboard = new Vector2(
                    Input.GetAxisRaw("Horizontal"),
                    Input.GetAxisRaw("Vertical"));
                player.SetMoveInput(Vector2.ClampMagnitude(keyboard, 1f));
                return;
            }

            if (pointerId < 0)
            {
                for (int i = 0; i < Input.touchCount; i++)
                {
                    Touch touch = Input.GetTouch(i);
                    if (touch.phase != TouchPhase.Began || touch.position.x > Screen.width * 0.52f)
                        continue;

                    pointerId = touch.fingerId;
                    start = touch.position;
                    value = Vector2.zero;
                    break;
                }
            }

            if (pointerId >= 0)
            {
                bool found = false;
                for (int i = 0; i < Input.touchCount; i++)
                {
                    Touch touch = Input.GetTouch(i);
                    if (touch.fingerId != pointerId)
                        continue;

                    found = true;
                    Vector2 delta = touch.position - start;
                    value = Vector2.ClampMagnitude(delta / Mathf.Max(1f, radius), 1f);
                    player.SetMoveInput(ToWorldMove(value));

                    if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                    {
                        pointerId = -1;
                        value = Vector2.zero;
                        player.SetMoveInput(Vector2.zero);
                    }
                    break;
                }

                if (!found)
                {
                    pointerId = -1;
                    value = Vector2.zero;
                    player.SetMoveInput(Vector2.zero);
                }
            }
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

        private void OnGUI()
        {
            if (!matchActive || !Application.isMobilePlatform || player == null)
                return;

            float scale = Mathf.Clamp(Mathf.Min(Screen.width, Screen.height) / 1080f, 0.75f, 1.35f);
            float r = radius * scale;
            Vector2 basePos = pointerId >= 0
                ? new Vector2(start.x, Screen.height - start.y)
                : new Vector2(112f * scale, Screen.height - 128f * scale);

            Vector2 knob = basePos + new Vector2(value.x, -value.y) * r;
            DrawCircle(basePos, r, new Color(0f, 0f, 0f, 0.30f));
            DrawCircle(knob, r * 0.38f, new Color(1f, 1f, 1f, 0.72f));

            GUI.Label(
                new Rect(basePos.x - r, basePos.y + r * 0.62f, r * 2f, 28f * scale),
                "MOVE",
                new GUIStyle(GUI.skin.label)
                {
                    fontSize = Mathf.RoundToInt(17f * scale),
                    fontStyle = FontStyle.Bold,
                    alignment = TextAnchor.MiddleCenter
                });
        }

        private static void DrawCircle(Vector2 center, float r, Color color)
        {
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(
                new Rect(center.x - r, center.y - r, r * 2f, r * 2f),
                Texture2D.whiteTexture);
            GUI.color = old;
        }
    }
}
