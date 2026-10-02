using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace PersiaWar.Unity2D5D
{
    /// <summary>
    /// Presentation-only character layer.
    /// Android uses a tiny untextured mesh during gameplay to avoid texture-backed
    /// SpriteRenderer work during the Mali-sensitive first render transition.
    /// </summary>
    public sealed class StylizedCharacterVisual : MonoBehaviour
    {
        private const string HeroResource = "PersianCharacters/Hero";
        private static readonly Dictionary<string, Sprite> SpriteCache = new Dictionary<string, Sprite>();

        private Transform artRoot;
        private Transform muzzle;
#if UNITY_ANDROID
        private MeshFilter androidMeshFilter;
        private MeshRenderer androidRenderer;
#else
        private SpriteRenderer sprite;
#endif
        private ParticleSystem muzzleFlash;

        private bool moving;
        private bool playerCharacter;
        private int archetype = 1;
        private float phase;
        private float fireUntil;

        public Transform Muzzle => muzzle;

        public static StylizedCharacterVisual Attach(Transform owner, bool isPlayer, int characterArchetype)
        {
            string objectName = isPlayer ? "PlayerVisual" : "EnemyVisual";
            Transform existing = owner.Find(objectName);
            StylizedCharacterVisual visual = existing != null ? existing.GetComponent<StylizedCharacterVisual>() : null;

            if (visual == null)
            {
                GameObject root = new GameObject(objectName);
                root.transform.SetParent(owner, false);
                visual = root.AddComponent<StylizedCharacterVisual>();
            }

            visual.Configure(isPlayer, characterArchetype);
            return visual;
        }

        public void Configure(bool isPlayer, int characterArchetype)
        {
            playerCharacter = isPlayer;
            archetype = Mathf.Clamp(characterArchetype, 1, 3);

            EnsurePresentation();
            LoadCharacterPresentation();
            ApplyScale();
        }

        public void ConfigurePlayerHero(int heroIndex)
        {
            playerCharacter = true;
            archetype = 1;

            EnsurePresentation();
            LoadCharacterPresentation();

            int index = Mathf.Clamp(heroIndex, 0, 4);
            Color[] variants =
            {
                new Color(1f, 1f, 1f, 1f),
                new Color(0.86f, 0.95f, 1f, 1f),
                new Color(0.86f, 1f, 0.90f, 1f),
                new Color(1f, 0.89f, 0.93f, 1f),
                new Color(1f, 0.96f, 0.84f, 1f)
            };

#if UNITY_ANDROID
            if (androidRenderer != null)
                androidRenderer.sharedMaterial = RuntimeMaterialFactory.Create("AndroidPlayerVisual", variants[index]);
#else
            if (sprite != null)
                sprite.color = variants[index];
#endif
        }

        public void SetFacing(Vector3 worldDirection)
        {
            worldDirection.y = 0f;
            if (worldDirection.sqrMagnitude < 0.0005f)
                return;

            Quaternion target = Quaternion.LookRotation(worldDirection.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                target,
                1f - Mathf.Exp(-18f * Time.deltaTime));
        }

        public void SetMoving(bool value) => moving = value;

        public void PlayFire()
        {
            fireUntil = Time.time + 0.07f;
#if UNITY_ANDROID
            // No runtime particle allocation on the Android compatibility path.
            return;
#else
            if (muzzleFlash == null)
                muzzleFlash = CreateMuzzleFlash();

            if (muzzleFlash == null)
                return;

            muzzleFlash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            muzzleFlash.Play();
#endif
        }

        private void Awake()
        {
            EnsurePresentation();
        }

        private void Update()
        {
            Camera cam = Camera.main;
            if (cam != null && artRoot != null)
            {
                Vector3 toCamera = cam.transform.position - artRoot.position;
                toCamera.y = 0f;
                if (toCamera.sqrMagnitude > 0.001f)
                    artRoot.rotation = Quaternion.LookRotation(toCamera.normalized, Vector3.up);
            }

            float t = Time.time + phase;
            float bob = moving
                ? Mathf.Abs(Mathf.Sin(t * 11f)) * 0.045f
                : Mathf.Sin(t * 2.2f) * 0.015f;

            float squash = moving
                ? 1f + Mathf.Sin(t * 11f) * 0.018f
                : 1f + Mathf.Sin(t * 2.2f) * 0.008f;

#if UNITY_ANDROID
            if (artRoot != null)
            {
                artRoot.localPosition = new Vector3(0f, bob, 0f);
                artRoot.localScale = new Vector3(
                    squash * 1.6f,
                    (2f - squash) * 1.6f,
                    1f);
            }
#else
            if (sprite == null)
                return;

            sprite.transform.localPosition = new Vector3(0f, bob, 0f);
            sprite.transform.localScale = new Vector3(
                transform.localScale.x * squash,
                transform.localScale.y * (2f - squash),
                1f);

            if (muzzle != null && Time.time < fireUntil)
                muzzle.localPosition = new Vector3(0.42f, 1.05f, 0.62f);
            else if (muzzle != null)
                muzzle.localPosition = new Vector3(0.42f, 1.05f, 0.56f);
#endif
        }

        private void EnsurePresentation()
        {
            if (artRoot == null)
            {
                GameObject art = new GameObject("Art");
                art.transform.SetParent(transform, false);
                artRoot = art.transform;
            }

#if UNITY_ANDROID
            if (androidMeshFilter == null)
            {
                androidMeshFilter = artRoot.GetComponent<MeshFilter>();
                if (androidMeshFilter == null)
                    androidMeshFilter = artRoot.gameObject.AddComponent<MeshFilter>();

                androidRenderer = artRoot.GetComponent<MeshRenderer>();
                if (androidRenderer == null)
                    androidRenderer = artRoot.gameObject.AddComponent<MeshRenderer>();

                androidMeshFilter.sharedMesh = CreateAndroidQuad();
                androidRenderer.sharedMaterial = RuntimeMaterialFactory.Create("AndroidCharacter", Color.white);
                androidRenderer.shadowCastingMode = ShadowCastingMode.Off;
                androidRenderer.receiveShadows = false;
                androidRenderer.lightProbeUsage = LightProbeUsage.Off;
                androidRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
#else
            if (sprite == null)
            {
                sprite = artRoot.GetComponent<SpriteRenderer>();
                if (sprite == null)
                    sprite = artRoot.gameObject.AddComponent<SpriteRenderer>();

                sprite.sortingOrder = 20;
                sprite.maskInteraction = SpriteMaskInteraction.None;
            }
#endif

            if (muzzle == null)
            {
                GameObject muzzleObject = new GameObject("WeaponMuzzle");
                muzzleObject.transform.SetParent(transform, false);
                muzzle = muzzleObject.transform;
                muzzle.localPosition = new Vector3(0.42f, 1.05f, 0.56f);
            }
        }

        private void LoadCharacterPresentation()
        {
#if UNITY_ANDROID
            Color color = playerCharacter
                ? Color.white
                : archetype == 1
                    ? new Color(0.86f, 0.22f, 0.16f, 1f)
                    : archetype == 2
                        ? new Color(0.24f, 0.60f, 0.94f, 1f)
                        : new Color(0.72f, 0.30f, 0.86f, 1f);

            if (androidRenderer != null)
            {
                androidRenderer.sharedMaterial = RuntimeMaterialFactory.Create(
                    playerCharacter ? "AndroidPlayerVisual" : "AndroidEnemyVisual",
                    color);
            }

            ApplyScale();
            StartupCheckpoint.Set(playerCharacter ? "PlayerVisualReadyAndroid" : "EnemyVisualReadyAndroid");
#else
            string resource = playerCharacter
                ? HeroResource
                : $"PersianCharacters/Enemy_0{archetype}";

            if (!SpriteCache.TryGetValue(resource, out Sprite loaded) || loaded == null)
            {
                loaded = Resources.Load<Sprite>(resource);
                if (loaded != null)
                    SpriteCache[resource] = loaded;
            }

            if (loaded == null)
            {
                Debug.LogError($"Missing character sprite resource: Resources/{resource}.png");
                StartupCheckpoint.Set("CharacterSpriteMissing");
                sprite.sprite = null;
                return;
            }

            sprite.sprite = loaded;
            sprite.color = Color.white;
            ApplyScale();
            StartupCheckpoint.Set(playerCharacter ? "PlayerSpriteReady" : "EnemySpriteReady");
#endif
        }

        private void ApplyScale()
        {
            float scale = archetype == 3 ? 1.16f : archetype == 2 ? 1.05f : 1f;
            transform.localScale = playerCharacter
                ? Vector3.one * 1.06f
                : Vector3.one * scale;
        }

#if UNITY_ANDROID
        private static Mesh CreateAndroidQuad()
        {
            Mesh mesh = new Mesh { name = "AndroidCharacterQuad" };
            mesh.vertices = new[]
            {
                new Vector3(-0.42f, 0f, 0f),
                new Vector3(0.42f, 0f, 0f),
                new Vector3(0.42f, 1.15f, 0f),
                new Vector3(-0.42f, 1.15f, 0f)
            };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.normals = new[]
            {
                Vector3.forward, Vector3.forward, Vector3.forward, Vector3.forward
            };
            mesh.uv = new[]
            {
                new Vector2(0f, 0f), new Vector2(1f, 0f),
                new Vector2(1f, 1f), new Vector2(0f, 1f)
            };
            mesh.RecalculateBounds();
            return mesh;
        }
#endif

#if !UNITY_ANDROID
        private ParticleSystem CreateMuzzleFlash()
        {
            GameObject go = new GameObject("MuzzleFlash");
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0.42f, 1.05f, 0.66f);

            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.duration = 0.06f;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.035f, 0.055f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.35f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.16f);
            main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            main.maxParticles = 6;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 3, 4) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.03f;

            var renderer = ps.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;

            var colorOverLifetime = ps.colorOverLifetime;
            colorOverLifetime.enabled = true;
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new[]
                {
                    new GradientColorKey(new Color(1f, 0.75f, 0.18f), 0f),
                    new GradientColorKey(new Color(1f, 0.2f, 0.03f), 1f)
                },
                new[]
                {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0f, 1f)
                });
            colorOverLifetime.color = gradient;

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return ps;
        }
#endif
    }
}
