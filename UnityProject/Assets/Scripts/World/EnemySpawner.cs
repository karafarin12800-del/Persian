using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public sealed class EnemySpawner : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private int startingCount = 8;
        [SerializeField] private float spawnRadius = 44f;
        [SerializeField] private float nextWaveDelay = 1.2f;
        [SerializeField] private int maxPerWave = 15;

        private int wave = 1;
        private float nextWaveTime;
        private bool spawning;

        public int CurrentWave => wave;

        public void Configure(Transform playerTransform, int enemyCount, float radius, float unusedSpeed)
        {
            player = playerTransform;
            startingCount = Mathf.Clamp(enemyCount, 1, maxPerWave);
            spawnRadius = Mathf.Max(12f, radius);
        }

        private void Start()
        {
            if (player == null)
            {
                PlayerController found = FindFirstObjectByType<PlayerController>();
                if (found != null) player = found.transform;
            }

            if (player != null) SpawnWave();
        }

        private void Update()
        {
            if (player == null || spawning) return;

            EnemyChase[] enemies = FindObjectsByType<EnemyChase>(FindObjectsSortMode.None);
            if (enemies.Length == 0 && Time.time >= nextWaveTime)
            {
                nextWaveTime = Time.time + nextWaveDelay;
                spawning = true;
                Invoke(nameof(SpawnNextWave), nextWaveDelay);
            }
        }

        private void SpawnNextWave()
        {
            spawning = false;
            wave++;
            SpawnWave();
        }

        private void SpawnWave()
        {
            if (player == null) return;

            int count = Mathf.Min(startingCount + wave - 1, maxPerWave);
            for (int i = 0; i < count; i++)
            {
                float angle = (i / Mathf.Max(1f, count)) * Mathf.PI * 2f + Random.Range(-0.25f, 0.25f);
                float distance = Random.Range(spawnRadius * 0.72f, spawnRadius);
                Vector3 offset = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
                Vector3 spawnPosition = player.position + offset;
                spawnPosition.y = 0f;

                if (Physics.CheckSphere(spawnPosition + Vector3.up * 0.7f, 1f, ~0, QueryTriggerInteraction.Ignore))
                    continue;

                GameObject enemy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                enemy.name = $"Enemy_W{wave}_{i}";
                enemy.tag = "Enemy";
                enemy.transform.position = spawnPosition + Vector3.up * 1f;
                enemy.transform.localScale = new Vector3(0.9f, 1f, 0.9f);

                Renderer renderer = enemy.GetComponent<Renderer>();
                if (renderer != null)
                {
                    int archetype = i % 7 == 0 ? 3 : (i % 3 == 0 ? 2 : 1);
                    Color color = archetype == 3 ? new Color(0.28f, 0.06f, 0.05f) : (archetype == 2 ? new Color(0.40f, 0.12f, 0.08f) : new Color(0.48f, 0.18f, 0.12f));
                    renderer.sharedMaterial = RuntimeMaterialFactory.Create(enemy.name + "Material", color);
                }

                TargetHealth health = enemy.AddComponent<TargetHealth>();
                EnemyChase chase = enemy.AddComponent<EnemyChase>();
                int type = i % 7 == 0 ? 3 : (i % 3 == 0 ? 2 : 1);
                health.SetMaxHealth(type == 3 ? 160 : (type == 2 ? 120 : 100));
                chase.Configure(player, type);
            }
        }
    }

    public sealed class EnemyChase : MonoBehaviour
    {
        private Transform target;
        private float speed;
        private int damage;
        private float attackDistance;
        private float attackCooldown;
        private float nextAttackTime;

        public void Configure(Transform targetTransform, int archetype)
        {
            target = targetTransform;
            speed = archetype == 3 ? 3.5f : (archetype == 2 ? 3.0f : 2.5f);
            damage = archetype == 3 ? 12 : (archetype == 2 ? 8 : 6);
            attackDistance = archetype == 3 ? 2.7f : 2.35f;
            attackCooldown = archetype == 3 ? 1.15f : (archetype == 2 ? 1.4f : 1.8f);
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
                Vector3 next = transform.position + normalized * speed * Time.deltaTime;
                if (!Physics.CheckSphere(next + Vector3.up * 0.7f, 0.55f, ~0, QueryTriggerInteraction.Ignore) || Physics.CheckSphere(next + Vector3.up * 0.7f, 0.55f, LayerMask.GetMask("Player"), QueryTriggerInteraction.Ignore))
                    transform.position = next;
                return;
            }

            if (Time.time >= nextAttackTime)
            {
                PlayerController player = target.GetComponentInParent<PlayerController>();
                if (player != null) player.ReceiveDamage(damage);
                nextAttackTime = Time.time + attackCooldown;
            }
        }
    }
}