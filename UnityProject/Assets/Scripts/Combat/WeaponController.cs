using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public sealed class WeaponController : MonoBehaviour
    {
        [SerializeField] private Projectile projectilePrefab;
        [SerializeField] private Transform muzzle;
        [SerializeField] private float fireCooldown = 0.155f;
        [SerializeField] private float projectileSpeed = 45f;
        [SerializeField] private float projectileLifetime = 2.2f;
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
            EnsureProjectileTemplate();

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
                if (target == null || target.transform == transform || !target.CompareTag("Enemy")) continue;
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