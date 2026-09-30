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
        [SerializeField] private float worldSize = 220f;

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
                float height = Random.Range(buildingMinHeight, buildingMaxHeight);

                var b = GameObject.CreatePrimitive(PrimitiveType.Cube);
                b.name = "Building_" + spawned;
                b.transform.position = new Vector3(p.x, height * 0.5f, p.z);
                b.transform.localScale = new Vector3(width, height, depth);
                spawned++;
            }
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
