using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public sealed class WeaponController : MonoBehaviour
    {
        [SerializeField] private Projectile projectilePrefab;
        [SerializeField] private Transform muzzle;
        [SerializeField] private float fireCooldown = 0.20f;
        [SerializeField] private float projectileSpeed = 48f;
        [SerializeField] private float projectileLifetime = 2.4f;
        [SerializeField] private int projectileDamage = 30;
        [SerializeField] private int magazineSize = 25;
        [SerializeField] private int startingMagazine = 25;
        [SerializeField] private int startingReserve = 120;
        [SerializeField] private int meleeDamage = 45;
        [SerializeField] private float meleeRange = 3.1f;
        [SerializeField] private float meleeCooldown = 0.32f;

        private float nextFireTime;
        private float nextMeleeTime;
        private int magazine;
        private int reserve;
        private PlayerController player;
        private StylizedCharacterVisual visual;

        public Transform Muzzle => muzzle != null ? muzzle : transform;
        public int Magazine => magazine;
        public int Reserve => reserve;
        public int MagazineSize => magazineSize;
        public bool IsMelee { get; private set; }

        private void Awake()
        {
            player = GetComponent<PlayerController>();
            visual = GetComponentInChildren<StylizedCharacterVisual>();

            magazine = Mathf.Clamp(startingMagazine, 0, magazineSize);
            reserve = Mathf.Max(0, startingReserve);

            EnsureProjectileTemplate();
            EnsureMuzzle();
        }

        private void EnsureMuzzle()
        {
            if (visual == null)
                visual = GetComponentInChildren<StylizedCharacterVisual>();

            if (visual != null && visual.Muzzle != null)
            {
                muzzle = visual.Muzzle;
                return;
            }

            if (muzzle == null)
            {
                GameObject muzzleObject = new GameObject("WeaponMuzzle");
                muzzle = muzzleObject.transform;
                muzzle.SetParent(transform, false);
                muzzle.localPosition = new Vector3(0f, 1.05f, 0.8f);
            }
        }

        private void EnsureProjectileTemplate()
        {
            if (projectilePrefab != null) return;

            GameObject projectileObject = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            projectileObject.name = "RuntimeProjectileTemplate";
            projectileObject.SetActive(false);
            projectileObject.transform.position = transform.position;
            projectileObject.transform.localScale = new Vector3(0.075f, 0.24f, 0.075f);

            CapsuleCollider collider = projectileObject.GetComponent<CapsuleCollider>();
            collider.isTrigger = true;
            collider.direction = 1;
            collider.radius = 0.5f;
            collider.height = 2f;

            Rigidbody body = projectileObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            Renderer renderer = projectileObject.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = RuntimeMaterialFactory.Create("PlayerProjectileMaterial", new Color(1f, 0.72f, 0.12f));

            projectilePrefab = projectileObject.AddComponent<Projectile>();
        }

        public bool TryFire(Vector3 targetWorldPosition)
        {
            Vector3 origin = Muzzle.position;
            Vector3 direction = targetWorldPosition - origin;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f)
                return false;

            return TryFireDirection(direction.normalized);
        }

        public bool TryFireDirection(Vector3 worldDirection)
        {
            IsMelee = false;

            if (Time.time < nextFireTime)
                return false;

            if (magazine <= 0)
            {
                Reload();
                return false;
            }

            if (projectilePrefab == null)
                return false;

            worldDirection.y = 0f;
            if (worldDirection.sqrMagnitude < 0.001f)
                return false;

            worldDirection.Normalize();

            nextFireTime = Time.time + fireCooldown;
            magazine--;

            if (player != null)
                player.SetFacingDirection(worldDirection);

            if (visual != null)
                visual.PlayFire();

            Vector3 origin = Muzzle.position + worldDirection * 0.15f;
            Projectile projectile = Object.Instantiate(projectilePrefab, origin, Quaternion.LookRotation(worldDirection, Vector3.up));
            projectile.gameObject.SetActive(true);
            projectile.Launch(worldDirection);
            return true;
        }

        public void Reload()
        {
            if (magazine >= magazineSize || reserve <= 0)
                return;

            int amount = Mathf.Min(magazineSize - magazine, reserve);
            magazine += amount;
            reserve -= amount;
        }

        public void AddReserveAmmo(int amount)
        {
            reserve = Mathf.Clamp(reserve + Mathf.Max(0, amount), 0, 240);
        }

        public bool TryMelee()
        {
            if (player == null || player.IsDefeated || Time.time < nextMeleeTime)
                return false;

            nextMeleeTime = Time.time + meleeCooldown;
            IsMelee = true;

            TargetHealth[] targets = Object.FindObjectsByType<TargetHealth>(FindObjectsSortMode.None);
            Vector3 origin = transform.position;
            Vector3 forward = player.FacingDirection;
            bool hitSomething = false;

            foreach (TargetHealth target in targets)
            {
                if (target == null || target.transform == transform ||
                    target.GetComponentInParent<EnemyChase>() == null)
                    continue;

                Vector3 delta = target.transform.position - origin;
                delta.y = 0f;
                float distance = delta.magnitude;
                if (distance <= 0.01f || distance > meleeRange)
                    continue;

                if (Vector3.Dot(forward, delta.normalized) < 0.25f)
                    continue;

                target.ApplyDamage(meleeDamage);
                hitSomething = true;
            }

            return hitSomething;
        }
    }
}
