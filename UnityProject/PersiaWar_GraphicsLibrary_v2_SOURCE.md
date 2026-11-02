# PersiaWar graphics library v2 - SOURCE (plain text, do NOT unzip anything)

# PersiaWar graphics library v2 - integration guide

Branch to work on: `Persian-war4-graphics`. Everything below was checked statically only (braces, C# tokens,
MeshKit call arity). It was NOT compiled in Unity or run on a device; fix compile errors with the smallest change.

## 0. State of the repo when v2 was written (verified by reading the branch)

Already in the repo (added by your AI): MeshKit, ModelLibrary (v1), ModelPreview, PickupVisual (v1), both shaders,
EnvironmentLightingProfile, character walk cycles + armor pieces in StylizedCharacterVisual, tree canopies and facade/crosswalk
details in GameBootstrap, energy-style hit/death effects in TargetHealth, PickupItem attaching Medkit / Shield / QuickDash models.

NOT wired yet (nothing calls the library): weapon models in the player's hand (WeaponController), enemy weapons
(EnemyChase), grenade / ammo / weapon pickups (only medkit, shield, dash use the new models), alien weapons, store previews
(PrototypeFlow.DrawStoreScreen still draws flat squares), blood on the ground, enemy knockback / death launch.

## 1. What changed in v2

* Shaders: colour-space safe (Linear and Gamma projects look the same). `PersiaWarVertexLit`: alpha of the vertex colour = surface
  (0 matte, 64 plastic, 128 metal, 255 glow).
* ModelLibrary: `GroundLift = 0.12f` (road/sidewalk/crosswalk quads sit 8-9 cm above the ground, so rings/decals lower than
  that were hidden on roads). New kinds: Barrel, Crate, Sandbags, StreetLamp, Rock, Bush, AlienPylon, Bullet, PlasmaBolt,
  MuzzleFlash, MuzzleFlashAlien. `IsProp()`, `IsProjectile()`.
* PickupVisual: ground ring stays at a fixed world height while the pickup bobs (`PickupGroundRing`); new
  `Attach(root, kind, hideOld, scaleMultiplier)`. Old signature still works.
* NEW MuzzleFlashFX: pooled flash, `MuzzleFlashFX.Show(muzzleTransform, isAlien)`.
* NEW CharacterModelFactory + CharacterRig: procedural humanoids on hip/shoulder pivots (HeroArdeshir, HeroAnahita, Soldier,
  Alien, AlienTrooper) with walk, aim and recoil animation. `CharacterModelFactory.Create(style, parent, localPos, scale)`.
  Feed `rig.moveSpeed01` and `rig.aiming`, call `rig.Kick(1f)` on every shot; weapon mount = `rig.weaponMount`.
* BloodFX: violence level `BloodFX.Level` (PlayerPrefs `PW_BLOOD_LEVEL`): 0 = no blood (energy sparks / scorch), 1 = normal
  (default), 2 = intense. Ground decals use GroundLift. HitReactor only spawns blood when level > 0 and never pushes hovercraft.
* TargetHealth.cs (paste-ready, in `paste_ready/`): uses BloodFX + HitReactor, keeps the existing energy effects for aliens and for
  level 0, drops the real weapon model on death (aliens drop the Void Rifle), pickups no longer hide under roads.

## 2. Files and paths (UnityProject/...)

Shaders (Assets/Resources): PersiaWarVertexLit.shader, PersiaWarDecal.shader (replace).
Assets/Scripts/Art: ModelLibrary.cs (replace), PickupVisual.cs (replace), CharacterModelFactory.cs (new). MeshKit.cs and
ModelPreview.cs are unchanged.
Assets/Scripts/Combat: BloodFX.cs (new), HitReactor.cs (new), MuzzleFlashFX.cs (new), TargetHealth.cs (replace with paste_ready/TargetHealth.cs).
Every new file needs its .meta (included).

## 3. Wiring still to do (the library is complete, these call sites are not)

a) Player's hand (WeaponController, where gun / axe parts are built or `RefreshWeaponVisual` runs). Build the model once per
   weapon change and reuse it:
   `ModelLibrary.AttachWeapon(gunRoot, ModelLibrary.KindFromName(weaponKind.ToString()), true, Vector3.zero, Quaternion.identity, 1f);`
   If WeaponController re-enables old parts on every refresh, deactivate/skip those parts there instead of relying on hideOldVisuals.
   Models point along +Z; if the existing root points along +X pass Quaternion.Euler(0, -90, 0).
b) Enemy weapons (EnemyChase.EnsureEnemyWeaponVisual): humans Pistol / AssaultRifle / HeavyMachineGun by archetype; aliens
   (name contains "Alien", or starts with S3_ / S4_) VoidRifle or AlienSword. Same AttachWeapon call.
c) Check that SelectWeapon(VoidRifle / AlienSword) works in Stages 2, 3 and 4 (it falls back to AssaultRifle when
   `axeEnabledForMatch` is false). TargetHealth v2 makes aliens drop a Void Rifle, so the pickup must really give it.
d) Pickups: PickupItem already attaches Medkit / Shield / QuickDash. Add Grenade, Ammo and weapon pickups the same way
   (`PickupVisual.Attach(transform, kind, true)`), and Stage 1 / Stage 2 loot in GameBootstrap.
e) Store (PrototypeFlow.DrawStoreScreen): weapon items use `ModelPreview.Draw(emblemRect, ModelKind.VoidRifle)` (AlienSword,
   ShieldPickup ...) instead of flat squares; `ModelPreview.ReleaseAll()` when leaving the store.
f) Shots: call `MuzzleFlashFX.Show(muzzle, isAlien)` where a shot is fired; projectiles can use ModelLibrary.Spawn(ModelKind.Bullet
   / PlasmaBolt, ...) as their visual (strip colliders from the visual, keep the physics object).
g) Characters (optional, bigger change): use `CharacterModelFactory.Create(...)` for Stage 3 / 4 aliens and for soldiers first; heroes later.
h) Settings screen: add a 3-state "VIOLENCE: OFF / NORMAL / HIGH" button that sets `BloodFX.Level = 0/1/2`.
i) Stage ends: `BloodFX.ClearAll(); ModelPreview.ReleaseAll(); CharacterModelFactory.ReleaseAll();`
j) Props (Barrel, Crate, Sandbags, StreetLamp, Rock, Bush, AlienPylon): place them with `ModelLibrary.Spawn` in the world builders,
   keep the existing colliders and hide the old renderers.
k) The walk cycle added to StylizedCharacterVisual rotates capsules around their own centre; use pivot children (like CharacterRig).

## 4. Rules for whoever edits

* Read the Python assertions in `.github/workflows/unity-android-build.yml` before touching PrototypeFlow / GameBootstrap /
  WeaponController / EnemyChase; asserted strings must stay, or be updated in the same commit.
* Never read `Material.color` / `SetColor("_Color")` on custom shaders without `HasProperty`.
* Do not say "done" until the files exist on the branch and the "Validate Unity project" job passes.


## 5. Files to create or REPLACE (exact content, UTF-8, LF)

Create each file at UnityProject/<path> with EXACTLY the content in its block (do not rewrite or shorten it first).
TargetHealth.cs replaces UnityProject/Assets/Scripts/Combat/TargetHealth.cs.

### UnityProject/Assets/Scripts/Art/CharacterModelFactory.cs.meta
```
fileFormatVersion: 2
guid: b0376cac84c9495f87cb88ef336c22d0
MonoImporter:
  externalObjects: {}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {instanceID: 0}
  userData:
  assetBundleName:
  assetBundleVariant:
```

### UnityProject/Assets/Scripts/Combat/MuzzleFlashFX.cs.meta
```
fileFormatVersion: 2
guid: 0f8b313d42f9417fbc75e3968c38818d
MonoImporter:
  externalObjects: {}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {instanceID: 0}
  userData:
  assetBundleName:
  assetBundleVariant:
```

### UnityProject/Assets/Scripts/Combat/BloodFX.cs.meta
```
fileFormatVersion: 2
guid: f338da3acf084e34b576d1001c857e4d
MonoImporter:
  externalObjects: {}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {instanceID: 0}
  userData:
  assetBundleName:
  assetBundleVariant:
```

### UnityProject/Assets/Scripts/Combat/HitReactor.cs.meta
```
fileFormatVersion: 2
guid: 22cabf93becd4ec98e109e0545ca5084
MonoImporter:
  externalObjects: {}
  serializedVersion: 2
  defaultReferences: []
  executionOrder: 0
  icon: {instanceID: 0}
  userData:
  assetBundleName:
  assetBundleVariant:
```

### FILE: UnityProject/Assets/Resources/PersiaWarVertexLit.shader
```hlsl
Shader "PersiaWar/VertexLit"
{
    // One cheap pass for every procedural model (weapons, pickups, props).
    // Vertex colour rgb = albedo. Vertex colour alpha encodes the surface:
    //   0 matte, 64 plastic, 128 metal (specular), 255 emissive glow.
    Properties
    {
        _Color ("Tint", Color) = (1,1,1,1)
        _EmissionBoost ("Emission Boost", Range(0, 4)) = 1.6
        _RimStrength ("Rim Strength", Range(0, 1)) = 0.30
        _SpecStrength ("Spec Strength", Range(0, 1)) = 0.55
        _Gloss ("Gloss", Range(4, 128)) = 36
        _AmbientFloor ("Ambient Floor", Range(0, 0.6)) = 0.20
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 200
        Pass
        {
            Tags { "LightMode"="ForwardBase" }
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"
            #include "Lighting.cginc"

            fixed4 _Color;
            half _EmissionBoost;
            half _RimStrength;
            half _SpecStrength;
            half _Gloss;
            half _AmbientFloor;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                half3 worldNormal : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
                fixed4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                half3 n = normalize(i.worldNormal);
                half3 l = normalize(_WorldSpaceLightPos0.xyz);
                half3 v = normalize(_WorldSpaceCameraPos.xyz - i.worldPos);

                half3 albedo = i.color.rgb * _Color.rgb;
                // Vertex colours are authored as display (sRGB) values.
                #if !defined(UNITY_COLORSPACE_GAMMA)
                albedo = GammaToLinearSpace(albedo);
                #endif
                half a = i.color.a;
                half specMask = (a <= 0.5h) ? saturate(a * 2.0h) : 0.0h;
                half emission = saturate((a - 0.5h) * 2.0h);

                half ndl = dot(n, l);
                half wrap = saturate(ndl * 0.5h + 0.5h);
                wrap = wrap * wrap;

                half hemi = saturate(n.y * 0.5h + 0.5h);
                half3 ambient = lerp(unity_AmbientGround.rgb, unity_AmbientSky.rgb, hemi);
                ambient = ambient * 0.8h + _AmbientFloor;

                half3 color = albedo * (ambient + _LightColor0.rgb * wrap);

                half3 h = normalize(l + v);
                half spec = pow(saturate(dot(n, h)), _Gloss) * saturate(ndl * 2.0h);
                color += _LightColor0.rgb * spec * specMask * _SpecStrength;

                half rim = pow(1.0h - saturate(dot(n, v)), 3.0h) * _RimStrength;
                color += albedo * rim;

                color += albedo * emission * _EmissionBoost;
                return fixed4(color, 1.0h);
            }
            ENDCG
        }
    }
    Fallback "Diffuse"
}
```

### FILE: UnityProject/Assets/Resources/PersiaWarDecal.shader
```hlsl
Shader "PersiaWar/Decal"
{
    // Unlit alpha-blended decal / particle shader. Colour comes from the vertex
    // (or particle) colour, shape from the texture alpha. One draw call for all
    // ground decals because they live in a single dynamic mesh.
    Properties
    {
        _MainTex ("Splat Atlas", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off
        Offset -1, -1
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 2.0
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 t = tex2D(_MainTex, i.uv);
                half3 tint = i.color.rgb;
                #if !defined(UNITY_COLORSPACE_GAMMA)
                tint = GammaToLinearSpace(tint);
                #endif
                return fixed4(tint * t.rgb, i.color.a * t.a);
            }
            ENDCG
        }
    }
    Fallback Off
}
```

### FILE: UnityProject/Assets/Scripts/Art/ModelLibrary.cs
```csharp
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
        QuickDash = 11,
        Barrel = 12,
        Crate = 13,
        Sandbags = 14,
        StreetLamp = 15,
        Rock = 16,
        Bush = 17,
        AlienPylon = 18,
        Bullet = 19,
        PlasmaBolt = 20,
        MuzzleFlash = 21,
        MuzzleFlashAlien = 22
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
        /// <summary>
        /// Height above the collider ground for rings, decals and ground effects. Road, sidewalk
        /// and crosswalk quads sit 8-9 cm above the ground, so anything lower is hidden on roads.
        /// </summary>
        public const float GroundLift = 0.12f;

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
            if (n.Contains("muzzle")) return n.Contains("alien") ? ModelKind.MuzzleFlashAlien : ModelKind.MuzzleFlash;
            if (n.Contains("plasma")) return ModelKind.PlasmaBolt;
            if (n.Contains("bullet") || n.Contains("tracer")) return ModelKind.Bullet;
            if (n.Contains("pylon")) return ModelKind.AlienPylon;
            if (n.Contains("lamp")) return ModelKind.StreetLamp;
            if (n.Contains("sandbag")) return ModelKind.Sandbags;
            if (n.Contains("barrel")) return ModelKind.Barrel;
            if (n.Contains("bush")) return ModelKind.Bush;
            if (n.Contains("rock")) return ModelKind.Rock;
            if (n.Contains("pistol") || n.Contains("light")) return ModelKind.Pistol;
            if (n.Contains("shield")) return ModelKind.ShieldPickup;
            if (n.Contains("grenade")) return ModelKind.Grenade;
            if (n.Contains("ammo")) return ModelKind.AmmoCrate;
            if (n.Contains("crate")) return ModelKind.Crate;
            if (n.Contains("med")) return ModelKind.Medkit;
            if (n.Contains("dash")) return ModelKind.QuickDash;
            if (n.Contains("assault") || n.Contains("rifle") || n.Contains("ak")) return ModelKind.AssaultRifle;
            return ModelKind.None;
        }

        public static bool IsWeapon(ModelKind kind)
        {
            return kind >= ModelKind.Pistol && kind <= ModelKind.AlienSword;
        }

        public static bool IsProp(ModelKind kind)
        {
            return kind >= ModelKind.Barrel && kind <= ModelKind.AlienPylon;
        }

        public static bool IsProjectile(ModelKind kind)
        {
            return kind == ModelKind.Bullet || kind == ModelKind.PlasmaBolt;
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
                case ModelKind.Barrel: BuildBarrel(k); break;
                case ModelKind.Crate: BuildCrate(k); break;
                case ModelKind.Sandbags: BuildSandbags(k); break;
                case ModelKind.StreetLamp: BuildStreetLamp(k); break;
                case ModelKind.Rock: BuildRock(k); break;
                case ModelKind.Bush: BuildBush(k); break;
                case ModelKind.AlienPylon: BuildAlienPylon(k); break;
                case ModelKind.Bullet: BuildBullet(k); break;
                case ModelKind.PlasmaBolt: BuildPlasmaBolt(k); break;
                case ModelKind.MuzzleFlash: BuildMuzzleFlash(k, false); break;
                case ModelKind.MuzzleFlashAlien: BuildMuzzleFlash(k, true); break;
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

        // ------------------------------------------------------------------ props

        private static void BuildBarrel(MeshKit k)
        {
            Color32 rust = MeshKit.C(0.50f, 0.16f, 0.10f, MeshKit.Plastic);
            k.AddCylinder(new Vector3(0f, 0.45f, 0f), 0.30f, 0.90f, 10, rust, MeshKit.AlongY);
            for (int i = 0; i < 3; i++)
                k.AddCylinder(new Vector3(0f, 0.15f + i * 0.30f, 0f), 0.312f, 0.04f, 10, DarkSteel, MeshKit.AlongY);
            k.AddCylinder(new Vector3(0f, 0.91f, 0f), 0.27f, 0.02f, 10, Steel, MeshKit.AlongY);
            k.AddCylinder(new Vector3(0.12f, 0.93f, 0.05f), 0.045f, 0.02f, 8, DarkSteel, MeshKit.AlongY);
        }

        private static void BuildCrate(MeshKit k)
        {
            k.AddBox(new Vector3(0f, 0.40f, 0f), new Vector3(0.80f, 0.80f, 0.80f), Wood);
            for (int sx = -1; sx <= 1; sx += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                    k.AddBox(new Vector3(sx * 0.38f, 0.40f, sz * 0.38f), new Vector3(0.10f, 0.84f, 0.10f), WoodDark);
            float h = 0.41f;
            k.AddBeam(new Vector3(-0.30f, 0.10f, h), new Vector3(0.30f, 0.70f, h), 0.03f, 0.07f, WoodDark);
            k.AddBeam(new Vector3(-0.30f, 0.10f, -h), new Vector3(0.30f, 0.70f, -h), 0.03f, 0.07f, WoodDark);
            k.AddBeam(new Vector3(h, 0.10f, -0.30f), new Vector3(h, 0.70f, 0.30f), 0.03f, 0.07f, WoodDark);
            k.AddBeam(new Vector3(-h, 0.10f, -0.30f), new Vector3(-h, 0.70f, 0.30f), 0.03f, 0.07f, WoodDark);
        }

        private static void BuildSandbags(MeshKit k)
        {
            Color32 sandA = MeshKit.C(0.72f, 0.62f, 0.42f, MeshKit.Matte);
            Color32 sandB = MeshKit.C(0.66f, 0.56f, 0.38f, MeshKit.Matte);
            Vector3 radii = new Vector3(0.27f, 0.13f, 0.18f);
            for (int i = 0; i < 4; i++)
                k.AddEllipsoid(new Vector3(-0.75f + i * 0.5f, 0.13f, 0f), radii, 8, 5, i % 2 == 0 ? sandA : sandB,
                    Quaternion.Euler(0f, i % 2 == 0 ? 4f : -5f, 0f));
            for (int i = 0; i < 3; i++)
                k.AddEllipsoid(new Vector3(-0.50f + i * 0.5f, 0.36f, 0.02f), radii, 8, 5, i % 2 == 0 ? sandB : sandA,
                    Quaternion.Euler(0f, i % 2 == 0 ? -4f : 6f, 0f));
            for (int i = 0; i < 2; i++)
                k.AddEllipsoid(new Vector3(-0.25f + i * 0.5f, 0.59f, 0f), radii, 8, 5, i % 2 == 0 ? sandA : sandB,
                    Quaternion.Euler(0f, 3f, 0f));
        }

        private static void BuildStreetLamp(MeshKit k)
        {
            k.AddCylinder(new Vector3(0f, 0.12f, 0f), 0.14f, 0.24f, 10, DarkSteel, MeshKit.AlongY);
            k.AddCylinder(new Vector3(0f, 1.80f, 0f), 0.05f, 3.40f, 8, DarkSteel, MeshKit.AlongY);
            k.AddBox(new Vector3(0f, 3.45f, 0.28f), new Vector3(0.08f, 0.08f, 0.70f), DarkSteel);
            k.AddBox(new Vector3(0f, 3.40f, 0.60f), new Vector3(0.30f, 0.10f, 0.45f), GunMetal);
            k.AddBox(new Vector3(0f, 3.34f, 0.60f), new Vector3(0.24f, 0.04f, 0.36f),
                MeshKit.C(1.00f, 0.88f, 0.55f, MeshKit.Glow));
        }

        private static void BuildRock(MeshKit k)
        {
            Color32 a = MeshKit.C(0.42f, 0.41f, 0.38f, MeshKit.Matte);
            Color32 b = MeshKit.C(0.34f, 0.34f, 0.32f, MeshKit.Matte);
            k.AddEllipsoid(new Vector3(0f, 0.28f, 0f), new Vector3(0.55f, 0.32f, 0.45f), 7, 4, a, Quaternion.Euler(0f, 20f, 0f));
            k.AddEllipsoid(new Vector3(0.38f, 0.18f, 0.12f), new Vector3(0.32f, 0.20f, 0.28f), 6, 4, b, Quaternion.Euler(0f, -30f, 10f));
            k.AddEllipsoid(new Vector3(-0.34f, 0.15f, -0.20f), new Vector3(0.28f, 0.17f, 0.24f), 6, 4, b, Quaternion.Euler(5f, 40f, 0f));
        }

        private static void BuildBush(MeshKit k)
        {
            Color32 a = MeshKit.C(0.16f, 0.38f, 0.13f, MeshKit.Matte);
            Color32 b = MeshKit.C(0.22f, 0.47f, 0.16f, MeshKit.Matte);
            k.AddEllipsoid(new Vector3(0f, 0.34f, 0f), new Vector3(0.50f, 0.34f, 0.46f), 8, 4, a, Quaternion.identity);
            k.AddEllipsoid(new Vector3(0.38f, 0.28f, 0.12f), new Vector3(0.34f, 0.26f, 0.32f), 7, 4, b, Quaternion.identity);
            k.AddEllipsoid(new Vector3(-0.34f, 0.26f, -0.10f), new Vector3(0.32f, 0.24f, 0.30f), 7, 4, b, Quaternion.identity);
            k.AddEllipsoid(new Vector3(0.04f, 0.52f, -0.02f), new Vector3(0.30f, 0.22f, 0.28f), 7, 4, a, Quaternion.identity);
        }

        private static void BuildAlienPylon(MeshKit k)
        {
            k.AddCylinder(new Vector3(0f, 0.10f, 0f), 0.55f, 0.20f, 6, VioletDark, MeshKit.AlongY);
            k.AddCylinder(new Vector3(0f, 1.10f, 0f), 0.40f, 0.26f, 1.70f, 6, Violet, MeshKit.AlongY, true, true);
            for (int i = 0; i < 3; i++)
                k.AddCylinder(new Vector3(0f, 0.55f + i * 0.50f, 0f), 0.37f - i * 0.04f, 0.06f, 6, Teal, MeshKit.AlongY);
            k.AddCylinder(new Vector3(0f, 2.20f, 0f), 0.18f, 0.0f, 0.70f, 6, Teal, MeshKit.AlongY, true, false);
            k.AddRing(new Vector3(0f, 0.02f, 0f), 0.55f, 0.85f, 20, SoftTeal);
        }

        // ------------------------------------------------------------------ projectiles and flashes

        private static void BuildBullet(MeshKit k)
        {
            Color32 amber = MeshKit.C(1.00f, 0.82f, 0.30f, MeshKit.Glow);
            Color32 white = MeshKit.C(1.00f, 0.97f, 0.85f, MeshKit.Glow);
            k.AddTaper(new Vector3(0f, 0f, -0.20f), new Vector2(0.004f, 0.004f), new Vector2(0.028f, 0.028f),
                0.60f, amber, MeshKit.AlongZ);
            k.AddSphere(new Vector3(0f, 0f, 0.12f), 0.03f, 6, 4, white);
        }

        private static void BuildPlasmaBolt(MeshKit k)
        {
            Color32 hot = MeshKit.C(0.90f, 1.00f, 1.00f, MeshKit.Glow);
            k.AddEllipsoid(Vector3.zero, new Vector3(0.07f, 0.07f, 0.30f), 8, 5, Teal, Quaternion.identity);
            k.AddEllipsoid(Vector3.zero, new Vector3(0.035f, 0.035f, 0.22f), 6, 4, hot, Quaternion.identity);
            k.AddEllipsoid(new Vector3(0f, 0f, -0.38f), new Vector3(0.04f, 0.04f, 0.12f), 6, 4, SoftTeal, Quaternion.identity);
            k.AddEllipsoid(new Vector3(0f, 0f, -0.60f), new Vector3(0.025f, 0.025f, 0.09f), 6, 4, SoftTeal, Quaternion.identity);
        }

        private static void BuildMuzzleFlash(MeshKit k, bool alien)
        {
            Color32 outer = alien ? MeshKit.C(0.20f, 0.95f, 0.85f, MeshKit.Glow) : MeshKit.C(1.00f, 0.86f, 0.45f, MeshKit.Glow);
            Color32 core = alien ? MeshKit.C(0.85f, 1.00f, 1.00f, MeshKit.Glow) : MeshKit.C(1.00f, 1.00f, 0.90f, MeshKit.Glow);
            k.AddCylinder(new Vector3(0f, 0f, 0.18f), 0.05f, 0.0f, 0.36f, 6, outer, MeshKit.AlongZ, false, false);
            k.AddTaper(new Vector3(0f, 0f, 0.12f), new Vector2(0.02f, 0.24f), new Vector2(0.004f, 0.02f), 0.26f, core, MeshKit.AlongZ);
            k.AddTaper(new Vector3(0f, 0f, 0.12f), new Vector2(0.24f, 0.02f), new Vector2(0.02f, 0.004f), 0.26f, core, MeshKit.AlongZ);
        }
    }
}
```

### FILE: UnityProject/Assets/Scripts/Art/PickupVisual.cs
```csharp
using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    /// <summary>Cheap spin + bob for models that do not already rotate themselves.</summary>
    public sealed class ModelSpinner : MonoBehaviour
    {
        public float spinDegreesPerSecond = 70f;
        public float bobHeight = 0.08f;
        public float bobSpeed = 2.2f;

        private Vector3 baseLocalPosition;
        private Quaternion baseLocalRotation;
        private float phase;

        private void Awake()
        {
            baseLocalPosition = transform.localPosition;
            baseLocalRotation = transform.localRotation;
            phase = Random.value * 6.2831853f;
        }

        private void Update()
        {
            float t = Time.time;
            transform.localPosition = baseLocalPosition +
                new Vector3(0f, Mathf.Sin(t * bobSpeed + phase) * bobHeight, 0f);
            transform.localRotation = Quaternion.AngleAxis(t * spinDegreesPerSecond + phase * 57.29578f,
                Vector3.up) * baseLocalRotation;
        }
    }

    /// <summary>Keeps a ground ring at a fixed world height while its pickup bobs and spins.</summary>
    public sealed class PickupGroundRing : MonoBehaviour
    {
        public float worldY;

        private void LateUpdate()
        {
            Vector3 p = transform.position;
            if (Mathf.Abs(p.y - worldY) > 0.001f)
            {
                p.y = worldY;
                transform.position = p;
            }
        }
    }

    /// <summary>
    /// Gives an existing pickup object (Stage 1 loot, Stage 2 loot, enemy drops) the real
    /// shared model plus a coloured ground ring. The old primitive renderers are only
    /// disabled, so colliders / PickupItem logic keep working unchanged. The pickup object
    /// itself (PickupItem) provides rotation and bobbing.
    /// </summary>
    public static class PickupVisual
    {
        private static readonly RaycastHit[] groundHits = new RaycastHit[8];

        public static Color32 RingColor(ModelKind kind)
        {
            switch (kind)
            {
                case ModelKind.Pistol:
                case ModelKind.AssaultRifle:
                case ModelKind.HeavyMachineGun:
                case ModelKind.Axe:
                    return new Color32(255, 200, 70, 255);
                case ModelKind.VoidRifle:
                case ModelKind.AlienSword:
                    return new Color32(60, 240, 215, 255);
                case ModelKind.ShieldPickup:
                    return new Color32(80, 170, 255, 255);
                case ModelKind.Grenade:
                    return new Color32(255, 120, 50, 255);
                case ModelKind.AmmoCrate:
                    return new Color32(255, 220, 90, 255);
                case ModelKind.Medkit:
                    return new Color32(255, 80, 80, 255);
                case ModelKind.QuickDash:
                    return new Color32(255, 70, 220, 255);
                default:
                    return new Color32(255, 255, 255, 255);
            }
        }

        public static GameObject Attach(Transform pickupRoot, ModelKind kind, bool hideOldVisuals)
        {
            return Attach(pickupRoot, kind, hideOldVisuals, 1f);
        }

        /// <summary>
        /// kind None -> resolved from the pickup object's name. scaleMultiplier scales the
        /// default pickup size (use it when the pickup object is already scaled up).
        /// Returns the model object.
        /// </summary>
        public static GameObject Attach(Transform pickupRoot, ModelKind kind, bool hideOldVisuals,
            float scaleMultiplier)
        {
            if (pickupRoot == null) return null;
            if (kind == ModelKind.None) kind = ModelLibrary.KindFromName(pickupRoot.name);
            if (kind == ModelKind.None) return null;

            if (hideOldVisuals)
            {
                Renderer[] old = pickupRoot.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < old.Length; i++)
                    if (old[i] != null) old[i].enabled = false;
            }

            GameObject model = ModelLibrary.Spawn(kind, pickupRoot, Vector3.zero, Quaternion.identity,
                ModelLibrary.PickupScale(kind) * Mathf.Max(0.05f, scaleMultiplier));
            if (model == null) return null;

            // Ground ring: a separate tiny mesh coloured by pickup type, fixed at ground level.
            GameObject ring = new GameObject("PW_PickupRing");
            ring.transform.SetParent(pickupRoot, false);
            Vector3 world = pickupRoot.position;
            float y = GroundY(world) + ModelLibrary.GroundLift;
            ring.transform.position = new Vector3(world.x, y, world.z);
            PickupGroundRing fixedRing = ring.AddComponent<PickupGroundRing>();
            fixedRing.worldY = y;
            MeshFilter filter = ring.AddComponent<MeshFilter>();
            filter.sharedMesh = ModelLibrary.GetRingMesh(RingColor(kind));
            MeshRenderer renderer = ring.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = ModelLibrary.GetMaterial();
            ModelLibrary.ConfigureRenderer(renderer);
            return model;
        }

        public static float GroundY(Vector3 worldPosition)
        {
            int count = Physics.RaycastNonAlloc(new Ray(worldPosition + Vector3.up * 0.6f, Vector3.down),
                groundHits, 6f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            float y = 0f;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                Collider c = groundHits[i].collider;
                if (c == null || groundHits[i].normal.y < 0.5f) continue;
                if (c.GetComponentInParent<TargetHealth>() != null) continue;
                if (groundHits[i].distance < best)
                {
                    best = groundHits[i].distance;
                    y = groundHits[i].point.y;
                    found = true;
                }
            }
            return found ? y : 0f;
        }
    }
}
```

### FILE: UnityProject/Assets/Scripts/Art/CharacterModelFactory.cs
```csharp
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
```

### FILE: UnityProject/Assets/Scripts/Combat/BloodFX.cs
```csharp
using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public enum BloodStyle
    {
        Red = 0,
        AlienGoo = 1,
        Scorch = 2,
        Energy = 3
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

        private const string LevelKey = "PW_BLOOD_LEVEL";
        private static int level = -1;
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

        /// <summary>
        /// Violence setting saved in PlayerPrefs: 0 = no blood (energy sparks / scorch marks only),
        /// 1 = normal (default), 2 = intense (more droplets and longer-lasting pools).
        /// Expose it as a three-state toggle in the settings screen.
        /// </summary>
        public static int Level
        {
            get
            {
                if (level < 0) level = Mathf.Clamp(PlayerPrefs.GetInt(LevelKey, 1), 0, 2);
                return level;
            }
            set
            {
                level = Mathf.Clamp(value, 0, 2);
                PlayerPrefs.SetInt(LevelKey, level);
            }
        }

        private static BloodStyle ResolveStyle(BloodStyle style)
        {
            if (Level > 0) return style;
            if (style == BloodStyle.AlienGoo) return BloodStyle.Energy;
            if (style == BloodStyle.Red) return BloodStyle.Scorch;
            return style;
        }

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
                case BloodStyle.Energy:
                    return new Color32(Clamp8(56f * shade), Clamp8(224f * shade), Clamp8(255f * shade), 235);
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
            style = ResolveStyle(style);
            float boost = Level >= 2 ? 1.4f : 1f;
            Vector3 dir = Flat(direction);
            int tier = Scale();
            int count = Mathf.Clamp(Mathf.RoundToInt((4f + damage * 0.12f) * boost), 4, 16);
            if (tier <= 0) count = Mathf.Max(2, count / 2);
            float groundY = GroundY(position);
            EmitDrops(position, dir, count, style, 1f, groundY, tier > 0);

            bool bloodStyle = style == BloodStyle.Red || style == BloodStyle.AlienGoo;
            int splats = !bloodStyle ? 0 : (tier <= 0 ? 1 : 2) + (Level >= 2 ? 1 : 0);
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
            style = ResolveStyle(style);
            float boost = Level >= 2 ? 1.4f : 1f;
            Vector3 dir = Flat(direction);
            int tier = Scale();
            float groundY = GroundY(position);
            Vector3 ground = new Vector3(position.x, groundY, position.z);
            Vector3 side = new Vector3(-dir.z, 0f, dir.x);

            int dropCount = Mathf.RoundToInt((tier <= 0 ? 10 : (tier == 1 ? 18 : 26)) * boost);
            EmitDrops(position, dir, dropCount, style, 1.3f + force * 0.08f, groundY, tier > 0);
            if (style != BloodStyle.Red && style != BloodStyle.AlienGoo) return;   // sparks / scorch only

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

                Color32 color;
                if (style == BloodStyle.Scorch)
                    color = new Color32(255, Clamp8(Random.Range(140f, 200f)), 60, 255);
                else if (style == BloodStyle.Energy)
                    color = new Color32(70, Clamp8(Random.Range(200f, 245f)), 255, 255);
                else
                    color = ColorFor(style, Random.Range(0.8f, 1.1f));
                emit.position = position;
                emit.velocity = velocity;
                emit.startLifetime = landing + 0.02f;
                emit.startSize = Random.Range(0.05f, 0.12f);
                emit.startColor = color;
                drops.Emit(emit, 1);

                if (landingSplats && (style == BloodStyle.Red || style == BloodStyle.AlienGoo) && Random.value < 0.45f)
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
            centers[slot] = new Vector3(center.x, center.y + ModelLibrary.GroundLift, center.z);
            if (Level >= 2) life *= 1.5f;
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
```

### FILE: UnityProject/Assets/Scripts/Combat/HitReactor.cs
```csharp
using System.Collections;
using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    /// <summary>
    /// Natural hit reaction without physics bodies: a short stagger + knockback when an
    /// enemy is shot, and a launched, tumbling fall when it dies. Movement is simulated
    /// kinematically with ray checks, so it never pushes bodies through walls and costs
    /// almost nothing on mobile. Attach with HitReactor.Ensure(gameObject).
    /// EnemyChase may check IsStaggered to pause its own movement for a moment.
    /// </summary>
    public sealed class HitReactor : MonoBehaviour
    {
        public const float SettleSeconds = 1.4f;

        private static readonly RaycastHit[] hitBuffer = new RaycastHit[10];

        private Vector3 knockVelocity;
        private float staggerUntil;
        private bool dying;

        public bool IsStaggered
        {
            get { return Time.time < staggerUntil; }
        }

        public bool IsDying
        {
            get { return dying; }
        }

        public static HitReactor Ensure(GameObject target)
        {
            if (target == null) return null;
            HitReactor reactor = target.GetComponent<HitReactor>();
            if (reactor == null) reactor = target.AddComponent<HitReactor>();
            return reactor;
        }

        // ------------------------------------------------------------------ one-call facade

        /// <summary>
        /// Call from TargetHealth.ApplyDamage for every non-lethal hit.
        /// attackerPosition may be Vector3.zero when unknown (a random direction is used).
        /// weaponName is optional (WeaponKind.ToString()); heavier weapons push harder.
        /// </summary>
        public static void NotifyDamaged(GameObject target, Vector3 attackerPosition, float damage, string weaponName)
        {
            if (target == null) return;
            Vector3 center = CenterOf(target);
            Vector3 direction = DirectionFrom(attackerPosition, target.transform.position);
            BloodStyle style = BloodFX.StyleFor(target.name);
            if (BloodFX.Level > 0) BloodFX.Hit(center, direction, damage, style);
            if (target.GetComponent<PlayerController>() != null) return;   // never shove the player
            if (style == BloodStyle.Scorch) return;                         // hovercraft are not pushed
            float force = ForceForWeapon(weaponName) * Mathf.Clamp(0.7f + damage * 0.01f, 0.7f, 1.8f);
            Ensure(target).Stagger(direction, force);
        }

        /// <summary>
        /// Call when the target dies. Returns how many seconds the body needs to settle;
        /// destroy the object after at least that long (plus whatever fade you already use).
        /// </summary>
        public static float NotifyKilled(GameObject target, Vector3 attackerPosition, float damage,
            string weaponName, bool explosive)
        {
            if (target == null) return 0f;
            Vector3 center = CenterOf(target);
            Vector3 direction = DirectionFrom(attackerPosition, target.transform.position);
            float force = ForceForWeapon(weaponName) * Mathf.Clamp(0.8f + damage * 0.012f, 0.8f, 2.0f);
            if (explosive) force = Mathf.Max(force, 6.5f);
            BloodStyle style = BloodFX.StyleFor(target.name);
            if (BloodFX.Level > 0) BloodFX.Death(center, direction, force, style);
            if (target.GetComponent<PlayerController>() != null) return 0f;
            if (style == BloodStyle.Scorch) return 0f;                      // hovercraft keep their own wreck logic
            return Ensure(target).PlayDeath(direction, force);
        }

        public static float ForceForWeapon(string weaponName)
        {
            switch (ModelLibrary.KindFromName(weaponName))
            {
                case ModelKind.Pistol: return 1.4f;
                case ModelKind.AssaultRifle: return 2.0f;
                case ModelKind.HeavyMachineGun: return 2.8f;
                case ModelKind.Axe: return 3.4f;
                case ModelKind.VoidRifle: return 2.6f;
                case ModelKind.AlienSword: return 3.8f;
                default: return 1.8f;
            }
        }

        private static Vector3 CenterOf(GameObject target)
        {
            Collider collider = target.GetComponent<Collider>();
            if (collider != null) return collider.bounds.center;
            return target.transform.position + Vector3.up * 0.9f;
        }

        private static Vector3 DirectionFrom(Vector3 attackerPosition, Vector3 targetPosition)
        {
            Vector3 direction = targetPosition - attackerPosition;
            direction.y = 0f;
            if (attackerPosition == Vector3.zero || direction.sqrMagnitude < 0.0001f)
            {
                float a = Random.Range(0f, 6.2831853f);
                return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            }
            return direction.normalized;
        }

        // ------------------------------------------------------------------ stagger

        public void Stagger(Vector3 direction, float force)
        {
            if (dying) return;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return;
            knockVelocity += direction.normalized * force;
            if (knockVelocity.magnitude > 5f) knockVelocity = knockVelocity.normalized * 5f;
            staggerUntil = Mathf.Max(staggerUntil, Time.time + Mathf.Clamp(force * 0.045f, 0.06f, 0.18f));
        }

        private void LateUpdate()
        {
            if (dying) return;
            if (knockVelocity.sqrMagnitude < 0.0009f)
            {
                knockVelocity = Vector3.zero;
                return;
            }
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            Vector3 allowed;
            if (TryStep(knockVelocity * dt, out allowed))
                transform.position += allowed;
            else
                knockVelocity = Vector3.zero;
            knockVelocity *= Mathf.Exp(-9f * dt);
        }

        // ------------------------------------------------------------------ death launch

        /// <summary>Starts the launched fall. Returns the settle time in seconds.</summary>
        public float PlayDeath(Vector3 direction, float force)
        {
            if (dying) return SettleSeconds;
            dying = true;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
            {
                float a = Random.Range(0f, 6.2831853f);
                direction = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            }
            StartCoroutine(DeathRoutine(direction.normalized, Mathf.Clamp(force, 1.5f, 9f)));
            return SettleSeconds;
        }

        private IEnumerator DeathRoutine(Vector3 dir, float force)
        {
            Transform t = transform;
            float groundY = t.position.y;
            Quaternion startRotation = t.rotation;
            Vector3 axis = Vector3.Cross(Vector3.up, dir);
            if (axis.sqrMagnitude < 0.0001f) axis = Vector3.right;
            axis.Normalize();

            float roll = Random.Range(8f, 28f) * (Random.value < 0.5f ? -1f : 1f);
            Vector3 velocity = dir * force;
            float vy = 2.0f + force * 0.35f;
            const float gravity = 14f;
            const float targetAngle = 88f;
            float angle = 0f;
            float elapsed = 0f;
            bool airborne = true;
            int bounces = 0;

            while (elapsed < 2.2f)
            {
                float dt = Time.deltaTime;
                if (dt <= 0f)
                {
                    yield return null;
                    continue;
                }
                elapsed += dt;

                Vector3 position = t.position;
                if (airborne)
                {
                    vy -= gravity * dt;
                    position.y += vy * dt;
                    if (position.y <= groundY && vy < 0f)
                    {
                        position.y = groundY;
                        if (bounces == 0 && Mathf.Abs(vy) > 2.2f)
                        {
                            vy = -vy * 0.28f;
                            bounces++;
                        }
                        else
                        {
                            airborne = false;
                            vy = 0f;
                        }
                    }
                }

                Vector3 allowed;
                if (TryStep(new Vector3(velocity.x, 0f, velocity.z) * dt, out allowed))
                    position += allowed;
                else
                    velocity *= 0.2f;
                velocity *= Mathf.Exp(-(airborne ? 0.4f : 7f) * dt);
                t.position = position;

                angle = Mathf.MoveTowards(angle, targetAngle, (airborne ? 520f : 300f) * dt);
                float blend = angle / targetAngle;
                t.rotation = Quaternion.AngleAxis(angle, axis) *
                             Quaternion.AngleAxis(roll * blend, dir) * startRotation;

                if (!airborne && velocity.sqrMagnitude < 0.01f && angle >= targetAngle - 0.01f) break;
                yield return null;
            }

            t.rotation = Quaternion.AngleAxis(targetAngle, axis) * Quaternion.AngleAxis(roll, dir) * startRotation;
        }

        // ------------------------------------------------------------------ collision check

        private bool TryStep(Vector3 step, out Vector3 allowed)
        {
            allowed = step;
            float distance = step.magnitude;
            if (distance < 0.0001f) return true;

            Vector3 origin = transform.position + Vector3.up * 0.5f;
            int count = Physics.RaycastNonAlloc(origin, step / distance, hitBuffer, distance + 0.35f,
                ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider collider = hitBuffer[i].collider;
                if (collider == null) continue;
                if (collider.transform.IsChildOf(transform)) continue;
                if (hitBuffer[i].normal.y > 0.6f) continue;
                if (collider.GetComponentInParent<TargetHealth>() != null) continue;
                if (collider.GetComponentInParent<PlayerController>() != null) continue;
                allowed = Vector3.zero;
                return false;
            }
            return true;
        }
    }
}
```

### FILE: UnityProject/Assets/Scripts/Combat/MuzzleFlashFX.cs
```csharp
using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    /// <summary>
    /// Pooled muzzle flash: one tiny glowing mesh per muzzle that is switched on for ~50 ms.
    /// No instantiate/destroy per shot. Call MuzzleFlashFX.Show(muzzleTransform, isAlien).
    /// </summary>
    public sealed class MuzzleFlashFX : MonoBehaviour
    {
        private const string ChildName = "PW_MuzzleFlash";
        private float hideAt;
        private Renderer flashRenderer;

        public static void Show(Transform muzzle, bool alien)
        {
            if (muzzle == null) return;
            Transform existing = muzzle.Find(ChildName);
            MuzzleFlashFX fx = null;
            if (existing != null) fx = existing.GetComponent<MuzzleFlashFX>();
            if (fx == null)
            {
                ModelKind kind = alien ? ModelKind.MuzzleFlashAlien : ModelKind.MuzzleFlash;
                GameObject go = ModelLibrary.Spawn(kind, muzzle, Vector3.zero, Quaternion.identity, 1f);
                if (go == null) return;
                go.name = ChildName;
                fx = go.AddComponent<MuzzleFlashFX>();
                fx.flashRenderer = go.GetComponent<Renderer>();
            }
            fx.Trigger();
        }

        private void Trigger()
        {
            if (flashRenderer == null) return;
            transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));
            float s = Random.Range(0.8f, 1.25f);
            transform.localScale = new Vector3(s, s, s);
            flashRenderer.enabled = true;
            hideAt = Time.time + 0.05f;
            enabled = true;
        }

        private void Update()
        {
            if (Time.time < hideAt) return;
            if (flashRenderer != null) flashRenderer.enabled = false;
            enabled = false;
        }
    }
}
```

### FILE: UnityProject/Assets/Scripts/Combat/TargetHealth.cs
```csharp
using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public sealed class TargetHealth : MonoBehaviour
    {
        [SerializeField] private int maxHealth = 100;
        public int CurrentHealth { get; private set; }
        public int MaxHealth => maxHealth;
        // Reused, visual-only hit feedback used when the violence level is 0 (energy mode).
        private ParticleSystem hitEnergyParticles;
        private float nextEnergyHitEffectTime;
        private float nextPlayerHitEffectTime;


        private void Awake()
        {
            CurrentHealth = maxHealth;
        }

        public void SetMaxHealth(int value)
        {
            maxHealth = Mathf.Max(1, value);
            CurrentHealth = maxHealth;
        }

        public void ApplyDamage(int amount, Transform attacker = null)
        {
            if (CurrentHealth <= 0) return;
            amount = Mathf.Max(0, amount);
            if (amount == 0) return;

            CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
            Vector3 attackerPosition = attacker != null ? attacker.position : Vector3.zero;
            if (CurrentHealth > 0)
            {
                if (GetComponent<EnemyChase>() != null)
                {
                    // Blood (when enabled) + a short stagger/knockback. Level 0 keeps the energy sparks.
                    HitReactor.NotifyDamaged(gameObject, attackerPosition, amount, null);
                    if (BloodFX.Level <= 0)
                        PlayEnergyHitEffect();
                }
                else if (BloodFX.Level > 0 && GetComponent<PlayerController>() != null &&
                         Time.time >= nextPlayerHitEffectTime)
                {
                    nextPlayerHitEffectTime = Time.time + 0.12f;
                    HitReactor.NotifyDamaged(gameObject, attackerPosition, amount, null);
                }
                return;
            }

            PlayerController player = GetComponent<PlayerController>();
            if (player != null)
            {
                player.HandleDefeat();
                return;
            }

            EnemyChase enemy = GetComponent<EnemyChase>();
            if (enemy != null)
            {
                int scoreBonus = Mathf.Max(0, enemy.ScoreValue - 10);
                if (StageTwoArenaManager.Instance != null)
                    StageTwoArenaManager.Instance.RegisterKill(attacker);
                if (GameSession.Instance != null)
                {
                    GameSession.Instance.RegisterEnemyDefeated();
                    GameSession.Instance.AddScore(scoreBonus);
                }

                bool isStageThreeCraft = enemy.gameObject.name.StartsWith(
                    "S3_HostileHovercraft_", System.StringComparison.Ordinal);
                if (!isStageThreeCraft)
                    SpawnDroppedWeapon(enemy);
                BeginEnemyDeathSequence(enemy, attackerPosition, amount);
                return;
            }

            Destroy(gameObject);
        }

        private void BeginEnemyDeathSequence(EnemyChase enemy, Vector3 attackerPosition, int damage)
        {
            Vector3 deathPosition = enemy != null ? enemy.transform.position : transform.position;
            bool deferCraftRemoval = enemy != null &&
                enemy.gameObject.name.StartsWith("S3_HostileHovercraft_", System.StringComparison.Ordinal);
            if (enemy != null)
            {
                // Keep the existing combat shutdown behaviour unchanged.
                enemy.enabled = false;
                Collider[] colliders = enemy.GetComponentsInChildren<Collider>();
                for (int i = 0; i < colliders.Length; i++)
                    if (colliders[i] != null) colliders[i].enabled = false;

                MonoBehaviour[] behaviours = enemy.GetComponentsInChildren<MonoBehaviour>();
                for (int i = 0; i < behaviours.Length; i++)
                {
                    if (behaviours[i] == null || behaviours[i] == this) continue;
                    if (behaviours[i] is HitReactor) continue;   // it drives the launched fall below
                    behaviours[i].enabled = false;
                }

                if (!deferCraftRemoval)
                {
                    // Blood / goo + a launched, tumbling fall. Falls back to the old tip-over if
                    // the reactor cannot run (returns 0).
                    float settle = HitReactor.NotifyKilled(enemy.gameObject, attackerPosition, damage, null, false);
                    if (settle > 0f)
                        StartCoroutine(RemoveBodyAfter(enemy.transform, Mathf.Max(1.5f, settle + 0.5f)));
                    else
                        StartCoroutine(FallAndRemoveEnemy(enemy.transform));
                }
            }

            bool isStageThreeAlien = enemy != null &&
                enemy.gameObject.name.StartsWith("S3_Alien", System.StringComparison.Ordinal);
            bool isStageThreeCraft = enemy != null &&
                (enemy.gameObject.name.StartsWith("S3_HostileHovercraft_", System.StringComparison.Ordinal) ||
                 enemy.gameObject.name.StartsWith("S3_DestroyedHovercraftWreck", System.StringComparison.Ordinal));
            bool isStageFourAlien = enemy != null &&
                enemy.gameObject.name.StartsWith("S4_AlienTrooper_", System.StringComparison.Ordinal);

            // Humans bleed (violence level > 0) and aliens keep their cyan energy discharge on top of
            // the goo. At level 0 everybody uses the energy discharge. Stage 3 craft already own a
            // dedicated vehicle-destruction effect, so do not stack a second burst.
            bool isHumanStyleTarget = !isStageThreeAlien && !isStageThreeCraft && !isStageFourAlien;
            bool showEnergy = BloodFX.Level <= 0 || !isHumanStyleTarget;
            if (!isStageThreeCraft && showEnergy)
            {
                Color energyColor = isHumanStyleTarget
                    ? new Color(1f, 0.72f, 0.28f, 0.95f)
                    : new Color(0.22f, 0.88f, 1f, 0.95f);
                SpawnEnergyDefeatEffect(deathPosition, energyColor);
            }
        }

        private System.Collections.IEnumerator RemoveBodyAfter(Transform body, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (body != null)
                Destroy(body.gameObject);
        }

        private System.Collections.IEnumerator FallAndRemoveEnemy(Transform body)
        {
            if (body == null) yield break;

            Vector3 startPosition = body.position;
            Quaternion startRotation = body.rotation;
            const float fallDuration = 0.22f;
            float elapsed = 0f;

            // Tip the enemy forward onto the ground, then leave the body visible until
            // 1.5 seconds have elapsed from the hit. No fade/scale shrink during that time.
            while (elapsed < fallDuration && body != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / fallDuration);
                body.position = startPosition + Vector3.down * (0.04f * t);
                body.rotation = startRotation * Quaternion.Euler(88f * t, 0f, 0f);
                yield return null;
            }

            if (body == null) yield break;
            body.rotation = startRotation * Quaternion.Euler(88f, 0f, 0f);
            float remaining = Mathf.Max(0f, 1.5f - fallDuration);
            if (remaining > 0f)
                yield return new WaitForSeconds(remaining);

            if (body != null)
                Destroy(body.gameObject);
        }

        /// <summary>
        /// Short-lived sci-fi energy discharge (aliens always, everybody at violence level 0).
        /// All particles are bounded and share the project's existing additive material.
        /// </summary>
        private void SpawnEnergyDefeatEffect(Vector3 position, Color color)
        {
            GameObject effectRoot = new GameObject("PW_EnemyEnergyDischarge");
            effectRoot.transform.position = position + Vector3.up * 0.22f;

            ParticleSystem sparks = effectRoot.AddComponent<ParticleSystem>();
            var main = sparks.main;
            main.duration = 0.42f;
            main.loop = false;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.22f, 0.58f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(1.1f, 3.1f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.055f, 0.14f);
            main.startColor = color;
            main.maxParticles = 28;

            var emission = sparks.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 22) });

            var shape = sparks.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 58f;
            shape.radius = 0.13f;

            var fade = sparks.colorOverLifetime;
            fade.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(Color.white, 0f),
                    new GradientColorKey(color, 0.38f),
                    new GradientColorKey(color, 1f)
                },
                new[]
                {
                    new GradientAlphaKey(0.95f, 0f),
                    new GradientAlphaKey(0.8f, 0.55f),
                    new GradientAlphaKey(0f, 1f)
                });
            fade.color = new ParticleSystem.MinMaxGradient(gradient);

            ParticleSystemRenderer particleRenderer = sparks.GetComponent<ParticleSystemRenderer>();
            Material energyMaterial = StageFourMothershipExplosionFX.GetSoftAdditiveMaterial();
            if (energyMaterial != null)
                particleRenderer.sharedMaterial = energyMaterial;
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            particleRenderer.receiveShadows = false;
            sparks.Play();

            // A separate ground pulse makes the defeat readable. It sits above road decor (GroundLift).
            GameObject ringObject = new GameObject("PW_EnergyGroundPulse");
            float groundY = PickupVisual.GroundY(position);
            ringObject.transform.position = new Vector3(position.x, groundY + ModelLibrary.GroundLift, position.z);
            LineRenderer ring = ringObject.AddComponent<LineRenderer>();
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.positionCount = 32;
            ring.alignment = LineAlignment.View;
            ring.widthMultiplier = 0.045f;
            ring.startColor = new Color(color.r, color.g, color.b, 0.9f);
            ring.endColor = new Color(color.r, color.g, color.b, 0f);
            ring.sharedMaterial = energyMaterial;
            ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ring.receiveShadows = false;
            for (int i = 0; i < ring.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2f / ring.positionCount;
                ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * 0.5f, 0f, Mathf.Sin(angle) * 0.5f));
            }
            ringObject.transform.localScale = new Vector3(0.12f, 1f, 0.12f);
            StartCoroutine(AnimateEnergyGroundPulse(ringObject.transform, ring, color));
            Destroy(effectRoot, 0.95f);
        }

        private System.Collections.IEnumerator AnimateEnergyGroundPulse(
            Transform pulseTransform, LineRenderer pulseLine, Color energyColor)
        {
            const float duration = 0.42f;
            float elapsed = 0f;
            Color initial = new Color(energyColor.r, energyColor.g, energyColor.b, 0.9f);
            Color transparent = new Color(energyColor.r, energyColor.g, energyColor.b, 0f);

            while (elapsed < duration && pulseTransform != null && pulseLine != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / duration);
                float scale = Mathf.Lerp(0.12f, 3.0f, t);
                pulseTransform.localScale = new Vector3(scale, 1f, scale);
                pulseLine.startColor = Color.Lerp(initial, transparent, t);
                pulseLine.endColor = transparent;
                yield return null;
            }

            if (pulseTransform != null)
                Destroy(pulseTransform.gameObject);
        }

        /// <summary>
        /// Visual-only hit feedback for violence level 0: a small reusable energy spark burst.
        /// </summary>
        private void PlayEnergyHitEffect()
        {
            if (Time.time < nextEnergyHitEffectTime)
                return;
            nextEnergyHitEffectTime = Time.time + 0.10f;

            if (hitEnergyParticles == null)
            {
                GameObject emitter = new GameObject("PW_EnemyEnergyHitEmitter");
                emitter.transform.SetParent(transform, false);
                emitter.transform.localPosition = Vector3.up * 0.55f;

                hitEnergyParticles = emitter.AddComponent<ParticleSystem>();
                var main = hitEnergyParticles.main;
                main.duration = 1f;
                main.loop = true;
                main.playOnAwake = false;
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.startLifetime = new ParticleSystem.MinMaxCurve(0.12f, 0.24f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.65f, 1.45f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.045f, 0.09f);
                main.startColor = ResolveEnergyColor();
                main.maxParticles = 12;

                var emission = hitEnergyParticles.emission;
                emission.rateOverTime = 0f;

                var shape = hitEnergyParticles.shape;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 66f;
                shape.radius = 0.045f;

                ParticleSystemRenderer particleRenderer = hitEnergyParticles.GetComponent<ParticleSystemRenderer>();
                Material energyMaterial = StageFourMothershipExplosionFX.GetSoftAdditiveMaterial();
                if (energyMaterial != null)
                    particleRenderer.sharedMaterial = energyMaterial;
                particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
                particleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                particleRenderer.receiveShadows = false;
                hitEnergyParticles.Play();
            }

            hitEnergyParticles.Emit(5);
        }

        private Color ResolveEnergyColor()
        {
            string enemyName = gameObject.name;
            if (enemyName.StartsWith("S3_Alien", System.StringComparison.Ordinal) ||
                enemyName.StartsWith("S4_AlienTrooper_", System.StringComparison.Ordinal))
                return new Color(0.22f, 0.88f, 1f, 0.95f);
            return new Color(1f, 0.72f, 0.28f, 0.95f);
        }

        private void SpawnDroppedWeapon(EnemyChase enemy)
        {
            if (enemy == null)
                return;

            // Keep the dropped item low to the ground and give it its own trigger
            // Rigidbody so automatic pickup events work with the player's trigger collider.
            GameObject dropped = new GameObject("DroppedWeaponPickup_" + enemy.Archetype);
            dropped.transform.position = new Vector3(transform.position.x, 0.22f, transform.position.z);
            dropped.transform.rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);

            SphereCollider trigger = dropped.AddComponent<SphereCollider>();
            trigger.isTrigger = true;
            trigger.radius = 0.8f;

            Rigidbody body = dropped.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;

            Material receiver = RuntimeMaterialFactory.Create(
                "DroppedWeaponReceiver",
                enemy.Archetype == 1 ? new Color(0.16f, 0.17f, 0.18f) : new Color(0.07f, 0.09f, 0.11f));
            Material metal = RuntimeMaterialFactory.Create(
                "DroppedWeaponMetal",
                enemy.Archetype == 2 ? new Color(0.55f, 0.47f, 0.22f) : new Color(0.38f, 0.40f, 0.40f));

            // Primitive fallback model (hidden automatically when the shared model attaches below).
            if (enemy.Archetype == 1)
            {
                CreateDroppedWeaponPart(dropped.transform, "PistolGrip",
                    new Vector3(0f, 0.02f, -0.04f), new Vector3(0.10f, 0.09f, 0.15f), receiver);
                CreateDroppedWeaponPart(dropped.transform, "PistolSlide",
                    new Vector3(0f, 0.055f, 0.06f), new Vector3(0.10f, 0.08f, 0.24f), metal);
            }
            else
            {
                CreateDroppedWeaponPart(dropped.transform,
                    enemy.Archetype == 2 ? "MachineGunBody" : "AKBody",
                    new Vector3(0f, 0.04f, 0.02f),
                    enemy.Archetype == 2 ? new Vector3(0.15f, 0.12f, 0.42f) : new Vector3(0.13f, 0.10f, 0.38f),
                    receiver);
                CreateDroppedWeaponPart(dropped.transform, "WeaponStock",
                    new Vector3(0f, 0.035f, -0.23f), new Vector3(0.10f, 0.09f, 0.20f), receiver);
                CreateDroppedWeaponPart(dropped.transform, "WeaponBarrel",
                    new Vector3(0f, 0.045f, 0.29f), new Vector3(0.055f, 0.055f, 0.24f), metal);
                CreateDroppedWeaponPart(dropped.transform, "WeaponMagazine",
                    new Vector3(0f, -0.015f, 0.02f), new Vector3(0.085f, 0.12f, 0.10f), metal);
            }

            // Enlarge the fallback primitives (PickupItem applies its own readable-pickup scale).
            for (int childIndex = 0; childIndex < dropped.transform.childCount; childIndex++)
                dropped.transform.GetChild(childIndex).localScale *= 2f;

            // Aliens leave an alien weapon (the pickup kind equals the model shown); humans leave
            // the human weapon of their archetype.
            var dropKind = enemy.DroppedWeaponKind;
            ModelKind visualKind = ModelLibrary.KindFromName(dropKind.ToString());
            if (BloodFX.StyleFor(enemy.gameObject.name) == BloodStyle.AlienGoo)
            {
                var alienKind = dropKind;
                if (System.Enum.TryParse("VoidRifle", out alienKind))
                {
                    dropKind = alienKind;
                    visualKind = ModelKind.VoidRifle;
                }
            }

            PickupItem pickup = dropped.AddComponent<PickupItem>();
            pickup.ConfigureWeapon(dropKind, 24);
            // PickupItem already scales weapon pickups x2, so keep the model at 0.8 of its default size.
            PickupVisual.Attach(dropped.transform, visualKind, true, 0.8f);
            Debug.Log("PERSIA_COMBAT: dropped enemy weapon " + dropKind);
        }

        private static void CreateDroppedWeaponPart(
            Transform parent, string partName, Vector3 localPosition, Vector3 localScale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = partName;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;

            Collider collider = part.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        public void Restore(int amount)
        {
            if (CurrentHealth <= 0) return;
            CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + Mathf.Max(0, amount));
        }
    }
}
```
