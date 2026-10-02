using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    /// <summary>
    /// Presentation-only character layer.
    /// Gameplay scripts keep owning movement, aiming, health and combat; this component
    /// only selects a character texture, billboards it toward the camera and provides
    /// a stable weapon muzzle transform.
    /// </summary>
    public sealed class StylizedCharacterVisual : MonoBehaviour
    {
        private const string HeroResource = "PersianCharacters/Hero";

        private Transform artRoot;
        private Transform muzzle;
        private SpriteRenderer sprite;
        private ParticleSystem muzzleFlash;
        private Texture2D loadedTexture;
        private Sprite loadedSprite;

        private bool moving;
        private bool playerCharacter;
        private int archetype = 1;
        private float phase;
        private float fireUntil;
        private Vector3 lastFacing = Vector3.forward;
        private Vector3 baseLocalPosition;

        public Transform Muzzle => muzzle;

        public static StylizedCharacterVisual Attach(Transform owner, bool isPlayer, int characterArchetype)
        {
            string objectName = isPlayer ? "PlayerVisual" : "EnemyVisual";
            Transform existing = owner.Find(objectName);
            StylizedCharacterVisual visual = existing != null
                ? existing.GetComponent<StylizedCharacterVisual>()
                : null;

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
            LoadCharacterSprite();
            ApplyScale();
        }

        public void ConfigurePlayerHero(int heroIndex)
        {
            playerCharacter = true;
            archetype = 1;

            EnsurePresentation();
            LoadCharacterSprite();

            int index = Mathf.Clamp(heroIndex, 0, 4);
            Color[] variants =
            {
                new Color(1.00f, 1.00f, 1.00f, 1.00f),
                new Color(0.86f, 0.95f, 1.00f, 1.00f),
                new Color(0.86f, 1.00f, 0.90f, 1.00f),
                new Color(1.00f, 0.89f, 0.93f, 1.00f),
                new Color(1.00f, 0.96f, 0.84f, 1.00f)
            };

            if (sprite != null)
                sprite.color = variants[index];
        }

        public void SetFacing(Vector3 worldDirection)
        {
            worldDirection.y = 0f;
            if (worldDirection.sqrMagnitude < 0.0005f)
                return;

            lastFacing = worldDirection.normalized;
            Quaternion target = Quaternion.LookRotation(lastFacing, Vector3.up);
            transform.rotation = Quaternion.Slerp(
                transform.rotation,
                target,
                1f - Mathf.Exp(-18f * Time.deltaTime));
        }

        public void SetMoving(bool value)
        {
            moving = value;
        }

        public void PlayFire()
        {
            fireUntil = Time.time + 0.07f;
            // Muzzle flash is intentionally lazy. It is a presentation effect, not a
            // dependency for entering the match, so do not allocate a ParticleSystem
            // during the critical Android player activation path.
            if (muzzleFlash == null)
                return;

            muzzleFlash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            muzzleFlash.Play();
        }

        private void Awake()
        {
            EnsurePresentation();
        }

        private void Update()
        {
            if (sprite == null)
                return;

            Camera cam = Camera.main;
            if (cam != null && artRoot != null)
            {
                Vector3 toCamera = cam.transform.position - artRoot.position;
                toCamera.y = 0f;
                if (toCamera.sqrMagnitude > 0.001f)
                    artRoot.rotation = Quaternion.LookRotation(toCamera.normalized, Vector3.up);
            }

            float t = Time.time + phase;
            float bob = moving ? Mathf.Abs(Mathf.Sin(t * 11f)) * 0.045f : Mathf.Sin(t * 2.2f) * 0.015f;
            sprite.transform.localPosition = new Vector3(0f, bob, 0f);

            float squash = moving
                ? 1f + Mathf.Sin(t * 11f) * 0.018f
                : 1f + Mathf.Sin(t * 2.2f) * 0.008f;

            sprite.transform.localScale = new Vector3(
                transform.localScale.x * squash,
                transform.localScale.y * (2f - squash),
                1f);

            if (muzzle != null && Time.time < fireUntil)
                muzzle.localPosition = new Vector3(0.42f, 1.05f, 0.62f);
            else if (muzzle != null)
                muzzle.localPosition = new Vector3(0.42f, 1.05f, 0.56f);
        }

        private void EnsurePresentation()
        {
            if (artRoot == null)
            {
                GameObject art = new GameObject("Art");
                art.transform.SetParent(transform, false);
                artRoot = art.transform;
            }

            if (sprite == null)
            {
                sprite = artRoot.GetComponent<SpriteRenderer>();
                if (sprite == null)
                    sprite = artRoot.gameObject.AddComponent<SpriteRenderer>();

                sprite.sortingOrder = 20;
                sprite.maskInteraction = SpriteMaskInteraction.None;
                baseLocalPosition = Vector3.zero;
            }

            if (muzzle == null)
            {
                GameObject muzzleObject = new GameObject("WeaponMuzzle");
                muzzleObject.transform.SetParent(transform, false);
                muzzle = muzzleObject.transform;
                muzzle.localPosition = new Vector3(0.42f, 1.05f, 0.56f);
            }

            if (muzzleFlash == null)
                muzzleFlash = CreateMuzzleFlash();
        }

        private void LoadCharacterSprite()
        {
            string resource = playerCharacter
                ? HeroResource
                : $"PersianCharacters/Enemy_0{archetype}";

            Texture2D texture = Resources.Load<Texture2D>(resource);
            if (texture == null)
            {
                Debug.LogError($"Missing character art resource: Resources/{resource}.png");
                sprite.sprite = null;
                return;
            }

            if (loadedTexture == texture && loadedSprite != null)
            {
                sprite.sprite = loadedSprite;
                sprite.color = Color.white;
                ApplyScale();
                return;
            }

            // FullRect avoids generating an alpha-derived tight mesh from an imported
            // non-readable texture. This keeps Android sprite activation on the simple
            // renderer path and removes an unnecessary CPU-side texture dependency.
            loadedSprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.08f),
                64f,
                0,
                SpriteMeshType.FullRect);
            loadedTexture = texture;
            sprite.sprite = loadedSprite;
            sprite.color = Color.white;
            ApplyScale();
        }

        private void ApplyScale()
        {
            float scale = archetype == 3 ? 1.16f : archetype == 2 ? 1.05f : 1f;
            transform.localScale = playerCharacter
                ? Vector3.one * 1.06f
                : Vector3.one * scale;
        }

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
                new[] { new GradientColorKey(new Color(1f, 0.75f, 0.18f), 0f), new GradientColorKey(new Color(1f, 0.2f, 0.03f), 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            colorOverLifetime.color = gradient;

            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            return ps;
        }
    }
}
