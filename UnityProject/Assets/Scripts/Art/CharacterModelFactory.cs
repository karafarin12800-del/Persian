using System.Collections.Generic;
using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public enum CharacterStyle
    {
        HeroArdeshir = 0,
        HeroAnahita = 1,
        Soldier = 2,
        Alien = 3,
        AlienTrooper = 4
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

            Transform torso = MakePart(root.transform, "Torso", new Vector3(0f, HipHeight, 0f), GetMesh(style, Part.Torso));
            Transform head = MakePart(torso, "Head", new Vector3(0f, NeckHeight, 0f), GetMesh(style, Part.Head));
            Transform leftArm = MakePart(torso, "ArmL", new Vector3(-ShoulderWidth, ShoulderHeight, 0f), GetMesh(style, Part.Arm));
            Transform rightArm = MakePart(torso, "ArmR", new Vector3(ShoulderWidth, ShoulderHeight, 0f), GetMesh(style, Part.Arm));
            Transform leftLeg = MakePart(root.transform, "LegL", new Vector3(-HipWidth, HipHeight, 0f), GetMesh(style, Part.Leg));
            Transform rightLeg = MakePart(root.transform, "LegR", new Vector3(HipWidth, HipHeight, 0f), GetMesh(style, Part.Leg));

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

        private static Transform MakePart(Transform parent, string name, Vector3 localPosition, Mesh mesh)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            if (mesh == null) return go.transform;
            MeshFilter filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = mesh;
            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = ModelLibrary.GetMaterial();
            ModelLibrary.ConfigureRenderer(renderer);
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
                    k.AddBox(new Vector3(0f, 0.34f, 0.13f), new Vector3(0.20f, 0.20f, 0.02f), Gold);
                    k.AddBox(new Vector3(0f, 0.15f, 0f), new Vector3(0.37f, 0.06f, 0.22f), Gold);
                    k.AddBox(new Vector3(0f, 0.08f, 0.10f), new Vector3(0.10f, 0.10f, 0.03f), Gold);
                    k.AddBox(new Vector3(0f, 0.20f, -0.17f), new Vector3(0.40f, 0.70f, 0.03f), Crimson,
                        Quaternion.Euler(8f, 0f, 0f));
                    break;
                case CharacterStyle.HeroAnahita:
                    k.AddBox(new Vector3(0f, 0.06f, 0f), new Vector3(0.30f, 0.14f, 0.18f), Magenta);
                    k.AddBox(new Vector3(0f, 0.34f, 0f), new Vector3(0.36f, 0.42f, 0.21f), Crimson);
                    k.AddBox(new Vector3(0f, 0.38f, 0.115f), new Vector3(0.16f, 0.14f, 0.02f), Gold);
                    k.AddBox(new Vector3(0f, 0.15f, 0f), new Vector3(0.32f, 0.05f, 0.20f), Gold);
                    k.AddCylinder(new Vector3(0f, -0.04f, 0f), 0.27f, 0.17f, 0.30f, 10, Magenta,
                        MeshKit.AlongY, true, true);
                    break;
                case CharacterStyle.Soldier:
                    k.AddBox(new Vector3(0f, 0.06f, 0f), new Vector3(0.34f, 0.14f, 0.20f), OliveDark);
                    k.AddBox(new Vector3(0f, 0.34f, 0f), new Vector3(0.44f, 0.44f, 0.25f), Olive);
                    k.AddBox(new Vector3(0f, 0.34f, 0.12f), new Vector3(0.38f, 0.34f, 0.05f), Khaki);
                    k.AddBox(new Vector3(0f, 0.15f, 0f), new Vector3(0.37f, 0.06f, 0.22f), Black);
                    k.AddBox(new Vector3(0.12f, 0.20f, 0.12f), new Vector3(0.09f, 0.10f, 0.05f), OliveDark);
                    k.AddBox(new Vector3(-0.12f, 0.20f, 0.12f), new Vector3(0.09f, 0.10f, 0.05f), OliveDark);
                    k.AddBox(new Vector3(0f, 0.34f, -0.19f), new Vector3(0.32f, 0.36f, 0.14f), OliveDark);
                    break;
                case CharacterStyle.Alien:
                    k.AddEllipsoid(new Vector3(0f, 0.08f, 0f), new Vector3(0.12f, 0.12f, 0.09f), 8, 5, AlienSkinDark,
                        Quaternion.identity);
                    k.AddEllipsoid(new Vector3(0f, 0.32f, 0f), new Vector3(0.17f, 0.26f, 0.12f), 8, 6, AlienSkin,
                        Quaternion.identity);
                    k.AddBox(new Vector3(0f, 0.34f, 0.11f), new Vector3(0.10f, 0.22f, 0.02f), AlienSkinDark);
                    break;
                default:   // AlienTrooper
                    k.AddEllipsoid(new Vector3(0f, 0.08f, 0f), new Vector3(0.12f, 0.12f, 0.09f), 8, 5, VioletDark,
                        Quaternion.identity);
                    k.AddEllipsoid(new Vector3(0f, 0.32f, 0f), new Vector3(0.18f, 0.27f, 0.13f), 8, 6, AlienSkin,
                        Quaternion.identity);
                    k.AddBox(new Vector3(0f, 0.34f, 0.12f), new Vector3(0.28f, 0.30f, 0.05f), Violet);
                    k.AddSphere(new Vector3(0f, 0.36f, 0.15f), 0.045f, 8, 5, Teal);
                    k.AddBox(new Vector3(0f, 0.15f, 0f), new Vector3(0.28f, 0.05f, 0.18f), VioletDark);
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
                        MeshKit.C(0.10f, 0.07f, 0.05f, MeshKit.Matte), Quaternion.identity);   // beard
                    k.AddCylinder(new Vector3(0f, 0.27f, 0f), 0.115f, 0.03f, 0.10f, 8, Steel,
                        MeshKit.AlongY, true, true);   // helmet cone
                    k.AddCylinder(new Vector3(0f, 0.20f, 0f), 0.118f, 0.04f, 8, Gold, MeshKit.AlongY);   // band
                    k.AddBox(new Vector3(0f, 0.34f, 0f), new Vector3(0.015f, 0.08f, 0.015f), Gold);
                    break;
                case CharacterStyle.HeroAnahita:
                    k.AddCylinder(new Vector3(0f, 0.03f, 0f), 0.045f, 0.08f, 8, Skin, MeshKit.AlongY);
                    k.AddEllipsoid(new Vector3(0f, 0.16f, 0f), new Vector3(0.095f, 0.115f, 0.10f), 8, 6, Skin,
                        Quaternion.identity);
                    k.AddEllipsoid(new Vector3(0f, 0.20f, -0.02f), new Vector3(0.108f, 0.11f, 0.115f), 8, 6,
                        MeshKit.C(0.12f, 0.07f, 0.05f, MeshKit.Matte), Quaternion.identity);   // hair
                    k.AddEllipsoid(new Vector3(0f, 0.04f, -0.13f), new Vector3(0.05f, 0.16f, 0.05f), 6, 5,
                        MeshKit.C(0.12f, 0.07f, 0.05f, MeshKit.Matte), Quaternion.Euler(-12f, 0f, 0f));   // ponytail
                    k.AddBox(new Vector3(0f, 0.27f, 0.07f), new Vector3(0.12f, 0.02f, 0.02f), Gold);   // diadem
                    break;
                case CharacterStyle.Soldier:
                    k.AddCylinder(new Vector3(0f, 0.03f, 0f), 0.05f, 0.08f, 8, SkinDark, MeshKit.AlongY);
                    k.AddEllipsoid(new Vector3(0f, 0.16f, 0f), new Vector3(0.10f, 0.12f, 0.11f), 8, 6, Skin,
                        Quaternion.identity);
                    k.AddEllipsoid(new Vector3(0f, 0.21f, 0f), new Vector3(0.125f, 0.10f, 0.135f), 8, 5, OliveDark,
                        Quaternion.identity);   // helmet
                    k.AddBox(new Vector3(0f, 0.15f, 0.09f), new Vector3(0.15f, 0.04f, 0.03f), Black);   // strap / goggles
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
                default:   // AlienTrooper
                    k.AddCylinder(new Vector3(0f, 0.03f, 0f), 0.035f, 0.10f, 8, AlienSkinDark, MeshKit.AlongY);
                    k.AddEllipsoid(new Vector3(0f, 0.22f, 0.02f), new Vector3(0.16f, 0.21f, 0.15f), 10, 7, AlienSkin,
                        Quaternion.identity);
                    k.AddEllipsoid(new Vector3(0f, 0.30f, -0.01f), new Vector3(0.17f, 0.13f, 0.16f), 8, 5, Violet,
                        Quaternion.identity);   // crest
                    k.AddBox(new Vector3(0f, 0.20f, 0.14f), new Vector3(0.22f, 0.07f, 0.04f), Teal);   // visor
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
                    k.AddBox(new Vector3(0f, -0.15f, 0f), new Vector3(0.12f, 0.30f, 0.12f), Navy);
                    k.AddBox(new Vector3(0f, -0.42f, 0f), new Vector3(0.10f, 0.26f, 0.10f), Steel);
                    k.AddSphere(new Vector3(0f, -0.58f, 0f), 0.055f, 6, 4, Skin);
                    break;
                case CharacterStyle.HeroAnahita:
                    k.AddSphere(new Vector3(0f, 0.02f, 0f), 0.08f, 8, 5, Gold);
                    k.AddBox(new Vector3(0f, -0.15f, 0f), new Vector3(0.10f, 0.30f, 0.10f), Crimson);
                    k.AddBox(new Vector3(0f, -0.42f, 0f), new Vector3(0.085f, 0.26f, 0.085f), Skin);
                    k.AddBox(new Vector3(0f, -0.36f, 0f), new Vector3(0.10f, 0.08f, 0.10f), Gold);
                    k.AddSphere(new Vector3(0f, -0.58f, 0f), 0.05f, 6, 4, Skin);
                    break;
                case CharacterStyle.Soldier:
                    k.AddSphere(new Vector3(0f, 0.02f, 0f), 0.085f, 8, 5, OliveDark);
                    k.AddBox(new Vector3(0f, -0.15f, 0f), new Vector3(0.12f, 0.30f, 0.12f), Olive);
                    k.AddBox(new Vector3(0f, -0.42f, 0f), new Vector3(0.10f, 0.26f, 0.10f), Khaki);
                    k.AddSphere(new Vector3(0f, -0.58f, 0f), 0.055f, 6, 4, Black);
                    break;
                case CharacterStyle.Alien:
                    k.AddBox(new Vector3(0f, -0.20f, 0f), new Vector3(0.07f, 0.40f, 0.07f), AlienSkin);
                    k.AddBox(new Vector3(0f, -0.55f, 0f), new Vector3(0.06f, 0.30f, 0.06f), AlienSkinDark);
                    k.AddBox(new Vector3(0f, -0.74f, 0.02f), new Vector3(0.09f, 0.10f, 0.05f), AlienSkin);
                    break;
                default:   // AlienTrooper
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
                    k.AddBox(new Vector3(0f, -0.64f, 0f), new Vector3(0.14f, 0.38f, 0.15f), Navy);
                    k.AddBox(new Vector3(0f, -0.83f, 0.04f), new Vector3(0.16f, 0.10f, 0.26f), BootBlack);
                    break;
                case CharacterStyle.HeroAnahita:
                    k.AddBox(new Vector3(0f, -0.22f, 0f), new Vector3(0.15f, 0.44f, 0.16f), Magenta);
                    k.AddBox(new Vector3(0f, -0.64f, 0f), new Vector3(0.12f, 0.38f, 0.13f), NavyDark);
                    k.AddBox(new Vector3(0f, -0.83f, 0.04f), new Vector3(0.14f, 0.10f, 0.24f), BootBlack);
                    break;
                case CharacterStyle.Soldier:
                    k.AddBox(new Vector3(0f, -0.22f, 0f), new Vector3(0.17f, 0.44f, 0.18f), Olive);
                    k.AddBox(new Vector3(0f, -0.44f, 0.09f), new Vector3(0.15f, 0.10f, 0.05f), OliveDark);
                    k.AddBox(new Vector3(0f, -0.64f, 0f), new Vector3(0.14f, 0.38f, 0.15f), OliveDark);
                    k.AddBox(new Vector3(0f, -0.83f, 0.04f), new Vector3(0.16f, 0.10f, 0.26f), BootBlack);
                    break;
                case CharacterStyle.Alien:
                    k.AddBox(new Vector3(0f, -0.25f, 0f), new Vector3(0.10f, 0.50f, 0.10f), AlienSkin);
                    k.AddBox(new Vector3(0f, -0.65f, 0f), new Vector3(0.08f, 0.40f, 0.08f), AlienSkinDark);
                    k.AddBox(new Vector3(0f, -0.85f, 0.05f), new Vector3(0.10f, 0.06f, 0.20f), AlienSkin);
                    break;
                default:   // AlienTrooper
                    k.AddBox(new Vector3(0f, -0.25f, 0f), new Vector3(0.11f, 0.50f, 0.11f), AlienSkin);
                    k.AddBox(new Vector3(0f, -0.44f, 0.06f), new Vector3(0.12f, 0.12f, 0.04f), Violet);
                    k.AddBox(new Vector3(0f, -0.65f, 0f), new Vector3(0.09f, 0.40f, 0.09f), VioletDark);
                    k.AddBox(new Vector3(0f, -0.85f, 0.05f), new Vector3(0.11f, 0.06f, 0.21f), Violet);
                    break;
            }
        }
    }
}