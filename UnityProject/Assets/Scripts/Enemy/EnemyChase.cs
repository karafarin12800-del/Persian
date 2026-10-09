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
        private Transform weaponVisualRoot;
        private static Material enemyRifleReceiverMaterial;
        private static Material enemyRifleMetalMaterial;

        public void SetTarget(Transform targetTransform)
        {
            target = targetTransform;
        }

        public int ScoreValue => archetype == 3 ? 40 : (archetype == 2 ? 20 : 10);

        public void Configure(Transform targetTransform, int enemyArchetype)
        {
            target = targetTransform;
            archetype = Mathf.Clamp(enemyArchetype, 1, 3);
            // With a full 32-combatant lobby, lower mobile AI polling frequency to
            // reduce per-frame physics-query pressure without changing attack rules.
            retargetInterval = Application.isMobilePlatform ? 0.22f : 0.12f;

            moveSpeed = archetype == 3 ? 2.6f : (archetype == 2 ? 3.1f : 3.0f);
            meleeDamage = archetype == 3 ? 14 : (archetype == 2 ? 9 : 7);
            rangedDamage = archetype == 3 ? 15 : (archetype == 2 ? 10 : 8);
            stopDistance = archetype == 3 ? 10.5f : (archetype == 2 ? 9f : 7.5f);
            rangedRange = archetype == 3 ? 46f : (archetype == 2 ? 40f : 34f);
            meleeCooldown = archetype == 3 ? 1.05f : (archetype == 2 ? 1.25f : 1.5f);
            rangedCooldown = archetype == 3 ? 0.90f : (archetype == 2 ? 1.10f : 1.35f);
            if (visual == null)
                visual = StylizedCharacterVisual.Attach(transform, false, archetype);
            else
                visual.Configure(false, archetype);


            if (visual != null)
                visual.Configure(false, archetype);

            EnsureEnemyWeaponVisual();
        }

        private void EnsureEnemyWeaponVisual()
        {
            if (visual == null)
                return;

            if (weaponVisualRoot != null)
                return;

            Transform existing = visual.transform.Find("EnemyRifleVisual");
            if (existing != null)
            {
                weaponVisualRoot = existing;
                return;
            }

            weaponVisualRoot = new GameObject("EnemyRifleVisual").transform;
            weaponVisualRoot.SetParent(visual.transform, false);
            weaponVisualRoot.localPosition = new Vector3(0.34f, 0.86f, 0.10f);
            weaponVisualRoot.localRotation = Quaternion.identity;
            weaponVisualRoot.localScale = Vector3.one;

            if (enemyRifleReceiverMaterial == null)
                enemyRifleReceiverMaterial = RuntimeMaterialFactory.Create(
                    "EnemyRifleReceiver", new Color(0.07f, 0.09f, 0.11f));
            if (enemyRifleMetalMaterial == null)
                enemyRifleMetalMaterial = RuntimeMaterialFactory.Create(
                    "EnemyRifleMetal", new Color(0.42f, 0.43f, 0.39f));

            CreateEnemyWeaponPart("RifleStock", new Vector3(0f, 0f, -0.16f),
                new Vector3(0.12f, 0.10f, 0.22f), enemyRifleReceiverMaterial);
            CreateEnemyWeaponPart("RifleReceiver", new Vector3(0f, 0f, 0.12f),
                new Vector3(0.16f, 0.13f, 0.38f), enemyRifleReceiverMaterial);
            CreateEnemyWeaponPart("RifleBarrel", new Vector3(0f, 0.015f, 0.43f),
                new Vector3(0.065f, 0.065f, 0.28f), enemyRifleMetalMaterial);
        }

        private void CreateEnemyWeaponPart(
            string partName,
            Vector3 localPosition,
            Vector3 localScale,
            Material material)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = partName;
            part.transform.SetParent(weaponVisualRoot, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;

            Collider collider = part.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        private void Awake()
        {
            CapsuleCollider capsule = GetComponent<CapsuleCollider>();
            if (capsule != null)
                collisionRadius = Mathf.Max(
                    0.35f,
                    capsule.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z));

            // Wait for Configure() so the correct archetype is known before loading art.
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

            // Ranged combat is intentionally enabled on Android as well. The
            // projectile uses the lightweight EnemyProjectile sweep path, so enemies
            // can fight the player without introducing a second physics system.
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

            // Ignore this enemy's own collider and projectile colliders. A ray that
            // starts inside the enemy capsule must never make the enemy think the
            // player is behind a wall.
            RaycastHit[] hits = Physics.RaycastAll(
                origin,
                direction.normalized,
                distance,
                ~0,
                QueryTriggerInteraction.Ignore);

            if (hits == null || hits.Length == 0)
                return true;

            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (RaycastHit hit in hits)
            {
                if (hit.collider == null)
                    continue;

                Transform hitTransform = hit.collider.transform;

                if (hitTransform == transform || hitTransform.IsChildOf(transform))
                    continue;

                if (hit.collider.GetComponentInParent<EnemyChase>() != null ||
                    hit.collider.GetComponentInParent<EnemyProjectile>() != null ||
                    hit.collider.GetComponentInParent<Projectile>() != null)
                    continue;

                return hit.collider.GetComponentInParent<PlayerController>() == player;
            }

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

#if UNITY_ANDROID
            GameObject projectile = AndroidSafeRuntimeFactory.CreateProjectile(
                "EnemyProjectile",
                origin + shotDirection * 0.16f,
                Quaternion.LookRotation(shotDirection, Vector3.up),
                new Vector3(0.07f, 0.07f, 0.07f),
                new Color(0.92f, 0.18f, 0.10f));
#else
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
                renderer.sharedMaterial = RuntimeMaterialFactory.Create(
                    "EnemyProjectileMaterial",
                    new Color(0.92f, 0.18f, 0.10f));
#endif

            EnemyProjectile shot = projectile.AddComponent<EnemyProjectile>();
            shot.Configure(shotDirection, rangedDamage, transform);

            if (visual != null)
                visual.PlayFire();
        }
    }
}
