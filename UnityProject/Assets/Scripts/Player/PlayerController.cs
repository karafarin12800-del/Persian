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
        private Transform visualRoot;
        private Transform body;
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

        public void ReceiveDamage(int amount)
        {
            if (IsDefeated || health == null) return;
            amount = Mathf.Max(0, amount);
            if (amount == 0) return;

            int blocked = Mathf.Min(shield, amount);
            shield -= blocked;
            amount -= blocked;
            if (amount > 0) health.ApplyDamage(amount);
        }

        public void Heal(int amount)
        {
            if (health != null) health.Restore(amount);
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
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 1f - Mathf.Exp(-turnSpeed * Time.deltaTime));
                if (body != null)
                {
                    float bob = Mathf.Sin(Time.time * 14f) * 0.035f;
                    body.localPosition = new Vector3(0f, 0.78f + bob, 0f);
                }
            }
            else if (body != null)
            {
                body.localPosition = Vector3.Lerp(body.localPosition, new Vector3(0f, 0.78f, 0f), 1f - Mathf.Exp(-12f * Time.deltaTime));
            }
        }

        private bool WouldCollide(Vector3 position)
        {
            Collider[] hits = Physics.OverlapSphere(position + Vector3.up * 0.7f, collisionRadius, ~0, QueryTriggerInteraction.Ignore);
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
            if (visualRoot != null || transform.Find("PlayerVisual") != null) return;

            visualRoot = new GameObject("PlayerVisual").transform;
            visualRoot.SetParent(transform, false);

            // Presentation-only upgrade: layered Persian warrior silhouette.
            body = CreatePart(PrimitiveType.Capsule, "Body", new Vector3(0f, 0.88f, 0f), new Vector3(0.66f, 0.86f, 0.66f), new Color(0.28f, 0.34f, 0.40f));
            CreatePart(PrimitiveType.Capsule, "ChestPlate", new Vector3(0f, 1.10f, 0.08f), new Vector3(0.72f, 0.48f, 0.76f), new Color(0.12f, 0.19f, 0.25f));
            CreatePart(PrimitiveType.Sphere, "ShoulderL", new Vector3(-0.42f, 1.18f, 0f), new Vector3(0.28f, 0.22f, 0.34f), new Color(0.70f, 0.54f, 0.25f));
            CreatePart(PrimitiveType.Sphere, "ShoulderR", new Vector3(0.42f, 1.18f, 0f), new Vector3(0.28f, 0.22f, 0.34f), new Color(0.70f, 0.54f, 0.25f));
            CreatePart(PrimitiveType.Capsule, "LegL", new Vector3(-0.22f, 0.38f, 0.02f), new Vector3(0.24f, 0.50f, 0.24f), new Color(0.16f, 0.21f, 0.25f));
            CreatePart(PrimitiveType.Capsule, "LegR", new Vector3(0.22f, 0.38f, 0.02f), new Vector3(0.24f, 0.50f, 0.24f), new Color(0.16f, 0.21f, 0.25f));

            CreatePart(PrimitiveType.Sphere, "Head", new Vector3(0f, 1.92f, 0f), new Vector3(0.46f, 0.46f, 0.46f), new Color(0.73f, 0.47f, 0.29f));
            CreatePart(PrimitiveType.Cylinder, "Helmet", new Vector3(0f, 2.18f, 0f), new Vector3(0.50f, 0.18f, 0.50f), new Color(0.73f, 0.55f, 0.22f));
            CreatePart(PrimitiveType.Cube, "HelmetRim", new Vector3(0f, 2.08f, 0.02f), new Vector3(0.68f, 0.09f, 0.58f), new Color(0.18f, 0.23f, 0.27f));
            CreatePart(PrimitiveType.Capsule, "Plume", new Vector3(0f, 2.50f, -0.02f), new Vector3(0.20f, 0.38f, 0.20f), new Color(0.52f, 0.12f, 0.09f));

            Transform forearmL = CreatePart(PrimitiveType.Capsule, "ForearmL", new Vector3(-0.46f, 0.92f, 0.27f), new Vector3(0.18f, 0.40f, 0.18f), new Color(0.68f, 0.48f, 0.22f));
            forearmL.localRotation = Quaternion.Euler(18f, 0f, 22f);
            Transform forearmR = CreatePart(PrimitiveType.Capsule, "ForearmR", new Vector3(0.46f, 0.92f, 0.27f), new Vector3(0.18f, 0.40f, 0.18f), new Color(0.68f, 0.48f, 0.22f));
            forearmR.localRotation = Quaternion.Euler(18f, 0f, -22f);

            Transform weapon = CreatePart(PrimitiveType.Cube, "Weapon", new Vector3(0.38f, 1.08f, 0.48f), new Vector3(0.16f, 0.14f, 0.92f), new Color(0.08f, 0.10f, 0.12f));
            weapon.localRotation = Quaternion.Euler(18f, 0f, 8f);
            Transform weaponTop = CreatePart(PrimitiveType.Cube, "WeaponTop", new Vector3(0.38f, 1.18f, 0.56f), new Vector3(0.10f, 0.08f, 0.52f), new Color(0.70f, 0.50f, 0.20f));
            weaponTop.localRotation = Quaternion.Euler(18f, 0f, 8f);
        }

        private Transform CreatePart(PrimitiveType primitive, string partName, Vector3 localPosition, Vector3 localScale, Color color)
        {
            GameObject part = GameObject.CreatePrimitive(primitive);
            part.name = partName;
            part.transform.SetParent(visualRoot, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;

            Collider collider = part.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = RuntimeMaterialFactory.Create(partName + "Material", color);

            return part.transform;
        }
    }
}