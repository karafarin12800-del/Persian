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
            EnemySpawner spawner = FindFirstObjectByType<EnemySpawner>();
            GameSession session = GameSession.Instance;

            string hp = health != null ? $"HP {health.CurrentHealth}/{health.MaxHealth}" : "HP --";
            string shield = $"SHIELD {player.Shield}";
            string ammo = weapon != null ? $"AMMO {weapon.Magazine}/{weapon.Reserve}" : "AMMO --";
            string wave = spawner != null ? $"WAVE {spawner.CurrentWave}" : "WAVE --";
            string score = session != null ? $"SCORE {session.Score}" : "SCORE 0";

            GUI.Label(new Rect(20f, 16f, 330f, 34f), hp, labelStyle);
            GUI.Label(new Rect(20f, 48f, 330f, 34f), shield, labelStyle);
            GUI.Label(new Rect(20f, 80f, 330f, 34f), ammo, labelStyle);
            GUI.Label(new Rect(Screen.width - 250f, 18f, 225f, 32f), wave, titleStyle);
            GUI.Label(new Rect(Screen.width - 250f, 50f, 225f, 32f), score, titleStyle);

            if (player.IsDefeated)
            {
                GUI.Box(new Rect(Screen.width * 0.5f - 150f, Screen.height * 0.5f - 45f, 300f, 90f), "GAME OVER");
            }
        }
    }
}