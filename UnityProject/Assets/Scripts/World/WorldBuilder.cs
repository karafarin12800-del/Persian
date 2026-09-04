using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public sealed class WorldBuilder : MonoBehaviour
    {
        [SerializeField] private float worldSize = 1056f;
        [SerializeField] private float roadWidth = 8f;
        [SerializeField] private int roadCount = 5;
        [SerializeField] private float roadCoverage = 0.72f;
        [SerializeField] private Material groundMaterial;
        [SerializeField] private Material roadMaterial;

        private void Start()
        {
            BuildGround();
            BuildRoads();
        }

        private void BuildGround()
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "World_Ground";
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = Vector3.one * (worldSize / 10f);
            ApplyMaterial(ground, groundMaterial, new Color(0.32f, 0.34f, 0.28f));
        }

        private void BuildRoads()
        {
            float spacing = worldSize / (roadCount + 1);
            float length = worldSize * roadCoverage;
            for (int i = 1; i <= roadCount; i++)
            {
                float p = -worldSize * 0.5f + spacing * i;
                CreateRoad(new Vector3(p, 0.012f, 0f), new Vector3(roadWidth, 0.02f, length), "Road_Vertical");
                CreateRoad(new Vector3(0f, 0.013f, p), new Vector3(length, 0.02f, roadWidth), "Road_Horizontal");
            }
        }

        private void CreateRoad(Vector3 position, Vector3 scale, string name)
        {
            var road = GameObject.CreatePrimitive(PrimitiveType.Cube);
            road.name = name;
            road.transform.position = position;
            road.transform.localScale = scale;
            ApplyMaterial(road, roadMaterial, new Color(0.12f, 0.12f, 0.11f));
        }

        private static void ApplyMaterial(GameObject obj, Material source, Color fallback)
        {
            Renderer renderer = obj.GetComponent<Renderer>();
            if (renderer == null) return;
            if (source != null)
            {
                renderer.sharedMaterial = source;
                return;
            }
            Shader shader = Shader.Find("Standard");
            if (shader != null)
            {
                Material material = new Material(shader) { color = fallback, enableInstancing = true };
                renderer.sharedMaterial = material;
            }
        }
    }
}
