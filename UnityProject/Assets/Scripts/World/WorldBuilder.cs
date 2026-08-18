using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public sealed class WorldBuilder : MonoBehaviour
    {
        [SerializeField] private float worldSize = 1056f;
        [SerializeField] private float roadWidth = 8f;
        [SerializeField] private float roadLength = 180f;
        [SerializeField] private int roadCount = 5;

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
        }

        private void BuildRoads()
        {
            float spacing = worldSize / (roadCount + 1);
            for (int i = 1; i <= roadCount; i++)
            {
                float p = -worldSize * 0.5f + spacing * i;
                CreateRoad(new Vector3(p, 0.012f, 0f), new Vector3(roadWidth, 0.02f, worldSize * 0.72f));
                CreateRoad(new Vector3(0f, 0.013f, p), new Vector3(worldSize * 0.72f, 0.02f, roadWidth));
            }
        }

        private static void CreateRoad(Vector3 position, Vector3 scale)
        {
            var road = GameObject.CreatePrimitive(PrimitiveType.Cube);
            road.name = "Road_Block";
            road.transform.position = position;
            road.transform.localScale = scale;
        }
    }
}
