using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    /// <summary>
    /// One mobile control layer for the same 3D gameplay scene.
    /// Left side = virtual movement joystick. Right side = camera orbit.
    /// Two-finger pinch = zoom. A small live top-down minimap is shown in the HUD.
    /// </summary>
    public sealed class MobileInputHub : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [SerializeField] private Camera gameplayCamera;
        [SerializeField] private CameraFollow25D cameraFollow;
        [SerializeField] private float joystickRadius = 120f;
        [SerializeField] private float cameraSensitivity = 0.55f;
        [SerializeField] private float minimapSize = 180f;

        private int movePointerId = -1;
        private int lookPointerId = -1;
        private Vector2 moveStartScreen;
        private Vector2 moveValue;
        private Vector2 lookLastScreen;
        private Camera minimapCamera;
        private RenderTexture minimapTexture;

        private void Awake()
        {
            if (player == null) player = FindFirstObjectByType<PlayerController>();
            if (gameplayCamera == null) gameplayCamera = Camera.main;
            if (cameraFollow == null && gameplayCamera != null) cameraFollow = gameplayCamera.GetComponent<CameraFollow25D>();
            CreateMinimap();
        }

        private void OnDestroy()
        {
            if (minimapTexture != null)
            {
                minimapTexture.Release();
                Destroy(minimapTexture);
            }
            if (minimapCamera != null) Destroy(minimapCamera.gameObject);
        }

        private void Update()
        {
            if (player == null) return;

#if UNITY_EDITOR || UNITY_STANDALONE
            Vector2 keyboard = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            player.SetMoveInput(ToCameraRelative(keyboard));
            return;
#endif

            HandleTouches();
            player.SetMoveInput(ToCameraRelative(moveValue));
            UpdateMinimap();
        }

        private void HandleTouches()
        {
            if (Input.touchCount == 0)
            {
                moveValue = Vector2.zero;
                movePointerId = -1;
                lookPointerId = -1;
                return;
            }

            // First touch on the left half controls movement; touch on the right half rotates the camera.
            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                bool leftSide = touch.position.x < Screen.width * 0.48f;

                if (touch.phase == TouchPhase.Began)
                {
                    if (leftSide && movePointerId < 0)
                    {
                        movePointerId = touch.fingerId;
                        moveStartScreen = touch.position;
                    }
                    else if (!leftSide && lookPointerId < 0)
                    {
                        lookPointerId = touch.fingerId;
                        lookLastScreen = touch.position;
                    }
                }
            }

            if (movePointerId >= 0 && TryGetTouch(movePointerId, out Touch moveTouch))
            {
                Vector2 delta = moveTouch.position - moveStartScreen;
                moveValue = Vector2.ClampMagnitude(delta / joystickRadius, 1f);
                if (moveTouch.phase == TouchPhase.Ended || moveTouch.phase == TouchPhase.Canceled)
                {
                    movePointerId = -1;
                    moveValue = Vector2.zero;
                }
            }
            else
            {
                moveValue = Vector2.zero;
                movePointerId = -1;
            }

            if (lookPointerId >= 0 && TryGetTouch(lookPointerId, out Touch lookTouch))
            {
                Vector2 delta = lookTouch.position - lookLastScreen;
                lookLastScreen = lookTouch.position;
                if (cameraFollow != null) cameraFollow.Rotate(delta.x * cameraSensitivity);
                if (lookTouch.phase == TouchPhase.Ended || lookTouch.phase == TouchPhase.Canceled)
                    lookPointerId = -1;
            }

            if (Input.touchCount >= 2 && cameraFollow != null)
            {
                Touch a = Input.GetTouch(0);
                Touch b = Input.GetTouch(1);
                float previous = (a.position - a.deltaPosition - (b.position - b.deltaPosition)).magnitude;
                float current = (a.position - b.position).magnitude;
                if (previous > 0.01f)
                    cameraFollow.Zoom((current - previous));
            }
        }

        private Vector2 ToCameraRelative(Vector2 input)
        {
            if (input.sqrMagnitude < 0.0001f || gameplayCamera == null) return Vector2.zero;

            Vector3 forward = gameplayCamera.transform.forward;
            Vector3 right = gameplayCamera.transform.right;
            forward.y = 0f;
            right.y = 0f;
            forward.Normalize();
            right.Normalize();

            Vector3 world = right * input.x + forward * input.y;
            return new Vector2(world.x, world.z);
        }

        private void CreateMinimap()
        {
            GameObject mapObject = new GameObject("MinimapCamera");
            minimapCamera = mapObject.AddComponent<Camera>();
            minimapCamera.orthographic = true;
            minimapCamera.orthographicSize = 96f;
            minimapCamera.nearClipPlane = 0.1f;
            minimapCamera.farClipPlane = 300f;
            minimapCamera.clearFlags = CameraClearFlags.SolidColor;
            minimapCamera.backgroundColor = new Color(0.08f, 0.09f, 0.1f, 1f);
            minimapCamera.depth = -20;
            minimapTexture = new RenderTexture(256, 256, 16, RenderTextureFormat.ARGB32);
            minimapTexture.name = "GameplayMinimap";
            minimapTexture.Create();
            minimapCamera.targetTexture = minimapTexture;
        }

        private void UpdateMinimap()
        {
            if (minimapCamera == null || player == null) return;
            minimapCamera.transform.position = player.transform.position + Vector3.up * 90f;
            minimapCamera.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }

        private void OnGUI()
        {
            if (!Application.isMobilePlatform && !Application.isEditor) return;

            float scale = Mathf.Min(Screen.width, Screen.height) / 1080f;
            float radius = joystickRadius * scale;
            Vector2 basePos = movePointerId >= 0 ? moveStartScreen : new Vector2(120f * scale, Screen.height - 140f * scale);
            DrawCircle(basePos, radius, new Color(0f, 0f, 0f, 0.24f));
            DrawCircle(basePos + moveValue * radius, radius * 0.42f, new Color(1f, 1f, 1f, 0.55f));

            if (minimapTexture != null)
            {
                float size = minimapSize * scale;
                Rect rect = new Rect(Screen.width - size - 18f * scale, 18f * scale, size, size);
                GUI.DrawTexture(rect, minimapTexture, ScaleMode.StretchToFill, false);
                GUI.Box(rect, "");
            }
        }

        private static void DrawCircle(Vector2 center, float radius, Color color)
        {
            Texture2D texture = Texture2D.whiteTexture;
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f), texture);
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
