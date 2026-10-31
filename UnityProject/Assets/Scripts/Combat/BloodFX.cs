using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public enum BloodStyle
    {
        Red = 0,
        AlienGoo = 1,
        Scorch = 2
    }

    /// <summary>
    /// Persistent blood on the ground + flying droplets, for humans, aliens and hovercraft.
    /// All ground decals live in ONE dynamic mesh (a ring buffer of quads), so any amount
    /// of blood costs a single draw call. Droplets use one shared ParticleSystem and also
    /// leave a splat where they land. Capacity follows the graphics tier.
    /// </summary>
    public sealed class BloodFX : MonoBehaviour
    {
        private const int Capacity = 96;
        private const int AtlasCell = 64;

        private static BloodFX instance;
        private static readonly RaycastHit[] groundHits = new RaycastHit[10];

        private Mesh mesh;
        private Material decalMaterial;
        private Material dropMaterial;
        private Texture2D atlasTexture;
        private Texture2D dropTexture;
        private ParticleSystem drops;

        private readonly Vector3[] verts = new Vector3[Capacity * 4];
        private readonly Vector2[] uvs = new Vector2[Capacity * 4];
        private readonly Color32[] cols = new Color32[Capacity * 4];
        private readonly int[] tris = new int[Capacity * 6];

        private readonly bool[] active = new bool[Capacity];
        private readonly bool[] shown = new bool[Capacity];
        private readonly Vector3[] centers = new Vector3[Capacity];
        private readonly float[] rotations = new float[Capacity];
        private readonly float[] startTimes = new float[Capacity];
        private readonly float[] lifeTimes = new float[Capacity];
        private readonly float[] sizeFrom = new float[Capacity];
        private readonly float[] sizeTo = new float[Capacity];
        private readonly float[] growTimes = new float[Capacity];
        private readonly Color32[] baseColors = new Color32[Capacity];
        private int cursor;
        private bool dirty;

        // ------------------------------------------------------------------ public API

        public static BloodStyle StyleFor(string objectName)
        {
            if (string.IsNullOrEmpty(objectName)) return BloodStyle.Red;
            string n = objectName.ToLowerInvariant();
            if (n.Contains("craft") || n.Contains("hover") || n.Contains("drone")) return BloodStyle.Scorch;
            if (n.Contains("alien") || n.StartsWith("s3_") || n.StartsWith("s4_")) return BloodStyle.AlienGoo;
            return BloodStyle.Red;
        }

        /// <summary>Small spurt + ground spatter for a non-lethal hit. direction = way the shot travelled.</summary>
        public static void Hit(Vector3 worldPosition, Vector3 direction, float damage, BloodStyle style)
        {
            BloodFX fx = Ensure();
            if (fx != null) fx.DoHit(worldPosition, direction, damage, style);
        }

        /// <summary>Big burst + growing pool + splatter trail for a kill.</summary>
        public static void Death(Vector3 worldPosition, Vector3 direction, float force, BloodStyle style)
        {
            BloodFX fx = Ensure();
            if (fx != null) fx.DoDeath(worldPosition, direction, force, style);
        }

        public static void ClearAll()
        {
            if (instance != null) instance.ClearInternal();
        }

        // ------------------------------------------------------------------ setup

        private static BloodFX Ensure()
        {
            if (instance != null) return instance;
            GameObject go = new GameObject("PW_BloodFX");
            go.hideFlags = HideFlags.HideInHierarchy;
            DontDestroyOnLoad(go);
            instance = go.AddComponent<BloodFX>();
            instance.Build();
            return instance;
        }

        private void OnEnable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private void OnDisable()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnDestroy()
        {
            if (instance == this) instance = null;
        }

        private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene,
            UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            ClearInternal();
        }

        private void Build()
        {
            Shader decalShader = Resources.Load<Shader>("PersiaWarDecal");
            if (decalShader == null) decalShader = Shader.Find("PersiaWar/Decal");
            if (decalShader == null) decalShader = Shader.Find("Sprites/Default");

            atlasTexture = BuildAtlas();
            dropTexture = BuildDropTexture();

            decalMaterial = new Material(decalShader);
            decalMaterial.name = "PW_BloodDecal";
            decalMaterial.mainTexture = atlasTexture;
            decalMaterial.hideFlags = HideFlags.DontUnloadUnusedAsset;
            dropMaterial = new Material(decalShader);
            dropMaterial.name = "PW_BloodDrop";
            dropMaterial.mainTexture = dropTexture;
            dropMaterial.hideFlags = HideFlags.DontUnloadUnusedAsset;

            for (int i = 0; i < Capacity; i++)
            {
                int v = i * 4;
                int t = i * 6;
                tris[t] = v;
                tris[t + 1] = v + 1;
                tris[t + 2] = v + 2;
                tris[t + 3] = v;
                tris[t + 4] = v + 2;
                tris[t + 5] = v + 3;
            }

            mesh = new Mesh();
            mesh.name = "PW_BloodDecals";
            mesh.MarkDynamic();
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.colors32 = cols;
            mesh.triangles = tris;
            mesh.bounds = new Bounds(Vector3.zero, new Vector3(4000f, 200f, 4000f));

            GameObject decalObject = new GameObject("PW_BloodDecalMesh");
            decalObject.transform.SetParent(transform, false);
            MeshFilter filter = decalObject.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = decalObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = decalMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            GameObject dropObject = new GameObject("PW_BloodDrops");
            dropObject.transform.SetParent(transform, false);
            drops = dropObject.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = drops.main;
            main.loop = true;
            main.duration = 1f;
            main.playOnAwake = false;
            main.maxParticles = 220;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 2.4f;
            main.startLifetime = 0.6f;
            main.startSpeed = 0f;
            main.startSize = 0.08f;
            ParticleSystem.EmissionModule emission = drops.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = drops.shape;
            shape.enabled = false;
            ParticleSystemRenderer dropRenderer = dropObject.GetComponent<ParticleSystemRenderer>();
            dropRenderer.renderMode = ParticleSystemRenderMode.Stretch;
            dropRenderer.velocityScale = 0.05f;
            dropRenderer.lengthScale = 1.2f;
            dropRenderer.sharedMaterial = dropMaterial;
            drops.Play();

            for (int i = 0; i < Capacity; i++) Deactivate(i);
            dirty = true;
        }

        // ------------------------------------------------------------------ effects

        private int Scale()
        {
            return (int)GraphicsQualityProfile.CurrentTier;   // 0 low, 1 medium, 2 high
        }

        private int ActiveCapacity()
        {
            int tier = Scale();
            return tier <= 0 ? 32 : (tier == 1 ? 64 : Capacity);
        }

        private static byte Clamp8(float value)
        {
            return (byte)Mathf.Clamp(Mathf.RoundToInt(value), 0, 255);
        }

        private static Color32 ColorFor(BloodStyle style, float shade)
        {
            switch (style)
            {
                case BloodStyle.AlienGoo:
                    return new Color32(Clamp8(70f * shade), Clamp8(235f * shade), Clamp8(90f * shade), 235);
                case BloodStyle.Scorch:
                    return new Color32(Clamp8(18f * shade), Clamp8(16f * shade), Clamp8(15f * shade), 215);
                default:
                    return new Color32(Clamp8(150f * shade), Clamp8(8f * shade), Clamp8(12f * shade), 235);
            }
        }

        private static Vector3 Flat(Vector3 direction)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
            {
                float a = Random.Range(0f, 6.2831853f);
                return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            }
            return direction.normalized;
        }

        private void DoHit(Vector3 position, Vector3 direction, float damage, BloodStyle style)
        {
            Vector3 dir = Flat(direction);
            int tier = Scale();
            int count = Mathf.Clamp(Mathf.RoundToInt(4f + damage * 0.12f), 4, 12);
            if (tier <= 0) count = Mathf.Max(2, count / 2);
            float groundY = GroundY(position);
            EmitDrops(position, dir, count, style, 1f, groundY, tier > 0);

            int splats = tier <= 0 ? 1 : 2;
            for (int i = 0; i < splats; i++)
            {
                Vector3 side = new Vector3(-dir.z, 0f, dir.x);
                Vector3 p = new Vector3(position.x, groundY, position.z) +
                    dir * Random.Range(0.35f, 1.1f) + side * Random.Range(-0.25f, 0.25f);
                Splat(p, Random.Range(0.28f, 0.55f), Random.Range(0f, 6.28f), Random.Range(0, 4),
                    ColorFor(style, Random.Range(0.75f, 1f)), 42f, Random.Range(0.05f, 0.2f), 1f, 0f);
            }
        }

        private void DoDeath(Vector3 position, Vector3 direction, float force, BloodStyle style)
        {
            Vector3 dir = Flat(direction);
            int tier = Scale();
            float groundY = GroundY(position);
            Vector3 ground = new Vector3(position.x, groundY, position.z);
            Vector3 side = new Vector3(-dir.z, 0f, dir.x);

            int dropCount = tier <= 0 ? 10 : (tier == 1 ? 18 : 26);
            EmitDrops(position, dir, dropCount, style, 1.3f + force * 0.08f, groundY, tier > 0);

            // main pool grows under the body for ~1.6 s
            Splat(ground + dir * 0.25f, 0f, Random.Range(0f, 6.28f), Random.Range(0, 4),
                ColorFor(style, 0.85f), 60f, 0.15f, 0.30f, 1.6f, Random.Range(1.5f, 2.3f));
            if (tier > 0)
            {
                Splat(ground + dir * 0.9f + side * Random.Range(-0.3f, 0.3f), 0f, Random.Range(0f, 6.28f),
                    Random.Range(0, 4), ColorFor(style, 0.8f), 55f, 0.4f, 0.25f, 1.2f, Random.Range(0.8f, 1.3f));
            }

            // drag trail while the body slides
            int trail = tier <= 0 ? 2 : 5;
            for (int i = 0; i < trail; i++)
            {
                Vector3 p = ground + dir * (0.4f + i * 0.35f) + side * Random.Range(-0.12f, 0.12f);
                Splat(p, Random.Range(0.25f, 0.45f), Random.Range(0f, 6.28f), Random.Range(0, 4),
                    ColorFor(style, Random.Range(0.7f, 0.95f)), 48f, 0.25f + i * 0.08f, 1f, 0f);
            }

            // random spatter around the corpse
            int spatter = tier <= 0 ? 3 : (tier == 1 ? 6 : 9);
            for (int i = 0; i < spatter; i++)
            {
                Vector2 ring = Random.insideUnitCircle * 0.9f;
                Vector3 p = ground + dir * Random.Range(0.2f, 1.5f) + new Vector3(ring.x, 0f, ring.y);
                Splat(p, Random.Range(0.22f, 0.70f), Random.Range(0f, 6.28f), Random.Range(0, 4),
                    ColorFor(style, Random.Range(0.7f, 1f)), 45f, Random.Range(0.1f, 0.5f), 1f, 0f);
            }
        }

        private void EmitDrops(Vector3 position, Vector3 dir, int count, BloodStyle style,
            float speedScale, float groundY, bool landingSplats)
        {
            if (drops == null) return;
            ParticleSystem.EmitParams emit = new ParticleSystem.EmitParams();
            const float gravity = 9.81f * 2.4f;
            float height = Mathf.Max(0.2f, position.y - groundY);
            for (int i = 0; i < count; i++)
            {
                Vector3 scatter = new Vector3(Random.Range(-0.45f, 0.45f), Random.Range(0.1f, 0.8f),
                    Random.Range(-0.45f, 0.45f));
                Vector3 velocity = (dir * Random.Range(1.5f, 4.2f) + scatter * 2.5f) * speedScale;
                float landing = (velocity.y + Mathf.Sqrt(velocity.y * velocity.y + 2f * gravity * height)) / gravity;
                landing = Mathf.Clamp(landing, 0.12f, 0.9f);

                Color32 color = style == BloodStyle.Scorch
                    ? new Color32(255, Clamp8(Random.Range(140f, 200f)), 60, 255)
                    : ColorFor(style, Random.Range(0.8f, 1.1f));
                emit.position = position;
                emit.velocity = velocity;
                emit.startLifetime = landing + 0.02f;
                emit.startSize = Random.Range(0.05f, 0.12f);
                emit.startColor = color;
                drops.Emit(emit, 1);

                if (landingSplats && style != BloodStyle.Scorch && Random.value < 0.45f)
                {
                    Vector3 landingPoint = new Vector3(position.x + velocity.x * landing, groundY,
                        position.z + velocity.z * landing);
                    Splat(landingPoint, Random.Range(0.14f, 0.32f), Random.Range(0f, 6.28f), Random.Range(0, 4),
                        ColorFor(style, Random.Range(0.7f, 1f)), 40f, landing, 1f, 0f);
                }
            }

            if (style == BloodStyle.Scorch)
            {
                Splat(new Vector3(position.x, groundY, position.z), 0.7f, Random.Range(0f, 6.28f),
                    Random.Range(0, 4), ColorFor(style, 1f), 35f, 0.1f, 1f, 0f);
            }
        }

        // ------------------------------------------------------------------ decal ring buffer

        private void Splat(Vector3 center, float size, float rotation, int variant, Color32 color,
            float life, float delay, float startScale, float growTime)
        {
            Splat(center, size, rotation, variant, color, life, delay, startScale, growTime, size);
        }

        private void Splat(Vector3 center, float size, float rotation, int variant, Color32 color,
            float life, float delay, float startScale, float growTime, float finalSize)
        {
            if (mesh == null) return;
            int capacity = ActiveCapacity();
            int slot = cursor % capacity;
            cursor = (cursor + 1) % capacity;

            active[slot] = true;
            shown[slot] = false;
            centers[slot] = new Vector3(center.x, center.y + 0.025f, center.z);
            rotations[slot] = rotation;
            startTimes[slot] = Time.time + delay;
            lifeTimes[slot] = life;
            float target = growTime > 0f ? finalSize : size;
            sizeTo[slot] = target;
            sizeFrom[slot] = growTime > 0f ? target * startScale : target;
            growTimes[slot] = growTime;
            baseColors[slot] = color;

            int cellX = variant & 1;
            int cellY = (variant >> 1) & 1;
            float u0 = cellX * 0.5f + 0.004f;
            float v0 = cellY * 0.5f + 0.004f;
            float u1 = u0 + 0.492f;
            float v1 = v0 + 0.492f;
            int v = slot * 4;
            uvs[v] = new Vector2(u0, v0);
            uvs[v + 1] = new Vector2(u1, v0);
            uvs[v + 2] = new Vector2(u1, v1);
            uvs[v + 3] = new Vector2(u0, v1);

            HideQuad(slot);
            dirty = true;
        }

        private void HideQuad(int slot)
        {
            int v = slot * 4;
            Vector3 p = centers[slot];
            for (int i = 0; i < 4; i++)
            {
                verts[v + i] = p;
                Color32 c = cols[v + i];
                c.a = 0;
                cols[v + i] = c;
            }
        }

        private void Deactivate(int slot)
        {
            active[slot] = false;
            shown[slot] = false;
            centers[slot] = Vector3.zero;
            HideQuad(slot);
        }

        private void WriteQuad(int slot, float size, float alpha)
        {
            float h = size * 0.5f;
            float c = Mathf.Cos(rotations[slot]);
            float s = Mathf.Sin(rotations[slot]);
            Vector3 center = centers[slot];
            int v = slot * 4;

            verts[v] = center + new Vector3(-h * c + h * s, 0f, -h * s - h * c);
            verts[v + 1] = center + new Vector3(h * c + h * s, 0f, h * s - h * c);
            verts[v + 2] = center + new Vector3(h * c - h * s, 0f, h * s + h * c);
            verts[v + 3] = center + new Vector3(-h * c - h * s, 0f, -h * s + h * c);

            Color32 color = baseColors[slot];
            color.a = (byte)Mathf.Clamp(Mathf.RoundToInt(color.a * alpha), 0, 255);
            cols[v] = color;
            cols[v + 1] = color;
            cols[v + 2] = color;
            cols[v + 3] = color;
        }

        private void LateUpdate()
        {
            if (mesh == null) return;
            float now = Time.time;
            bool changed = dirty;
            dirty = false;

            for (int i = 0; i < Capacity; i++)
            {
                if (!active[i]) continue;
                float age = now - startTimes[i];
                if (age < 0f) continue;

                float life = lifeTimes[i];
                if (age >= life)
                {
                    Deactivate(i);
                    changed = true;
                    continue;
                }

                float grow = growTimes[i];
                bool growing = grow > 0f && age < grow;
                float fadeStart = life * 0.8f;
                bool fading = age > fadeStart;
                if (!shown[i] || growing || fading)
                {
                    float size = sizeTo[i];
                    if (growing)
                    {
                        float k = Mathf.Clamp01(age / grow);
                        k = k * k * (3f - 2f * k);
                        size = Mathf.Lerp(sizeFrom[i], sizeTo[i], k);
                    }
                    float alpha = fading ? Mathf.Clamp01(1f - (age - fadeStart) / Mathf.Max(0.01f, life - fadeStart)) : 1f;
                    WriteQuad(i, size, alpha);
                    shown[i] = true;
                    changed = true;
                }
            }

            if (changed)
            {
                mesh.vertices = verts;
                mesh.uv = uvs;
                mesh.colors32 = cols;
            }
        }

        private void ClearInternal()
        {
            for (int i = 0; i < Capacity; i++) Deactivate(i);
            cursor = 0;
            dirty = true;
            if (drops != null) drops.Clear();
        }

        // ------------------------------------------------------------------ ground

        private static float GroundY(Vector3 position)
        {
            int count = Physics.RaycastNonAlloc(new Ray(position + Vector3.up * 1.2f, Vector3.down),
                groundHits, 8f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            float y = 0.02f;
            for (int i = 0; i < count; i++)
            {
                Collider c = groundHits[i].collider;
                if (c == null || groundHits[i].normal.y < 0.5f) continue;
                if (c.GetComponentInParent<TargetHealth>() != null) continue;
                if (c.GetComponentInParent<PlayerController>() != null) continue;
                if (groundHits[i].distance < best)
                {
                    best = groundHits[i].distance;
                    y = groundHits[i].point.y;
                }
            }
            return y;
        }

        // ------------------------------------------------------------------ procedural textures

        private static Texture2D BuildAtlas()
        {
            int size = AtlasCell * 2;
            Color32[] pixels = new Color32[size * size];
            for (int variant = 0; variant < 4; variant++)
                FillSplat(pixels, size, (variant & 1) * AtlasCell, ((variant >> 1) & 1) * AtlasCell, variant);
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "PW_BloodAtlas";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            texture.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return texture;
        }

        private static void FillSplat(Color32[] pixels, int stride, int ox, int oy, int variant)
        {
            System.Random rng = new System.Random(7919 * (variant + 3));
            float p1 = (float)rng.NextDouble() * 6.28f;
            float p2 = (float)rng.NextDouble() * 6.28f;
            float p3 = (float)rng.NextDouble() * 6.28f;
            int dropletCount = 6 + variant;
            float[] dx = new float[dropletCount];
            float[] dy = new float[dropletCount];
            float[] dr = new float[dropletCount];
            for (int i = 0; i < dropletCount; i++)
            {
                float a = (float)rng.NextDouble() * 6.28f;
                float d = 0.62f + (float)rng.NextDouble() * 0.28f;
                dx[i] = Mathf.Cos(a) * d;
                dy[i] = Mathf.Sin(a) * d;
                dr[i] = 0.04f + (float)rng.NextDouble() * 0.05f;
            }
            float streakAngle = (float)rng.NextDouble() * 3.14f;
            float streakCos = Mathf.Cos(streakAngle);
            float streakSin = Mathf.Sin(streakAngle);
            bool streak = variant >= 2;

            for (int y = 0; y < AtlasCell; y++)
            {
                for (int x = 0; x < AtlasCell; x++)
                {
                    float u = (x + 0.5f) / AtlasCell * 2f - 1f;
                    float v = (y + 0.5f) / AtlasCell * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    float ang = Mathf.Atan2(v, u);
                    float edge = 0.50f + 0.14f * Mathf.Sin(3f * ang + p1) + 0.10f * Mathf.Sin(5f * ang + p2)
                        + 0.07f * Mathf.Sin(8f * ang + p3);
                    float alpha = Mathf.Clamp01((edge - r) / 0.10f);

                    for (int i = 0; i < dropletCount; i++)
                    {
                        float ddx = u - dx[i];
                        float ddy = v - dy[i];
                        float dd = Mathf.Sqrt(ddx * ddx + ddy * ddy) / dr[i];
                        alpha = Mathf.Max(alpha, Mathf.Clamp01((1f - dd) / 0.35f));
                    }
                    if (streak)
                    {
                        float sx = (u - 0.1f) * streakCos + v * streakSin;
                        float sy = -(u - 0.1f) * streakSin + v * streakCos;
                        float sd = Mathf.Sqrt((sx / 0.85f) * (sx / 0.85f) + (sy / 0.20f) * (sy / 0.20f));
                        alpha = Mathf.Max(alpha, Mathf.Clamp01((1f - sd) / 0.30f) * 0.9f);
                    }

                    float shade = 0.78f + 0.22f * Mathf.Clamp01(r / Mathf.Max(0.2f, edge));
                    byte grey = (byte)Mathf.Clamp(Mathf.RoundToInt(255f * shade), 0, 255);
                    pixels[(oy + y) * stride + ox + x] = new Color32(grey, grey, grey,
                        (byte)Mathf.Clamp(Mathf.RoundToInt(alpha * 255f), 0, 255));
                }
            }
        }

        private static Texture2D BuildDropTexture()
        {
            const int size = 32;
            Color32[] pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size * 2f - 1f;
                    float v = (y + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v);
                    byte a = (byte)Mathf.Clamp(Mathf.RoundToInt(Mathf.Clamp01((1f - r) / 0.35f) * 255f), 0, 255);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.name = "PW_BloodDrop";
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.SetPixels32(pixels);
            texture.Apply(false, false);
            texture.hideFlags = HideFlags.DontUnloadUnusedAsset;
            return texture;
        }
    }
}
