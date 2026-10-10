using System.Collections;
using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public sealed class EnemySpawner : MonoBehaviour
    {
        [SerializeField] private Transform player;
        [SerializeField] private int startingCount = 24;
        [SerializeField] private float spawnRadius = 44f;
        [SerializeField] private float nextWaveDelay = 3f;
        [SerializeField] private float initialSpawnDelay = 6.5f;
        [SerializeField] private float enemyCheckInterval = 0.5f;
        [SerializeField] private int maxPerWave = 24;
        [SerializeField] private int victoryWave = 5;

#if UNITY_ANDROID
        // Spawn 24 enemies in the initial Android match, spreading construction across frames.
        private const bool AndroidEnemyDiagnosticDisabled = false;
#endif

        private int wave = 1;
        private bool spawning;
        private float nextEnemyCheckTime;
        private int failedSpawnAttempts;
        private bool initialWavePending;
        private bool initialWaveComplete;
        private const int MaxFailedSpawnAttempts = 2;

        public int CurrentWave => wave;

        public void Configure(Transform playerTransform, int enemyCount, float radius, float unusedSpeed)
        {
            player = playerTransform;
#if UNITY_ANDROID
            // Enforce a smaller mobile wave cap even when older scene data requested 42+ enemies.
            maxPerWave = Mathf.Clamp(maxPerWave, 1, 24);
#endif
            startingCount = Mathf.Clamp(enemyCount, 1, maxPerWave);
            spawnRadius = Mathf.Max(16f, radius);
        }

        private void Start()
        {
#if UNITY_ANDROID
            if (AndroidEnemyDiagnosticDisabled)
            {
                Debug.Log("PERSIA_DIAGNOSTIC: Android enemy spawning DISABLED for crash isolation.");
                enabled = false;
                return;
            }
#endif

            if (player == null)
            {
                PlayerController found = FindFirstObjectByType<PlayerController>();
                if (found != null) player = found.transform;
            }

            if (player != null)
                StartCoroutine(SpawnInitialWaveAfterStartup());
        }

        private IEnumerator SpawnInitialWaveAfterStartup()
        {
#if UNITY_ANDROID
            // Spawn the full lobby as soon as the match is activated, not several seconds later.
            yield return new WaitForSecondsRealtime(0.15f);
#else
            yield return new WaitForSecondsRealtime(initialSpawnDelay);
#endif
            initialWavePending = false;

            if (player != null && player.GetComponent<PlayerController>()?.IsDefeated != true)
            {
                StartupCheckpoint.Set("AndroidEnemyWaveStart");
                SpawnWave();
                initialWaveComplete = true;
                StartupCheckpoint.Set("AndroidEnemyWaveReady");
            }
        }

        private void Update()
        {
            if (GameSession.Instance != null && GameSession.Instance.IsFinished)
                return;

            if (player == null || spawning || initialWavePending || !initialWaveComplete || Time.unscaledTime < nextEnemyCheckTime) return;
            nextEnemyCheckTime = Time.unscaledTime + enemyCheckInterval;

            EnemyChase[] enemies = FindObjectsByType<EnemyChase>(FindObjectsSortMode.None);
            if (enemies.Length == 0)
            {
                if (wave >= Mathf.Max(1, victoryWave))
                {
                    PlayerController activePlayer = player.GetComponent<PlayerController>();
                    if (activePlayer != null && !activePlayer.IsDefeated)
                        ExtractionBeacon.ActivateForVictory(activePlayer);
                    else
                        GameSession.Instance?.EndMission(false);

                    // Do not declare victory here. ExtractionBeacon completes the
                    // mission only after the player reaches the visible blue beam.
                    enabled = false;
                    return;
                }

                if (failedSpawnAttempts >= MaxFailedSpawnAttempts)
                    return;

                spawning = true;
                Invoke(nameof(SpawnNextWave), nextWaveDelay);
            }
        }

        private void SpawnNextWave()
        {
            if (player == null || player.GetComponent<PlayerController>()?.IsDefeated == true)
            {
                spawning = false;
                return;
            }

            wave++;
            SpawnWave();
        }

        private void SpawnWave()
        {
            int count = Mathf.Min(startingCount + wave - 1, maxPerWave);
#if UNITY_ANDROID
            count = Mathf.Min(count, 24);
#endif

#if UNITY_ANDROID
            spawning = true;
            StartCoroutine(SpawnAndroidWaveGradually(count, wave));
            return;
#else
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
                Debug.LogWarning($"PERSIA_COMBAT: enemy spawn attempt failed ({failedSpawnAttempts}/{MaxFailedSpawnAttempts}).");
                return;
            }

            failedSpawnAttempts = 0;
            if (GameSession.Instance != null)
                GameSession.Instance.SetWave(wave);

            SpawnWaveReward();
#endif
        }

#if UNITY_ANDROID
        private IEnumerator SpawnAndroidWaveGradually(int count, int currentWave)
        {
            int spawned = 0;
            int attemptsPerEnemy = 60;

            // Select positions throughout the playable city instead of clustering
            // everyone in a ring around the player. Spawn one valid enemy per frame
            // to avoid a single-frame allocation spike on Android.
            while (spawned < count)
            {
                bool foundPosition = false;
                Vector3 position = Vector3.zero;

                for (int attempt = 0; attempt < attemptsPerEnemy; attempt++)
                {
                    Vector3 candidate = new Vector3(
                        Random.Range(-82f, 82f),
                        1f,
                        Random.Range(-82f, 82f));

                    Vector2 candidateXZ = new Vector2(candidate.x, candidate.z);
                    Vector2 playerXZ = player != null
                        ? new Vector2(player.position.x, player.position.z)
                        : Vector2.zero;

                    if (Vector2.Distance(candidateXZ, playerXZ) < 18f)
                        continue;

                    if (Physics.CheckSphere(candidate + Vector3.up * 0.7f, 0.85f, ~0,
                        QueryTriggerInteraction.Ignore))
                        continue;

                    position = candidate;
                    foundPosition = true;
                    break;
                }

                if (!foundPosition)
                {
                    // Deterministic fallback ring prevents a crowded city tile from
                    // silently reducing the requested 32-player starting lobby.
                    float angle = (spawned + 0.5f) / Mathf.Max(1, count) * Mathf.PI * 2f;
                    float distance = 70f;
                    position = new Vector3(
                        Mathf.Clamp((player != null ? player.position.x : 0f) + Mathf.Cos(angle) * distance, -84f, 84f),
                        1f,
                        Mathf.Clamp((player != null ? player.position.z : 0f) + Mathf.Sin(angle) * distance, -84f, 84f));
                }

                StartupCheckpoint.Set("AndroidEnemySpawnAttempt");
                SpawnEnemy(position, spawned, currentWave);
                spawned++;
                StartupCheckpoint.Set("AndroidEnemySpawnComplete");
                yield return null;
            }

            failedSpawnAttempts = 0;
            if (GameSession.Instance != null)
                GameSession.Instance.SetWave(currentWave);

            SpawnWaveReward();
            spawning = false;
        }
#endif

        private void SpawnEnemy(Vector3 position, int index, int currentWave)
        {
            StartupCheckpoint.Set("AndroidEnemyObjectConstructionStarted");
            int archetype = index % 7 == 0 ? 3 : (index % 3 == 0 ? 2 : 1);
#if UNITY_ANDROID
            GameObject enemy = new GameObject($"Enemy_W{currentWave}_{index}");
            enemy.transform.position = position;
            enemy.transform.localScale = new Vector3(0.9f, 0.9f, 0.9f);

            CapsuleCollider capsule = enemy.AddComponent<CapsuleCollider>();
            capsule.radius = 0.55f;
            capsule.height = 1.8f;
            capsule.center = new Vector3(0f, 0.9f, 0f);

            TargetHealth health = enemy.AddComponent<TargetHealth>();
            health.SetMaxHealth(archetype == 3 ? 160 : (archetype == 2 ? 120 : 100));
            enemy.AddComponent<EnemyHealthBar>();

            EnemyChase chase = enemy.AddComponent<EnemyChase>();
            chase.Configure(player, archetype);
#else
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
            enemy.AddComponent<EnemyHealthBar>();

            EnemyChase chase = enemy.AddComponent<EnemyChase>();
            chase.Configure(player, archetype);
#endif
            StartupCheckpoint.Set("AndroidEnemyObjectConstructionComplete");
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
#if UNITY_ANDROID
            Color color = type == PickupItem.PickupType.Ammo
                ? new Color(0.95f, 0.72f, 0.12f)
                : (type == PickupItem.PickupType.Medkit
                    ? new Color(0.14f, 0.75f, 0.28f)
                    : new Color(0.55f, 0.28f, 0.78f));

            GameObject pickup = AndroidSafeRuntimeFactory.CreateMarker(
                $"Pickup_{type}", position, Vector3.one * 0.46f, color, true);

            PickupItem item = pickup.AddComponent<PickupItem>();
            item.Configure(type, amount);
#else
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
#endif
        }
    }
}