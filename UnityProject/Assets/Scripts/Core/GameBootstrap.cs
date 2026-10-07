using System.Collections;
using System.Collections.Generic;
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
        // Reuse identical primitive meshes across the whole Android city. Creating a
        // separate Mesh asset for every window, road dash and facade piece was causing
        // unnecessary native/managed memory pressure during match startup.
        private Mesh androidUnitCubeMesh;
        private Mesh androidUnitQuadMesh;

        // Static city visuals are accumulated into a small number of baked meshes.
        // This keeps the same detail while dramatically reducing GameObjects/Renderers.
        private sealed class AndroidBoxBatch
        {
            public readonly Material Material;
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<Vector3> Normals = new List<Vector3>();
            public readonly List<int> Triangles = new List<int>();

            public AndroidBoxBatch(Material material) => Material = material;
        }

        private readonly Dictionary<Material, AndroidBoxBatch> androidBoxBatches =
            new Dictionary<Material, AndroidBoxBatch>();

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

            roadMaterial = MakeMaterial("Road", new Color(0.105f, 0.12f, 0.135f));

#if UNITY_ANDROID
            // Android uses a lightweight real 3D terrain mesh rather than the old flat
            // green isolation floor. It keeps geometry/collider cost bounded while giving
            // the camera genuine height, slope and depth information.
            // The old CreateFlatMesh battlefield floor is intentionally not used here;
            // this path now owns the actual 3D terrain geometry.
            Material groundMaterial = MakeMaterial("AndroidGround3D", new Color(0.28f, 0.31f, 0.26f));
            buildingMaterial = MakeMaterial("AndroidBuilding", new Color(0.54f, 0.45f, 0.35f));
            roofMaterial = MakeMaterial("AndroidRoof", new Color(0.095f, 0.115f, 0.145f));
            accentMaterial = MakeMaterial("AndroidAccent", new Color(0.86f, 0.66f, 0.22f));
            androidWindowMaterial = MakeMaterial("AndroidWindow", new Color(0.08f, 0.24f, 0.32f));
            androidShadowMaterial = MakeMaterial("AndroidFacadeShadow", new Color(0.24f, 0.19f, 0.16f));
            androidSidewalkMaterial = MakeMaterial("AndroidSidewalk", new Color(0.43f, 0.42f, 0.38f));
            EnsureAndroidCityMaterials();
            BuildAndroidTerrain3D(groundMaterial);
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
        private void BuildAndroidTerrain3D(Material material)
        {
            const int grid = 17;
            float half = worldSize * 0.5f;
            float step = worldSize / (grid - 1);

            GameObject obj = new GameObject("AndroidTerrain3D");
            obj.transform.SetParent(worldRoot, true);

            Mesh mesh = new Mesh { name = "AndroidTerrain3DMesh" };
            Vector3[] vertices = new Vector3[grid * grid];
            Vector2[] uv = new Vector2[vertices.Length];
            int[] triangles = new int[(grid - 1) * (grid - 1) * 6];

            for (int z = 0; z < grid; z++)
            {
                float worldZ = -half + z * step;
                for (int x = 0; x < grid; x++)
                {
                    float worldX = -half + x * step;
                    float radial = Vector2.Distance(new Vector2(worldX, worldZ), Vector2.zero) / half;
                    float edge = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((radial - 0.52f) / 0.48f));
                    float undulation =
                        Mathf.Sin(worldX * 0.075f) * 0.045f +
                        Mathf.Cos(worldZ * 0.065f) * 0.04f +
                        Mathf.Sin((worldX + worldZ) * 0.035f) * 0.025f;

                    // Keep the playable city on a broad, stable plateau while the
                    // outer terrain gently rises/falls so the 3D camera reads depth.
                    float y = edge * 0.18f + undulation * 0.35f;
                    vertices[z * grid + x] = new Vector3(worldX, y, worldZ);
                    uv[z * grid + x] = new Vector2((float)x / (grid - 1), (float)z / (grid - 1));
                }
            }

            int t = 0;
            for (int z = 0; z < grid - 1; z++)
            {
                for (int x = 0; x < grid - 1; x++)
                {
                    int i = z * grid + x;
                    triangles[t++] = i;
                    triangles[t++] = i + grid;
                    triangles[t++] = i + 1;
                    triangles[t++] = i + 1;
                    triangles[t++] = i + grid;
                    triangles[t++] = i + grid + 1;
                }
            }

            mesh.vertices = vertices;
            mesh.triangles = triangles;
            mesh.uv = uv;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            MeshFilter filter = obj.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = obj.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;

            MeshCollider collider = obj.AddComponent<MeshCollider>();
            collider.sharedMesh = mesh;
        }

        private void BuildAndroidRoadGrid()
        {
            // Redesigned macro layout: a broad boulevard/crossroads, secondary streets,
            // sidewalks and a central civic district. The goal is to make the city shape
            // visibly different from the previous uniform 24m grid.
            const float boulevardWidth = 14f;
            const float streetWidth = 8f;
            const float sidewalkWidth = 1.55f;
            float half = worldSize * 0.5f;

            List<Vector3> roadVertices = new List<Vector3>();
            List<int> roadTriangles = new List<int>();
            List<Vector3> sidewalkVertices = new List<Vector3>();
            List<int> sidewalkTriangles = new List<int>();

            AddAndroidQuad(roadVertices, roadTriangles, new Vector3(0f, 0.025f, 0f),
                new Vector2(boulevardWidth, worldSize));
            AddAndroidQuad(roadVertices, roadTriangles, new Vector3(0f, 0.026f, 0f),
                new Vector2(worldSize, boulevardWidth));

            AddAndroidQuad(sidewalkVertices, sidewalkTriangles,
                new Vector3(-boulevardWidth * 0.5f - sidewalkWidth * 0.5f, 0.035f, 0f),
                new Vector2(sidewalkWidth, worldSize));
            AddAndroidQuad(sidewalkVertices, sidewalkTriangles,
                new Vector3(boulevardWidth * 0.5f + sidewalkWidth * 0.5f, 0.035f, 0f),
                new Vector2(sidewalkWidth, worldSize));
            AddAndroidQuad(sidewalkVertices, sidewalkTriangles,
                new Vector3(0f, 0.035f, -boulevardWidth * 0.5f - sidewalkWidth * 0.5f),
                new Vector2(worldSize, sidewalkWidth));
            AddAndroidQuad(sidewalkVertices, sidewalkTriangles,
                new Vector3(0f, 0.035f, boulevardWidth * 0.5f + sidewalkWidth * 0.5f),
                new Vector2(worldSize, sidewalkWidth));

            float[] secondary = { -54f, -27f, 27f, 54f };
            for (int i = 0; i < secondary.Length; i++)
            {
                float p = secondary[i];
                AddAndroidQuad(roadVertices, roadTriangles, new Vector3(p, 0.02f, 0f),
                    new Vector2(streetWidth, worldSize));
                AddAndroidQuad(roadVertices, roadTriangles, new Vector3(0f, 0.021f, p),
                    new Vector2(worldSize, streetWidth));

                AddAndroidQuad(sidewalkVertices, sidewalkTriangles,
                    new Vector3(p - streetWidth * 0.5f - sidewalkWidth * 0.5f, 0.035f, 0f),
                    new Vector2(sidewalkWidth, worldSize));
                AddAndroidQuad(sidewalkVertices, sidewalkTriangles,
                    new Vector3(p + streetWidth * 0.5f + sidewalkWidth * 0.5f, 0.035f, 0f),
                    new Vector2(sidewalkWidth, worldSize));
                AddAndroidQuad(sidewalkVertices, sidewalkTriangles,
                    new Vector3(0f, 0.035f, p - streetWidth * 0.5f - sidewalkWidth * 0.5f),
                    new Vector2(worldSize, sidewalkWidth));
                AddAndroidQuad(sidewalkVertices, sidewalkTriangles,
                    new Vector3(0f, 0.035f, p + streetWidth * 0.5f + sidewalkWidth * 0.5f),
                    new Vector2(worldSize, sidewalkWidth));
            }

            CreateAndroidQuadBatch("AndroidRedesignedBoulevards", roadVertices, roadTriangles, roadMaterial);
            CreateAndroidQuadBatch("AndroidRedesignedSidewalks", sidewalkVertices, sidewalkTriangles, androidSidewalkMaterial);
            BuildAndroidIntersectionsAndLaneMarks(boulevardWidth);
        }

        private void BuildAndroidCentralPlaza()
        {
            Material plaza = MakeMaterial("CentralPlaza", new Color(0.46f, 0.44f, 0.39f));
            Material tile = MakeMaterial("CentralPlazaTile", new Color(0.60f, 0.56f, 0.48f));

            QueueAndroidBox("PlazaBase", new Vector3(0f, 0.06f, 0f), new Vector3(15.5f, 0.12f, 15.5f), plaza);
            for (int i = -2; i <= 2; i++)
            {
                QueueAndroidBox("PlazaTileV", new Vector3(i * 2.9f, 0.125f, 0f), new Vector3(0.11f, 0.03f, 15.2f), tile);
                QueueAndroidBox("PlazaTileH", new Vector3(0f, 0.126f, i * 2.9f), new Vector3(15.2f, 0.03f, 0.11f), tile);
            }

            QueueAndroidBox("PlazaPlanterN", new Vector3(0f, 0.40f, 5.8f), new Vector3(9.4f, 0.68f, 0.90f), androidPlanterMaterial);
            QueueAndroidBox("PlazaHedgeN", new Vector3(0f, 0.92f, 5.5f), new Vector3(8.2f, 0.82f, 0.52f), androidHedgeMaterial);
            QueueAndroidBox("PlazaPlanterS", new Vector3(0f, 0.40f, -5.8f), new Vector3(9.4f, 0.68f, 0.90f), androidPlanterMaterial);
            QueueAndroidBox("PlazaHedgeS", new Vector3(0f, 0.92f, -5.5f), new Vector3(8.2f, 0.82f, 0.52f), androidHedgeMaterial);
        }

        private void BuildAndroidCityPresentation()
        {
            androidBoxBatches.Clear();
            EnsureAndroidCityMaterials();

            // Low-rise gameplay-first city: every normal building is strictly one or two
            // floors, with wider footprints, clearer lanes, courtyards and short sightline
            // breaks so the environment supports movement and combat instead of becoming
            // a tall visual wall.
            StartupCheckpoint.Set("CITY_LOW_RISE_START");

            Vector3[] residential =
            {
                new Vector3(-62f, 0f, -62f), new Vector3(-35f, 0f, -62f),
                new Vector3(-62f, 0f, -35f), new Vector3(-35f, 0f, -35f),
                new Vector3(-62f, 0f, 35f),  new Vector3(-35f, 0f, 35f),
                new Vector3(-62f, 0f, 62f),  new Vector3(-35f, 0f, 62f),
                new Vector3(35f, 0f, -62f),  new Vector3(62f, 0f, -62f),
                new Vector3(35f, 0f, -35f),  new Vector3(62f, 0f, -35f),
                new Vector3(35f, 0f, 35f),   new Vector3(62f, 0f, 35f),
                new Vector3(35f, 0f, 62f),   new Vector3(62f, 0f, 62f)
            };

            for (int i = 0; i < residential.Length; i++)
            {
                float footprint = 12.5f + (i % 3) * 1.6f;
                float height = (i % 4 == 0 || i % 5 == 0) ? 7.8f : 5.3f;
                float depth = 10.8f + (i % 2) * 1.1f;
                CreateAndroidBuilding(
                    residential[i],
                    footprint,
                    height,
                    depth,
                    i % 5,
                    false,
                    false);
            }

            // NORTH MARKET: one-story warehouses and two-story shops create a readable
            // skyline without introducing tall combat-obscuring blocks.
            CreateAndroidBuilding(new Vector3(-27f, 0f, 66f), 18.5f, 6.2f, 13f, 2, false, true);
            CreateAndroidBuilding(new Vector3(0f, 0f, 66f), 18.5f, 6.2f, 13f, 0, false, true);
            CreateAndroidBuilding(new Vector3(27f, 0f, 66f), 18.5f, 6.2f, 13f, 3, false, true);
            CreateAndroidBuilding(new Vector3(-27f, 0f, -66f), 15.0f, 7.8f, 11.5f, 4, false, false);
            CreateAndroidBuilding(new Vector3(27f, 0f, -66f), 15.0f, 7.8f, 11.5f, 1, false, false);

            BuildAndroidCityLandmarks();
            BuildAndroidCentralPlaza();
            BuildAndroidCivicGreen();
            StartupCheckpoint.Set("CITY_LOW_RISE_BLOCKS_DONE");

            Vector3[] treePoints =
            {
                new Vector3(-20f, 0f, -51f), new Vector3(20f, 0f, -51f),
                new Vector3(-20f, 0f, 51f),  new Vector3(20f, 0f, 51f),
                new Vector3(-51f, 0f, -20f), new Vector3(-51f, 0f, 20f),
                new Vector3(51f, 0f, -20f),  new Vector3(51f, 0f, 20f),
                new Vector3(-72f, 0f, 0f),   new Vector3(72f, 0f, 0f),
                new Vector3(0f, 0f, -72f),   new Vector3(0f, 0f, 72f)
            };
            for (int i = 0; i < treePoints.Length; i++)
                CreateAndroidTree(treePoints[i], 3.0f + (i % 3) * 0.35f);

            Vector3[] lampPoints =
            {
                new Vector3(-20f, 0f, -48f), new Vector3(20f, 0f, -48f),
                new Vector3(-20f, 0f, 48f),  new Vector3(20f, 0f, 48f),
                new Vector3(-48f, 0f, -20f), new Vector3(-48f, 0f, 20f),
                new Vector3(48f, 0f, -20f),  new Vector3(48f, 0f, 20f)
            };
            for (int i = 0; i < lampPoints.Length; i++)
                CreateAndroidStreetLamp(lampPoints[i]);

            BuildAndroidFencesAndStreetDetails();
            BuildAndroidAlleysAndVehicles();
            BuildAndroidRuinedQuarter();

            StartupCheckpoint.Set("CITY_LOW_RISE_FLUSH_START");
            FlushAndroidBoxBatches();
            StartupCheckpoint.Set("CITY_FLUSH_DONE");
            StartupCheckpoint.Set("CITY_PRESENTATION_DONE");
        }

        private void BuildAndroidCityLandmarks()
        {
            Material landmark = MakeMaterial("LandmarkStone", new Color(0.52f, 0.47f, 0.39f));
            Material landmarkDark = MakeMaterial("LandmarkDark", new Color(0.20f, 0.23f, 0.25f));
            Material landmarkAccent = MakeMaterial("LandmarkAccent", new Color(0.72f, 0.38f, 0.16f));

            // Entrance arch is a navigation marker, not a combat-height structure.
            QueueAndroidBox("GateLeft", new Vector3(-6.8f, 3.4f, -73f), new Vector3(2.2f, 6.8f, 2.0f), landmark);
            QueueAndroidBox("GateRight", new Vector3(6.8f, 3.4f, -73f), new Vector3(2.2f, 6.8f, 2.0f), landmark);
            QueueAndroidBox("GateTop", new Vector3(0f, 6.1f, -73f), new Vector3(15.8f, 1.8f, 2.0f), landmarkAccent);

            // Two-floor civic hall: visually distinctive but never a tall skyline obstacle.
            QueueAndroidBox("CivicHallBase", new Vector3(0f, 2.55f, 20f), new Vector3(10.0f, 5.1f, 8.5f), landmark);
            QueueAndroidBox("CivicHallUpper", new Vector3(0f, 6.25f, 20f), new Vector3(7.2f, 2.3f, 6.6f), landmarkDark);
            QueueAndroidBox("CivicHallCap", new Vector3(0f, 7.65f, 20f), new Vector3(8.0f, 0.35f, 7.4f), landmarkAccent);

            // A small front stair/forecourt improves readability without blocking the lane.
            QueueAndroidBox("CivicHallStep", new Vector3(0f, 0.32f, 15.2f), new Vector3(5.4f, 0.48f, 2.2f), landmark);
        }

        private void BuildAndroidCivicGreen()
        {
            Material green = MakeMaterial("CivicGreen", new Color(0.31f, 0.43f, 0.20f));
            Material path = MakeMaterial("CivicPath", new Color(0.65f, 0.59f, 0.47f));

            QueueAndroidBox("GreenWest", new Vector3(-20f, 0.08f, 0f), new Vector3(2.8f, 0.16f, 30f), green);
            QueueAndroidBox("GreenEast", new Vector3(20f, 0.08f, 0f), new Vector3(2.8f, 0.16f, 30f), green);
            QueueAndroidBox("GreenNorth", new Vector3(0f, 0.08f, 20f), new Vector3(30f, 0.16f, 2.8f), green);
            QueueAndroidBox("GreenSouth", new Vector3(0f, 0.08f, -20f), new Vector3(30f, 0.16f, 2.8f), green);

            QueueAndroidBox("PathWest", new Vector3(-20f, 0.17f, 0f), new Vector3(0.55f, 0.03f, 29f), path);
            QueueAndroidBox("PathEast", new Vector3(20f, 0.17f, 0f), new Vector3(0.55f, 0.03f, 29f), path);
            QueueAndroidBox("PathNorth", new Vector3(0f, 0.17f, 20f), new Vector3(29f, 0.03f, 0.55f), path);
            QueueAndroidBox("PathSouth", new Vector3(0f, 0.17f, -20f), new Vector3(29f, 0.03f, 0.55f), path);
        }

        private void BuildAndroidFencesAndStreetDetails()
        {
            // Low fences, corner planters and utility boxes add mid-range detail without
            // creating a new renderer for every small prop.
            Vector3[] centers =
            {
                new Vector3(-53f, 0f, -30f), new Vector3(53f, 0f, 30f),
                new Vector3(-30f, 0f, 53f),  new Vector3(30f, 0f, -53f),
                new Vector3(-67f, 0f, 35f),  new Vector3(67f, 0f, -35f)
            };

            for (int i = 0; i < centers.Length; i++)
            {
                Vector3 c0 = centers[i];
                bool horizontal = i % 2 == 0;
                float length = 8.0f;

                QueueAndroidBox("FenceRail", c0 + Vector3.up * 0.66f,
                    horizontal ? new Vector3(length, 0.12f, 0.12f) : new Vector3(0.12f, 0.12f, length),
                    androidFenceRailMaterial);
                QueueAndroidBox("FenceRailLow", c0 + Vector3.up * 0.34f,
                    horizontal ? new Vector3(length, 0.10f, 0.10f) : new Vector3(0.10f, 0.10f, length),
                    androidFenceRailMaterial);

                for (int p = -1; p <= 1; p++)
                {
                    Vector3 post = c0 + (horizontal ? Vector3.right : Vector3.forward) * (p * length * 0.50f);
                    QueueAndroidBox("FencePost", post + Vector3.up * 0.45f, new Vector3(0.16f, 0.90f, 0.16f), androidFenceMaterial);
                }
            }

            for (int i = 0; i < 6; i++)
            {
                float x = -72f + i * 28f;
                QueueAndroidBox("RoadsidePlanter", new Vector3(x, 0.38f, -9.0f),
                    new Vector3(2.8f, 0.64f, 1.0f), androidPlanterMaterial);
                QueueAndroidBox("RoadsideHedge", new Vector3(x, 0.86f, -8.86f),
                    new Vector3(2.20f, 0.72f, 0.72f), androidHedgeMaterial);
            }
        }

        private void BuildAndroidAlleysAndVehicles()
        {
            StartupCheckpoint.Set("CITY_ALLEYS_START");
            const float alleyWidth = 4.2f;

            // Four clear east/west and north/south alleys sit between building rows.
            // They are sparse on purpose: the player gets readable cover lanes instead
            // of a dense carpet of intersecting road meshes.
            float[] alleyOffsets = { -48f, -13f, 13f, 48f };
            List<Vector3> alleyVertices = new List<Vector3>();
            List<int> alleyTriangles = new List<int>();
            for (int i = 0; i < alleyOffsets.Length; i++)
            {
                float offset = alleyOffsets[i];
                AddAndroidQuad(alleyVertices, alleyTriangles,
                    new Vector3(offset, -0.012f, 0f),
                    new Vector2(alleyWidth, worldSize));
                AddAndroidQuad(alleyVertices, alleyTriangles,
                    new Vector3(0f, -0.011f, offset),
                    new Vector2(worldSize, alleyWidth));
            }
            CreateAndroidQuadBatch("AndroidGameplayAlleys", alleyVertices, alleyTriangles, roadMaterial);
            StartupCheckpoint.Set("CITY_ALLEYS_DONE");

            Material[] vehicleMaterials =
            {
                MakeMaterial("VehicleSand", new Color(0.52f, 0.38f, 0.19f)),
                MakeMaterial("VehicleBlue", new Color(0.12f, 0.28f, 0.46f)),
                MakeMaterial("VehicleIvory", new Color(0.72f, 0.68f, 0.57f)),
                MakeMaterial("VehicleRed", new Color(0.48f, 0.16f, 0.12f))
            };

            // Vehicles are placed on the actual clear lanes, not inside building footprints.
            Vector3[] vehiclePoints =
            {
                new Vector3(-48f, 0f, -13f), new Vector3(48f, 0f, 13f),
                new Vector3(-13f, 0f, 48f),  new Vector3(13f, 0f, -48f),
                new Vector3(-48f, 0f, 20f),  new Vector3(48f, 0f, -20f),
                new Vector3(-20f, 0f, -48f), new Vector3(20f, 0f, 48f)
            };

            for (int i = 0; i < vehiclePoints.Length; i++)
            {
                StartupCheckpoint.Set("CITY_VEHICLE_" + (i + 1) + "_START");
                CreateAndroidVehicle(vehiclePoints[i], vehicleMaterials[i % vehicleMaterials.Length], i % 2 == 0);
                StartupCheckpoint.Set("CITY_VEHICLE_" + (i + 1) + "_DONE");
            }
            StartupCheckpoint.Set("CITY_VEHICLES_DONE");
        }

        private void CreateAndroidVehicle(Vector3 position, Material body, bool longAxisZ)
        {
            float length = longAxisZ ? 5.2f : 2.9f;
            float width = longAxisZ ? 2.8f : 5.2f;

            QueueAndroidBox(
                "CityVehicleBody",
                position + Vector3.up * 0.45f,
                new Vector3(width, 0.9f, length),
                body);

            QueueAndroidBox(
                "CityVehicleCabin",
                position + Vector3.up * 0.94f,
                new Vector3(width * 0.68f, 0.40f, length * 0.55f),
                androidVehicleGlassRoofMaterial);

            QueueAndroidBox(
                "CityVehicleBumper",
                position + Vector3.up * 0.28f,
                new Vector3(width * 0.90f, 0.16f, length * 0.90f),
                accentMaterial);

            float wheelY = 0.34f;
            Vector3[] wheels =
            {
                position + new Vector3(-width * 0.36f, wheelY, -length * 0.34f),
                position + new Vector3(width * 0.36f, wheelY, -length * 0.34f),
                position + new Vector3(-width * 0.36f, wheelY, length * 0.34f),
                position + new Vector3(width * 0.36f, wheelY, length * 0.34f)
            };

            for (int i = 0; i < wheels.Length; i++)
                QueueAndroidBox("VehicleWheel", wheels[i], new Vector3(0.42f, 0.30f, 0.58f), androidVehicleWheelMaterial);

            QueueAndroidBox(
                "VehicleGlass",
                position + Vector3.up * 1.11f,
                new Vector3(width * 0.72f, 0.22f, length * 0.47f),
                androidWindowMaterial);

            QueueAndroidBox(
                "VehicleLights",
                position + Vector3.up * 0.52f +
                    (longAxisZ ? Vector3.forward : Vector3.right) * (longAxisZ ? length : width) * 0.39f,
                new Vector3(
                    longAxisZ ? width * 0.38f : 0.34f,
                    0.16f,
                    longAxisZ ? 0.16f : length * 0.38f),
                androidVehicleLightMaterial);
        }

        private void BuildAndroidIntersectionsAndLaneMarks(float roadWidth)
        {
            Material lane = MakeMaterial("AndroidLane", new Color(0.78f, 0.68f, 0.34f));
            Material curb = MakeMaterial("AndroidCurb", new Color(0.56f, 0.54f, 0.49f));
            float half = worldSize * 0.5f;

            List<Vector3> verticalVertices = new List<Vector3>(512);
            List<int> verticalTriangles = new List<int>(768);
            List<Vector3> horizontalVertices = new List<Vector3>(512);
            List<int> horizontalTriangles = new List<int>(768);
            List<Vector3> curbVertices = new List<Vector3>(256);
            List<int> curbTriangles = new List<int>(384);

            float[] roadCenters = { -54f, -27f, 0f, 27f, 54f };
            for (int i = 0; i < roadCenters.Length; i++)
            {
                float center = roadCenters[i];
                float localWidth = Mathf.Approximately(center, 0f) ? roadWidth : 8f;

                for (float z = -half + 8f; z < half; z += 12f)
                {
                    AddAndroidQuad(verticalVertices, verticalTriangles,
                        new Vector3(center, 0.012f, z), new Vector2(0.22f, 4.6f));
                }

                for (float x = -half + 8f; x < half; x += 12f)
                {
                    AddAndroidQuad(horizontalVertices, horizontalTriangles,
                        new Vector3(x, 0.013f, center), new Vector2(4.6f, 0.22f));
                }

                float edge = localWidth * 0.5f;
                AddAndroidQuad(curbVertices, curbTriangles,
                    new Vector3(center - edge, 0.014f, 0f), new Vector2(0.10f, worldSize));
                AddAndroidQuad(curbVertices, curbTriangles,
                    new Vector3(center + edge, 0.014f, 0f), new Vector2(0.10f, worldSize));
                AddAndroidQuad(curbVertices, curbTriangles,
                    new Vector3(0f, 0.014f, center - edge), new Vector2(worldSize, 0.10f));
                AddAndroidQuad(curbVertices, curbTriangles,
                    new Vector3(0f, 0.014f, center + edge), new Vector2(worldSize, 0.10f));
            }

            CreateAndroidQuadBatch("AndroidGameplayLaneDashesV", verticalVertices, verticalTriangles, lane);
            CreateAndroidQuadBatch("AndroidGameplayLaneDashesH", horizontalVertices, horizontalTriangles, lane);
            CreateAndroidQuadBatch("AndroidGameplayCurbs", curbVertices, curbTriangles, curb);
        }

        private static void AddAndroidQuad(List<Vector3> vertices, List<int> triangles, Vector3 center, Vector2 size)
        {
            int start = vertices.Count;
            float hx = size.x * 0.5f;
            float hz = size.y * 0.5f;
            vertices.Add(center + new Vector3(-hx, 0f, -hz));
            vertices.Add(center + new Vector3(hx, 0f, -hz));
            vertices.Add(center + new Vector3(hx, 0f, hz));
            vertices.Add(center + new Vector3(-hx, 0f, hz));
            triangles.Add(start + 0);
            triangles.Add(start + 2);
            triangles.Add(start + 1);
            triangles.Add(start + 0);
            triangles.Add(start + 3);
            triangles.Add(start + 2);
        }

        private GameObject CreateAndroidQuadBatch(string objectName, List<Vector3> vertices, List<int> triangles, Material material)
        {
            if (vertices == null || vertices.Count == 0)
                return null;

            GameObject obj = new GameObject(objectName);
            obj.transform.SetParent(worldRoot, true);
            obj.transform.position = Vector3.zero;

            Mesh mesh = new Mesh { name = objectName + "Mesh" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0, true);
            mesh.RecalculateBounds();
            mesh.UploadMeshData(true);

            MeshFilter filter = obj.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = obj.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            return obj;
        }

        private void CreateAndroidBuilding(
            Vector3 position,
            float footprint,
            float height,
            float depth,
            int styleIndex,
            bool ruined,
            bool warehouse)
        {
            // Gameplay constraint: no ordinary city building exceeds two floors.
            // One-story houses/shops use compact silhouettes; two-story blocks stay low enough
            // to preserve sightlines for the player and enemies.
            float bodyHeight = warehouse
                ? Mathf.Clamp(height, 4.8f, 6.0f)
                : Mathf.Clamp(height, 4.4f, 8.2f);
            int style = Mathf.Abs(styleIndex) % androidFacadeMaterials.Length;
            Material facade = androidFacadeMaterials[style];
            Material trim = androidTrimMaterials[style];
            Material accent = androidAccentMaterials[style];
            Material window = androidWindowMaterial;
            Material door = androidDoorMaterial;

            Vector3 bodySize = new Vector3(footprint, bodyHeight, depth);
            CreateAndroidChamferedShell(
                position,
                footprint,
                bodyHeight,
                depth,
                Mathf.Clamp(Mathf.Min(footprint, depth) * 0.10f, 0.55f, 1.35f),
                facade);
            CreateAndroidCollider("CityBuildingCollider", position, bodySize);

            // Real silhouette pass: chamfered corners + a roof profile that varies by district.
            QueueAndroidBox(
                "BuildingPlinth",
                position + new Vector3(0f, 0.28f, 0f),
                new Vector3(footprint + 0.50f, 0.56f, depth + 0.50f),
                androidPlinthMaterial);

            bool usePitchedRoof = !warehouse && bodyHeight <= 13.5f && (style == 0 || style == 2 || style == 4);
            if (usePitchedRoof)
            {
                CreateAndroidPitchedRoof(
                    position + new Vector3(0f, bodyHeight, 0f),
                    footprint + 0.80f,
                    depth + 0.80f,
                    bodyHeight < 6.1f ? 1.35f : 1.60f,
                    androidRoofTileMaterial);
            }
            else
            {
                QueueAndroidBox(
                    "CityRoofCap",
                    position + Vector3.up * (bodyHeight + 0.22f),
                    new Vector3(footprint + 0.60f, 0.44f, depth + 0.60f),
                    trim);
            }

            // Front floor bands and corner columns make the facade read at camera distance.
            int floorCount = warehouse ? 1 : Mathf.Clamp(Mathf.RoundToInt(bodyHeight / 3.6f), 1, 2);
            for (int row = 0; row < floorCount; row++)
            {
                float y = 1.15f + row * ((bodyHeight - 2.2f) / Mathf.Max(1, floorCount - 1));
                QueueAndroidBox(
                    "FacadeBand",
                    position + new Vector3(0f, y, -depth * 0.515f),
                    new Vector3(footprint * 0.94f, 0.12f, 0.10f),
                    trim);
            }

            float columnWidth = Mathf.Min(0.34f, footprint * 0.035f);
            for (int side = -1; side <= 1; side += 2)
            {
                QueueAndroidBox(
                    "FacadeColumn",
                    position + new Vector3(side * (footprint * 0.43f), bodyHeight * 0.52f, -depth * 0.525f),
                    new Vector3(columnWidth, bodyHeight * 0.92f, 0.18f),
                    trim);
            }

            if (warehouse)
            {
                // Warehouse: wide doors + roof skylights to break the repeated apartment silhouette.
                QueueAndroidBox(
                    "WarehouseDoor",
                    position + new Vector3(0f, 1.90f, -depth * 0.54f),
                    new Vector3(footprint * 0.42f, 3.15f, 0.16f),
                    door);
                QueueAndroidBox(
                    "WarehouseDoorTrim",
                    position + new Vector3(0f, 3.55f, -depth * 0.555f),
                    new Vector3(footprint * 0.50f, 0.20f, 0.42f),
                    accent);

                for (int i = -1; i <= 1; i++)
                {
                    QueueAndroidBox(
                        "WarehouseSkylight",
                        position + new Vector3(i * footprint * 0.25f, bodyHeight + 0.46f, depth * 0.06f),
                        new Vector3(2.20f, 0.18f, 3.00f),
                        androidRoofDetailMaterial);
                }
            }
            else
            {
                int columns = Mathf.Clamp(Mathf.FloorToInt(footprint / 2.45f), 2, 5);
                float spacing = footprint / (columns + 1);

                for (int row = 0; row < floorCount; row++)
                {
                    float y = 1.55f + row * ((bodyHeight - 2.8f) / Mathf.Max(1, floorCount - 1));
                    for (int col = 0; col < columns; col++)
                    {
                        float x = -footprint * 0.5f + spacing * (col + 1);
                        QueueAndroidBox(
                            "WindowFront",
                            position + new Vector3(x, y, -depth * 0.522f),
                            new Vector3(Mathf.Min(1.20f, spacing * 0.50f), 0.84f, 0.10f),
                            window);

                        QueueAndroidBox(
                            "WindowSill",
                            position + new Vector3(x, y - 0.50f, -depth * 0.532f),
                            new Vector3(Mathf.Min(1.35f, spacing * 0.58f), 0.10f, 0.15f),
                            trim);
                    }
                }

                int sideRows = Mathf.Clamp(Mathf.RoundToInt(depth / 3.3f), 2, 4);
                for (int row = 0; row < floorCount; row++)
                {
                    float y = 1.65f + row * ((bodyHeight - 3.0f) / Mathf.Max(1, floorCount - 1));
                    for (int col = 0; col < sideRows; col++)
                    {
                        float z = Mathf.Lerp(-depth * 0.34f, depth * 0.34f, sideRows == 1 ? 0.5f : col / (float)(sideRows - 1));
                        QueueAndroidBox(
                            "WindowSide",
                            position + new Vector3(footprint * 0.522f, y, z),
                            new Vector3(0.10f, 0.82f, Mathf.Min(1.15f, (depth * 0.58f) / sideRows)),
                            window);
                    }
                }

                bool lowRiseHouse = bodyHeight < 6.1f && (style == 0 || style == 2 || style == 4);
                if (lowRiseHouse)
                {
                    QueueAndroidBox(
                        "HousePorch",
                        position + new Vector3(0f, 0.16f, -depth * 0.59f),
                        new Vector3(Mathf.Min(3.6f, footprint * 0.36f), 0.32f, 1.10f),
                        trim);
                    QueueAndroidBox(
                        "HouseDoor",
                        position + new Vector3(0f, 1.14f, -depth * 0.57f),
                        new Vector3(Mathf.Min(1.25f, footprint * 0.16f), 2.20f, 0.16f),
                        door);
                    QueueAndroidBox(
                        "HouseAwning",
                        position + new Vector3(0f, 2.42f, -depth * 0.60f),
                        new Vector3(Mathf.Min(2.8f, footprint * 0.30f), 0.16f, 0.74f),
                        accent);
                }
                else
                {
                    // Shop/apartment entrance: a stronger front band gives the low-rise block
                    // a clearly readable facade without making it taller.
                    QueueAndroidBox(
                        "Storefront",
                        position + new Vector3(0f, 1.18f, -depth * 0.54f),
                        new Vector3(footprint * 0.78f, 1.55f, 0.12f),
                        accent);

                    QueueAndroidBox(
                        "Door",
                        position + new Vector3(0f, 1.20f, -depth * 0.565f),
                        new Vector3(Mathf.Min(1.35f, footprint * 0.18f), 2.30f, 0.15f),
                        door);

                    QueueAndroidBox(
                        "DoorCanopy",
                        position + new Vector3(0f, 2.48f, -depth * 0.58f),
                        new Vector3(Mathf.Min(2.8f, footprint * 0.34f), 0.18f, 0.72f),
                        trim);
                }

                // Select buildings get balconies; others get side AC units.
                if (style == 1 || style == 3)
                {
                    int balconies = bodyHeight > 7.0f ? 1 : 1;
                    for (int i = 0; i < balconies; i++)
                    {
                        float y = 3.10f + i * 3.35f;
                        QueueAndroidBox(
                            "BalconySlab",
                            position + new Vector3(-footprint * 0.20f, y, -depth * 0.60f),
                            new Vector3(footprint * 0.34f, 0.16f, depth * 0.22f),
                            accent);
                        QueueAndroidBox(
                            "BalconyRail",
                            position + new Vector3(-footprint * 0.20f, y + 0.44f, -depth * 0.71f),
                            new Vector3(footprint * 0.34f, 0.66f, 0.08f),
                            trim);
                    }
                }
                else
                {
                    for (int i = 0; i < 2; i++)
                    {
                        QueueAndroidBox(
                            "ACUnit",
                            position + new Vector3(
                                footprint * 0.48f,
                                2.8f + i * 2.25f,
                                -depth * 0.05f + i * 0.70f),
                            new Vector3(0.55f, 0.35f, 0.52f),
                            androidRoofDetailMaterial);
                    }
                }
            }

            // Rooftop equipment gives the camera a deliberate skyline.
            QueueAndroidBox(
                "RoofEquipmentA",
                position + new Vector3(-footprint * 0.28f, bodyHeight + 0.78f, depth * 0.18f),
                new Vector3(1.45f, 0.72f, 1.05f),
                androidRoofDetailMaterial);
            QueueAndroidBox(
                "RoofEquipmentB",
                position + new Vector3(footprint * 0.24f, bodyHeight + 0.56f, -depth * 0.18f),
                new Vector3(0.90f, 0.50f, 0.90f),
                androidRoofDetailMaterial);

            // Raised edge parapets.
            float parapetY = bodyHeight + 0.56f;
            QueueAndroidBox("ParapetFront",
                position + new Vector3(0f, parapetY, -depth * 0.48f),
                new Vector3(footprint + 0.25f, 0.28f, 0.16f), trim);
            QueueAndroidBox("ParapetBack",
                position + new Vector3(0f, parapetY, depth * 0.48f),
                new Vector3(footprint + 0.25f, 0.28f, 0.16f), trim);

            // Ground-level planting breaks up long facade runs.
            QueueAndroidBox(
                "Planter",
                position + new Vector3(-footprint * 0.28f, 0.34f, -depth * 0.66f),
                new Vector3(Mathf.Min(2.5f, footprint * 0.22f), 0.68f, 0.56f),
                androidPlanterMaterial);

            QueueAndroidBox(
                "Hedge",
                position + new Vector3(footprint * 0.28f, 0.58f, -depth * 0.66f),
                new Vector3(Mathf.Min(3.0f, footprint * 0.26f), 1.12f, 0.64f),
                androidHedgeMaterial);
        }

        private Material[] androidFacadeMaterials;
        private Material[] androidTrimMaterials;
        private Material[] androidAccentMaterials;
        private Material androidDoorMaterial;
        private Material androidPlinthMaterial;
        private Material androidRoofDetailMaterial;
        private Material androidRoofTileMaterial;
        private Material androidPlanterMaterial;
        private Material androidHedgeMaterial;
        private Material androidVehicleWheelMaterial;
        private Material androidVehicleGlassRoofMaterial;
        private Material androidVehicleLightMaterial;
        private Material androidFenceMaterial;
        private Material androidFenceRailMaterial;
        private Material androidDebrisMaterial;
        private Material androidCrackMaterial;
        private Material androidTreeTrunkMaterial;
        private Material androidTreeCrownMaterial;
        private Material androidLampMaterial;
        private Material androidLampGlowMaterial;
        private Material androidWindowMaterial;
        private Material androidShadowMaterial;
        private Material androidSidewalkMaterial;

        private void EnsureAndroidCityMaterials()
        {
            if (androidFacadeMaterials != null && androidFacadeMaterials.Length == 5)
                return;

            androidFacadeMaterials = new[]
            {
                MakeMaterial("FacadeSand", new Color(0.67f, 0.58f, 0.46f)),
                MakeMaterial("FacadeSlate", new Color(0.47f, 0.55f, 0.58f)),
                MakeMaterial("FacadeIvory", new Color(0.76f, 0.69f, 0.56f)),
                MakeMaterial("FacadeBlue", new Color(0.40f, 0.51f, 0.54f)),
                MakeMaterial("FacadeOlive", new Color(0.52f, 0.58f, 0.48f))
            };

            androidTrimMaterials = new[]
            {
                MakeMaterial("TrimSand", new Color(0.84f, 0.77f, 0.64f)),
                MakeMaterial("TrimSlate", new Color(0.69f, 0.74f, 0.75f)),
                MakeMaterial("TrimIvory", new Color(0.90f, 0.85f, 0.72f)),
                MakeMaterial("TrimBlue", new Color(0.62f, 0.71f, 0.72f)),
                MakeMaterial("TrimOlive", new Color(0.70f, 0.76f, 0.62f))
            };

            androidAccentMaterials = new[]
            {
                MakeMaterial("AwningTerracotta", new Color(0.61f, 0.28f, 0.17f)),
                MakeMaterial("AwningBlue", new Color(0.17f, 0.35f, 0.42f)),
                MakeMaterial("AwningOchre", new Color(0.72f, 0.50f, 0.18f)),
                MakeMaterial("AwningTeal", new Color(0.18f, 0.42f, 0.42f)),
                MakeMaterial("AwningGreen", new Color(0.31f, 0.45f, 0.27f))
            };

            androidDoorMaterial = MakeMaterial("CityDoor", new Color(0.17f, 0.13f, 0.11f));
            androidPlinthMaterial = MakeMaterial("CityPlinth", new Color(0.36f, 0.35f, 0.32f));
            androidRoofDetailMaterial = MakeMaterial("RoofDetail", new Color(0.26f, 0.28f, 0.29f));
            androidRoofTileMaterial = MakeMaterial("RoofTile", new Color(0.50f, 0.20f, 0.12f));
            androidPlanterMaterial = MakeMaterial("Planter", new Color(0.27f, 0.21f, 0.14f));
            androidHedgeMaterial = MakeMaterial("Hedge", new Color(0.20f, 0.38f, 0.17f));
            androidVehicleWheelMaterial = MakeMaterial("VehicleWheel", new Color(0.035f, 0.045f, 0.055f));
            androidVehicleGlassRoofMaterial = MakeMaterial("VehicleRoofGlass", new Color(0.10f, 0.16f, 0.19f));
            androidVehicleLightMaterial = MakeMaterial("VehicleLight", new Color(0.93f, 0.75f, 0.34f));
            androidFenceMaterial = MakeMaterial("FencePost", new Color(0.21f, 0.22f, 0.20f));
            androidFenceRailMaterial = MakeMaterial("FenceRail", new Color(0.34f, 0.34f, 0.31f));
            androidDebrisMaterial = MakeMaterial("Rubble", new Color(0.29f, 0.28f, 0.25f));
            androidCrackMaterial = MakeMaterial("RuinDark", new Color(0.13f, 0.13f, 0.12f));
            androidTreeTrunkMaterial = MakeMaterial("AndroidTreeTrunk", new Color(0.25f, 0.16f, 0.09f));
            androidTreeCrownMaterial = MakeMaterial("AndroidTreeCrown", new Color(0.17f, 0.38f, 0.13f));
            androidLampMaterial = MakeMaterial("AndroidLamp", new Color(0.10f, 0.12f, 0.14f));
            androidLampGlowMaterial = MakeMaterial("AndroidLampGlow", new Color(0.92f, 0.70f, 0.28f));
        }

        private void CreateAndroidTree(Vector3 position, float scale)
        {
            QueueAndroidBox(
                "TreeTrunk",
                position + Vector3.up * (scale * 0.75f),
                new Vector3(scale * 0.24f, scale * 1.50f, scale * 0.24f),
                androidTreeTrunkMaterial);

            QueueAndroidBox(
                "TreeCrownLower",
                position + Vector3.up * (scale * 1.72f),
                new Vector3(scale * 1.55f, scale * 0.88f, scale * 1.55f),
                androidTreeCrownMaterial);

            QueueAndroidBox(
                "TreeCrownMid",
                position + Vector3.up * (scale * 2.18f),
                new Vector3(scale * 1.22f, scale * 0.74f, scale * 1.22f),
                androidTreeCrownMaterial);

            QueueAndroidBox(
                "TreeCrownUpper",
                position + Vector3.up * (scale * 2.56f),
                new Vector3(scale * 0.86f, scale * 0.58f, scale * 0.86f),
                androidTreeCrownMaterial);
        }

        private void CreateAndroidStreetLamp(Vector3 position)
        {
            QueueAndroidBox(
                "LampPost",
                position + Vector3.up * 2.0f,
                new Vector3(0.16f, 4.0f, 0.16f),
                androidLampMaterial);

            QueueAndroidBox(
                "LampArm",
                position + new Vector3(0.28f, 3.84f, 0f),
                new Vector3(0.72f, 0.12f, 0.12f),
                androidLampMaterial);

            QueueAndroidBox(
                "LampHead",
                position + new Vector3(0.62f, 3.70f, 0f),
                new Vector3(0.58f, 0.18f, 0.34f),
                androidLampGlowMaterial);
        }

        private void BuildAndroidRuinedQuarter()
        {
            Material[] ruinFacades =
            {
                MakeMaterial("RuinFacadeA", new Color(0.35f, 0.33f, 0.30f)),
                MakeMaterial("RuinFacadeB", new Color(0.40f, 0.37f, 0.32f))
            };

            Vector3[] ruinPoints =
            {
                new Vector3(54f, 0f, 54f),
                new Vector3(72f, 0f, 54f),
                new Vector3(54f, 0f, 72f),
                new Vector3(72f, 0f, 72f)
            };

            for (int i = 0; i < ruinPoints.Length; i++)
            {
                Vector3 p = ruinPoints[i];
                float width = 11f + (i % 2) * 2.0f;
                float depth = 9.0f + ((i + 1) % 2) * 1.5f;
                float h = 6.2f + (i % 3) * 0.9f;

                QueueAndroidBox("RuinBlock",
                    p + Vector3.up * (h * 0.5f),
                    new Vector3(width, h, depth),
                    ruinFacades[i % ruinFacades.Length]);

                QueueAndroidBox("RuinUpper",
                    p + new Vector3((i % 2 == 0 ? -2.4f : 2.4f), h + 1.15f, 0f),
                    new Vector3(width * 0.42f, 2.3f, depth * 0.70f),
                    androidCrackMaterial);

                // Large broken openings read better from the elevated camera than tiny debris.
                QueueAndroidBox("RuinOpening",
                    p + new Vector3(0f, 1.65f, -depth * 0.53f),
                    new Vector3(width * 0.48f, 2.7f, 0.12f),
                    androidCrackMaterial);

                for (int d = -1; d <= 1; d++)
                {
                    QueueAndroidBox("Rubble",
                        p + new Vector3(d * 2.1f, 0.30f + (d == 0 ? 0.24f : 0f), depth * 0.70f),
                        new Vector3(1.45f + Mathf.Abs(d) * 0.30f, 0.60f + (d == 0 ? 0.30f : 0f), 1.10f),
                        androidDebrisMaterial);
                }
            }

            // Subtle "crack" bars rather than expensive particle effects.
            for (int i = 0; i < 6; i++)
            {
                float x = 50f + i * 4.2f;
                QueueAndroidBox("GroundCrack",
                    new Vector3(x, 0.072f, 62f + Mathf.Sin(i * 1.7f) * 3f),
                    new Vector3(2.8f, 0.025f, 0.08f),
                    androidCrackMaterial);
            }
        }


        private void CreateAndroidChamferedShell(
            Vector3 position,
            float width,
            float height,
            float depth,
            float chamfer,
            Material material)
        {
            if (material == null)
                return;

            if (!androidBoxBatches.TryGetValue(material, out AndroidBoxBatch batch))
            {
                batch = new AndroidBoxBatch(material);
                androidBoxBatches.Add(material, batch);
            }

            AddAndroidChamferedPrismGeometry(
                batch.Vertices,
                batch.Normals,
                batch.Triangles,
                position,
                width,
                height,
                depth,
                chamfer);
        }

        private void AddAndroidChamferedPrismGeometry(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<int> triangles,
            Vector3 center,
            float width,
            float height,
            float depth,
            float chamfer)
        {
            float x = width * 0.5f;
            float z = depth * 0.5f;
            float c = Mathf.Clamp(chamfer, 0.05f, Mathf.Min(x, z) * 0.45f);

            Vector2[] polygon =
            {
                new Vector2(-x + c, -z),
                new Vector2(x - c, -z),
                new Vector2(x, -z + c),
                new Vector2(x, z - c),
                new Vector2(x - c, z),
                new Vector2(-x + c, z),
                new Vector2(-x, z - c),
                new Vector2(-x, -z + c)
            };

            Vector3[] bottom = new Vector3[polygon.Length];
            Vector3[] top = new Vector3[polygon.Length];
            for (int i = 0; i < polygon.Length; i++)
            {
                bottom[i] = center + new Vector3(polygon[i].x, 0f, polygon[i].y);
                top[i] = center + new Vector3(polygon[i].x, height, polygon[i].y);
            }

            // Bottom face.
            int bottomStart = vertices.Count;
            for (int i = 0; i < bottom.Length; i++)
            {
                vertices.Add(bottom[i]);
                normals.Add(Vector3.down);
            }
            for (int i = 1; i < bottom.Length - 1; i++)
            {
                triangles.Add(bottomStart);
                triangles.Add(bottomStart + i);
                triangles.Add(bottomStart + i + 1);
            }

            // Side faces.
            for (int i = 0; i < polygon.Length; i++)
            {
                int next = (i + 1) % polygon.Length;
                Vector3 edge = top[next] - top[i];
                Vector3 normal = Vector3.Cross(Vector3.up, edge).normalized;

                int start = vertices.Count;
                vertices.Add(bottom[i]);
                vertices.Add(top[i]);
                vertices.Add(top[next]);
                vertices.Add(bottom[next]);
                normals.Add(normal);
                normals.Add(normal);
                normals.Add(normal);
                normals.Add(normal);

                triangles.Add(start + 0);
                triangles.Add(start + 1);
                triangles.Add(start + 2);
                triangles.Add(start + 0);
                triangles.Add(start + 2);
                triangles.Add(start + 3);
            }

            // Top face.
            int topStart = vertices.Count;
            for (int i = 0; i < top.Length; i++)
            {
                vertices.Add(top[i]);
                normals.Add(Vector3.up);
            }
            for (int i = 1; i < top.Length - 1; i++)
            {
                triangles.Add(topStart);
                triangles.Add(topStart + i + 1);
                triangles.Add(topStart + i);
            }
        }

        private void CreateAndroidPitchedRoof(
            Vector3 position,
            float width,
            float depth,
            float rise,
            Material material)
        {
            if (material == null)
                return;

            if (!androidBoxBatches.TryGetValue(material, out AndroidBoxBatch batch))
            {
                batch = new AndroidBoxBatch(material);
                androidBoxBatches.Add(material, batch);
            }

            float x = width * 0.5f;
            float z = depth * 0.5f;
            float overhang = 0.10f;
            x += overhang;
            z += overhang;

            Vector3 fl = position + new Vector3(-x, 0f, -z);
            Vector3 fr = position + new Vector3(x, 0f, -z);
            Vector3 br = position + new Vector3(x, 0f, z);
            Vector3 bl = position + new Vector3(-x, 0f, z);
            Vector3 rl = position + new Vector3(-x, rise, 0f);
            Vector3 rr = position + new Vector3(x, rise, 0f);

            AddAndroidQuadFace(batch.Vertices, batch.Normals, batch.Triangles, fl, rl, rr, fr);
            AddAndroidQuadFace(batch.Vertices, batch.Normals, batch.Triangles, br, rr, rl, bl);
            AddAndroidTriangleFace(batch.Vertices, batch.Normals, batch.Triangles, bl, rl, fl);
            AddAndroidTriangleFace(batch.Vertices, batch.Normals, batch.Triangles, fr, rr, br);
        }

        private void AddAndroidQuadFace(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<int> triangles,
            Vector3 a,
            Vector3 b,
            Vector3 c,
            Vector3 d)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
            int start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            vertices.Add(d);
            normals.Add(normal);
            normals.Add(normal);
            normals.Add(normal);
            normals.Add(normal);
            triangles.Add(start + 0);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start + 0);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
        }

        private void AddAndroidTriangleFace(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<int> triangles,
            Vector3 a,
            Vector3 b,
            Vector3 c)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
            int start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            normals.Add(normal);
            normals.Add(normal);
            normals.Add(normal);
            triangles.Add(start + 0);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
        }

        private void QueueAndroidBox(string objectName, Vector3 position, Vector3 size, Material material)
        {
            if (material == null)
                return;

            if (!androidBoxBatches.TryGetValue(material, out AndroidBoxBatch batch))
            {
                batch = new AndroidBoxBatch(material);
                androidBoxBatches.Add(material, batch);
            }

            AddAndroidBoxGeometry(batch.Vertices, batch.Normals, batch.Triangles, position, size);
        }

        private void AddAndroidBoxGeometry(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<int> triangles,
            Vector3 center,
            Vector3 size)
        {
            Vector3 h = size * 0.5f;

            AddAndroidBoxFace(vertices, normals, triangles,
                new Vector3(-h.x, -h.y, -h.z), new Vector3(h.x, -h.y, -h.z),
                new Vector3(h.x, -h.y, h.z), new Vector3(-h.x, -h.y, h.z), Vector3.down, center);

            AddAndroidBoxFace(vertices, normals, triangles,
                new Vector3(-h.x, h.y, -h.z), new Vector3(-h.x, h.y, h.z),
                new Vector3(h.x, h.y, h.z), new Vector3(h.x, h.y, -h.z), Vector3.up, center);

            AddAndroidBoxFace(vertices, normals, triangles,
                new Vector3(-h.x, -h.y, -h.z), new Vector3(-h.x, h.y, -h.z),
                new Vector3(h.x, h.y, -h.z), new Vector3(h.x, -h.y, -h.z), Vector3.back, center);

            AddAndroidBoxFace(vertices, normals, triangles,
                new Vector3(h.x, -h.y, -h.z), new Vector3(h.x, h.y, -h.z),
                new Vector3(h.x, h.y, h.z), new Vector3(h.x, -h.y, h.z), Vector3.right, center);

            AddAndroidBoxFace(vertices, normals, triangles,
                new Vector3(h.x, -h.y, h.z), new Vector3(h.x, h.y, h.z),
                new Vector3(-h.x, h.y, h.z), new Vector3(-h.x, -h.y, h.z), Vector3.forward, center);

            AddAndroidBoxFace(vertices, normals, triangles,
                new Vector3(-h.x, -h.y, h.z), new Vector3(-h.x, h.y, h.z),
                new Vector3(-h.x, h.y, -h.z), new Vector3(-h.x, -h.y, -h.z), Vector3.left, center);
        }

        private void AddAndroidBoxFace(
            List<Vector3> vertices,
            List<Vector3> normals,
            List<int> triangles,
            Vector3 a,
            Vector3 b,
            Vector3 c,
            Vector3 d,
            Vector3 normal,
            Vector3 center)
        {
            int start = vertices.Count;
            vertices.Add(center + a);
            vertices.Add(center + b);
            vertices.Add(center + c);
            vertices.Add(center + d);
            normals.Add(normal);
            normals.Add(normal);
            normals.Add(normal);
            normals.Add(normal);
            triangles.Add(start + 0);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start + 0);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
        }

        private void CreateAndroidCollider(string objectName, Vector3 position, Vector3 size)
        {
            GameObject obj = new GameObject(objectName);
            obj.transform.SetParent(worldRoot, true);
            obj.transform.position = position + Vector3.up * (size.y * 0.5f);
            BoxCollider box = obj.AddComponent<BoxCollider>();
            box.center = Vector3.zero;
            box.size = size;
        }

        private void FlushAndroidBoxBatches()
        {
            foreach (AndroidBoxBatch batch in androidBoxBatches.Values)
            {
                if (batch.Vertices.Count == 0)
                    continue;

                GameObject obj = new GameObject("AndroidCityBatch");
                obj.transform.SetParent(worldRoot, true);

                Mesh mesh = new Mesh { name = "AndroidCityBatchMesh" };
                if (batch.Vertices.Count > 65000)
                    mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;

                mesh.SetVertices(batch.Vertices);
                mesh.SetNormals(batch.Normals);
                mesh.SetTriangles(batch.Triangles, 0, true);
                mesh.RecalculateBounds();
                mesh.UploadMeshData(true);

                MeshFilter filter = obj.AddComponent<MeshFilter>();
                filter.sharedMesh = mesh;

                MeshRenderer renderer = obj.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = batch.Material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            androidBoxBatches.Clear();
        }

        private GameObject CreateAndroidBox(string objectName, Vector3 position, Vector3 size, Material material, bool collider)
        {
            EnsureAndroidPrimitiveMeshes();

            GameObject obj = new GameObject(objectName);
            obj.transform.SetParent(worldRoot, true);
            obj.transform.position = position;
            obj.transform.localScale = size;

            MeshFilter filter = obj.AddComponent<MeshFilter>();
            filter.sharedMesh = androidUnitCubeMesh;

            MeshRenderer renderer = obj.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;

            if (collider)
            {
                BoxCollider box = obj.AddComponent<BoxCollider>();
                box.center = Vector3.zero;
                box.size = Vector3.one;
            }

            return obj;
        }

        private GameObject CreateFlatMesh(string objectName, Vector3 position, Vector2 size, Material material)
        {
            EnsureAndroidPrimitiveMeshes();

            GameObject obj = new GameObject(objectName);
            obj.transform.SetParent(worldRoot, true);
            obj.transform.position = position;
            obj.transform.localScale = new Vector3(size.x, 1f, size.y);

            MeshFilter filter = obj.AddComponent<MeshFilter>();
            filter.sharedMesh = androidUnitQuadMesh;

            MeshRenderer renderer = obj.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            return obj;
        }

        private void EnsureAndroidPrimitiveMeshes()
        {
            if (androidUnitCubeMesh == null)
            {
                androidUnitCubeMesh = new Mesh { name = "AndroidSharedCube" };
                androidUnitCubeMesh.vertices = new[]
                {
                    new Vector3(-0.5f, -0.5f, -0.5f), new Vector3(0.5f, -0.5f, -0.5f),
                    new Vector3(0.5f, -0.5f, 0.5f), new Vector3(-0.5f, -0.5f, 0.5f),
                    new Vector3(-0.5f, 0.5f, -0.5f), new Vector3(0.5f, 0.5f, -0.5f),
                    new Vector3(0.5f, 0.5f, 0.5f), new Vector3(-0.5f, 0.5f, 0.5f)
                };
                androidUnitCubeMesh.triangles = new[]
                {
                    0, 2, 1, 0, 3, 2,
                    4, 5, 6, 4, 6, 7,
                    0, 1, 5, 0, 5, 4,
                    1, 2, 6, 1, 6, 5,
                    2, 3, 7, 2, 7, 6,
                    3, 0, 4, 3, 4, 7
                };
                androidUnitCubeMesh.RecalculateNormals();
                androidUnitCubeMesh.RecalculateBounds();
                androidUnitCubeMesh.UploadMeshData(true);
            }

            if (androidUnitQuadMesh == null)
            {
                androidUnitQuadMesh = new Mesh { name = "AndroidSharedQuad" };
                androidUnitQuadMesh.vertices = new[]
                {
                    new Vector3(-0.5f, 0f, -0.5f), new Vector3(0.5f, 0f, -0.5f),
                    new Vector3(0.5f, 0f, 0.5f), new Vector3(-0.5f, 0f, 0.5f)
                };
                androidUnitQuadMesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
                androidUnitQuadMesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
                androidUnitQuadMesh.uv = new[]
                {
                    new Vector2(0f, 0f), new Vector2(1f, 0f),
                    new Vector2(1f, 1f), new Vector2(0f, 1f)
                };
                androidUnitQuadMesh.RecalculateBounds();
                androidUnitQuadMesh.UploadMeshData(true);
            }
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