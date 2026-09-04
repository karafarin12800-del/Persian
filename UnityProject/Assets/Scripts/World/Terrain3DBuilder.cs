using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    /// <summary>
    /// Builds the playable ground as real 3D geometry instead of a single flat cube.
    /// Height is intentionally subtle so existing combat, roads and movement remain stable.
    /// </summary>
    public sealed class Terrain3DBuilder : MonoBehaviour
    {
        [SerializeField] private float size = 192f;
        [SerializeField] private int subdivisions = 32;
        [SerializeField] private float height = 0.65f;
        [SerializeField] private float baseY = -0.35f;
        [SerializeField] private int seed = 32025;

        public void Build()
        {
            MeshFilter filter = GetComponent<MeshFilter>();
            if (filter == null) filter = gameObject.AddComponent<MeshFilter>();
            MeshRenderer renderer = GetComponent<MeshRenderer>();
            if (renderer == null) renderer = gameObject.AddComponent<MeshRenderer>();
            MeshCollider collider = GetComponent<MeshCollider>();
            if (collider == null) collider = gameObject.AddComponent<MeshCollider>();

            Mesh mesh = new Mesh { name = "PersiaWar3DTerrain" };
            int count = subdivisions + 1;
            Vector3[] vertices = new Vector3[count * count];
            Vector2[] uv = new Vector2[vertices.Length];
            int[] triangles = new int[subdivisions * subdivisions * 6];
            Random.InitState(seed);

            for (int z = 0; z < count; z++)
            {
                for (int x = 0; x < count; x++)
                {
                    float nx = (float)x / subdivisions;
                    float nz = (float)z / subdivisions;
                    float worldX = (nx - 0.5f) * size;
                    float worldZ = (nz - 0.5f) * size;

                    float broad = Mathf.PerlinNoise((worldX + seed) * 0.035f, (worldZ + seed) * 0.035f);
                    float detail = Mathf.PerlinNoise((worldX - seed) * 0.09f, (worldZ + seed) * 0.09f);
                    float edge = Mathf.Clamp01(1f - Mathf.Max(Mathf.Abs(nx - 0.5f), Mathf.Abs(nz - 0.5f)) * 2f);
                    float y = baseY + ((broad - 0.5f) * 0.32f + (detail - 0.5f) * 0.10f) * height * edge;

                    vertices[z * count + x] = new Vector3(worldX, y, worldZ);
                    uv[z * count + x] = new Vector2(nx, nz);
                }
            }

            int t = 0;
            for (int z = 0; z < subdivisions; z++)
            {
                for (int x = 0; x < subdivisions; x++)
                {
                    int i = z * count + x;
                    triangles[t++] = i;
                    triangles[t++] = i + count;
                    triangles[t++] = i + 1;
                    triangles[t++] = i + 1;
                    triangles[t++] = i + count;
                    triangles[t++] = i + count + 1;
                }
            }

            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            filter.sharedMesh = mesh;
            collider.sharedMesh = null;
            collider.sharedMesh = mesh;

            Material material = RuntimeMaterialFactory.Create("Terrain3D", new Color(0.28f, 0.56f, 0.16f));
            if (material != null) renderer.sharedMaterial = material;
        }
    }
}
