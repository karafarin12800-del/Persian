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
            Shield,
            Weapon
        }

        [SerializeField] private PickupType type = PickupType.Ammo;
        [SerializeField] private int amount = 30;
        [SerializeField] private float rotateSpeed = 90f;
        [SerializeField] private float bobHeight = 0.15f;
        [SerializeField] private WeaponController.WeaponKind weaponKind = WeaponController.WeaponKind.AssaultRifle;

        private float startY;
        private bool weaponPickupScaleApplied;

        public void Configure(PickupType pickupType, int value)
        {
            type = pickupType;
            amount = Mathf.Max(1, value);
        }

        public void ConfigureWeapon(WeaponController.WeaponKind kind, int reserveAmmo)
        {
            type = PickupType.Weapon;
            weaponKind = kind;
            amount = Mathf.Max(1, reserveAmmo);
            ApplyWeaponPickupScale();
        }

        private void Awake()
        {
            if (type == PickupType.Weapon) ApplyWeaponPickupScale();
            startY = transform.position.y;
            SphereCollider trigger = GetComponent<SphereCollider>();
            if (trigger == null) trigger = gameObject.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 0.7f;
        }

        private void ApplyWeaponPickupScale()
        {
            if (weaponPickupScaleApplied) return;

            // Dropped weapon parts are child meshes under a root pickup collider.
            // Enlarge the visible model only so the pickup radius does not double.
            if (transform.childCount > 0)
            {
                for (int i = 0; i < transform.childCount; i++)
                    transform.GetChild(i).localScale *= 2f;
            }
            else
            {
                transform.localScale *= 2f;
            }

            weaponPickupScaleApplied = true;
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
                case PickupType.Weapon:
                    if (player.Weapon != null)
                        player.Weapon.EquipWeaponFromPickup(weaponKind, amount);
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