using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public sealed class WorldBounds : MonoBehaviour
    {
        [SerializeField] private Vector2 size = new Vector2(220.8f, 220.8f);
        [SerializeField] private float margin = 4f;
        [SerializeField] private Transform target;

        private void Awake()
        {
            if (size.x > 400f || size.y > 400f)
                size = new Vector2(220.8f, 220.8f);

            if (target == null)
            {
                PlayerController player = FindFirstObjectByType<PlayerController>();
                if (player != null)
                    target = player.transform;
            }
        }

        private void LateUpdate()
        {
            if (target == null)
                return;

            Vector3 p = target.position;
            float halfX = Mathf.Max(1f, size.x * 0.5f - margin);
            float halfZ = Mathf.Max(1f, size.y * 0.5f - margin);

            p.x = Mathf.Clamp(p.x, -halfX, halfX);
            p.z = Mathf.Clamp(p.z, -halfZ, halfZ);
            target.position = p;
        }
    }
}