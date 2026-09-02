using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public sealed class MobileInputHub : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [SerializeField] private float joystickRadius = 120f;
        [SerializeField] private float minimapSize = 190f;
        [SerializeField] private float fireRepeatInterval = 0.155f;

        private int movePointerId = -1;
        private int firePointerId = -1;
        private Vector2 moveStartScreen;
        private Vector2 moveValue;
        private float nextFireTime;
        private Camera minimapCamera;
        private RenderTexture minimapTexture;
        private Texture2D markerTexture;

        public Vector2 MoveValue => moveValue;

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
            if (markerTexture != null) Destroy(markerTexture);
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

            if (Input.GetKeyDown(KeyCode.R)) player.Weapon?.Reload();
            if (Input.GetKeyDown(KeyCode.Space)) player.Weapon?.TryMelee();
            if (Input.GetMouseButton(0) && Time.time >= nextFireTime)
            {
                FireAtNearestTarget();
            }
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
                        moveValue = Vector2.zero;
                    }
                    else if (!leftSide && firePointerId < 0)
                    {
                        firePointerId = touch.fingerId;
                        FireAtNearestTarget();
                    }
                }
            }

            if (movePointerId >= 0 && TryGetTouch(movePointerId, out Touch moveTouch))
            {
                Vector2 delta = moveTouch.position - moveStartScreen;
                moveValue = Vector2.ClampMagnitude(delta / joystickRadius, 1f);
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

        private void CreateMinimap()
        {
            GameObject mapObject = new GameObject("MinimapCamera");
            mapObject.hideFlags = HideFlags.HideAndDontSave;
            minimapCamera = mapObject.AddComponent<Camera>();
            minimapCamera.orthographic = true;
            minimapCamera.orthographicSize = 96f;
            minimapCamera.nearClipPlane = 0.1f;
            minimapCamera.farClipPlane = 350f;
            minimapCamera.clearFlags = CameraClearFlags.SolidColor;
            minimapCamera.backgroundColor = new Color(0.06f, 0.07f, 0.08f, 1f);
            minimapCamera.enabled = true;

            minimapTexture = new RenderTexture(256, 256, 16, RenderTextureFormat.ARGB32);
            minimapTexture.name = "GameplayMinimap";
            minimapTexture.filterMode = FilterMode.Bilinear;
            minimapTexture.Create();
            minimapCamera.targetTexture = minimapTexture;

            markerTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            markerTexture.SetPixel(0, 0, Color.white);
            markerTexture.Apply();
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

            float scale = Mathf.Clamp(Mathf.Min(Screen.width, Screen.height) / 1080f, 0.75f, 1.35f);
            float radius = joystickRadius * scale;
            Vector2 defaultBase = new Vector2(120f * scale, Screen.height - 140f * scale);
            Vector2 basePos = movePointerId >= 0 ? moveStartScreen : defaultBase;

            DrawCircle(basePos, radius, new Color(0f, 0f, 0f, 0.24f));
            DrawCircle(basePos + moveValue * radius, radius * 0.42f, new Color(1f, 1f, 1f, 0.60f));

            Vector2 firePos = new Vector2(Screen.width - 120f * scale, Screen.height - 140f * scale);
            DrawCircle(firePos, radius * 0.72f, new Color(0.65f, 0.12f, 0.08f, firePointerId >= 0 ? 0.45f : 0.18f));
            DrawCircle(firePos, radius * 0.38f, new Color(1f, 1f, 1f, 0.58f));

            if (minimapTexture != null)
            {
                float size = minimapSize * scale;
                Rect rect = new Rect(Screen.width - size - 18f * scale, 18f * scale, size, size);
                GUI.DrawTexture(rect, minimapTexture, ScaleMode.StretchToFill, false);
                GUI.Box(rect, GUIContent.none);
                GUI.DrawTexture(new Rect(rect.center.x - 2f * scale, rect.center.y - 2f * scale, 4f * scale, 4f * scale), markerTexture);
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