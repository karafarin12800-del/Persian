using System.Collections;
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

        public bool IsWorldReady { get; private set; }
        public bool IsWorldPreparing { get; private set; }
        public bool WorldBuildFailed { get; private set; }
        public string WorldBuildError { get; private set; } = string.Empty;
        public string CurrentStage { get; private set; } = "Stage 0: waiting for battlefield preparation...";

        private Transform worldRoot;
        private Material roadMaterial;
        private Material buildingMaterial;
        private Material roofMaterial;
        private Material accentMaterial;
        private bool prepareRequested;

        private void ReportStage(string message, string checkpoint)
        {
            CurrentStage = message;
            StartupCheckpoint.Set(checkpoint);
            Debug.Log("[PERSIA STARTUP] " + message);
        }

        private void OnEnable() => StartupCheckpoint.Set("GameBootstrapOnEnable");

        private void Awake()
        {
            IsWorldReady = false;
            StartupCheckpoint.Set("GameBootstrapAwakeEntered");
            StartupCheckpoint.Set("GameBootstrapAwake");
        }

        private void Start() => StartupCheckpoint.Set("GameBootstrapStartEntered");

        public void PrepareWorld()
        {
            if (prepareRequested || IsWorldReady || IsWorldPreparing) return;
            prepareRequested = true;
            StartCoroutine(BuildWorldAfterStartup());
        }

        private IEnumerator BuildWorldAfterStartup()
        {
            IsWorldPreparing = true;
            IsWorldReady = false;
            WorldBuildFailed = false;
            WorldBuildError = string.Empty;
            yield return null;
            ReportStage("Stage 1: battlefield preparation started.", "WorldBuildStarted");

            System.Exception startupException = null;
            try
            {
                Application.targetFrameRate = 60;
                QualitySettings.vSyncCount = 0;
                Random.InitState(seed);
                EnsureGameSession();
                GameSession.Instance?.ResetMatch();
                ReportStage("Stage 2: creating game session...", "GameSessionReady");
                ConfigureCameraSafe();
                ConfigureLighting();
                ReportStage("Stage 3: building Android-safe battlefield base...", "BeforeWorldBaseBuilt");
                BuildWorldBase();
                ReportStage("Stage 4: battlefield base completed.", "WorldBaseBuilt");
            }
            catch (System.Exception ex)
            {
                startupException = ex;
            }

            if (startupException != null)
            {
                MarkWorldBuildFailed(startupException);
                yield break;
            }

#if UNITY_ANDROID
            yield return null;
            IsWorldReady = true;
            IsWorldPreparing = false;
            ReportStage("Stage 5: Android-safe battlefield ready.", "WorldBuildReady");
            yield break;
#else
            yield return null;
            ReportStage("Stage 5: painting road markings...", "BeforeRoadMarkings");
            if (!RunWorldBuildStep(() => BuildRoadMarkings(8f), "RoadMarkingsBuilt")) yield break;
            yield return null;
            ReportStage("Stage 6: creating city buildings and blocks...", "BeforeCityBlocks");
            if (!RunWorldBuildStep(() => BuildCityBlocks(8f), "CityBlocksBuilt")) yield break;
            yield return null;
            ReportStage("Stage 7: creating landmarks...", "BeforeLandmarks");
            if (!RunWorldBuildStep(BuildLandmarks, "LandmarksBuilt")) yield break;
            yield return null;
            ReportStage("Stage 8: placing trees and vehicles...", "BeforeStreetProps");
            if (!RunWorldBuildStep(BuildStreetProps, "StreetPropsBuilt")) yield break;
            yield return null;
            ReportStage("Stage 9: creating ruined quarter and debris...", "BeforeRuinedQuarter");
            if (!RunWorldBuildStep(BuildRuinedQuarter, "WorldBuildComplete")) yield break;
            IsWorldReady = true;
            IsWorldPreparing = false;
            ReportStage("Stage 10: battlefield completely ready.", "WorldBuildReady");
#endif
        }

        private bool RunWorldBuildStep(System.Action buildStep, string checkpoint)
        {
            try
            {
                buildStep();
                StartupCheckpoint.Set(checkpoint);
                return true;
            }
            catch (System.Exception ex)
            {
                MarkWorldBuildFailed(ex);
                return false;
            }
        }

        private void MarkWorldBuildFailed(System.Exception ex)
        {
            WorldBuildFailed = true;
            WorldBuildError = ex.Message;
            CurrentStage = "FAILED: " + ex.Message;
            IsWorldReady = false;
            IsWorldPreparing = false;
            StartupCheckpoint.Set("WorldBuildFailed");
            Debug.LogException(ex);
        }

        private void EnsureGameSession()
        {
            if (GameSession.Instance != null) return;
            GameObject sessionObject = new GameObject("GameSession");
            sessionObject.AddComponent<GameSession>();
        }

        private void ConfigureCameraSafe()
        {
            if (gameplayCamera == null)
            {
                StartupCheckpoint.Set("CameraMissingSafePath");
                return;
            }
            StartupCheckpoint.Set("CameraSafePathEntered");
        }

        private void ConfigureLighting()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.24f, 0.27f, 0.30f);
            RenderSettings.fog = false;
#if UNITY_ANDROID
            QualitySettings.antiAliasing = 0;
#endif
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
#if UNITY_ANDROID
            sun.shadows = LightShadows.None;
#else
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.78f;
#endif
            sun.transform.rotation = Quaternion.Euler(48f, -32f, 0f);
        }

        private void BuildWorldBase()
        {
            GameObject legacyGround = GameObject.Find("Ground");
            if (legacyGround != null) legacyGround.SetActive(false);

            if (worldRoot != null) Destroy(worldRoot.gameObject);
            worldRoot = new GameObject("BattleRoyaleCity").transform;

            roadMaterial = MakeMaterial("Road", new Color(0.18f, 0.20f, 0.22f));

#if UNITY_ANDROID
            // Do not invoke Terrain3DBuilder during the first Android match. The previous
            // crash happened in this critical startup window, so use one tiny static mesh
            // with a single material as the isolation-safe battlefield floor.
            Material groundMaterial = MakeMaterial("AndroidGround", new Color(0.33f, 0.52f, 0.20f));
            buildingMaterial = MakeMaterial("AndroidBuilding", new Color(0.50f, 0.38f, 0.25f));
            roofMaterial = MakeMaterial("AndroidRoof", new Color(0.16f, 0.19f, 0.23f));
            accentMaterial = MakeMaterial("AndroidAccent", new Color(0.82f, 0.62f, 0.22f));
            CreateFlatMesh("AndroidGround", Vector3.zero, new Vector2(worldSize, worldSize), groundMaterial);
            BuildAndroidRoadGrid();
            BuildAndroidCityPresentation();
            return;
#else
            buildingMaterial = MakeMaterial("Building", new Color(0.88f, 0.76f, 0.30f));
            roofMaterial = MakeMaterial("Roof", new Color(0.24f, 0.28f, 0.34f));
            accentMaterial = MakeMaterial("Accent", new Color(1.00f, 0.46f, 0.12f));

            GameObject terrainObject = new GameObject("Terrain3D");
            terrainObject.transform.SetParent(worldRoot, true);
            Terrain3DBuilder terrain = terrainObject.AddComponent<Terrain3DBuilder>();
            terrain.Build();

            const float roadWidth = 8f;
            for (float x = -worldSize * 0.5f + roadWidth * 0.5f; x <= worldSize * 0.5f; x += 24f)
                CreateBox("RoadX", new Vector3(x, -0.05f, 0f), new Vector3(roadWidth, 0.18f, worldSize), roadMaterial, false);
            for (float z = -worldSize * 0.5f + roadWidth * 0.5f; z <= worldSize * 0.5f; z += 24f)
                CreateBox("RoadZ", new Vector3(0f, -0.04f, z), new Vector3(worldSize, 0.18f, roadWidth), roadMaterial, false);
#endif
        }

        private void BuildAndroidRoadGrid()
        {
            const float roadWidth = 8f;
            float half = worldSize * 0.5f;
            for (float x = -half + roadWidth * 0.5f; x <= half; x += 24f)
                CreateFlatMesh("RoadX", new Vector3(x, -0.02f, 0f), new Vector2(roadWidth, worldSize), roadMaterial);
            for (float z = -half + roadWidth * 0.5f; z <= half; z += 24f)
                CreateFlatMesh("RoadZ", new Vector3(0f, -0.015f, z), new Vector2(worldSize, roadWidth), roadMaterial);
        }

        private void BuildAndroidCityPresentation()
        {
            // Android main-game presentation: keep the real battlefield/combat path,
            // but avoid a large allocation burst when the match starts. The previous
            // 8x8 procedural pass created hundreds of Mesh objects in one frame.
            Vector3[] buildingPoints =
            {
                new Vector3(-60f, 0f, -60f), new Vector3(-36f, 0f, -60f), new Vector3(-12f, 0f, -60f),
                new Vector3(12f, 0f, -60f),  new Vector3(36f, 0f, -60f),  new Vector3(60f, 0f, -60f),
                new Vector3(-60f, 0f, -36f), new Vector3(-36f, 0f, -36f), new Vector3(36f, 0f, -36f), new Vector3(60f, 0f, -36f),
                new Vector3(-60f, 0f, -12f), new Vector3(-36f, 0f, -12f), new Vector3(36f, 0f, -12f), new Vector3(60f, 0f, -12f),
                new Vector3(-60f, 0f, 12f),  new Vector3(-36f, 0f, 12f),  new Vector3(36f, 0f, 12f),  new Vector3(60f, 0f, 12f),
                new Vector3(-60f, 0f, 36f),  new Vector3(-36f, 0f, 36f),  new Vector3(36f, 0f, 36f),  new Vector3(60f, 0f, 36f),
                new Vector3(-60f, 0f, 60f),  new Vector3(-36f, 0f, 60f),  new Vector3(-12f, 0f, 60f),
                new Vector3(12f, 0f, 60f),  new Vector3(36f, 0f, 60f),  new Vector3(60f, 0f, 60f)
            };

            for (int i = 0; i < buildingPoints.Length; i++)
            {
                float footprint = (i % 3 == 0) ? 9f : 7.5f;
                float height = (i % 4 == 0) ? 10f : 7.5f;
                float depth = (i % 2 == 0) ? 8f : 7f;
                CreateAndroidBuilding(buildingPoints[i], footprint, height, depth);
            }

            Vector3[] treePoints =
            {
                new Vector3(-30f, 0f, -30f), new Vector3(30f, 0f, 30f),
                new Vector3(-30f, 0f, 30f),  new Vector3(30f, 0f, -30f)
            };

            for (int i = 0; i < treePoints.Length; i++)
                CreateAndroidTree(treePoints[i], 2.8f + (i % 2) * 0.35f);

            Vector3[] plazaPillars =
            {
                new Vector3(-8f, 0f, 8f), new Vector3(8f, 0f, 8f),
                new Vector3(-8f, 0f, -8f), new Vector3(8f, 0f, -8f)
            };

            for (int i = 0; i < plazaPillars.Length; i++)
                CreateAndroidStreetLamp(plazaPillars[i]);
        }

        private void CreateAndroidBuilding(Vector3 position, float footprint, float height, float depth)
        {
            float bodyHeight = Mathf.Max(4.5f, height);

            CreateAndroidBox(
                "CityBuilding",
                position + Vector3.up * (bodyHeight * 0.5f),
                new Vector3(footprint, bodyHeight, depth),
                buildingMaterial,
                true);

            CreateAndroidBox(
                "CityRoof",
                position + Vector3.up * (bodyHeight + 0.22f),
                new Vector3(footprint + 0.55f, 0.45f, depth + 0.55f),
                roofMaterial,
                false);

            // Three slim facade bands read as windows from the isometric camera.
            for (int row = 0; row < 3; row++)
            {
                float y = 1.6f + row * Mathf.Max(1.6f, (bodyHeight - 2.7f) / 2f);
                for (int col = -1; col <= 1; col++)
                {
                    float x = col * footprint * 0.23f;
                    CreateAndroidBox(
                        "Window",
                        position + new Vector3(x, y, -depth * 0.512f),
                        new Vector3(Mathf.Min(1.35f, footprint * 0.16f), 0.72f, 0.10f),
                        accentMaterial,
                        false);
                }
            }

            // One entrance canopy gives larger blocks a distinctive silhouette.
            CreateAndroidBox(
                "DoorCanopy",
                position + new Vector3(0f, 1.45f, -depth * 0.54f),
                new Vector3(Mathf.Min(2.8f, footprint * 0.28f), 0.22f, 0.75f),
                roofMaterial,
                false);
        }

        private Material androidTreeTrunkMaterial;
        private Material androidTreeCrownMaterial;
        private Material androidLampMaterial;
        private Material androidLampGlowMaterial;

        private void CreateAndroidTree(Vector3 position, float scale)
        {
            if (androidTreeTrunkMaterial == null)
                androidTreeTrunkMaterial = MakeMaterial("AndroidTreeTrunk", new Color(0.25f, 0.16f, 0.09f));
            if (androidTreeCrownMaterial == null)
                androidTreeCrownMaterial = MakeMaterial("AndroidTreeCrown", new Color(0.17f, 0.40f, 0.14f));

            CreateAndroidBox(
                "TreeTrunk",
                position + Vector3.up * (scale * 0.8f),
                new Vector3(scale * 0.22f, scale * 1.6f, scale * 0.22f),
                androidTreeTrunkMaterial,
                false);

            CreateAndroidBox(
                "TreeCrown",
                position + Vector3.up * (scale * 2.0f),
                new Vector3(scale * 1.35f, scale * 1.15f, scale * 1.35f),
                androidTreeCrownMaterial,
                false);
        }

        private void CreateAndroidStreetLamp(Vector3 position)
        {
            if (androidLampMaterial == null)
                androidLampMaterial = MakeMaterial("AndroidLamp", new Color(0.10f, 0.12f, 0.14f));
            if (androidLampGlowMaterial == null)
                androidLampGlowMaterial = MakeMaterial("AndroidLampGlow", new Color(0.95f, 0.78f, 0.30f));

            CreateAndroidBox(
                "LampPost",
                position + Vector3.up * 2.0f,
                new Vector3(0.16f, 4.0f, 0.16f),
                androidLampMaterial,
                false);

            CreateAndroidBox(
                "LampHead",
                position + Vector3.up * 4.0f,
                new Vector3(0.65f, 0.18f, 0.38f),
                androidLampGlowMaterial,
                false);
        }

        private GameObject CreateAndroidBox(string objectName, Vector3 position, Vector3 size, Material material, bool collider)
        {
            GameObject obj = new GameObject(objectName);
            obj.transform.SetParent(worldRoot, true);
            obj.transform.position = position;

            Mesh mesh = new Mesh { name = objectName + "Mesh" };
            float x = size.x * 0.5f;
            float y = size.y * 0.5f;
            float z = size.z * 0.5f;

            mesh.vertices = new[]
            {
                new Vector3(-x, -y, -z), new Vector3(x, -y, -z), new Vector3(x, -y, z), new Vector3(-x, -y, z),
                new Vector3(-x, y, -z), new Vector3(x, y, -z), new Vector3(x, y, z), new Vector3(-x, y, z)
            };

            mesh.triangles = new[]
            {
                0,2,1, 0,3,2,
                4,5,6, 4,6,7,
                0,1,5, 0,5,4,
                1,2,6, 1,6,5,
                2,3,7, 2,7,6,
                3,0,4, 3,4,7
            };
            mesh.RecalculateNormals();

            MeshFilter filter = obj.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;

            MeshRenderer renderer = obj.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;

            if (collider)
            {
                BoxCollider box = obj.AddComponent<BoxCollider>();
                box.center = Vector3.zero;
                box.size = size;
            }

            return obj;
        }

        private GameObject CreateFlatMesh(string objectName, Vector3 position, Vector2 size, Material material)
        {
            GameObject obj = new GameObject(objectName);
            obj.transform.SetParent(worldRoot, true);
            obj.transform.position = position;
            Mesh mesh = new Mesh { name = objectName + "Mesh" };
            float hx = size.x * 0.5f;
            float hz = size.y * 0.5f;
            mesh.vertices = new[]
            {
                new Vector3(-hx, 0f, -hz), new Vector3(hx, 0f, -hz),
                new Vector3(hx, 0f, hz), new Vector3(-hx, 0f, hz)
            };
            mesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(1f, 1f), new Vector2(0f, 1f)
            };
            MeshFilter filter = obj.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = obj.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            return obj;
        }

        private void BuildRoadMarkings(float roadWidth)
        {
            Material marking = MakeMaterial("RoadMarking", new Color(0.95f, 0.88f, 0.45f));
            float half = worldSize * 0.5f;
            for (float x = -half + roadWidth * 0.5f; x <= half; x += 24f)
                for (float z = -half + 3f; z < half; z += 8f)
                    CreateBox("RoadMark", new Vector3(x, 0.08f, z), new Vector3(0.32f, 0.04f, 3f), marking, false);
            for (float z = -half + roadWidth * 0.5f; z <= half; z += 24f)
                for (float x = -half + 3f; x < half; x += 8f)
                    CreateBox("RoadMark", new Vector3(x, 0.081f, z), new Vector3(3f, 0.04f, 0.32f), marking, false);
        }

        private void BuildCityBlocks(float roadWidth)
        {
            float half = worldSize * 0.5f - 5f;
            for (float x = -half; x <= half; x += 24f)
            for (float z = -half; z <= half; z += 24f)
            {
                if (Vector2.Distance(new Vector2(x, z), new Vector2(0f, -4f)) < 16f) continue;
                if (Random.value < 0.12f) continue;
                int count = Random.Range(1, 3);
                for (int i = 0; i < count; i++)
                {
                    float px = x + Random.Range(-6.5f, 6.5f);
                    float pz = z + Random.Range(-6.5f, 6.5f);
                    CreateBuilding(new Vector3(px, 0f, pz), new Vector3(Random.Range(5.5f, 9.5f), Random.Range(2.8f, 6.5f), Random.Range(5.5f, 9.5f)));
                }
            }
        }

        private void CreateBuilding(Vector3 position, Vector3 size)
        {
            GameObject building = CreateBox("Building", position + Vector3.up * (size.y * 0.5f), size, buildingMaterial, true);
            CreateBox("Roof", position + Vector3.up * (size.y + 0.18f), new Vector3(size.x + 0.25f, 0.35f, size.z + 0.25f), roofMaterial, true);
            if (Random.value > 0.35f)
                CreateBox("Door", position + new Vector3(0f, 0.9f, -size.z * 0.51f), new Vector3(1f, 1.8f, 0.12f), accentMaterial, false);
        }

        private void BuildLandmarks() { }
        private void BuildStreetProps() { }
        private void BuildRuinedQuarter() { }

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

        private Material MakeMaterial(string name, Color color)
        {
            return RuntimeMaterialFactory.Create(name, color);
        }
    }
}