using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 11.5f;
        [SerializeField] private float worldLimit = 108.1f;
        [SerializeField] private float turnSpeed = 18f;
        [SerializeField] private float collisionRadius = 0.62f;
        [SerializeField] private int shield = 0;
        [SerializeField] private float spawnMovementLockSeconds = 0.45f;

        // Reusable movement query buffer avoids allocating a Collider[] every frame.
        private static readonly Collider[] MovementCollisionBuffer = new Collider[128];
        private Vector3 input;
        private Vector3 aimDirection;
        private bool hasAimDirection;
        private WeaponController weapon;
        private NearestTargetAim aim;
        private TargetHealth health;
        private PlayerInventory inventory;
        private GrenadeController grenadeController;
        private StylizedCharacterVisual visual;
        private float groundY;
        private float movementLockUntil;
        private bool movementEnabled = true;
        [SerializeField] private int quickDashCharges;
        public int QuickDashCharges => quickDashCharges;

        public NearestTargetAim Aim => aim;
        public WeaponController Weapon => weapon;
        public TargetHealth Health => health;
        public PlayerInventory Inventory => inventory;
        public GrenadeController Grenades => grenadeController;
        public int Shield => shield;
        public float WorldLimit => worldLimit;
        public Vector2 MoveInput => new Vector2(input.x, input.z);
        public bool IsDefeated { get; private set; }
        public bool IsMovementEnabled => movementEnabled && Time.time >= movementLockUntil && !IsDefeated;
        public bool IsGameplayReady => health != null && weapon != null && aim != null && inventory != null && grenadeController != null;

        private void OnEnable()
        {
            StartupCheckpoint.Set("PlayerOnEnable");
        }

        private void Awake()
        {
            StartupCheckpoint.Set("PlayerAwakeEntered");
            // Keep scene activation lightweight on Android. Visual construction is deferred
            // until Start and uses the shared sprite presentation path instead of a large
            // set of runtime 3D primitives.
            StartupCheckpoint.Set("PlayerAwakeStarted");
        }

        private void Start()
        {
            EnsurePlayerVisual();
            StartupCheckpoint.Set("PlayerVisualBuilt");
            EnsureGameplayComponents();
            StartupCheckpoint.Set("PlayerComponentsReady");
        }

        public void PrepareForMatch(int heroIndex)
        {
            IsDefeated = false;
            input = Vector3.zero;
            aimDirection = Vector3.zero;
            hasAimDirection = false;
            movementEnabled = true;
            groundY = 0f;
            movementLockUntil = Time.time + Mathf.Max(0f, spawnMovementLockSeconds);

            EnsureStablePhysics();
            EnsurePlayerVisual();
            EnsureGameplayComponents();
            visual = GetComponentInChildren<StylizedCharacterVisual>(true);
            if (visual != null)
                visual.ConfigurePlayerHero(heroIndex);
            StartupCheckpoint.Set("PlayerPreparedForMatch");
        }

        public void SetGroundedPosition(Vector3 worldPosition)
        {
            groundY = worldPosition.y;
            Vector3 position = worldPosition;
            position.y = groundY;
            transform.position = position;
        }

        public void SetMovementEnabled(bool enabled)
        {
            movementEnabled = enabled;
            if (!enabled)
            {
                SetMoveInput(Vector2.zero);
                ClearAimDirection();
            }
        }

        public void LockMovementFor(float seconds)
        {
            movementLockUntil = Mathf.Max(movementLockUntil, Time.time + Mathf.Max(0f, seconds));
            input = Vector3.zero;
        }

        public void SetMoveInput(Vector2 value)
        {
            if (!movementEnabled || IsDefeated)
            {
                input = Vector3.zero;
                return;
            }

            Vector2 clamped = Vector2.ClampMagnitude(value, 1f);
            input = new Vector3(clamped.x, 0f, clamped.y);
        }

        // Aim is a separate intent from movement: the player can keep strafing
        // while facing the assisted target under the held-fire control.
        public void SetAimDirection(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f || IsDefeated)
            {
                hasAimDirection = false;
                aimDirection = Vector3.zero;
                return;
            }

            aimDirection = direction.normalized;
            hasAimDirection = true;
        }

        public void ClearAimDirection()
        {
            hasAimDirection = false;
            aimDirection = Vector3.zero;
        }

        public void ReceiveDamage(int amount)
        {
            if (IsDefeated || health == null) return;

            amount = Mathf.Max(0, amount);
            if (amount == 0) return;

            int blocked = Mathf.Min(shield, amount);
            shield -= blocked;
            amount -= blocked;

            if (amount > 0)
                health.ApplyDamage(amount);
        }

        public void Heal(int amount)
        {
            if (health != null)
                health.Restore(amount);
        }

        public void AddShield(int amount)
        {
            shield = Mathf.Clamp(shield + Mathf.Max(0, amount), 0, 100);
        }

        public void SetShield(int amount)
        {
            shield = Mathf.Clamp(amount, 0, 100);
        }

        public void AddQuickDashCharges(int amount) { quickDashCharges = Mathf.Clamp(quickDashCharges + Mathf.Max(0, amount), 0, 12); }
        public bool TryQuickDash()
        {
            if (!IsMovementEnabled || quickDashCharges <= 0) return false;
            Vector3 direction = input.sqrMagnitude > 0.01f ? input.normalized : transform.forward;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f) return false;
            direction.Normalize(); Vector3 origin = transform.position; float moved = 0f;
            const float distance = 2.6f, step = 0.20f;
            while (moved < distance) { float delta = Mathf.Min(step, distance - moved); Vector3 candidate = origin + direction * (moved + delta); candidate.y = groundY; candidate.x = Mathf.Clamp(candidate.x, -worldLimit, worldLimit); candidate.z = Mathf.Clamp(candidate.z, -worldLimit, worldLimit); if (WouldCollide(candidate)) break; moved += delta; }
            if (moved < 0.35f) return false;
            transform.position = new Vector3(origin.x + direction.x * moved, groundY, origin.z + direction.z * moved); quickDashCharges--; StartupCheckpoint.Set("QuickDashUsed"); return true;
        }

        public void HandleDefeat()
        {
            IsDefeated = true;
            movementEnabled = false;
            input = Vector3.zero;
            ClearAimDirection();
            enabled = false;
            GameSession.Instance?.EndMission(false);
        }

        private void Update()
        {
            if (IsDefeated) return;

            // The battlefield is intentionally planar. Keep Y pinned to the match ground
            // so scene activation/camera hand-off cannot visually launch the player.
            Vector3 anchored = transform.position;
            anchored.y = groundY;
            transform.position = anchored;

            bool canMove = IsMovementEnabled;
            if (!canMove)
            {
                if (visual != null) visual.SetMoving(false);
                return;
            }

            float weaponMoveMultiplier = weapon != null ? weapon.MoveSpeedMultiplier : 1f;
            Vector3 desired = input * moveSpeed * weaponMoveMultiplier * Time.deltaTime;
            Vector3 next = transform.position + desired;
            next.x = Mathf.Clamp(next.x, -worldLimit, worldLimit);
            next.z = Mathf.Clamp(next.z, -worldLimit, worldLimit);
            next.y = 0f;

            if (desired.sqrMagnitude > 0.00001f)
            {
                if (!WouldCollide(next))
                {
                    transform.position = next;
                }
                else
                {
                    // Slide along walls and parked vehicles instead of stopping at
                    // every diagonal collision. Both candidate positions still pass
                    // through the same solid-obstacle query.
                    Vector3 slideX = new Vector3(next.x, next.y, transform.position.z);
                    Vector3 slideZ = new Vector3(transform.position.x, next.y, next.z);
                    if (!WouldCollide(slideX))
                        transform.position = slideX;
                    else if (!WouldCollide(slideZ))
                        transform.position = slideZ;
                }
            }

            if (visual != null)
                visual.SetMoving(input.sqrMagnitude > 0.0001f);

            Vector3 facingDirection = hasAimDirection ? aimDirection : input;
            if (facingDirection.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(facingDirection, Vector3.up);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    1f - Mathf.Exp(-turnSpeed * Time.deltaTime));
            }
        }

        private bool WouldCollide(Vector3 position)
        {
            int hitCount = Physics.OverlapSphereNonAlloc(
                position + Vector3.up * 0.7f,
                collisionRadius,
                MovementCollisionBuffer,
                ~0,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = MovementCollisionBuffer[i];
                if (hit == null) continue;
                if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;
                if (hit.GetComponentInParent<EnemyChase>() != null) continue;
                if (hit.GetComponentInParent<Projectile>() != null) continue;
                if (hit.GetComponentInParent<EnemyProjectile>() != null) continue;

                // The procedural battlefield terrain uses a MeshCollider covering
                // the whole ground. It must not block the player's horizontal
                // movement query; only solid world props/buildings should do so.
                if (hit is MeshCollider)
                    continue;

                return true;
            }

            return false;
        }

        private void EnsureStablePhysics()
        {
            // Player movement is transform-driven, not Rigidbody-driven. If an old
            // scene component ever carries a Rigidbody, gravity/velocity can launch
            // the player during match activation. Neutralize that path completely.
            Rigidbody body = GetComponent<Rigidbody>();
            if (body != null)
            {
                body.isKinematic = true;
                body.useGravity = false;
                body.velocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }

            // Keep the player collider as a trigger so it cannot physically push or
            // bounce against the procedural city while our explicit collision query
            // remains responsible for movement blocking.
            Collider ownCollider = GetComponent<Collider>();
            if (ownCollider != null)
                ownCollider.isTrigger = true;
        }

        private void EnsureGameplayComponents()
        {
            health = GetComponent<TargetHealth>();
            if (health == null) health = gameObject.AddComponent<TargetHealth>();

            weapon = GetComponent<WeaponController>();
            if (weapon == null) weapon = gameObject.AddComponent<WeaponController>();

            aim = GetComponent<NearestTargetAim>();
            if (aim == null) aim = gameObject.AddComponent<NearestTargetAim>();

            inventory = GetComponent<PlayerInventory>();
            if (inventory == null) inventory = gameObject.AddComponent<PlayerInventory>();

            grenadeController = GetComponent<GrenadeController>();
            if (grenadeController == null) grenadeController = gameObject.AddComponent<GrenadeController>();
        }

        private void EnsurePlayerVisual()
        {
            visual = StylizedCharacterVisual.Attach(transform, true, 1);
            visual.ConfigurePlayerHero(0);
        }
    }
}