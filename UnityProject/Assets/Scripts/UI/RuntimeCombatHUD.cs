using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public sealed class RuntimeCombatHUD : MonoBehaviour
    {
        [SerializeField] private PlayerController player;

        private GUIStyle labelStyle;
        private GUIStyle titleStyle;

        private void Awake()
        {
            if (player == null) player = FindFirstObjectByType<PlayerController>();

            labelStyle = new GUIStyle(GUI.skin.label)
            {
                fontSize = 24,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.UpperLeft
            };
            titleStyle = new GUIStyle(labelStyle)
            {
                fontSize = 20,
                alignment = TextAnchor.UpperRight
            };
        }

        private void OnGUI()
        {
            if (player == null) player = FindFirstObjectByType<PlayerController>();
            if (player == null || labelStyle == null) return;

            TargetHealth health = player.Health;
            WeaponController weapon = player.Weapon;
            PlayerInventory inventory = player.Inventory;
            EnemySpawner spawner = FindFirstObjectByType<EnemySpawner>();
            GameSession session = GameSession.Instance;

            string hp = health != null ? $"HP {health.CurrentHealth}/{health.MaxHealth}" : "HP --";
            string shield = $"SHIELD {player.Shield}";
            string ammo = weapon != null ? $"AMMO {weapon.Magazine}/{weapon.Reserve}" : "AMMO --";
            string grenades = inventory != null ? $"GRENADES {inventory.Grenades}" : "GRENADES --";
            string wave = spawner != null ? $"WAVE {spawner.CurrentWave}" : "WAVE --";
            string score = session != null ? $"SCORE {session.Score}" : "SCORE 0";

            GUI.Label(new Rect(20f, 16f, 360f, 32f), hp, labelStyle);
            GUI.Label(new Rect(20f, 48f, 360f, 32f), shield, labelStyle);
            GUI.Label(new Rect(20f, 80f, 360f, 32f), ammo, labelStyle);
            GUI.Label(new Rect(20f, 112f, 360f, 32f), grenades, labelStyle);
            GUI.Label(new Rect(Screen.width - 260f, 18f, 240f, 30f), wave, titleStyle);
            GUI.Label(new Rect(Screen.width - 260f, 50f, 240f, 30f), score, titleStyle);

            if (player.IsDefeated)
            {
                GUI.Box(new Rect(Screen.width * 0.5f - 150f, Screen.height * 0.5f - 45f, 300f, 90f), "GAME OVER");
            }
        }
    }
}