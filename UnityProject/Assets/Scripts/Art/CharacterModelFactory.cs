using System.Collections.Generic;
using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public enum CharacterStyle
    {
        HeroArdeshir = 0,
        HeroAnahita = 1,
        Soldier = 2,
        RoyalScout = 3,
        SilkWarrior = 4,
        DesertKnight = 5,
        Alien = 6,
        AlienTrooper = 7
    }

    /// <summary>
    /// Drives the limbs of a CharacterModelFactory character with pivot-based swing
    /// (hips and shoulders), so walking, aiming and recoil look right. No allocations.
    /// Feed it from your movement code: rig.moveSpeed01 = speed / maxSpeed; rig.aiming = true while shooting.
    /// </summary>
    public sealed class CharacterRig : MonoBehaviour
    {
        public Transform torso;
        public Transform head;
        public Transform leftArm;
        public Transform rightArm;
        public Transform leftLeg;
        public Transform rightLeg;
        public Transform weaponMount;
        public float moveSpeed01;
        public bool aiming;

        private float phase;
        private float recoil;
        private Vector3 torsoBase;
        private bool torsoCaptured;

        public void Kick(float amount)
        {
            recoil = Mathf.Clamp01(Mathf.Max(recoil, amount));
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            if (!torsoCaptured && torso != null)
            {
                torsoBase = torso.localPosition;
                torsoCaptured = true;
            }

            float speed = Mathf.Clamp01(moveSpeed01);
            phase += dt * (2.5f + speed * 9f);
            float swing = Mathf.Sin(phase) * 42f * speed;
            float breathe = Mathf.Sin(Time.time * 1.8f);
            recoil = Mathf.MoveTowards(recoil, 0f, dt * 7f);

            SetPitch(leftLeg, swing);
            SetPitch(rightLeg, -swing);
            SetPitch(leftArm, aiming ? -35f : -swing * 0.8f + breathe * 1.5f * (1f - speed));
            SetPitch(rightArm, aiming ? -80f + recoil * 18f : swing * 0.8f - breathe * 1.5f * (1f - speed));

            if (torso != null)
            {
                Vector3 p = torsoBase;
                p.y += Mathf.Abs(Mathf.Sin(phase)) * 0.04f * speed + breathe * 0.004f;
                torso.localPosition = p;
            }
            if (head != null)
                head.localRotation = Quaternion.Euler(Mathf.Sin(phase * 2f) * 2f * speed, 0f, 0f);
        }

        private static void SetPitch(Transform t, float degrees)
        {
            if (t != null) t.localRotation = Quaternion.Euler(degrees, 0f, 0f);
        }
    }

    /// <summary>
    /// Procedural humanoid characters from MeshKit: heroes, soldiers and aliens. Each limb is a
    /// small cached mesh on a pivot (hip / shoulder / neck) and all parts share the single
    /// PersiaWar/VertexLit material. Characters face +Z, feet at y = 0, about 1.75 m tall.
    /// </summary>
    public static class CharacterModelFactory
    {
        private enum Part { Torso = 0, Head = 1, Arm = 2, Leg = 3 }

        private const float HipHeight = 0.88f;
        private const float NeckHeight = 0.56f;
        private const float ShoulderHeight = 0.52f;
        private const float ShoulderWidth = 0.26f;
        private const float HipWidth = 0.10f;

        private static readonly Dictionary<int, Mesh> meshes = new Dictionary<int, Mesh>();
        private static Material characterMaterial;

        // ------------------------------------------------------------------ palette
        private static readonly Color32 Skin = MeshKit.C(0.86f, 0.64f, 0.48f, MeshKit.Matte);
        private static readonly Color32 SkinDark = MeshKit.C(0.70f, 0.50f, 0.36f, MeshKit.Matte);
        private static readonly Color32 Black = MeshKit.C(0.05f, 0.05f, 0.06f, MeshKit.Plastic);
        private static readonly Color32 BootBlack = MeshKit.C(0.09f, 0.08f, 0.08f, MeshKit.Plastic);
        private static readonly Color32 Gold = MeshKit.C(0.88f, 0.68f, 0.22f, MeshKit.Metal);
        private static readonly Color32 Steel = MeshKit.C(0.62f, 0.65f, 0.70f, MeshKit.Metal);
        private static readonly Color32 Navy = MeshKit.C(0.10f, 0.18f, 0.42f, MeshKit.Metal);
        private static readonly Color32 NavyDark = MeshKit.C(0.07f, 0.11f, 0.26f, MeshKit.Matte);
        private static readonly Color32 Crimson = MeshKit.C(0.62f, 0.10f, 0.16f, MeshKit.Matte);
        private static readonly Color32 Magenta = MeshKit.C(0.74f, 0.17f, 0.43f, MeshKit.Matte);
        private static readonly Color32 Olive = MeshKit.C(0.30f, 0.34f, 0.20f, MeshKit.Matte);
        private static readonly Color32 OliveDark = MeshKit.C(0.20f, 0.24f, 0.14f, MeshKit.Matte);
        private static readonly Color32 Khaki = MeshKit.C(0.55f, 0.50f, 0.34f, MeshKit.Matte);
        private static readonly Color32 AlienSkin = MeshKit.C(0.48f, 0.58f, 0.52f, MeshKit.Plastic);
        private static readonly Color32 AlienSkinDark = MeshKit.C(0.36f, 0.45f, 0.42f, MeshKit.Plastic);
        private static readonly Color32 Violet = MeshKit.C(0.22f, 0.13f, 0.34f, MeshKit.Metal);
        private static readonly Color32 VioletDark = MeshKit.C(0.12f, 0.07f, 0.20f, MeshKit.Plastic);
        private static readonly Color32 Teal = MeshKit.C(0.20f, 0.95f, 0.85f, MeshKit.Glow);
        private static readonly Color32 EyeDark = MeshKit.C(0.02f, 0.10f, 0.12f, MeshKit.Plastic);

        public static bool IsAlien(CharacterStyle style)
        {
            return style == CharacterStyle.Alien || style == CharacterStyle.AlienTrooper;
        }

        /// <summary>Builds a character under parent. Returns the rig (null if the shader is missing).</summary>
        public static CharacterRig Create(CharacterStyle style, Transform parent, Vector3 localPosition, float scale)
        {
            GameObject root = new GameObject("PW_Character_" + style);
            if (parent != null) root.transform.SetParent(parent, false);
            root.transform.localPosition = localPosition;
            float s = scale * (IsAlien(style) ? 1.08f : 1f);
            root.transform.localScale = new Vector3(s, s, s);

            bool castCharacterShadows = !IsAlien(style);
            Transform torso = MakePart(root.transform, "Torso", new Vector3(0f, HipHeight, 0f), GetMesh(style, Part.Torso), castCharacterShadows);
            Transform head = MakePart(torso, "Head", new Vector3(0f, NeckHeight, 0f), GetMesh(style, Part.Head), castCharacterShadows);
            Transform leftArm = MakePart(torso, "ArmL", new Vector3(-ShoulderWidth, ShoulderHeight, 0f), GetMesh(style, Part.Arm), castCharacterShadows);
            Transform rightArm = MakePart(torso, "ArmR", new Vector3(ShoulderWidth, ShoulderHeight, 0f), GetMesh(style, Part.Arm), castCharacterShadows);
            Transform leftLeg = MakePart(root.transform, "LegL", new Vector3(-HipWidth, HipHeight, 0f), GetMesh(style, Part.Leg), castCharacterShadows);
            Transform rightLeg = MakePart(root.transform, "LegR", new Vector3(HipWidth, HipHeight, 0f), GetMesh(style, Part.Leg), castCharacterShadows);

            GameObject mount = new GameObject("WeaponMount");
            mount.transform.SetParent(rightArm, false);
            mount.transform.localPosition = new Vector3(0f, -0.58f, 0.10f);
            mount.transform.localRotation = Quaternion.Euler(80f, 0f, 0f);

            CharacterRig rig = root.AddComponent<CharacterRig>();
            rig.torso = torso;
            rig.head = head;
            rig.leftArm = leftArm;
            rig.rightArm = rightArm;
            rig.leftLeg = leftLeg;
            rig.rightLeg = rightLeg;
            rig.weaponMount = mount.transform;
            return rig;
        }

        public static void ReleaseAll()
        {
            foreach (KeyValuePair<int, Mesh> pair in meshes)
                if (pair.Value != null) Object.Destroy(pair.Value);
            meshes.Clear();
        }

        public static void ApplyQualityTier()
        {
            if (characterMaterial == null) return;
            bool high = GraphicsQualityProfile.CurrentTier == GraphicsQualityProfile.QualityTier.High;
            bool medium = GraphicsQualityProfile.CurrentTier == GraphicsQualityProfile.QualityTier.Medium;
            if (characterMaterial.HasProperty("_RimStrength"))
                characterMaterial.SetFloat("_RimStrength", high ? 0.58f : (medium ? 0.38f : 0.12f));
            if (characterMaterial.HasProperty("_SpecStrength"))
                characterMaterial.SetFloat("_SpecStrength", high ? 0.95f : (medium ? 0.68f : 0.28f));
            if (characterMaterial.HasProperty("_Gloss"))
                characterMaterial.SetFloat("_Gloss", high ? 72f : (medium ? 48f : 28f));
            if (characterMaterial.HasProperty("_EmissionBoost"))
                characterMaterial.SetFloat("_EmissionBoost", high ? 2.4f : (medium ? 1.8f : 1.2f));
            if (characterMaterial.HasProperty("_AmbientFloor"))
                characterMaterial.SetFloat("_AmbientFloor", high ? 0.25f : (medium ? 0.22f : 0.18f));
        }

        private static Material GetCharacterMaterial()
        {
            if (characterMaterial != null) return characterMaterial;
            Shader shader = Resources.Load<Shader>("PersiaWarCharacterLit");
            if (shader == null)
                return ModelLibrary.GetMaterial();
            characterMaterial = new Material(shader)
            {
                name = "PW_HeroCharacterLitMaterial",
                enableInstancing = true,
                hideFlags = HideFlags.DontUnloadUnusedAsset
            };
            ApplyQualityTier();
            return characterMaterial;
        }

        private static Transform MakePart(Transform parent, string name, Vector3 localPosition, Mesh mesh, bool castCharacterShadows)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            if (mesh == null) return go.transform;
            MeshFilter filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = GetCharacterMaterial();
            ModelLibrary.ConfigureRenderer(renderer);
            if (renderer.sharedMaterial == characterMaterial)
            {
                renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.BlendProbes;
                renderer.reflectionProbeUsage = UnityEngine.Rendering.ReflectionProbeUsage.BlendProbes;
            }
            if (castCharacterShadows)
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }
            return go.transform;
        }

        private static Mesh GetMesh(CharacterStyle style, Part part)
        {
            int key = (int)style * 10 + (int)part;
            Mesh mesh;
            if (meshes.TryGetValue(key, out mesh) && mesh != null) return mesh;
            MeshKit k = new MeshKit();
            switch (part)
            {
                case Part.Torso: BuildTorso(k, style); break;
                case Part.Head: BuildHead(k, style); break;
                case Part.Arm: BuildArm(k, style); break;
                default: BuildLeg(k, style); break;
            }
            mesh = k.Build("PW_Char_" + style + "_" + part);
            mesh.hideFlags = HideFlags.DontUnloadUnusedAsset;
            meshes[key] = mesh;
            return mesh;
        }

        // ------------------------------------------------------------------ torso (origin = hip centre)


        private static void BuildTorso(MeshKit k, CharacterStyle style)
        {
            switch (style)
            {
                case CharacterStyle.HeroArdeshir:
                    k.AddBox(new Vector3(0f, 0.06f, 0f), new Vector3(0.34f, 0.14f, 0.20f), NavyDark);
                    k.AddBox(new Vector3(0f, 0.34f, 0f), new Vector3(0.44f, 0.44f, 0.25f), Navy);
                    k.AddTaper(new Vector3(0f, 0.37f, 0.145f), new Vector2(0.31f, 0.35f),
                        new Vector2(0.22f, 0.29f), 0.045f, Steel, Quaternion.identity);
                    k.AddBox(new Vector3(0f, 0.38f, 0.175f), new Vector3(0.16f, 0.18f, 0.025f), Gold);
                    k.AddCylinder(new Vector3(0f, 0.38f, 0.196f), 0.055f, 0.055f, 0.025f, 10,
                        Crimson, MeshKit.AlongY, true, true);
                    k.AddBox(new Vector3(0f, 0.15f, 0f), new Vector3(0.39f, 0.07f, 0.26f), Gold);
                    k.AddBox(new Vector3(0f, 0.08f, 0.145f), new Vector3(0.12f, 0.11f, 0.035f), Gold);
                    k.AddBox(new Vector3(0f, 0.20f, -0.17f), new Vector3(0.42f, 0.72f, 0.035f), Crimson,
                        Quaternion.Euler(8f, 0f, 0f));
                    k.AddCylinder(new Vector3(-0.23f, 0.51f, 0f), 0.12f, 0.10f, 0.12f, 8, Gold,
                        MeshKit.AlongY, true, true);
                    k.AddCylinder(new Vector3(0.23f, 0.51f, 0f), 0.12f, 0.10f, 0.12f, 8, Gold,
                        MeshKit.AlongY, true, true);
                    break;
                case CharacterStyle.HeroAnahita:
                    k.AddBox(new Vector3(0f, 0.06f, 0f), new Vector3(0.30f, 0.14f, 0.18f), Magenta);
                    k.AddBox(new Vector3(0f, 0.34f, 0f), new Vector3(0.36f, 0.42f, 0.21f), Crimson);
                    k.AddTaper(new Vector3(0f, 0.38f, 0.12f), new Vector2(0.27f, 0.28f),
                        new Vector2(0.19f, 0.24f), 0.04f, Gold, Quaternion.identity);
                    k.AddBox(new Vector3(0f, 0.38f, 0.15f), new Vector3(0.12f, 0.13f, 0.035f), Magenta);
                    k.AddBox(new Vector3(0f, 0.15f, 0f), new Vector3(0.34f, 0.06f, 0.22f), Gold);
                    k.AddCylinder(new Vector3(0f, -0.04f, 0f), 0.27f, 0.17f, 0.30f, 12, Magenta,
                        MeshKit.AlongY, true, true);
                    k.AddBox(new Vector3(0f, 0.18f, -0.16f), new Vector3(0.34f, 0.62f, 0.035f), Crimson,
                        Quaternion.Euler(6f, 0f, 0f));
                    break;
                case CharacterStyle.Soldier:
                    k.AddBox(new Vector3(0f, 0.06f, 0f), new Vector3(0.34f, 0.14f, 0.20f), OliveDark);
                    k.AddBox(new Vector3(0f, 0.34f, 0f), new Vector3(0.44f, 0.44f, 0.25f), Olive);
                    k.AddBox(new Vector3(0f, 0.34f, 0.135f), new Vector3(0.36f, 0.34f, 0.05f), Khaki);
                    k.AddBox(new Vector3(0f, 0.15f, 0f), new Vector3(0.39f, 0.07f, 0.24f), Black);
                    k.AddBox(new Vector3(0.12f, 0.20f, 0.16f), new Vector3(0.10f, 0.12f, 0.07f), OliveDark);
                    k.AddBox(new Vector3(-0.12f, 0.20f, 0.16f), new Vector3(0.10f, 0.12f, 0.07f), OliveDark);
                    k.AddBox(new Vector3(0f, 0.34f, -0.19f), new Vector3(0.36f, 0.40f, 0.15f), OliveDark);
                    k.AddBeam(new Vector3(-0.19f, 0.53f, 0.15f), new Vector3(0.18f, 0.16f, 0.15f),
                        0.035f, 0.035f, Khaki);
                    k.AddCylinder(new Vector3(-0.25f, 0.48f, 0f), 0.11f, 0.10f, 0.10f, 8, OliveDark,
                        MeshKit.AlongY, true, true);
                    k.AddCylinder(new Vector3(0.25f, 0.48f, 0f), 0.11f, 0.10f, 0.10f, 8, OliveDark,
                        MeshKit.AlongY, true, true);
                    break;
                case CharacterStyle.RoyalScout:
                    k.AddBox(new Vector3(0f, 0.06f, 0f), new Vector3(0.31f, 0.13f, 0.19f), Black);
                    k.AddBox(new Vector3(0f, 0.34f, 0f), new Vector3(0.39f, 0.43f, 0.23f), Olive);
                    k.AddTaper(new Vector3(0f, 0.36f, 0.13f), new Vector2(0.30f, 0.32f),
                        new Vector2(0.22f, 0.25f), 0.045f, Khaki, Quaternion.identity);
                    k.AddBeam(new Vector3(-0.16f, 0.55f, 0.17f), new Vector3(0.17f, 0.16f, 0.17f),
                        0.035f, 0.035f, Black);
                    k.AddBeam(new Vector3(0.16f, 0.55f, 0.17f), new Vector3(-0.17f, 0.16f, 0.17f),
                        0.035f, 0.035f, Khaki);
                    k.AddBox(new Vector3(-0.22f, 0.19f, 0.12f), new Vector3(0.12f, 0.15f, 0.09f), Black);
                    k.AddBox(new Vector3(0.22f, 0.19f, 0.12f), new Vector3(0.12f, 0.15f, 0.09f), OliveDark);
                    k.AddBox(new Vector3(0f, 0.18f, -0.16f), new Vector3(0.37f, 0.62f, 0.04f), OliveDark);
                    break;
                case CharacterStyle.SilkWarrior:
                    k.AddBox(new Vector3(0f, 0.06f, 0f), new Vector3(0.32f, 0.14f, 0.20f), VioletDark);
                    k.AddBox(new Vector3(0f, 0.34f, 0f), new Vector3(0.41f, 0.43f, 0.23f), Magenta);
                    k.AddTaper(new Vector3(0f, 0.39f, 0.14f), new Vector2(0.31f, 0.33f),
                        new Vector2(0.22f, 0.25f), 0.05f, Gold, Quaternion.identity);
                    k.AddBeam(new Vector3(-0.19f, 0.54f, 0.15f), new Vector3(0.16f, 0.16f, 0.17f),
                        0.045f, 0.045f, Crimson);
                    k.AddBox(new Vector3(0f, 0.16f, 0f), new Vector3(0.40f, 0.07f, 0.25f), Gold);
                    k.AddBox(new Vector3(0f, 0.20f, -0.17f), new Vector3(0.43f, 0.72f, 0.04f), VioletDark,
                        Quaternion.Euler(8f, 0f, 0f));
                    k.AddCylinder(new Vector3(-0.23f, 0.5f, 0f), 0.11f, 0.10f, 0.10f, 8, Gold,
                        MeshKit.AlongY, true, true);
                    k.AddCylinder(new Vector3(0.23f, 0.5f, 0f), 0.11f, 0.10f, 0.10f, 8, Gold,
                        MeshKit.AlongY, true, true);
                    break;
                case CharacterStyle.DesertKnight:
                    k.AddBox(new Vector3(0f, 0.06f, 0f), new Vector3(0.34f, 0.14f, 0.20f), Black);
                    k.AddBox(new Vector3(0f, 0.34f, 0f), new Vector3(0.44f, 0.44f, 0.25f), Khaki);
                    k.AddTaper(new Vector3(0f, 0.38f, 0.145f), new Vector2(0.34f, 0.34f),
                        new Vector2(0.23f, 0.26f), 0.05f, Gold, Quaternion.identity);
                    k.AddBox(new Vector3(0f, 0.34f, 0.18f), new Vector3(0.12f, 0.21f, 0.035f), OliveDark);
                    k.AddBox(new Vector3(0f, 0.16f, 0f), new Vector3(0.40f, 0.07f, 0.25f), Black);
                    k.AddBox(new Vector3(0f, 0.16f, -0.17f), new Vector3(0.43f, 0.71f, 0.04f), Khaki,
                        Quaternion.Euler(7f, 0f, 0f));
                    k.AddCylinder(new Vector3(-0.24f, 0.50f, 0f), 0.13f, 0.11f, 0.12f, 8, Gold,
                        MeshKit.AlongY, true, true);
                    k.AddCylinder(new Vector3(0.24f, 0.50f, 0f), 0.13f, 0.11f, 0.12f, 8, Gold,
                        MeshKit.AlongY, true, true);
                    break;
                case CharacterStyle.Alien:
                    k.AddEllipsoid(new Vector3(0f, 0.08f, 0f), new Vector3(0.12f, 0.12f, 0.09f), 8, 5, AlienSkinDark,
                        Quaternion.identity);
                    k.AddEllipsoid(new Vector3(0f, 0.32f, 0f), new Vector3(0.17f, 0.26f, 0.12f), 8, 6, AlienSkin,
                        Quaternion.identity);
                    k.AddBox(new Vector3(0f, 0.34f, 0.11f), new Vector3(0.10f, 0.22f, 0.02f), AlienSkinDark);
                    break;
                default:
                    k.AddEllipsoid(new Vector3(0f, 0.08f, 0f), new Vector3(0.12f, 0.12f, 0.09f), 8, 5, VioletDark,
                        Quaternion.identity);
                    k.AddEllipsoid(new Vector3(0f, 0.32f, 0f), new Vector3(0.18f, 0.27f, 0.13f), 8, 6, AlienSkin,
                        Quaternion.identity);
                    k.AddBox(new Vector3(0f, 0.34f, 0.12f), new Vector3(0.28f, 0.30f, 0.05f), Violet);
                    k.AddSphere(new Vector3(0f, 0.36f, 0.15f), 0.045f, 8, 5, Teal);
                    break;
            }
        }

        // ------------------------------------------------------------------ head (origin = neck)


        private static void BuildHead(MeshKit k, CharacterStyle style)
        {
            switch (style)
            {
                case CharacterStyle.HeroArdeshir:
                    k.AddCylinder(new Vector3(0f, 0.03f, 0f), 0.05f, 0.08f, 8, SkinDark, MeshKit.AlongY);
                    k.AddEllipsoid(new Vector3(0f, 0.16f, 0f), new Vector3(0.10f, 0.12f, 0.11f), 8, 6, Skin,
                        Quaternion.identity);
                    k.AddEllipsoid(new Vector3(0f, 0.08f, 0.04f), new Vector3(0.085f, 0.07f, 0.08f), 8, 5,
                        MeshKit.C(0.10f, 0.07f, 0.05f, MeshKit.Matte), Quaternion.identity);
                    k.AddSphere(new Vector3(-0.045f, 0.18f, 0.094f), 0.018f, 6, 4, EyeDark);
                    k.AddSphere(new Vector3(0.045f, 0.18f, 0.094f), 0.018f, 6, 4, EyeDark);
                    k.AddCylinder(new Vector3(0f, 0.27f, 0f), 0.115f, 0.03f, 0.10f, 8, Steel,
                        MeshKit.AlongY, true, true);
                    k.AddCylinder(new Vector3(0f, 0.20f, 0f), 0.118f, 0.04f, 8, Gold, MeshKit.AlongY);
                    k.AddTaper(new Vector3(0f, 0.35f, -0.01f), new Vector2(0.055f, 0.02f),
                        new Vector2(0.012f, 0.01f), 0.17f, Crimson, Quaternion.identity);
                    break;
                case CharacterStyle.HeroAnahita:
                    k.AddCylinder(new Vector3(0f, 0.03f, 0f), 0.045f, 0.08f, 8, Skin, MeshKit.AlongY);
                    k.AddEllipsoid(new Vector3(0f, 0.16f, 0f), new Vector3(0.095f, 0.115f, 0.10f), 8, 6, Skin,
                        Quaternion.identity);
                    k.AddEllipsoid(new Vector3(0f, 0.20f, -0.02f), new Vector3(0.108f, 0.11f, 0.115f), 8, 6,
                        MeshKit.C(0.12f, 0.07f, 0.05f, MeshKit.Matte), Quaternion.identity);
                    k.AddEllipsoid(new Vector3(0f, 0.04f, -0.13f), new Vector3(0.05f, 0.16f, 0.05f), 6, 5,
                        MeshKit.C(0.12f, 0.07f, 0.05f, MeshKit.Matte), Quaternion.Euler(-12f, 0f, 0f));
                    k.AddSphere(new Vector3(-0.043f, 0.17f, 0.09f), 0.016f, 6, 4, EyeDark);
                    k.AddSphere(new Vector3(0.043f, 0.17f, 0.09f), 0.016f, 6, 4, EyeDark);
                    k.AddBox(new Vector3(0f, 0.27f, 0.07f), new Vector3(0.16f, 0.025f, 0.035f), Gold);
                    k.AddSphere(new Vector3(0f, 0.285f, 0.095f), 0.025f, 6, 4, Magenta);
                    break;
                case CharacterStyle.Soldier:
                    k.AddCylinder(new Vector3(0f, 0.03f, 0f), 0.05f, 0.08f, 8, SkinDark, MeshKit.AlongY);
                    k.AddEllipsoid(new Vector3(0f, 0.16f, 0f), new Vector3(0.10f, 0.12f, 0.11f), 8, 6, Skin,
                        Quaternion.identity);
                    k.AddEllipsoid(new Vector3(0f, 0.21f, 0f), new Vector3(0.125f, 0.10f, 0.135f), 8, 5, OliveDark,
                        Quaternion.identity);
                    k.AddBox(new Vector3(0f, 0.15f, 0.09f), new Vector3(0.15f, 0.04f, 0.03f), Black);
                    k.AddSphere(new Vector3(-0.045f, 0.17f, 0.108f), 0.014f, 5, 4, EyeDark);
                    k.AddSphere(new Vector3(0.045f, 0.17f, 0.108f), 0.014f, 5, 4, EyeDark);
                    break;
                case CharacterStyle.RoyalScout:
                    k.AddEllipsoid(new Vector3(0f, 0.16f, 0f), new Vector3(0.10f, 0.12f, 0.11f), 8, 6, Skin,
                        Quaternion.identity);
                    k.AddEllipsoid(new Vector3(0f, 0.19f, -0.025f), new Vector3(0.135f, 0.17f, 0.14f), 8, 6,
                        OliveDark, Quaternion.identity);
                    k.AddEllipsoid(new Vector3(0f, 0.10f, 0.09f), new Vector3(0.09f, 0.075f, 0.045f), 7, 5,
                        Khaki, Quaternion.identity);
                    k.AddSphere(new Vector3(-0.045f, 0.17f, 0.104f), 0.015f, 5, 4, EyeDark);
                    k.AddSphere(new Vector3(0.045f, 0.17f, 0.104f), 0.015f, 5, 4, EyeDark);
                    k.AddBox(new Vector3(0f, 0.29f, 0f), new Vector3(0.22f, 0.045f, 0.19f), Olive);
                    k.AddTaper(new Vector3(0f, 0.35f, -0.01f), new Vector2(0.025f, 0.025f),
                        new Vector2(0.005f, 0.005f), 0.15f, Khaki, Quaternion.identity);
                    break;
                case CharacterStyle.SilkWarrior:
                    k.AddCylinder(new Vector3(0f, 0.03f, 0f), 0.045f, 0.08f, 8, Skin, MeshKit.AlongY);
                    k.AddEllipsoid(new Vector3(0f, 0.16f, 0f), new Vector3(0.095f, 0.115f, 0.10f), 8, 6, Skin,
                        Quaternion.identity);
                    k.AddEllipsoid(new Vector3(0f, 0.21f, -0.04f), new Vector3(0.115f, 0.13f, 0.12f), 8, 6,
                        VioletDark, Quaternion.identity);
                    k.AddBox(new Vector3(0f, 0.27f, 0.06f), new Vector3(0.22f, 0.035f, 0.035f), Gold);
                    k.AddSphere(new Vector3(-0.042f, 0.17f, 0.09f), 0.016f, 6, 4, EyeDark);
                    k.AddSphere(new Vector3(0.042f, 0.17f, 0.09f), 0.016f, 6, 4, EyeDark);
                    k.AddTaper(new Vector3(0.09f, 0.17f, -0.02f), new Vector2(0.08f, 0.06f),
                        new Vector2(0.03f, 0.02f), 0.12f, Magenta, Quaternion.Euler(0f, 0f, -22f));
                    break;
                case CharacterStyle.DesertKnight:
                    k.AddCylinder(new Vector3(0f, 0.03f, 0f), 0.045f, 0.08f, 8, SkinDark, MeshKit.AlongY);
                    k.AddEllipsoid(new Vector3(0f, 0.16f, 0f), new Vector3(0.10f, 0.12f, 0.11f), 8, 6, Skin,
                        Quaternion.identity);
                    k.AddEllipsoid(new Vector3(0f, 0.22f, -0.015f), new Vector3(0.145f, 0.16f, 0.14f), 8, 6,
                        Khaki, Quaternion.identity);
                    k.AddBox(new Vector3(0f, 0.12f, 0.105f), new Vector3(0.18f, 0.10f, 0.04f), OliveDark);
                    k.AddSphere(new Vector3(-0.045f, 0.17f, 0.105f), 0.014f, 5, 4, EyeDark);
                    k.AddSphere(new Vector3(0.045f, 0.17f, 0.105f), 0.014f, 5, 4, EyeDark);
                    k.AddBox(new Vector3(0f, 0.29f, 0f), new Vector3(0.25f, 0.04f, 0.24f), Khaki);
                    k.AddTaper(new Vector3(0f, 0.31f, -0.04f), new Vector2(0.13f, 0.06f),
                        new Vector2(0.04f, 0.025f), 0.17f, Gold, Quaternion.identity);
                    break;
                case CharacterStyle.Alien:
                    k.AddCylinder(new Vector3(0f, 0.03f, 0f), 0.035f, 0.10f, 8, AlienSkinDark, MeshKit.AlongY);
                    k.AddEllipsoid(new Vector3(0f, 0.22f, 0.02f), new Vector3(0.16f, 0.21f, 0.15f), 10, 7, AlienSkin,
                        Quaternion.identity);
                    k.AddEllipsoid(new Vector3(0.07f, 0.20f, 0.12f), new Vector3(0.045f, 0.075f, 0.03f), 8, 5, EyeDark,
                        Quaternion.Euler(0f, 0f, -20f));
                    k.AddEllipsoid(new Vector3(-0.07f, 0.20f, 0.12f), new Vector3(0.045f, 0.075f, 0.03f), 8, 5, EyeDark,
                        Quaternion.Euler(0f, 0f, 20f));
                    k.AddSphere(new Vector3(0.07f, 0.20f, 0.14f), 0.012f, 5, 4, Teal);
                    k.AddSphere(new Vector3(-0.07f, 0.20f, 0.14f), 0.012f, 5, 4, Teal);
                    break;
                default:
                    k.AddCylinder(new Vector3(0f, 0.03f, 0f), 0.035f, 0.10f, 8, AlienSkinDark, MeshKit.AlongY);
                    k.AddEllipsoid(new Vector3(0f, 0.22f, 0.02f), new Vector3(0.16f, 0.21f, 0.15f), 10, 7, AlienSkin,
                        Quaternion.identity);
                    k.AddEllipsoid(new Vector3(0f, 0.30f, -0.01f), new Vector3(0.17f, 0.13f, 0.16f), 8, 5, Violet,
                        Quaternion.identity);
                    k.AddBox(new Vector3(0f, 0.20f, 0.14f), new Vector3(0.22f, 0.07f, 0.04f), Teal);
                    break;
            }
        }

        // ------------------------------------------------------------------ arm (origin = shoulder, hangs down -Y)


        private static void BuildArm(MeshKit k, CharacterStyle style)
        {
            switch (style)
            {
                case CharacterStyle.HeroArdeshir:
                    k.AddSphere(new Vector3(0f, 0.02f, 0f), 0.10f, 8, 5, Gold);
                    k.AddBox(new Vector3(0f, -0.15f, 0f), new Vector3(0.14f, 0.30f, 0.14f), Navy);
                    k.AddCylinder(new Vector3(0f, -0.40f, 0.02f), 0.082f, 0.09f, 0.12f, 8, Gold,
                        MeshKit.AlongY, true, true);
                    k.AddBox(new Vector3(0f, -0.48f, 0f), new Vector3(0.11f, 0.25f, 0.12f), Steel);
                    k.AddSphere(new Vector3(0f, -0.61f, 0.02f), 0.065f, 7, 5, Skin);
                    break;
                case CharacterStyle.HeroAnahita:
                    k.AddSphere(new Vector3(0f, 0.02f, 0f), 0.08f, 8, 5, Gold);
                    k.AddBox(new Vector3(0f, -0.15f, 0f), new Vector3(0.12f, 0.30f, 0.12f), Crimson);
                    k.AddCylinder(new Vector3(0f, -0.39f, 0.02f), 0.073f, 0.08f, 0.10f, 8, Gold,
                        MeshKit.AlongY, true, true);
                    k.AddBox(new Vector3(0f, -0.48f, 0f), new Vector3(0.095f, 0.24f, 0.095f), Skin);
                    k.AddSphere(new Vector3(0f, -0.61f, 0.02f), 0.055f, 7, 5, Skin);
                    break;
                case CharacterStyle.Soldier:
                    k.AddSphere(new Vector3(0f, 0.02f, 0f), 0.085f, 8, 5, OliveDark);
                    k.AddBox(new Vector3(0f, -0.15f, 0f), new Vector3(0.14f, 0.30f, 0.14f), Olive);
                    k.AddBox(new Vector3(0f, -0.40f, 0.02f), new Vector3(0.12f, 0.28f, 0.12f), Khaki);
                    k.AddCylinder(new Vector3(0f, -0.47f, 0.02f), 0.073f, 0.08f, 0.10f, 8, OliveDark,
                        MeshKit.AlongY, true, true);
                    k.AddSphere(new Vector3(0f, -0.61f, 0.02f), 0.06f, 7, 5, Black);
                    break;
                case CharacterStyle.RoyalScout:
                    k.AddSphere(new Vector3(0f, 0.01f, 0f), 0.08f, 7, 5, Khaki);
                    k.AddBox(new Vector3(0f, -0.16f, 0f), new Vector3(0.12f, 0.31f, 0.12f), Olive);
                    k.AddBox(new Vector3(0f, -0.40f, 0.02f), new Vector3(0.10f, 0.27f, 0.10f), OliveDark);
                    k.AddCylinder(new Vector3(0f, -0.48f, 0.02f), 0.07f, 0.075f, 0.10f, 8, Khaki,
                        MeshKit.AlongY, true, true);
                    k.AddSphere(new Vector3(0f, -0.61f, 0.02f), 0.055f, 7, 5, Skin);
                    break;
                case CharacterStyle.SilkWarrior:
                    k.AddSphere(new Vector3(0f, 0.02f, 0f), 0.085f, 7, 5, Gold);
                    k.AddBox(new Vector3(0f, -0.16f, 0f), new Vector3(0.13f, 0.32f, 0.13f), Magenta);
                    k.AddBox(new Vector3(0f, -0.39f, 0.02f), new Vector3(0.10f, 0.26f, 0.10f), Violet);
                    k.AddCylinder(new Vector3(0f, -0.48f, 0.02f), 0.074f, 0.08f, 0.10f, 8, Gold,
                        MeshKit.AlongY, true, true);
                    k.AddSphere(new Vector3(0f, -0.61f, 0.02f), 0.058f, 7, 5, Skin);
                    break;
                case CharacterStyle.DesertKnight:
                    k.AddSphere(new Vector3(0f, 0.02f, 0f), 0.09f, 7, 5, Gold);
                    k.AddBox(new Vector3(0f, -0.16f, 0f), new Vector3(0.14f, 0.32f, 0.14f), Khaki);
                    k.AddBox(new Vector3(0f, -0.40f, 0.02f), new Vector3(0.11f, 0.27f, 0.11f), OliveDark);
                    k.AddCylinder(new Vector3(0f, -0.48f, 0.02f), 0.078f, 0.085f, 0.10f, 8, Gold,
                        MeshKit.AlongY, true, true);
                    k.AddSphere(new Vector3(0f, -0.61f, 0.02f), 0.06f, 7, 5, SkinDark);
                    break;
                case CharacterStyle.Alien:
                    k.AddBox(new Vector3(0f, -0.20f, 0f), new Vector3(0.07f, 0.40f, 0.07f), AlienSkin);
                    k.AddBox(new Vector3(0f, -0.55f, 0f), new Vector3(0.06f, 0.30f, 0.06f), AlienSkinDark);
                    k.AddBox(new Vector3(0f, -0.74f, 0.02f), new Vector3(0.09f, 0.10f, 0.05f), AlienSkin);
                    break;
                default:
                    k.AddBeam(new Vector3(0f, 0.04f, 0f), new Vector3(0f, 0.12f, 0.08f), 0.05f, 0.05f, Violet);
                    k.AddBox(new Vector3(0f, 0.02f, 0f), new Vector3(0.16f, 0.07f, 0.14f), Violet);
                    k.AddBox(new Vector3(0f, -0.20f, 0f), new Vector3(0.07f, 0.40f, 0.07f), AlienSkin);
                    k.AddBox(new Vector3(0f, -0.55f, 0f), new Vector3(0.08f, 0.30f, 0.08f), VioletDark);
                    k.AddBox(new Vector3(0f, -0.74f, 0.02f), new Vector3(0.09f, 0.10f, 0.05f), AlienSkin);
                    break;
            }
        }

        // ------------------------------------------------------------------ leg (origin = hip, hangs down -Y)


        private static void BuildLeg(MeshKit k, CharacterStyle style)
        {
            switch (style)
            {
                case CharacterStyle.HeroArdeshir:
                    k.AddBox(new Vector3(0f, -0.22f, 0f), new Vector3(0.17f, 0.44f, 0.18f), NavyDark);
                    k.AddBox(new Vector3(0f, -0.44f, 0.09f), new Vector3(0.15f, 0.10f, 0.05f), Gold);
                    k.AddBox(new Vector3(0f, -0.64f, 0f), new Vector3(0.15f, 0.38f, 0.16f), Navy);
                    k.AddTaper(new Vector3(0f, -0.65f, 0.09f), new Vector2(0.13f, 0.12f),
                        new Vector2(0.10f, 0.10f), 0.06f, Steel, Quaternion.identity);
                    k.AddBox(new Vector3(0f, -0.83f, 0.07f), new Vector3(0.18f, 0.12f, 0.28f), BootBlack);
                    break;
                case CharacterStyle.HeroAnahita:
                    k.AddBox(new Vector3(0f, -0.22f, 0f), new Vector3(0.15f, 0.44f, 0.16f), Magenta);
                    k.AddBox(new Vector3(0f, -0.64f, 0f), new Vector3(0.13f, 0.38f, 0.14f), NavyDark);
                    k.AddBox(new Vector3(0f, -0.83f, 0.07f), new Vector3(0.17f, 0.11f, 0.25f), BootBlack);
                    break;
                case CharacterStyle.Soldier:
                    k.AddBox(new Vector3(0f, -0.22f, 0f), new Vector3(0.17f, 0.44f, 0.18f), Olive);
                    k.AddBox(new Vector3(0f, -0.44f, 0.09f), new Vector3(0.16f, 0.11f, 0.055f), OliveDark);
                    k.AddBox(new Vector3(0f, -0.64f, 0f), new Vector3(0.15f, 0.38f, 0.16f), OliveDark);
                    k.AddBox(new Vector3(0f, -0.83f, 0.07f), new Vector3(0.18f, 0.12f, 0.28f), BootBlack);
                    break;
                case CharacterStyle.RoyalScout:
                    k.AddBox(new Vector3(0f, -0.22f, 0f), new Vector3(0.15f, 0.44f, 0.16f), Olive);
                    k.AddBox(new Vector3(0f, -0.44f, 0.09f), new Vector3(0.13f, 0.10f, 0.05f), Khaki);
                    k.AddBox(new Vector3(0f, -0.64f, 0f), new Vector3(0.13f, 0.36f, 0.14f), Black);
                    k.AddBox(new Vector3(0f, -0.83f, 0.07f), new Vector3(0.17f, 0.11f, 0.25f), BootBlack);
                    break;
                case CharacterStyle.SilkWarrior:
                    k.AddBox(new Vector3(0f, -0.22f, 0f), new Vector3(0.16f, 0.44f, 0.17f), Magenta);
                    k.AddBox(new Vector3(0f, -0.44f, 0.09f), new Vector3(0.14f, 0.10f, 0.05f), Gold);
                    k.AddBox(new Vector3(0f, -0.64f, 0f), new Vector3(0.14f, 0.37f, 0.15f), VioletDark);
                    k.AddBox(new Vector3(0f, -0.83f, 0.07f), new Vector3(0.17f, 0.11f, 0.25f), Black);
                    break;
                case CharacterStyle.DesertKnight:
                    k.AddBox(new Vector3(0f, -0.22f, 0f), new Vector3(0.17f, 0.44f, 0.18f), Khaki);
                    k.AddBox(new Vector3(0f, -0.44f, 0.09f), new Vector3(0.16f, 0.11f, 0.055f), Gold);
                    k.AddBox(new Vector3(0f, -0.64f, 0f), new Vector3(0.15f, 0.38f, 0.16f), OliveDark);
                    k.AddBox(new Vector3(0f, -0.83f, 0.07f), new Vector3(0.18f, 0.12f, 0.28f), BootBlack);
                    break;
                case CharacterStyle.Alien:
                    k.AddBox(new Vector3(0f, -0.25f, 0f), new Vector3(0.10f, 0.50f, 0.10f), AlienSkin);
                    k.AddBox(new Vector3(0f, -0.65f, 0f), new Vector3(0.08f, 0.40f, 0.08f), AlienSkinDark);
                    k.AddBox(new Vector3(0f, -0.85f, 0.05f), new Vector3(0.10f, 0.06f, 0.20f), AlienSkin);
                    break;
                default:
                    k.AddBox(new Vector3(0f, -0.25f, 0f), new Vector3(0.11f, 0.50f, 0.11f), AlienSkin);
                    k.AddBox(new Vector3(0f, -0.44f, 0.06f), new Vector3(0.12f, 0.12f, 0.04f), Violet);
                    k.AddBox(new Vector3(0f, -0.65f, 0f), new Vector3(0.09f, 0.40f, 0.09f), VioletDark);
                    k.AddBox(new Vector3(0f, -0.85f, 0.05f), new Vector3(0.11f, 0.06f, 0.21f), Violet);
                    break;
            }
        }
    }
}
