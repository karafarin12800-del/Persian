using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public sealed class Projectile : MonoBehaviour
    {
        [SerializeField] private float speed = 48f;
        [SerializeField] private float lifetime = 2.4f;
        [SerializeField] private int damage = 30;

        private Vector3 direction;
        private float remainingLife;

        public void SetDefaults(float projectileSpeed, float projectileLifetime, int projectileDamage)
        {
            speed = projectileSpeed;
            lifetime = projectileLifetime;
            damage = projectileDamage;
        }

        public void Launch(Vector3 worldDirection)
        {
            direction = worldDirection.normalized;
            remainingLife = lifetime;

            if (direction.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        }

        private void Update()
        {
            transform.position += direction * speed * Time.deltaTime;
            remainingLife -= Time.deltaTime;

            if (remainingLife <= 0f)
                Destroy(gameObject);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other == null) return;
            if (other.GetComponentInParent<PlayerController>() != null) return;
            if (other.GetComponentInParent<Projectile>() != null) return;
            if (other.GetComponentInParent<EnemyProjectile>() != null) return;

            TargetHealth target = other.GetComponentInParent<TargetHealth>();
            if (target != null && target.GetComponentInParent<EnemyChase>() != null)
            {
                target.ApplyDamage(damage);
                Destroy(gameObject);
                return;
            }

            Destroy(gameObject);
        }
    }
}
