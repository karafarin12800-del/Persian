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

        // Tactical disengage is time-boxed; enemies resume combat instead of fleeing forever.
        [SerializeField] private float retreatDuration = 2.1f;
        [SerializeField] private float retreatCooldown = 6.0f;
        [SerializeField] private float stuckRecoveryDelay = 1.35f;
        [SerializeField] private float stuckRecoveryDuration = 2.2f;
        private float retreatUntil;
        private float nextRetreatAllowed;
        private float recoveryUntil;
        private float nextRecoveryAllowed;
        private float lastProgressTime;
        private Vector3 lastObservedPosition;
        private Vector3 recoveryDirection;
        private bool movementRequested;
        [SerializeField] private float fallbackWorldLimit = 108.1f;
        private PlayerController targetPlayer;
        private StylizedCharacterVisual visual;
        private CharacterRig libraryCharacterRig;
        private Transform libraryMuzzle;
        private Vector3 libraryCharacterLastPosition;
        private bool libraryCharacterPositionInitialized;
        private Transform weaponVisualRoot;
        private static Material enemyRifleReceiverMaterial;
        private static Material enemyRifleMetalMaterial;
        private static Material stageOneHoverboardMaterial;
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
            targetPlayer = target != null ? target.GetComponentInParent<PlayerController>() : null;
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
            SetTarget(targetTransform);
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
            // Use the shared detailed character meshes for every enemy archetype.
            // Fall back to the legacy visual only if a mesh/shader could not be created.
            EnsureLibraryCharacterVisual();
            if (libraryCharacterRig == null)
            {
                if (visual == null)
                    visual = StylizedCharacterVisual.Attach(transform, false, archetype);
                else
                    visual.Configure(false, archetype);
            }
            EnsureEnemyWeaponVisual();
            RemoveLegacyEnemyRangeIndicator();
        }

        private void RemoveLegacyEnemyRangeIndicator()
        {
            // Remove the old 48-segment LineRenderer from any reused/legacy enemy object.
            Transform legacyIndicator = transform.Find("EnemyWeaponRangeIndicator");
            if (legacyIndicator != null)
                Destroy(legacyIndicator.gameObject);
        }

        private void EnsureLibraryCharacterVisual()
        {
            string objectName = transform.name;
            bool stageOneAlien = objectName.StartsWith("Enemy_W", System.StringComparison.Ordinal);
            bool stageThreeAlien = objectName.StartsWith("S3_Alien", System.StringComparison.Ordinal);
            bool stageFourAlien = objectName.StartsWith("S4_AlienTrooper_", System.StringComparison.Ordinal);
            bool stageTwoAlien = objectName.StartsWith("Alien_W", System.StringComparison.Ordinal);
            if (libraryCharacterRig != null) return;

            CharacterStyle style;
            if (stageOneAlien)
                style = archetype == 1 ? CharacterStyle.AlienScout
                    : archetype == 2 ? CharacterStyle.AlienBrute : CharacterStyle.AlienHunter;
            else if (stageFourAlien)
                style = CharacterStyle.AlienTrooper;
            else if (stageThreeAlien)
                style = archetype == 1 ? CharacterStyle.AlienScout
                    : archetype == 2 ? CharacterStyle.AlienBrute : CharacterStyle.AlienHunter;
            else if (stageTwoAlien)
                style = archetype == 1 ? CharacterStyle.StageTwoAlienScout
                    : archetype == 2 ? CharacterStyle.StageTwoAlienBrute : CharacterStyle.StageTwoAlienElite;
            else
                style = archetype == 1 ? CharacterStyle.Soldier
                    : archetype == 2 ? CharacterStyle.RoyalScout : CharacterStyle.DesertKnight;

            // Enemy models receive world lighting and shadows but do not cast their
            // own realtime shadows in large waves, avoiding a large GPU shadow cost.
            CharacterRig created = CharacterModelFactory.Create(
                style, transform, Vector3.zero, 1f, false, true);
            if (created == null) return;

            libraryCharacterRig = created;
            if (visual != null)
                visual.gameObject.SetActive(false);
            if (stageOneAlien)
                AttachStageOneHoverboard(created.transform, archetype);
            if (stageTwoAlien)
            {
                // Stage 2's old capsule was only a placeholder body. Keep its
                // collider/TargetHealth on the root, but hide the placeholder renderer.
                Renderer placeholderRenderer = GetComponent<Renderer>();
                if (placeholderRenderer != null)
                    placeholderRenderer.enabled = false;
            }
            libraryCharacterPositionInitialized = false;
        }

        private static void AttachStageOneHoverboard(Transform characterRoot, int enemyArchetype)
        {
            if (characterRoot == null || characterRoot.Find("StageOneAlienHoverboard") != null)
                return;

            if (stageOneHoverboardMaterial == null)
                stageOneHoverboardMaterial = RuntimeMaterialFactory.Create(
                    "StageOneAlienHoverboard", new Color(0.045f, 0.16f, 0.20f));

            GameObject board = GameObject.CreatePrimitive(PrimitiveType.Cube);
            board.name = "StageOneAlienHoverboard";
            board.transform.SetParent(characterRoot, false);
            board.transform.localPosition = new Vector3(0f, -0.14f, -0.015f);
            board.transform.localRotation = Quaternion.identity;
            board.transform.localScale = enemyArchetype == 2
                ? new Vector3(1.18f, 0.12f, 0.62f)
                : (enemyArchetype == 3
                    ? new Vector3(1.12f, 0.10f, 0.50f)
                    : new Vector3(1.02f, 0.10f, 0.46f));

            Collider boardCollider = board.GetComponent<Collider>();
            if (boardCollider != null)
                Destroy(boardCollider);
            Renderer renderer = board.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = stageOneHoverboardMaterial;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = true;
            }

            // Two swept side rails make the levitating board readable from the
            // isometric camera instead of looking like a thin dark floor slab.
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rail.name = side < 0 ? "StageOneAlienHoverboard_RailL" : "StageOneAlienHoverboard_RailR";
                rail.transform.SetParent(board.transform, false);
                rail.transform.localPosition = new Vector3(side * 0.48f, 0.22f, 0.02f);
                rail.transform.localRotation = Quaternion.Euler(0f, side * -9f, side * 7f);
                rail.transform.localScale = new Vector3(0.18f, 0.42f, 0.62f);
                Collider railCollider = rail.GetComponent<Collider>();
                if (railCollider != null)
                    Destroy(railCollider);
                Renderer railRenderer = rail.GetComponent<Renderer>();
                if (railRenderer != null)
                {
                    railRenderer.sharedMaterial = stageOneHoverboardMaterial;
                    railRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    railRenderer.receiveShadows = true;
                }
            }
        }

        private void EnsureEnemyWeaponVisual()
        {
            if (visual == null && libraryCharacterRig == null)
                return;

            if (weaponVisualRoot != null)
                return;

            // Mount the rifle to the character's 3D hand/body layer on mobile,
            // not to the outer aiming transform. This keeps it visibly in the hands
            // while the enemy separately faces its target for projectile aiming.
            Transform mount = libraryCharacterRig != null
                ? libraryCharacterRig.weaponMount
                : (visual.WeaponMount != null ? visual.WeaponMount : visual.transform);
            Transform existing = mount.Find("EnemyRifleVisual");
            if (existing != null)
            {
                weaponVisualRoot = existing;
                return;
            }

            weaponVisualRoot = new GameObject("EnemyRifleVisual").transform;
            weaponVisualRoot.SetParent(mount, false);
            weaponVisualRoot.localPosition = libraryCharacterRig != null
                ? Vector3.zero
                : (mount == visual.transform
                    ? new Vector3(0.20f, 0.83f, 0.28f)
                    : new Vector3(0.16f, 0.72f, 0.42f));
            // Make the silhouette readable at the game's isometric camera distance.
            weaponVisualRoot.localRotation = libraryCharacterRig != null
                ? Quaternion.identity : Quaternion.Euler(0f, -8f, 0f);
            weaponVisualRoot.localScale = Vector3.one * 1.45f;

            if (libraryCharacterRig != null)
            {
                AttachLibraryEnemyWeapon();
                return;
            }

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

            AttachLibraryEnemyWeapon();
        }

        private void AttachLibraryEnemyWeapon()
        {
            if (weaponVisualRoot == null) return;
            bool alien = transform.name.IndexOf("Alien", System.StringComparison.OrdinalIgnoreCase) >= 0
                || transform.name.StartsWith("S3_") || transform.name.StartsWith("S4_");
            ModelKind kind = alien ? ModelKind.VoidRifle
                : archetype == 1 ? ModelKind.Pistol
                : archetype == 2 ? ModelKind.HeavyMachineGun : ModelKind.AssaultRifle;

            Transform model = weaponVisualRoot.Find("PW_" + kind);
            if (model == null)
                model = ModelLibrary.AttachWeapon(weaponVisualRoot, kind, true, Vector3.zero,
                    Quaternion.identity, 0.75f)?.transform;
            if (model == null) return;

            Renderer[] renderers = weaponVisualRoot.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null)
                    renderers[i].enabled = renderers[i].transform == model || renderers[i].transform.IsChildOf(model);

            if (libraryCharacterRig != null && libraryMuzzle == null && model != null)
            {
                GameObject muzzleObject = new GameObject("LibraryWeaponMuzzle");
                libraryMuzzle = muzzleObject.transform;
                libraryMuzzle.SetParent(model, false);
                libraryMuzzle.localPosition = new Vector3(0f, 0f, 0.92f);
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
            if (target != null)
                targetPlayer = target.GetComponentInParent<PlayerController>();

            CapsuleCollider capsule = GetComponent<CapsuleCollider>();
            if (capsule != null)
                collisionRadius = Mathf.Max(
                    0.35f,
                    capsule.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z));

            lastObservedPosition = transform.position;
            lastProgressTime = Time.time;

            // Wait for Configure() so the correct archetype is known before loading art.
        }

        private void Update()
        {
            // Keep actors inside the arena and detect stalled movement every frame,
            // not only on the slower mobile AI retarget ticks.
            ClampInsidePlayableArea();
            TrackMovementProgress();

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

            TargetHealth ownHealth = GetComponent<TargetHealth>();
            bool criticallyWounded = ownHealth != null &&
                ownHealth.MaxHealth > 0 &&
                ownHealth.CurrentHealth <= Mathf.CeilToInt(ownHealth.MaxHealth * 0.30f) &&
                player != null;

            // Retreat only for a short burst, then re-engage. This avoids the old
            // permanent flee state that pushed wounded enemies into map corners.
            if (criticallyWounded && Time.time >= nextRetreatAllowed)
            {
                retreatUntil = Time.time + retreatDuration;
                nextRetreatAllowed = Time.time + retreatCooldown;
            }

            bool retreating = criticallyWounded && Time.time < retreatUntil;
            bool recoveringFromStall = Time.time < recoveryUntil;
            Vector3 movementDirection = retreating
                ? GetBoundaryAwareRetreatDirection(-direction)
                : direction;

            // A lateral recovery step breaks wall/corner deadlocks; it takes priority
            // over both pursuit and retreat for a short, bounded interval.
            if (recoveringFromStall)
                movementDirection = recoveryDirection;

            // If a wall blocks sight, keep navigating even inside the normal stop range.
            bool shouldMove = recoveringFromStall || retreating ||
                distance > stopDistance || (player != null && !hasLineOfSight);
            movementRequested = shouldMove;

            if (shouldMove)
                UpdatePursuitMovement(
                    movementDirection,
                    distance,
                    (retreating || recoveringFromStall) ? false : hasLineOfSight);
            else
                ClearNavigationPath();

            if (visual != null || libraryCharacterRig != null)
            {
                bool repositioning = retreating || recoveringFromStall ||
                    (shouldMove && !hasLineOfSight);
                Vector3 facing = repositioning
                    ? GetCurrentMovementDirection(movementDirection)
                    : direction;

                if (libraryCharacterRig != null)
                {
                    libraryCharacterRig.moveSpeed01 = shouldMove ? Mathf.Clamp01(moveSpeed / 8.5f) : 0f;
                    libraryCharacterRig.aiming = player != null && !retreating &&
                        hasLineOfSight && distance <= rangedRange;
                    if (facing.sqrMagnitude > 0.001f)
                    {
                        Quaternion rigFacing = Quaternion.LookRotation(facing.normalized, Vector3.up);
                        libraryCharacterRig.transform.rotation = Quaternion.Slerp(
                            libraryCharacterRig.transform.rotation, rigFacing,
                            1f - Mathf.Exp(-16f * Time.deltaTime));
                    }
                }
                else
                {
                    visual.SetFacing(facing);
                    visual.SetMoving(shouldMove);
                }
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

        private void TrackMovementProgress()
        {
            Vector3 current = transform.position;
            if (!movementRequested)
            {
                lastObservedPosition = current;
                lastProgressTime = Time.time;
                return;
            }

            if (HorizontalDistance(current, lastObservedPosition) >= 0.18f)
            {
                lastObservedPosition = current;
                lastProgressTime = Time.time;
                return;
            }

            lastObservedPosition = current;
            if (Time.time - lastProgressTime < stuckRecoveryDelay ||
                Time.time < nextRecoveryAllowed)
                return;

            recoveryDirection = ChooseRecoveryDirection();
            recoveryUntil = Time.time + stuckRecoveryDuration;
            nextRecoveryAllowed = recoveryUntil + 0.8f;
            lastProgressTime = Time.time;
            ClearNavigationPath();
        }

        private Vector3 GetBoundaryAwareRetreatDirection(Vector3 awayFromTarget)
        {
            float safeLimit = Mathf.Max(1f, GetWorldLimit() - 5f);
            Vector3 position = transform.position;

            if ((position.x >= safeLimit && awayFromTarget.x > 0f) ||
                (position.x <= -safeLimit && awayFromTarget.x < 0f))
                awayFromTarget.x = 0f;

            if ((position.z >= safeLimit && awayFromTarget.z > 0f) ||
                (position.z <= -safeLimit && awayFromTarget.z < 0f))
                awayFromTarget.z = 0f;

            if (awayFromTarget.sqrMagnitude < 0.01f)
            {
                // At a corner, move inward rather than repeatedly pressing against bounds.
                awayFromTarget = new Vector3(
                    -Mathf.Sign(position.x),
                    0f,
                    -Mathf.Sign(position.z));
            }

            return awayFromTarget.sqrMagnitude > 0.01f
                ? awayFromTarget.normalized
                : Vector3.forward;
        }

        private Vector3 ChooseRecoveryDirection()
        {
            Vector3 towardTarget = target != null
                ? target.position - transform.position
                : transform.forward;
            towardTarget.y = 0f;
            if (towardTarget.sqrMagnitude < 0.01f)
                towardTarget = transform.forward;
            towardTarget.Normalize();

            Vector3 tangent = Vector3.Cross(Vector3.up, towardTarget).normalized;
            if ((GetInstanceID() & 1) != 0)
                tangent = -tangent;

            Vector3 candidate = transform.position + tangent * 2.5f;
            ClampToPlayableArea(ref candidate);
            if (CanMoveTo(candidate))
                return tangent;

            tangent = -tangent;
            candidate = transform.position + tangent * 2.5f;
            ClampToPlayableArea(ref candidate);
            if (CanMoveTo(candidate))
                return tangent;

            Vector3 inward = new Vector3(
                -Mathf.Sign(transform.position.x),
                0f,
                -Mathf.Sign(transform.position.z)).normalized;
            candidate = transform.position + inward * 2.5f;
            ClampToPlayableArea(ref candidate);
            if (inward.sqrMagnitude > 0.01f && CanMoveTo(candidate))
                return inward;

            // Last resort: advance toward the fight instead of staying in place.
            return towardTarget;
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
                ClampToPlayableArea(ref routeNext);
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
                ClampToPlayableArea(ref directNext);
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

        private float GetWorldLimit()
        {
            if (targetPlayer == null && target != null)
                targetPlayer = target.GetComponentInParent<PlayerController>();

            return targetPlayer != null
                ? Mathf.Max(1f, targetPlayer.WorldLimit)
                : Mathf.Max(1f, fallbackWorldLimit);
        }

        private void ClampToPlayableArea(ref Vector3 position)
        {
            float limit = GetWorldLimit();
            position.x = Mathf.Clamp(position.x, -limit, limit);
            position.z = Mathf.Clamp(position.z, -limit, limit);
        }

        private void ClampInsidePlayableArea()
        {
            Vector3 position = transform.position;
            Vector3 clamped = position;
            ClampToPlayableArea(ref clamped);
            if ((position - clamped).sqrMagnitude > 0.0001f)
                transform.position = clamped;
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

            Vector3 origin = libraryMuzzle != null ? libraryMuzzle.position
                : (visual != null && visual.Muzzle != null
                    ? visual.Muzzle.position
                    : transform.position + Vector3.up * 0.9f);

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

            bool alienWeapon = transform.name.IndexOf("Alien", System.StringComparison.OrdinalIgnoreCase) >= 0
                || transform.name.StartsWith("S3_") || transform.name.StartsWith("S4_");
            MuzzleFlashFX.Show(libraryMuzzle != null ? libraryMuzzle
                : (visual != null ? visual.Muzzle : null), alienWeapon);

            if (libraryCharacterRig != null)
                libraryCharacterRig.Kick(1f);
            else if (visual != null)
                visual.PlayFire(rangedCooldown);
            RuntimeGameAudio.PlayEnemyShot();
        }
    }
}
