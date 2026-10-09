using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    /// <summary>
    /// Screen-space enemy health number/bar. IMGUI is deliberately used so building
    /// geometry cannot depth-occlude the enemy's current health indicator.
    /// </summary>
    public sealed class EnemyHealthBar : MonoBehaviour
    {
        [SerializeField] private float maxVisibleDistance = 26f;
        private TargetHealth health;
        private GUIStyle labelStyle;
        private static Camera sharedWorldCamera;
        private static float nextCameraSearchTime;

        private void Awake()
        {
            health = GetComponent<TargetHealth>();
            ResolveWorldCamera();
        }

        private static Camera ResolveWorldCamera()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null && mainCamera.isActiveAndEnabled)
            {
                sharedWorldCamera = mainCamera;
                return sharedWorldCamera;
            }

            if (sharedWorldCamera != null &&
                sharedWorldCamera.isActiveAndEnabled &&
                sharedWorldCamera.gameObject.activeInHierarchy)
                return sharedWorldCamera;

            if (Time.unscaledTime < nextCameraSearchTime)
                return sharedWorldCamera;

            nextCameraSearchTime = Time.unscaledTime + 0.25f;
            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
            Camera best = null;
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera candidate = cameras[i];
                if (candidate == null ||
                    !candidate.isActiveAndEnabled ||
                    !candidate.gameObject.activeInHierarchy)
                    continue;

                if (candidate.CompareTag("MainCamera"))
                {
                    best = candidate;
                    break;
                }

                if (best == null || candidate.depth > best.depth)
                    best = candidate;
            }

            sharedWorldCamera = best;
            return sharedWorldCamera;
        }

        private void OnGUI()
        {
            if (health == null)
                health = GetComponent<TargetHealth>();

            if (health == null || health.CurrentHealth <= 0 || health.MaxHealth <= 0)
                return;

            Camera worldCamera = ResolveWorldCamera();
            if (worldCamera == null)
                return;

            // The enemy root is grounded at y=1. This offset places the label near
            // the top of the character rather than floating a long way above it.
            Vector3 worldPoint = transform.position + Vector3.up * 2.0f;
            Vector3 cameraToEnemy = worldPoint - worldCamera.transform.position;
            float cameraDistance = cameraToEnemy.magnitude;
            if (cameraDistance > Mathf.Max(1f, maxVisibleDistance))
                return;

            // Screen-space bars must still respect scene visibility. Hide the bar if
            // a wall, building, tree, or other collider is between camera and enemy.
            if (cameraDistance > 0.01f && Physics.Raycast(
                worldCamera.transform.position,
                cameraToEnemy / cameraDistance,
                out RaycastHit obstruction,
                cameraDistance,
                ~0,
                QueryTriggerInteraction.Ignore))
            {
                Transform hitTransform = obstruction.collider != null ? obstruction.collider.transform : null;
                if (hitTransform != transform && (hitTransform == null || !hitTransform.IsChildOf(transform)))
                    return;
            }

            Vector3 screenPoint = worldCamera.WorldToScreenPoint(worldPoint);
            if (screenPoint.z <= 0f ||
                screenPoint.x < 0f || screenPoint.x > Screen.width ||
                screenPoint.y < 0f || screenPoint.y > Screen.height)
                return;

            float scale = Mathf.Clamp(Screen.height / 720f, 0.70f, 1.25f);
            float width = 78f * scale;
            float barHeight = 8f * scale;
            float labelHeight = 17f * scale;
            float x = Mathf.Clamp(screenPoint.x - width * 0.5f, 2f, Screen.width - width - 2f);
            float guiY = Screen.height - screenPoint.y;
            float labelY = guiY - labelHeight - barHeight - 4f * scale;

            Rect labelRect = new Rect(x, labelY, width, labelHeight);
            Rect barRect = new Rect(x, labelRect.yMax + 1f * scale, width, barHeight);

            // Screen-space GUI is rendered independently from scene depth. A negative
            // depth also prioritizes this label over normal-depth gameplay HUD elements.
            int previousDepth = GUI.depth;
            GUI.depth = -50;
            DrawRect(labelRect, new Color(0.02f, 0.025f, 0.035f, 0.94f));
            if (labelStyle == null)
            {
                labelStyle = new GUIStyle(GUI.skin.label)
                {
                    alignment = TextAnchor.MiddleCenter,
                    fontStyle = FontStyle.Bold
                };
            }

            labelStyle.fontSize = Mathf.Clamp(Mathf.RoundToInt(12f * scale), 9, 16);
            labelStyle.normal.textColor = Color.white;
            GUI.Label(labelRect, health.CurrentHealth + " / " + health.MaxHealth, labelStyle);

            DrawRect(barRect, new Color(0f, 0f, 0f, 0.94f));
            Rect innerRect = new Rect(
                barRect.x + 1f * scale,
                barRect.y + 1f * scale,
                Mathf.Max(0f, barRect.width - 2f * scale),
                Mathf.Max(0f, barRect.height - 2f * scale));

            float ratio = Mathf.Clamp01(health.CurrentHealth / (float)health.MaxHealth);
            innerRect.width *= ratio;
            Color fill = ratio > 0.55f
                ? new Color(0.24f, 0.91f, 0.35f, 1f)
                : (ratio > 0.25f
                    ? new Color(1f, 0.69f, 0.18f, 1f)
                    : new Color(0.94f, 0.22f, 0.16f, 1f));
            DrawRect(innerRect, fill);
            GUI.depth = previousDepth;
        }

        private static void DrawRect(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}
