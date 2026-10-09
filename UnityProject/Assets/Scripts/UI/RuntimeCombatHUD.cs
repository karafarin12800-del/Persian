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
                fontSize = 15,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleLeft
            };
            medium = new GUIStyle(small) { fontSize = 20 };
            bold = new GUIStyle(small)
            {
                fontSize = 24,
                alignment = TextAnchor.MiddleCenter
            };
        }

        private void OnDestroy()
        {
            if (pixel != null) Destroy(pixel);
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

            if (player.IsDefeated)
            {
                float w = 360f * scale;
                Rect panel = new Rect(Screen.width * 0.5f - w * 0.5f, Screen.height * 0.5f - 48f * scale, w, 96f * scale);
                Fill(panel, new Color(0.05f, 0.06f, 0.08f, 0.86f));
                GUI.Label(panel, "GAME OVER", bold);
            }
        }

        private void DrawTopStatus(float margin, float scale, float hp, int currentHealth, int maxHealth, float shield, int shieldAmount, int ammo, int reserve, int grenades)
        {
            float panelW = 360f * scale;
            float panelH = 96f * scale;
            Rect panel = new Rect(margin, margin, panelW, panelH);
            Fill(panel, new Color(0.05f, 0.08f, 0.12f, 0.74f));

            Rect portrait = new Rect(panel.x + 10f * scale, panel.y + 10f * scale, 58f * scale, 58f * scale);
            Fill(portrait, new Color(0.90f, 0.40f, 0.16f, 0.96f));
            GUI.Label(portrait, "P", bold);

            float barX = portrait.xMax + 10f * scale;
            float barWidth = panelW - (barX - panel.x) - 12f * scale;
            GUI.Label(new Rect(barX, panel.y + 5f * scale, barWidth, 25f * scale), "PERSIA WARRIOR", medium);

            GUI.Label(new Rect(barX, panel.y + 31f * scale, barWidth, 17f * scale),
                "HP  " + currentHealth + " / " + maxHealth, small);
            DrawBar(new Rect(barX, panel.y + 48f * scale, barWidth, 9f * scale),
                hp, new Color(0.25f, 0.90f, 0.36f));

            GUI.Label(new Rect(barX, panel.y + 58f * scale, barWidth, 17f * scale),
                "SHIELD  " + shieldAmount + " / 100", small);
            DrawBar(new Rect(barX, panel.y + 75f * scale, barWidth, 9f * scale),
                shield, new Color(0.30f, 0.66f, 1f));

            float itemY = panel.yMax + 8f * scale;
            DrawChip(new Rect(panel.x, itemY, 120f * scale, 36f * scale), "⚡  " + grenades, new Color(0.13f, 0.30f, 0.15f));
            DrawChip(new Rect(panel.x + 128f * scale, itemY, 190f * scale, 36f * scale), "AMMO  " + ammo + "/" + reserve, new Color(0.24f, 0.19f, 0.08f));
        }

        private void DrawCounters(float margin, float scale, int wave, int score)
        {
            float w = 180f * scale;
            Rect waveRect = new Rect(Screen.width - w - margin, margin, w, 38f * scale);
            DrawChip(waveRect, "WAVE  " + wave, new Color(0.10f, 0.12f, 0.18f));

            Rect scoreRect = new Rect(Screen.width - w - margin, waveRect.yMax + 8f * scale, w, 38f * scale);
            DrawChip(scoreRect, "KILLS  " + score, new Color(0.18f, 0.10f, 0.10f));
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
