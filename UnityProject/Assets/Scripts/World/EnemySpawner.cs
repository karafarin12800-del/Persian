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
        [SerializeField] private int victoryWave = 4;

#if UNITY_ANDROID
        // Spawn 24 enemies in the initial Android match, spreading construction across frames.
        private const bool AndroidEnemyDiagnosticDisabled = false;
#endif

        private int wave = 1;
        private bool stageTwoArena;
        private bool spawning;
        private float nextEnemyCheckTime;
        private int failedSpawnAttempts;
        private bool initialWavePending;
        private bool initialWaveComplete;
        private bool initialWaveStarted;
        private Coroutine initialWaveCoroutine;
        private const int MaxFailedSpawnAttempts = 2;

        public int CurrentWave => wave;
        public bool IsStageTwoArena => stageTwoArena;
        public int EnemiesSpawnedForArena { get; private set; }

        public void ConfigureStageTwoArena(Transform playerTransform)
        {
            stageTwoArena = true;
            player = playerTransform;
            startingCount = 100;
            maxPerWave = 20;
            victoryWave = 5;
            wave = 1;
            EnemiesSpawnedForArena = 0;
            failedSpawnAttempts = 0;
            spawning = false;
            if (initialWaveCoroutine != null)
            {
                StopCoroutine(initialWaveCoroutine);
                initialWaveCoroutine = null;
            }
            initialWaveStarted = false;
            initialWavePending = false;
            initialWaveComplete = false;
            nextEnemyCheckTime = 0f;

            // Stage 2 can reuse the existing EnemySpawner after Stage 1. Start() does
            // not run a second time on a reused component, so explicitly arm the first
            // green-beam wave when the component is already active.
            if (isActiveAndEnabled)
                TryStartInitialWave();
        }

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

        private void OnEnable()
        {
            // ConfigureStageTwoArena may have been called while this component was
            // disabled by the match-start safety gate. Re-enable must start the wave.
            if (stageTwoArena && player != null)
                TryStartInitialWave();
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

            TryStartInitialWave();
        }

        private void TryStartInitialWave()
        {
            if (!isActiveAndEnabled || player == null || initialWaveStarted)
                return;

            initialWaveStarted = true;
            initialWavePending = true;
            initialWaveCoroutine = StartCoroutine(SpawnInitialWaveAfterStartup());
        }

        private IEnumerator SpawnInitialWaveAfterStartup()
        {
#if UNITY_ANDROID
            // The first green landing beams appear promptly; each alien then descends
            // through its own two-second animation in AnimateAlienLanding().
            yield return new WaitForSecondsRealtime(stageTwoArena ? 0.30f : 0.15f);
#else
            yield return new WaitForSecondsRealtime(initialSpawnDelay);
#endif
            initialWaveCoroutine = null;
            initialWavePending = false;

            if (player != null)
            {
                StartupCheckpoint.Set(stageTwoArena ? "StageTwoGreenBeamWaveStart" : "AndroidEnemyWaveStart");
                Debug.Log(stageTwoArena
                    ? "PERSIA_STAGE2: starting the initial green-beam alien wave."
                    : "PERSIA_COMBAT: starting the initial enemy wave.");
                SpawnWave();
                initialWaveComplete = true;
                StartupCheckpoint.Set(stageTwoArena ? "StageTwoGreenBeamWaveReady" : "AndroidEnemyWaveReady");
            }
            else
            {
                initialWaveStarted = false;
                Debug.LogWarning("PERSIA_COMBAT: initial wave cannot start because the player target is missing.");
            }
        }

        private void Update()
        {
            if (GameSession.Instance != null && GameSession.Instance.IsFinished)
                return;

            if (player == null || spawning || initialWavePending || !initialWaveComplete || Time.unscaledTime < nextEnemyCheckTime) return;
            nextEnemyCheckTime = Time.unscaledTime + enemyCheckInterval;

            EnemyChase[] enemies = FindObjectsByType<EnemyChase>(FindObjectsSortMode.None);
            if (stageTwoArena)
            {
                if (wave >= 5 && enemies.Length == 0)
                {
                    PlayerController activePlayer = player != null ? player.GetComponent<PlayerController>() : null;
                    StageTwoArenaManager.Instance?.FinishArena();
                    GameSession.Instance?.EndMission(activePlayer != null && !activePlayer.IsDefeated);
                    enabled = false;
                    return;
                }

                // Five 20-alien waves; begin the next drop as soon as six or fewer
                // aliens remain on the ground. Stage one retains its original logic.
                if (wave < 5 && enemies.Length <= 6 && !spawning)
                {
                    spawning = true;
                    Invoke(nameof(SpawnNextWave), 1f);
                }
                return;
            }

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
            // Four sequential combat waves: six enemies per wave when the match is configured for 24.
            // The next wave is not spawned until Update confirms every enemy in this wave is gone.
            int count = stageTwoArena
                ? Mathf.Clamp(Mathf.CeilToInt(startingCount / 5f), 1, maxPerWave)
                : Mathf.Clamp(Mathf.CeilToInt(startingCount / 4f), 1, maxPerWave);
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
                float distance = Random.Range(Mathf.Max(45f, spawnRadius * 0.72f), Mathf.Max(50f, spawnRadius));
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
            if (stageTwoArena) spawning = false;
#endif
        }

#if UNITY_ANDROID
        private IEnumerator SpawnAndroidWaveGradually(int count, int currentWave)
        {
            int spawned = 0;
            int attemptsPerEnemy = 60;
            // Only this wave is spawned here; the next starts after every enemy dies.

            // Select positions throughout the playable city instead of clustering
            // everyone in a ring around the player. Spawn one valid enemy per frame,
            // in four clearly separated groups of six, to smooth both visuals and CPU load.
            while (spawned < count)
            {
                bool foundPosition = false;
                Vector3 position = Vector3.zero;

                for (int attempt = 0; attempt < attemptsPerEnemy; attempt++)
                {
                    // Stage 1 keeps its original map-wide random candidate selection.
                    Vector3 candidate = new Vector3(
                        Random.Range(-82f, 82f),
                        1f,
                        Random.Range(-82f, 82f));

                    // Stage 2 uses a shuffled grid across the whole playable terrain,
                    // not a ring around the player. Every wave lands aliens in
                    // separated parts of the village instead of one small cluster.
                    if (stageTwoArena)
                    {
                        int columns = Mathf.CeilToInt(Mathf.Sqrt(count));
                        int rows = Mathf.CeilToInt(count / (float)Mathf.Max(1, columns));
                        int slot = (spawned + (currentWave - 1) * 7) % Mathf.Max(1, count);
                        int column = slot % columns;
                        int row = slot / columns;
                        float cellWidth = 172f / columns;
                        float cellDepth = 172f / rows;
                        candidate = new Vector3(
                            -86f + (column + Random.Range(0.20f, 0.80f)) * cellWidth,
                            1f,
                            -86f + (row + Random.Range(0.20f, 0.80f)) * cellDepth);
                    }

                    Vector2 candidateXZ = new Vector2(candidate.x, candidate.z);
                    Vector2 playerXZ = player != null
                        ? new Vector2(player.position.x, player.position.z)
                        : Vector2.zero;

                    if (!stageTwoArena)
                    {
                        if (Vector2.Distance(candidateXZ, playerXZ) < 45f)
                            continue;
                    }

                    if (Physics.CheckSphere(candidate + Vector3.up * 0.7f, 0.85f, ~0,
                        QueryTriggerInteraction.Ignore))
                        continue;

                    position = candidate;
                    foundPosition = true;
                    break;
                }

                if (!foundPosition)
                {
                    if (stageTwoArena)
                    {
                        // Last-resort placements remain map-wide too. This path still
                        // creates the alien and its green descent beam if buildings
                        // occupy all 60 attempts inside an individual grid cell.
                        int columns = Mathf.CeilToInt(Mathf.Sqrt(count));
                        int rows = Mathf.CeilToInt(count / (float)Mathf.Max(1, columns));
                        int slot = (spawned + (currentWave - 1) * 7) % Mathf.Max(1, count);
                        int column = slot % columns;
                        int row = slot / columns;
                        position = new Vector3(
                            -86f + (column + 0.5f) * (172f / columns),
                            1f,
                            -86f + (row + 0.5f) * (172f / rows));
                    }
                    else
                    {
                        // Preserve the stage-one fallback unchanged.
                        float fallbackAngle = (spawned + 0.5f) / Mathf.Max(1, count) * Mathf.PI * 2f;
                        float fallbackDistance = 26f;
                        position = new Vector3(
                            Mathf.Clamp((player != null ? player.position.x : 0f) + Mathf.Cos(fallbackAngle) * fallbackDistance, -82f, 82f),
                            1f,
                            Mathf.Clamp((player != null ? player.position.z : 0f) + Mathf.Sin(fallbackAngle) * fallbackDistance, -82f, 82f));
                    }
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

            if (stageTwoArena)
                Debug.Log("PERSIA_STAGE2: spawned " + spawned + " aliens for wave " + currentWave +
                    "; total arena aliens spawned=" + EnemiesSpawnedForArena + ".");

            SpawnWaveReward();
            spawning = false;
        }
#endif

        private void SpawnEnemy(Vector3 position, int index, int currentWave)
        {
            StartupCheckpoint.Set("AndroidEnemyObjectConstructionStarted");
            int archetype = stageTwoArena ? (index % 7 == 0 ? 3 : (index % 3 == 0 ? 2 : 1))
                : (index % 7 == 0 ? 3 : (index % 3 == 0 ? 2 : 1));
            Vector3 groundPosition = position;
            Vector3 spawnPosition = stageTwoArena ? groundPosition + Vector3.up * 13f : groundPosition;
#if UNITY_ANDROID
            GameObject enemy = new GameObject(stageTwoArena ? $"Alien_W{currentWave}_{index}" : $"Enemy_W{currentWave}_{index}");
            enemy.transform.position = spawnPosition;
            enemy.transform.localScale = new Vector3(0.9f, 0.9f, 0.9f);

            CapsuleCollider capsule = enemy.AddComponent<CapsuleCollider>();
            capsule.radius = 0.55f;
            capsule.height = 1.8f;
            capsule.center = new Vector3(0f, 0.9f, 0f);

            if (stageTwoArena)
            {
                GameObject meshSource = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                MeshFilter sourceFilter = meshSource.GetComponent<MeshFilter>();
                MeshFilter alienFilter = enemy.AddComponent<MeshFilter>();
                alienFilter.sharedMesh = sourceFilter.sharedMesh;
                MeshRenderer alienRenderer = enemy.AddComponent<MeshRenderer>();
                Color alienColor = archetype == 3 ? new Color(0.12f, 0.58f, 0.20f)
                    : (archetype == 2 ? new Color(0.18f, 0.78f, 0.25f) : new Color(0.28f, 0.92f, 0.34f));
                alienRenderer.sharedMaterial = RuntimeMaterialFactory.Create("StageTwoAlien_" + archetype, alienColor);
                alienRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                alienRenderer.receiveShadows = false;
                Collider temporaryCollider = meshSource.GetComponent<Collider>();
                if (temporaryCollider != null) Destroy(temporaryCollider);
                Destroy(meshSource);
            }

            TargetHealth health = enemy.AddComponent<TargetHealth>();
            health.SetMaxHealth(archetype == 3 ? 160 : (archetype == 2 ? 120 : 100));
            enemy.AddComponent<EnemyHealthBar>();

            EnemyChase chase = enemy.AddComponent<EnemyChase>();
            if (stageTwoArena) chase.enabled = false;
            chase.Configure(player, archetype);
            if (stageTwoArena) StartCoroutine(AnimateAlienLanding(enemy.transform, chase, groundPosition, archetype));
#else
            GameObject enemy = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            enemy.name = stageTwoArena ? $"Alien_W{currentWave}_{index}" : $"Enemy_W{currentWave}_{index}";
            enemy.transform.position = spawnPosition;
            enemy.transform.localScale = new Vector3(0.9f, 1f, 0.9f);

            Renderer renderer = enemy.GetComponent<Renderer>();
            if (renderer != null)
            {
                Color color = stageTwoArena
                    ? (archetype == 3 ? new Color(0.12f, 0.58f, 0.20f) : (archetype == 2 ? new Color(0.18f, 0.78f, 0.25f) : new Color(0.28f, 0.92f, 0.34f)))
                    : (archetype == 3 ? new Color(0.28f, 0.06f, 0.05f) : (archetype == 2 ? new Color(0.40f, 0.12f, 0.08f) : new Color(0.48f, 0.18f, 0.12f)));
                renderer.sharedMaterial = RuntimeMaterialFactory.Create(enemy.name + "Material", color);
            }

            TargetHealth health = enemy.AddComponent<TargetHealth>();
            health.SetMaxHealth(archetype == 3 ? 160 : (archetype == 2 ? 120 : 100));
            enemy.AddComponent<EnemyHealthBar>();

            EnemyChase chase = enemy.AddComponent<EnemyChase>();
            if (stageTwoArena) chase.enabled = false;
            chase.Configure(player, archetype);
            if (stageTwoArena) StartCoroutine(AnimateAlienLanding(enemy.transform, chase, groundPosition, archetype));
#endif
            if (stageTwoArena)
            {
                CreateAlienEye(enemy.transform, new Vector3(-0.15f, 0.24f, 0.39f), "L_" + currentWave + "_" + index);
                CreateAlienEye(enemy.transform, new Vector3(0.15f, 0.24f, 0.39f), "R_" + currentWave + "_" + index);
                EnemiesSpawnedForArena++;
            }
            StartupCheckpoint.Set("AndroidEnemyObjectConstructionComplete");
        }

        private IEnumerator AnimateAlienLanding(Transform alien, EnemyChase chase, Vector3 groundPosition, int archetype)
        {
            if (alien == null) yield break;
            GameObject beam = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            beam.name = "AlienGreenLandingBeam";
            beam.transform.position = new Vector3(groundPosition.x, 9f, groundPosition.z);
            beam.transform.localScale = new Vector3(0.60f, 9f, 0.60f);
            Collider beamCollider = beam.GetComponent<Collider>();
            if (beamCollider != null) Destroy(beamCollider);
            Renderer beamRenderer = beam.GetComponent<Renderer>();
            if (beamRenderer != null)
            {
                Material beamMaterial = RuntimeMaterialFactory.Create("AlienGreenLandingBeamMaterial", new Color(0.15f, 1f, 0.22f));
                beamRenderer.sharedMaterial = beamMaterial;
                beamRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                beamRenderer.receiveShadows = false;
            }

            // Keep the green column visible long enough to read clearly on mobile:
            // each alien descends from the sky for exactly about two seconds.
            Vector3 start = groundPosition + Vector3.up * 18f;
            float duration = 2.0f;
            float elapsed = 0f;
            while (elapsed < duration && alien != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                alien.position = Vector3.Lerp(start, groundPosition, t * t);
                yield return null;
            }

            if (alien != null)
            {
                alien.position = groundPosition;
                if (chase != null) chase.enabled = true;
                StageTwoAlienTactics tactics = alien.GetComponent<StageTwoAlienTactics>();
                if (tactics == null) tactics = alien.gameObject.AddComponent<StageTwoAlienTactics>();
                tactics.Configure(player, archetype);
            }
            if (beam != null) Destroy(beam, 0.12f);
        }

        private void CreateAlienEye(Transform parent, Vector3 localPosition, string suffix)
        {
            GameObject eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            eye.name = "AlienGreenEye_" + suffix;
            eye.transform.SetParent(parent, false);
            eye.transform.localPosition = localPosition;
            eye.transform.localScale = new Vector3(0.10f, 0.075f, 0.06f);
            Collider eyeCollider = eye.GetComponent<Collider>();
            if (eyeCollider != null) Destroy(eyeCollider);
            Renderer eyeRenderer = eye.GetComponent<Renderer>();
            if (eyeRenderer != null)
            {
                eyeRenderer.sharedMaterial = RuntimeMaterialFactory.Create("AlienEyeGlow", new Color(0.45f, 1f, 0.22f));
                eyeRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                eyeRenderer.receiveShadows = false;
            }
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