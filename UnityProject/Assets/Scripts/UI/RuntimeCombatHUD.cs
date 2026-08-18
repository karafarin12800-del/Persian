using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public sealed class RuntimeCombatHUD : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private TargetHealth playerHealth;
        [SerializeField] private PickupReceiver ammoSource;
        [SerializeField] private NearestTargetAim aim;

        private GUIStyle style;

        private void Awake()
        {
            style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 28,
                fontStyle = FontStyle.Bold
            };
        }

        private void OnGUI()
        {
            if (style == null) return;
            string health = playerHealth != null ? $"HP {playerHealth.Current}/{playerHealth.Max}" : "HP --";
            string ammo = ammoSource != null ? $"AMMO {ammoSource.Ammo}" : "AMMO --";
            string target = aim != null && aim.CurrentTarget != null ? "TARGET LOCK" : "TARGET -";

            GUI.Label(new Rect(24f, 18f, 260f, 42f), health, style);
            GUI.Label(new Rect(24f, 58f, 260f, 42f), ammo, style);
            GUI.Label(new Rect(Screen.width - 260f, 18f, 240f, 42f), target, style);
        }
    }
}
