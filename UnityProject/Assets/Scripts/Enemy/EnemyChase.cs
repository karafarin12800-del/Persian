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
        private const int NavigationGridMaxSide = 40;
        private const int NavigationCellCapacity = NavigationGridMaxSide * NavigationGridMaxSide;
        private const int MaxNavigationWaypoints = 96;
        private const float NavigationLookAhead = 17f;
        private const float NavigationPadding = 8f;
        private const float NavigationWaypointReach = 0.90f;

        // Allocated once per enemy. Clear-path pursuit stays cheap; the local
        // occupancy grid is generated only when a wall/prop blocks direct movement.
        private readonly Collider[] movementHits = new Collider[64];
        private readonly RaycastHit[] navigationRayHits = new RaycastHit[64];
        private readonly bool[] navigationBlocked = new bool[NavigationCellCapacity];
        private readonly int[] navigationPrevious = new int[NavigationCellCapacity];
        private readonly int[] navigationQueue = new int[NavigationCellCapacity];
        private readonly int[] navigationReversePath = new int[NavigationCellCapacity];
        private readonly Vector3[] navigationWaypoints = new Vector3[MaxNavigationWaypoints];
        private int navigationWaypointCount;
        private int navigationWaypointIndex;
        private Vector3 navigationTargetSnapshot;
        private float nextNavigationPlanTime;

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

            // Faster mobile combat pacing, tuned by the enemy's carried weapon:
            // pistol users close distance fastest; heavy-gun users move more slowly.
            // Keep enemies challenging but controllable on mobile: reduce the
            // previous sprint speeds by 25 percent.
            moveSpeed = DroppedWeaponKind == WeaponController.WeaponKind.LightPistol
                ? 7.83f
                : (DroppedWeaponKind == WeaponController.WeaponKind.HeavyMachineGun ? 6.21f : 7.0875f);
            meleeDamage = archetype == 3 ? 14 : (archetype == 2 ? 9 : 7);
            rangedDamage = archetype == 3 ? 15 : (archetype == 2 ? 10 : 8);
            // Use the same configured range as the weapon the enemy visibly carries/drops.
            // Archetype 1=pistol, 2=heavy machine gun, 3=assault rifle.
            rangedRange = WeaponController.GetEffectiveRangeForKind(DroppedWeaponKind);
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
                ? new Vector3(0.20f, 0.83f, 0.28f)
                : new Vector3(0.16f, 0.72f, 0.42f);
            // Make the silhouette readable at the game's isometric camera distance.
            weaponVisualRoot.localRotation = Quaternion.Euler(0f, -8f, 0f);
            weaponVisualRoot.localScale = Vector3.one * 1.45f;

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
            part.transform.localRotation = Quaternion.identity;

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
            PlayerController player = target.GetComponentInParent<PlayerController>();
            bool hasLineOfSight = player == null || HasLineOfSightToPlayer(player);

            // Lightweight mobile AI: when critically wounded, disengage and create
            // distance instead of blindly charging. Reuses existing obstacle navigation.
            TargetHealth ownHealth = GetComponent<TargetHealth>();
            bool retreating = ownHealth != null &&
                ownHealth.MaxHealth > 0 &&
                ownHealth.CurrentHealth <= Mathf.CeilToInt(ownHealth.MaxHealth * 0.30f) &&
                player != null;
            Vector3 movementDirection = retreating ? -direction : direction;

            // If a wall blocks sight, keep navigating even inside the normal stop range
            // instead of idling against a wall with no attack lane.
            bool shouldMove = retreating || distance > stopDistance || (player != null && !hasLineOfSight);
            if (shouldMove)
                UpdatePursuitMovement(movementDirection, distance, retreating ? false : hasLineOfSight);
            else
                ClearNavigationPath();

            if (visual != null)
            {
                Vector3 facing = retreating
                    ? GetCurrentMovementDirection(movementDirection)
                    : (shouldMove && !hasLineOfSight ? GetCurrentMovementDirection(direction) : direction);
                visual.SetFacing(facing);
                visual.SetMoving(shouldMove);
            }

            if (player == null || retreating)
                return;

            if (distance <= meleeDistance && Time.time >= nextAttackTime && hasLineOfSight)
            {
                player.ReceiveDamage(meleeDamage);
                nextAttackTime = Time.time + meleeCooldown;
            }

            if (distance <= rangedRange && Time.time >= nextRangedTime && hasLineOfSight)
            {
                FireProjectile(direction);
                nextRangedTime = Time.time + rangedCooldown;
            }
        }

        private Vector3 GetCurrentMovementDirection(Vector3 fallback)
        {
            if (navigationWaypointIndex >= 0 && navigationWaypointIndex < navigationWaypointCount)
            {
                Vector3 delta = navigationWaypoints[navigationWaypointIndex] - transform.position;
                delta.y = 0f;
                if (delta.sqrMagnitude > 0.001f)
                    return delta.normalized;
            }
            return fallback;
        }

        private void UpdatePursuitMovement(Vector3 direction, float distance, bool hasLineOfSight)
        {
            if (navigationWaypointCount > 0 &&
                HorizontalDistance(target.position, navigationTargetSnapshot) > 3f)
                ClearNavigationPath();

            while (navigationWaypointIndex < navigationWaypointCount &&
                   HorizontalDistance(transform.position, navigationWaypoints[navigationWaypointIndex]) <= NavigationWaypointReach)
                navigationWaypointIndex++;

            float step = Mathf.Max(0.01f, moveSpeed * retargetInterval);
            if (navigationWaypointIndex < navigationWaypointCount)
            {
                Vector3 routeNext = Vector3.MoveTowards(
                    transform.position,
                    navigationWaypoints[navigationWaypointIndex],
                    step);
                routeNext.y = 0f;
                if (CanMoveTo(routeNext))
                {
                    transform.position = routeNext;
                    return;
                }

                // If the map changes or another actor blocks a planned node, abandon it
                // and replan instead of freezing in place.
                ClearNavigationPath();
                nextNavigationPlanTime = Time.time;
            }

            float keepDistance = hasLineOfSight ? stopDistance : 1.1f;
            float advance = Mathf.Min(step, Mathf.Max(0f, distance - keepDistance));
            if (advance > 0.01f)
            {
                Vector3 directNext = transform.position + direction * advance;
                directNext.y = 0f;
                if (CanMoveTo(directNext))
                {
                    transform.position = directNext;
                    ClearNavigationPath();
                    return;
                }
            }

            if (Time.time >= nextNavigationPlanTime)
                PlanLocalRoute(direction, distance, hasLineOfSight);
        }

        private void PlanLocalRoute(Vector3 direction, float distance, bool hasLineOfSight)
        {
            nextNavigationPlanTime = Time.time + 0.55f;
            ClearNavigationPath();

            Vector3 start = transform.position;
            start.y = 0f;
            float desiredGap = hasLineOfSight ? stopDistance : 1.1f;
            float advance = Mathf.Min(NavigationLookAhead, Mathf.Max(1.8f, distance - desiredGap));
            if (distance < 1.8f)
                advance = distance;
            if (advance < 0.8f)
                return;

            Vector3 requestedGoal = start + direction * advance;
            float minX = Mathf.Max(-94f, Mathf.Min(start.x, requestedGoal.x) - NavigationPadding);
            float maxX = Mathf.Min(94f, Mathf.Max(start.x, requestedGoal.x) + NavigationPadding);
            float minZ = Mathf.Max(-94f, Mathf.Min(start.z, requestedGoal.z) - NavigationPadding);
            float maxZ = Mathf.Min(94f, Mathf.Max(start.z, requestedGoal.z) + NavigationPadding);
            float cellSize = Mathf.Max(
                1.35f,
                Mathf.Max((maxX - minX) / (NavigationGridMaxSide - 1f),
                          (maxZ - minZ) / (NavigationGridMaxSide - 1f)));
            int columns = Mathf.Clamp(Mathf.CeilToInt((maxX - minX) / cellSize) + 1, 3, NavigationGridMaxSide);
            int rows = Mathf.Clamp(Mathf.CeilToInt((maxZ - minZ) / cellSize) + 1, 3, NavigationGridMaxSide);
            int total = columns * rows;

            for (int i = 0; i < total; i++)
            {
                navigationPrevious[i] = -2;
                navigationBlocked[i] = IsNavigationPointBlocked(GridToWorld(i, columns, minX, minZ, cellSize));
            }

            int startX = Mathf.Clamp(Mathf.RoundToInt((start.x - minX) / cellSize), 0, columns - 1);
            int startZ = Mathf.Clamp(Mathf.RoundToInt((start.z - minZ) / cellSize), 0, rows - 1);
            int startIndex = startZ * columns + startX;
            navigationBlocked[startIndex] = false;

            Vector3 flatDirection = new Vector3(direction.x, 0f, direction.z).normalized;
            int goalIndex = -1;
            float bestGoalScore = float.PositiveInfinity;
            for (int i = 0; i < total; i++)
            {
                if (navigationBlocked[i] || i == startIndex)
                    continue;

                Vector3 candidate = GridToWorld(i, columns, minX, minZ, cellSize);
                Vector3 progress = candidate - start;
                float forward = Vector3.Dot(progress, flatDirection);
                if (forward < 0.9f)
                    continue;

                float score = (candidate - requestedGoal).sqrMagnitude;
                if (forward < advance * 0.25f)
                    score += 12f;
                if (score < bestGoalScore)
                {
                    bestGoalScore = score;
                    goalIndex = i;
                }
            }

            if (goalIndex < 0)
                return;

            int head = 0;
            int tail = 0;
            navigationQueue[tail++] = startIndex;
            navigationPrevious[startIndex] = startIndex;
            bool found = false;

            // Local breadth-first search gives a collision-aware route without a baked
            // NavMesh. Diagonals cannot cut the corner where either side cell is blocked.
            while (head < tail)
            {
                int current = navigationQueue[head++];
                if (current == goalIndex)
                {
                    found = true;
                    break;
                }

                int cx = current % columns;
                int cz = current / columns;
                for (int dz = -1; dz <= 1; dz++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (dx == 0 && dz == 0)
                            continue;

                        int nx = cx + dx;
                        int nz = cz + dz;
                        if (nx < 0 || nx >= columns || nz < 0 || nz >= rows)
                            continue;

                        int next = nz * columns + nx;
                        if (navigationBlocked[next] || navigationPrevious[next] != -2)
                            continue;
                        if (dx != 0 && dz != 0 &&
                            (navigationBlocked[cz * columns + nx] ||
                             navigationBlocked[nz * columns + cx]))
                            continue;

                        navigationPrevious[next] = current;
                        navigationQueue[tail++] = next;
                    }
                }
            }

            if (!found)
                return;

            int reverseCount = 0;
            int cursor = goalIndex;
            while (cursor != startIndex && reverseCount < NavigationCellCapacity)
            {
                navigationReversePath[reverseCount++] = cursor;
                cursor = navigationPrevious[cursor];
                if (cursor < 0 || cursor >= total)
                    return;
            }

            if (cursor != startIndex || reverseCount == 0)
                return;

            // String-pull grid nodes into longer collider-checked segments so enemies
            // do not stutter at each small occupancy cell.
            Vector3 from = start;
            int farthestRemaining = reverseCount - 1;
            while (farthestRemaining >= 0 && navigationWaypointCount < MaxNavigationWaypoints)
            {
                int chosen = -1;
                for (int k = 0; k <= farthestRemaining; k++)
                {
                    Vector3 candidate = GridToWorld(
                        navigationReversePath[k], columns, minX, minZ, cellSize);
                    if (CanTravelDirectly(from, candidate))
                    {
                        chosen = k;
                        break;
                    }
                }

                if (chosen < 0)
                    chosen = farthestRemaining;

                Vector3 waypoint = GridToWorld(
                    navigationReversePath[chosen], columns, minX, minZ, cellSize);
                navigationWaypoints[navigationWaypointCount++] = waypoint;
                from = waypoint;
                farthestRemaining = chosen - 1;
            }

            navigationWaypointIndex = 0;
            navigationTargetSnapshot = target.position;
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private static Vector3 GridToWorld(int index, int columns, float minX, float minZ, float cellSize)
        {
            return new Vector3(
                minX + (index % columns) * cellSize,
                0f,
                minZ + (index / columns) * cellSize);
        }

        private bool IsNavigationPointBlocked(Vector3 position)
        {
            int count = Physics.OverlapSphereNonAlloc(
                position + Vector3.up * 0.75f,
                collisionRadius + 0.12f,
                movementHits,
                ~0,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                if (!IsIgnoredMovementCollider(movementHits[i]))
                    return true;
            }
            return false;
        }

        private bool CanMoveTo(Vector3 position)
        {
            int count = Physics.OverlapSphereNonAlloc(
                position + Vector3.up * 0.75f,
                collisionRadius,
                movementHits,
                ~0,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                if (!IsIgnoredMovementCollider(movementHits[i]))
                    return false;
            }
            return true;
        }

        private bool CanTravelDirectly(Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            delta.y = 0f;
            float distance = delta.magnitude;
            if (distance <= 0.1f)
                return true;

            int count = Physics.SphereCastNonAlloc(
                from + Vector3.up * 0.75f,
                collisionRadius * 0.72f,
                delta / distance,
                navigationRayHits,
                Mathf.Max(0f, distance - 0.08f),
                ~0,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                Collider hit = navigationRayHits[i].collider;
                if (hit != null && !IsIgnoredMovementCollider(hit))
                    return false;
            }
            return true;
        }

        private bool IsIgnoredMovementCollider(Collider hit)
        {
            if (hit == null)
                return true;
            if (hit.transform == transform || hit.transform.IsChildOf(transform))
                return true;
            if (hit.GetComponentInParent<EnemyChase>() != null)
                return true;
            if (hit.GetComponentInParent<PlayerController>() != null)
                return true;
            if (hit.GetComponentInParent<Projectile>() != null)
                return true;
            if (hit.GetComponentInParent<EnemyProjectile>() != null)
                return true;
            // The terrain mesh covers all ground and must not be treated as a blocking wall.
            if (hit is MeshCollider)
                return true;
            return false;
        }

        private void ClearNavigationPath()
        {
            navigationWaypointCount = 0;
            navigationWaypointIndex = 0;
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
                new Color(0.10f, 0.55f, 1f));
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
            RuntimeGameAudio.PlayEnemyShot();
        }
    }
}
