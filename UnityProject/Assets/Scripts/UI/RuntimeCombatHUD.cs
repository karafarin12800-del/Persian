using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    /// <summary>
    /// Lightweight runtime HUD styled for a colorful mobile shooter.
    /// It only reads existing gameplay state.
    /// </summary>
    public sealed class RuntimeCombatHUD : MonoBehaviour
    {
        [SerializeField] private PlayerController player;

        private GUIStyle small;
        private GUIStyle medium;
        private GUIStyle bold;
        private Texture2D pixel;
        private EnemyChase[] radarEnemies = System.Array.Empty<EnemyChase>();
        private float nextRadarRefreshTime;
        private const float RadarWorldRadius = 55f;

        public void ConfigurePlayer(PlayerController value)
        {
            player = value;
        }

        private void Awake()
        {
            if (player == null) player = FindFirstObjectByType<PlayerController>();

            pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            pixel.SetPixel(0, 0, Color.white);
            pixel.Apply();

            small = new GUIStyle(GUI.skin.label)
            {
                fontSize = 18,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            medium = new GUIStyle(small) { fontSize = 24 };
            bold = new GUIStyle(small)
            {
                fontSize = 28,
                alignment = TextAnchor.MiddleCenter
            };
        }

        private void OnDestroy()
        {
            if (pixel != null) Destroy(pixel);
        }

        private void Update()
        {
            if (Time.unscaledTime < nextRadarRefreshTime) return;
            nextRadarRefreshTime = Time.unscaledTime + 0.5f;
            radarEnemies = FindObjectsByType<EnemyChase>(FindObjectsSortMode.None);
        }

        private void OnGUI()
        {
            if (player == null) player = FindFirstObjectByType<PlayerController>();
            if (player == null || pixel == null) return;

            TargetHealth health = player.Health;
            WeaponController weapon = player.Weapon;
            PlayerInventory inventory = player.Inventory;
            EnemySpawner spawner = FindFirstObjectByType<EnemySpawner>();
            GameSession session = GameSession.Instance;

            int currentHealth = health != null ? health.CurrentHealth : 0;
            int maxHealth = health != null ? health.MaxHealth : 0;
            float hp = maxHealth > 0 ? currentHealth / (float)maxHealth : 0f;
            int shieldAmount = player.Shield;
            float shield = Mathf.Clamp01(shieldAmount / 100f);
            int ammo = weapon != null ? weapon.Magazine : 0;
            int reserve = weapon != null ? weapon.Reserve : 0;
            int grenades = inventory != null ? inventory.Grenades : 0;
            int wave = spawner != null ? spawner.CurrentWave : 0;
            int score = session != null ? session.Score : 0;

            float scale = Mathf.Clamp(Screen.height / 720f, 0.75f, 1.35f);
            float margin = 22f * scale;

            DrawTopStatus(margin, scale, hp, currentHealth, maxHealth, shield, shieldAmount, ammo, reserve, grenades);
            DrawCounters(margin, scale, wave, score);
            DrawEnemyRadar(margin, scale);

            if (player.IsDefeated)
            {
                float w = 400f * scale;
                Rect panel = new Rect(Screen.width * 0.5f - w * 0.5f, Screen.height * 0.5f - 48f * scale, w, 110f * scale);
                Fill(panel, new Color(0.05f, 0.06f, 0.08f, 0.86f));
                GUI.Label(panel, "GAME OVER", bold);
            }
        }

        private void DrawTopStatus(float margin, float scale, float hp, int currentHealth, int maxHealth, float shield, int shieldAmount, int ammo, int reserve, int grenades)
        {
            float panelW = 400f * scale;
            float panelH = 110f * scale;
            Rect panel = new Rect(margin, margin, panelW, panelH);
            Fill(panel, new Color(0.05f, 0.08f, 0.12f, 0.74f));

            Rect portrait = new Rect(panel.x + 10f * scale, panel.y + 10f * scale, 62f * scale, 62f * scale);
            Fill(portrait, new Color(0.90f, 0.40f, 0.16f, 0.96f));
            GUI.Label(portrait, "P", bold);

            float barX = portrait.xMax + 10f * scale;
            float barWidth = panelW - (barX - panel.x) - 12f * scale;
            GUI.Label(new Rect(barX, panel.y + 5f * scale, barWidth, 29f * scale), "PERSIA WARRIOR", medium);

            GUI.Label(new Rect(barX, panel.y + 35f * scale, barWidth, 20f * scale),
                "HP  " + currentHealth + " / " + maxHealth, small);
            DrawBar(new Rect(barX, panel.y + 55f * scale, barWidth, 9f * scale),
                hp, new Color(0.25f, 0.90f, 0.36f));

            GUI.Label(new Rect(barX, panel.y + 66f * scale, barWidth, 20f * scale),
                "SHIELD  " + shieldAmount + " / 100", small);
            DrawBar(new Rect(barX, panel.y + 87f * scale, barWidth, 9f * scale),
                shield, new Color(0.30f, 0.66f, 1f));

            float itemY = panel.yMax + 8f * scale;
            DrawChip(new Rect(panel.x, itemY, 120f * scale, 36f * scale), "⚡  " + grenades, new Color(0.13f, 0.30f, 0.15f));
            DrawChip(new Rect(panel.x + 128f * scale, itemY, 190f * scale, 36f * scale), "AMMO  " + ammo + "/" + reserve, new Color(0.24f, 0.19f, 0.08f));
        }

        private void DrawCounters(float margin, float scale, int wave, int score)
        {
            float w = 200f * scale;
            Rect waveRect = new Rect(Screen.width - w - margin, margin, w, 38f * scale);
            DrawChip(waveRect, "WAVE  " + wave, new Color(0.10f, 0.12f, 0.18f));

            Rect scoreRect = new Rect(Screen.width - w - margin, waveRect.yMax + 8f * scale, w, 38f * scale);
            DrawChip(scoreRect, "SCORE  " + score, new Color(0.18f, 0.10f, 0.10f));
        }

        private void DrawEnemyRadar(float margin, float scale)
        {
            if (player == null) return;

            // Compact north-up tactical minimap. Blue dots are enemies; the warm
            // center dot is the player. Refreshing the enemy cache at 2 Hz keeps
            // this affordable on Android.
            float size = 132f * scale;
            float x = Screen.width - size - margin;
            float y = margin + 84f * scale;
            Rect panel = new Rect(x, y, size, size);
            Fill(panel, new Color(0.025f, 0.06f, 0.10f, 0.90f));
            Fill(new Rect(x + 2f, y + 2f, size - 4f, 2f * scale), new Color(0.24f, 0.66f, 0.92f, 0.95f));
            GUI.Label(new Rect(x + 7f * scale, y + 3f * scale, size - 14f * scale, 22f * scale),
                "RADAR  •  ENEMIES", small);

            float mapLeft = x + 8f * scale;
            float mapTop = y + 28f * scale;
            float mapSize = size - 16f * scale;
            float centerX = mapLeft + mapSize * 0.5f;
            float centerY = mapTop + mapSize * 0.5f;
            Fill(new Rect(mapLeft, mapTop, mapSize, mapSize), new Color(0.08f, 0.13f, 0.17f, 0.95f));
            Fill(new Rect(centerX - 0.5f * scale, mapTop, 1f * scale, mapSize), new Color(0.32f, 0.43f, 0.48f, 0.50f));
            Fill(new Rect(mapLeft, centerY - 0.5f * scale, mapSize, 1f * scale), new Color(0.32f, 0.43f, 0.48f, 0.50f));

            Vector3 playerPosition = player.transform.position;
            float pixelsPerUnit = mapSize / (RadarWorldRadius * 2f);
            for (int i = 0; i < radarEnemies.Length; i++)
            {
                EnemyChase enemy = radarEnemies[i];
                if (enemy == null || !enemy.isActiveAndEnabled) continue;
                Vector3 offset = enemy.transform.position - playerPosition;
                if (Mathf.Abs(offset.x) > RadarWorldRadius || Mathf.Abs(offset.z) > RadarWorldRadius) continue;

                float dotX = centerX + offset.x * pixelsPerUnit;
                float dotY = centerY - offset.z * pixelsPerUnit;
                float dotSize = 7f * scale;
                Fill(new Rect(dotX - dotSize * 0.5f, dotY - dotSize * 0.5f, dotSize, dotSize),
                    new Color(0.08f, 0.55f, 1f, 1f));
                Fill(new Rect(dotX - dotSize * 0.5f - scale, dotY - dotSize * 0.5f - scale,
                    dotSize + 2f * scale, dotSize + 2f * scale), new Color(0.30f, 0.78f, 1f, 0.35f));
            }

            float playerDot = 8f * scale;
            Fill(new Rect(centerX - playerDot * 0.5f, centerY - playerDot * 0.5f, playerDot, playerDot),
                new Color(1f, 0.66f, 0.18f, 1f));
        }

        private void DrawBar(Rect rect, float value, Color fill)
        {
            Fill(rect, new Color(0f, 0f, 0f, 0.46f));
            Rect inside = new Rect(rect.x + 2f, rect.y + 2f, Mathf.Max(0f, rect.width - 4f) * Mathf.Clamp01(value), Mathf.Max(0f, rect.height - 4f));
            Fill(inside, fill);
        }

        private void DrawChip(Rect rect, string text, Color color)
        {
            Fill(rect, new Color(0.03f, 0.04f, 0.06f, 0.70f));
            Fill(new Rect(rect.x + 2f, rect.y + 2f, 5f, rect.height - 4f), color);
            GUI.Label(new Rect(rect.x + 12f, rect.y, rect.width - 12f, rect.height), text, small);
        }

        private void Fill(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, pixel);
            GUI.color = previous;
        }
    }
}
