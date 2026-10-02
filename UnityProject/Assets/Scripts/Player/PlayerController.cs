using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 11.5f;
        [SerializeField] private float worldLimit = 94f;
        [SerializeField] private float turnSpeed = 18f;
        [SerializeField] private float collisionRadius = 0.62f;
        [SerializeField] private int shield = 0;

        private Vector3 input;
        private WeaponController weapon;
        private NearestTargetAim aim;
        private TargetHealth health;
        private PlayerInventory inventory;
        private GrenadeController grenadeController;

        public NearestTargetAim Aim => aim;
        public WeaponController Weapon => weapon;
        public TargetHealth Health => health;
        public PlayerInventory Inventory => inventory;
        public GrenadeController Grenades => grenadeController;
        public int Shield => shield;
        public Vector2 MoveInput => new Vector2(input.x, input.z);
        public bool IsDefeated { get; private set; }
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
            EnsurePlayerVisual();
            EnsureGameplayComponents();
            StylizedCharacterVisual visual = GetComponentInChildren<StylizedCharacterVisual>(true);
            if (visual != null)
                visual.ConfigurePlayerHero(heroIndex);
            StartupCheckpoint.Set("PlayerPreparedForMatch");
        }

        public void SetMoveInput(Vector2 value)
        {
            if (IsDefeated)
            {
                input = Vector3.zero;
                return;
            }

            Vector2 clamped = Vector2.ClampMagnitude(value, 1f);
            input = new Vector3(clamped.x, 0f, clamped.y);
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

        public void HandleDefeat()
        {
            IsDefeated = true;
            input = Vector3.zero;
            enabled = false;
            GameSession.Instance?.EndMission(false);
        }

        private void Update()
        {
            if (IsDefeated) return;

            Vector3 desired = input * moveSpeed * Time.deltaTime;
            Vector3 next = transform.position + desired;
            next.x = Mathf.Clamp(next.x, -worldLimit, worldLimit);
            next.z = Mathf.Clamp(next.z, -worldLimit, worldLimit);
            next.y = 0f;

            if (desired.sqrMagnitude > 0.00001f && !WouldCollide(next))
                transform.position = next;

            if (input.sqrMagnitude > 0.0001f)
            {
                Quaternion targetRotation = Quaternion.LookRotation(input, Vector3.up);
                transform.rotation = Quaternion.Slerp(
                    transform.rotation,
                    targetRotation,
                    1f - Mathf.Exp(-turnSpeed * Time.deltaTime));
            }
        }

        private bool WouldCollide(Vector3 position)
        {
            Collider[] hits = Physics.OverlapSphere(
                position + Vector3.up * 0.7f,
                collisionRadius,
                ~0,
                QueryTriggerInteraction.Ignore);

            foreach (Collider hit in hits)
            {
                if (hit.transform == transform || hit.transform.IsChildOf(transform)) continue;
                if (hit.GetComponentInParent<EnemyChase>() != null) continue;
                if (hit.GetComponentInParent<Projectile>() != null) continue;
                if (hit.GetComponentInParent<EnemyProjectile>() != null) continue;
                return true;
            }

            return false;
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
            StylizedCharacterVisual visual = StylizedCharacterVisual.Attach(transform, true, 1);
            visual.ConfigurePlayerHero(0);
        }
    }
}