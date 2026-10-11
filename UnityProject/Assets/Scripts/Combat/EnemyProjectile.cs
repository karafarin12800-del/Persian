using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public sealed class EnemyProjectile : MonoBehaviour
    {
        [SerializeField] private float speed = 24f;
        [SerializeField] private float lifetime = 2f;
        [SerializeField] private int damage = 8;

        private Vector3 direction;
        private Transform owner;
        private float remainingDistance;

        public void Configure(Vector3 launchDirection, int damageAmount)
        {
            Configure(launchDirection, damageAmount, null);
        }

        public void Configure(Vector3 launchDirection, int damageAmount, Transform ownerTransform)
        {
            Configure(launchDirection, damageAmount, ownerTransform, speed * lifetime);
        }

        public void Configure(Vector3 launchDirection, int damageAmount, Transform ownerTransform, float maxRange)
        {
            direction = launchDirection.normalized;
            damage = Mathf.Max(1, damageAmount);
            owner = ownerTransform;
            // Lifetime is the physical range limit: the projectile cannot damage
            // anything after travelling farther than the configured weapon range.
            remainingDistance = Mathf.Max(0f, maxRange);
            lifetime = Mathf.Max(0.05f, Mathf.Max(1f, maxRange) / Mathf.Max(1f, speed));
        }

        private void Update()
        {
            float step = Mathf.Min(speed * Time.deltaTime, remainingDistance);

            if (step <= 0.001f || remainingDistance <= 0.001f)
            {
                Destroy(gameObject);
                return;
            }

            if (TryResolveSweep(step))
                return;

            transform.position += direction * step;
            remainingDistance -= step;
            lifetime -= Time.deltaTime;

            if (lifetime <= 0f || remainingDistance <= 0.001f)
                Destroy(gameObject);
        }

        private bool TryResolveSweep(float distance)
        {
            // Enemy shots are visible capsules, not mathematical zero-width rays.
            // Sweep a small radius and include trigger colliders so the projectile can
            // reliably register the player's runtime collider on Android.
            RaycastHit[] hits = Physics.SphereCastAll(
                transform.position,
                0.22f,
                direction,
                distance,
                ~0,
                QueryTriggerInteraction.Collide);

            if (hits == null || hits.Length == 0)
                return false;

            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (RaycastHit hit in hits)
            {
                Collider collider = hit.collider;
                if (collider == null)
                    continue;

                if (owner != null &&
                    (collider.transform == owner || collider.transform.IsChildOf(owner)))
                    continue;

                if (collider.GetComponentInParent<Projectile>() != null ||
                    collider.GetComponentInParent<EnemyProjectile>() != null ||
                    collider.GetComponentInParent<EnemyChase>() != null)
                    continue;

                PlayerController player = collider.GetComponentInParent<PlayerController>();
                if (player != null)
                {
                    player.ReceiveDamage(damage);
                    Destroy(gameObject);
                    return true;
                }

                Destroy(gameObject);
                return true;
            }

            return false;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other == null)
                return;

            if (owner != null &&
                (other.transform == owner || other.transform.IsChildOf(owner)))
                return;

            PlayerController player = other.GetComponentInParent<PlayerController>();
            if (player != null)
            {
                player.ReceiveDamage(damage);
                Destroy(gameObject);
                return;
            }

            if (other.GetComponentInParent<EnemyChase>() != null ||
                other.GetComponentInParent<Projectile>() != null)
                return;

            Destroy(gameObject);
        }
    }
}