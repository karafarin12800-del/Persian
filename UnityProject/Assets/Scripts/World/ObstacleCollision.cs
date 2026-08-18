using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    [RequireComponent(typeof(Collider))]
    public sealed class ObstacleCollision : MonoBehaviour
    {
        [SerializeField] private bool blockProjectiles = true;

        private void Awake()
        {
            Collider col = GetComponent<Collider>();
            col.isTrigger = false;

            if (blockProjectiles)
                gameObject.layer = LayerMask.NameToLayer("Default");
        }
    }
}
