using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public sealed class GrenadeController : MonoBehaviour
    {
        [SerializeField] private GameObject grenadePrefab;
        [SerializeField] private Transform throwOrigin;
        [SerializeField] private float throwDistance = 5f;
        [SerializeField] private float explosionRadius = 2.5f;
        [SerializeField] private int damage = 60;
        [SerializeField] private int grenades = 3;
        [SerializeField] private float fuse = 0.8f;

        public int Grenades => grenades;

        public bool Throw(Vector2 direction)
        {
            if (grenades <= 0) return false;
            grenades--;
            Vector3 origin = throwOrigin != null ? throwOrigin.position : transform.position;
            Vector3 target = origin + new Vector3(direction.x, 0f, direction.y).normalized * throwDistance;
            if (grenadePrefab != null)
            {
                GameObject grenade = Instantiate(grenadePrefab, target, Quaternion.identity);
                Destroy(grenade, fuse);
            }
            Invoke(nameof(Explode), fuse);
            return true;
        }

        private void Explode()
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, explosionRadius);
            foreach (Collider hit in hits)
            {
                TargetHealth health = hit.GetComponentInParent<TargetHealth>();
                if (health != null && hit.CompareTag("Enemy")) health.ApplyDamage(damage);
            }
        }
    }
}
