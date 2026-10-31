using System.Collections.Generic;
using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    /// <summary>
    /// Tiny procedural low-poly mesh builder. Every shape is baked into ONE mesh with
    /// vertex colours, so a whole weapon or pickup is a single draw call and a single
    /// shared material. Vertex colour alpha selects the surface for PersiaWar/VertexLit:
    /// 0 = matte, 64 = plastic, 128 = metal (specular), 255 = emissive glow.
    /// Shapes are generated around a local origin; the +Z axis is the "long" axis.
    /// </summary>
    public sealed class MeshKit
    {
        public const byte Matte = 0;
        public const byte Plastic = 64;
        public const byte Metal = 128;
        public const byte Glow = 255;

        public static readonly Quaternion AlongZ = Quaternion.identity;
        public static readonly Quaternion AlongX = Quaternion.Euler(0f, 90f, 0f);
        public static readonly Quaternion AlongY = Quaternion.Euler(-90f, 0f, 0f);

        private readonly List<Vector3> vertices = new List<Vector3>(512);
        private readonly List<Vector3> normals = new List<Vector3>(512);
        private readonly List<Color32> colors = new List<Color32>(512);
        private readonly List<int> triangles = new List<int>(1024);

        public int VertexCount
        {
            get { return vertices.Count; }
        }

        public static Color32 C(float r, float g, float b, byte surface)
        {
            return new Color32(ToByte(r), ToByte(g), ToByte(b), surface);
        }

        private static byte ToByte(float value)
        {
            return (byte)Mathf.Clamp(Mathf.RoundToInt(value * 255f), 0, 255);
        }

        private static Vector3 P(Vector3 center, Quaternion rot, Vector3 local)
        {
            return center + rot * local;
        }

        // ------------------------------------------------------------------ primitives

        private void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 outward, Color32 color)
        {
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-12f) n = Vector3.Cross(c - a, d - a);
            if (n.sqrMagnitude < 1e-12f) return;
            if (Vector3.Dot(n, outward) < 0f)
            {
                Vector3 swap = b;
                b = d;
                d = swap;
                n = -n;
            }
            n.Normalize();
            int s = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            vertices.Add(d);
            for (int i = 0; i < 4; i++)
            {
                normals.Add(n);
                colors.Add(color);
            }
            triangles.Add(s);
            triangles.Add(s + 1);
            triangles.Add(s + 2);
            triangles.Add(s);
            triangles.Add(s + 2);
            triangles.Add(s + 3);
        }

        private void QuadSmooth(Vector3 a, Vector3 b, Vector3 c, Vector3 d,
            Vector3 na, Vector3 nb, Vector3 nc, Vector3 nd, Color32 color)
        {
            Vector3 face = Vector3.Cross(b - a, c - a);
            if (face.sqrMagnitude < 1e-12f) face = Vector3.Cross(c - a, d - a);
            if (face.sqrMagnitude < 1e-12f) return;
            bool flip = Vector3.Dot(face, na + nb + nc + nd) < 0f;
            int s = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            vertices.Add(d);
            normals.Add(na);
            normals.Add(nb);
            normals.Add(nc);
            normals.Add(nd);
            for (int i = 0; i < 4; i++) colors.Add(color);
            if (!flip)
            {
                triangles.Add(s);
                triangles.Add(s + 1);
                triangles.Add(s + 2);
                triangles.Add(s);
                triangles.Add(s + 2);
                triangles.Add(s + 3);
            }
            else
            {
                triangles.Add(s);
                triangles.Add(s + 2);
                triangles.Add(s + 1);
                triangles.Add(s);
                triangles.Add(s + 3);
                triangles.Add(s + 2);
            }
        }

        private void Tri(Vector3 a, Vector3 b, Vector3 c, Vector3 outward, Color32 color)
        {
            Vector3 n = Vector3.Cross(b - a, c - a);
            if (n.sqrMagnitude < 1e-12f) return;
            if (Vector3.Dot(n, outward) < 0f)
            {
                Vector3 swap = b;
                b = c;
                c = swap;
                n = -n;
            }
            n.Normalize();
            int s = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            normals.Add(n);
            normals.Add(n);
            normals.Add(n);
            colors.Add(color);
            colors.Add(color);
            colors.Add(color);
            triangles.Add(s);
            triangles.Add(s + 1);
            triangles.Add(s + 2);
        }

        // ------------------------------------------------------------------ public shapes

        public void AddBox(Vector3 center, Vector3 size, Color32 color)
        {
            AddTaper(center, new Vector2(size.x, size.y), new Vector2(size.x, size.y), size.z, color,
                Quaternion.identity);
        }

        public void AddBox(Vector3 center, Vector3 size, Color32 color, Quaternion rot)
        {
            AddTaper(center, new Vector2(size.x, size.y), new Vector2(size.x, size.y), size.z, color, rot);
        }

        /// <summary>
        /// Box that narrows (or widens) along local Z. nearSize sits at -Z, farSize at +Z.
        /// Used for stocks, hand-guards, blades and wedges.
        /// </summary>
        public void AddTaper(Vector3 center, Vector2 nearSize, Vector2 farSize, float length,
            Color32 color, Quaternion rot)
        {
            float hz = length * 0.5f;
            float nx = nearSize.x * 0.5f;
            float ny = nearSize.y * 0.5f;
            float fx = farSize.x * 0.5f;
            float fy = farSize.y * 0.5f;

            Vector3 n0 = P(center, rot, new Vector3(-nx, -ny, -hz));
            Vector3 n1 = P(center, rot, new Vector3(nx, -ny, -hz));
            Vector3 n2 = P(center, rot, new Vector3(nx, ny, -hz));
            Vector3 n3 = P(center, rot, new Vector3(-nx, ny, -hz));
            Vector3 f0 = P(center, rot, new Vector3(-fx, -fy, hz));
            Vector3 f1 = P(center, rot, new Vector3(fx, -fy, hz));
            Vector3 f2 = P(center, rot, new Vector3(fx, fy, hz));
            Vector3 f3 = P(center, rot, new Vector3(-fx, fy, hz));

            Quad(n0, n1, n2, n3, rot * new Vector3(0f, 0f, -1f), color);
            Quad(f0, f1, f2, f3, rot * new Vector3(0f, 0f, 1f), color);
            Quad(n0, n1, f1, f0, rot * new Vector3(0f, -1f, 0f), color);
            Quad(n3, n2, f2, f3, rot * new Vector3(0f, 1f, 0f), color);
            Quad(n0, n3, f3, f0, rot * new Vector3(-1f, 0f, 0f), color);
            Quad(n1, n2, f2, f1, rot * new Vector3(1f, 0f, 0f), color);
        }

        /// <summary>Straight box between two points (arms, struts, blades).</summary>
        public void AddBeam(Vector3 a, Vector3 b, float thicknessX, float thicknessY, Color32 color)
        {
            Vector3 d = b - a;
            float len = d.magnitude;
            if (len < 0.0001f) return;
            Vector3 dir = d / len;
            Vector3 up = Mathf.Abs(Vector3.Dot(dir, Vector3.up)) > 0.98f ? Vector3.forward : Vector3.up;
            Quaternion rot = Quaternion.LookRotation(dir, up);
            AddBox((a + b) * 0.5f, new Vector3(thicknessX, thicknessY, len), color, rot);
        }

        public void AddCylinder(Vector3 center, float radius, float length, int segments,
            Color32 color, Quaternion rot)
        {
            AddCylinder(center, radius, radius, length, segments, color, rot, true, true);
        }

        /// <summary>
        /// Cylinder / cone frustum along local Z. radiusNear is at -Z, radiusFar at +Z.
        /// Use MeshKit.AlongX / AlongY to lay it along another axis.
        /// </summary>
        public void AddCylinder(Vector3 center, float radiusNear, float radiusFar, float length,
            int segments, Color32 color, Quaternion rot, bool capNear, bool capFar)
        {
            segments = Mathf.Max(3, segments);
            float hz = length * 0.5f;
            float slope = (radiusNear - radiusFar) / Mathf.Max(length, 0.0001f);

            for (int i = 0; i < segments; i++)
            {
                float a0 = 2f * Mathf.PI * i / segments;
                float a1 = 2f * Mathf.PI * (i + 1) / segments;
                float c0 = Mathf.Cos(a0);
                float s0 = Mathf.Sin(a0);
                float c1 = Mathf.Cos(a1);
                float s1 = Mathf.Sin(a1);

                Vector3 n0 = P(center, rot, new Vector3(c0 * radiusNear, s0 * radiusNear, -hz));
                Vector3 n1 = P(center, rot, new Vector3(c1 * radiusNear, s1 * radiusNear, -hz));
                Vector3 f1 = P(center, rot, new Vector3(c1 * radiusFar, s1 * radiusFar, hz));
                Vector3 f0 = P(center, rot, new Vector3(c0 * radiusFar, s0 * radiusFar, hz));
                Vector3 na = (rot * new Vector3(c0, s0, slope)).normalized;
                Vector3 nb = (rot * new Vector3(c1, s1, slope)).normalized;
                QuadSmooth(n0, n1, f1, f0, na, nb, nb, na, color);

                if (capNear && radiusNear > 0.0001f)
                {
                    Tri(P(center, rot, new Vector3(0f, 0f, -hz)), n0, n1,
                        rot * new Vector3(0f, 0f, -1f), color);
                }
                if (capFar && radiusFar > 0.0001f)
                {
                    Tri(P(center, rot, new Vector3(0f, 0f, hz)), f0, f1,
                        rot * new Vector3(0f, 0f, 1f), color);
                }
            }
        }

        public void AddSphere(Vector3 center, float radius, int segments, int rings, Color32 color)
        {
            AddEllipsoid(center, new Vector3(radius, radius, radius), segments, rings, color,
                Quaternion.identity);
        }

        public void AddEllipsoid(Vector3 center, Vector3 radii, int segments, int rings,
            Color32 color, Quaternion rot)
        {
            segments = Mathf.Max(4, segments);
            rings = Mathf.Max(3, rings);
            for (int r = 0; r < rings; r++)
            {
                float v0 = Mathf.PI * r / rings;
                float v1 = Mathf.PI * (r + 1) / rings;
                for (int s = 0; s < segments; s++)
                {
                    float u0 = 2f * Mathf.PI * s / segments;
                    float u1 = 2f * Mathf.PI * (s + 1) / segments;
                    Vector3 d00 = SphereDir(u0, v0);
                    Vector3 d10 = SphereDir(u1, v0);
                    Vector3 d11 = SphereDir(u1, v1);
                    Vector3 d01 = SphereDir(u0, v1);
                    QuadSmooth(
                        EllipsoidPoint(center, radii, rot, d00),
                        EllipsoidPoint(center, radii, rot, d10),
                        EllipsoidPoint(center, radii, rot, d11),
                        EllipsoidPoint(center, radii, rot, d01),
                        EllipsoidNormal(radii, rot, d00),
                        EllipsoidNormal(radii, rot, d10),
                        EllipsoidNormal(radii, rot, d11),
                        EllipsoidNormal(radii, rot, d01),
                        color);
                }
            }
        }

        private static Vector3 SphereDir(float u, float v)
        {
            float sv = Mathf.Sin(v);
            return new Vector3(sv * Mathf.Cos(u), Mathf.Cos(v), sv * Mathf.Sin(u));
        }

        private static Vector3 EllipsoidPoint(Vector3 center, Vector3 radii, Quaternion rot, Vector3 dir)
        {
            return center + rot * new Vector3(dir.x * radii.x, dir.y * radii.y, dir.z * radii.z);
        }

        private static Vector3 EllipsoidNormal(Vector3 radii, Quaternion rot, Vector3 dir)
        {
            Vector3 n = new Vector3(dir.x / Mathf.Max(radii.x, 0.0001f),
                dir.y / Mathf.Max(radii.y, 0.0001f),
                dir.z / Mathf.Max(radii.z, 0.0001f));
            return (rot * n).normalized;
        }

        /// <summary>Flat ring facing +Y (ground rings, glow halos).</summary>
        public void AddRing(Vector3 center, float innerRadius, float outerRadius, int segments, Color32 color)
        {
            segments = Mathf.Max(6, segments);
            for (int i = 0; i < segments; i++)
            {
                float a0 = 2f * Mathf.PI * i / segments;
                float a1 = 2f * Mathf.PI * (i + 1) / segments;
                float c0 = Mathf.Cos(a0);
                float s0 = Mathf.Sin(a0);
                float c1 = Mathf.Cos(a1);
                float s1 = Mathf.Sin(a1);
                Vector3 i0 = center + new Vector3(c0 * innerRadius, 0f, s0 * innerRadius);
                Vector3 o0 = center + new Vector3(c0 * outerRadius, 0f, s0 * outerRadius);
                Vector3 o1 = center + new Vector3(c1 * outerRadius, 0f, s1 * outerRadius);
                Vector3 i1 = center + new Vector3(c1 * innerRadius, 0f, s1 * innerRadius);
                Quad(i0, o0, o1, i1, Vector3.up, color);
            }
        }

        // ------------------------------------------------------------------ output

        public Mesh Build(string name)
        {
            Mesh mesh = new Mesh();
            mesh.name = name;
            if (vertices.Count > 60000)
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
