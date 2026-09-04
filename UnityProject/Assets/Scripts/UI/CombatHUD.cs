using UnityEngine;
using UnityEngine.UI;

namespace PersiaWar.Unity2D5D
{
    public sealed class CombatHUD : MonoBehaviour
    {
        [SerializeField] private Slider healthBar;
        [SerializeField] private Text ammoText;

        public void SetHealth(float current, float max)
        {
            if (healthBar != null)
            {
                healthBar.maxValue = Mathf.Max(1f, max);
                healthBar.value = Mathf.Clamp(current, 0f, healthBar.maxValue);
            }
        }

        public void SetAmmo(int current, int reserve)
        {
            if (ammoText != null)
                ammoText.text = $"{current} / {reserve}";
        }
    }
}
