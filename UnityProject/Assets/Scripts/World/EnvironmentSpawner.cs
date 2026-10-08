using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    /// <summary>
    /// Lightweight, deterministic city dressing for the 2.5D prototype with Persian-inspired facade accents and street dressing.
    /// Gameplay-scale buildings keep their solid body colliders; decorative details
    /// share materials and do not create extra colliders on Android.
    /// </summary>
    public sealed class EnvironmentSpawner : MonoBehaviour
    {
        [SerializeField] private int buildings = 28;
        [SerializeField] private int trees = 44;
        [SerializeField] private float worldSize = 220f;
        [SerializeField] private float mainRoadWidth = 14f;
        [SerializeField] private float alleyWidth = 7f;
        [SerializeField] private float buildingMinHeight = 12f;
        [SerializeField] private float buildingMaxHeight = 20f;
        [SerializeField] private float treeHeight = 9f;

        private static readonly Color[] FacadePalette =
        {
            new Color(0.69f, 0.58f, 0.43f),
            new Color(0.76f, 0.68f, 0.54f),
            new Color(0.55f, 0.53f, 0.47f),
            new Color(0.48f, 0.51f, 0.51f),
            new Color(0.67f, 0.63f, 0.54f),
            new Color(0.58f, 0.46f, 0.37f)
        };

        private static readonly Color[] RoofPalette =
        {
            new Color(0.30f, 0.31f, 0.30f),
            new Color(0.39f, 0.34f, 0.29f),
            new Color(0.43f, 0.39f, 0.33f)
        };

        private static Material[] facadeMaterials;
        private static Material[] roofMaterials;
        private static Material windowMaterial;
        private static Material accentMaterial;
        private static Material doorMaterial;
        private static Material trunkMaterial;
        private static Material foliageMaterial;
        private static Material tileAccentMaterial;
        private static Material stoneMaterial;
        private static Material metalMaterial;

        private void Start()
        {
            EnsureMaterials();
            Random.InitState(20260930);
            SpawnBuildings();
            SpawnTrees();
            SpawnStreetProps();
            SpawnPlazas();
        }

        private static void EnsureMaterials()
        {
            if (facadeMaterials != null) return;

            facadeMaterials = new Material[FacadePalette.Length];
            for (int i = 0; i < facadeMaterials.Length; i++)
                facadeMaterials[i] = CreateSharedMaterial("CityFacade_" + i, FacadePalette[i]);

            roofMaterials = new Material[RoofPalette.Length];
            for (int i = 0; i < roofMaterials.Length; i++)
                roofMaterials[i] = CreateSharedMaterial("CityRoof_" + i, RoofPalette[i]);

            windowMaterial = CreateSharedMaterial("CityWindow", new Color(0.09f, 0.20f, 0.25f));
            accentMaterial = CreateSharedMaterial("CityTrim", new Color(0.30f, 0.25f, 0.20f));
            doorMaterial = CreateSharedMaterial("CityDoor", new Color(0.25f, 0.15f, 0.09f));
            trunkMaterial = CreateSharedMaterial("CityTreeTrunk", new Color(0.30f, 0.20f, 0.12f));
            foliageMaterial = CreateSharedMaterial("CityTreeFoliage", new Color(0.22f, 0.37f, 0.18f));
            tileAccentMaterial = CreateSharedMaterial("CityTileAccent", new Color(0.10f, 0.33f, 0.38f));
            stoneMaterial = CreateSharedMaterial("CityStone", new Color(0.43f, 0.39f, 0.33f));
            metalMaterial = CreateSharedMaterial("CityLampMetal", new Color(0.15f, 0.17f, 0.16f));
        }

        private static Material CreateSharedMaterial(string materialName, Color color)
        {
            Shader shader = Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) shader = Shader.Find("Unlit/Color");
            if (shader == null) return null;

            var material = new Material(shader);
            material.name = materialName;
            material.color = color;
            material.enableInstancing = true;
            return material;
        }

        private void SpawnBuildings()
        {
            int spawned = 0;
            int attempts = 0;

            while (spawned < buildings && attempts++ < buildings * 30)
            {
                Vector3 p = RandomPointInsideCity();
                if (IsOnRoadOrAlley(p, 4f)) continue;

                float width = Random.Range(12f, 19f);
                float depth = Random.Range(11f, 18f);
                float height = Random.Range(buildingMinHeight, buildingMaxHeight);
                CreateDetailedBuilding(p, width, depth, height, spawned);
                spawned++;
            }
        }

        private void CreateDetailedBuilding(Vector3 p, float width, float depth, float height, int index)
        {
            var root = new GameObject("Building_" + index);
            root.transform.position = p;

            Material facade = facadeMaterials[index % facadeMaterials.Length];
            Material roofMaterial = roofMaterials[index % roofMaterials.Length];

            // Keep one simple solid collider per building for reliable movement and combat.
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Facade";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
            body.transform.localScale = new Vector3(width, height, depth);
            SetMaterial(body, facade);

            // Roof cap, foundation band and corner pilasters add readable silhouette detail.
            CreateDetail(root.transform, "Roof", new Vector3(0f, height + 0.30f, 0f),
                new Vector3(width * 1.04f, 0.60f, depth * 1.04f), roofMaterial);
            CreateDetail(root.transform, "FoundationBand", new Vector3(0f, 0.42f, -depth * 0.505f),
                new Vector3(width * 0.98f, 0.55f, 0.12f), accentMaterial);
            CreateDetail(root.transform, "LeftCornerTrim", new Vector3(-width * 0.46f, height * 0.5f, -depth * 0.507f),
                new Vector3(0.32f, height * 0.96f, 0.12f), roofMaterial);
            CreateDetail(root.transform, "RightCornerTrim", new Vector3(width * 0.46f, height * 0.5f, -depth * 0.507f),
                new Vector3(0.32f, height * 0.96f, 0.12f), roofMaterial);

            // Entrance door on the camera-facing facade.
            CreateDetail(root.transform, "EntranceDoor", new Vector3(0f, 1.45f, -depth * 0.512f),
                new Vector3(1.65f, 2.8f, 0.10f), doorMaterial);
            CreateDetail(root.transform, "DoorCanopy", new Vector3(0f, 2.95f, -depth * 0.55f),
                new Vector3(2.25f, 0.22f, 0.75f), accentMaterial);
            CreateDetail(root.transform, "DoorStep", new Vector3(0f, 0.12f, -depth * 0.55f),
                new Vector3(2.0f, 0.24f, 0.55f), roofMaterial);

            int rows = Mathf.Clamp(Mathf.RoundToInt(height / 3.2f), 3, 7);
            int columns = Mathf.Clamp(Mathf.RoundToInt(width / 3.2f), 3, 6);
            float windowW = Mathf.Min(1.25f, (width * 0.72f) / columns);
            const float windowH = 1.15f;

            for (int row = 0; row < rows; row++)
            {
                float y = 1.6f + row * ((height - 3.0f) / Mathf.Max(1, rows - 1));
                for (int col = 0; col < columns; col++)
                {
                    float x = Mathf.Lerp(-width * 0.36f, width * 0.36f,
                        columns == 1 ? 0.5f : col / (float)(columns - 1));
                    // Leave the central ground-floor opening clear for the entrance.
                    if (row == 0 && Mathf.Abs(x) < 1.5f) continue;

                    CreateDetail(root.transform, "Window",
                        new Vector3(x, y, -depth * 0.512f),
                        new Vector3(windowW, windowH, 0.08f), windowMaterial);
                    CreateWindowFrame(root.transform, x, y, depth, windowW, windowH);
                }
            }

            int sideColumns = Mathf.Clamp(Mathf.RoundToInt(depth / 3.4f), 3, 5);
            for (int row = 0; row < rows; row++)
            {
                float y = 1.6f + row * ((height - 3.0f) / Mathf.Max(1, rows - 1));
                for (int col = 0; col < sideColumns; col++)
                {
                    float z = Mathf.Lerp(-depth * 0.32f, depth * 0.32f,
                        sideColumns == 1 ? 0.5f : col / (float)(sideColumns - 1));
                    CreateDetail(root.transform, "SideWindow",
                        new Vector3(width * 0.512f, y, z),
                        new Vector3(0.08f, windowH, Mathf.Min(1.15f, (depth * 0.62f) / sideColumns)),
                        windowMaterial);
                }
            }

            // Small balconies on selected buildings prevent every facade from looking identical.
            if (index % 3 != 1)
            {
                int balconyCount = Mathf.Clamp(Mathf.FloorToInt(height / 6.5f), 1, 3);
                for (int i = 0; i < balconyCount; i++)
                {
                    float y = 3.0f + i * 5.0f;
                    float x = (i % 2 == 0 ? -1f : 1f) * width * 0.18f;
                    CreateDetail(root.transform, "Balcony",
                        new Vector3(x, y, -depth * 0.56f),
                        new Vector3(width * 0.28f, 0.18f, depth * 0.18f), accentMaterial);
                    CreateDetail(root.transform, "BalconyRail",
                        new Vector3(x, y + 0.42f, -depth * 0.65f),
                        new Vector3(width * 0.28f, 0.62f, 0.07f), roofMaterial);
                }
            }

            // Rooftop equipment helps the city read from the elevated camera.
            CreateDetail(root.transform, "RoofUnit",
                new Vector3(width * 0.20f, height + 0.82f, depth * 0.15f),
                new Vector3(Mathf.Min(2.2f, width * 0.16f), 0.8f, Mathf.Min(1.6f, depth * 0.14f)),
                accentMaterial);

            // Persian-inspired trim and pilasters add a stronger silhouette without adding colliders.
            if (index % 2 == 0)
            {
                CreateDetail(root.transform, "LeftPilaster",
                    new Vector3(-width * 0.38f, height * 0.5f, -depth * 0.53f),
                    new Vector3(0.42f, height * 0.88f, 0.16f), stoneMaterial);
                CreateDetail(root.transform, "RightPilaster",
                    new Vector3(width * 0.38f, height * 0.5f, -depth * 0.53f),
                    new Vector3(0.42f, height * 0.88f, 0.16f), stoneMaterial);
                CreateDetail(root.transform, "TileBand",
                    new Vector3(0f, Mathf.Min(height - 1.2f, 3.8f), -depth * 0.525f),
                    new Vector3(width * 0.78f, 0.34f, 0.14f), tileAccentMaterial);
            }

            if (index % 4 == 0)
            {
                CreateDetail(root.transform, "ParapetFront",
                    new Vector3(0f, height + 0.72f, -depth * 0.36f),
                    new Vector3(width * 0.76f, 0.65f, 0.18f), stoneMaterial);
            }
        }

        private static void CreateWindowFrame(Transform parent, float x, float y, float depth, float width, float height)
        {
            const float frame = 0.10f;
            float z = -depth * 0.518f;
            CreateDetail(parent, "WindowSill", new Vector3(x, y - height * 0.5f, z),
                new Vector3(width + 0.18f, frame, 0.12f), accentMaterial);
            CreateDetail(parent, "WindowLintel", new Vector3(x, y + height * 0.5f, z),
                new Vector3(width + 0.18f, frame, 0.12f), accentMaterial);
        }

        private static GameObject CreateDetail(Transform parent, string objectName,
            Vector3 localPosition, Vector3 localScale, Material material)
        {
            var obj = GameObject.CreatePrimitive(PrimitiveType.Cube);
            obj.name = objectName;
            obj.transform.SetParent(parent, false);
            obj.transform.localPosition = localPosition;
            obj.transform.localScale = localScale;

            Collider collider = obj.GetComponent<Collider>();
            if (collider != null) Object.Destroy(collider);

            SetMaterial(obj, material);
            return obj;
        }

        private static void SetMaterial(GameObject obj, Material material)
        {
            Renderer renderer = obj.GetComponent<Renderer>();
            if (renderer != null && material != null) renderer.sharedMaterial = material;
        }

        private void SpawnTrees()
        {
            for (int spawned = 0, attempts = 0; spawned < trees && attempts < trees * 30; attempts++)
            {
                Vector3 p = RandomPointInsideCity();
                if (IsOnRoadOrAlley(p, 3.5f)) continue;

                float height = Random.Range(treeHeight * 0.85f, treeHeight * 1.15f);
                float trunkRadius = Mathf.Clamp(height * 0.075f, 0.55f, 0.8f);
                float crownDiameter = height * 0.62f;

                var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                trunk.name = "Tree_" + spawned;
                trunk.transform.position = new Vector3(p.x, height * 0.32f, p.z);
                trunk.transform.localScale = new Vector3(trunkRadius, height * 0.32f, trunkRadius);
                SetMaterial(trunk, trunkMaterial);

                var crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                crown.name = "TreeCrown_" + spawned;
                crown.transform.position = new Vector3(p.x, height * 0.72f, p.z);
                crown.transform.localScale = new Vector3(crownDiameter, crownDiameter * 0.82f, crownDiameter);
                SetMaterial(crown, foliageMaterial);

                spawned++;
            }
        }

        private void SpawnStreetProps()
        {
            for (int spawned = 0, attempts = 0; spawned < 34 && attempts < 34 * 40; attempts++)
            {
                Vector3 p = RandomPointInsideCity();
                if (!IsOnRoadOrAlley(p, 1.5f)) continue;

                float roadOffset = (spawned % 2 == 0 ? -1f : 1f) * Random.Range(3.0f, 4.8f);
                bool placeOnXRoad = Mathf.Abs(p.x) > Mathf.Abs(p.z);
                Vector3 propPos = placeOnXRoad
                    ? new Vector3(p.x, 0f, p.z + roadOffset)
                    : new Vector3(p.x + roadOffset, 0f, p.z);

                CreateStreetLamp(propPos, spawned);
                spawned++;
            }
        }

        private void CreateStreetLamp(Vector3 position, int index)
        {
            var root = new GameObject("StreetLamp_" + index);
            root.transform.position = position;

            var pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            pole.name = "Pole";
            pole.transform.SetParent(root.transform, false);
            pole.transform.localPosition = new Vector3(0f, 2.4f, 0f);
            pole.transform.localScale = new Vector3(0.16f, 2.4f, 0.16f);
            SetMaterial(pole, metalMaterial);
            RemoveCollider(pole);

            var arm = CreateDetail(root.transform, "Arm", new Vector3(0.34f, 4.55f, 0f),
                new Vector3(0.68f, 0.14f, 0.14f), metalMaterial);
            var lamp = CreateDetail(root.transform, "Lamp",
                new Vector3(0.66f, 4.35f, 0f), new Vector3(0.38f, 0.24f, 0.38f),
                tileAccentMaterial);

            if (index % 3 == 0)
                CreateDetail(root.transform, "Base", new Vector3(0f, 0.12f, 0f),
                    new Vector3(0.6f, 0.24f, 0.6f), stoneMaterial);
        }

        private void SpawnPlazas()
        {
            Vector3[] positions =
            {
                new Vector3(-42f, 0.02f, 46f),
                new Vector3(48f, 0.02f, -40f)
            };

            for (int i = 0; i < positions.Length; i++)
            {
                var plaza = new GameObject("Plaza_" + i);
                plaza.transform.position = positions[i];

                CreateDetail(plaza.transform, "Paving",
                    Vector3.zero, new Vector3(24f, 0.18f, 24f), stoneMaterial);

                CreateDetail(plaza.transform, "Inset",
                    new Vector3(0f, 0.11f, 0f), new Vector3(15f, 0.10f, 15f),
                    tileAccentMaterial);

                CreatePlazaMarker(plaza.transform, -8f, -8f);
                CreatePlazaMarker(plaza.transform, 8f, -8f);
                CreatePlazaMarker(plaza.transform, -8f, 8f);
                CreatePlazaMarker(plaza.transform, 8f, 8f);
            }
        }

        private static void CreatePlazaMarker(Transform parent, float x, float z)
        {
            CreateDetail(parent, "Marker", new Vector3(x, 0.22f, z),
                new Vector3(1.0f, 0.25f, 1.0f), stoneMaterial);
            CreateDetail(parent, "Accent", new Vector3(x, 0.36f, z),
                new Vector3(0.52f, 0.10f, 0.52f), tileAccentMaterial);
        }

        private static void RemoveCollider(GameObject obj)
        {
            Collider collider = obj.GetComponent<Collider>();
            if (collider != null) Object.Destroy(collider);
        }

        private Vector3 RandomPointInsideCity()
        {
            float half = worldSize * 0.5f - 8f;
            return new Vector3(Random.Range(-half, half), 0f, Random.Range(-half, half));
        }

        private bool IsOnRoadOrAlley(Vector3 p, float padding)
        {
            float spacing = worldSize / 6f;
            for (int i = 1; i <= 5; i++)
            {
                float main = -worldSize * 0.5f + spacing * i;
                if (Mathf.Abs(p.x - main) < mainRoadWidth * 0.5f + padding ||
                    Mathf.Abs(p.z - main) < mainRoadWidth * 0.5f + padding)
                    return true;
            }

            float alleySpacing = worldSize / 12f;
            for (int i = 1; i < 12; i++)
            {
                float lane = -worldSize * 0.5f + alleySpacing * i;
                if (Mathf.Abs(p.x - lane) < alleyWidth * 0.5f + padding ||
                    Mathf.Abs(p.z - lane) < alleyWidth * 0.5f + padding)
                    return true;
            }

            return false;
        }
    }
}