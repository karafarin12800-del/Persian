using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public sealed class Projectile : MonoBehaviour
    {
        [SerializeField] private float speed = 48f;
        [SerializeField] private float lifetime = 2.4f;
        [SerializeField] private int damage = 30;

        private Vector3 direction;
        private float remainingLife;
        private Transform owner;
        private readonly RaycastHit[] sweepHits = new RaycastHit[16];

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
                if (collider.GetComponentInParent<Projectile>() != null || collider.GetComponentInParent<EnemyProjectile>() != null) continue;
                if (collider.GetComponentInParent<PlayerController>() != null) continue;
                if (sweepHits[i].distance < closestDistance)
                {
                    closestDistance = sweepHits[i].distance;
                    closestCollider = collider;
                }
            }

            if (closestCollider == null)
                return false;

            TargetHealth target = closestCollider.GetComponentInParent<TargetHealth>();
            if (target != null && target.GetComponentInParent<EnemyChase>() != null)
                target.ApplyDamage(damage);

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