using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public sealed class MobileInputHub : MonoBehaviour
    {
        [SerializeField] private PlayerController player;
        [SerializeField] private float joystickRadius = 120f;
        [SerializeField] private float minimapSize = 190f;
        [SerializeField] private float fireRepeatInterval = 0.155f;

        // Legacy serialized fields kept intentionally so older scenes load without stale-field warnings.
        [SerializeField] private Camera gameplayCamera;
        [SerializeField] private float moveRadius = 140f;

        private int movePointerId = -1;
        private int firePointerId = -1;
        private int grenadePointerId = -1;
        private Vector2 moveStartScreen;
        private Vector2 moveValue;
        private float nextFireTime;
        private Camera minimapCamera;
        private RenderTexture minimapTexture;
        private Texture2D circleTexture;
        private Texture2D lineTexture;
        private GrenadeController grenadeController;

        public Vector2 MoveValue => moveValue;

        private void Awake()
        {
            if (player == null) player = FindFirstObjectByType<PlayerController>();
            if (gameplayCamera == null) gameplayCamera = Camera.main;
            if (player != null) grenadeController = player.Grenades;
            if (moveRadius > 0f) joystickRadius = Mathf.Clamp(moveRadius * 0.86f, 90f, 150f);
            CreateMinimap();
            CreateGuiTextures();
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
            if (player == null)
            {
                player = FindFirstObjectByType<PlayerController>();
                if (player == null) return;
                grenadeController = player.Grenades;
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
            Vector2 grenadePos = new Vector2(Screen.width - 245f * scale, Screen.height - 245f * scale);
            float grenadeRadius = radius * 0.56f;

            for (int i = 0; i < Input.touchCount; i++)
            {
                Touch touch = Input.GetTouch(i);
                if (touch.phase != TouchPhase.Began) continue;

                bool leftSide = touch.position.x < Screen.width * 0.48f;
                if (leftSide && movePointerId < 0)
                {
                    movePointerId = touch.fingerId;
                    moveStartScreen = touch.position;
                    moveValue = Vector2.zero;
                    continue;
                }

                if (!leftSide && grenadePointerId < 0 && Vector2.Distance(touch.position, grenadePos) <= grenadeRadius)
                {
                    grenadePointerId = touch.fingerId;
                    ThrowGrenadeAtTarget();
                    continue;
                }

                if (!leftSide && firePointerId < 0)
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
        }

        private void CreateGuiTextures()
        {
            circleTexture = new Texture2D(128, 128, TextureFormat.RGBA32, false);
            lineTexture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            lineTexture.SetPixel(0, 0, Color.white);
            lineTexture.Apply();

            Vector2 center = new Vector2(63.5f, 63.5f);
            float radius = 63f;
            for (int y = 0; y < 128; y++)
            {
                for (int x = 0; x < 128; x++)
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

            float scale = Mathf.Clamp(Mathf.Min(Screen.width, Screen.height) / 1080f, 0.75f, 1.35f);
            float radius = joystickRadius * scale;
            Vector2 defaultBase = new Vector2(120f * scale, Screen.height - 140f * scale);
            Vector2 basePos = movePointerId >= 0 ? moveStartScreen : defaultBase;
            Vector2 firePos = new Vector2(Screen.width - 120f * scale, Screen.height - 140f * scale);
            Vector2 grenadePos = new Vector2(Screen.width - 245f * scale, Screen.height - 245f * scale);

            DrawCircle(basePos, radius, new Color(0f, 0f, 0f, 0.34f));
            DrawCircle(basePos + moveValue * radius, radius * 0.42f, new Color(1f, 1f, 1f, 0.72f));

            DrawCircle(firePos, radius * 0.72f, new Color(0.65f, 0.12f, 0.08f, firePointerId >= 0 ? 0.50f : 0.28f));
            DrawCircle(firePos, radius * 0.38f, new Color(1f, 1f, 1f, 0.60f));

            DrawCircle(grenadePos, radius * 0.56f, new Color(0.32f, 0.24f, 0.10f, grenadePointerId >= 0 ? 0.55f : 0.32f));
            GUIStyle buttonText = new GUIStyle(GUI.skin.label)
            {
                fontSize = Mathf.RoundToInt(20f * scale),
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter
            };
            GUI.Label(new Rect(grenadePos.x - radius * 0.5f, grenadePos.y - radius * 0.5f, radius, radius), "G", buttonText);
            GUI.Label(new Rect(firePos.x - radius, firePos.y + radius * 0.52f, radius * 2f, 26f * scale), "AIM / FIRE", buttonText);
            GUI.Label(new Rect(basePos.x - radius, basePos.y + radius * 0.52f, radius * 2f, 26f * scale), "MOVE", buttonText);

            DrawAimGuide(scale);

            if (minimapTexture != null)
            {
                float size = minimapSize * scale;
                Rect rect = new Rect(Screen.width - size - 18f * scale, 18f * scale, size, size);
                GUI.DrawTexture(rect, minimapTexture, ScaleMode.StretchToFill, false);
                GUI.Box(rect, GUIContent.none);
            }
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
