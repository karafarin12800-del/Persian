using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public sealed class WeaponController : MonoBehaviour
    {
        [SerializeField] private Projectile projectilePrefab;
        [SerializeField] private Transform muzzle;
        [SerializeField] private float fireCooldown = 0.18f;

        private float nextFireTime;

        public void Configure(Projectile projectile, Transform muzzleTransform)
        {
            projectilePrefab = projectile;
            muzzle = muzzleTransform;
        }

        public bool TryFire(Vector3 targetWorldPosition)
        {
            if (projectilePrefab == null || Time.time < nextFireTime)
                return false;

            Vector3 origin = muzzle != null ? muzzle.position : transform.position + Vector3.up;
            Vector3 direction = targetWorldPosition - origin;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f)
                return false;

            nextFireTime = Time.time + fireCooldown;
            Quaternion rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            transform.rotation = rotation;

            Projectile projectile = Object.Instantiate(projectilePrefab, origin, rotation);
            projectile.gameObject.SetActive(true);
            projectile.Launch(direction);
            return true;
        }

        public Transform Muzzle => muzzle != null ? muzzle : transform;
    }
}
