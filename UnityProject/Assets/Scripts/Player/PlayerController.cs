using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 7.2f;
        [SerializeField] private float worldLimit = 106f;
        [SerializeField] private float visualTurnSpeed = 18f;
        [SerializeField] private float collisionRadius = 0.62f;
        [SerializeField] private int shield = 0;

        private Vector3 input;
        private Transform visualRoot;
        private WeaponController weapon;
        private NearestTargetAim aim;
        private TargetHealth health;
        private PlayerInventory inventory;
        private GrenadeController grenadeController;
        private StylizedCharacterVisual characterVisual;

        public NearestTargetAim Aim => aim;
        public WeaponController Weapon => weapon;
        public TargetHealth Health => health;
        public PlayerInventory Inventory => inventory;
        public GrenadeController Grenades => grenadeController;
        public int Shield => shield;
        public Vector2 MoveInput => new Vector2(input.x, input.z);
        public Vector3 FacingDirection { get; private set; } = Vector3.forward;
        public bool IsDefeated { get; private set; }

        private void Awake()
        {
            EnsurePlayerVisual();
            EnsureGameplayComponents();
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

        public void SetFacingDirection(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0005f)
                return;

            FacingDirection = direction.normalized;
            if (characterVisual != null)
                characterVisual.SetFacing(FacingDirection);
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
            if (characterVisual != null)
                characterVisual.SetMoving(false);

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

            Vector3 desiredFacing = input;
            if (aim != null && aim.CurrentTarget != null)
            {
                Vector3 targetDelta = aim.CurrentTarget.transform.position - transform.position;
                targetDelta.y = 0f;
                if (targetDelta.sqrMagnitude > 0.001f)
                    desiredFacing = targetDelta.normalized;
            }

            if (desiredFacing.sqrMagnitude > 0.0001f)
                SetFacingDirection(desiredFacing);

            if (characterVisual != null)
                characterVisual.SetMoving(input.sqrMagnitude > 0.0001f);
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
                if (hit == null) continue;
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
            if (health == null)
                health = gameObject.AddComponent<TargetHealth>();

            weapon = GetComponent<WeaponController>();
            if (weapon == null)
                weapon = gameObject.AddComponent<WeaponController>();

            aim = GetComponent<NearestTargetAim>();
            if (aim == null)
                aim = gameObject.AddComponent<NearestTargetAim>();

            inventory = GetComponent<PlayerInventory>();
            if (inventory == null)
                inventory = gameObject.AddComponent<PlayerInventory>();

            grenadeController = GetComponent<GrenadeController>();
            if (grenadeController == null)
                grenadeController = gameObject.AddComponent<GrenadeController>();
        }

        private void EnsurePlayerVisual()
        {
            visualRoot = transform.Find("PlayerVisual");
            if (visualRoot == null)
            {
                visualRoot = new GameObject("PlayerVisual").transform;
                visualRoot.SetParent(transform, false);
            }

            characterVisual = visualRoot.GetComponent<StylizedCharacterVisual>();
            if (characterVisual == null)
                characterVisual = visualRoot.gameObject.AddComponent<StylizedCharacterVisual>();

            characterVisual.Configure(true, 1);
        }
    }
}
