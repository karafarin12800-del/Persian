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
        private static Material enemyRangeRingMaterial;
        private LineRenderer rangeRing;
        private const int RangeRingSegments = 48;

        public void SetTarget(Transform targetTransform)
        {
            target = targetTransform;
        }

        public int ScoreValue => archetype == 3 ? 40 : (archetype == 2 ? 20 : 10);
        public int Archetype => archetype;

        public WeaponController.WeaponKind DroppedWeaponKind => archetype == 1
            ? WeaponController.WeaponKind.LightPistol
            : (archetype == 2
                ? WeaponController.WeaponKind.HeavyMachineGun
                : WeaponController.WeaponKind.AssaultRifle);

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
            // Effective enemy weapon ranges are intentionally halved for fairer combat:
            // pistol = 1.5m, machine gun = 2.5m, AK-style rifle = 4m.
            rangedRange = archetype == 3 ? 4f : (archetype == 2 ? 2.5f : 1.5f);
            stopDistance = Mathf.Max(1f, rangedRange * 0.70f);
            meleeCooldown = archetype == 3 ? 1.05f : (archetype == 2 ? 1.25f : 1.5f);
            rangedCooldown = archetype == 3 ? 0.90f : (archetype == 2 ? 1.10f : 1.35f);
            if (visual == null)
                visual = StylizedCharacterVisual.Attach(transform, false, archetype);
            else
                visual.Configure(false, archetype);


            if (visual != null)
                visual.Configure(false, archetype);

            EnsureEnemyWeaponVisual();
            EnsureEnemyRangeIndicator();
        }

        private void EnsureEnemyRangeIndicator()
        {
            if (rangeRing != null)
                return;

            GameObject indicator = new GameObject("EnemyWeaponRangeIndicator");
            indicator.transform.SetParent(transform, false);
            rangeRing = indicator.AddComponent<LineRenderer>();
            rangeRing.useWorldSpace = true;
            rangeRing.loop = true;
            rangeRing.positionCount = RangeRingSegments;
            rangeRing.widthMultiplier = 0.045f;
            rangeRing.numCornerVertices = 1;
            rangeRing.numCapVertices = 0;
            rangeRing.alignment = LineAlignment.View;
            rangeRing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rangeRing.receiveShadows = false;

            if (enemyRangeRingMaterial == null)
                enemyRangeRingMaterial = RuntimeMaterialFactory.Create(
                    "EnemyWeaponRangeRingRed", new Color(1f, 0.18f, 0.12f, 0.82f));

            rangeRing.sharedMaterial = enemyRangeRingMaterial;
            rangeRing.startColor = new Color(1f, 0.18f, 0.12f, 0.82f);
            rangeRing.endColor = new Color(1f, 0.18f, 0.12f, 0.82f);
            rangeRing.enabled = true;
            UpdateEnemyRangeIndicator();
        }

        private void LateUpdate()
        {
            UpdateEnemyRangeIndicator();
        }

        private void UpdateEnemyRangeIndicator()
        {
            if (rangeRing == null)
                return;

            rangeRing.enabled = isActiveAndEnabled;
            if (!rangeRing.enabled)
                return;

            Vector3 center = transform.position;
            float radius = Mathf.Max(1f, rangedRange);
            const float groundY = 0.10f;
            for (int i = 0; i < RangeRingSegments; i++)
            {
                float angle = i / (float)RangeRingSegments * Mathf.PI * 2f;
                rangeRing.SetPosition(i, new Vector3(
                    center.x + Mathf.Cos(angle) * radius,
                    groundY,
                    center.z + Mathf.Sin(angle) * radius));
            }
        }

        private void EnsureEnemyWeaponVisual()
        {
            if (visual == null)
                return;

            if (weaponVisualRoot != null)
                return;

            // Mount the rifle to the character's 3D hand/body layer on mobile,
            // not to the outer aiming transform. This keeps it visibly in the hands
            // while the enemy separately faces its target for projectile aiming.
            Transform mount = visual.WeaponMount != null ? visual.WeaponMount : visual.transform;
            Transform existing = mount.Find("EnemyRifleVisual");
            if (existing != null)
            {
                weaponVisualRoot = existing;
                return;
            }

            weaponVisualRoot = new GameObject("EnemyRifleVisual").transform;
            weaponVisualRoot.SetParent(mount, false);
            weaponVisualRoot.localPosition = mount == visual.transform
                ? new Vector3(0.34f, 0.86f, 0.10f)
                : new Vector3(0.24f, 0.78f, 0.34f);
            weaponVisualRoot.localRotation = Quaternion.identity;
            weaponVisualRoot.localScale = Vector3.one;

            if (enemyRifleReceiverMaterial == null)
                enemyRifleReceiverMaterial = RuntimeMaterialFactory.Create(
                    "EnemyRifleReceiver", new Color(0.07f, 0.09f, 0.11f));
            if (enemyRifleMetalMaterial == null)
                enemyRifleMetalMaterial = RuntimeMaterialFactory.Create(
                    "EnemyRifleMetal", new Color(0.42f, 0.43f, 0.39f));

            if (archetype == 1)
            {
                // Compact pistol silhouette.
                CreateEnemyWeaponPart("PistolGrip", new Vector3(0f, -0.055f, -0.015f),
                    new Vector3(0.085f, 0.17f, 0.10f), enemyRifleReceiverMaterial);
                CreateEnemyWeaponPart("PistolSlide", new Vector3(0f, 0.025f, 0.105f),
                    new Vector3(0.10f, 0.085f, 0.27f), enemyRifleReceiverMaterial);
                CreateEnemyWeaponPart("PistolBarrel", new Vector3(0f, 0.025f, 0.265f),
                    new Vector3(0.045f, 0.045f, 0.10f), enemyRifleMetalMaterial);
            }
            else if (archetype == 2)
            {
                // Heavy machine-gun silhouette with a larger receiver and magazine.
                CreateEnemyWeaponPart("MachineGunStock", new Vector3(0f, 0f, -0.19f),
                    new Vector3(0.13f, 0.11f, 0.25f), enemyRifleReceiverMaterial);
                CreateEnemyWeaponPart("MachineGunReceiver", new Vector3(0f, 0f, 0.10f),
                    new Vector3(0.18f, 0.15f, 0.40f), enemyRifleReceiverMaterial);
                CreateEnemyWeaponPart("MachineGunMagazine", new Vector3(0f, -0.13f, 0.12f),
                    new Vector3(0.11f, 0.23f, 0.16f), enemyRifleMetalMaterial);
                CreateEnemyWeaponPart("MachineGunBarrel", new Vector3(0f, 0.015f, 0.43f),
                    new Vector3(0.075f, 0.075f, 0.30f), enemyRifleMetalMaterial);
            }
            else
            {
                // AK-style rifle silhouette with a distinct magazine.
                CreateEnemyWeaponPart("AKStock", new Vector3(0f, 0f, -0.18f),
                    new Vector3(0.12f, 0.10f, 0.25f), enemyRifleReceiverMaterial);
                CreateEnemyWeaponPart("AKReceiver", new Vector3(0f, 0f, 0.12f),
                    new Vector3(0.16f, 0.13f, 0.38f), enemyRifleReceiverMaterial);
                CreateEnemyWeaponPart("AKMagazine", new Vector3(0f, -0.13f, 0.13f),
                    new Vector3(0.10f, 0.24f, 0.15f), enemyRifleMetalMaterial);
                CreateEnemyWeaponPart("RifleBarrel", new Vector3(0f, 0.015f, 0.43f),
                    new Vector3(0.065f, 0.065f, 0.30f), enemyRifleMetalMaterial);
            }
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
            shot.Configure(shotDirection, rangedDamage, transform, rangedRange);

            if (visual != null)
                visual.PlayFire();
        }
    }
}
