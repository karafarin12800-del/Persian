using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public sealed class WorldBounds : MonoBehaviour
    {
        [SerializeField] private Vector2 size = new Vector2(1056f, 1056f);
        [SerializeField] private Transform target;

        private void LateUpdate()
        {
            if (target == null) return;
            Vector3 p = target.position;
            float halfX = size.x * 0.5f;
            float halfZ = size.y * 0.5f;
            p.x = Mathf.Clamp(p.x, -halfX, halfX);
            p.z = Mathf.Clamp(p.z, -halfZ, halfZ);
            target.position = p;
        }
    }
}
