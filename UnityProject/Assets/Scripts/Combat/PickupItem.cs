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
            Weapon,
            QuickDash
        }

        [SerializeField] private PickupType type = PickupType.Ammo;
        [SerializeField] private int amount = 30;
        [SerializeField] private float rotateSpeed = 90f;
        [SerializeField] private float bobHeight = 0.15f;
        [SerializeField] private WeaponController.WeaponKind weaponKind = WeaponController.WeaponKind.AssaultRifle;

        private float startY;
        private bool weaponPickupScaleApplied;
        private LineRenderer rewardAccentRing;
        private Material rewardAccentMaterial;

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
            BuildRewardAccent();
        }

        private void BuildRewardAccent()
        {
            // Decorative ring is a child of this pickup only. It does not recolor
            // the pickup mesh, any weapon model, or any enemy material.
            GameObject ringObject = new GameObject("RewardAccentRing");
            ringObject.transform.SetParent(transform, false);
            ringObject.transform.localPosition = new Vector3(0f, -0.32f, 0f);
            rewardAccentRing = ringObject.AddComponent<LineRenderer>();
            rewardAccentRing.useWorldSpace = false;
            rewardAccentRing.loop = true;
            rewardAccentRing.positionCount = 32;
            rewardAccentRing.widthMultiplier = 0.035f;
            rewardAccentRing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rewardAccentRing.receiveShadows = false;

            Shader shader = Shader.Find("Unlit/Color");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null)
            {
                Destroy(ringObject);
                rewardAccentRing = null;
                return;
            }

            rewardAccentMaterial = new Material(shader);
            Color accent;
            switch (type)
            {
                case PickupType.Ammo: accent = new Color(0.20f, 0.86f, 1f, 0.95f); break;
                case PickupType.Medkit: accent = new Color(0.28f, 1f, 0.48f, 0.95f); break;
                case PickupType.Grenade: accent = new Color(1f, 0.62f, 0.20f, 0.95f); break;
                case PickupType.Shield: accent = new Color(0.28f, 0.55f, 1f, 0.95f); break;
                case PickupType.Weapon: accent = new Color(0.96f, 0.82f, 0.42f, 0.95f); break;
                case PickupType.QuickDash: accent = new Color(0.82f, 0.96f, 0.30f, 0.95f); break;
                default: accent = new Color(0.72f, 0.88f, 0.96f, 0.95f); break;
            }
            if (rewardAccentMaterial.HasProperty("_Color"))
                rewardAccentMaterial.SetColor("_Color", accent);
            if (rewardAccentMaterial.HasProperty("_BaseColor"))
                rewardAccentMaterial.SetColor("_BaseColor", accent);
            rewardAccentRing.sharedMaterial = rewardAccentMaterial;

            const float radius = 0.82f;
            for (int i = 0; i < rewardAccentRing.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2f / rewardAccentRing.positionCount;
                rewardAccentRing.SetPosition(i, new Vector3(
                    Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
            }
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
                case PickupType.QuickDash:
                    player.AddQuickDashCharges(2);
                    break;
            }

            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (rewardAccentMaterial != null)
                Destroy(rewardAccentMaterial);
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