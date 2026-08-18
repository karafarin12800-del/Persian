using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public sealed class TargetHealth : MonoBehaviour
    {
        [SerializeField] private int maxHealth = 100;
        public int CurrentHealth { get; private set; }

        private void Awake()
        {
            CurrentHealth = maxHealth;
        }

        public void ApplyDamage(int amount)
        {
            CurrentHealth = Mathf.Max(0, CurrentHealth - Mathf.Max(0, amount));
            if (CurrentHealth == 0)
                Destroy(gameObject);
        }
    }
}
