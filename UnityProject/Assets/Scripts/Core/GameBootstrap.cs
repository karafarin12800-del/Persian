using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private Camera gameplayCamera;
        [SerializeField] private float worldSize = 220.8f;
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
        // Each Android house owns its own batched renderers so the camera can fade
        // only the building that actually blocks the player, not every house at once.
        private Transform androidCurrentBuildingRoot;

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
                RuntimeGameAudio.EnsureInstance();
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
#if UNITY_ANDROID
            DisableLegacyWorldBuilderPath();
#endif
            GameObject legacyGround = GameObject.Find("Ground");
            if (legacyGround != null) legacyGround.SetActive(false);

            if (worldRoot != null) Destroy(worldRoot.gameObject);
            worldRoot = new GameObject("BattleRoyaleCity").transform;

            // Mid-light neutral asphalt: the previous near-black tint made streets
            // too dark on Android and reduced contrast against buildings and sidewalks.
            roadMaterial = MakeMaterial("Road", new Color(0.52f, 0.54f, 0.57f));

#if UNITY_ANDROID
            // Android uses a lightweight real 3D terrain mesh rather than the old flat
            // green isolation floor. It keeps geometry/collider cost bounded while giving
            // the camera genuine height, slope and depth information.
            // The old CreateFlatMesh battlefield floor is intentionally not used here;
            // this path now owns the actual 3D terrain geometry.
            Material groundMaterial = MakeMaterial("AndroidGround3D", new Color(0.12f, 0.46f, 0.08f));
            buildingMaterial = MakeMaterial("AndroidBuilding", new Color(0.54f, 0.40f, 0.28f));
            roofMaterial = MakeMaterial("AndroidRoof", new Color(0.095f, 0.115f, 0.145f));
            accentMaterial = MakeMaterial("AndroidAccent", new Color(0.86f, 0.66f, 0.22f));
            androidWindowMaterial = MakeMaterial("AndroidWindow", new Color(0.08f, 0.24f, 0.32f));
            androidShadowMaterial = MakeMaterial("AndroidFacadeShadow", new Color(0.24f, 0.19f, 0.16f));
            androidSidewalkMaterial = MakeMaterial("AndroidSidewalk", new Color(0.58f, 0.54f, 0.47f));
            BuildAndroidTerrain3D(groundMaterial);
            // A continuous backing slab extends beyond the terrain bounds. Its top
            // stays below the lowest terrain vertices, preventing black pinholes
            // without z-fighting against the visible terrain surface.
            CreateAndroidBox(
                "AndroidContinuousGroundFoundation",
                new Vector3(0f, -0.26f, 0f),
                new Vector3(worldSize + 8f, 0.35f, worldSize + 8f),
                groundMaterial,
                false);
            BuildAndroidRoadGrid();
            BuildAndroidCityPresentation();
            SpawnRandomHouseLoot();
            RemoveAnyExtractionBeacon();
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

#if UNITY_ANDROID
        private void DisableLegacyWorldBuilderPath()
        {
            // The retired WorldBuilder path used gray-olive ground and nearly black roads.
            // Disable its generator and clean up its objects if it ran before this bootstrap.
            WorldBuilder[] legacyBuilders = FindObjectsByType<WorldBuilder>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < legacyBuilders.Length; i++)
            {
                if (legacyBuilders[i] != null)
                    legacyBuilders[i].enabled = false;
            }

            GameObject[] existingObjects = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            int removed = 0;
            for (int i = 0; i < existingObjects.Length; i++)
            {
                GameObject candidate = existingObjects[i];
                if (candidate == null)
                    continue;

                string objectName = candidate.name;
                bool legacySurface =
                    objectName == "World_Ground" ||
                    objectName.StartsWith("MainRoad_") ||
                    objectName.StartsWith("Alley_");

                if (legacySurface)
                {
                    candidate.SetActive(false);
                    removed++;
                }
            }

            Debug.Log("PERSIA_WORLD_CLEANUP: legacy builders=" + legacyBuilders.Length +
                      ", removed legacy ground/road objects=" + removed);
        }
#endif

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
                    float y = CalculateAndroidTerrainHeight(worldX, worldZ);
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

        private float CalculateAndroidTerrainHeight(float worldX, float worldZ)
        {
            float half = worldSize * 0.5f;
            float radial = Vector2.Distance(new Vector2(worldX, worldZ), Vector2.zero) / half;
            float edge = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((radial - 0.52f) / 0.48f));
            float undulation =
                Mathf.Sin(worldX * 0.075f) * 0.045f +
                Mathf.Cos(worldZ * 0.065f) * 0.04f +
                Mathf.Sin((worldX + worldZ) * 0.035f) * 0.025f;
            return edge * 0.18f + undulation * 0.35f;
        }

        private void AddAndroidTerrainFollowingQuad(
            List<Vector3> vertices,
            List<int> triangles,
            Vector3 center,
            Vector2 size,
            float heightOffset)
        {
            // Long roads and sidewalks use short terrain-sampled segments rather
            // than one large quad. This prevents the road mesh from floating above
            // or sinking below the radial terrain profile between its endpoints.
            const float maxSegmentLength = 8f;
            int xSegments = Mathf.Max(1, Mathf.CeilToInt(size.x / maxSegmentLength));
            int zSegments = Mathf.Max(1, Mathf.CeilToInt(size.y / maxSegmentLength));
            int start = vertices.Count;
            float halfX = size.x * 0.5f;
            float halfZ = size.y * 0.5f;

            for (int z = 0; z <= zSegments; z++)
            {
                float worldZ = center.z - halfZ + size.y * z / zSegments;
                for (int x = 0; x <= xSegments; x++)
                {
                    float worldX = center.x - halfX + size.x * x / xSegments;
                    vertices.Add(new Vector3(
                        worldX,
                        CalculateAndroidTerrainHeight(worldX, worldZ) + heightOffset,
                        worldZ));
                }
            }

            for (int z = 0; z < zSegments; z++)
            {
                for (int x = 0; x < xSegments; x++)
                {
                    int a = start + z * (xSegments + 1) + x;
                    int b = a + 1;
                    int d = a + xSegments + 1;
                    int c = d + 1;
                    // Same upward-facing winding as AddAndroidQuad.
                    triangles.Add(a);
                    triangles.Add(c);
                    triangles.Add(b);
                    triangles.Add(a);
                    triangles.Add(d);
                    triangles.Add(c);
                }
            }
        }

        private void BuildAndroidRoadGrid()
        {
            const float roadWidth = 10f;
            const float sidewalkWidth = 1.0f;
            float half = worldSize * 0.5f;

            List<Vector3> roadVertices = new List<Vector3>();
            List<int> roadTriangles = new List<int>();
            List<Vector3> sidewalkVertices = new List<Vector3>();
            List<int> sidewalkTriangles = new List<int>();

            for (float x = -half + roadWidth * 0.5f; x <= half; x += 24f)
            {
                AddAndroidTerrainFollowingQuad(roadVertices, roadTriangles,
                    new Vector3(x, 0f, 0f),
                    new Vector2(roadWidth, worldSize), 0.035f);

                AddAndroidTerrainFollowingQuad(sidewalkVertices, sidewalkTriangles,
                    new Vector3(x - roadWidth * 0.5f - sidewalkWidth * 0.5f, 0f, 0f),
                    new Vector2(sidewalkWidth, worldSize), 0.045f);

                AddAndroidTerrainFollowingQuad(sidewalkVertices, sidewalkTriangles,
                    new Vector3(x + roadWidth * 0.5f + sidewalkWidth * 0.5f, 0f, 0f),
                    new Vector2(sidewalkWidth, worldSize), 0.045f);
            }

            for (float z = -half + roadWidth * 0.5f; z <= half; z += 24f)
            {
                AddAndroidTerrainFollowingQuad(roadVertices, roadTriangles,
                    new Vector3(0f, 0f, z),
                    new Vector2(worldSize, roadWidth), 0.035f);

                AddAndroidTerrainFollowingQuad(sidewalkVertices, sidewalkTriangles,
                    new Vector3(0f, 0f, z - roadWidth * 0.5f - sidewalkWidth * 0.5f),
                    new Vector2(worldSize, sidewalkWidth), 0.045f);

                AddAndroidTerrainFollowingQuad(sidewalkVertices, sidewalkTriangles,
                    new Vector3(0f, 0f, z + roadWidth * 0.5f + sidewalkWidth * 0.5f),
                    new Vector2(worldSize, sidewalkWidth), 0.045f);
            }

            CreateAndroidQuadBatch("AndroidRoadGrid", roadVertices, roadTriangles, roadMaterial);
            CreateAndroidQuadBatch("AndroidSidewalkGrid", sidewalkVertices, sidewalkTriangles, androidSidewalkMaterial);
            BuildAndroidIntersectionsAndLaneMarks(roadWidth);
        }

        private bool extractionBeaconShown;

        private void RemoveAnyExtractionBeacon()
        {
            string[] names = { "BlueSkyGuideBeamOuter", "BlueSkyGuideBeamCore", "BlueSkyGuideGroundRing", "BlueSkyGuideGroundLight" };
            GameObject[] existing = FindObjectsByType<GameObject>(FindObjectsSortMode.None);
            for (int i = 0; i < existing.Length; i++)
            {
                if (existing[i] == null) continue;
                for (int n = 0; n < names.Length; n++)
                    if (existing[i].name == names[n]) Destroy(existing[i]);
            }
            extractionBeaconShown = false;
        }

        public void ShowRandomExtractionBeacon()
        {
            if (extractionBeaconShown || worldRoot == null) return;
            RemoveAnyExtractionBeacon();
            extractionBeaconShown = true;

            // Random road intersection chosen only at victory; no blue beam is visible during the match.
            float[] roads = { -67f, -43f, -19f, 5f, 29f, 53f, 77f };
            float beaconX = roads[Random.Range(0, roads.Length)];
            float beaconZ = roads[Random.Range(0, roads.Length)];
            Vector3 ground = new Vector3(beaconX, CalculateAndroidTerrainHeight(beaconX, beaconZ) + 0.08f, beaconZ);

            CreateAndroidSkyGuideCylinder("BlueSkyGuideBeamOuter", ground + Vector3.up * 19f,
                new Vector3(3.4f, 19f, 3.4f), new Color(0.04f, 0.25f, 1f, 0.20f), true);
            CreateAndroidSkyGuideCylinder("BlueSkyGuideBeamCore", ground + Vector3.up * 19f,
                new Vector3(1.15f, 19f, 1.15f), new Color(0.20f, 0.66f, 1f, 0.48f), true);
            CreateAndroidSkyGuideCylinder("BlueSkyGuideGroundRing", ground + Vector3.up * 0.06f,
                new Vector3(4.6f, 0.06f, 4.6f), new Color(0.12f, 0.60f, 1f, 0.96f), false);

            GameObject lightObject = new GameObject("BlueSkyGuideGroundLight");
            lightObject.transform.SetParent(worldRoot, false);
            lightObject.transform.position = ground + Vector3.up * 0.45f;
            Light blueLight = lightObject.AddComponent<Light>();
            blueLight.type = LightType.Point;
            blueLight.color = new Color(0.12f, 0.42f, 1f);
            blueLight.intensity = 1.25f;
            blueLight.range = 7f;
            blueLight.shadows = LightShadows.None;
            Debug.Log("PERSIA_GUIDE: Random extraction beacon activated at " + beaconX + "," + beaconZ);
        }

        private void SpawnRandomHouseLoot()
        {
            // Eight of each supply type are placed at randomized floor positions
            // inside generated houses each time a new match is built.
            if (androidBuildingCenters.Count == 0) return;
            const int countPerType = 8;
            for (int i = 0; i < countPerType * 3; i++)
            {
                int houseIndex = Random.Range(0, androidBuildingCenters.Count);
                Vector3 center = androidBuildingCenters[houseIndex];
                Vector2 half = androidBuildingHalfExtents[houseIndex];
                float x = center.x + Random.Range(-half.x * 0.42f, half.x * 0.42f);
                float z = center.z + Random.Range(-half.y * 0.42f, half.y * 0.42f);
                float y = CalculateAndroidTerrainHeight(x, z) + 0.38f;
                PickupItem.PickupType type = i < countPerType ? PickupItem.PickupType.Weapon
                    : (i < countPerType * 2 ? PickupItem.PickupType.Shield : PickupItem.PickupType.Grenade);
                Color color = type == PickupItem.PickupType.Weapon ? new Color(0.95f, 0.72f, 0.18f)
                    : (type == PickupItem.PickupType.Shield ? new Color(0.12f, 0.55f, 1f) : new Color(0.95f, 0.28f, 0.12f));

                GameObject item = GameObject.CreatePrimitive(type == PickupItem.PickupType.Weapon ? PrimitiveType.Cube : PrimitiveType.Sphere);
                item.name = "HouseLoot_" + type + "_" + i;
                item.transform.SetParent(worldRoot, true);
                item.transform.position = new Vector3(x, y, z);
                item.transform.localScale = type == PickupItem.PickupType.Weapon ? new Vector3(0.72f, 0.18f, 0.24f) : Vector3.one * 0.48f;
                Renderer renderer = item.GetComponent<Renderer>();
                if (renderer != null) renderer.sharedMaterial = MakeMaterial("HouseLootMaterial_" + type + "_" + i, color);
                Collider collider = item.GetComponent<Collider>();
                if (collider != null) collider.isTrigger = true;
                Rigidbody body = item.AddComponent<Rigidbody>();
                body.isKinematic = true;
                body.useGravity = false;
                PickupItem pickup = item.AddComponent<PickupItem>();
                if (type == PickupItem.PickupType.Weapon)
                    pickup.ConfigureWeapon((WeaponController.WeaponKind)Random.Range(0, 3), 30);
                else
                    pickup.Configure(type, type == PickupItem.PickupType.Shield ? 35 : 2);
            }
            Debug.Log("PERSIA_LOOT: spawned 8 weapons, 8 shields and 8 grenade pickups in houses");
        }

        private void CreateAndroidSkyGuideCylinder(
            string objectName,
            Vector3 position,
            Vector3 scale,
            Color color,
            bool transparent)
        {
            GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder.name = objectName;
            cylinder.transform.SetParent(worldRoot, false);
            cylinder.transform.position = position;
            cylinder.transform.localScale = scale;

            Collider collider = cylinder.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            Shader shader = transparent
                ? (Resources.Load<Shader>("PersiaWarAndroidFade") ??
                   Shader.Find("PersiaWar/AndroidFade") ??
                   Shader.Find("Unlit/Transparent") ??
                   Shader.Find("Sprites/Default"))
                : (Resources.Load<Shader>("PersiaWarAndroidFlat") ??
                   Shader.Find("PersiaWar/AndroidFlat") ??
                   Shader.Find("Unlit/Color"));
            if (shader == null)
            {
                Debug.LogWarning("PERSIA_GUIDE: no supported shader for " + objectName);
                Destroy(cylinder);
                return;
            }

            Material material = new Material(shader)
            {
                name = objectName + "Material",
                color = color
            };
            if (material.HasProperty("_Color"))
                material.SetColor("_Color", color);
            if (material.HasProperty("_BaseColor"))
                material.SetColor("_BaseColor", color);
            if (material.HasProperty("_FadeAlpha"))
                material.SetFloat("_FadeAlpha", 1f);
            if (transparent)
                material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

            Renderer renderer = cylinder.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        private void BuildAndroidCityPresentation()
        {
            // Dense, readable 2.5D city layout: buildings hug the streets so the
            // gameplay camera never opens onto a large empty floor.
            androidBoxBatches.Clear();
            androidBuildingCenters.Clear();
            androidBuildingHalfExtents.Clear();
            // Spread buildings across the playable map, including outer blocks.
            // Coordinates sit between the 24m road lanes so streets and intersections stay open.
            Vector3[] buildingPoints =
            {
                new Vector3(-79f, 0f, -79f), new Vector3(-55f, 0f, -79f), new Vector3(41f, 0f, -79f), new Vector3(65f, 0f, -79f),
                new Vector3(-79f, 0f, -55f), new Vector3(-31f, 0f, -55f), new Vector3(17f, 0f, -55f), new Vector3(65f, 0f, -55f),
                new Vector3(-55f, 0f, -31f), new Vector3(-7f, 0f, -31f), new Vector3(41f, 0f, -31f),
                new Vector3(-79f, 0f, -7f), new Vector3(-31f, 0f, -7f), new Vector3(41f, 0f, -7f), new Vector3(65f, 0f, -7f),
                new Vector3(-55f, 0f, 17f), new Vector3(-7f, 0f, 17f), new Vector3(41f, 0f, 17f),
                new Vector3(-79f, 0f, 41f), new Vector3(-31f, 0f, 41f), new Vector3(17f, 0f, 41f), new Vector3(65f, 0f, 41f),
                new Vector3(-55f, 0f, 65f), new Vector3(-7f, 0f, 65f), new Vector3(41f, 0f, 65f), new Vector3(65f, 0f, 65f),
                new Vector3(-79f, 0f, 89f), new Vector3(-31f, 0f, 89f), new Vector3(17f, 0f, 89f), new Vector3(65f, 0f, 89f)
            };

            for (int i = 0; i < buildingPoints.Length; i++)
            {
                StartupCheckpoint.Set("CITY_BUILDING_" + (i + 1) + "_START");

                // The Android match is built here, not by EnvironmentSpawner. Use
                // stable per-building variation so silhouettes do not repeat in a
                // small set of modulo-based sizes between builds.
                float widthRoll = Mathf.Abs(Mathf.Sin((i + 1) * 12.9898f));
                float heightRoll = Mathf.Abs(Mathf.Sin((i + 1) * 78.233f));
                float depthRoll = Mathf.Abs(Mathf.Sin((i + 1) * 39.425f));

                bool isWarehouse = i == 4 || i == 9 || i == 11 || i == 16;
                // Two out of every three residential houses receive a gabled roof.
                // Warehouses keep broad low rooflines for a readable silhouette.
                bool isPitchedRoof = !isWarehouse && (i % 3 != 0 || i == 0);

                // Keep the full footprint (including facade trim) within the
                // sidewalk-to-sidewalk block so roads never run under house walls.
                float footprint = Mathf.Lerp(7.2f, 10.2f, widthRoll);
                float depth = Mathf.Lerp(6.8f, 9.8f, depthRoll);
                bool twoStorey = heightRoll > 0.52f;
                float height = twoStorey
                    ? Mathf.Lerp(5.4f, 6.4f, Mathf.InverseLerp(0.52f, 1f, heightRoll))
                    : Mathf.Lerp(3.25f, 3.95f, Mathf.InverseLerp(0f, 0.52f, heightRoll));

                // Inner homes keep smaller footprints so the spawn lanes and alleys
                // remain open. All ordinary houses are only one or two storeys.
                if (i >= 22)
                {
                    footprint = Mathf.Lerp(6.5f, 8.2f, widthRoll);
                    height = twoStorey ? 5.4f : 3.55f;
                    depth = Mathf.Lerp(6.0f, 7.8f, depthRoll);
                }
                else if (i >= 18 && i <= 21)
                {
                    footprint = Mathf.Lerp(7.5f, 9.8f, widthRoll);
                    depth = Mathf.Lerp(6.8f, 9.2f, depthRoll);
                }

                // A few wider corner homes become landmarks without becoming towers.
                // Four dedicated low, deep industrial buildings are warehouses.
                if (i == 0 || i == 7 || i == 13)
                    footprint = Mathf.Max(footprint, 10.2f);

                if (isWarehouse)
                {
                    footprint = Mathf.Lerp(9.4f, 10.4f, widthRoll);
                    depth = Mathf.Lerp(8.2f, 10.0f, depthRoll);
                    height = Mathf.Lerp(3.55f, 4.45f, heightRoll);
                }

                buildingPoints[i].y = CalculateAndroidTerrainHeight(buildingPoints[i].x, buildingPoints[i].z);
                androidBuildingCenters.Add(buildingPoints[i]);
                androidBuildingHalfExtents.Add(new Vector2(footprint * 0.5f, depth * 0.5f));

                androidCurrentBuildingRoot = new GameObject("Building_House_" + (i + 1)).transform;
                androidCurrentBuildingRoot.SetParent(worldRoot, true);
                CreateAndroidBuilding(
                    buildingPoints[i], footprint, height, depth, i, isWarehouse, isPitchedRoof);
                // Flush per house into material-group meshes parented beneath this house.
                // This preserves most batching while making per-house transparency possible.
                FlushAndroidBoxBatches();
                androidCurrentBuildingRoot = null;
                StartupCheckpoint.Set("CITY_BUILDING_" + (i + 1) + "_DONE");
            }

            StartupCheckpoint.Set("CITY_BUILDINGS_ALL_DONE");
            StartupCheckpoint.Set("CITY_FLUSH_START");

            // One renderer per material group replaces hundreds of tiny facade/window
            // renderers while preserving the exact box-based visual language.
            FlushAndroidBoxBatches();
            StartupCheckpoint.Set("CITY_FLUSH_DONE");

            Vector3[] treePoints =
            {
                new Vector3(-30f, 0f, -30f), new Vector3(30f, 0f, 30f),
                new Vector3(-30f, 0f, 30f),  new Vector3(30f, 0f, -30f),
                new Vector3(-66f, 0f, 0f),   new Vector3(66f, 0f, 0f),
                new Vector3(0f, 0f, 66f),    new Vector3(0f, 0f, -66f)
            };

            StartupCheckpoint.Set("CITY_TREES_START");
            for (int i = 0; i < treePoints.Length; i++)
            {
                StartupCheckpoint.Set("CITY_TREE_" + (i + 1) + "_START");
                CreateAndroidTree(treePoints[i], 2.9f + (i % 2) * 0.35f);
                StartupCheckpoint.Set("CITY_TREE_" + (i + 1) + "_DONE");
            }
            StartupCheckpoint.Set("CITY_TREES_DONE");

            // Keep a small number of landmarks so Android remains light, but give
            // the camera more of the readable street-furniture language from the reference.
            Vector3[] plazaPillars =
            {
                new Vector3(-7f, 0f, 7f), new Vector3(7f, 0f, 7f),
                new Vector3(-7f, 0f, -7f), new Vector3(7f, 0f, -7f),
                new Vector3(-36f, 0f, -10f), new Vector3(36f, 0f, 10f),
                new Vector3(-60f, 0f, 48f), new Vector3(60f, 0f, -48f)
            };

            StartupCheckpoint.Set("CITY_LAMPS_START");
            for (int i = 0; i < plazaPillars.Length; i++)
            {
                StartupCheckpoint.Set("CITY_LAMP_" + (i + 1) + "_START");
                CreateAndroidStreetLamp(plazaPillars[i]);
                StartupCheckpoint.Set("CITY_LAMP_" + (i + 1) + "_DONE");
            }
            StartupCheckpoint.Set("CITY_LAMPS_DONE");

            StartupCheckpoint.Set("CITY_ALLEYS_VEHICLES_START");
            BuildAndroidAlleysAndVehicles();
            StartupCheckpoint.Set("CITY_ALLEYS_VEHICLES_DONE");
            StartupCheckpoint.Set("CITY_PRESENTATION_DONE");
        }

        private readonly List<Vector3> androidBuildingCenters = new List<Vector3>();
        private readonly List<Vector2> androidBuildingHalfExtents = new List<Vector2>();

        private void BuildAndroidAlleysAndVehicles()
        {
            StartupCheckpoint.Set("CITY_ALLEYS_START");
            const float alleyWidth = 3.2f;
            const float alleySpacing = 12f;
            const int alleyCountPerAxis = 15;
            float half = worldSize * 0.5f;

            // Build narrow 12m alleys as clear segments. Each segment stops before
            // the actual house footprint (including a small safety margin), rather
            // than drawing an infinite road strip through house interiors.
            List<Vector3> alleyVertices = new List<Vector3>();
            List<int> alleyTriangles = new List<int>();
            for (int i = 0; i < alleyCountPerAxis; i++)
            {
                float offset = -half + alleySpacing + i * alleySpacing;
                AddClearAndroidAlleySegments(alleyVertices, alleyTriangles, offset, true, half, alleyWidth);
                AddClearAndroidAlleySegments(alleyVertices, alleyTriangles, offset, false, half, alleyWidth);
            }
            CreateAndroidQuadBatch("AndroidSecondaryAlleys", alleyVertices, alleyTriangles, roadMaterial);
            StartupCheckpoint.Set("CITY_ALLEYS_DONE");

            Material[] vehicleMaterials =
            {
                MakeMaterial("VehicleSand", new Color(0.52f, 0.38f, 0.19f)),
                MakeMaterial("VehicleBlue", new Color(0.12f, 0.28f, 0.46f)),
                MakeMaterial("VehicleIvory", new Color(0.72f, 0.68f, 0.57f)),
                MakeMaterial("VehicleRed", new Color(0.48f, 0.16f, 0.12f))
            };

            // Park vehicles on the primary street centerlines, not on house plots.
            Vector3[] vehiclePoints =
            {
                new Vector3(-43f, 0f, -19f), new Vector3(53f, 0f, 29f),
                new Vector3(-67f, 0f, 53f),  new Vector3(77f, 0f, -67f),
                new Vector3(-19f, 0f, 53f),  new Vector3(5f, 0f, -67f),
                new Vector3(-67f, 0f, 29f),  new Vector3(77f, 0f, -19f)
            };

            for (int i = 0; i < vehiclePoints.Length; i++)
            {
                StartupCheckpoint.Set("CITY_VEHICLE_" + (i + 1) + "_START");
                CreateAndroidVehicle(vehiclePoints[i], vehicleMaterials[i % vehicleMaterials.Length], i % 2 == 0);
                StartupCheckpoint.Set("CITY_VEHICLE_" + (i + 1) + "_DONE");
            }
            StartupCheckpoint.Set("CITY_VEHICLES_DONE");
        }

        private void AddClearAndroidAlleySegments(
            List<Vector3> vertices,
            List<int> triangles,
            float fixedOffset,
            bool vertical,
            float half,
            float alleyWidth)
        {
            const float safetyMargin = 0.55f;
            List<Vector2> blockedIntervals = new List<Vector2>();

            for (int i = 0; i < androidBuildingCenters.Count; i++)
            {
                Vector3 center = androidBuildingCenters[i];
                Vector2 extents = androidBuildingHalfExtents[i];
                float perpendicularDistance = vertical
                    ? Mathf.Abs(center.x - fixedOffset)
                    : Mathf.Abs(center.z - fixedOffset);
                float perpendicularExtent = vertical ? extents.x : extents.y;
                if (perpendicularDistance >= perpendicularExtent + alleyWidth * 0.5f + safetyMargin)
                    continue;

                float alongCenter = vertical ? center.z : center.x;
                float alongExtent = vertical ? extents.y : extents.x;
                blockedIntervals.Add(new Vector2(
                    Mathf.Max(-half, alongCenter - alongExtent - safetyMargin),
                    Mathf.Min(half, alongCenter + alongExtent + safetyMargin)));
            }

            blockedIntervals.Sort((a, b) => a.x.CompareTo(b.x));
            float cursor = -half;
            for (int i = 0; i < blockedIntervals.Count; i++)
            {
                Vector2 interval = blockedIntervals[i];
                if (interval.x > cursor + 1f)
                {
                    AddAndroidTerrainFollowingQuad(
                        vertices,
                        triangles,
                        vertical
                            ? new Vector3(fixedOffset, 0f, (cursor + interval.x) * 0.5f)
                            : new Vector3((cursor + interval.x) * 0.5f, 0f, fixedOffset),
                        vertical
                            ? new Vector2(alleyWidth, interval.x - cursor)
                            : new Vector2(interval.x - cursor, alleyWidth),
                        0.035f);
                }

                cursor = Mathf.Max(cursor, interval.y);
                if (cursor >= half)
                    break;
            }

            if (cursor < half - 1f)
            {
                AddAndroidTerrainFollowingQuad(
                    vertices,
                    triangles,
                    vertical
                        ? new Vector3(fixedOffset, 0f, (cursor + half) * 0.5f)
                        : new Vector3((cursor + half) * 0.5f, 0f, fixedOffset),
                    vertical
                        ? new Vector2(alleyWidth, half - cursor)
                        : new Vector2(half - cursor, alleyWidth),
                    0.035f);
            }
        }

        private void CreateAndroidVehicle(Vector3 position, Material body, bool longAxisZ)
        {
            position.y = CalculateAndroidTerrainHeight(position.x, position.z);
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

            // Visual car pieces have no individual colliders; one solid body collider
            // makes the entire parked vehicle an obstacle for player movement.
            CreateAndroidCollider("CityVehicleCollider", position,
                new Vector3(width, 1.35f, length));
        }

        private void BuildAndroidIntersectionsAndLaneMarks(float roadWidth)
        {
            Material lane = MakeMaterial("AndroidLane", new Color(0.78f, 0.68f, 0.34f));
            Material curb = MakeMaterial("AndroidCurb", new Color(0.78f, 0.74f, 0.65f));
            float half = worldSize * 0.5f;

            List<Vector3> verticalVertices = new List<Vector3>(1024);
            List<int> verticalTriangles = new List<int>(1536);
            List<Vector3> horizontalVertices = new List<Vector3>(1024);
            List<int> horizontalTriangles = new List<int>(1536);
            List<Vector3> curbVertices = new List<Vector3>(256);
            List<int> curbTriangles = new List<int>(384);

            for (float x = -half + 5f; x < half; x += 10f)
            {
                for (float z = -half + roadWidth * 0.5f; z <= half; z += 24f)
                    AddAndroidTerrainFollowingQuad(verticalVertices, verticalTriangles,
                        new Vector3(x, 0f, z), new Vector2(0.28f, 4.2f), 0.075f);
            }

            for (float z = -half + 5f; z < half; z += 10f)
            {
                for (float x = -half + roadWidth * 0.5f; x <= half; x += 24f)
                    AddAndroidTerrainFollowingQuad(horizontalVertices, horizontalTriangles,
                        new Vector3(x, 0f, z), new Vector2(4.2f, 0.28f), 0.075f);
            }

            for (float roadCenter = -half + roadWidth * 0.5f; roadCenter <= half; roadCenter += 24f)
            {
                AddAndroidTerrainFollowingQuad(curbVertices, curbTriangles,
                    new Vector3(roadCenter - roadWidth * 0.5f, 0f, 0f),
                    new Vector2(0.08f, worldSize), 0.055f);
                AddAndroidTerrainFollowingQuad(curbVertices, curbTriangles,
                    new Vector3(roadCenter + roadWidth * 0.5f, 0f, 0f),
                    new Vector2(0.08f, worldSize), 0.055f);
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

        private void CreateAndroidBuilding(
            Vector3 position,
            float footprint,
            float height,
            float depth,
            int buildingIndex,
            bool isWarehouse,
            bool isPitchedRoof)
        {
            // Residential bodies are limited to one or two storeys. A pitched roof
            // adds roof height, not a third occupied floor.
            float bodyHeight = Mathf.Clamp(height, 3.2f, 6.5f);

            // Brighter, clean stylized-realism palette inspired by the free city
            // reference, while staying fully procedural and Android-light.
            Color[] palette =
            {
                new Color(0.76f, 0.67f, 0.54f),
                new Color(0.67f, 0.73f, 0.75f),
                new Color(0.80f, 0.71f, 0.57f),
                new Color(0.58f, 0.66f, 0.68f),
                new Color(0.63f, 0.70f, 0.58f)
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

            float doorWidth = isWarehouse
                ? Mathf.Min(3.4f, footprint * 0.32f)
                : Mathf.Min(1.35f, footprint * 0.18f);
            float doorHeight = isWarehouse ? 2.35f : 2.15f;
            QueueAndroidBuildingFacade(position, footprint, bodyHeight, depth, doorWidth, doorHeight, facade);

            // Split solid collision around the same visible doorway. Windows and all
            // other wall sections remain blocked by solid colliders.
            float collisionHeight = bodyHeight + (isPitchedRoof ? 1.6f : 0.65f);
            CreateAndroidBuildingColliders(
                position, footprint, depth, collisionHeight, doorWidth, doorHeight);

            if (isPitchedRoof)
            {
                CreateAndroidPitchedRoof(position, footprint, bodyHeight, depth, buildingIndex);
            }
            else
            {
                QueueAndroidBox(
                    "CityRoof",
                    position + Vector3.up * (bodyHeight + 0.22f),
                    new Vector3(footprint + 0.55f, 0.45f, depth + 0.55f),
                    trim);
            }

            int facadeBandCount = bodyHeight >= 5.0f && !isWarehouse ? 2 : 1;
            for (int row = 0; row < facadeBandCount; row++)
            {
                float y = row == 0 ? 0.30f : 3.15f;
                if (y >= bodyHeight - 0.15f) continue;
                QueueAndroidBox(
                    "FacadeBand",
                    position + new Vector3(0f, y, -depth * 0.515f),
                    new Vector3(footprint * 0.92f, 0.13f, 0.10f),
                    trim);
            }

            int columns = isWarehouse
                ? Mathf.Clamp(Mathf.FloorToInt(footprint / 2.6f), 3, 4)
                : Mathf.Clamp(Mathf.FloorToInt(footprint / 2.5f), 2, 4);
            float spacing = footprint / (columns + 1);
            int windowRows = isWarehouse ? 1 : (bodyHeight >= 5.0f ? 2 : 1);
            for (int row = 0; row < windowRows; row++)
            {
                float y = isWarehouse
                    ? bodyHeight * 0.54f
                    : (windowRows == 1 ? 1.65f : 1.65f + row * 2.65f);
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
                isWarehouse ? "WarehouseLoadingDoor" : "Door",
                position + new Vector3(0f, 1.12f, -depth * 0.54f),
                new Vector3(doorWidth, isWarehouse ? 2.35f : 2.15f, 0.14f),
                door);

            if (isWarehouse)
            {
                QueueAndroidBox(
                    "WarehouseFacadeSign",
                    position + new Vector3(0f, bodyHeight - 0.48f, -depth * 0.535f),
                    new Vector3(Mathf.Min(3.4f, footprint * 0.40f), 0.30f, 0.10f),
                    accentMaterial);
            }
            else
            {
                QueueAndroidBox(
                    "DoorCanopy",
                    position + new Vector3(0f, 2.30f, -depth * 0.56f),
                    new Vector3(Mathf.Min(2.4f, footprint * 0.30f), 0.18f, 0.72f),
                    trim);
            }

            // Flat roofs get small utility vents; the pitched roofs stay clean.
            if (!isPitchedRoof)
            {
                int rooftopUnits = isWarehouse ? 2 : 1;
                for (int i = 0; i < rooftopUnits; i++)
                {
                    float side = rooftopUnits == 1 ? 0f : (i == 0 ? -0.28f : 0.28f);
                    Vector3 offset = new Vector3(
                        side * footprint,
                        bodyHeight + 0.48f,
                        (i == 0 ? -0.20f : 0.22f) * depth);

                    QueueAndroidBox(
                        "RooftopUnit",
                        position + offset,
                        new Vector3(isWarehouse ? 1.25f : 0.90f, 0.38f, 0.75f),
                        rooftop);
                }
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

        private void CreateAndroidPitchedRoof(
            Vector3 position,
            float footprint,
            float bodyHeight,
            float depth,
            int buildingIndex)
        {
            // Two shallow, sloped box panels form a small gable. They use the cached
            // Android cube mesh and have no colliders, so the detail stays inexpensive.
            float span = footprint + 0.55f;
            float halfSpan = span * 0.5f;
            float rise = Mathf.Clamp(footprint * 0.095f, 0.82f, 1.18f);
            float slopedLength = Mathf.Sqrt(halfSpan * halfSpan + rise * rise);
            float angle = Mathf.Atan2(rise, halfSpan) * Mathf.Rad2Deg;
            float roofDepth = depth + 0.55f;

            GameObject leftPanel = CreateAndroidBox(
                "PitchedRoof_Left_" + buildingIndex,
                position + new Vector3(-span * 0.25f, bodyHeight + rise * 0.5f, 0f),
                new Vector3(slopedLength, 0.20f, roofDepth),
                roofMaterial,
                false);
            leftPanel.transform.rotation = Quaternion.Euler(0f, 0f, angle);

            GameObject rightPanel = CreateAndroidBox(
                "PitchedRoof_Right_" + buildingIndex,
                position + new Vector3(span * 0.25f, bodyHeight + rise * 0.5f, 0f),
                new Vector3(slopedLength, 0.20f, roofDepth),
                roofMaterial,
                false);
            rightPanel.transform.rotation = Quaternion.Euler(0f, 0f, -angle);
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
            position.y = CalculateAndroidTerrainHeight(position.x, position.z);
            if (androidTreeTrunkMaterial == null)
                androidTreeTrunkMaterial = MakeMaterial("AndroidTreeTrunk", new Color(0.25f, 0.16f, 0.09f));
            if (androidTreeCrownMaterial == null)
                androidTreeCrownMaterial = MakeMaterial("AndroidTreeCrown", new Color(0.17f, 0.40f, 0.14f));

            CreateAndroidBox(
                "TreeTrunk",
                position + Vector3.up * (scale * 0.8f),
                new Vector3(scale * 0.22f, scale * 1.6f, scale * 0.22f),
                androidTreeTrunkMaterial,
                true);

            CreateAndroidBox(
                "TreeCrownLower",
                position + Vector3.up * (scale * 1.75f),
                new Vector3(scale * 1.55f, scale * 0.92f, scale * 1.55f),
                androidTreeCrownMaterial,
                true);

            CreateAndroidBox(
                "TreeCrownUpper",
                position + Vector3.up * (scale * 2.30f),
                new Vector3(scale * 1.02f, scale * 0.78f, scale * 1.02f),
                androidTreeCrownMaterial,
                true);
        }

        private void CreateAndroidStreetLamp(Vector3 position)
        {
            position.y = CalculateAndroidTerrainHeight(position.x, position.z);
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
                androidLampGlowMaterial,                false);
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

        private void QueueAndroidBuildingFacade(
            Vector3 position,
            float footprint,
            float bodyHeight,
            float depth,
            float doorWidth,
            float doorHeight,
            Material facade)
        {
            float thickness = Mathf.Clamp(Mathf.Min(footprint, depth) * 0.08f, 0.35f, 0.60f);
            float frontZ = -depth * 0.5f + thickness * 0.5f;
            float backZ = depth * 0.5f - thickness * 0.5f;
            float sideWidth = Mathf.Max(0.35f, (footprint - doorWidth) * 0.5f);

            // Build a hollow wall shell so the doorway is a visible opening rather
            // than a collision gap hidden behind a solid facade cube.
            QueueAndroidBox("CityBuildingSideWall",
                position + new Vector3(-footprint * 0.5f + thickness * 0.5f, bodyHeight * 0.5f, 0f),
                new Vector3(thickness, bodyHeight, depth), facade);
            QueueAndroidBox("CityBuildingSideWall",
                position + new Vector3(footprint * 0.5f - thickness * 0.5f, bodyHeight * 0.5f, 0f),
                new Vector3(thickness, bodyHeight, depth), facade);
            QueueAndroidBox("CityBuildingRearWall",
                position + new Vector3(0f, bodyHeight * 0.5f, backZ),
                new Vector3(footprint, bodyHeight, thickness), facade);

            QueueAndroidBox("CityBuildingFrontWall",
                position + new Vector3(-doorWidth * 0.5f - sideWidth * 0.5f, bodyHeight * 0.5f, frontZ),
                new Vector3(sideWidth, bodyHeight, thickness), facade);
            QueueAndroidBox("CityBuildingFrontWall",
                position + new Vector3(doorWidth * 0.5f + sideWidth * 0.5f, bodyHeight * 0.5f, frontZ),
                new Vector3(sideWidth, bodyHeight, thickness), facade);

            float headerHeight = Mathf.Max(0.1f, bodyHeight - doorHeight);
            QueueAndroidBox("CityBuildingDoorHeader",
                position + new Vector3(0f, doorHeight + headerHeight * 0.5f, frontZ),
                new Vector3(doorWidth, headerHeight, thickness), facade);
        }

        private void CreateAndroidBuildingColliders(
            Vector3 position,
            float footprint,
            float depth,
            float totalHeight,
            float doorWidth,
            float doorHeight)
        {
            float thickness = Mathf.Clamp(Mathf.Min(footprint, depth) * 0.08f, 0.35f, 0.60f);
            float frontZ = -depth * 0.5f + thickness * 0.5f;
            float backZ = depth * 0.5f - thickness * 0.5f;
            float sideWidth = Mathf.Max(0.35f, (footprint - doorWidth) * 0.5f);

            // Side and rear walls stay fully solid.
            CreateAndroidCollider("CityBuildingSideCollider",
                position + Vector3.left * (footprint * 0.5f - thickness * 0.5f),
                new Vector3(thickness, totalHeight, depth));
            CreateAndroidCollider("CityBuildingSideCollider",
                position + Vector3.right * (footprint * 0.5f - thickness * 0.5f),
                new Vector3(thickness, totalHeight, depth));
            CreateAndroidCollider("CityBuildingRearCollider",
                position + Vector3.forward * backZ,
                new Vector3(footprint, totalHeight, thickness));

            // The two front sections leave a gap only as wide as the door.
            CreateAndroidCollider("CityBuildingFrontCollider",
                position + new Vector3(-(doorWidth * 0.5f + sideWidth * 0.5f), 0f, frontZ),
                new Vector3(sideWidth, totalHeight, thickness));
            CreateAndroidCollider("CityBuildingFrontCollider",
                position + new Vector3(doorWidth * 0.5f + sideWidth * 0.5f, 0f, frontZ),
                new Vector3(sideWidth, totalHeight, thickness));

            // The wall above the door is still solid.
            float headerHeight = Mathf.Max(0.1f, totalHeight - doorHeight);
            CreateAndroidCollider("CityBuildingDoorHeaderCollider",
                position + new Vector3(0f, doorHeight, frontZ),
                new Vector3(doorWidth, headerHeight, thickness));
        }

        private void CreateAndroidCollider(string objectName, Vector3 position, Vector3 size)
        {
            GameObject obj = new GameObject(objectName);
            Transform parent = androidCurrentBuildingRoot != null ? androidCurrentBuildingRoot : worldRoot;
            obj.transform.SetParent(parent, true);
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

                GameObject obj = new GameObject(
                    androidCurrentBuildingRoot != null ? "BuildingFacadeBatch_" + batch.Material.name : "AndroidCityBatch");
                Transform batchParent = androidCurrentBuildingRoot != null ? androidCurrentBuildingRoot : worldRoot;
                obj.transform.SetParent(batchParent, true);

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
            // Keep roof panels and other individual building details inside the same
            // house hierarchy as the facade batches. The occlusion fader discovers
            // the house root from CityBuildingCollider and fades all child renderers.
            Transform parent = androidCurrentBuildingRoot != null ? androidCurrentBuildingRoot : worldRoot;
            obj.transform.SetParent(parent, true);
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