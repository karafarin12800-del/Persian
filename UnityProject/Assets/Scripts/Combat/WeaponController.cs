using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public sealed class WeaponController : MonoBehaviour
    {
        [SerializeField] private Transform muzzle;
        [SerializeField] private float fireCooldown = 0.18f;
        private float nextFireTime;

        public bool TryFire(Vector3 targetWorldPosition)
        {
            if (Time.time < nextFireTime) return false;
            nextFireTime = Time.time + fireCooldown;

            Vector3 direction = targetWorldPosition - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
            return true;
        }

        public Transform Muzzle => muzzle != null ? muzzle : transform;
    }
}
