using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public sealed class WorldFoundation : MonoBehaviour
    {
        [SerializeField] private Vector2 worldSize = new Vector2(1056f, 1056f);
        [SerializeField] private float playableHeight = 0f;

        public Vector2 WorldSize => worldSize;

        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = Matrix4x4.TRS(new Vector3(worldSize.x * 0.5f, playableHeight, worldSize.y * 0.5f), Quaternion.identity, Vector3.one);
            Gizmos.DrawWireCube(Vector3.zero, new Vector3(worldSize.x, 0.05f, worldSize.y));
        }
    }
}
