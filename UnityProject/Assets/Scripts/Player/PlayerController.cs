using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 6f;
        [SerializeField] private float worldLimit = 88f;

        private Vector3 input;
        private Transform visualRoot;
        private Transform body;

        private void Awake()
        {
            EnsurePlayerVisual();
        }

        public void SetMoveInput(Vector2 value)
        {
            input = new Vector3(value.x, 0f, value.y);
            if (input.sqrMagnitude > 1f)
                input.Normalize();
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
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 1f - Mathf.Exp(-18f * Time.deltaTime));
                if (body != null)
                {
                    float bob = Mathf.Sin(Time.time * 14f) * 0.035f;
                    body.localPosition = new Vector3(0f, 0.78f + bob, 0f);
                }
            }
        }

        private void EnsurePlayerVisual()
        {
            if (visualRoot != null || transform.Find("PlayerVisual") != null)
                return;

            visualRoot = new GameObject("PlayerVisual").transform;
            visualRoot.SetParent(transform, false);

            body = CreatePart(PrimitiveType.Capsule, "Body", new Vector3(0f, 0.78f, 0f), new Vector3(0.62f, 0.78f, 0.62f), new Color(0.58f, 0.34f, 0.12f));
            CreatePart(PrimitiveType.Sphere, "Head", new Vector3(0f, 1.82f, 0f), new Vector3(0.43f, 0.43f, 0.43f), new Color(0.78f, 0.55f, 0.34f));
            CreatePart(PrimitiveType.Cylinder, "Crown", new Vector3(0f, 2.18f, 0f), new Vector3(0.42f, 0.16f, 0.42f), new Color(0.85f, 0.62f, 0.12f));
            CreatePart(PrimitiveType.Cube, "ShoulderArmor", new Vector3(0f, 1.15f, 0f), new Vector3(0.95f, 0.18f, 0.55f), new Color(0.12f, 0.18f, 0.22f));
            CreatePart(PrimitiveType.Cylinder, "WeaponGrip", new Vector3(0.48f, 0.92f, 0.28f), new Vector3(0.07f, 0.42f, 0.07f), new Color(0.18f, 0.18f, 0.18f)).transform.localRotation = Quaternion.Euler(25f, 0f, -35f);
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
            {
                Shader shader = Shader.Find("Standard");
                if (shader != null)
                {
                    Material material = new Material(shader);
                    material.color = color;
                    material.enableInstancing = true;
                    renderer.sharedMaterial = material;
                }
            }
            return part.transform;
        }
    }
}
