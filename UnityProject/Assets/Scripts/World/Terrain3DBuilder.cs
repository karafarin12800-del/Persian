using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    /// <summary>
    /// Builds the playable ground as real 3D geometry instead of a single flat cube.
    /// Height is intentionally subtle so existing combat, roads and movement remain stable.
    /// The generated terrain is visual-only on Android; gameplay movement is kept at y=0
    /// and building colliders provide the actual collision surfaces.
    /// </summary>
    public sealed class Terrain3DBuilder : MonoBehaviour
    {
        [SerializeField] private float size = 220f;
        [SerializeField] private int subdivisions = 24;
        [SerializeField] private float height = 0.65f;
        [SerializeField] private float baseY = -0.35f;
        [SerializeField] private int seed = 32025;

        public void Build()
        {
            MeshFilter filter = GetComponent<MeshFilter>();
            if (filter == null) filter = gameObject.AddComponent<MeshFilter>();
            MeshRenderer renderer = GetComponent<MeshRenderer>();
            if (renderer == null) renderer = gameObject.AddComponent<MeshRenderer>();

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

            // Android startup must keep allocations conservative. The terrain mesh
            // itself supplies the shape; use a shared unlit material instead of generating
            // a procedural texture during the critical match transition.
            Material material = RuntimeMaterialFactory.Create(
                "Terrain3D",
                new Color(0.47f, 0.62f, 0.23f, 1f));

            if (material != null)
                renderer.sharedMaterial = material;
        }

        private Texture2D BuildGrassTexture(int resolution, int textureSeed)
        {
            Texture2D texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false);
            texture.name = "ProceduralGrassDetail";
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Bilinear;
            texture.anisoLevel = 4;

            for (int y = 0; y < resolution; y++)
            {
                for (int x = 0; x < resolution; x++)
                {
                    float nx = (float)x / resolution;
                    float ny = (float)y / resolution;

                    float broad = Mathf.PerlinNoise(nx * 5.2f + textureSeed * 0.013f, ny * 5.2f + textureSeed * 0.017f);
                    float fine = Mathf.PerlinNoise(nx * 19.0f + textureSeed * 0.007f, ny * 19.0f + textureSeed * 0.011f);
                    float speck = Mathf.PerlinNoise(nx * 43.0f + 7.3f, ny * 43.0f + 3.1f);

                    float variation = (broad - 0.5f) * 0.28f + (fine - 0.5f) * 0.12f + (speck - 0.5f) * 0.06f;
                    Color baseTone = new Color(0.47f, 0.62f, 0.23f, 1f);
                    Color tone = new Color(
                        Mathf.Clamp01(baseTone.r + variation),
                        Mathf.Clamp01(baseTone.g + variation * 0.75f),
                        Mathf.Clamp01(baseTone.b + variation * 0.35f),
                        1f);

                    texture.SetPixel(x, y, tone);
                }
            }

            texture.Apply(false, true);
            return texture;
        }
    }
}