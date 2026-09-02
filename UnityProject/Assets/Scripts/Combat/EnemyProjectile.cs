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
            PlayerController player = other.GetComponentInParent<PlayerController>();
            if (player == null) return;
            player.ReceiveDamage(damage);
            Destroy(gameObject);
        }
    }
}