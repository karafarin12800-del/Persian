using System.Collections.Generic;
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
        private static readonly Dictionary<string, Sprite> SpriteCache = new Dictionary<string, Sprite>();

        private Transform artRoot;
        private Transform muzzle;
        private SpriteRenderer sprite;
        private SpriteRenderer shadowSprite;
        private ParticleSystem muzzleFlash;

        private bool moving;
        private bool playerCharacter;
        private bool portraitMode;
        private float smoothedMoveAmount;
        private PlayerController cachedPlayerController;
        private int archetype = 1;
        private float phase;
        private float fireUntil;
        private Vector3 lastFacing = Vector3.forward;
        private Vector3 baseLocalPosition;

        private Transform mobileModelRoot;
        private Renderer mobileBody;
        private Renderer mobileHead;
        private Renderer mobileHair;
        private Renderer mobileShirt;
        private Renderer mobilePants;
        private Renderer mobileShoes;
        private Renderer mobileArmor;
        private Renderer mobileLeftArm;
        private Renderer mobileRightArm;
        private Renderer mobilePack;
        private Renderer mobileHelmet;
        private Renderer mobileVisor;
        private Renderer mobileFemaleHairLeft;
        private Renderer mobileFemaleHairRight;
        private Renderer mobileFemaleHairCap;
        private Renderer mobileLeftLeg;
        private Renderer mobileRightLeg;
        private Transform[] heroAccessoryRoots;
        private static readonly System.Collections.Generic.Dictionary<int, Material> LitCharacterMaterialCache =
            new System.Collections.Generic.Dictionary<int, Material>();

        public Transform Muzzle => muzzle;
        public Transform WeaponMount => mobileModelRoot != null ? mobileModelRoot : transform;

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
            if (Application.isMobilePlatform)
                EnsureMobile3DCharacter();
        }

        public void ConfigurePortraitModel(int heroIndex)
        {
            portraitMode = true;
            playerCharacter = true;
            archetype = 1;
            EnsurePresentation();
            LoadCharacterSprite();
            EnsureMobile3DCharacter();
            ApplyMobilePlayerColors(Mathf.Clamp(heroIndex, 0, 5));
        }

        public void ConfigurePlayerHero(int heroIndex)
        {
            playerCharacter = true;
            archetype = 1;

            EnsurePresentation();
            LoadCharacterSprite();

            int index = Mathf.Clamp(heroIndex, 0, 5);
            Color[] variants =
            {
                new Color(1.00f, 1.00f, 1.00f, 1.00f),
                new Color(1.00f, 0.87f, 0.95f, 1.00f),
                new Color(0.86f, 0.95f, 1.00f, 1.00f),
                new Color(0.86f, 1.00f, 0.90f, 1.00f),
                new Color(1.00f, 0.89f, 0.93f, 1.00f),
                new Color(1.00f, 0.96f, 0.84f, 1.00f)
            };

            if (sprite != null)
                sprite.color = variants[index];

            if (Application.isMobilePlatform)
            {
                EnsureMobile3DCharacter();
                ApplyMobilePlayerColors(index);
            }
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
            // The muzzle flash is a presentation effect, not a dependency for entering
            // the match. Allocate it only when the first shot is actually fired.
            if (muzzleFlash == null)
                muzzleFlash = CreateMuzzleFlash();
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

            Camera cam = portraitMode ? null : Camera.main;
            if (cam != null && artRoot != null)
            {
                Vector3 toCamera = cam.transform.position - artRoot.position;
                toCamera.y = 0f;
                if (toCamera.sqrMagnitude > 0.001f)
                    artRoot.rotation = Quaternion.LookRotation(toCamera.normalized, Vector3.up);
            }

            if (playerCharacter && !portraitMode && cachedPlayerController == null)
                cachedPlayerController = GetComponentInParent<PlayerController>();
            float targetMove = cachedPlayerController != null ? cachedPlayerController.Speed01 : (moving ? 1f : 0f);
            smoothedMoveAmount = Mathf.MoveTowards(smoothedMoveAmount, targetMove, Time.deltaTime * 6.5f);
            float t = Time.time + phase;
            float bob = smoothedMoveAmount > 0.025f
                ? Mathf.Abs(Mathf.Sin(t * 11f)) * 0.045f * smoothedMoveAmount
                : Mathf.Sin(t * 2.2f) * 0.015f;
            sprite.transform.localPosition = new Vector3(0f, bob, 0f);

            float squash = smoothedMoveAmount > 0.025f
                ? 1f + Mathf.Sin(t * 11f) * 0.018f * smoothedMoveAmount
                : 1f + Mathf.Sin(t * 2.2f) * 0.008f;

            sprite.transform.localScale = new Vector3(
                transform.localScale.x * squash,
                transform.localScale.y * (2f - squash),
                1f);

            if (mobileModelRoot != null)
            {
                mobileModelRoot.localPosition = new Vector3(0f, bob, 0f);
                float walk = Mathf.Sin(t * 11f) * 24f * smoothedMoveAmount;
                if (mobileLeftArm != null) mobileLeftArm.transform.localRotation = Quaternion.Euler(walk, 0f, -7f * smoothedMoveAmount);
                if (mobileRightArm != null) mobileRightArm.transform.localRotation = Quaternion.Euler(-walk, 0f, 7f * smoothedMoveAmount);
                if (mobileLeftLeg != null) mobileLeftLeg.transform.localRotation = Quaternion.Euler(-walk * .65f, 0f, 0f);
                if (mobileRightLeg != null) mobileRightLeg.transform.localRotation = Quaternion.Euler(walk * .65f, 0f, 0f);
                if (!portraitMode && cachedPlayerController != null)
                    mobileModelRoot.localRotation = Quaternion.Euler(0f, 0f, -cachedPlayerController.MoveInput.x * 5f * smoothedMoveAmount);
                if (Time.time < fireUntil && mobileRightArm != null)
                    mobileRightArm.transform.localRotation = Quaternion.Euler(-10f, 0f, 5f);
            }

            if (muzzle != null && Time.time < fireUntil)
                muzzle.localPosition = new Vector3(0.42f, 1.05f, 0.62f);
            else if (muzzle != null)
                muzzle.localPosition = new Vector3(0.42f, 1.05f, 0.56f);
        }


        private void EnsureMobile3DCharacter()
        {
            if (mobileModelRoot != null)
                return;

            mobileModelRoot = new GameObject("Mobile3DModel").transform;
            mobileModelRoot.SetParent(artRoot, false);
            mobileModelRoot.localPosition = Vector3.zero;
            mobileModelRoot.localRotation = Quaternion.identity;
            mobileModelRoot.localScale = Vector3.one;

            if (!playerCharacter)
            {
                // Android enemy silhouettes use a reduced primitive set to avoid a native
                // allocation spike when a wave is created. The player keeps the full model.
                mobileHead = CreateMobilePrimitive(PrimitiveType.Sphere, "Head", mobileModelRoot,
                    new Vector3(0f, 1.35f, 0f), new Vector3(0.62f, 0.62f, 0.62f));
                mobileBody = CreateMobilePrimitive(PrimitiveType.Capsule, "Body", mobileModelRoot,
                    new Vector3(0f, 0.72f, 0f), new Vector3(0.72f, 0.82f, 0.52f));
                mobileShirt = CreateMobilePrimitive(PrimitiveType.Cube, "Shirt", mobileModelRoot,
                    new Vector3(0f, 0.78f, 0f), new Vector3(0.78f, 0.42f, 0.56f));
                mobilePants = CreateMobilePrimitive(PrimitiveType.Cube, "Pants", mobileModelRoot,
                    new Vector3(0f, 0.28f, 0f), new Vector3(0.62f, 0.28f, 0.48f));
                mobileShoes = null;
                mobileHair = null;
                mobileArmor = null;
                mobileLeftArm = CreateMobilePrimitive(PrimitiveType.Capsule, "LeftArm", mobileModelRoot,
                    new Vector3(-0.47f, 0.76f, 0.03f), new Vector3(0.20f, 0.50f, 0.20f));
                mobileRightArm = CreateMobilePrimitive(PrimitiveType.Capsule, "RightArm", mobileModelRoot,
                    new Vector3(0.47f, 0.76f, 0.03f), new Vector3(0.20f, 0.50f, 0.20f));
                mobilePack = null;
                mobileHelmet = CreateMobilePrimitive(PrimitiveType.Sphere, "Helmet", mobileModelRoot,
                    new Vector3(0f, 1.57f, 0.01f), new Vector3(0.70f, 0.38f, 0.66f));
                mobileVisor = null;
            }
            else
            {
                mobileHead = CreateMobilePrimitive(PrimitiveType.Sphere, "Head", mobileModelRoot,
                    new Vector3(0f, 1.35f, 0f), new Vector3(0.62f, 0.62f, 0.62f));
                mobileBody = CreateMobilePrimitive(PrimitiveType.Capsule, "Body", mobileModelRoot,
                    new Vector3(0f, 0.72f, 0f), new Vector3(0.72f, 0.82f, 0.52f));
                mobileShirt = CreateMobilePrimitive(PrimitiveType.Cube, "Shirt", mobileModelRoot,
                    new Vector3(0f, 0.78f, 0f), new Vector3(0.78f, 0.42f, 0.56f));
                mobilePants = CreateMobilePrimitive(PrimitiveType.Cube, "Pants", mobileModelRoot,
                    new Vector3(0f, 0.28f, 0f), new Vector3(0.62f, 0.28f, 0.48f));
                mobileLeftLeg = CreateMobilePrimitive(PrimitiveType.Capsule, "LeftLeg", mobileModelRoot,
                    new Vector3(-0.18f, 0.31f, 0f), new Vector3(0.20f, 0.40f, 0.20f));
                mobileRightLeg = CreateMobilePrimitive(PrimitiveType.Capsule, "RightLeg", mobileModelRoot,
                    new Vector3(0.18f, 0.31f, 0f), new Vector3(0.20f, 0.40f, 0.20f));
                mobileShoes = CreateMobilePrimitive(PrimitiveType.Cube, "Shoes", mobileModelRoot,
                    new Vector3(0f, 0.05f, 0f), new Vector3(0.78f, 0.14f, 0.56f));
                mobileHair = CreateMobilePrimitive(PrimitiveType.Cylinder, "Hair", mobileModelRoot,
                    new Vector3(0f, 1.67f, 0f), new Vector3(0.48f, 0.16f, 0.48f));
                mobileArmor = CreateMobilePrimitive(PrimitiveType.Cube, "Armor", mobileModelRoot,
                    new Vector3(0f, 0.92f, 0.10f), new Vector3(0.66f, 0.48f, 0.20f));
                mobileLeftArm = CreateMobilePrimitive(PrimitiveType.Capsule, "LeftArm", mobileModelRoot,
                    new Vector3(-0.47f, 0.76f, 0.03f), new Vector3(0.20f, 0.50f, 0.20f));
                mobileRightArm = CreateMobilePrimitive(PrimitiveType.Capsule, "RightArm", mobileModelRoot,
                    new Vector3(0.47f, 0.76f, 0.03f), new Vector3(0.20f, 0.50f, 0.20f));
                mobilePack = CreateMobilePrimitive(PrimitiveType.Cube, "Backpack", mobileModelRoot,
                    new Vector3(0f, 0.88f, -0.30f), new Vector3(0.46f, 0.55f, 0.20f));
                mobileHelmet = CreateMobilePrimitive(PrimitiveType.Sphere, "Helmet", mobileModelRoot,
                    new Vector3(0f, 1.57f, 0.01f), new Vector3(0.70f, 0.38f, 0.66f));
                mobileVisor = CreateMobilePrimitive(PrimitiveType.Cube, "Visor", mobileModelRoot,
                    new Vector3(0f, 1.49f, 0.31f), new Vector3(0.44f, 0.15f, 0.10f));
                mobileFemaleHairLeft = CreateMobilePrimitive(PrimitiveType.Capsule, "AnahitaLongHairLeft", mobileModelRoot,
                    new Vector3(-0.25f, 1.16f, -0.06f), new Vector3(0.14f, 0.32f, 0.16f));
                mobileFemaleHairRight = CreateMobilePrimitive(PrimitiveType.Capsule, "AnahitaLongHairRight", mobileModelRoot,
                    new Vector3(0.25f, 1.16f, -0.06f), new Vector3(0.14f, 0.32f, 0.16f));
                mobileFemaleHairCap = CreateMobilePrimitive(PrimitiveType.Sphere, "AnahitaHairCap", mobileModelRoot,
                    new Vector3(0f, 1.60f, -0.015f), new Vector3(0.37f, 0.23f, 0.34f));
                mobileFemaleHairLeft.enabled = false;
                mobileFemaleHairRight.enabled = false;
                mobileFemaleHairCap.enabled = false;
            }

            Renderer[] parts =
            {
                mobileHead, mobileBody, mobileShirt, mobilePants, mobileShoes, mobileHair,
                mobileArmor, mobileLeftArm, mobileRightArm, mobilePack, mobileHelmet, mobileVisor,
                mobileFemaleHairLeft, mobileFemaleHairRight, mobileFemaleHairCap
            };
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == null) continue;
                parts[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                parts[i].receiveShadows = false;
            }

            if (sprite != null)
                sprite.enabled = false;
            if (shadowSprite != null)
                shadowSprite.enabled = false;

            if (playerCharacter)
                ApplyMobilePlayerColors(0);
            else
                ApplyMobileEnemyColors(archetype);

            if (!playerCharacter)
                StartupCheckpoint.Set("AndroidEnemyVisualReady");

            transform.localScale = Vector3.one;
            StartupCheckpoint.Set("Mobile3DCharacterReady");
        }

        private Renderer CreateMobilePrimitive(
            PrimitiveType primitiveType,
            string partName,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale)
        {
            GameObject part = GameObject.CreatePrimitive(primitiveType);
            part.name = partName;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;

            Collider collider = part.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer != null)
                renderer.sharedMaterial = RuntimeMaterialFactory.Create(
                    "Mobile_" + partName,
                    Color.white);

            return renderer;
        }

        private void ApplyMobilePlayerColors(int heroIndex)
        {
            bool isAnahita = heroIndex == 1;
            Color shirtColor = isAnahita ? new Color(0.82f, 0.22f, 0.50f)
                : heroIndex == 0 ? new Color(0.08f, 0.26f, 0.52f)
                : heroIndex == 2 ? new Color(0.10f, 0.42f, 0.52f)
                : heroIndex == 3 ? new Color(0.16f, 0.46f, 0.24f)
                : heroIndex == 4 ? new Color(0.42f, 0.16f, 0.46f)
                : new Color(0.48f, 0.32f, 0.08f);

            Color hairColor = heroIndex == 0
                ? new Color(0.56f, 0.20f, 0.08f)
                : new Color(0.10f, 0.055f, 0.035f);

            // Restore the common base measurements first; selecting another hero
            // after Anahita must not leave the smaller silhouette or dress behind.
            if (mobileHead != null)
                mobileHead.transform.localScale = new Vector3(0.62f, 0.62f, 0.62f);
            if (mobileBody != null)
                mobileBody.transform.localScale = new Vector3(0.72f, 0.82f, 0.52f);
            if (mobileShirt != null)
                mobileShirt.transform.localScale = new Vector3(0.78f, 0.42f, 0.56f);
            if (mobileLeftArm != null)
            {
                mobileLeftArm.transform.localPosition = new Vector3(-0.47f, 0.76f, 0.03f);
                mobileLeftArm.transform.localScale = new Vector3(0.20f, 0.50f, 0.20f);
            }
            if (mobileRightArm != null)
            {
                mobileRightArm.transform.localPosition = new Vector3(0.47f, 0.76f, 0.03f);
                mobileRightArm.transform.localScale = new Vector3(0.20f, 0.50f, 0.20f);
            }
            if (mobileHair != null)
                mobileHair.transform.localScale = new Vector3(0.48f, 0.16f, 0.48f);
            if (mobileModelRoot != null)
                mobileModelRoot.localScale = isAnahita ? Vector3.one * 0.82f : Vector3.one;

            SetRendererColor(mobileHead, new Color(0.78f, 0.53f, 0.34f));
            SetRendererColor(mobileBody, shirtColor * 0.85f);
            SetRendererColor(mobileShirt, shirtColor);
            SetRendererColor(mobilePants, isAnahita ? new Color(0.70f, 0.17f, 0.43f) : new Color(0.08f, 0.12f, 0.20f));
            SetRendererColor(mobileLeftLeg, isAnahita ? new Color(0.70f, 0.17f, 0.43f) : new Color(0.08f, 0.12f, 0.20f));
            SetRendererColor(mobileRightLeg, isAnahita ? new Color(0.70f, 0.17f, 0.43f) : new Color(0.08f, 0.12f, 0.20f));
            SetRendererColor(mobileShoes, new Color(0.10f, 0.11f, 0.13f));
            SetRendererColor(mobileHair, hairColor);
            SetRendererColor(mobileArmor, Color.Lerp(shirtColor, Color.white, 0.16f));
            SetRendererColor(mobileLeftArm, shirtColor);
            SetRendererColor(mobileRightArm, shirtColor);
            SetRendererColor(mobilePack, new Color(0.07f, 0.09f, 0.12f));
            SetRendererColor(mobileHelmet, new Color(0.13f, 0.16f, 0.19f));
            SetRendererColor(mobileVisor, new Color(0.18f, 0.48f, 0.58f));

            if (isAnahita)
            {
                // Smaller shoulders, a narrower torso, a slightly smaller head and a
                // skirt-shaped lower garment give her a more feminine silhouette.
                if (mobileHead != null)
                    mobileHead.transform.localScale = new Vector3(0.56f, 0.58f, 0.55f);
                if (mobileBody != null)
                    mobileBody.transform.localScale = new Vector3(0.62f, 0.75f, 0.46f);
                if (mobileShirt != null)
                {
                    mobileShirt.transform.localPosition = new Vector3(0f, 0.79f, 0f);
                    mobileShirt.transform.localScale = new Vector3(0.65f, 0.40f, 0.48f);
                }
                if (mobileLeftArm != null)
                    mobileLeftArm.transform.localScale = new Vector3(0.16f, 0.43f, 0.17f);
                if (mobileRightArm != null)
                    mobileRightArm.transform.localScale = new Vector3(0.16f, 0.43f, 0.17f);
                if (mobileHair != null)
                    mobileHair.transform.localScale = new Vector3(0.52f, 0.18f, 0.52f);
            }

            if (mobilePants != null)
            {
                mobilePants.transform.localPosition = isAnahita
                    ? new Vector3(0f, 0.37f, 0f)
                    : new Vector3(0f, 0.28f, 0f);
                mobilePants.transform.localScale = isAnahita
                    ? new Vector3(0.66f, 0.38f, 0.52f)
                    : new Vector3(0.62f, 0.28f, 0.48f);
            }
            if (mobileHelmet != null) mobileHelmet.enabled = !isAnahita;
            if (mobileVisor != null) mobileVisor.enabled = !isAnahita;
            if (mobileArmor != null) mobileArmor.enabled = !isAnahita;
            if (mobilePack != null) mobilePack.enabled = !isAnahita;
            if (mobileFemaleHairLeft != null)
            {
                mobileFemaleHairLeft.enabled = isAnahita;
                SetRendererColor(mobileFemaleHairLeft, hairColor);
            }
            if (mobileFemaleHairRight != null)
            {
                mobileFemaleHairRight.enabled = isAnahita;
                SetRendererColor(mobileFemaleHairRight, hairColor);
            }
            if (mobileFemaleHairCap != null)
            {
                mobileFemaleHairCap.enabled = isAnahita;
                SetRendererColor(mobileFemaleHairCap, hairColor);
            }
            EnsureHeroAccessories();
            for (int i = 0; i < heroAccessoryRoots.Length; i++)
                heroAccessoryRoots[i].gameObject.SetActive(i == heroIndex);
        }

        private void EnsureHeroAccessories()
        {
            if (heroAccessoryRoots != null || mobileModelRoot == null) return;
            heroAccessoryRoots = new Transform[6];
            for (int i = 0; i < heroAccessoryRoots.Length; i++)
            {
                Transform group = new GameObject("HeroAccessory_" + i).transform;
                group.SetParent(mobileModelRoot, false);
                heroAccessoryRoots[i] = group;
                Color gold = new Color(0.96f, 0.70f, 0.18f);
                Color shirt = i == 0 ? new Color(0.46f,0.12f,0.08f) : i == 1 ? new Color(0.85f,0.25f,0.52f) :
                    i == 2 ? new Color(0.10f,0.55f,0.63f) : i == 3 ? new Color(0.12f,0.25f,0.17f) :
                    i == 4 ? new Color(0.46f,0.16f,0.52f) : new Color(0.56f,0.39f,0.13f);
                if (i == 0)
                {
                    SetRendererColor(CreateMobilePrimitive(PrimitiveType.Cube,"RoyalCrownBand",group,new Vector3(0,1.70f,0.02f),new Vector3(.52f,.09f,.48f)),gold);
                    for(int spike=0;spike<3;spike++) SetRendererColor(CreateMobilePrimitive(PrimitiveType.Cube,"RoyalCrownSpike_"+spike,group,new Vector3((spike-1)*.16f,1.81f,.02f),new Vector3(.07f,.16f,.08f)),gold);
                    SetRendererColor(CreateMobilePrimitive(PrimitiveType.Cube,"RoyalCape",group,new Vector3(0,.76f,-.31f),new Vector3(.65f,.70f,.12f)),shirt);
                }
                else if (i == 1)
                {
                    SetRendererColor(CreateMobilePrimitive(PrimitiveType.Cube,"AnahitaSash",group,new Vector3(.03f,.90f,.30f),new Vector3(.12f,.56f,.05f)),gold);
                    SetRendererColor(CreateMobilePrimitive(PrimitiveType.Sphere,"AnahitaShoulderLight",group,new Vector3(-.39f,.98f,.03f),new Vector3(.20f,.18f,.20f)),new Color(.85f,.82f,.73f));
                }
                else if (i == 2)
                {
                    SetRendererColor(CreateMobilePrimitive(PrimitiveType.Cube,"GuardShoulderLeft",group,new Vector3(-.48f,1.03f,0),new Vector3(.30f,.23f,.34f)),shirt);
                    SetRendererColor(CreateMobilePrimitive(PrimitiveType.Cube,"GuardShoulderRight",group,new Vector3(.48f,1.03f,0),new Vector3(.30f,.23f,.34f)),shirt);
                    SetRendererColor(CreateMobilePrimitive(PrimitiveType.Cube,"GuardChestPlate",group,new Vector3(0,.88f,.23f),new Vector3(.46f,.30f,.09f)),new Color(.15f,.72f,.72f));
                }
                else if (i == 3)
                {
                    SetRendererColor(CreateMobilePrimitive(PrimitiveType.Sphere,"ScoutHood",group,new Vector3(0,1.51f,-.05f),new Vector3(.70f,.55f,.65f)),shirt);
                    SetRendererColor(CreateMobilePrimitive(PrimitiveType.Cube,"ScoutQuiver",group,new Vector3(.25f,.94f,-.37f),new Vector3(.18f,.58f,.16f)),new Color(.36f,.25f,.12f));
                }
                else if (i == 4)
                {
                    SetRendererColor(CreateMobilePrimitive(PrimitiveType.Cube,"SilkScarf",group,new Vector3(0,1.14f,.20f),new Vector3(.70f,.12f,.12f)),new Color(.82f,.20f,.28f));
                    SetRendererColor(CreateMobilePrimitive(PrimitiveType.Cube,"SilkWristBladeLeft",group,new Vector3(-.52f,.65f,.15f),new Vector3(.08f,.27f,.08f)),gold);
                    SetRendererColor(CreateMobilePrimitive(PrimitiveType.Cube,"SilkWristBladeRight",group,new Vector3(.52f,.65f,.15f),new Vector3(.08f,.27f,.08f)),gold);
                }
                else
                {
                    SetRendererColor(CreateMobilePrimitive(PrimitiveType.Sphere,"DesertTurban",group,new Vector3(0,1.69f,0),new Vector3(.68f,.28f,.62f)),new Color(.78f,.68f,.46f));
                    SetRendererColor(CreateMobilePrimitive(PrimitiveType.Cube,"DesertCloak",group,new Vector3(0,.78f,-.30f),new Vector3(.70f,.76f,.12f)),shirt);
                    SetRendererColor(CreateMobilePrimitive(PrimitiveType.Cube,"DesertBelt",group,new Vector3(0,.51f,.20f),new Vector3(.64f,.09f,.09f)),gold);
                }
                group.gameObject.SetActive(false);
            }
        }

        private void ApplyMobileEnemyColors(int archetypeIndex)
        {
            Color body = archetypeIndex == 1 ? new Color(0.48f, 0.15f, 0.12f)
                : archetypeIndex == 2 ? new Color(0.18f, 0.34f, 0.18f)
                : new Color(0.32f, 0.20f, 0.42f);

            SetRendererColor(mobileHead, new Color(0.66f, 0.43f, 0.28f));
            SetRendererColor(mobileBody, body * 0.85f);
            SetRendererColor(mobileShirt, body);
            SetRendererColor(mobilePants, new Color(0.09f, 0.09f, 0.12f));
            SetRendererColor(mobileShoes, new Color(0.07f, 0.07f, 0.08f));
            SetRendererColor(mobileHair, new Color(0.08f, 0.06f, 0.05f));
        }

        private static void SetRendererColor(Renderer renderer, Color color)
        {
            if (renderer == null) return;
            Material material = RuntimeMaterialFactory.Create("MobileCharacterColor", color);
            if (material == null) return;
            if (GraphicsQualityProfile.CurrentTier != GraphicsQualityProfile.QualityTier.Low)
            {
                Shader lit = Shader.Find("PersiaWar/MobileLit");
                if (lit != null && material.shader != lit)
                {
                    int key = material.GetInstanceID();
                    if (!LitCharacterMaterialCache.TryGetValue(key, out Material litMaterial) || litMaterial == null)
                    {
                        litMaterial = new Material(material);
                        litMaterial.shader = lit;
                        litMaterial.enableInstancing = true;
                        if (material.HasProperty("_MainTex") && litMaterial.HasProperty("_MainTex")) litMaterial.mainTexture = material.mainTexture;
                        if (material.HasProperty("_Color") && litMaterial.HasProperty("_Color")) litMaterial.SetColor("_Color", color);
                        LitCharacterMaterialCache[key] = litMaterial;
                    }
                    material = litMaterial;
                }
            }
            renderer.sharedMaterial = material;
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

                shadowSprite = new GameObject("CharacterShadow").AddComponent<SpriteRenderer>();
                shadowSprite.transform.SetParent(artRoot, false);
                shadowSprite.transform.localPosition = new Vector3(0.10f, -0.48f, 0f);
                shadowSprite.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                shadowSprite.transform.localScale = new Vector3(0.78f, 0.28f, 1f);
                shadowSprite.sortingOrder = 19;
                shadowSprite.color = new Color(0f, 0f, 0f, 0.30f);
                shadowSprite.maskInteraction = SpriteMaskInteraction.None;
            }

            if (muzzle == null)
            {
                GameObject muzzleObject = new GameObject("WeaponMuzzle");
                muzzleObject.transform.SetParent(transform, false);
                muzzle = muzzleObject.transform;
                muzzle.localPosition = new Vector3(0.42f, 1.05f, 0.56f);
            }

        }

        private void LoadCharacterSprite()
        {
            string resource = playerCharacter
                ? HeroResource
                : $"PersianCharacters/Enemy_0{archetype}";

            // The PNG assets are already imported by Unity as Sprite assets. Load the
            // Sprite object directly instead of reading the texture and constructing a
            // new Sprite at runtime. This keeps Android match activation on the imported
            // asset path and avoids a second CPU/GPU sprite-allocation step.
            Sprite loaded;
            if (!SpriteCache.TryGetValue(resource, out loaded) || loaded == null)
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
            if (shadowSprite != null)
            {
                shadowSprite.sprite = loaded;
                shadowSprite.color = new Color(0f, 0f, 0f, 0.30f);
            }
            sprite.color = Color.white;
            ApplyScale();
            StartupCheckpoint.Set(playerCharacter ? "PlayerSpriteReady" : "EnemySpriteReady");
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
