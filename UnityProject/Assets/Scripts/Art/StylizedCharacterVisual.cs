using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    /// <summary>
    /// Replaces the old capsule/primitive placeholders with a compact low-poly character rig.
    /// The rig rotates independently from gameplay colliders so aiming never spins the player root.
    /// </summary>
    public sealed class StylizedCharacterVisual : MonoBehaviour
    {
        private Transform torso;
        private Transform head;
        private Transform leftArm;
        private Transform rightArm;
        private Transform leftLeg;
        private Transform rightLeg;
        private Transform weaponPivot;
        private Transform muzzle;
        private Transform backpack;
        private Transform crest;
        private Transform leftShoulder;
        private Transform rightShoulder;
        private GameObject muzzleFlash;

        private Material skinMaterial;
        private Material clothMaterial;
        private Material armorMaterial;
        private Material accentMaterial;
        private Material darkMaterial;
        private Material weaponMaterial;
        private bool moving;
        private bool playerCharacter;
        private int archetype;
        private float phase;
        private float fireFlashUntil;
        private Vector3 lastFacing = Vector3.forward;
        private Vector3 baseTorsoPosition = new Vector3(0f, 0.86f, 0f);

        public Transform Muzzle => muzzle;

        public static StylizedCharacterVisual Attach(Transform owner, bool isPlayer, int characterArchetype)
        {
            Transform existing = owner.Find("PlayerVisual");
            if (!isPlayer)
                existing = owner.Find("EnemyVisual");

            StylizedCharacterVisual visual = existing != null
                ? existing.GetComponent<StylizedCharacterVisual>()
                : null;

            if (visual == null)
            {
                GameObject rootObject = new GameObject(isPlayer ? "PlayerVisual" : "EnemyVisual");
                rootObject.transform.SetParent(owner, false);
                visual = rootObject.AddComponent<StylizedCharacterVisual>();
            }

            visual.playerCharacter = isPlayer;
            visual.Configure(isPlayer, characterArchetype);
            return visual;
        }

        public void Configure(bool isPlayer, int characterArchetype)
        {
            playerCharacter = isPlayer;
            archetype = Mathf.Clamp(characterArchetype, 1, 3);
            phase = Random.Range(0f, Mathf.PI * 2f);

            if (torso == null)
                BuildModel();

            ApplyPalette();
        }

        public void ConfigurePlayerHero(int heroIndex)
        {
            playerCharacter = true;
            archetype = 1;
            if (torso == null)
                BuildModel();

            int index = Mathf.Clamp(heroIndex, 0, 4);
            Color[] cloth =
            {
                new Color(0.46f, 0.11f, 0.08f),
                new Color(0.06f, 0.28f, 0.40f),
                new Color(0.10f, 0.36f, 0.20f),
                new Color(0.31f, 0.10f, 0.38f),
                new Color(0.39f, 0.24f, 0.06f)
            };
            Color[] accent =
            {
                new Color(0.92f, 0.65f, 0.10f),
                new Color(0.76f, 0.84f, 0.90f),
                new Color(0.70f, 0.84f, 0.20f),
                new Color(0.95f, 0.28f, 0.20f),
                new Color(0.92f, 0.66f, 0.16f)
            };

            clothMaterial = RuntimeMaterialFactory.Create("PlayerCloth", cloth[index]);
            accentMaterial = RuntimeMaterialFactory.Create("PlayerAccent", accent[index]);
            if (torso != null) torso.GetComponent<Renderer>().sharedMaterial = clothMaterial;
            if (crest != null) crest.GetComponent<Renderer>().sharedMaterial = accentMaterial;
            if (leftShoulder != null) leftShoulder.GetComponent<Renderer>().sharedMaterial = accentMaterial;
            if (rightShoulder != null) rightShoulder.GetComponent<Renderer>().sharedMaterial = accentMaterial;
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
            fireFlashUntil = Time.time + 0.055f;
            if (muzzleFlash != null)
                muzzleFlash.SetActive(true);
        }

        private void Update()
        {
            if (torso == null)
                return;

            float speed = moving ? 11f : 3.2f;
            float wave = Mathf.Sin(Time.time * speed + phase);
            float stride = moving ? wave : 0f;

            torso.localPosition = baseTorsoPosition + Vector3.up * (moving ? Mathf.Abs(wave) * 0.045f : Mathf.Sin(Time.time * 2.2f + phase) * 0.018f);
            if (head != null)
                head.localRotation = Quaternion.Euler(0f, 0f, moving ? wave * 2.0f : Mathf.Sin(Time.time * 1.7f + phase) * 1.2f);

            if (leftArm != null)
                leftArm.localRotation = Quaternion.Euler(0f, 0f, -12f - stride * 18f);
            if (rightArm != null)
                rightArm.localRotation = Quaternion.Euler(0f, 0f, 12f + stride * 18f);
            if (leftLeg != null)
                leftLeg.localRotation = Quaternion.Euler(stride * 18f, 0f, 0f);
            if (rightLeg != null)
                rightLeg.localRotation = Quaternion.Euler(-stride * 18f, 0f, 0f);

            if (weaponPivot != null)
                weaponPivot.localPosition = new Vector3(0.38f, 1.03f, 0.42f + (Time.time < fireFlashUntil ? -0.055f : 0f));

            if (muzzleFlash != null && Time.time >= fireFlashUntil)
                muzzleFlash.SetActive(false);

            if (backpack != null)
                backpack.localPosition = new Vector3(0f, 1.02f + (moving ? Mathf.Abs(stride) * 0.025f : 0f), -0.30f);
        }

        private void BuildModel()
        {
            foreach (Transform child in transform)
                Destroy(child.gameObject);

            skinMaterial = RuntimeMaterialFactory.Create(playerCharacter ? "PlayerSkin" : "EnemySkin", new Color(0.68f, 0.43f, 0.28f));
            clothMaterial = RuntimeMaterialFactory.Create(playerCharacter ? "PlayerCloth" : "EnemyCloth", new Color(0.18f, 0.24f, 0.30f));
            armorMaterial = RuntimeMaterialFactory.Create(playerCharacter ? "PlayerArmor" : "EnemyArmor", new Color(0.09f, 0.12f, 0.16f));
            accentMaterial = RuntimeMaterialFactory.Create(playerCharacter ? "PlayerAccent" : "EnemyAccent", new Color(0.84f, 0.54f, 0.12f));
            darkMaterial = RuntimeMaterialFactory.Create(playerCharacter ? "PlayerDark" : "EnemyDark", new Color(0.055f, 0.065f, 0.08f));
            weaponMaterial = RuntimeMaterialFactory.Create(playerCharacter ? "PlayerWeapon" : "EnemyWeapon", new Color(0.12f, 0.14f, 0.16f));

            torso = CreatePart(PrimitiveType.Capsule, "Body", baseTorsoPosition, new Vector3(0.62f, 0.78f, 0.50f), clothMaterial);
            head = CreatePart(PrimitiveType.Sphere, "Head", new Vector3(0f, 1.72f, 0f), new Vector3(0.48f, 0.48f, 0.48f), skinMaterial);
            CreatePart(PrimitiveType.Cylinder, "NeckGuard", new Vector3(0f, 1.49f, 0f), new Vector3(0.24f, 0.10f, 0.24f), armorMaterial);
            CreatePart(PrimitiveType.Cylinder, "Crown", new Vector3(0f, 2.02f, 0f), new Vector3(0.53f, 0.17f, 0.53f), accentMaterial);
            crest = CreatePart(PrimitiveType.Cube, "Crest", new Vector3(0f, 2.17f, -0.02f), new Vector3(0.12f, 0.34f, 0.06f), accentMaterial);

            leftShoulder = CreatePart(PrimitiveType.Sphere, "ShoulderArmor", new Vector3(-0.48f, 1.24f, 0f), new Vector3(0.31f, 0.22f, 0.31f), armorMaterial);
            rightShoulder = CreatePart(PrimitiveType.Sphere, "RightShoulderArmor", new Vector3(0.48f, 1.24f, 0f), new Vector3(0.31f, 0.22f, 0.31f), armorMaterial);

            leftArm = CreatePart(PrimitiveType.Capsule, "LeftArm", new Vector3(-0.48f, 0.94f, 0f), new Vector3(0.18f, 0.46f, 0.18f), armorMaterial);
            rightArm = CreatePart(PrimitiveType.Capsule, "RightArm", new Vector3(0.48f, 0.94f, 0.10f), new Vector3(0.18f, 0.46f, 0.18f), armorMaterial);
            CreatePart(PrimitiveType.Sphere, "LeftHand", new Vector3(-0.50f, 0.54f, 0.10f), new Vector3(0.18f, 0.18f, 0.18f), skinMaterial);
            CreatePart(PrimitiveType.Sphere, "RightHand", new Vector3(0.50f, 0.54f, 0.30f), new Vector3(0.18f, 0.18f, 0.18f), skinMaterial);

            leftLeg = CreatePart(PrimitiveType.Capsule, "LeftLeg", new Vector3(-0.20f, 0.31f, 0f), new Vector3(0.19f, 0.46f, 0.19f), clothMaterial);
            rightLeg = CreatePart(PrimitiveType.Capsule, "RightLeg", new Vector3(0.20f, 0.31f, 0f), new Vector3(0.19f, 0.46f, 0.19f), clothMaterial);
            CreatePart(PrimitiveType.Cube, "LeftBoot", new Vector3(-0.20f, 0.05f, 0.12f), new Vector3(0.28f, 0.18f, 0.42f), darkMaterial);
            CreatePart(PrimitiveType.Cube, "RightBoot", new Vector3(0.20f, 0.05f, 0.12f), new Vector3(0.28f, 0.18f, 0.42f), darkMaterial);

            CreatePart(PrimitiveType.Cube, "ChestPlate", new Vector3(0f, 0.96f, 0.25f), new Vector3(0.48f, 0.53f, 0.08f), armorMaterial);
            CreatePart(PrimitiveType.Cube, "Belt", new Vector3(0f, 0.63f, 0.15f), new Vector3(0.50f, 0.10f, 0.34f), accentMaterial);
            backpack = CreatePart(PrimitiveType.Cube, "Backpack", new Vector3(0f, 1.02f, -0.30f), new Vector3(0.46f, 0.58f, 0.22f), darkMaterial);

            weaponPivot = new GameObject("WeaponPivot").transform;
            weaponPivot.SetParent(transform, false);
            weaponPivot.localPosition = new Vector3(0.38f, 1.03f, 0.42f);

            CreatePartUnder(weaponPivot, PrimitiveType.Cube, "GunBody", new Vector3(0f, 0f, 0.34f), new Vector3(0.20f, 0.14f, 0.52f), weaponMaterial);
            CreatePartUnder(weaponPivot, PrimitiveType.Cylinder, "GunBarrel", new Vector3(0f, 0f, 0.78f), new Vector3(0.075f, 0.34f, 0.075f), darkMaterial, Quaternion.Euler(90f, 0f, 0f));
            CreatePartUnder(weaponPivot, PrimitiveType.Cube, "Magazine", new Vector3(0f, -0.16f, 0.31f), new Vector3(0.13f, 0.25f, 0.16f), darkMaterial);

            muzzle = new GameObject("WeaponMuzzle").transform;
            muzzle.SetParent(weaponPivot, false);
            muzzle.localPosition = new Vector3(0f, 0f, 1.08f);
            muzzle.localRotation = Quaternion.identity;

            muzzleFlash = CreatePartUnder(weaponPivot, PrimitiveType.Sphere, "MuzzleFlash", new Vector3(0f, 0f, 1.10f), Vector3.one * 0.24f, accentMaterial).gameObject;
            muzzleFlash.SetActive(false);

            Vector3 visualScale = archetype == 3 ? new Vector3(1.16f, 1.16f, 1.16f)
                : archetype == 2 ? new Vector3(1.05f, 1.05f, 1.05f)
                : Vector3.one;
            transform.localScale = visualScale;
        }

        private void ApplyPalette()
        {
            if (playerCharacter)
            {
                clothMaterial = RuntimeMaterialFactory.Create("PlayerCloth", new Color(0.12f, 0.27f, 0.38f));
                armorMaterial = RuntimeMaterialFactory.Create("PlayerArmor", new Color(0.08f, 0.12f, 0.17f));
                accentMaterial = RuntimeMaterialFactory.Create("PlayerAccent", new Color(0.90f, 0.64f, 0.12f));
            }
            else
            {
                Color cloth = archetype == 3 ? new Color(0.32f, 0.06f, 0.06f)
                    : archetype == 2 ? new Color(0.06f, 0.24f, 0.34f)
                    : new Color(0.38f, 0.12f, 0.09f);
                Color armor = archetype == 3 ? new Color(0.13f, 0.09f, 0.08f)
                    : archetype == 2 ? new Color(0.10f, 0.17f, 0.21f)
                    : new Color(0.16f, 0.12f, 0.10f);
                Color accent = archetype == 3 ? new Color(0.88f, 0.65f, 0.12f)
                    : archetype == 2 ? new Color(0.36f, 0.74f, 0.82f)
                    : new Color(0.82f, 0.30f, 0.12f);

                clothMaterial = RuntimeMaterialFactory.Create("EnemyCloth", cloth);
                armorMaterial = RuntimeMaterialFactory.Create("EnemyArmor", armor);
                accentMaterial = RuntimeMaterialFactory.Create("EnemyAccent", accent);
            }

            if (torso != null) torso.GetComponent<Renderer>().sharedMaterial = clothMaterial;
            if (leftArm != null) leftArm.GetComponent<Renderer>().sharedMaterial = armorMaterial;
            if (rightArm != null) rightArm.GetComponent<Renderer>().sharedMaterial = armorMaterial;
            if (leftShoulder != null) leftShoulder.GetComponent<Renderer>().sharedMaterial = accentMaterial;
            if (rightShoulder != null) rightShoulder.GetComponent<Renderer>().sharedMaterial = accentMaterial;
            if (crest != null) crest.GetComponent<Renderer>().sharedMaterial = accentMaterial;
        }

        private Transform CreatePart(PrimitiveType primitive, string partName, Vector3 localPosition, Vector3 localScale, Material material)
        {
            return CreatePartUnder(transform, primitive, partName, localPosition, localScale, material, Quaternion.identity);
        }

        private Transform CreatePartUnder(Transform parent, PrimitiveType primitive, string partName, Vector3 localPosition, Vector3 localScale, Material material, Quaternion rotation = default)
        {
            GameObject part = GameObject.CreatePrimitive(primitive);
            part.name = partName;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localRotation = rotation;
            part.transform.localScale = localScale;

            Collider collider = part.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
                renderer.receiveShadows = true;
            }

            return part.transform;
        }
    }
}
