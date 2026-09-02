using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    /// <summary>
    /// Builds a lightweight runtime 3D terrain mesh for the battle world.
    /// Gameplay systems continue to use the existing X/Z world coordinates;
    /// this component only supplies the ground surface and collision.
    /// </summary>
    public sealed class Terrain3DBuilder : MonoBehaviour
    {
        [SerializeField] private float worldSize = 192f;
        [SerializeField] private int gridResolution = 32;
        [SerializeField] private float baseHeight = -0.55f;
        [SerializeField] private float heightAmplitude = 0.28f;
        [SerializeField] private float noiseScale = 0.055f;

        public void Build()
        {
            MeshFilter filter = GetComponent<MeshFilter>();
            if (filter == null) filter = gameObject.AddComponent<MeshFilter>();

            MeshRenderer renderer = GetComponent<MeshRenderer>();
            if (renderer == null) renderer = gameObject.AddComponent<MeshRenderer>();

            MeshCollider collider = GetComponent<MeshCollider>();
            if (collider == null) collider = gameObject.AddComponent<MeshCollider>();

            Mesh mesh = BuildMesh();
            filter.sharedMesh = mesh;
            collider.sharedMesh = null;
            collider.sharedMesh = mesh;

            if (renderer.sharedMaterial == null)
            {
                renderer.sharedMaterial = RuntimeMaterialFactory.Create(
                    "Terrain3D",
                    new Color(0.33f, 0.68f, 0.18f));
            }
        }

        private Mesh BuildMesh()
        {
            int resolution = Mathf.Clamp(gridResolution, 8, 64);
            int verticesPerSide = resolution + 1;
            int vertexCount = verticesPerSide * verticesPerSide;
            int[] triangles = new int[resolution * resolution * 6];
            Vector3[] vertices = new Vector3[vertexCount];
            Vector2[] uv = new Vector2[vertexCount];

            float half = worldSize * 0.5f;
            float step = worldSize / resolution;

            for (int z = 0; z < verticesPerSide; z++)
            {
                for (int x = 0; x < verticesPerSide; x++)
                {
                    int index = z * verticesPerSide + x;
                    float worldX = -half + x * step;
                    float worldZ = -half + z * step;

                    // Keep the height variation subtle so existing roads,
                    // buildings and combat coordinates remain stable.
                    float coarse = Mathf.PerlinNoise(
                        (worldX + 1000f) * noiseScale,
                        (worldZ + 1000f) * noiseScale);
                    float fine = Mathf.PerlinNoise(
                        (worldX - 350f) * noiseScale * 2.1f,
                        (worldZ - 350f) * noiseScale * 2.1f);
                    float normalized = (coarse * 0.7f + fine * 0.3f) - 0.5f;
                    float edgeFade = Mathf.Clamp01(Mathf.Min(
                        (worldX + half) / 12f,
                        (half - worldX) / 12f,
                        (worldZ + half) / 12f,
                        (half - worldZ) / 12f));
                    float height = baseHeight + normalized * heightAmplitude * edgeFade;

                    vertices[index] = new Vector3(worldX, height, worldZ);
                    uv[index] = new Vector2((float)x / resolution, (float)z / resolution);
                }
            }

            int triangleIndex = 0;
            for (int z = 0; z < resolution; z++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    int a = z * verticesPerSide + x;
                    int b = a + 1;
                    int c = a + verticesPerSide;
                    int d = c + 1;

                    triangles[triangleIndex++] = a;
                    triangles[triangleIndex++] = c;
                    triangles[triangleIndex++] = b;
                    triangles[triangleIndex++] = b;
                    triangles[triangleIndex++] = c;
                    triangles[triangleIndex++] = d;
                }
            }

            Mesh mesh = new Mesh
            {
                name = "BattleTerrain3D",
                vertices = vertices,
                triangles = triangles,
                uv = uv
            };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
