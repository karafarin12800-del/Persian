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
            Material groundMaterial = MakeMaterial("AndroidGround3D", new Color(0.25f, 0.44f, 0.18f));
            buildingMaterial = MakeMaterial("AndroidBuilding", new Color(0.54f, 0.40f, 0.28f));
            roofMaterial = MakeMaterial("AndroidRoof", new Color(0.095f, 0.115f, 0.145f));
            accentMaterial = MakeMaterial("AndroidAccent", new Color(0.86f, 0.66f, 0.22f));
            androidWindowMaterial = MakeMaterial("AndroidWindow", new Color(0.08f, 0.24f, 0.32f));
            androidShadowMaterial = MakeMaterial("AndroidFacadeShadow", new Color(0.24f, 0.19f, 0.16f));
            androidSidewalkMaterial = MakeMaterial("AndroidSidewalk", new Color(0.38f, 0.36f, 0.31f));
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
            const float roadWidth = 10f;
            const float sidewalkWidth = 1.35f;
            float half = worldSize * 0.5f;

            List<Vector3> roadVertices = new List<Vector3>();
            List<int> roadTriangles = new List<int>();
            List<Vector3> sidewalkVertices = new List<Vector3>();
            List<int> sidewalkTriangles = new List<int>();

            for (float x = -half + roadWidth * 0.5f; x <= half; x += 24f)
            {
                AddAndroidQuad(roadVertices, roadTriangles,
                    new Vector3(x, 0.025f, 0f),
                    new Vector2(roadWidth, worldSize));

                AddAndroidQuad(sidewalkVertices, sidewalkTriangles,
                    new Vector3(x - roadWidth * 0.5f - sidewalkWidth * 0.5f, 0.035f, 0f),
                    new Vector2(sidewalkWidth, worldSize));

                AddAndroidQuad(sidewalkVertices, sidewalkTriangles,
                    new Vector3(x + roadWidth * 0.5f + sidewalkWidth * 0.5f, 0.035f, 0f),
                    new Vector2(sidewalkWidth, worldSize));
            }

            for (float z = -half + roadWidth * 0.5f; z <= half; z += 24f)
            {
                AddAndroidQuad(roadVertices, roadTriangles,
                    new Vector3(0f, 0.025f, z),
                    new Vector2(worldSize, roadWidth));

                AddAndroidQuad(sidewalkVertices, sidewalkTriangles,
                    new Vector3(0f, 0.035f, z - roadWidth * 0.5f - sidewalkWidth * 0.5f),
                    new Vector2(worldSize, sidewalkWidth));

                AddAndroidQuad(sidewalkVertices, sidewalkTriangles,
                    new Vector3(0f, 0.035f, z + roadWidth * 0.5f + sidewalkWidth * 0.5f),
                    new Vector2(worldSize, sidewalkWidth));
            }

            CreateAndroidQuadBatch("AndroidRoadGrid", roadVertices, roadTriangles, roadMaterial);
            CreateAndroidQuadBatch("AndroidSidewalkGrid", sidewalkVertices, sidewalkTriangles, androidSidewalkMaterial);
            BuildAndroidIntersectionsAndLaneMarks(roadWidth);
        }

        private void BuildAndroidCityPresentation()
        {
            // Dense, readable 2.5D city layout: buildings hug the streets so the
            // gameplay camera never opens onto a large empty floor.
            androidBoxBatches.Clear();
            Vector3[] buildingPoints =
            {
                new Vector3(-42f, 0f, -42f), new Vector3(-21f, 0f, -42f), new Vector3(21f, 0f, -42f), new Vector3(42f, 0f, -42f),
                new Vector3(-42f, 0f, -21f), new Vector3(42f, 0f, -21f),
                new Vector3(-42f, 0f, 0f),   new Vector3(42f, 0f, 0f),
                new Vector3(-42f, 0f, 21f),  new Vector3(42f, 0f, 21f),
                new Vector3(-42f, 0f, 42f),  new Vector3(-21f, 0f, 42f), new Vector3(21f, 0f, 42f), new Vector3(42f, 0f, 42f),

                new Vector3(-21f, 0f, -21f), new Vector3(21f, 0f, -21f),
                new Vector3(-21f, 0f, 21f),  new Vector3(21f, 0f, 21f),
                new Vector3(-10f, 0f, -34f),  new Vector3(10f, 0f, 34f),
                new Vector3(-34f, 0f, 10f),   new Vector3(34f, 0f, -10f)
            };

            for (int i = 0; i < buildingPoints.Length; i++)
            {
                float footprint = i % 5 == 0 ? 13.5f : (i % 2 == 0 ? 11.5f : 10.0f);
                float height = i % 4 == 0 ? 13.5f : (i % 3 == 0 ? 11.0f : 9.0f);
                float depth = i % 3 == 0 ? 10.5f : 9.0f;
                CreateAndroidBuilding(buildingPoints[i], footprint, height, depth);
            }

            // One renderer per material group replaces hundreds of tiny facade/window
            // renderers while preserving the exact box-based visual language.
            FlushAndroidBoxBatches();

            Vector3[] treePoints =
            {
                new Vector3(-30f, 0f, -30f), new Vector3(30f, 0f, 30f),
                new Vector3(-30f, 0f, 30f),  new Vector3(30f, 0f, -30f),
                new Vector3(-66f, 0f, 0f),   new Vector3(66f, 0f, 0f),
                new Vector3(0f, 0f, 66f),    new Vector3(0f, 0f, -66f)
            };

            for (int i = 0; i < treePoints.Length; i++)
                CreateAndroidTree(treePoints[i], 2.9f + (i % 2) * 0.35f);

            Vector3[] plazaPillars =
            {
                new Vector3(-7f, 0f, 7f), new Vector3(7f, 0f, 7f),
                new Vector3(-7f, 0f, -7f), new Vector3(7f, 0f, -7f)
            };

            for (int i = 0; i < plazaPillars.Length; i++)
                CreateAndroidStreetLamp(plazaPillars[i]);

            BuildAndroidAlleysAndVehicles();
        }

        private void BuildAndroidAlleysAndVehicles()
        {
            const float alleyWidth = 4.5f;
            const float alleySpacing = 12f;
            const int alleyCountPerAxis = 15;
            float half = worldSize * 0.5f - alleySpacing;

            // 15 lanes in each axis = 30 secondary alleys, kept much narrower
            // than the main roads so the city reads as streets + side lanes.
            for (int i = 0; i < alleyCountPerAxis; i++)
            {
                float offset = -half + i * alleySpacing;
                CreateFlatMesh(
                    "Alley_V_" + i,
                    new Vector3(offset, -0.01f, 0f),
                    new Vector2(alleyWidth, worldSize),
                    roadMaterial);

                CreateFlatMesh(
                    "Alley_H_" + i,
                    new Vector3(0f, -0.005f, offset),
                    new Vector2(worldSize, alleyWidth),
                    roadMaterial);
            }

            Material[] vehicleMaterials =
            {
                MakeMaterial("VehicleSand", new Color(0.52f, 0.38f, 0.19f)),
                MakeMaterial("VehicleBlue", new Color(0.12f, 0.28f, 0.46f)),
                MakeMaterial("VehicleIvory", new Color(0.72f, 0.68f, 0.57f)),
                MakeMaterial("VehicleRed", new Color(0.48f, 0.16f, 0.12f))
            };

            Vector3[] vehiclePoints =
            {
                new Vector3(-36f, 0f, -12f), new Vector3(36f, 0f, 12f),
                new Vector3(-60f, 0f, 60f),  new Vector3(60f, 0f, -60f),
                new Vector3(-12f, 0f, 60f),  new Vector3(12f, 0f, -60f),
                new Vector3(-72f, 0f, 24f),  new Vector3(72f, 0f, -24f)
            };

            for (int i = 0; i < vehiclePoints.Length; i++)
                CreateAndroidVehicle(vehiclePoints[i], vehicleMaterials[i % vehicleMaterials.Length], i % 2 == 0);
        }

        private void CreateAndroidVehicle(Vector3 position, Material body, bool longAxisZ)
        {
            float length = longAxisZ ? 5.2f : 2.9f;
            float width = longAxisZ ? 2.8f : 5.2f;
            CreateAndroidBox(
                "CityVehicleBody",
                position + Vector3.up * 0.45f,
                new Vector3(width, 0.9f, length),
                body,
                false);

            CreateAndroidBox(
                "CityVehicleCabin",
                position + Vector3.up * 0.92f,
                new Vector3(width * 0.68f, 0.38f, length * 0.55f),
                roofMaterial,
                false);

            CreateAndroidBox(
                "CityVehicleBumper",
                position + Vector3.up * 0.28f,
                new Vector3(width * 0.88f, 0.16f, length * 0.90f),
                accentMaterial,
                false);

            Material wheel = MakeMaterial("VehicleWheel", new Color(0.035f, 0.045f, 0.055f));
            Material glass = MakeMaterial("VehicleGlass", new Color(0.08f, 0.18f, 0.22f));
            Material light = MakeMaterial("VehicleLight", new Color(0.95f, 0.82f, 0.45f));

            float wheelY = 0.34f;
            Vector3[] wheels =
            {
                position + new Vector3(-width * 0.36f, wheelY, -length * 0.34f),
                position + new Vector3(width * 0.36f, wheelY, -length * 0.34f),
                position + new Vector3(-width * 0.36f, wheelY, length * 0.34f),
                position + new Vector3(width * 0.36f, wheelY, length * 0.34f)
            };
            for (int i = 0; i < wheels.Length; i++)
                CreateAndroidBox("VehicleWheel", wheels[i], new Vector3(0.42f, 0.30f, 0.58f), wheel, false);

            CreateAndroidBox("VehicleGlass", position + Vector3.up * 1.10f,
                new Vector3(width * 0.72f, 0.22f, length * 0.47f), glass, false);

            CreateAndroidBox("VehicleLights",
                position + Vector3.up * 0.52f + (longAxisZ ? Vector3.forward : Vector3.right) * (longAxisZ ? length : width) * 0.39f,
                new Vector3(longAxisZ ? width * 0.38f : 0.34f, 0.16f, longAxisZ ? 0.16f : length * 0.38f),
                light,
                false);
        }

        private void BuildAndroidIntersectionsAndLaneMarks(float roadWidth)
        {
            Material lane = MakeMaterial("AndroidLane", new Color(0.78f, 0.68f, 0.34f));
            Material curb = MakeMaterial("AndroidCurb", new Color(0.56f, 0.54f, 0.49f));
            float half = worldSize * 0.5f;

            // Build all repeated road markings into a few shared meshes instead of
            // hundreds of separate GameObjects. This preserves the same visual grid
            // while dramatically reducing Android hierarchy/renderer overhead.
            List<Vector3> verticalVertices = new List<Vector3>(1024);
            List<int> verticalTriangles = new List<int>(1536);
            List<Vector3> horizontalVertices = new List<Vector3>(1024);
            List<int> horizontalTriangles = new List<int>(1536);
            List<Vector3> curbVertices = new List<Vector3>(256);
            List<int> curbTriangles = new List<int>(384);

            for (float x = -half + 5f; x < half; x += 10f)
            {
                for (float z = -half + roadWidth * 0.5f; z <= half; z += 24f)
                    AddAndroidQuad(verticalVertices, verticalTriangles, new Vector3(x, -0.006f, z), new Vector2(0.28f, 4.2f));
            }

            for (float z = -half + 5f; z < half; z += 10f)
            {
                for (float x = -half + roadWidth * 0.5f; x <= half; x += 24f)
                    AddAndroidQuad(horizontalVertices, horizontalTriangles, new Vector3(x, 0f, z), new Vector2(4.2f, 0.28f));
            }

            for (float x = -half + 0.68f; x <= half; x += 24f)
            {
                AddAndroidQuad(curbVertices, curbTriangles,
                    new Vector3(x - roadWidth * 0.5f, 0.01f, 0f),
                    new Vector2(0.08f, worldSize));
                AddAndroidQuad(curbVertices, curbTriangles,
                    new Vector3(x + roadWidth * 0.5f, 0.01f, 0f),
                    new Vector2(0.08f, worldSize));
            }

            CreateAndroidQuadBatch("AndroidLaneDashV", verticalVertices, verticalTriangles, lane);
            CreateAndroidQuadBatch("AndroidLaneDashH", horizontalVertices, horizontalTriangles, lane);
            CreateAndroidQuadBatch("AndroidCurbs", curbVertices, curbTriangles, curb);
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

        private void CreateAndroidBuilding(Vector3 position, float footprint, float height, float depth)
        {
            float bodyHeight = Mathf.Max(4.5f, height);

            Color[] palette =
            {
                new Color(0.56f, 0.50f, 0.42f),
                new Color(0.68f, 0.58f, 0.43f),
                new Color(0.34f, 0.43f, 0.50f),
                new Color(0.53f, 0.49f, 0.44f),
                new Color(0.40f, 0.49f, 0.38f)
            };
            int paletteIndex = Mathf.Abs(Mathf.RoundToInt(position.x + position.z)) % palette.Length;
            Material facade = MakeMaterial("CityFacade_" + paletteIndex, palette[paletteIndex]);
            Material trim = MakeMaterial("CityTrim_" + paletteIndex, Color.Lerp(palette[paletteIndex], Color.white, 0.28f));
            Material darkWindow = androidWindowMaterial != null
                ? androidWindowMaterial
                : MakeMaterial("CityGlass", new Color(0.09f, 0.20f, 0.25f));
            Material door = MakeMaterial("CityDoor_" + paletteIndex, new Color(0.16f, 0.12f, 0.10f));
            Material rooftop = MakeMaterial("RooftopEquipment_" + paletteIndex, new Color(0.31f, 0.33f, 0.34f));
            Material planter = MakeMaterial("Planter", new Color(0.25f, 0.20f, 0.13f));
            Material hedge = MakeMaterial("Hedge", new Color(0.18f, 0.42f, 0.18f));

            Vector3 bodySize = new Vector3(footprint, bodyHeight, depth);
            QueueAndroidBox("CityBuilding", position + Vector3.up * (bodyHeight * 0.5f), bodySize, facade);
            CreateAndroidCollider("CityBuildingCollider", position, bodySize);

            QueueAndroidBox(
                "CityRoof",
                position + Vector3.up * (bodyHeight + 0.22f),
                new Vector3(footprint + 0.55f, 0.45f, depth + 0.55f),
                trim);

            for (int row = 0; row < 3; row++)
            {
                float y = 1.25f + row * Mathf.Max(1.7f, (bodyHeight - 2.4f) / 3f);
                QueueAndroidBox(
                    "FacadeBand",
                    position + new Vector3(0f, y, -depth * 0.515f),
                    new Vector3(footprint * 0.92f, 0.16f, 0.10f),
                    trim);
            }

            int columns = Mathf.Clamp(Mathf.FloorToInt(footprint / 2.5f), 2, 4);
            float spacing = footprint / (columns + 1);
            for (int row = 0; row < 3; row++)
            {
                float y = 1.75f + row * Mathf.Max(1.7f, (bodyHeight - 3.1f) / 3f);
                for (int col = 0; col < columns; col++)
                {
                    float x = -footprint * 0.5f + spacing * (col + 1);
                    QueueAndroidBox(
                        "Window",
                        position + new Vector3(x, y, -depth * 0.522f),
                        new Vector3(Mathf.Min(1.15f, spacing * 0.48f), 0.72f, 0.11f),
                        darkWindow);

                    QueueAndroidBox(
                        "WindowSill",
                        position + new Vector3(x, y - 0.46f, -depth * 0.53f),
                        new Vector3(Mathf.Min(1.32f, spacing * 0.56f), 0.10f, 0.16f),
                        trim);
                }
            }

            QueueAndroidBox(
                "Door",
                position + new Vector3(0f, 1.10f, -depth * 0.54f),
                new Vector3(Mathf.Min(1.35f, footprint * 0.18f), 2.15f, 0.14f),
                door);

            QueueAndroidBox(
                "DoorCanopy",
                position + new Vector3(0f, 2.30f, -depth * 0.56f),
                new Vector3(Mathf.Min(2.4f, footprint * 0.30f), 0.18f, 0.72f),
                trim);

            for (int i = 0; i < 2; i++)
            {
                Vector3 offset = new Vector3(
                    (i == 0 ? -0.28f : 0.28f) * footprint,
                    bodyHeight + 0.70f,
                    (i == 0 ? -0.20f : 0.22f) * depth);

                QueueAndroidBox(
                    "RooftopUnit",
                    position + offset,
                    new Vector3(1.15f, 0.55f, 0.85f),
                    rooftop);
            }

            QueueAndroidBox(
                "Planter",
                position + new Vector3(-footprint * 0.30f, 0.34f, -depth * 0.66f),
                new Vector3(Mathf.Min(2.6f, footprint * 0.22f), 0.68f, 0.55f),
                planter);

            QueueAndroidBox(
                "Hedge",
                position + new Vector3(footprint * 0.30f, 0.55f, -depth * 0.66f),
                new Vector3(Mathf.Min(3.0f, footprint * 0.26f), 1.10f, 0.65f),
                hedge);
        }

        private Material androidTreeTrunkMaterial;
        private Material androidTreeCrownMaterial;
        private Material androidLampMaterial;
        private Material androidLampGlowMaterial;
        private Material androidWindowMaterial;
        private Material androidShadowMaterial;
        private Material androidSidewalkMaterial;

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
                "TreeCrownLower",
                position + Vector3.up * (scale * 1.75f),
                new Vector3(scale * 1.55f, scale * 0.92f, scale * 1.55f),
                androidTreeCrownMaterial,
                false);

            CreateAndroidBox(
                "TreeCrownUpper",
                position + Vector3.up * (scale * 2.30f),
                new Vector3(scale * 1.02f, scale * 0.78f, scale * 1.02f),
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