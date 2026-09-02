using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 6f;
        [SerializeField] private float worldLimit = 88f;
        [SerializeField] private float turnSpeed = 18f;

        private Vector3 input;
        private Transform visualRoot;
        private Transform body;
        private WeaponController weapon;
        private NearestTargetAim aim;

        public NearestTargetAim Aim => aim;
        public WeaponController Weapon => weapon;

        private void Awake()
        {
            EnsurePlayerVisual();
            if (!CompareTag("Player")) gameObject.tag = "Player";
            EnsureGameplayComponents();
        }

        public void SetMoveInput(Vector2 value)
        {
            input = new Vector3(value.x, 0f, value.y);
            if (input.sqrMagnitude > 1f) input.Normalize();
        }

        private void Update()
        {
            Vector3 next = transform.position + input * moveSpeed * Time.deltaTime;
            next.x = Mathf.Clamp(next.x, -worldLimit, worldLimit);
            next.z = Mathf.Clamp(next.z, -worldLimit, worldLimit);
            next.y = 0f;
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

        private void EnsureGameplayComponents()
        {
            if (GetComponent<TargetHealth>() == null)
                gameObject.AddComponent<TargetHealth>();

            if (GetComponent<PickupReceiver>() == null)
                gameObject.AddComponent<PickupReceiver>();

            weapon = GetComponent<WeaponController>();
            if (weapon == null) weapon = gameObject.AddComponent<WeaponController>();

            aim = GetComponent<NearestTargetAim>();
            if (aim == null) aim = gameObject.AddComponent<NearestTargetAim>();

            Transform muzzle = transform.Find("WeaponMuzzle");
            if (muzzle == null)
            {
                GameObject muzzleObject = new GameObject("WeaponMuzzle");
                muzzle = muzzleObject.transform;
                muzzle.SetParent(transform, false);
                muzzle.localPosition = new Vector3(0f, 1.05f, 0.8f);
            }

            Projectile projectileTemplate = CreateProjectileTemplate();
            weapon.Configure(projectileTemplate, muzzle);
        }

        private Projectile CreateProjectileTemplate()
        {
            GameObject projectileObject = new GameObject("RuntimeProjectileTemplate");
            projectileObject.SetActive(false);
            projectileObject.transform.position = transform.position;

            SphereCollider collider = projectileObject.AddComponent<SphereCollider>();
            collider.isTrigger = true;
            collider.radius = 0.14f;

            Rigidbody rigidbody = projectileObject.AddComponent<Rigidbody>();
            rigidbody.isKinematic = true;
            rigidbody.useGravity = false;
            rigidbody.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            Projectile projectile = projectileObject.AddComponent<Projectile>();
            return projectile;
        }

        private void EnsurePlayerVisual()
        {
            if (visualRoot != null || transform.Find("PlayerVisual") != null) return;

            visualRoot = new GameObject("PlayerVisual").transform;
            visualRoot.SetParent(transform, false);

            body = CreatePart(PrimitiveType.Capsule, "Body", new Vector3(0f, 0.78f, 0f), new Vector3(0.62f, 0.78f, 0.62f), new Color(0.58f, 0.34f, 0.12f));
            CreatePart(PrimitiveType.Sphere, "Head", new Vector3(0f, 1.82f, 0f), new Vector3(0.43f, 0.43f, 0.43f), new Color(0.78f, 0.55f, 0.34f));
            CreatePart(PrimitiveType.Cylinder, "Crown", new Vector3(0f, 2.18f, 0f), new Vector3(0.42f, 0.16f, 0.42f), new Color(0.85f, 0.62f, 0.12f));
            CreatePart(PrimitiveType.Cube, "ShoulderArmor", new Vector3(0f, 1.15f, 0f), new Vector3(0.95f, 0.18f, 0.55f), new Color(0.12f, 0.18f, 0.22f));
            CreatePart(PrimitiveType.Cylinder, "WeaponGrip", new Vector3(0.48f, 0.92f, 0.28f), new Vector3(0.07f, 0.42f, 0.07f), new Color(0.18f, 0.18f, 0.18f)).localRotation = Quaternion.Euler(25f, 0f, -35f);
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
            if (renderer != null)
                renderer.sharedMaterial = RuntimeMaterialFactory.Create(partName + "Material", color);

            return part.transform;
        }
    }
}
