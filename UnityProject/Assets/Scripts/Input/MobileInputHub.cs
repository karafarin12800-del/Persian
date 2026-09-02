using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    /// <summary>
    /// Mobile gameplay controls: left side = continuous virtual joystick,
    /// right side = fire/aim. The gameplay camera stays fixed in isometric 2.5D.
    /// </summary>
    public sealed class MobileInputHub : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [SerializeField] private float joystickRadius = 120f;
        [SerializeField] private float fireInterval = 0.12f;
        [SerializeField] private float minimapSize = 180f;

        private int movePointerId = -1;
        private int firePointerId = -1;
        private Vector2 moveStartScreen;
        private Vector2 moveValue;
        private float nextFireTime;
        private Camera minimapCamera;
        private RenderTexture minimapTexture;

        private void Awake()
        {
            if (player == null) player = FindFirstObjectByType<PlayerController>();
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
            if (player == null)
            {
                player = FindFirstObjectByType<PlayerController>();
                if (player == null) return;
            }

#if UNITY_EDITOR || UNITY_STANDALONE
            Vector2 keyboard = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));
            if (keyboard.sqrMagnitude > 1f) keyboard.Normalize();
            player.SetMoveInput(keyboard);

            if (Input.GetMouseButton(0) && Time.time >= nextFireTime)
                FireAtNearestTargetOrForward();
#else
            HandleTouches();
#endif

            UpdateMinimap();
        }

        private void HandleTouches()
        {
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
                    else if (!leftSide && firePointerId < 0)
                    {
                        firePointerId = touch.fingerId;
                        FireAtNearestTargetOrForward();
                    }
                }
            }

            // The movement vector is persistent while the same finger remains down.
            // The player therefore keeps moving without repeated dragging.
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
            else if (movePointerId >= 0)
            {
                movePointerId = -1;
                moveValue = Vector2.zero;
            }

            player.SetMoveInput(moveValue);

            if (firePointerId >= 0 && TryGetTouch(firePointerId, out Touch fireTouch))
            {
                if (Time.time >= nextFireTime)
                    FireAtNearestTargetOrForward();

                if (fireTouch.phase == TouchPhase.Ended || fireTouch.phase == TouchPhase.Canceled)
                    firePointerId = -1;
            }
            else if (firePointerId >= 0)
            {
                firePointerId = -1;
            }
        }

        private void FireAtNearestTargetOrForward()
        {
            if (player == null || player.Aim == null || Time.time < nextFireTime)
                return;

            TargetHealth target = player.Aim.CurrentTarget;
            if (target != null)
            {
                if (player.Aim.FireAt(target.transform.position))
                    nextFireTime = Time.time + fireInterval;
                return;
            }

            Vector3 forwardTarget = player.transform.position + player.transform.forward * 25f;
            if (player.Aim.FireAt(forwardTarget))
                nextFireTime = Time.time + fireInterval;
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

            if (firePointerId >= 0)
            {
                Vector2 firePos = new Vector2(Screen.width - 120f * scale, Screen.height - 140f * scale);
                DrawCircle(firePos, radius * 0.72f, new Color(0.65f, 0.12f, 0.08f, 0.38f));
                DrawCircle(firePos, radius * 0.38f, new Color(1f, 1f, 1f, 0.60f));
            }

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
            Color old = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(new Rect(center.x - radius, center.y - radius, radius * 2f, radius * 2f), Texture2D.whiteTexture);
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
