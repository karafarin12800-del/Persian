using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public sealed class EnemySpawner : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private int count = 12;
        [SerializeField] private float radius = 70f;
        [SerializeField] private float speed = 1.6f;

        public void Configure(Transform playerTransform, int enemyCount, float spawnRadius, float enemySpeed)
        {
            player = playerTransform;
            count = enemyCount;
            radius = spawnRadius;
            speed = enemySpeed;
        }

        private void Start()
        {
            if (player == null)
            {
                PlayerController found = FindFirstObjectByType<PlayerController>();
                if (found != null) player = found.transform;
            }

            if (player == null) return;

            for (int i = 0; i < count; i++)
            {
                float angle = (i / Mathf.Max(1f, count)) * Mathf.PI * 2f + Random.Range(-0.25f, 0.25f);
                float distance = Random.Range(radius * 0.55f, radius);
                Vector3 spawnPosition = player.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;

                GameObject enemy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                enemy.name = "Enemy_" + i;
                enemy.tag = "Enemy";
                enemy.transform.position = new Vector3(spawnPosition.x, 1f, spawnPosition.z);
                enemy.transform.localScale = new Vector3(0.9f, 1f, 0.9f);

                Renderer renderer = enemy.GetComponent<Renderer>();
                if (renderer != null)
                    renderer.sharedMaterial = RuntimeMaterialFactory.Create("EnemyMaterial_" + i, new Color(0.42f, 0.10f, 0.08f));

                enemy.AddComponent<TargetHealth>();
                EnemyChase chase = enemy.AddComponent<EnemyChase>();
                chase.Configure(player, speed);
            }
        }
    }

    public sealed class EnemyChase : MonoBehaviour
    {
        [SerializeField] private int contactDamage = 8;
        [SerializeField] private float attackDistance = 1.35f;
        [SerializeField] private float attackCooldown = 0.8f;

        private Transform target;
        private float speed;
        private float nextAttackTime;

        public void Configure(Transform targetTransform, float moveSpeed)
        {
            target = targetTransform;
            speed = moveSpeed;
        }

        private void Update()
        {
            if (target == null) return;

            Vector3 direction = target.position - transform.position;
            direction.y = 0f;
            float distance = direction.magnitude;
            if (distance <= 0.01f) return;

            Vector3 normalized = direction / distance;
            transform.rotation = Quaternion.LookRotation(normalized, Vector3.up);

            if (distance > attackDistance)
            {
                transform.position += normalized * speed * Time.deltaTime;
                return;
            }

            if (Time.time >= nextAttackTime)
            {
                TargetHealth health = target.GetComponentInParent<TargetHealth>();
                if (health != null)
                    health.ApplyDamage(contactDamage);
                nextAttackTime = Time.time + attackCooldown;
            }
        }
    }
}
