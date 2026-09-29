using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public sealed class PickupItem : MonoBehaviour
    {
        public enum PickupType
        {
            Ammo,
            Medkit,
            Grenade,
            Shield
        }

        [SerializeField] private PickupType type = PickupType.Ammo;
        [SerializeField] private int amount = 30;
        [SerializeField] private float rotateSpeed = 90f;
        [SerializeField] private float bobHeight = 0.15f;

        private float startY;

        public void Configure(PickupType pickupType, int value)
        {
            type = pickupType;
            amount = Mathf.Max(1, value);
        }

        private void Awake()
        {
            startY = transform.position.y;
            SphereCollider trigger = GetComponent<SphereCollider>();
            if (trigger == null) trigger = gameObject.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 0.7f;
        }

        private void Update()
        {
            transform.Rotate(0f, rotateSpeed * Time.deltaTime, 0f, Space.World);
            Vector3 p = transform.position;
            p.y = startY + Mathf.Sin(Time.time * 3f) * bobHeight;
            transform.position = p;
        }

        private void OnTriggerEnter(Collider other)
        {
            PlayerController player = other.GetComponentInParent<PlayerController>();
            if (player == null || player.IsDefeated) return;

            switch (type)
            {
                case PickupType.Ammo:
                    if (player.Weapon != null) player.Weapon.AddReserveAmmo(amount);
                    break;
                case PickupType.Medkit:
                    player.Heal(amount);
                    break;
                case PickupType.Grenade:
                    player.GetComponent<PlayerInventory>()?.AddGrenades(amount);
                    break;
                case PickupType.Shield:
                    player.AddShield(amount);
                    break;
            }

            Destroy(gameObject);
        }
    }

    public sealed class PlayerInventory : MonoBehaviour
    {
        [SerializeField] private int grenades = 3;
        public int Grenades => grenades;

        public void AddGrenades(int amount)
        {
            grenades = Mathf.Clamp(grenades + Mathf.Max(0, amount), 0, 9);
        }

        public bool TryConsumeGrenade()
        {
            if (grenades <= 0) return false;
            grenades--;
            return true;
        }
    }
}