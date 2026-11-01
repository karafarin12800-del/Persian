using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    /// <summary>
    /// Stage 1-only, presentation-focused Persian architectural layer.
    /// All static scenery is baked into one vertex-coloured mesh and deliberately
    /// has no colliders, so the established combat and rescue spaces remain traversable.
    /// </summary>
    public static class StageOneWorldArt
    {
        private static readonly Color32 Limestone = MeshKit.C(0.72f, 0.61f, 0.46f, MeshKit.Matte);
        private static readonly Color32 Ivory = MeshKit.C(0.91f, 0.81f, 0.65f, MeshKit.Plastic);
        private static readonly Color32 Turquoise = MeshKit.C(0.08f, 0.43f, 0.43f, MeshKit.Metal);
        private static readonly Color32 TurquoiseLight = MeshKit.C(0.20f, 0.65f, 0.59f, MeshKit.Plastic);
        private static readonly Color32 DeepBlue = MeshKit.C(0.06f, 0.20f, 0.34f, MeshKit.Metal);
        private static readonly Color32 Brass = MeshKit.C(0.82f, 0.58f, 0.20f, MeshKit.Metal);
        private static readonly Color32 Terracotta = MeshKit.C(0.55f, 0.20f, 0.12f, MeshKit.Matte);
        private static readonly Color32 CypressDark = MeshKit.C(0.10f, 0.25f, 0.15f, MeshKit.Matte);
        private static readonly Color32 CypressLight = MeshKit.C(0.19f, 0.39f, 0.22f, MeshKit.Matte);
        private static readonly Color32 WarmGlow = MeshKit.C(1.00f, 0.67f, 0.24f, MeshKit.Glow);

        public static void Build(Transform worldRoot)
        {
            if (worldRoot == null || worldRoot.Find("StageOne_PersianSkyline") != null)
                return;

            Transform root = new GameObject("StageOne_PersianSkyline").transform;
            root.SetParent(worldRoot, false);

            MeshKit scenery = new MeshKit();

            // Four skyline towers make the city readable from long sightlines without
            // occupying the playable streets or changing any building collision.
            AddMinaret(scenery, -94f, -94f, 0);
            AddMinaret(scenery, 94f, -94f, 1);
            AddMinaret(scenery, -94f, 94f, 2);
            AddMinaret(scenery, 94f, 94f, 3);

            // Ceremonial arches mark the four distant approaches to the city.
            AddGate(scenery, new Vector3(0f, 0f, -100f), Vector3.right, 0);
            AddGate(scenery, new Vector3(0f, 0f, 100f), Vector3.right, 1);
            AddGate(scenery, new Vector3(-100f, 0f, 0f), Vector3.forward, 2);
            AddGate(scenery, new Vector3(100f, 0f, 0f), Vector3.forward, 3);

            AddCentralMosaic(scenery);
            AddRoadsideTileMedallions(scenery);

            Mesh mesh = scenery.Build("StageOne_PersianArchitecture_Batched");
            GameObject combined = new GameObject("StageOne_PersianArchitectureMesh");
            combined.transform.SetParent(root, false);
            MeshFilter filter = combined.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = combined.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = ModelLibrary.GetMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            CreateGateBanner(root, new Vector3(0f, GroundHeight(0f, -100f) + 9.1f, -100f), false, Terracotta, 0.2f);
            CreateGateBanner(root, new Vector3(0f, GroundHeight(0f, 100f) + 9.1f, 100f), false, DeepBlue, 1.7f);
            CreateGateBanner(root, new Vector3(-100f, GroundHeight(-100f, 0f) + 9.1f, 0f), true, Turquoise, 2.9f);
            CreateGateBanner(root, new Vector3(100f, GroundHeight(100f, 0f) + 9.1f, 0f), true, Terracotta, 4.1f);

            Debug.Log("PERSIA_STAGE1_ART: four minarets, four tiled gates, central mosaic and animated banners built as a low-draw-call 3D layer.");
        }

        private static void AddMinaret(MeshKit kit, float x, float z, int variation)
        {
            float y = GroundHeight(x, z);
            Vector3 center = new Vector3(x, y, z);

            kit.AddCylinder(center + Vector3.up * 0.48f, 1.55f, 1.55f, 0.96f, 10, Limestone, MeshKit.AlongY, true, true);
            kit.AddCylinder(center + Vector3.up * 1.14f, 1.26f, 1.05f, 0.38f, 10, Ivory, MeshKit.AlongY, true, true);
            kit.AddCylinder(center + Vector3.up * 4.25f, 0.96f, 0.70f, 5.95f, 10, Limestone, MeshKit.AlongY, true, true);
            kit.AddCylinder(center + Vector3.up * 7.25f, 0.78f, 0.96f, 0.26f, 10, Turquoise, MeshKit.AlongY, true, true);
            kit.AddRing(center + Vector3.up * 7.42f, 0.80f, 1.12f, 12, Brass);
            kit.AddCylinder(center + Vector3.up * 7.72f, 0.80f, 0.68f, 0.42f, 10, Ivory, MeshKit.AlongY, true, true);
            kit.AddCylinder(center + Vector3.up * 8.55f, 0.78f, 0.04f, 1.52f, 12,
                variation % 2 == 0 ? TurquoiseLight : DeepBlue, MeshKit.AlongY, true, true);
            kit.AddRing(center + Vector3.up * 8.05f, 0.58f, 0.78f, 12, Brass);
            kit.AddSphere(center + Vector3.up * 9.38f, 0.78f, 10, 7,
                variation % 2 == 0 ? Turquoise : DeepBlue);
            kit.AddCylinder(center + Vector3.up * 10.25f, 0.10f, 0.015f, 1.35f, 8, Brass, MeshKit.AlongY, true, true);
            kit.AddSphere(center + Vector3.up * 10.95f, 0.17f, 8, 5, WarmGlow);

            // Paired cypress silhouettes give the skyline a distinctly Persian garden rhythm.
            float side = variation % 2 == 0 ? 1f : -1f;
            AddCypress(kit, x + side * 2.6f, z, y, 2.4f);
            AddCypress(kit, x, z - side * 2.6f, GroundHeight(x, z - side * 2.6f), 2.0f);
        }

        private static void AddCypress(MeshKit kit, float x, float z, float y, float scale)
        {
            kit.AddCylinder(new Vector3(x, y + scale * 0.48f, z),
                scale * 0.075f, scale * 0.055f, scale * 0.96f, 6, Terracotta, MeshKit.AlongY, true, true);
            for (int tier = 0; tier < 4; tier++)
            {
                float radius = scale * (0.47f - tier * 0.085f);
                float height = scale * 0.92f;
                float baseY = y + scale * (0.54f + tier * 0.58f);
                kit.AddCylinder(new Vector3(x, baseY + height * 0.5f, z),
                    radius, radius * 0.08f, height, 8,
                    tier % 2 == 0 ? CypressDark : CypressLight, MeshKit.AlongY, true, true);
            }
        }

        private static void AddGate(MeshKit kit, Vector3 center, Vector3 axis, int variation)
        {
            float ground = GroundHeight(center.x, center.z);
            center.y = ground;
            float halfWidth = 5.15f;
            float spring = 4.05f;
            float outerRadius = halfWidth;
            Vector3 left = center - axis * halfWidth;
            Vector3 right = center + axis * halfWidth;

            // Broad sandstone piers with turquoise tile strips and brass capitals.
            for (int side = -1; side <= 1; side += 2)
            {
                Vector3 foot = center + axis * (side * halfWidth);
                kit.AddBox(foot + Vector3.up * 0.32f, new Vector3(1.55f, 0.64f, 1.55f), Limestone);
                kit.AddBox(foot + Vector3.up * 0.78f, new Vector3(1.20f, 0.30f, 1.20f), Brass);
                kit.AddBeam(foot + Vector3.up * 0.90f, foot + Vector3.up * spring,
                    0.88f, 0.88f, Ivory);
                kit.AddBeam(foot + Vector3.up * 1.65f, foot + Vector3.up * 3.45f,
                    0.94f, 0.94f, Limestone);
                kit.AddBeam(foot + Vector3.up * 1.75f + (axis * 0.10f), foot + Vector3.up * 3.25f + (axis * 0.10f),
                    0.12f, 0.12f, variation % 2 == 0 ? Turquoise : DeepBlue);
                kit.AddBox(foot + Vector3.up * (spring + 0.18f), new Vector3(1.28f, 0.35f, 1.28f), Brass);
            }

            AddArchCurve(kit, center, axis, outerRadius, spring, 0.62f, 0.62f, Limestone, false);
            AddArchCurve(kit, center, axis, outerRadius - 0.48f, spring, 0.17f, 0.17f,
                variation % 2 == 0 ? Turquoise : DeepBlue, true);

            // A shallow stepped crown and a winged medallion complete the gate silhouette.
            kit.AddBeam(center + Vector3.up * 8.95f - axis * 2.2f,
                center + Vector3.up * 8.95f + axis * 2.2f, 0.42f, 0.42f, Brass);
            kit.AddSphere(center + Vector3.up * 9.12f, 0.34f, 9, 6, WarmGlow);
            kit.AddBeam(center + Vector3.up * 9.08f - axis * 0.30f,
                center + Vector3.up * 9.08f - axis * 1.35f - Vector3.up * 0.15f, 0.16f, 0.16f, Brass);
            kit.AddBeam(center + Vector3.up * 9.08f + axis * 0.30f,
                center + Vector3.up * 9.08f + axis * 1.35f - Vector3.up * 0.15f, 0.16f, 0.16f, Brass);

            kit.AddRing(center + Vector3.up * 0.07f, 1.28f, 1.48f, 20, Brass);
            kit.AddRing(center + Vector3.up * 0.08f, 0.92f, 1.08f, 20,
                variation % 2 == 0 ? TurquoiseLight : Terracotta);
        }

        private static void AddArchCurve(
            MeshKit kit, Vector3 center, Vector3 axis, float radius,
            float springHeight, float thickness, float tileThickness, Color32 color, bool segmented)
        {
            const int segments = 14;
            Vector3 previous = center + axis * radius + Vector3.up * springHeight;
            for (int i = 1; i <= segments; i++)
            {
                float angle = Mathf.PI * i / segments;
                Vector3 next = center + axis * (Mathf.Cos(angle) * radius) +
                    Vector3.up * (springHeight + Mathf.Sin(angle) * radius);
                Color32 segmentColor = color;
                if (segmented && i % 3 == 0)
                    segmentColor = Brass;
                else if (segmented && i % 3 == 1)
                    segmentColor = TurquoiseLight;
                kit.AddBeam(previous, next, thickness, thickness, segmentColor);
                previous = next;
            }
        }

        private static void AddCentralMosaic(MeshKit kit)
        {
            Vector3 center = new Vector3(0f, GroundHeight(0f, 0f) + 0.045f, 0f);
            kit.AddRing(center, 3.25f, 3.48f, 48, Limestone);
            kit.AddRing(center + Vector3.up * 0.012f, 2.68f, 2.88f, 40, Turquoise);
            kit.AddRing(center + Vector3.up * 0.020f, 1.82f, 2.02f, 32, Brass);
            kit.AddRing(center + Vector3.up * 0.026f, 0.82f, 1.04f, 24, DeepBlue);

            for (int i = 0; i < 8; i++)
            {
                Quaternion rotation = Quaternion.Euler(0f, i * 45f, 0f);
                Vector3 ray = center + rotation * new Vector3(0f, 0.035f, 2.36f);
                kit.AddBox(ray, new Vector3(0.18f, 0.045f, 0.92f),
                    i % 2 == 0 ? Terracotta : TurquoiseLight, rotation);
                Vector3 jewel = center + rotation * new Vector3(0f, 0.06f, 1.42f);
                kit.AddSphere(jewel, 0.13f, 6, 4, Brass);
            }
            kit.AddSphere(center + Vector3.up * 0.075f, 0.34f, 8, 5, WarmGlow);
        }

        private static void AddRoadsideTileMedallions(MeshKit kit)
        {
            Vector2[] points =
            {
                new Vector2(-48f, -96f), new Vector2(48f, -96f),
                new Vector2(-48f, 96f), new Vector2(48f, 96f),
                new Vector2(-96f, -48f), new Vector2(96f, -48f),
                new Vector2(-96f, 48f), new Vector2(96f, 48f)
            };
            for (int i = 0; i < points.Length; i++)
            {
                float y = GroundHeight(points[i].x, points[i].y) + 0.045f;
                Vector3 center = new Vector3(points[i].x, y, points[i].y);
                kit.AddRing(center, 0.72f, 0.86f, 16, Brass);
                kit.AddRing(center + Vector3.up * 0.01f, 0.38f, 0.54f, 12,
                    i % 2 == 0 ? Turquoise : Terracotta);
            }
        }

        private static void CreateGateBanner(
            Transform parent, Vector3 position, bool rotateSideways, Color32 color, float phase)
        {
            Transform flagRoot = new GameObject("StageOne_AnimatedGateBanner").transform;
            flagRoot.SetParent(parent, false);
            flagRoot.position = position;
            flagRoot.rotation = rotateSideways ? Quaternion.Euler(0f, 90f, 0f) : Quaternion.identity;

            MeshKit cloth = new MeshKit();
            cloth.AddBox(new Vector3(0f, -0.68f, 0f), new Vector3(0.98f, 1.34f, 0.07f), color);
            cloth.AddBox(new Vector3(0f, -1.27f, 0.045f), new Vector3(0.98f, 0.11f, 0.10f), Brass);
            cloth.AddSphere(new Vector3(0f, -0.55f, 0.08f), 0.17f, 7, 5, WarmGlow);

            GameObject flagMesh = new GameObject("StageOne_BannerClothMesh");
            flagMesh.transform.SetParent(flagRoot, false);
            MeshFilter filter = flagMesh.AddComponent<MeshFilter>();
            filter.sharedMesh = cloth.Build("StageOne_BannerCloth");
            MeshRenderer renderer = flagMesh.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = ModelLibrary.GetMaterial();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            StageOneBannerMotion motion = flagRoot.gameObject.AddComponent<StageOneBannerMotion>();
            motion.SetPhase(phase);
        }

        private static float GroundHeight(float x, float z)
        {
            const float half = 110.4f;
            float radial = Vector2.Distance(new Vector2(x, z), Vector2.zero) / half;
            float edge = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((radial - 0.52f) / 0.48f));
            float undulation =
                Mathf.Sin(x * 0.075f) * 0.045f +
                Mathf.Cos(z * 0.065f) * 0.04f +
                Mathf.Sin((x + z) * 0.035f) * 0.025f;
            return edge * 0.18f + undulation * 0.35f;
        }
    }
}
