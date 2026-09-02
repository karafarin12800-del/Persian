using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public sealed class EnemyChase : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private float moveSpeed = 2.5f;
        [SerializeField] private float stopDistance = 2.35f;
        [SerializeField] private int meleeDamage = 6;
        [SerializeField] private float meleeCooldown = 1.8f;
        [SerializeField] private float rangedCooldown = 1.8f;
        [SerializeField] private float rangedRange = 36f;
        [SerializeField] private int rangedDamage = 8;
        [SerializeField] private float retargetInterval = 0.25f;

        private float nextRetargetTime;
        private float nextAttackTime;
        private float nextRangedTime;
        private int archetype = 1;
        private float collisionRadius = 0.55f;

        public int ScoreValue => archetype == 3 ? 40 : (archetype == 2 ? 20 : 10);

        public void Configure(Transform targetTransform, int enemyArchetype)
        {
            target = targetTransform;
            archetype = Mathf.Clamp(enemyArchetype, 1, 3);

            moveSpeed = archetype == 3 ? 3.5f : (archetype == 2 ? 3.0f : 2.5f);
            meleeDamage = archetype == 3 ? 12 : (archetype == 2 ? 8 : 6);
            rangedDamage = archetype == 3 ? 12 : 8;
            stopDistance = archetype == 3 ? 2.7f : 2.35f;
            meleeCooldown = archetype == 3 ? 1.15f : (archetype == 2 ? 1.4f : 1.8f);
            rangedCooldown = archetype == 3 ? 1.15f : (archetype == 2 ? 1.5f : 1.8f);
        }

        private void Awake()
        {
            CapsuleCollider capsule = GetComponent<CapsuleCollider>();
            if (capsule != null)
                collisionRadius = Mathf.Max(0.35f, capsule.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z));
        }

        private void Update()
        {
            if (target == null || Time.time < nextRetargetTime)
                return;

            nextRetargetTime = Time.time + retargetInterval;

            Vector3 delta = target.position - transform.position;
            delta.y = 0f;
            float distance = delta.magnitude;
            if (distance <= 0.01f)
                return;

            Vector3 direction = delta / distance;
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);

            if (distance > stopDistance)
            {
                float step = moveSpeed * retargetInterval;
                Vector3 nextPosition = transform.position + direction * Mathf.Min(step, distance - stopDistance);
                nextPosition.y = 0f;

                if (CanMoveTo(nextPosition))
                    transform.position = nextPosition;
            }

            if (distance <= stopDistance && Time.time >= nextAttackTime)
            {
                PlayerController player = target.GetComponentInParent<PlayerController>();
                if (player != null && HasLineOfSightToPlayer(player))
                    player.ReceiveDamage(meleeDamage);
                nextAttackTime = Time.time + meleeCooldown;
            }

            if (distance <= rangedRange && Time.time >= nextRangedTime)
            {
                FireProjectile(direction);
                nextRangedTime = Time.time + rangedCooldown;
            }
        }

        private bool CanMoveTo(Vector3 position)
        {
            Collider[] hits = Physics.OverlapSphere(position + Vector3.up * 0.75f, collisionRadius, ~0, QueryTriggerInteraction.Ignore);
            foreach (Collider hit in hits)
            {
                if (hit == null || hit.transform == transform || hit.transform.IsChildOf(transform))
                    continue;
                if (hit.GetComponentInParent<EnemyChase>() != null)
                    continue;
                if (hit.GetComponentInParent<PlayerController>() != null)
                    continue;
                if (hit.GetComponentInParent<Projectile>() != null)
                    continue;
                if (hit.GetComponentInParent<EnemyProjectile>() != null)
                    continue;
                return false;
            }

            return true;
        }

        private bool HasLineOfSightToPlayer(PlayerController player)
        {
            Vector3 origin = transform.position + Vector3.up * 0.8f;
            Vector3 targetPoint = player.transform.position + Vector3.up * 0.7f;
            Vector3 direction = targetPoint - origin;
            float distance = direction.magnitude;
            if (distance <= 0.01f)
                return true;

            if (Physics.Raycast(origin, direction.normalized, out RaycastHit hit, distance, ~0, QueryTriggerInteraction.Ignore))
                return hit.collider.GetComponentInParent<PlayerController>() == player;

            return false;
        }

        private void FireProjectile(Vector3 direction)
        {
            if (target == null)
                return;

            Vector3 origin = transform.position + Vector3.up * 0.75f + direction * 0.8f;
            Vector3 targetPoint = target.position + Vector3.up * 0.7f;
            Vector3 shotDirection = targetPoint - origin;
            shotDirection.y = 0f;
            if (shotDirection.sqrMagnitude < 0.001f)
                return;

            PlayerController player = target.GetComponentInParent<PlayerController>();
            if (player == null || !HasLineOfSightToPlayer(player))
                return;

            GameObject projectile = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projectile.name = "EnemyProjectile";
            projectile.transform.position = origin;
            projectile.transform.localScale = Vector3.one * 0.18f;

            SphereCollider collider = projectile.GetComponent<SphereCollider>();
            collider.isTrigger = true;

            Rigidbody body = projectile.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            EnemyProjectile shot = projectile.AddComponent<EnemyProjectile>();
            shot.Configure(shotDirection.normalized, rangedDamage);
        }
    }
}
