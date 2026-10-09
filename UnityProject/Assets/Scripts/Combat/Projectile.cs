using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public sealed class Projectile : MonoBehaviour
    {
        [SerializeField] private float speed = 48f;
        [SerializeField] private float lifetime = 1.2f;
        [SerializeField] private int damage = 30;

        private Vector3 direction;
        private float remainingLife;
        private Transform owner;

        public void SetDefaults(float projectileSpeed, float projectileLifetime, int projectileDamage)
        {
            speed = projectileSpeed;
            lifetime = projectileLifetime;
            damage = projectileDamage;
        }

        public void SetOwner(Transform ownerTransform)
        {
            owner = ownerTransform;
        }

        public void Launch(Vector3 worldDirection)
        {
            direction = worldDirection.normalized;
            remainingLife = lifetime;

            if (direction.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        }

        private void Update()
        {
            float step = speed * Time.deltaTime;

            if (step > 0.001f && TryResolveSweep(step))
                return;

            transform.position += direction * step;
            remainingLife -= Time.deltaTime;

            if (remainingLife <= 0f)
                Destroy(gameObject);
        }

        private bool TryResolveSweep(float distance)
        {
            RaycastHit[] hits = Physics.RaycastAll(
                transform.position,
                direction,
                distance + 0.08f,
                ~0,
                QueryTriggerInteraction.Ignore);

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
                    collider.GetComponentInParent<EnemyProjectile>() != null)
                    continue;

                PlayerController player = collider.GetComponentInParent<PlayerController>();
                if (player != null)
                    continue;

                TargetHealth target = collider.GetComponentInParent<TargetHealth>();
                if (target != null && target.GetComponentInParent<EnemyChase>() != null)
                {
                    target.ApplyDamage(damage);
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

            if (other.GetComponentInParent<PlayerController>() != null ||
                other.GetComponentInParent<Projectile>() != null ||
                other.GetComponentInParent<EnemyProjectile>() != null)
                return;

            TargetHealth target = other.GetComponentInParent<TargetHealth>();
            if (target != null && target.GetComponentInParent<EnemyChase>() != null)
                target.ApplyDamage(damage);

            Destroy(gameObject);
        }
    }
}