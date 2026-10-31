using System.Collections.Generic;
using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public enum ModelKind
    {
        None = 0,
        Pistol = 1,
        AssaultRifle = 2,
        HeavyMachineGun = 3,
        Axe = 4,
        VoidRifle = 5,
        AlienSword = 6,
        ShieldPickup = 7,
        Grenade = 8,
        AmmoCrate = 9,
        Medkit = 10,
        QuickDash = 11
    }

    /// <summary>
    /// ONE shared place that builds every weapon / pickup model. Each model is generated
    /// once into a single vertex-coloured mesh and cached; every user (hand, enemy hand,
    /// ground pickup, enemy drop, store preview) just instantiates a MeshFilter +
    /// MeshRenderer that shares the same mesh and the same material.
    /// Weapons point along +Z with the grip near the origin and +Y up.
    /// Meshes and the material are rebuilt on demand if a stage clean-up destroyed them.
    /// </summary>
    public static class ModelLibrary
    {
        private static readonly Dictionary<ModelKind, Mesh> meshes = new Dictionary<ModelKind, Mesh>();
        private static readonly Dictionary<uint, Mesh> ringMeshes = new Dictionary<uint, Mesh>();
        private static Material sharedMaterial;

        // ------------------------------------------------------------------ palette
        private static readonly Color32 GunMetal = MeshKit.C(0.15f, 0.16f, 0.18f, MeshKit.Metal);
        private static readonly Color32 GunMatte = MeshKit.C(0.10f, 0.10f, 0.11f, MeshKit.Matte);
        private static readonly Color32 Steel = MeshKit.C(0.62f, 0.65f, 0.70f, MeshKit.Metal);
        private static readonly Color32 DarkSteel = MeshKit.C(0.30f, 0.32f, 0.36f, MeshKit.Metal);
        private static readonly Color32 Black = MeshKit.C(0.04f, 0.04f, 0.05f, MeshKit.Plastic);
        private static readonly Color32 Wood = MeshKit.C(0.43f, 0.27f, 0.13f, MeshKit.Matte);
        private static readonly Color32 WoodDark = MeshKit.C(0.28f, 0.17f, 0.08f, MeshKit.Matte);
        private static readonly Color32 Brass = MeshKit.C(0.80f, 0.62f, 0.20f, MeshKit.Metal);
        private static readonly Color32 Olive = MeshKit.C(0.28f, 0.33f, 0.18f, MeshKit.Matte);
        private static readonly Color32 OliveDark = MeshKit.C(0.20f, 0.24f, 0.13f, MeshKit.Matte);
        private static readonly Color32 Violet = MeshKit.C(0.22f, 0.13f, 0.34f, MeshKit.Metal);
        private static readonly Color32 VioletDark = MeshKit.C(0.12f, 0.07f, 0.20f, MeshKit.Plastic);
        private static readonly Color32 Teal = MeshKit.C(0.20f, 0.95f, 0.85f, MeshKit.Glow);
        private static readonly Color32 SoftTeal = MeshKit.C(0.20f, 0.95f, 0.85f, 215);
        private static readonly Color32 Cyan = MeshKit.C(0.45f, 0.85f, 1.00f, MeshKit.Glow);
        private static readonly Color32 White = MeshKit.C(0.92f, 0.94f, 0.96f, MeshKit.Plastic);
        private static readonly Color32 Red = MeshKit.C(0.85f, 0.08f, 0.08f, MeshKit.Plastic);
        private static readonly Color32 Magenta = MeshKit.C(1.00f, 0.25f, 0.85f, MeshKit.Glow);
        private static readonly Color32 Blue = MeshKit.C(0.10f, 0.28f, 0.75f, MeshKit.Metal);
        private static readonly Color32 Yellow = MeshKit.C(0.95f, 0.80f, 0.10f, MeshKit.Plastic);

        // ------------------------------------------------------------------ lookup helpers

        /// <summary>Maps any weapon / pickup name (enum ToString, object name) to a model kind.</summary>
        public static ModelKind KindFromName(string name)
        {
            if (string.IsNullOrEmpty(name)) return ModelKind.None;
            string n = name.ToLowerInvariant();
            if (n.Contains("void")) return ModelKind.VoidRifle;
            if (n.Contains("sword") || n.Contains("blade")) return ModelKind.AlienSword;
            if (n.Contains("axe")) return ModelKind.Axe;
            if (n.Contains("heavy") || n.Contains("machine") || n.Contains("hmg")) return ModelKind.HeavyMachineGun;
            if (n.Contains("pistol") || n.Contains("light")) return ModelKind.Pistol;
            if (n.Contains("shield")) return ModelKind.ShieldPickup;
            if (n.Contains("grenade")) return ModelKind.Grenade;
            if (n.Contains("ammo")) return ModelKind.AmmoCrate;
            if (n.Contains("med")) return ModelKind.Medkit;
            if (n.Contains("dash")) return ModelKind.QuickDash;
            if (n.Contains("assault") || n.Contains("rifle") || n.Contains("ak")) return ModelKind.AssaultRifle;
            return ModelKind.None;
        }

        public static bool IsWeapon(ModelKind kind)
        {
            return kind >= ModelKind.Pistol && kind <= ModelKind.AlienSword;
        }

        public static bool IsAlienWeapon(ModelKind kind)
        {
            return kind == ModelKind.VoidRifle || kind == ModelKind.AlienSword;
        }

        // ------------------------------------------------------------------ material

        public static Material GetMaterial()
        {
            if (sharedMaterial != null) return sharedMaterial;
            Shader shader = Resources.Load<Shader>("PersiaWarVertexLit");
            if (shader == null) shader = Shader.Find("PersiaWar/VertexLit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            sharedMaterial = new Material(shader);
            sharedMaterial.name = "PW_SharedVertexLit";
            sharedMaterial.hideFlags = HideFlags.DontUnloadUnusedAsset;
            ApplyQualityTier(sharedMaterial);
            return sharedMaterial;
        }

        private static void ApplyQualityTier(Material material)
        {
            if (material == null) return;
            bool low = GraphicsQualityProfile.CurrentTier == GraphicsQualityProfile.QualityTier.Low;
            if (material.HasProperty("_RimStrength")) material.SetFloat("_RimStrength", low ? 0f : 0.30f);
            if (material.HasProperty("_SpecStrength")) material.SetFloat("_SpecStrength", low ? 0f : 0.55f);
        }

        // ------------------------------------------------------------------ spawning

        public static float PickupScale(ModelKind kind)
        {
            switch (kind)
            {
                case ModelKind.Pistol: return 2.1f;
                case ModelKind.AssaultRifle: return 1.0f;
                case ModelKind.HeavyMachineGun: return 0.9f;
                case ModelKind.Axe: return 1.0f;
                case ModelKind.VoidRifle: return 1.0f;
                case ModelKind.AlienSword: return 1.0f;
                case ModelKind.ShieldPickup: return 1.3f;
                case ModelKind.Grenade: return 2.2f;
                case ModelKind.AmmoCrate: return 1.4f;
                case ModelKind.Medkit: return 1.4f;
                case ModelKind.QuickDash: return 1.4f;
                default: return 1f;
            }
        }

        public static GameObject Spawn(ModelKind kind, Transform parent, Vector3 localPosition,
            Quaternion localRotation, float scale)
        {
            Mesh mesh = GetMesh(kind);
            if (mesh == null) return null;
            GameObject go = new GameObject("PW_" + kind);
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation;
            go.transform.localScale = new Vector3(scale, scale, scale);
            MeshFilter filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = GetMaterial();
            ConfigureRenderer(renderer);
            return go;
        }

        public static void ConfigureRenderer(Renderer renderer)
        {
            if (renderer == null) return;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.Off;
        }

        /// <summary>
        /// Puts the shared weapon model under a weapon root (hand or enemy). Existing
        /// primitive renderers under the root are only disabled, never destroyed, so code
        /// that still references them keeps working.
        /// </summary>
        public static GameObject AttachWeapon(Transform weaponRoot, ModelKind kind, bool hideOldVisuals,
            Vector3 localPosition, Quaternion localRotation, float scale)
        {
            if (weaponRoot == null || !IsWeapon(kind)) return null;
            if (hideOldVisuals)
            {
                Renderer[] old = weaponRoot.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < old.Length; i++)
                    if (old[i] != null) old[i].enabled = false;
            }
            return Spawn(kind, weaponRoot, localPosition, localRotation, scale);
        }

        // ------------------------------------------------------------------ mesh cache

        public static Mesh GetMesh(ModelKind kind)
        {
            if (kind == ModelKind.None) return null;
            Mesh mesh;
            if (meshes.TryGetValue(kind, out mesh) && mesh != null) return mesh;
            mesh = Build(kind);
            if (mesh != null)
            {
                mesh.hideFlags = HideFlags.DontUnloadUnusedAsset;
                meshes[kind] = mesh;
            }
            return mesh;
        }

        public static Mesh GetRingMesh(Color32 tint)
        {
            uint key = ((uint)tint.r << 16) | ((uint)tint.g << 8) | tint.b;
            Mesh mesh;
            if (ringMeshes.TryGetValue(key, out mesh) && mesh != null) return mesh;
            MeshKit k = new MeshKit();
            Color32 bright = new Color32(tint.r, tint.g, tint.b, MeshKit.Glow);
            Color32 soft = new Color32((byte)(tint.r / 2), (byte)(tint.g / 2), (byte)(tint.b / 2), 150);
            k.AddRing(Vector3.zero, 0.34f, 0.42f, 24, bright);
            k.AddRing(new Vector3(0f, -0.001f, 0f), 0.42f, 0.54f, 24, soft);
            mesh = k.Build("PW_PickupRing");
            mesh.hideFlags = HideFlags.DontUnloadUnusedAsset;
            ringMeshes[key] = mesh;
            return mesh;
        }

        public static void ReleaseAll()
        {
            foreach (KeyValuePair<ModelKind, Mesh> pair in meshes)
                if (pair.Value != null) Object.Destroy(pair.Value);
            meshes.Clear();
            foreach (KeyValuePair<uint, Mesh> pair in ringMeshes)
                if (pair.Value != null) Object.Destroy(pair.Value);
            ringMeshes.Clear();
            if (sharedMaterial != null) Object.Destroy(sharedMaterial);
            sharedMaterial = null;
        }

        private static Mesh Build(ModelKind kind)
        {
            MeshKit k = new MeshKit();
            switch (kind)
            {
                case ModelKind.Pistol: BuildPistol(k); break;
                case ModelKind.AssaultRifle: BuildAssaultRifle(k); break;
                case ModelKind.HeavyMachineGun: BuildHeavyMachineGun(k); break;
                case ModelKind.Axe: BuildAxe(k); break;
                case ModelKind.VoidRifle: BuildVoidRifle(k); break;
                case ModelKind.AlienSword: BuildAlienSword(k); break;
                case ModelKind.ShieldPickup: BuildShield(k); break;
                case ModelKind.Grenade: BuildGrenade(k); break;
                case ModelKind.AmmoCrate: BuildAmmoCrate(k); break;
                case ModelKind.Medkit: BuildMedkit(k); break;
                case ModelKind.QuickDash: BuildQuickDash(k); break;
                default: return null;
            }
            return k.Build("PW_Model_" + kind);
        }

        // ------------------------------------------------------------------ weapons

        private static void BuildPistol(MeshKit k)
        {
            // frame, slide, barrel
            k.AddBox(new Vector3(0f, 0.012f, 0.045f), new Vector3(0.032f, 0.028f, 0.20f), GunMatte);
            k.AddBox(new Vector3(0f, 0.045f, 0.065f), new Vector3(0.034f, 0.040f, 0.24f), Steel);
            k.AddCylinder(new Vector3(0f, 0.046f, 0.205f), 0.008f, 0.050f, 8, Black, MeshKit.AlongZ);
            // slide serrations
            for (int i = 0; i < 3; i++)
                k.AddBox(new Vector3(0f, 0.046f, -0.015f - i * 0.012f), new Vector3(0.036f, 0.030f, 0.004f), DarkSteel);
            // grip + magazine base
            Quaternion tilt = Quaternion.Euler(14f, 0f, 0f);
            k.AddBox(new Vector3(0f, -0.048f, -0.028f), new Vector3(0.030f, 0.115f, 0.052f), Black, tilt);
            k.AddBox(new Vector3(0f, -0.106f, -0.042f), new Vector3(0.032f, 0.010f, 0.056f), Steel, tilt);
            // trigger guard + trigger
            k.AddBox(new Vector3(0f, -0.021f, 0.055f), new Vector3(0.008f, 0.008f, 0.070f), GunMetal);
            k.AddBox(new Vector3(0f, -0.008f, 0.092f), new Vector3(0.008f, 0.026f, 0.008f), GunMetal);
            k.AddBox(new Vector3(0f, -0.012f, 0.044f), new Vector3(0.006f, 0.020f, 0.008f), Steel);
            // sights
            k.AddBox(new Vector3(0f, 0.070f, -0.045f), new Vector3(0.016f, 0.012f, 0.012f), Black);
            k.AddBox(new Vector3(0f, 0.070f, 0.175f), new Vector3(0.008f, 0.012f, 0.010f), Black);
        }

        private static void BuildAssaultRifle(MeshKit k)
        {
            // receiver, dust cover, rear sight
            k.AddBox(new Vector3(0f, 0.010f, 0.000f), new Vector3(0.050f, 0.070f, 0.320f), GunMetal);
            k.AddBox(new Vector3(0f, 0.055f, 0.000f), new Vector3(0.045f, 0.020f, 0.300f), Steel);
            k.AddBox(new Vector3(0f, 0.068f, 0.140f), new Vector3(0.030f, 0.020f, 0.050f), DarkSteel);
            k.AddBox(new Vector3(0.034f, 0.030f, 0.020f), new Vector3(0.030f, 0.012f, 0.012f), Steel);
            // barrel, gas tube, muzzle brake, front sight
            k.AddCylinder(new Vector3(0f, 0.002f, 0.575f), 0.012f, 0.520f, 8, Black, MeshKit.AlongZ);
            k.AddCylinder(new Vector3(0f, 0.045f, 0.520f), 0.011f, 0.320f, 8, DarkSteel, MeshKit.AlongZ);
            k.AddCylinder(new Vector3(0f, 0.002f, 0.835f), 0.017f, 0.050f, 8, Black, MeshKit.AlongZ);
            k.AddBox(new Vector3(0f, 0.050f, 0.790f), new Vector3(0.020f, 0.015f, 0.020f), Black);
            k.AddBox(new Vector3(0f, 0.068f, 0.790f), new Vector3(0.006f, 0.030f, 0.006f), Black);
            // wooden hand-guards
            k.AddTaper(new Vector3(0f, -0.003f, 0.460f), new Vector2(0.060f, 0.055f), new Vector2(0.054f, 0.050f),
                0.260f, Wood, MeshKit.AlongZ);
            k.AddBox(new Vector3(0f, 0.043f, 0.500f), new Vector3(0.050f, 0.028f, 0.220f), WoodDark);
            // stock + butt plate
            k.AddTaper(new Vector3(0f, -0.005f, -0.310f), new Vector2(0.040f, 0.120f), new Vector2(0.036f, 0.070f),
                0.300f, Wood, MeshKit.AlongZ);
            k.AddBox(new Vector3(0f, -0.005f, -0.462f), new Vector3(0.047f, 0.135f, 0.015f), Black);
            // pistol grip, trigger guard
            k.AddBox(new Vector3(0f, -0.075f, -0.060f), new Vector3(0.035f, 0.110f, 0.050f), WoodDark,
                Quaternion.Euler(18f, 0f, 0f));
            k.AddBox(new Vector3(0f, -0.040f, 0.020f), new Vector3(0.010f, 0.010f, 0.100f), GunMetal);
            // curved magazine (two segments)
            k.AddBox(new Vector3(0f, -0.085f, 0.075f), new Vector3(0.032f, 0.120f, 0.058f), GunMetal,
                Quaternion.Euler(-10f, 0f, 0f));
            k.AddBox(new Vector3(0f, -0.155f, 0.098f), new Vector3(0.032f, 0.080f, 0.056f), DarkSteel,
                Quaternion.Euler(-26f, 0f, 0f));
        }

        private static void BuildHeavyMachineGun(MeshKit k)
        {
            // body, top cover, carry handle
            k.AddBox(new Vector3(0f, 0.020f, 0.000f), new Vector3(0.075f, 0.100f, 0.420f), GunMetal);
            k.AddBox(new Vector3(0f, 0.085f, -0.020f), new Vector3(0.070f, 0.030f, 0.360f), Steel);
            k.AddBox(new Vector3(0f, 0.150f, 0.000f), new Vector3(0.020f, 0.015f, 0.200f), DarkSteel);
            k.AddBox(new Vector3(0f, 0.118f, 0.090f), new Vector3(0.020f, 0.070f, 0.020f), DarkSteel);
            k.AddBox(new Vector3(0f, 0.118f, -0.090f), new Vector3(0.020f, 0.070f, 0.020f), DarkSteel);
            // barrel shroud with cooling rings, barrel, flash hider
            k.AddCylinder(new Vector3(0f, 0.020f, 0.500f), 0.030f, 0.420f, 10, GunMetal, MeshKit.AlongZ);
            for (int i = 0; i < 3; i++)
                k.AddCylinder(new Vector3(0f, 0.020f, 0.360f + i * 0.14f), 0.035f, 0.016f, 10, Steel, MeshKit.AlongZ);
            k.AddCylinder(new Vector3(0f, 0.020f, 0.950f), 0.016f, 0.280f, 8, Black, MeshKit.AlongZ);
            k.AddCylinder(new Vector3(0f, 0.020f, 1.110f), 0.024f, 0.070f, 8, Black, MeshKit.AlongZ);
            // ammo box + brass belt
            k.AddBox(new Vector3(-0.085f, -0.030f, 0.000f), new Vector3(0.090f, 0.110f, 0.140f), Olive);
            k.AddBox(new Vector3(-0.085f, 0.030f, 0.000f), new Vector3(0.094f, 0.012f, 0.144f), OliveDark);
            for (int i = 0; i < 5; i++)
                k.AddBox(new Vector3(-0.075f + i * 0.014f, 0.040f, -0.020f),
                    new Vector3(0.012f, 0.012f, 0.040f), Brass);
            // stock + spade grips, pistol grip
            k.AddBox(new Vector3(0f, 0.000f, -0.340f), new Vector3(0.050f, 0.100f, 0.220f), Black);
            k.AddBox(new Vector3(0.040f, 0.030f, -0.300f), new Vector3(0.020f, 0.050f, 0.040f), Black);
            k.AddBox(new Vector3(-0.040f, 0.030f, -0.300f), new Vector3(0.020f, 0.050f, 0.040f), Black);
            k.AddBox(new Vector3(0f, -0.090f, -0.100f), new Vector3(0.040f, 0.130f, 0.050f), Black,
                Quaternion.Euler(15f, 0f, 0f));
            // bipod
            k.AddBox(new Vector3(0f, -0.030f, 0.620f), new Vector3(0.050f, 0.030f, 0.030f), DarkSteel);
            k.AddBeam(new Vector3(0.020f, -0.030f, 0.620f), new Vector3(0.090f, -0.130f, 0.760f), 0.014f, 0.014f, Steel);
            k.AddBeam(new Vector3(-0.020f, -0.030f, 0.620f), new Vector3(-0.090f, -0.130f, 0.760f), 0.014f, 0.014f, Steel);
        }

        private static void BuildAxe(MeshKit k)
        {
            // handle + leather grip + pommel
            k.AddCylinder(new Vector3(0f, 0f, 0.300f), 0.018f, 0.800f, 8, Wood, MeshKit.AlongZ);
            k.AddCylinder(new Vector3(0f, 0f, 0.050f), 0.022f, 0.200f, 8, Black, MeshKit.AlongZ);
            k.AddCylinder(new Vector3(0f, 0f, -0.110f), 0.026f, 0.030f, 8, DarkSteel, MeshKit.AlongZ);
            // socket and head
            k.AddBox(new Vector3(0f, 0f, 0.640f), new Vector3(0.045f, 0.060f, 0.080f), DarkSteel);
            // blade: wide at the handle, tapering to a bright edge (+X)
            k.AddTaper(new Vector3(0.115f, 0f, 0.650f), new Vector2(0.110f, 0.030f), new Vector2(0.012f, 0.012f),
                0.200f, Steel, MeshKit.AlongX);
            k.AddBox(new Vector3(0.215f, 0f, 0.650f), new Vector3(0.012f, 0.014f, 0.012f), White);
            // poll spike (-X)
            k.AddTaper(new Vector3(-0.060f, 0f, 0.650f), new Vector2(0.050f, 0.026f), new Vector2(0.010f, 0.012f),
                0.090f, DarkSteel, Quaternion.Euler(0f, -90f, 0f));
            // grip bands
            for (int i = 0; i < 3; i++)
                k.AddCylinder(new Vector3(0f, 0f, 0.000f + i * 0.070f), 0.024f, 0.012f, 8, DarkSteel, MeshKit.AlongZ);
        }

        private static void BuildVoidRifle(MeshKit k)
        {
            // organic violet body with a glowing core
            k.AddEllipsoid(new Vector3(0f, 0f, 0.050f), new Vector3(0.050f, 0.060f, 0.320f), 10, 7, Violet,
                Quaternion.identity);
            k.AddEllipsoid(new Vector3(0f, 0f, 0.050f), new Vector3(0.026f, 0.034f, 0.260f), 8, 6, Teal,
                Quaternion.identity);
            // energy cells on top
            for (int i = 0; i < 3; i++)
                k.AddSphere(new Vector3(0f, 0.062f, -0.050f + i * 0.130f), 0.020f, 8, 5, Cyan);
            // barrel with glowing rings and flared muzzle
            k.AddCylinder(new Vector3(0f, 0f, 0.560f), 0.020f, 0.500f, 8, VioletDark, MeshKit.AlongZ);
            for (int i = 0; i < 4; i++)
                k.AddCylinder(new Vector3(0f, 0f, 0.400f + i * 0.100f), 0.027f, 0.012f, 10, Teal, MeshKit.AlongZ);
            k.AddCylinder(new Vector3(0f, 0f, 0.835f), 0.034f, 0.014f, 0.070f, 10, Violet, MeshKit.AlongZ, true, true);
            // fins
            k.AddBeam(new Vector3(0.040f, 0.010f, 0.180f), new Vector3(0.095f, 0.050f, 0.380f), 0.014f, 0.030f, Violet);
            k.AddBeam(new Vector3(-0.040f, 0.010f, 0.180f), new Vector3(-0.095f, 0.050f, 0.380f), 0.014f, 0.030f, Violet);
            // under-barrel crystal
            k.AddCylinder(new Vector3(0f, -0.070f, 0.220f), 0.0f, 0.030f, 0.110f, 6, Teal, MeshKit.AlongY, false, true);
            // organic grip and crescent stock with a glow strip
            k.AddEllipsoid(new Vector3(0f, -0.075f, -0.100f), new Vector3(0.026f, 0.075f, 0.030f), 8, 5, VioletDark,
                Quaternion.Euler(15f, 0f, 0f));
            k.AddEllipsoid(new Vector3(0f, 0.000f, -0.300f), new Vector3(0.030f, 0.055f, 0.150f), 8, 6, Violet,
                Quaternion.identity);
            k.AddBox(new Vector3(0f, 0.052f, -0.300f), new Vector3(0.010f, 0.006f, 0.200f), Teal);
        }

        private static void BuildAlienSword(MeshKit k)
        {
            // hilt and pommel
            k.AddCylinder(new Vector3(0f, 0f, -0.020f), 0.018f, 0.220f, 8, VioletDark, MeshKit.AlongZ);
            for (int i = 0; i < 3; i++)
                k.AddCylinder(new Vector3(0f, 0f, -0.090f + i * 0.060f), 0.022f, 0.012f, 8, Violet, MeshKit.AlongZ);
            k.AddSphere(new Vector3(0f, 0f, -0.150f), 0.030f, 8, 5, Teal);
            // swept guard
            k.AddBox(new Vector3(0f, 0f, 0.105f), new Vector3(0.180f, 0.026f, 0.030f), Violet);
            k.AddBeam(new Vector3(0.090f, 0f, 0.105f), new Vector3(0.115f, 0f, 0.190f), 0.020f, 0.022f, Violet);
            k.AddBeam(new Vector3(-0.090f, 0f, 0.105f), new Vector3(-0.115f, 0f, 0.190f), 0.020f, 0.022f, Violet);
            // glowing energy blade, wide at the guard and tapering to the tip
            k.AddTaper(new Vector3(0f, 0f, 0.560f), new Vector2(0.070f, 0.014f), new Vector2(0.012f, 0.008f),
                0.880f, SoftTeal, MeshKit.AlongZ);
            k.AddTaper(new Vector3(0f, 0.002f, 0.560f), new Vector2(0.018f, 0.018f), new Vector2(0.004f, 0.012f),
                0.860f, Cyan, MeshKit.AlongZ);
            // dark metal spine on both edges
            k.AddBox(new Vector3(0.036f, 0f, 0.260f), new Vector3(0.008f, 0.016f, 0.280f), Violet);
            k.AddBox(new Vector3(-0.036f, 0f, 0.260f), new Vector3(0.008f, 0.016f, 0.280f), Violet);
        }

        // ------------------------------------------------------------------ pickups

        private static void BuildShield(MeshKit k)
        {
            // layered hexagonal shield seen from above, with rivets and a glowing emblem
            k.AddCylinder(Vector3.zero, 0.30f, 0.050f, 6, Steel, MeshKit.AlongY);
            k.AddCylinder(new Vector3(0f, 0.020f, 0f), 0.260f, 0.030f, 6, Blue, MeshKit.AlongY);
            k.AddCylinder(new Vector3(0f, 0.040f, 0f), 0.200f, 0.014f, 6, SoftTeal, MeshKit.AlongY);
            k.AddBox(new Vector3(0f, 0.052f, 0f), new Vector3(0.090f, 0.016f, 0.090f), White,
                Quaternion.Euler(0f, 45f, 0f));
            for (int i = 0; i < 6; i++)
            {
                float a = i * Mathf.PI / 3f;
                k.AddCylinder(new Vector3(Mathf.Cos(a) * 0.282f, 0.032f, Mathf.Sin(a) * 0.282f), 0.014f, 0.020f, 6,
                    Brass, MeshKit.AlongY);
            }
        }

        private static void BuildGrenade(MeshKit k)
        {
            k.AddEllipsoid(Vector3.zero, new Vector3(0.085f, 0.105f, 0.085f), 10, 8, Olive, Quaternion.identity);
            for (int i = -1; i <= 1; i++)
                k.AddCylinder(new Vector3(0f, i * 0.032f, 0f), 0.086f + (i == 0 ? 0.002f : 0f), 0.012f, 10,
                    OliveDark, MeshKit.AlongY);
            k.AddCylinder(new Vector3(0f, 0.118f, 0f), 0.032f, 0.040f, 8, Steel, MeshKit.AlongY);
            k.AddBox(new Vector3(0.040f, 0.075f, 0f), new Vector3(0.012f, 0.100f, 0.024f), DarkSteel,
                Quaternion.Euler(0f, 0f, -8f));
            k.AddBox(new Vector3(0.020f, 0.140f, 0f), new Vector3(0.040f, 0.008f, 0.014f), Brass);
        }

        private static void BuildAmmoCrate(MeshKit k)
        {
            k.AddBox(Vector3.zero, new Vector3(0.340f, 0.180f, 0.220f), Olive);
            k.AddBox(new Vector3(0f, 0.100f, 0f), new Vector3(0.350f, 0.030f, 0.230f), OliveDark);
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    k.AddBox(new Vector3(sx * 0.168f, 0f, sz * 0.108f), new Vector3(0.012f, 0.190f, 0.012f), DarkSteel);
            k.AddBox(new Vector3(0f, 0.118f, 0.050f), new Vector3(0.120f, 0.004f, 0.050f), Yellow);
            for (int i = -1; i <= 1; i++)
            {
                k.AddCylinder(new Vector3(i * 0.090f, 0.150f, -0.050f), 0.012f, 0.060f, 8, Brass, MeshKit.AlongY);
                k.AddCylinder(new Vector3(i * 0.090f, 0.190f, -0.050f), 0.012f, 0.0f, 0.024f, 8,
                    MeshKit.C(0.70f, 0.36f, 0.16f, MeshKit.Metal), MeshKit.AlongY, true, false);
            }
            k.AddBox(new Vector3(0.090f, 0.050f, 0.112f), new Vector3(0.030f, 0.030f, 0.010f), Brass);
            k.AddBox(new Vector3(-0.090f, 0.050f, 0.112f), new Vector3(0.030f, 0.030f, 0.010f), Brass);
        }

        private static void BuildMedkit(MeshKit k)
        {
            k.AddBox(Vector3.zero, new Vector3(0.300f, 0.170f, 0.200f), White);
            k.AddBox(new Vector3(0f, 0.095f, 0f), new Vector3(0.310f, 0.020f, 0.210f), White);
            // red cross on the lid and on the front
            k.AddBox(new Vector3(0f, 0.108f, 0f), new Vector3(0.140f, 0.006f, 0.040f), Red);
            k.AddBox(new Vector3(0f, 0.108f, 0f), new Vector3(0.040f, 0.006f, 0.140f), Red);
            k.AddBox(new Vector3(0f, 0.000f, 0.102f), new Vector3(0.100f, 0.040f, 0.004f), Red);
            k.AddBox(new Vector3(0f, 0.000f, 0.102f), new Vector3(0.040f, 0.100f, 0.004f), Red);
            // handle and latches
            k.AddBox(new Vector3(0f, 0.130f, 0f), new Vector3(0.100f, 0.012f, 0.020f), Black);
            k.AddBox(new Vector3(0.045f, 0.115f, 0f), new Vector3(0.012f, 0.030f, 0.020f), Black);
            k.AddBox(new Vector3(-0.045f, 0.115f, 0f), new Vector3(0.012f, 0.030f, 0.020f), Black);
            k.AddBox(new Vector3(0.100f, 0.070f, 0.103f), new Vector3(0.030f, 0.025f, 0.010f), Steel);
            k.AddBox(new Vector3(-0.100f, 0.070f, 0.103f), new Vector3(0.030f, 0.025f, 0.010f), Steel);
        }

        private static void BuildQuickDash(MeshKit k)
        {
            k.AddCylinder(Vector3.zero, 0.22f, 0.030f, 14, DarkSteel, MeshKit.AlongY);
            k.AddRing(new Vector3(0f, 0.016f, 0f), 0.185f, 0.215f, 20, Magenta);
            // three glowing chevrons pointing along +Z
            for (int i = 0; i < 3; i++)
            {
                float z = -0.07f + i * 0.085f;
                Vector3 tip = new Vector3(0f, 0.045f, z + 0.055f);
                k.AddBeam(tip, new Vector3(-0.095f, 0.045f, z - 0.030f), 0.030f, 0.030f, Magenta);
                k.AddBeam(tip, new Vector3(0.095f, 0.045f, z - 0.030f), 0.030f, 0.030f, Magenta);
            }
        }
    }
}
