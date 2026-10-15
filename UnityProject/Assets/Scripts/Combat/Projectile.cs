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
        private float remainingDistance;
        private static readonly RaycastHit[] SweepHitBuffer = new RaycastHit[96];

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
            remainingDistance = Mathf.Max(0f, speed * lifetime);

            if (direction.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
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
            remainingLife -= Time.deltaTime;

            if (remainingLife <= 0f || remainingDistance <= 0.001f)
                Destroy(gameObject);
        }

        private bool TryResolveSweep(float distance)
        {
            int hitCount = Physics.SphereCastNonAlloc(
                transform.position, 0.12f, direction, SweepHitBuffer, distance, ~0,
                QueryTriggerInteraction.Collide);
            if (hitCount <= 0)
                return false;

            // Sort only the populated prefix of a reusable buffer. SphereCastAll
            // allocated a new array for every projectile on every frame.
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
                    collider.GetComponentInParent<EnemyProjectile>() != null)
                    continue;
                if (collider.GetComponentInParent<PlayerController>() != null)
                    continue;

                TargetHealth target = collider.GetComponentInParent<TargetHealth>();
                if (target != null && target.GetComponentInParent<EnemyChase>() != null)
                    target.ApplyDamage(damage, owner);
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
                target.ApplyDamage(damage, owner);

            Destroy(gameObject);
        }
    }
}