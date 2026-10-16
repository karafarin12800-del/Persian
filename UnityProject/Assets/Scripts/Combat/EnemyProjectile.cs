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
        private bool impactResolved;
        private static readonly RaycastHit[] SweepHitBuffer = new RaycastHit[96];

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
            impactResolved = false;
        }

        private void Update()
        {
            if (impactResolved) return;
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
            int hitCount = Physics.SphereCastNonAlloc(
                transform.position, 0.22f, direction, SweepHitBuffer, distance, ~0,
                QueryTriggerInteraction.Collide);
            if (hitCount <= 0)
                return false;

            // Reuse one fixed buffer and sort only its populated prefix to avoid
            // per-frame garbage when many alien guards fire on mobile.
            for (int i = 1; i < hitCount; i++)
            {
                RaycastHit key = SweepHitBuffer[i];
                int j = i - 1;
                while (j >= 0 && SweepHitBuffer[j].distance > key.distance)
                {
                    SweepHitBuffer[j + 1] = SweepHitBuffer[j];
                    j--;
                }
                SweepHitBuffer[j + 1] = key;
            }

            for (int i = 0; i < hitCount; i++)
            {
                Collider collider = SweepHitBuffer[i].collider;
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
                impactResolved = true;
                if (player != null)
                    player.ReceiveDamage(damage);
                Destroy(gameObject);
                return true;
            }
            return false;
        }

        private void OnTriggerEnter(Collider other)
        {
            if (impactResolved || other == null)
                return;

            if (owner != null &&
                (other.transform == owner || other.transform.IsChildOf(owner)))
                return;

            PlayerController player = other.GetComponentInParent<PlayerController>();
            if (player != null)
            {
                impactResolved = true;
                player.ReceiveDamage(damage);
                Destroy(gameObject);
                return;
            }

            if (other.GetComponentInParent<EnemyChase>() != null ||
                other.GetComponentInParent<Projectile>() != null)
                return;

            impactResolved = true;
            Destroy(gameObject);
        }
    }
}