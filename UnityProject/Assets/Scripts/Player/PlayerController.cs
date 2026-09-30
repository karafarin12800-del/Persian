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

            // PRESENTATION ONLY: built-in Unity primitives assembled as a polished
            // mobile-friendly Persian warrior. Gameplay, movement and combat are untouched.
            body = CreatePart(PrimitiveType.Capsule, "Body", new Vector3(0f, 0.92f, 0f),
                new Vector3(0.62f, 0.82f, 0.62f), new Color(0.20f, 0.24f, 0.28f));

            CreatePart(PrimitiveType.Capsule, "Tunic", new Vector3(0f, 1.02f, 0.05f),
                new Vector3(0.72f, 0.58f, 0.70f), new Color(0.42f, 0.15f, 0.08f));

            CreatePart(PrimitiveType.Cube, "ChestArmor", new Vector3(0f, 1.25f, 0.17f),
                new Vector3(0.92f, 0.56f, 0.18f), new Color(0.72f, 0.52f, 0.20f));

            CreatePart(PrimitiveType.Cube, "Belt", new Vector3(0f, 0.78f, 0.04f),
                new Vector3(0.82f, 0.14f, 0.72f), new Color(0.78f, 0.62f, 0.25f));

            CreatePart(PrimitiveType.Capsule, "LegL", new Vector3(-0.22f, 0.38f, 0.02f),
                new Vector3(0.24f, 0.48f, 0.24f), new Color(0.14f, 0.18f, 0.22f));
            CreatePart(PrimitiveType.Capsule, "LegR", new Vector3(0.22f, 0.38f, 0.02f),
                new Vector3(0.24f, 0.48f, 0.24f), new Color(0.14f, 0.18f, 0.22f));

            CreatePart(PrimitiveType.Sphere, "Head", new Vector3(0f, 1.93f, 0f),
                new Vector3(0.45f, 0.45f, 0.45f), new Color(0.64f, 0.40f, 0.24f));

            CreatePart(PrimitiveType.Cylinder, "PersianHelmet", new Vector3(0f, 2.18f, 0f),
                new Vector3(0.54f, 0.22f, 0.54f), new Color(0.74f, 0.55f, 0.20f));
            CreatePart(PrimitiveType.Cube, "HelmetCrest", new Vector3(0f, 2.08f, 0.03f),
                new Vector3(0.76f, 0.10f, 0.62f), new Color(0.16f, 0.20f, 0.23f));
            CreatePart(PrimitiveType.Capsule, "RedPlume", new Vector3(0f, 2.48f, -0.04f),
                new Vector3(0.18f, 0.42f, 0.18f), new Color(0.52f, 0.08f, 0.05f));

            Transform armL = CreatePart(PrimitiveType.Capsule, "ArmL", new Vector3(-0.50f, 1.02f, 0.08f),
                new Vector3(0.18f, 0.48f, 0.18f), new Color(0.58f, 0.42f, 0.22f));
            armL.localRotation = Quaternion.Euler(15f, 0f, 25f);
            Transform armR = CreatePart(PrimitiveType.Capsule, "ArmR", new Vector3(0.50f, 1.02f, 0.18f),
                new Vector3(0.18f, 0.48f, 0.18f), new Color(0.58f, 0.42f, 0.22f));
            armR.localRotation = Quaternion.Euler(15f, 0f, -20f);

            // Shield: a layered circular Persian-style shield.
            Transform shield = CreatePart(PrimitiveType.Cylinder, "Shield", new Vector3(-0.58f, 1.05f, 0.20f),
                new Vector3(0.42f, 0.08f, 0.42f), new Color(0.10f, 0.18f, 0.22f));
            shield.localRotation = Quaternion.Euler(90f, 0f, 8f);
            Transform shieldBoss = CreatePart(PrimitiveType.Sphere, "ShieldBoss", new Vector3(-0.61f, 1.05f, 0.27f),
                new Vector3(0.11f, 0.11f, 0.11f), new Color(0.88f, 0.68f, 0.25f));

            // Spear/rifle silhouette retained purely as presentation geometry.
            Transform weapon = CreatePart(PrimitiveType.Cylinder, "Weapon", new Vector3(0.48f, 1.15f, 0.55f),
                new Vector3(0.075f, 0.72f, 0.075f), new Color(0.08f, 0.10f, 0.12f));
            weapon.localRotation = Quaternion.Euler(18f, 0f, 8f);
            Transform weaponTip = CreatePart(PrimitiveType.Cylinder, "WeaponTip", new Vector3(0.62f, 1.42f, 0.80f),
                new Vector3(0.12f, 0.22f, 0.12f), new Color(0.76f, 0.62f, 0.28f));
            weaponTip.localRotation = Quaternion.Euler(18f, 0f, 8f);

            // Small cape for a stronger silhouette from the 2.5D camera.
            Transform cape = CreatePart(PrimitiveType.Cube, "Cape", new Vector3(0f, 1.12f, -0.34f),
                new Vector3(0.92f, 1.05f, 0.08f), new Color(0.30f, 0.07f, 0.06f));
            cape.localRotation = Quaternion.Euler(5f, 0f, 0f);
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
// CI validation marker: visual-only build validation 2026-09-30
