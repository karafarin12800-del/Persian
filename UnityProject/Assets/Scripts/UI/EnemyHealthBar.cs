using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    /// <summary>
    /// Displays a lightweight world-projected health bar above runtime-spawned enemies.
    /// Uses Unity's built-in white texture and OnGUI to avoid canvas/prefab dependencies.
    /// </summary>
    public sealed class EnemyHealthBar : MonoBehaviour
    {
        private TargetHealth health;
        private Camera worldCamera;
        private GUIStyle labelStyle;

        private void Awake()
        {
            health = GetComponent<TargetHealth>();
            worldCamera = Camera.main;
        }

        private void OnGUI()
        {
            if (health == null)
                health = GetComponent<TargetHealth>();

            if (health == null || health.CurrentHealth <= 0 || health.MaxHealth <= 0)
                return;

            if (worldCamera == null)
                worldCamera = Camera.main;

            if (worldCamera == null)
                return;

            Vector3 worldPoint = transform.position + Vector3.up * 2.45f;
            Vector3 screenPoint = worldCamera.WorldToScreenPoint(worldPoint);
            if (screenPoint.z <= 0f ||
                screenPoint.x < 0f || screenPoint.x > Screen.width ||
                screenPoint.y < 0f || screenPoint.y > Screen.height)
                return;

            float scale = Mathf.Clamp(Screen.height / 720f, 0.70f, 1.25f);
            float width = 72f * scale;
            float barHeight = 8f * scale;
            float labelHeight = 15f * scale;
            float x = Mathf.Clamp(screenPoint.x - width * 0.5f, 2f, Screen.width - width - 2f);
            float guiY = Screen.height - screenPoint.y;
            float labelY = guiY - labelHeight - barHeight - 4f * scale;

            Rect labelRect = new Rect(x, labelY, width, labelHeight);
            Rect barRect = new Rect(x, labelRect.yMax + 1f * scale, width, barHeight);

            DrawRect(labelRect, new Color(0.02f, 0.025f, 0.035f, 0.86f));
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

            DrawRect(barRect, new Color(0f, 0f, 0f, 0.88f));
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