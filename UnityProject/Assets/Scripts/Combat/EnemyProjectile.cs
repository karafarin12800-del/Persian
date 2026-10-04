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
        private readonly RaycastHit[] sweepHits = new RaycastHit[16];

        public void Configure(Vector3 launchDirection, int damageAmount)
        {
            Configure(launchDirection, damageAmount, null);
        }

        public void Configure(Vector3 launchDirection, int damageAmount, Transform ownerTransform)
        {
            direction = launchDirection.normalized;
            damage = Mathf.Max(1, damageAmount);
            owner = ownerTransform;
        }

        private void Update()
        {
            float step = speed * Time.deltaTime;

            if (step > 0.001f && TryResolveSweep(step))
                return;

            transform.position += direction * step;
            lifetime -= Time.deltaTime;

            if (lifetime <= 0f)
                Destroy(gameObject);
        }

        private bool TryResolveSweep(float distance)
        {
            int hitCount = Physics.RaycastNonAlloc(
                transform.position,
                direction,
                sweepHits,
                distance + 0.08f,
                ~0,
                QueryTriggerInteraction.Ignore);

            float closestDistance = float.PositiveInfinity;
            Collider closestCollider = null;
            for (int i = 0; i < hitCount; i++)
            {
                Collider collider = sweepHits[i].collider;
                if (collider == null) continue;
                if (owner != null && (collider.transform == owner || collider.transform.IsChildOf(owner))) continue;
                if (collider.GetComponentInParent<Projectile>() != null ||
                    collider.GetComponentInParent<EnemyProjectile>() != null ||
                    collider.GetComponentInParent<EnemyChase>() != null)
                    continue;
                if (sweepHits[i].distance < closestDistance)
                {
                    closestDistance = sweepHits[i].distance;
                    closestCollider = collider;
                }
            }

            if (closestCollider == null)
                return false;

            PlayerController player = closestCollider.GetComponentInParent<PlayerController>();
            if (player != null)
                player.ReceiveDamage(damage);

            Destroy(gameObject);
            return true;
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