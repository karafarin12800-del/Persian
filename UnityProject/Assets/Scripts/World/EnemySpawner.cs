using System.Collections;
using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public sealed class EnemySpawner : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private int startingCount = 8;
        [SerializeField] private float spawnRadius = 44f;
        [SerializeField] private float nextWaveDelay = 3f;
        [SerializeField] private float initialSpawnDelay = 4f;
        [SerializeField] private float enemyCheckInterval = 0.5f;
        [SerializeField] private int maxPerWave = 15;

        private int wave = 1;
        private bool spawning;
        private float nextEnemyCheckTime;
        private int failedSpawnAttempts;
        private const int MaxFailedSpawnAttempts = 2;

        public int CurrentWave => wave;

        public void Configure(Transform playerTransform, int enemyCount, float radius, float unusedSpeed)
        {
            player = playerTransform;
            startingCount = Mathf.Clamp(enemyCount, 1, maxPerWave);
            spawnRadius = Mathf.Max(16f, radius);
        }

        private void Start()
        {
            if (player == null)
            {
                PlayerController found = FindFirstObjectByType<PlayerController>();
                if (found != null) player = found.transform;
            }

            // Enemy construction is deliberately delayed until the gameplay scene has
            // had time to render and finish its procedural world startup on Android.
            if (player != null)
                StartCoroutine(SpawnInitialWaveAfterStartup());
        }

        private IEnumerator SpawnInitialWaveAfterStartup()
        {
            yield return new WaitForSecondsRealtime(initialSpawnDelay);
            if (player != null && !player.GetComponent<PlayerController>()?.IsDefeated == true)
                SpawnWave();
        }

        private void Update()
        {
            if (player == null || spawning || Time.unscaledTime < nextEnemyCheckTime) return;
            nextEnemyCheckTime = Time.unscaledTime + enemyCheckInterval;

            EnemyChase[] enemies = FindObjectsByType<EnemyChase>(FindObjectsSortMode.None);
            if (enemies.Length == 0 && failedSpawnAttempts < MaxFailedSpawnAttempts)
            {
                spawning = true;
                Invoke(nameof(SpawnNextWave), nextWaveDelay);
            }
        }

        private void SpawnNextWave()
        {
            spawning = false;
            if (player == null || player.GetComponent<PlayerController>()?.IsDefeated == true) return;
            wave++;
            SpawnWave();
        }

        private void SpawnWave()
        {
            int count = Mathf.Min(startingCount + wave - 1, maxPerWave);
            int spawned = 0;

            for (int i = 0; i < count * 3 && spawned < count; i++)
            {
                float angle = Random.Range(0f, Mathf.PI * 2f);
                float distance = Random.Range(spawnRadius * 0.72f, spawnRadius);
                Vector3 position = player.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * distance;
                position.y = 1f;

                if (Physics.CheckSphere(position + Vector3.up * 0.7f, 0.85f, ~0, QueryTriggerInteraction.Ignore)) continue;
                SpawnEnemy(position, spawned, wave);
                spawned++;
            }

            if (spawned == 0)
            {
                failedSpawnAttempts++;
                Debug.LogWarning($"PERSIA_COMBAT: enemy spawn attempt failed ({failedSpawnAttempts}/{MaxFailedSpawnAttempts}); retrying is bounded to avoid an infinite pickup/wave loop.");
                return;
            }

            failedSpawnAttempts = 0;
            if (GameSession.Instance != null)
                GameSession.Instance.SetWave(wave);

            SpawnWaveReward();
        }

        private void SpawnEnemy(Vector3 position, int index, int currentWave)
        {
            int archetype = index % 7 == 0 ? 3 : (index % 3 == 0 ? 2 : 1);
            GameObject enemy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            enemy.name = $"Enemy_W{currentWave}_{index}";
            enemy.transform.position = position;
            enemy.transform.localScale = new Vector3(0.9f, 1f, 0.9f);

            Renderer renderer = enemy.GetComponent<Renderer>();
            if (renderer != null)
            {
                Color color = archetype == 3 ? new Color(0.28f, 0.06f, 0.05f) : (archetype == 2 ? new Color(0.40f, 0.12f, 0.08f) : new Color(0.48f, 0.18f, 0.12f));
                renderer.sharedMaterial = RuntimeMaterialFactory.Create(enemy.name + "Material", color);
            }

            TargetHealth health = enemy.AddComponent<TargetHealth>();
            health.SetMaxHealth(archetype == 3 ? 160 : (archetype == 2 ? 120 : 100));

            EnemyChase chase = enemy.AddComponent<EnemyChase>();
            chase.Configure(player, archetype);
        }

        private void SpawnWaveReward()
        {
            if (player == null) return;
            Vector3[] points =
            {
                player.position + new Vector3(7f, 0.5f, -5f),
                player.position + new Vector3(-7f, 0.5f, 5f),
                player.position + new Vector3(4f, 0.5f, 7f)
            };

            SpawnPickup(points[0], PickupItem.PickupType.Ammo, 30);
            SpawnPickup(points[1], PickupItem.PickupType.Medkit, 35);
            SpawnPickup(points[2], PickupItem.PickupType.Grenade, 1);
        }

        private void SpawnPickup(Vector3 position, PickupItem.PickupType type, int amount)
        {
            GameObject pickup = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            pickup.name = $"Pickup_{type}";
            pickup.transform.position = position;
            pickup.transform.localScale = Vector3.one * 0.65f;

            Collider collider = pickup.GetComponent<Collider>();
            if (collider != null)
            {
                collider.isTrigger = true;
                collider.enabled = true;
            }

            Renderer renderer = pickup.GetComponent<Renderer>();
            if (renderer != null)
            {
                Color color = type == PickupItem.PickupType.Ammo ? new Color(0.95f, 0.72f, 0.12f) : (type == PickupItem.PickupType.Medkit ? new Color(0.14f, 0.75f, 0.28f) : new Color(0.55f, 0.28f, 0.78f));
                renderer.sharedMaterial = RuntimeMaterialFactory.Create(pickup.name + "Material", color);
            }

            PickupItem item = pickup.AddComponent<PickupItem>();
            item.Configure(type, amount);
        }
    }
}