using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public sealed class EnemyChase : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private float moveSpeed = 2.8f;
        [SerializeField] private float stopDistance = 8f;
        [SerializeField] private float meleeDistance = 2.7f;
        [SerializeField] private int meleeDamage = 6;
        [SerializeField] private float meleeCooldown = 1.5f;
        [SerializeField] private float rangedCooldown = 1.25f;
        [SerializeField] private float rangedRange = 34f;
        [SerializeField] private int rangedDamage = 8;
        [SerializeField] private float retargetInterval = 0.12f;

        private float nextRetargetTime;
        private float nextAttackTime;
        private float nextRangedTime;
        private int archetype = 1;
        private float collisionRadius = 0.55f;
        private StylizedCharacterVisual visual;

        public int ScoreValue => archetype == 3 ? 40 : (archetype == 2 ? 20 : 10);

        public void Configure(Transform targetTransform, int enemyArchetype)
        {
            target = targetTransform;
            archetype = Mathf.Clamp(enemyArchetype, 1, 3);

            moveSpeed = archetype == 3 ? 2.6f : (archetype == 2 ? 3.1f : 3.0f);
            meleeDamage = archetype == 3 ? 14 : (archetype == 2 ? 9 : 7);
            rangedDamage = archetype == 3 ? 15 : (archetype == 2 ? 10 : 8);
            stopDistance = archetype == 3 ? 10.5f : (archetype == 2 ? 9f : 7.5f);
            rangedRange = archetype == 3 ? 46f : (archetype == 2 ? 40f : 34f);
            meleeCooldown = archetype == 3 ? 1.05f : (archetype == 2 ? 1.25f : 1.5f);
            rangedCooldown = archetype == 3 ? 0.90f : (archetype == 2 ? 1.10f : 1.35f);

            if (visual != null)
                visual.Configure(false, archetype);
        }

        private void Awake()
        {
            CapsuleCollider capsule = GetComponent<CapsuleCollider>();
            if (capsule != null)
                collisionRadius = Mathf.Max(
                    0.35f,
                    capsule.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z));

            visual = StylizedCharacterVisual.Attach(transform, false, archetype);
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
            if (visual != null)
            {
                visual.SetFacing(direction);
                visual.SetMoving(distance > stopDistance);
            }

            if (distance > stopDistance)
            {
                float step = moveSpeed * retargetInterval;
                Vector3 nextPosition = transform.position + direction * Mathf.Min(step, distance - stopDistance);
                nextPosition.y = 0f;

                if (CanMoveTo(nextPosition))
                    transform.position = nextPosition;
            }

            PlayerController player = target.GetComponentInParent<PlayerController>();
            if (player == null)
                return;

            if (distance <= meleeDistance && Time.time >= nextAttackTime && HasLineOfSightToPlayer(player))
            {
                player.ReceiveDamage(meleeDamage);
                nextAttackTime = Time.time + meleeCooldown;
            }

            if (distance <= rangedRange && Time.time >= nextRangedTime)
            {
                if (HasLineOfSightToPlayer(player))
                {
                    FireProjectile(direction);
                    nextRangedTime = Time.time + rangedCooldown;
                }
            }
        }

        private bool CanMoveTo(Vector3 position)
        {
            Collider[] hits = Physics.OverlapSphere(
                position + Vector3.up * 0.75f,
                collisionRadius,
                ~0,
                QueryTriggerInteraction.Ignore);

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
            Vector3 origin = visual != null && visual.Muzzle != null
                ? visual.Muzzle.position
                : transform.position + Vector3.up * 1.0f;

            Vector3 targetPoint = player.transform.position + Vector3.up * 0.75f;
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

            Vector3 origin = visual != null && visual.Muzzle != null
                ? visual.Muzzle.position
                : transform.position + Vector3.up * 0.9f;

            Vector3 targetPoint = target.position + Vector3.up * 0.75f;
            Vector3 shotDirection = targetPoint - origin;
            shotDirection.y = 0f;

            if (shotDirection.sqrMagnitude < 0.001f)
                return;

            shotDirection.Normalize();

            GameObject projectile = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            projectile.name = "EnemyProjectile";
            projectile.transform.position = origin + shotDirection * 0.16f;
            projectile.transform.rotation = Quaternion.LookRotation(shotDirection, Vector3.up);
            projectile.transform.localScale = new Vector3(0.08f, 0.26f, 0.08f);

            SphereCollider sphere = projectile.GetComponent<SphereCollider>();
            if (sphere != null)
                Destroy(sphere);

            CapsuleCollider collider = projectile.GetComponent<CapsuleCollider>();
            if (collider != null)
            {
                collider.isTrigger = true;
                collider.radius = 0.5f;
                collider.height = 2f;
                collider.direction = 1;
            }

            Rigidbody body = projectile.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            Renderer renderer = projectile.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = RuntimeMaterialFactory.Create("EnemyProjectileMaterial", new Color(0.92f, 0.18f, 0.10f));

            EnemyProjectile shot = projectile.AddComponent<EnemyProjectile>();
            shot.Configure(shotDirection, rangedDamage, transform);

            if (visual != null)
                visual.PlayFire();
        }
    }
}
