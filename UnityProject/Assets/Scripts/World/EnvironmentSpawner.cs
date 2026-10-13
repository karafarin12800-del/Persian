using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    /// <summary>
    /// Builds environment props using the existing character scale as the visual reference.
    /// The city is kept inside the playable 220m world instead of scattering props outside it.
    /// </summary>
    public sealed class EnvironmentSpawner : MonoBehaviour
    {
        [SerializeField] private int buildings = 28;
        [SerializeField] private int trees = 44;
        [SerializeField] private float worldSize = 220.8f;

        // Scale reference: the current player character remains the baseline and is NOT resized.
        // Roads/alleys/buildings/trees are enlarged around that baseline.
        [SerializeField] private float mainRoadWidth = 14f;
        [SerializeField] private float alleyWidth = 7f;
        [SerializeField] private float buildingMinHeight = 12f;
        [SerializeField] private float buildingMaxHeight = 20f;
        [SerializeField] private float treeHeight = 9f;

        private void Start()
        {
            Random.InitState(20260930);
            SpawnBuildings();
            SpawnTrees();
        }

        private void SpawnBuildings()
        {
            int spawned = 0;
            int attempts = 0;

            while (spawned < buildings && attempts++ < buildings * 30)
            {
                Vector3 p = RandomPointInsideCity();
                if (IsOnRoadOrAlley(p, 2.5f))
                    continue;

                float width = Random.Range(12f, 19f);
                float depth = Random.Range(11f, 18f);
                float height = Random.Range(buildingMinHeight, buildingMaxHeight);                CreateDetailedBuilding(p, width, depth, height, spawned);
                spawned++;
            }
        }
        private void CreateDetailedBuilding(Vector3 p, float width, float depth, float height, int index)
        {
            var root = new GameObject("Building_" + index);
            root.transform.position = p;

            // Main mass: substantially larger than the player while preserving the player's scale.
            var body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "Facade";
            body.transform.SetParent(root.transform, false);
            body.transform.localPosition = new Vector3(0f, height * 0.5f, 0f);
            body.transform.localScale = new Vector3(width, height, depth);
            ApplyBuildingMaterial(body, index);

            // Roof/parapet gives the buildings a finished silhouette instead of a plain cube.
            var roof = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roof.name = "Roof";
            roof.transform.SetParent(root.transform, false);
            roof.transform.localPosition = new Vector3(0f, height + 0.35f, 0f);
            roof.transform.localScale = new Vector3(width * 1.04f, 0.7f, depth * 1.04f);
            ApplyBuildingMaterial(roof, index + 17);

            // Window bands on the front and side create readable 2.5D urban facades.
            int rows = Mathf.Clamp(Mathf.RoundToInt(height / 3.2f), 3, 7);
            int columns = Mathf.Clamp(Mathf.RoundToInt(width / 3.2f), 3, 6);
            float windowW = Mathf.Min(1.25f, (width * 0.72f) / columns);
            float windowH = 1.15f;

            for (int row = 0; row < rows; row++)
            {
                float y = 1.6f + row * ((height - 3.0f) / Mathf.Max(1, rows - 1));
                for (int col = 0; col < columns; col++)
                {
                    float x = Mathf.Lerp(-width * 0.36f, width * 0.36f, columns == 1 ? 0.5f : col / (float)(columns - 1));
                    CreateWindow(root.transform, new Vector3(x, y, -depth * 0.506f), new Vector3(windowW, windowH, 0.08f));
                }
            }

            int sideRows = Mathf.Clamp(Mathf.RoundToInt(depth / 3.4f), 3, 5);
            for (int row = 0; row < rows; row++)
            {
                float y = 1.6f + row * ((height - 3.0f) / Mathf.Max(1, rows - 1));
                for (int col = 0; col < sideRows; col++)
                {
                    float z = Mathf.Lerp(-depth * 0.32f, depth * 0.32f, sideRows == 1 ? 0.5f : col / (float)(sideRows - 1));
                    CreateWindow(root.transform, new Vector3(width * 0.506f, y, z), new Vector3(0.08f, windowH, Mathf.Min(1.15f, (depth * 0.62f) / sideRows)));
                }
            }

            // A few balconies break up the repeated window grid.
            int balconyCount = Mathf.Clamp(Mathf.FloorToInt(height / 5.5f), 1, 3);
            for (int i = 0; i < balconyCount; i++)
            {
                float y = 3.0f + i * 5.0f;
                var balcony = GameObject.CreatePrimitive(PrimitiveType.Cube);
                balcony.name = "Balcony";
                balcony.transform.SetParent(root.transform, false);
                balcony.transform.localPosition = new Vector3(-width * 0.18f, y, -depth * 0.55f);
                balcony.transform.localScale = new Vector3(width * 0.30f, 0.18f, depth * 0.20f);
                ApplyAccentMaterial(balcony);

                var rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rail.name = "BalconyRail";
                rail.transform.SetParent(root.transform, false);
                rail.transform.localPosition = new Vector3(-width * 0.18f, y + 0.48f, -depth * 0.63f);
                rail.transform.localScale = new Vector3(width * 0.30f, 0.75f, 0.06f);
                ApplyAccentMaterial(rail);
            }

            // Rooftop equipment adds depth when the camera looks down into the city.
            var unit = GameObject.CreatePrimitive(PrimitiveType.Cube);
            unit.name = "RoofUnit";
            unit.transform.SetParent(root.transform, false);
            unit.transform.localPosition = new Vector3(width * 0.20f, height + 0.9f, depth * 0.15f);
            unit.transform.localScale = new Vector3(Mathf.Min(2.2f, width * 0.16f), 0.9f, Mathf.Min(1.6f, depth * 0.14f));
            ApplyAccentMaterial(unit);
        }

        private static void CreateWindow(Transform parent, Vector3 localPosition, Vector3 localScale)
        {
            var window = GameObject.CreatePrimitive(PrimitiveType.Cube);
            window.name = "Window";
            window.transform.SetParent(parent, false);
            window.transform.localPosition = localPosition;
            window.transform.localScale = localScale;
            ApplyWindowMaterial(window);
        }

        private static void ApplyWindowMaterial(GameObject obj)
        {
            var renderer = obj.GetComponent<Renderer>();
            if (renderer == null) return;
            var shader = Shader.Find("Standard");
            if (shader == null) return;
            var material = new Material(shader) { color = new Color(0.10f, 0.20f, 0.24f), enableInstancing = true };
            renderer.sharedMaterial = material;
        }

        private static void ApplyBuildingMaterial(GameObject obj, int seed)
        {
            var renderer = obj.GetComponent<Renderer>();
            if (renderer == null) return;
            var shader = Shader.Find("Standard");
            if (shader == null) return;
            float t = Mathf.Abs(Mathf.Sin(seed * 12.9898f));
            Color baseColor = Color.Lerp(new Color(0.52f, 0.48f, 0.42f), new Color(0.30f, 0.34f, 0.36f), t);
            var material = new Material(shader) { color = baseColor, enableInstancing = true };
            renderer.sharedMaterial = material;
        }

        private static void ApplyAccentMaterial(GameObject obj)
        {
            var renderer = obj.GetComponent<Renderer>();
            if (renderer == null) return;
            var shader = Shader.Find("Standard");
            if (shader == null) return;
            var material = new Material(shader) { color = new Color(0.18f, 0.18f, 0.17f), enableInstancing = true };
            renderer.sharedMaterial = material;
        }


        private void SpawnTrees()
        {
            int spawned = 0;
            int attempts = 0;

            while (spawned < trees && attempts++ < trees * 30)
            {
                Vector3 p = RandomPointInsideCity();
                if (IsOnRoadOrAlley(p, 3.5f))
                    continue;

                float height = Random.Range(treeHeight * 0.85f, treeHeight * 1.15f);
                float trunkRadius = Mathf.Clamp(height * 0.075f, 0.55f, 0.8f);
                float crownDiameter = height * 0.62f;

                var trunk = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                trunk.name = "Tree_" + spawned;
                trunk.transform.position = new Vector3(p.x, height * 0.32f, p.z);
                trunk.transform.localScale = new Vector3(trunkRadius, height * 0.32f, trunkRadius);

                var crown = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                crown.name = "TreeCrown_" + spawned;
                crown.transform.position = new Vector3(p.x, height * 0.72f, p.z);
                crown.transform.localScale = new Vector3(crownDiameter, crownDiameter * 0.82f, crownDiameter);

                spawned++;
            }
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

            // Secondary lanes form wider, walkable city blocks instead of narrow corridors.
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
