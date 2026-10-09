using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public sealed class WeaponController : MonoBehaviour
    {
        [SerializeField] private Projectile projectilePrefab;
        [SerializeField] private Transform muzzle;
        [SerializeField] private float fireCooldown = 0.155f;
        [SerializeField] private float projectileSpeed = 45f;
        [SerializeField] private float projectileLifetime = 1.1f;
        [SerializeField] private int projectileDamage = 30;
        [SerializeField] private int magazineSize = 12;
        [SerializeField] private int startingMagazine = 12;
        [SerializeField] private int startingReserve = 90;
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

        public Transform Muzzle => muzzle != null ? muzzle : transform;
        public int Magazine => magazine;
        public int Reserve => reserve;
        public int MagazineSize => magazineSize;
        public bool IsMelee { get; private set; }

        private void Awake()
        {
            player = GetComponent<PlayerController>();
            magazine = Mathf.Clamp(startingMagazine, 0, magazineSize);
            reserve = Mathf.Max(0, startingReserve);
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
            if (direction.sqrMagnitude < 0.001f) return false;

            nextFireTime = Time.time + fireCooldown;
            magazine--;
            transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);

            Projectile projectile = Object.Instantiate(projectilePrefab, origin, transform.rotation);
            projectile.gameObject.SetActive(true);
            projectile.Launch(direction.normalized);
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
            projectile.gameObject.SetActive(true);
            projectile.SetDefaults(projectileSpeed, projectileLifetime, projectileDamage);
            projectile.SetOwner(transform);
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