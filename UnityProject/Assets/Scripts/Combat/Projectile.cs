using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public sealed class Projectile : MonoBehaviour
    {
        [SerializeField] private float speed = 18f;
        [SerializeField] private float lifetime = 2f;
        [SerializeField] private int damage = 20;

        private Vector3 direction;

        public void Launch(Vector3 worldDirection)
        {
            direction = worldDirection.normalized;
            Destroy(gameObject, lifetime);
        }

        private void Update()
        {
            transform.position += direction * speed * Time.deltaTime;
        }

        private void OnTriggerEnter(Collider other)
        {
            TargetHealth target = other.GetComponentInParent<TargetHealth>();
            if (target != null)
            {
                target.ApplyDamage(damage);
                Destroy(gameObject);
            }
        }
    }
}
