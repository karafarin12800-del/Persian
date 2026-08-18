using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public sealed class PlayerController : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 6f;

        private Vector3 input;

        private void Awake()
        {
            EnsurePrototypeVisual();
        }

        public void SetMoveInput(Vector2 value)
        {
            input = new Vector3(value.x, 0f, value.y);
            if (input.sqrMagnitude > 1f)
                input.Normalize();
        }

        private void Update()
        {
            if (input.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(input, Vector3.up);
            transform.position += input * moveSpeed * Time.deltaTime;
        }

        private void EnsurePrototypeVisual()
        {
            if (transform.Find("PrototypeVisual") != null)
                return;

            GameObject visual = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            visual.name = "PrototypeVisual";
            visual.transform.SetParent(transform, false);
            visual.transform.localPosition = new Vector3(0f, 1f, 0f);
            visual.transform.localScale = new Vector3(0.7f, 1f, 0.7f);

            Collider collider = visual.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            Renderer renderer = visual.GetComponent<Renderer>();
            if (renderer != null)
            {
                Shader shader = Shader.Find("Standard");
                if (shader != null)
                {
                    Material material = new Material(shader)
                    {
                        name = "PrototypePlayerMaterial"
                    };
                    material.color = new Color(0.70f, 0.55f, 0.20f, 1f);
                    renderer.sharedMaterial = material;
                }
            }
        }
    }
}
