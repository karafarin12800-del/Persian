using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private Camera gameplayCamera;
        [SerializeField] private float worldSize = 192f;
        [SerializeField] private int seed = 32025;
        [SerializeField] private int enemyCount = 8;
        [SerializeField] private float enemySpawnRadius = 44f;

        private Transform worldRoot;
        private Material groundMaterial;
        private Material roadMaterial;
        private Material buildingMaterial;
        private Material roofMaterial;
        private Material accentMaterial;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
            Random.InitState(seed);

            EnsureGameSession();
            GameSession.Instance?.ResetMatch();

            if (gameplayCamera == null) gameplayCamera = Camera.main;
            ConfigureCamera();
            ConfigureLighting();

            PlayerController player = FindFirstObjectByType<PlayerController>();
            if (player != null)
            {
                player.transform.position = new Vector3(0f, 0f, -4f);
                CameraFollow25D follow = gameplayCamera != null ? gameplayCamera.GetComponent<CameraFollow25D>() : null;
                if (follow != null) follow.SetTarget(player.transform);
                EnsureEnemySpawner(player.transform);
                EnsureGameplayHUD(player);
            }

            BuildWorld();
        }

        private void EnsureGameSession()
        {
            if (GameSession.Instance != null) return;
            GameObject sessionObject = new GameObject("GameSession");
            sessionObject.AddComponent<GameSession>();
        }

        private void EnsureEnemySpawner(Transform player)
        {
            EnemySpawner spawner = FindFirstObjectByType<EnemySpawner>();
            if (spawner == null)
            {
                GameObject objectRoot = new GameObject("EnemySpawner");
                spawner = objectRoot.AddComponent<EnemySpawner>();
            }
            spawner.Configure(player, enemyCount, enemySpawnRadius, 0f);
        }

        private void EnsureGameplayHUD(PlayerController player)
        {
            RuntimeCombatHUD existing = FindFirstObjectByType<RuntimeCombatHUD>();
            if (existing != null) return;

            GameObject hudObject = new GameObject("RuntimeCombatHUD");
            hudObject.AddComponent<RuntimeCombatHUD>();
        }

        private void ConfigureCamera()
        {
            if (gameplayCamera == null) return;
            gameplayCamera.orthographic = false;
            gameplayCamera.fieldOfView = 52f;
            gameplayCamera.nearClipPlane = 0.1f;
            gameplayCamera.farClipPlane = 240f;
            gameplayCamera.allowHDR = false;
            gameplayCamera.allowMSAA = true;
            gameplayCamera.transform.position = new Vector3(0f, 14f, -14f);
            gameplayCamera.transform.rotation = Quaternion.Euler(48f, 0f, 0f);

            CameraFollow25D follow = gameplayCamera.GetComponent<CameraFollow25D>();
            if (follow == null) follow = gameplayCamera.gameObject.AddComponent<CameraFollow25D>();
            follow.SetTarget(FindFirstObjectByType<PlayerController>()?.transform);
        }

        private void ConfigureLighting()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.24f, 0.27f, 0.30f);
            RenderSettings.fog = true;
            RenderSettings.fogColor = new Color(0.20f, 0.23f, 0.25f);
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 75f;
            RenderSettings.fogEndDistance = 240f;

            Light sun = FindFirstObjectByType<Light>();
            if (sun == null)
            {
                GameObject lightObject = new GameObject("Sun");
                sun = lightObject.AddComponent<Light>();
                sun.type = LightType.Directional;
            }
            sun.type = LightType.Directional;
            sun.intensity = 1.15f;
            sun.color = new Color(1f, 0.93f, 0.82f);
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.78f;
            sun.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
        }

        private void BuildWorld()
        {
            GameObject legacyGround = GameObject.Find("Ground");
            if (legacyGround != null)
                legacyGround.SetActive(false);

            if (worldRoot != null) Destroy(worldRoot.gameObject);
            worldRoot = new GameObject("BattleRoyaleCity").transform;

            groundMaterial = MakeMaterial("Ground", new Color(0.33f, 0.68f, 0.18f));
            roadMaterial = MakeMaterial("Road", new Color(0.18f, 0.20f, 0.22f));
            buildingMaterial = MakeMaterial("Building", new Color(0.88f, 0.76f, 0.30f));
            roofMaterial = MakeMaterial("Roof", new Color(0.24f, 0.28f, 0.34f));
            accentMaterial = MakeMaterial("Accent", new Color(1.00f, 0.46f, 0.12f));

            CreateBox("Ground", new Vector3(0f, -0.35f, 0f), new Vector3(worldSize, 0.5f, worldSize), groundMaterial, true);

            const float roadWidth = 8f;
            for (float x = -worldSize * 0.5f + roadWidth * 0.5f; x <= worldSize * 0.5f; x += 24f)
                CreateBox("RoadX", new Vector3(x, -0.05f, 0f), new Vector3(roadWidth, 0.18f, worldSize), roadMaterial, false);
            for (float z = -worldSize * 0.5f + roadWidth * 0.5f; z <= worldSize * 0.5f; z += 24f)
                CreateBox("RoadZ", new Vector3(0f, -0.04f, z), new Vector3(worldSize, 0.18f, roadWidth), roadMaterial, false);

            BuildRoadMarkings(roadWidth);
            BuildCityBlocks(roadWidth);
            BuildLandmarks();
            BuildStreetProps();
            BuildRuinedQuarter();
        }

        private void BuildRoadMarkings(float roadWidth)
        {
            Material marking = MakeMaterial("RoadMarking", new Color(0.95f, 0.88f, 0.45f));
            float half = worldSize * 0.5f;
            for (float x = -half + roadWidth * 0.5f; x <= half; x += 24f)
            {
                for (float z = -half + 3f; z < half; z += 8f)
                    CreateBox("RoadMark", new Vector3(x, 0.08f, z), new Vector3(0.32f, 0.04f, 3.0f), marking, false);
            }
            for (float z = -half + roadWidth * 0.5f; z <= half; z += 24f)
            {
                for (float x = -half + 3f; x < half; x += 8f)
                    CreateBox("RoadMark", new Vector3(x, 0.081f, z), new Vector3(3.0f, 0.04f, 0.32f), marking, false);
            }
        }

        private void BuildCityBlocks(float roadWidth)
        {
            float half = worldSize * 0.5f - 5f;
            for (float x = -half; x <= half; x += 24f)
            {
                for (float z = -half; z <= half; z += 24f)
                {
                    if (Vector2.Distance(new Vector2(x, z), new Vector2(0f, -4f)) < 16f) continue;
                    if (Random.value < 0.12f) continue;

                    int count = Random.Range(1, 4);
                    for (int i = 0; i < count; i++)
                    {
                        float px = x + Random.Range(-6.5f, 6.5f);
                        float pz = z + Random.Range(-6.5f, 6.5f);
                        float sx = Random.Range(5.5f, 9.5f);
                        float sz = Random.Range(5.5f, 9.5f);
                        float h = Random.Range(2.8f, 6.5f);
                        CreateBuilding(new Vector3(px, 0f, pz), new Vector3(sx, h, sz));
                    }
                }
            }
        }

        private void CreateBuilding(Vector3 position, Vector3 size)
        {
            GameObject building = CreateBox("Building", position + Vector3.up * (size.y * 0.5f), size, buildingMaterial, true);
            GameObject roof = CreateBox("Roof", position + Vector3.up * (size.y + 0.18f), new Vector3(size.x + 0.25f, 0.35f, size.z + 0.25f), roofMaterial, true);
            roof.transform.SetParent(building.transform.parent, true);

            if (Random.value > 0.35f)
                CreateBox("Door", position + new Vector3(0f, 0.9f, -size.z * 0.51f), new Vector3(1.0f, 1.8f, 0.12f), accentMaterial, false);

            if (Random.value > 0.45f)
            {
                for (int side = -1; side <= 1; side += 2)
                    CreateBox("Window", position + new Vector3(side * size.x * 0.28f, size.y * 0.58f, -size.z * 0.51f), new Vector3(1.25f, 0.85f, 0.08f), accentMaterial, false);
            }
        }

        private void BuildLandmarks()
        {
            CreateBuilding(new Vector3(30f, 0f, 30f), new Vector3(15f, 7f, 11f));
            CreateBuilding(new Vector3(-34f, 0f, 30f), new Vector3(12f, 5f, 15f));
            CreateBuilding(new Vector3(42f, 0f, -35f), new Vector3(18f, 4f, 10f));
            CreateBuilding(new Vector3(-42f, 0f, -35f), new Vector3(10f, 8f, 17f));
        }

        private void BuildStreetProps()
        {
            for (int i = 0; i < 22; i++)
            {
                float x = Random.Range(-84f, 84f);
                float z = Random.Range(-84f, 84f);
                if (Mathf.Abs(Mathf.Repeat(x + 4f, 24f) - 12f) < 4f || Mathf.Abs(Mathf.Repeat(z + 4f, 24f) - 12f) < 4f) continue;
                CreateTree(new Vector3(x, 0f, z));
            }

            for (int i = 0; i < 10; i++)
            {
                float x = Random.Range(-84f, 84f);
                float z = Random.Range(-84f, 84f);
                CreateVehicle(new Vector3(x, 0.18f, z), Random.value > 0.55f);
            }
        }

        private void BuildRuinedQuarter()
        {
            Vector3 center = new Vector3(-62f, 0f, 62f);
            for (int i = 0; i < 9; i++)
            {
                float x = center.x + Random.Range(-15f, 15f);
                float z = center.z + Random.Range(-15f, 15f);
                float h = Random.Range(1.2f, 4.5f);
                CreateBox("RuinedBlock", new Vector3(x, h * 0.5f, z), new Vector3(Random.Range(3f, 7f), h, Random.Range(3f, 7f)), buildingMaterial, true);
            }
            for (int i = 0; i < 14; i++)
            {
                float x = center.x + Random.Range(-18f, 18f);
                float z = center.z + Random.Range(-18f, 18f);
                CreateBox("Debris", new Vector3(x, 0.25f, z), new Vector3(Random.Range(0.5f, 2.2f), Random.Range(0.3f, 0.8f), Random.Range(0.5f, 2.2f)), roofMaterial, false).transform.Rotate(0f, Random.Range(0f, 180f), Random.Range(-15f, 15f));
            }
        }

        private void CreateTree(Vector3 position)
        {
            Material trunkMaterial = MakeMaterial("Trunk", new Color(0.34f, 0.20f, 0.10f));
            Material foliageMaterial = MakeMaterial("Foliage", new Color(0.10f, 0.42f, 0.12f));
            GameObject trunk = CreateBox("TreeTrunk", position + Vector3.up * 0.9f, new Vector3(0.42f, 1.8f, 0.42f), trunkMaterial, true);

            GameObject crown = CreateBox("TreeCrown", position + Vector3.up * 2.4f, new Vector3(2.2f, 1.55f, 2.2f), foliageMaterial, false);
            crown.transform.Rotate(0f, Random.Range(0f, 45f), 0f);
            GameObject crownTop = CreateBox("TreeCrownTop", position + Vector3.up * 3.15f, new Vector3(1.45f, 0.85f, 1.45f), foliageMaterial, false);
            crownTop.transform.Rotate(0f, 45f, 0f);

            trunk.transform.SetParent(worldRoot, true);
        }

        private void CreateVehicle(Vector3 position, bool tanker)
        {
            GameObject car = CreateBox("EmptyVehicle", position, tanker ? new Vector3(2.3f, 1.0f, 5.5f) : new Vector3(2.0f, 0.8f, 3.8f), tanker ? roofMaterial : accentMaterial, true);
            car.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 180f), 0f);
            if (tanker)
                CreatePart(PrimitiveType.Cylinder, "Tank", position + Vector3.up * 0.75f, new Vector3(1.0f, 2.0f, 1.0f), roofMaterial.color, true).transform.rotation = Quaternion.Euler(90f, 0f, 0f);
        }

        private GameObject CreateBox(string objectName, Vector3 position, Vector3 scale, Material material, bool collider)
        {
            GameObject obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = objectName;
            obj.transform.SetParent(worldRoot, true);
            obj.transform.position = position;
            obj.transform.localScale = scale;
            Renderer renderer = obj.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = material;
            if (!collider)
            {
                Collider c = obj.GetComponent<Collider>();
                if (c != null) Destroy(c);
            }
            return obj;
        }

        private GameObject CreatePart(PrimitiveType primitive, string objectName, Vector3 position, Vector3 scale, Color color, bool collider)
        {
            GameObject obj = GameObject.CreatePrimitive(primitive);
            obj.name = objectName;
            obj.transform.position = position;
            obj.transform.localScale = scale;
            Renderer renderer = obj.GetComponent<Renderer>();
            if (renderer != null) renderer.sharedMaterial = MakeMaterial(objectName + "Material", color);
            if (!collider)
            {
                Collider c = obj.GetComponent<Collider>();
                if (c != null) Destroy(c);
            }
            return obj;
        }

        private Material MakeMaterial(string name, Color color)
        {
            return RuntimeMaterialFactory.Create(name, color);
        }
    }
}