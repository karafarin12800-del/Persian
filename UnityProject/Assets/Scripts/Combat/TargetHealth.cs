using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public sealed class TargetHealth : MonoBehaviour
    {
        [SerializeField] private int maxHealth = 100;
        public int CurrentHealth { get; private set; }
        public int MaxHealth => maxHealth;

        private void Awake()
        {
            CurrentHealth = maxHealth;
        }

        public void SetMaxHealth(int value)
        {
            maxHealth = Mathf.Max(1, value);
            CurrentHealth = maxHealth;
        }

        public void ApplyDamage(int amount)
        {
            if (CurrentHealth <= 0) return;
            amount = Mathf.Max(0, amount);
            if (amount == 0) return;

            CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
            if (CurrentHealth > 0) return;

            PlayerController player = GetComponent<PlayerController>();
            if (player != null)
            {
                player.HandleDefeat();
                return;
            }

            EnemyChase enemy = GetComponent<EnemyChase>();
            if (enemy != null)
            {
                int scoreBonus = Mathf.Max(0, enemy.ScoreValue - 10);
                if (GameSession.Instance != null)
                {
                    GameSession.Instance.RegisterEnemyDefeated();
                    GameSession.Instance.AddScore(scoreBonus);
                }
            }

            Destroy(gameObject);
        }

        public void Restore(int amount)
        {
            if (CurrentHealth <= 0) return;
            CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + Mathf.Max(0, amount));
        }
    }
}
