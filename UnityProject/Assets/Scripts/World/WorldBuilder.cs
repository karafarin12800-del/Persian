using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public sealed class WorldBuilder : MonoBehaviour
    {
        [SerializeField] private float worldSize = 220.8f;

        // These values are deliberately based on the existing character scale.
        // Character scale stays unchanged; the environment is enlarged to match it.
        [SerializeField] private float mainRoadWidth = 14f;
        [SerializeField] private float alleyWidth = 7f;
        [SerializeField] private int mainRoadCount = 5;
        [SerializeField] private int alleyCountPerAxis = 11;
        [SerializeField] private float roadCoverage = 0.90f;
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
            ApplyMaterial(ground, groundMaterial, new Color(0.12f, 0.46f, 0.08f));
        }

        private void BuildRoads()
        {
            float mainSpacing = worldSize / (mainRoadCount + 1);
            float mainLength = worldSize * roadCoverage;

            for (int i = 1; i <= mainRoadCount; i++)
            {
                float p = -worldSize * 0.5f + mainSpacing * i;

                CreateRoad(
                    new Vector3(p, 0.012f, 0f),
                    new Vector3(mainRoadWidth, 0.024f, mainLength),
                    "MainRoad_Vertical_" + i);

                CreateRoad(
                    new Vector3(0f, 0.013f, p),
                    new Vector3(mainLength, 0.024f, mainRoadWidth),
                    "MainRoad_Horizontal_" + i);
            }

            // 11 lanes each direction => 22 secondary streets, producing the requested
            // dense urban block structure while keeping enough width for the character.
            float alleySpacing = worldSize / (alleyCountPerAxis + 1);
            float alleyLength = worldSize * 0.86f;

            for (int i = 1; i <= alleyCountPerAxis; i++)
            {
                float p = -worldSize * 0.5f + alleySpacing * i;

                CreateRoad(
                    new Vector3(p, 0.015f, 0f),
                    new Vector3(alleyWidth, 0.026f, alleyLength),
                    "Alley_Vertical_" + i);

                CreateRoad(
                    new Vector3(0f, 0.016f, p),
                    new Vector3(alleyLength, 0.026f, alleyWidth),
                    "Alley_Horizontal_" + i);
            }
        }

        private void CreateRoad(Vector3 position, Vector3 scale, string name)
        {
            var road = GameObject.CreatePrimitive(PrimitiveType.Cube);
            road.name = name;
            road.transform.position = position;
            road.transform.localScale = scale;
            ApplyMaterial(road, roadMaterial, new Color(0.52f, 0.54f, 0.57f));
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

            renderer.sharedMaterial = RuntimeMaterialFactory.Create(
                "WorldBuilderFallback_" + obj.name, fallback);
        }
    }
}
