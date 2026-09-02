using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public sealed class EnemyProjectile : MonoBehaviour
    {
        [SerializeField] private float speed = 24f;
        [SerializeField] private float lifetime = 2f;
        [SerializeField] private int damage = 8;

        private Vector3 direction;

        public void Configure(Vector3 launchDirection, int damageAmount)
        {
            direction = launchDirection.normalized;
            damage = Mathf.Max(1, damageAmount);
        }

        private void Update()
        {
            transform.position += direction * speed * Time.deltaTime;
            lifetime -= Time.deltaTime;
            if (lifetime <= 0f) Destroy(gameObject);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (other == null) return;

            PlayerController player = other.GetComponentInParent<PlayerController>();
            if (player != null)
            {
                player.ReceiveDamage(damage);
                Destroy(gameObject);
                return;
            }

            if (other.GetComponentInParent<EnemyChase>() != null)
                return;

            if (other.GetComponentInParent<Projectile>() != null)
                return;

            // Enemy rounds stop on buildings, trees and other world geometry.
            Destroy(gameObject);
        }
    }
}
