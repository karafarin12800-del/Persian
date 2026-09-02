using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public sealed class Projectile : MonoBehaviour
    {
        [SerializeField] private float speed = 45f;
        [SerializeField] private float lifetime = 2.2f;
        [SerializeField] private int damage = 30;

        private Vector3 direction;

        public void SetDefaults(float projectileSpeed, float projectileLifetime, int projectileDamage)
        {
            speed = projectileSpeed;
            lifetime = projectileLifetime;
            damage = projectileDamage;
        }

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
            if (other.GetComponentInParent<PlayerController>() != null) return;

            TargetHealth target = other.GetComponentInParent<TargetHealth>();
            if (target != null && target.CompareTag("Enemy"))
            {
                target.ApplyDamage(damage);
                Destroy(gameObject);
            }
        }
    }
}