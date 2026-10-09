using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public sealed class WeaponController : MonoBehaviour
    {
        public enum WeaponKind
        {
            LightPistol = 0,
            AssaultRifle = 1,
            HeavyMachineGun = 2
        }

        [SerializeField] private Projectile projectilePrefab;
        [SerializeField] private Transform muzzle;
        [SerializeField] private float fireCooldown = 0.155f;
        [SerializeField] private float projectileSpeed = 45f;
        [SerializeField] private float projectileLifetime = 0.72f;
        [SerializeField] private float weaponRange = 27f;
        [SerializeField] private int projectileDamage = 22;
        [SerializeField] private int magazineSize = 24;
        [SerializeField] private int startingMagazine = 24;
        [SerializeField] private int startingReserve = 90;
        [SerializeField] private WeaponKind weaponKind = WeaponKind.AssaultRifle;
        [SerializeField] private int meleeDamage = 45;
        [SerializeField] private float meleeRange = 3.1f;
        [SerializeField] private float meleeCooldown = 0.32f;

        private float nextFireTime;
        private float nextMeleeTime;
        private int magazine;
        private int reserve;
        private PlayerController player;
        private Transform gunRoot;
        private Vector3 gunRestLocalPosition;
        private float recoilAmount;
        private LineRenderer rangeRing;
        private const int RangeRingSegments = 64;

        public Transform Muzzle => muzzle != null ? muzzle : transform;
        public WeaponKind CurrentWeapon => weaponKind;
        public string CurrentWeaponName => weaponKind == WeaponKind.LightPistol
            ? "PISTOL"
            : (weaponKind == WeaponKind.HeavyMachineGun ? "HEAVY" : "RIFLE");
        public float EffectiveRange => Mathf.Max(1f, weaponRange);
        public float MoveSpeedMultiplier => weaponKind == WeaponKind.LightPistol
            ? 1.20f
            : (weaponKind == WeaponKind.HeavyMachineGun ? 0.78f : 1f);
        public int Magazine => magazine;
        public int Reserve => reserve;
        public int MagazineSize => magazineSize;
        public bool IsMelee { get; private set; }

        private void Awake()
        {
            player = GetComponent<PlayerController>();
            ApplyWeaponProfile(false);
            // The projectile template creates a collider and Rigidbody. Defer that
            // physics allocation until the player actually fires.

            if (muzzle == null)
            {
                GameObject muzzleObject = new GameObject("WeaponMuzzle");
                muzzle = muzzleObject.transform;
                muzzle.SetParent(transform, false);
                muzzle.localPosition = new Vector3(0.36f, 1.02f, 1.12f);
            }

            EnsureWeaponVisual();
            EnsureRangeIndicator();
            RefreshWeaponVisual();
        }

        public void CycleWeapon()
        {
            weaponKind = (WeaponKind)(((int)weaponKind + 1) % 3);
            ApplyWeaponProfile(true);
            RefreshWeaponVisual();
            UpdateRangeIndicator();
            StartupCheckpoint.Set("WeaponSwitched_" + CurrentWeaponName);
        }

        public void SelectWeapon(WeaponKind selectedWeapon)
        {
            bool changed = weaponKind != selectedWeapon;
            weaponKind = selectedWeapon;

            // Always re-apply the chosen profile. In particular, a pickup must
            // restore the weapon's configured range, projectile lifetime, damage,
            // fire cadence, and magazine size even if its kind is already selected.
            ApplyWeaponProfile(true);
            RefreshWeaponVisual();
            UpdateRangeIndicator();

            if (changed)
                StartupCheckpoint.Set("WeaponSwitched_" + CurrentWeaponName);
        }

        public void EquipWeaponFromPickup(WeaponKind selectedWeapon, int reserveAmmo)
        {
            SelectWeapon(selectedWeapon);
            AddReserveAmmo(reserveAmmo);
            StartupCheckpoint.Set("WeaponPickupEquipped_" + CurrentWeaponName);
        }

        private void ApplyWeaponProfile(bool preserveAmmo)
        {
            int oldMagazineSize = Mathf.Max(1, magazineSize);
            float oldAmmoRatio = preserveAmmo ? magazine / (float)oldMagazineSize : 1f;

            switch (weaponKind)
            {
                case WeaponKind.LightPistol:
                    projectileSpeed = 34f;
                    weaponRange = 7.5f;
                    projectileDamage = 14;
                    fireCooldown = 0.24f;
                    magazineSize = 15;
                    break;

                case WeaponKind.HeavyMachineGun:
                    projectileSpeed = 40f;
                    weaponRange = 21f;
                    projectileDamage = 34;
                    fireCooldown = 0.31f;
                    magazineSize = 36;
                    break;

                default:
                    projectileSpeed = 38f;
                    weaponRange = 13.5f;
                    projectileDamage = 22;
                    fireCooldown = 0.19f;
                    magazineSize = 24;
                    break;
            }

            projectileLifetime = weaponRange / Mathf.Max(1f, projectileSpeed);
            magazine = preserveAmmo
                ? Mathf.Clamp(Mathf.RoundToInt(oldAmmoRatio * magazineSize), 0, magazineSize)
                : magazineSize;
            if (!preserveAmmo)
                reserve = Mathf.Max(0, startingReserve);

            if (gunRoot != null)
                RefreshWeaponVisual();
            if (rangeRing != null)
                UpdateRangeIndicator();
        }

        private void EnsureRangeIndicator()
        {
            if (rangeRing != null) return;

            GameObject indicator = new GameObject("WeaponRangeIndicator");
            indicator.transform.SetParent(transform, false);
            rangeRing = indicator.AddComponent<LineRenderer>();
            rangeRing.useWorldSpace = true;
            rangeRing.loop = true;
            rangeRing.positionCount = RangeRingSegments;
            rangeRing.widthMultiplier = 0.065f;
            rangeRing.numCornerVertices = 2;
            rangeRing.numCapVertices = 0;
            rangeRing.alignment = LineAlignment.View;
            rangeRing.textureMode = LineTextureMode.Stretch;
            rangeRing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rangeRing.receiveShadows = false;
            rangeRing.sharedMaterial = RuntimeMaterialFactory.Create(
                "WeaponRangeRingYellow",
                new Color(1f, 0.82f, 0.08f, 1f));
            rangeRing.startColor = new Color(1f, 0.82f, 0.08f, 1f);
            rangeRing.endColor = new Color(1f, 0.82f, 0.08f, 1f);
            rangeRing.enabled = true;
            UpdateRangeIndicator();
        }

        private void RefreshWeaponVisual()
        {
            if (gunRoot == null) return;

            bool pistol = weaponKind == WeaponKind.LightPistol;
            bool heavy = weaponKind == WeaponKind.HeavyMachineGun;

            // Keep the pistol compact by changing its actual silhouette rather than
            // merely shrinking a rifle. The heavy profile uses a thicker receiver,
            // larger magazine, long barrel, stock and foregrip.
            gunRoot.localScale = Vector3.one * (heavy ? 1.08f : 1f);
            gunRestLocalPosition = pistol
                ? new Vector3(0.24f, 0.93f, 0.33f)
                : (heavy
                    ? new Vector3(0.40f, 1.04f, 0.48f)
                    : new Vector3(0.34f, 1.02f, 0.42f));
            gunRoot.localPosition = gunRestLocalPosition;

            SetWeaponPart("Receiver", true,
                Vector3.zero,
                pistol ? new Vector3(0.16f, 0.11f, 0.30f)
                    : (heavy ? new Vector3(0.26f, 0.20f, 0.64f) : new Vector3(0.22f, 0.18f, 0.56f)));
            SetWeaponPart("Stock", !pistol,
                heavy ? new Vector3(0f, 0.015f, -0.39f) : new Vector3(0f, 0.015f, -0.38f),
                heavy ? new Vector3(0.18f, 0.16f, 0.36f) : new Vector3(0.16f, 0.14f, 0.34f));
            SetWeaponPart("Magazine", true,
                pistol ? new Vector3(0.01f, -0.115f, 0.01f) : new Vector3(0.01f, -0.15f, 0.03f),
                pistol ? new Vector3(0.09f, 0.18f, 0.12f)
                    : (heavy ? new Vector3(0.16f, 0.34f, 0.21f) : new Vector3(0.13f, 0.28f, 0.18f)));
            SetWeaponPart("TopRail", !pistol,
                new Vector3(0f, heavy ? 0.15f : 0.13f, 0.08f),
                heavy ? new Vector3(0.16f, 0.07f, 0.52f) : new Vector3(0.13f, 0.06f, 0.45f));
            SetWeaponPart("Barrel", true,
                new Vector3(0f, 0f, pistol ? 0.19f : (heavy ? 0.56f : 0.58f)),
                pistol ? new Vector3(0.042f, 0.13f, 0.042f)
                    : (heavy ? new Vector3(0.075f, 0.35f, 0.075f) : new Vector3(0.065f, 0.33f, 0.065f)));
            SetWeaponPart("MuzzleBreak", heavy,
                new Vector3(0f, 0f, 1.0f),
                new Vector3(0.15f, 0.12f, 0.13f));
            SetWeaponPart("FrontGrip", !pistol,
                new Vector3(0f, -0.12f, heavy ? 0.45f : 0.43f),
                heavy ? new Vector3(0.14f, 0.25f, 0.15f) : new Vector3(0.11f, 0.20f, 0.12f));

            SetWeaponPart("PistolSlide", pistol,
                new Vector3(0f, 0.055f, 0.06f), new Vector3(0.17f, 0.045f, 0.27f));
            SetWeaponPart("PistolGrip", pistol,
                new Vector3(0f, -0.115f, -0.035f), new Vector3(0.095f, 0.16f, 0.13f));

            if (muzzle != null)
            {
                muzzle.localPosition = pistol
                    ? new Vector3(0.24f, 0.93f, 0.66f)
                    : (heavy
                        ? new Vector3(0.40f, 1.04f, 1.38f)
                        : new Vector3(0.36f, 1.02f, 1.12f));
            }
        }

        private void SetWeaponPart(string partName, bool visible, Vector3 localPosition, Vector3 localScale)
        {
            if (gunRoot == null)
                return;

            Transform part = gunRoot.Find(partName);
            if (part == null)
                return;

            part.gameObject.SetActive(visible);
            if (!visible)
                return;

            part.localPosition = localPosition;
            part.localScale = localScale;
        }

        private void LateUpdate()
        {
            UpdateRangeIndicator();
        }

        private void UpdateRangeIndicator()
        {
            if (rangeRing == null || player == null)
                return;

            bool visible = isActiveAndEnabled && player.isActiveAndEnabled && !player.IsDefeated;
            rangeRing.enabled = visible;
            if (!visible) return;

            Vector3 center = player.transform.position;
            float y = center.y + 0.12f;
            float radius = EffectiveRange;
            for (int i = 0; i < RangeRingSegments; i++)
            {
                float angle = (i / (float)RangeRingSegments) * Mathf.PI * 2f;
                rangeRing.SetPosition(i, new Vector3(
                    center.x + Mathf.Cos(angle) * radius,
                    y,
                    center.z + Mathf.Sin(angle) * radius));
            }
        }

        private void EnsureProjectileTemplate()
        {
            if (projectilePrefab != null) return;

#if UNITY_ANDROID
            GameObject projectileObject = AndroidSafeRuntimeFactory.CreateProjectile(
                "RuntimeProjectileTemplate",
                transform.position,
                Quaternion.identity,
                Vector3.one * 0.18f,
                new Color(1f, 0.82f, 0.20f));
            projectileObject.SetActive(false);
            projectilePrefab = projectileObject.AddComponent<Projectile>();
#else
            GameObject projectileObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projectileObject.name = "RuntimeProjectileTemplate";
            projectileObject.SetActive(false);
            projectileObject.transform.position = transform.position;
            projectileObject.transform.localScale = Vector3.one * 0.18f;

            SphereCollider collider = projectileObject.GetComponent<SphereCollider>();
            collider.isTrigger = true;
            collider.radius = 0.5f;

            Rigidbody body = projectileObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            projectilePrefab = projectileObject.AddComponent<Projectile>();
#endif
        }

        public bool TryFire(Vector3 targetWorldPosition)
        {
            IsMelee = false;
            if (Time.time < nextFireTime) return false;
            if (magazine <= 0)
            {
                Reload();
                return false;
            }
            EnsureProjectileTemplate();
            if (projectilePrefab == null) return false;

            Vector3 origin = muzzle != null ? muzzle.position : transform.position + Vector3.up;
            Vector3 direction = targetWorldPosition - origin;
            direction.y = 0f;
            float distance = direction.magnitude;
            if (distance < 0.001f || distance > EffectiveRange)
                return false;

            nextFireTime = Time.time + fireCooldown;
            magazine--;
            transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);

            Projectile projectile = Object.Instantiate(projectilePrefab, origin, transform.rotation);
            projectile.SetDefaults(projectileSpeed, projectileLifetime, projectileDamage);
            projectile.SetOwner(transform);
            projectile.gameObject.SetActive(true);
            projectile.Launch(direction.normalized);
            recoilAmount = 0.12f;
            StylizedCharacterVisual characterVisual = GetComponentInChildren<StylizedCharacterVisual>(true);
            if (characterVisual != null)
                characterVisual.PlayFire();
            return true;
        }

        public bool TryFireDirection(Vector3 worldDirection)
        {
            IsMelee = false;

            if (Time.time < nextFireTime) return false;
            if (magazine <= 0)
            {
                Reload();
                return false;
            }
            EnsureProjectileTemplate();
            if (projectilePrefab == null) return false;

            worldDirection.y = 0f;
            if (worldDirection.sqrMagnitude < 0.001f)
                return false;

            Vector3 direction = worldDirection.normalized;
            Vector3 origin = muzzle != null ? muzzle.position : transform.position + Vector3.up;

            nextFireTime = Time.time + fireCooldown;
            magazine--;
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

            Projectile projectile = Object.Instantiate(projectilePrefab, origin, transform.rotation);
            projectile.SetDefaults(projectileSpeed, projectileLifetime, projectileDamage);
            projectile.SetOwner(transform);
            projectile.gameObject.SetActive(true);
            projectile.Launch(direction);
            recoilAmount = 0.12f;
            StylizedCharacterVisual characterVisual = GetComponentInChildren<StylizedCharacterVisual>(true);
            if (characterVisual != null)
                characterVisual.PlayFire();
            return true;
        }

        private void Update()
        {
            if (gunRoot == null) return;

            recoilAmount = Mathf.MoveTowards(recoilAmount, 0f, 5.5f * Time.deltaTime);
            Vector3 target = gunRestLocalPosition + Vector3.back * recoilAmount;
            gunRoot.localPosition = Vector3.Lerp(
                gunRoot.localPosition,
                target,
                1f - Mathf.Exp(-24f * Time.deltaTime));
        }

        private void EnsureWeaponVisual()
        {
            if (gunRoot != null)
                return;

            gunRoot = new GameObject("PlayerRifle").transform;
            gunRoot.SetParent(transform, false);
            gunRestLocalPosition = new Vector3(0.34f, 1.02f, 0.42f);
            gunRoot.localPosition = gunRestLocalPosition;
            gunRoot.localRotation = Quaternion.identity;

            Material receiver = RuntimeMaterialFactory.Create(
                "RifleReceiver", new Color(0.07f, 0.09f, 0.12f));
            Material metal = RuntimeMaterialFactory.Create(
                "RifleMetal", new Color(0.18f, 0.21f, 0.24f));
            Material accent = RuntimeMaterialFactory.Create(
                "RifleAccent", new Color(0.86f, 0.62f, 0.14f));
            Material grip = RuntimeMaterialFactory.Create(
                "RifleGrip", new Color(0.10f, 0.12f, 0.14f));

            CreateWeaponPart("Receiver", PrimitiveType.Cube, gunRoot,
                new Vector3(0f, 0f, 0f), new Vector3(0.22f, 0.18f, 0.56f), receiver);
            CreateWeaponPart("Stock", PrimitiveType.Cube, gunRoot,
                new Vector3(0f, 0.015f, -0.38f), new Vector3(0.16f, 0.14f, 0.34f), grip);
            CreateWeaponPart("Magazine", PrimitiveType.Cube, gunRoot,
                new Vector3(0.01f, -0.15f, 0.03f), new Vector3(0.13f, 0.28f, 0.18f), accent);
            CreateWeaponPart("TopRail", PrimitiveType.Cube, gunRoot,
                new Vector3(0f, 0.13f, 0.08f), new Vector3(0.13f, 0.06f, 0.45f), metal);
            CreateWeaponPart("Barrel", PrimitiveType.Cylinder, gunRoot,
                new Vector3(0f, 0f, 0.58f), new Vector3(0.065f, 0.33f, 0.065f), metal,
                Quaternion.Euler(90f, 0f, 0f));
            CreateWeaponPart("MuzzleBreak", PrimitiveType.Cube, gunRoot,
                new Vector3(0f, 0f, 0.94f), new Vector3(0.12f, 0.10f, 0.10f), accent);
            CreateWeaponPart("FrontGrip", PrimitiveType.Cube, gunRoot,
                new Vector3(0f, -0.12f, 0.43f), new Vector3(0.11f, 0.20f, 0.12f), grip);
            CreateWeaponPart("PistolSlide", PrimitiveType.Cube, gunRoot,
                new Vector3(0f, 0.055f, 0.06f), new Vector3(0.17f, 0.045f, 0.27f), metal);
            CreateWeaponPart("PistolGrip", PrimitiveType.Cube, gunRoot,
                new Vector3(0f, -0.115f, -0.035f), new Vector3(0.095f, 0.16f, 0.13f), grip);

            // Start with the pistol-only parts hidden until the active profile is applied.
            gunRoot.Find("PistolSlide").gameObject.SetActive(false);
            gunRoot.Find("PistolGrip").gameObject.SetActive(false);

            // Prevent the decorative weapon from participating in collision queries.
            Collider[] colliders = gunRoot.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
                if (colliders[i] != null) Object.Destroy(colliders[i]);
        }

        private static void CreateWeaponPart(
            string partName,
            PrimitiveType primitiveType,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            Quaternion? localRotation = null)
        {
            GameObject part = GameObject.CreatePrimitive(primitiveType);
            part.name = partName;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            if (localRotation.HasValue)
                part.transform.localRotation = localRotation.Value;

            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        public void Reload()
        {
            if (magazine >= magazineSize || reserve <= 0) return;
            int amount = Mathf.Min(magazineSize - magazine, reserve);
            magazine += amount;
            reserve -= amount;
        }

        public void AddReserveAmmo(int amount)
        {
            reserve = Mathf.Clamp(reserve + Mathf.Max(0, amount), 0, 180);
        }

        public bool TryMelee()
        {
            if (player == null || player.IsDefeated || Time.time < nextMeleeTime) return false;
            nextMeleeTime = Time.time + meleeCooldown;
            IsMelee = true;

            TargetHealth[] targets = Object.FindObjectsByType<TargetHealth>(FindObjectsSortMode.None);
            Vector3 origin = transform.position;
            Vector3 forward = transform.forward;
            bool hitSomething = false;

            foreach (TargetHealth target in targets)
            {
                if (target == null || target.transform == transform || target.GetComponentInParent<EnemyChase>() == null) continue;
                Vector3 delta = target.transform.position - origin;
                delta.y = 0f;
                float distance = delta.magnitude;
                if (distance <= 0.01f || distance > meleeRange) continue;
                if (Vector3.Dot(forward, delta.normalized) < 0.25f) continue;

                target.ApplyDamage(meleeDamage);
                hitSomething = true;
            }

            return hitSomething;
        }
    }
}