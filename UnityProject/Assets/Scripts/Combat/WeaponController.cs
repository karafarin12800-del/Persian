using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public sealed class WeaponController : MonoBehaviour
    {
        public enum WeaponKind
        {
            LightPistol = 0,
            AssaultRifle = 1,
            HeavyMachineGun = 2,
            Axe = 3,
            VoidRifle = 4,
            AlienSword = 5
        }

        [SerializeField] private Projectile projectilePrefab;
        [SerializeField] private Transform muzzle;
        [SerializeField] private float fireCooldown = 0.155f;
        [SerializeField] private float projectileSpeed = 45f;
        [SerializeField] private float projectileLifetime = 0.72f;
        [SerializeField] private float weaponRange = 27f;
        [SerializeField] private int projectileDamage = 22;
        [SerializeField] private int magazineSize = 24;
        [SerializeField] private int startingMagazine = 24;
        [SerializeField] private int startingReserve = 90;
        [SerializeField] private WeaponKind weaponKind = WeaponKind.AssaultRifle;
        [SerializeField] private int meleeDamage = 45;
        [SerializeField] private float meleeRange = 3.1f;
        [SerializeField] private float meleeCooldown = 0.32f;

        private float nextFireTime;
        private float nextMeleeTime;
        private int magazine;
        private int reserve;
        // Melee profiles use a one-slot placeholder magazine. Preserve firearm
        // ammunition independently so cycling through melee never refills or drains it.
        private float firearmAmmoRatio = 1f;
        private int firearmReserve;
        private bool axeEnabledForMatch;
        private bool axeEquippedAsMelee;
        private bool meleeProfileApplied;
        private PlayerController player;
        private StylizedCharacterVisual characterVisual;
        private Transform handWeaponMount;
        private static readonly Vector3 HandWeaponLocalPosition = new Vector3(0f, 0.02f, 0.06f);
        private Transform gunRoot;
        private Transform axeRoot;
        private Transform swordRoot;
        private Renderer[] gunRenderers;
        private Material[] baseGunMaterials;
        private Material voidRifleMaterial;
        private Vector3 swordRestLocalPosition;
        private Quaternion swordRestLocalRotation;
        private float swordSwingTimer;
        private Vector3 axeRestLocalPosition;
        private Quaternion axeRestLocalRotation;
        private float axeSwingTimer;
        private const float AxeSwingDuration = 0.48f;
        private Vector3 gunRestLocalPosition;
        private float recoilAmount;
        private LineRenderer rangeRing;
        private const int RangeRingSegments = 64;
        private const float LightPistolRange = 7.5f;
        private const float AssaultRifleRange = 13.5f;
        private const float HeavyMachineGunRange = 21f;

        public static float GetEffectiveRangeForKind(WeaponKind kind)
        {
            switch (kind)
            {
                case WeaponKind.LightPistol: return LightPistolRange;
                case WeaponKind.HeavyMachineGun: return HeavyMachineGunRange;
                case WeaponKind.Axe: return 3.1f;
                case WeaponKind.VoidRifle: return 28f;
                case WeaponKind.AlienSword: return 3.5f;
                default: return AssaultRifleRange;
            }
        }

        public Transform Muzzle => muzzle != null ? muzzle : transform;
        public WeaponKind CurrentWeapon => weaponKind;
        public bool IsMeleeWeaponEquipped => axeEquippedAsMelee ||
            weaponKind == WeaponKind.Axe || weaponKind == WeaponKind.AlienSword;
        public string CurrentWeaponName => axeEquippedAsMelee ? "AXE" : (weaponKind == WeaponKind.LightPistol ? "PISTOL"
            : weaponKind == WeaponKind.HeavyMachineGun ? "HEAVY"
            : weaponKind == WeaponKind.Axe ? "AXE"
            : weaponKind == WeaponKind.VoidRifle ? "VOID RIFLE"
            : weaponKind == WeaponKind.AlienSword ? "ALIEN SWORD" : "RIFLE");
        public float EffectiveRange => Mathf.Max(1f, weaponRange);
        public float MoveSpeedMultiplier => weaponKind == WeaponKind.LightPistol ? 1.20f
            : weaponKind == WeaponKind.HeavyMachineGun ? 0.78f
            : weaponKind == WeaponKind.Axe ? 0.92f
            : weaponKind == WeaponKind.AlienSword ? 1.05f
            : weaponKind == WeaponKind.VoidRifle ? 0.94f : 1f;
        public int Magazine => magazine;
        public int Reserve => reserve;
        public int MagazineSize => magazineSize;
        public bool IsMelee { get; private set; }

        private void Awake()
        {
            player = GetComponent<PlayerController>();
            characterVisual = GetComponentInChildren<StylizedCharacterVisual>(true);
            ApplyWeaponProfile(false);
            // The projectile template creates a collider and Rigidbody. Defer that
            // physics allocation until the player actually fires.

            if (muzzle == null)
            {
                GameObject muzzleObject = new GameObject("WeaponMuzzle");
                muzzle = muzzleObject.transform;
                muzzle.SetParent(transform, false);
                muzzle.localPosition = new Vector3(0.36f, 1.02f, 1.12f);
            }

            EnsureWeaponVisual();
            AttachWeaponsToHand();
            EnsureRangeIndicator();
            RefreshWeaponVisual();
        }

        public void AttachWeaponsToHand()
        {
            if (characterVisual == null)
                characterVisual = GetComponentInChildren<StylizedCharacterVisual>(true);

            Transform mount = characterVisual != null ? characterVisual.HandWeaponMount : null;
            handWeaponMount = mount;
            if (mount != null)
            {
                if (gunRoot != null)
                {
                    gunRoot.SetParent(mount, false);
                    gunRestLocalPosition = HandWeaponLocalPosition;
                    gunRoot.localPosition = gunRestLocalPosition;
                    gunRoot.localRotation = Quaternion.identity;
                }
                if (axeRoot != null)
                {
                    axeRoot.SetParent(mount, false);
                    axeRestLocalPosition = HandWeaponLocalPosition;
                    axeRestLocalRotation = Quaternion.identity;
                    axeRoot.localPosition = axeRestLocalPosition;
                    axeRoot.localRotation = axeRestLocalRotation;
                }
                if (swordRoot != null)
                {
                    swordRoot.SetParent(mount, false);
                    swordRestLocalPosition = HandWeaponLocalPosition;
                    swordRestLocalRotation = Quaternion.identity;
                    swordRoot.localPosition = swordRestLocalPosition;
                    swordRoot.localRotation = swordRestLocalRotation;
                }
            }

            if (muzzle != null && gunRoot != null)
            {
                muzzle.SetParent(gunRoot, false);
                muzzle.localPosition = ModelLibrary.MuzzleLocalOffset(GetActiveModelKind());
                muzzle.localRotation = Quaternion.identity;
            }

            if (mount != null)
            {
                string[] heroes = { "Ardeshir", "Anahita", "Soldier", "RoyalScout", "SilkWarrior", "DesertKnight" };
                int heroIndex = Mathf.Clamp(PlayerPrefs.GetInt("PW.SelectedHero", 0), 0, heroes.Length - 1);
                string checkpointWeapon = axeEquippedAsMelee ? "Axe" : weaponKind.ToString();
                string checkpoint = "WeaponAttachedToHand_" + checkpointWeapon + "_" + heroes[heroIndex];
                StartupCheckpoint.Set(checkpoint);
                Debug.Log("[PERSIA WEAPON] " + checkpoint);
            }
        }

        public void DetachWeaponsFromHand()
        {
            if (gunRoot != null)
            {
                gunRoot.SetParent(transform, false);
                gunRestLocalPosition = weaponKind == WeaponKind.LightPistol
                    ? new Vector3(0.24f, 0.93f, 0.33f)
                    : (weaponKind == WeaponKind.HeavyMachineGun
                        ? new Vector3(0.40f, 1.04f, 0.48f)
                        : new Vector3(0.34f, 1.02f, 0.42f));
                gunRoot.localPosition = gunRestLocalPosition;
            }
            if (axeRoot != null)
            {
                axeRoot.SetParent(transform, false);
                axeRestLocalPosition = new Vector3(-0.30f, 0.90f, 0.32f);
                axeRestLocalRotation = Quaternion.Euler(0f, 0f, -12f);
                axeRoot.localPosition = axeRestLocalPosition;
                axeRoot.localRotation = axeRestLocalRotation;
            }
            if (swordRoot != null)
            {
                swordRoot.SetParent(transform, false);
                swordRestLocalPosition = new Vector3(0.30f, 0.92f, 0.38f);
                swordRestLocalRotation = Quaternion.Euler(0f, 0f, -8f);
                swordRoot.localPosition = swordRestLocalPosition;
                swordRoot.localRotation = swordRestLocalRotation;
            }
            handWeaponMount = null;
        }

        private ModelKind GetActiveModelKind()
        {
            if (axeEquippedAsMelee)
                return ModelKind.Axe;
            if (weaponKind == WeaponKind.LightPistol)
                return ModelKind.Pistol;
            return ModelLibrary.KindFromName(weaponKind.ToString());
        }

        public void SetAxeEnabledForMatch(bool enabledForStageTwo)
        {
            axeEnabledForMatch = enabledForStageTwo;
            // Each match begins with fists again; an axe is kept only after pickup.
            axeEquippedAsMelee = false;
            if (!axeEnabledForMatch && (weaponKind == WeaponKind.Axe ||
                weaponKind == WeaponKind.VoidRifle || weaponKind == WeaponKind.AlienSword))
            {
                weaponKind = WeaponKind.AssaultRifle;
                ApplyWeaponProfile(false);
                RefreshWeaponVisual();
                UpdateRangeIndicator();
            }
            else
            {
                RefreshWeaponVisual();
            }
        }

        public void CycleWeapon()
        {
            SaveFirearmAmmoState();
            axeEquippedAsMelee = false;
            if (!axeEnabledForMatch)
            {
                weaponKind = (WeaponKind)(((int)weaponKind + 1) % 3);
            }
            else
            {
                WeaponKind[] stageTwoWeapons =
                {
                    WeaponKind.LightPistol, WeaponKind.AssaultRifle, WeaponKind.HeavyMachineGun,
                    WeaponKind.VoidRifle, WeaponKind.AlienSword
                };
                int current = System.Array.IndexOf(stageTwoWeapons, weaponKind);
                weaponKind = stageTwoWeapons[(current + 1 + stageTwoWeapons.Length) % stageTwoWeapons.Length];
            }
            ApplyWeaponProfile(true);
            RefreshWeaponVisual();
            UpdateRangeIndicator();
            StartupCheckpoint.Set("WeaponSwitched_" + CurrentWeaponName);
        }

        public void SelectWeapon(WeaponKind selectedWeapon)
        {
            SaveFirearmAmmoState();
            if (selectedWeapon == WeaponKind.Axe)
            {
                if (axeEnabledForMatch)
                {
                    axeEquippedAsMelee = true;
                    RefreshWeaponVisual();
                    StartupCheckpoint.Set("AxeEquippedAsMelee");
                }
                return;
            }

            axeEquippedAsMelee = false;
            if ((selectedWeapon == WeaponKind.VoidRifle || selectedWeapon == WeaponKind.AlienSword) &&
                !axeEnabledForMatch)
                selectedWeapon = WeaponKind.AssaultRifle;

            bool changed = weaponKind != selectedWeapon;
            weaponKind = selectedWeapon;

            // Always re-apply the chosen profile. In particular, a pickup must
            // restore the weapon's configured range, projectile lifetime, damage,
            // fire cadence, and magazine size even if its kind is already selected.
            ApplyWeaponProfile(true);
            RefreshWeaponVisual();
            UpdateRangeIndicator();

            if (changed)
                StartupCheckpoint.Set("WeaponSwitched_" + CurrentWeaponName);
        }

        public void EquipWeaponFromPickup(WeaponKind selectedWeapon, int reserveAmmo)
        {
            if (selectedWeapon == WeaponKind.Axe)
            {
                if (axeEnabledForMatch)
                {
                    axeEquippedAsMelee = true;
                    RefreshWeaponVisual();
                    StartupCheckpoint.Set("AxePickupEquippedAsMelee");
                }
                return;
            }

            SelectWeapon(selectedWeapon);
            AddReserveAmmo(reserveAmmo);
            StartupCheckpoint.Set("WeaponPickupEquipped_" + CurrentWeaponName);
        }

        private bool IsFirearmWeapon(WeaponKind kind)
        {
            return kind != WeaponKind.Axe && kind != WeaponKind.AlienSword;
        }

        private void SaveFirearmAmmoState()
        {
            if (!IsFirearmWeapon(weaponKind)) return;
            firearmAmmoRatio = magazineSize > 0
                ? Mathf.Clamp01(magazine / (float)magazineSize) : 1f;
            firearmReserve = Mathf.Max(0, reserve);
        }

        private void ApplyWeaponProfile(bool preserveAmmo)
        {
            int oldMagazineSize = Mathf.Max(1, magazineSize);
            float oldAmmoRatio = preserveAmmo
                ? (IsFirearmWeapon(weaponKind) ? firearmAmmoRatio : magazine / (float)oldMagazineSize)
                : 1f;
            // Only restore the original melee tuning after leaving the stage-two axe.
            // Normal stage-one firearm switches keep their original serialized values.
            if (meleeProfileApplied && weaponKind != WeaponKind.Axe && weaponKind != WeaponKind.AlienSword)
            {
                meleeDamage = 45;
                meleeRange = 3.1f;
                meleeCooldown = 0.32f;
                meleeProfileApplied = false;
            }

            switch (weaponKind)
            {
                case WeaponKind.LightPistol:
                    projectileSpeed = 34f;
                    weaponRange = LightPistolRange;
                    projectileDamage = 14;
                    fireCooldown = 0.24f;
                    magazineSize = 15;
                    break;

                case WeaponKind.HeavyMachineGun:
                    projectileSpeed = 40f;
                    weaponRange = HeavyMachineGunRange;
                    projectileDamage = 34;
                    fireCooldown = 0.31f;
                    magazineSize = 36;
                    break;

                case WeaponKind.Axe:
                    projectileSpeed = 1f;
                    weaponRange = 3.1f;
                    projectileDamage = 0;
                    fireCooldown = 0.55f;
                    magazineSize = 1;
                    meleeDamage = 72;
                    meleeRange = 3.2f;
                    meleeCooldown = 0.42f;
                    meleeProfileApplied = true;
                    break;

                case WeaponKind.VoidRifle:
                    projectileSpeed = 55f;
                    weaponRange = 28f;
                    projectileDamage = 42;
                    fireCooldown = 0.42f;
                    magazineSize = 18;
                    break;

                case WeaponKind.AlienSword:
                    projectileSpeed = 1f;
                    weaponRange = 3.5f;
                    projectileDamage = 0;
                    fireCooldown = 0.5f;
                    magazineSize = 1;
                    meleeDamage = 90;
                    meleeRange = 3.5f;
                    meleeCooldown = 0.34f;
                    meleeProfileApplied = true;
                    break;

                default:
                    projectileSpeed = 38f;
                    weaponRange = AssaultRifleRange;
                    projectileDamage = 22;
                    fireCooldown = 0.19f;
                    magazineSize = 24;
                    break;
            }

            projectileLifetime = weaponRange / Mathf.Max(1f, projectileSpeed);
            magazine = preserveAmmo
                ? Mathf.Clamp(Mathf.RoundToInt(oldAmmoRatio * magazineSize), 0, magazineSize)
                : magazineSize;
            if (!preserveAmmo)
            {
                reserve = Mathf.Max(0, startingReserve);
                firearmReserve = reserve;
                firearmAmmoRatio = 1f;
            }
            else if (IsFirearmWeapon(weaponKind))
            {
                reserve = Mathf.Max(0, firearmReserve);
                firearmAmmoRatio = oldAmmoRatio;
            }

            if (gunRoot != null)
                RefreshWeaponVisual();
            if (rangeRing != null)
                UpdateRangeIndicator();
        }

        private void EnsureRangeIndicator()
        {
            if (rangeRing != null) return;

            GameObject indicator = new GameObject("WeaponRangeIndicator");
            indicator.transform.SetParent(transform, false);
            rangeRing = indicator.AddComponent<LineRenderer>();
            rangeRing.useWorldSpace = true;
            rangeRing.loop = true;
            rangeRing.positionCount = RangeRingSegments;
            rangeRing.widthMultiplier = 0.065f;
            rangeRing.numCornerVertices = 2;
            rangeRing.numCapVertices = 0;
            rangeRing.alignment = LineAlignment.View;
            rangeRing.textureMode = LineTextureMode.Stretch;
            rangeRing.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            rangeRing.receiveShadows = false;
            rangeRing.sharedMaterial = RuntimeMaterialFactory.Create(
                "WeaponRangeRingYellow",
                new Color(1f, 0.82f, 0.08f, 1f));
            rangeRing.startColor = new Color(1f, 0.82f, 0.08f, 1f);
            rangeRing.endColor = new Color(1f, 0.82f, 0.08f, 1f);
            rangeRing.enabled = true;
            UpdateRangeIndicator();
        }

        private void RefreshWeaponVisual()
        {
            if (gunRoot == null) return;
            AttachWeaponsToHand();
            bool hasHandMount = handWeaponMount != null;

            bool pistol = weaponKind == WeaponKind.LightPistol;
            bool heavy = weaponKind == WeaponKind.HeavyMachineGun;
            bool axe = axeEquippedAsMelee || weaponKind == WeaponKind.Axe;
            bool sword = weaponKind == WeaponKind.AlienSword;
            bool voidRifle = weaponKind == WeaponKind.VoidRifle;

            // Keep the pistol compact by changing its actual silhouette rather than
            // merely shrinking a rifle. The heavy profile uses a thicker receiver,
            // larger magazine, long barrel, stock and foregrip.
            gunRoot.gameObject.SetActive(!axe && !sword);
            if (axeRoot != null)
            {
                axeRoot.gameObject.SetActive(axe);
                if (axe)
                {
                    axeRoot.localPosition = axeRestLocalPosition;
                    axeRoot.localRotation = axeRestLocalRotation;
                }
                else axeSwingTimer = 0f;
            }
            if (swordRoot != null)
            {
                swordRoot.gameObject.SetActive(sword);
                if (sword)
                {
                    swordRoot.localPosition = swordRestLocalPosition;
                    swordRoot.localRotation = swordRestLocalRotation;
                }
                else swordSwingTimer = 0f;
            }
            gunRoot.localScale = Vector3.one * (heavy ? 1.08f : 1f);
            if (!hasHandMount)
            {
                gunRestLocalPosition = pistol
                    ? new Vector3(0.24f, 0.93f, 0.33f)
                    : (heavy
                        ? new Vector3(0.40f, 1.04f, 0.48f)
                        : new Vector3(0.34f, 1.02f, 0.42f));
            }
            else
            {
                gunRestLocalPosition = HandWeaponLocalPosition;
            }
            gunRoot.localPosition = gunRestLocalPosition;

            SetWeaponPart("Receiver", true,
                Vector3.zero,
                pistol ? new Vector3(0.16f, 0.11f, 0.30f)
                    : (heavy ? new Vector3(0.26f, 0.20f, 0.64f) : new Vector3(0.22f, 0.18f, 0.56f)));
            SetWeaponPart("Stock", !pistol,
                heavy ? new Vector3(0f, 0.015f, -0.39f) : new Vector3(0f, 0.015f, -0.38f),
                heavy ? new Vector3(0.18f, 0.16f, 0.36f) : new Vector3(0.16f, 0.14f, 0.34f));
            SetWeaponPart("Magazine", true,
                pistol ? new Vector3(0.01f, -0.115f, 0.01f) : new Vector3(0.01f, -0.15f, 0.03f),
                pistol ? new Vector3(0.09f, 0.18f, 0.12f)
                    : (heavy ? new Vector3(0.16f, 0.34f, 0.21f) : new Vector3(0.13f, 0.28f, 0.18f)));
            SetWeaponPart("TopRail", !pistol,
                new Vector3(0f, heavy ? 0.15f : 0.13f, 0.08f),
                heavy ? new Vector3(0.16f, 0.07f, 0.52f) : new Vector3(0.13f, 0.06f, 0.45f));
            SetWeaponPart("Barrel", true,
                new Vector3(0f, 0f, pistol ? 0.19f : (heavy ? 0.56f : 0.58f)),
                pistol ? new Vector3(0.042f, 0.13f, 0.042f)
                    : (heavy ? new Vector3(0.075f, 0.35f, 0.075f) : new Vector3(0.065f, 0.33f, 0.065f)));
            SetWeaponPart("MuzzleBreak", heavy,
                new Vector3(0f, 0f, 1.0f),
                new Vector3(0.15f, 0.12f, 0.13f));
            SetWeaponPart("FrontGrip", !pistol,
                new Vector3(0f, -0.12f, heavy ? 0.45f : 0.43f),
                heavy ? new Vector3(0.14f, 0.25f, 0.15f) : new Vector3(0.11f, 0.20f, 0.12f));

            SetWeaponPart("PistolSlide", pistol,
                new Vector3(0f, 0.055f, 0.06f), new Vector3(0.17f, 0.045f, 0.27f));
            SetWeaponPart("PistolGrip", pistol,
                new Vector3(0f, -0.115f, -0.035f), new Vector3(0.095f, 0.16f, 0.13f));

            if (muzzle != null)
            {
                if (muzzle.parent != gunRoot)
                    muzzle.SetParent(gunRoot, false);
                muzzle.localPosition = ModelLibrary.MuzzleLocalOffset(GetActiveModelKind());
                muzzle.localRotation = Quaternion.identity;
            }

            if (voidRifle)
            {
                if (voidRifleMaterial == null)
                    voidRifleMaterial = RuntimeMaterialFactory.Create("VoidRifleEnergy", new Color(0.08f, 0.72f, 0.98f));
                if (gunRenderers != null)
                    for (int i = 0; i < gunRenderers.Length; i++)
                        if (gunRenderers[i] != null) gunRenderers[i].sharedMaterial = voidRifleMaterial;
            }
            else if (gunRenderers != null && baseGunMaterials != null)
            {
                for (int i = 0; i < gunRenderers.Length && i < baseGunMaterials.Length; i++)
                    if (gunRenderers[i] != null) gunRenderers[i].sharedMaterial = baseGunMaterials[i];
            }

            RefreshLibraryWeaponVisual();
        }

        private void RefreshLibraryWeaponVisual()
        {
            bool axe = axeEquippedAsMelee || weaponKind == WeaponKind.Axe;
            Transform root = axe ? axeRoot
                : weaponKind == WeaponKind.AlienSword ? swordRoot : gunRoot;
            if (root == null) return;

            ModelKind kind = axe ? ModelKind.Axe : ModelLibrary.KindFromName(weaponKind.ToString());
            if (!ModelLibrary.IsWeapon(kind)) return;

            string modelName = "PW_" + kind;
            Transform model = root.Find(modelName);
            if (model == null)
            {
                for (int i = root.childCount - 1; i >= 0; i--)
                {
                    Transform child = root.GetChild(i);
                    if (child != null && child.name.StartsWith("PW_"))
                        Destroy(child.gameObject);
                }
                model = ModelLibrary.AttachWeapon(root, kind, true, Vector3.zero, Quaternion.identity, 1f)?.transform;
            }

            if (model == null)
            {
                Renderer[] hiddenFallback = root.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < hiddenFallback.Length; i++)
                    if (hiddenFallback[i] != null) hiddenFallback[i].enabled = false;
                return;
            }
            model.gameObject.SetActive(true);
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
                if (renderers[i] != null)
                    renderers[i].enabled = renderers[i].transform == model || renderers[i].transform.IsChildOf(model);
        }

        private void SetWeaponPart(string partName, bool visible, Vector3 localPosition, Vector3 localScale)
        {
            if (gunRoot == null)
                return;

            Transform part = gunRoot.Find(partName);
            if (part == null)
                return;

            part.gameObject.SetActive(visible);
            if (!visible)
                return;

            part.localPosition = localPosition;
            part.localScale = localScale;
        }

        private void LateUpdate()
        {
            UpdateRangeIndicator();
        }

        private void UpdateRangeIndicator()
        {
            if (rangeRing == null || player == null)
                return;

            bool visible = isActiveAndEnabled && player.isActiveAndEnabled && !player.IsDefeated;
            rangeRing.enabled = visible;
            if (!visible) return;

            Vector3 center = player.transform.position;
            float y = center.y + 0.12f;
            float radius = EffectiveRange;
            for (int i = 0; i < RangeRingSegments; i++)
            {
                float angle = (i / (float)RangeRingSegments) * Mathf.PI * 2f;
                rangeRing.SetPosition(i, new Vector3(
                    center.x + Mathf.Cos(angle) * radius,
                    y,
                    center.z + Mathf.Sin(angle) * radius));
            }
        }

        private void EnsureProjectileTemplate()
        {
            if (projectilePrefab != null) return;

#if UNITY_ANDROID
            GameObject projectileObject = AndroidSafeRuntimeFactory.CreateProjectile(
                "RuntimeProjectileTemplate",
                transform.position,
                Quaternion.identity,
                Vector3.one * 0.18f,
                new Color(1f, 0.82f, 0.20f));
            projectileObject.SetActive(false);
            projectilePrefab = projectileObject.AddComponent<Projectile>();
#else
            GameObject projectileObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            projectileObject.name = "RuntimeProjectileTemplate";
            projectileObject.SetActive(false);
            projectileObject.transform.position = transform.position;
            projectileObject.transform.localScale = Vector3.one * 0.18f;

            SphereCollider collider = projectileObject.GetComponent<SphereCollider>();
            collider.isTrigger = true;
            collider.radius = 0.5f;

            Rigidbody body = projectileObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            projectilePrefab = projectileObject.AddComponent<Projectile>();
#endif
        }

        public bool TryFire(Vector3 targetWorldPosition)
        {
            if (axeEquippedAsMelee || weaponKind == WeaponKind.Axe || weaponKind == WeaponKind.AlienSword) return false;
            IsMelee = false;
            if (Time.time < nextFireTime) return false;
            if (magazine <= 0)
            {
                Reload();
                return false;
            }
            EnsureProjectileTemplate();
            if (projectilePrefab == null) return false;

            Vector3 initialOrigin = muzzle != null ? muzzle.position : transform.position + Vector3.up;
            Vector3 initialDirection = targetWorldPosition - initialOrigin;
            initialDirection.y = 0f;
            float distance = initialDirection.magnitude;
            if (distance < 0.001f || distance > EffectiveRange)
                return false;

            // Rotate first, then read the hand-mounted muzzle again: the barrel tip
            // moves with the right-hand rig as the player turns.
            transform.rotation = Quaternion.LookRotation(initialDirection.normalized, Vector3.up);
            Vector3 origin = muzzle != null ? muzzle.position : transform.position + Vector3.up;
            Vector3 direction = targetWorldPosition - origin;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f || direction.magnitude > EffectiveRange)
                return false;

            nextFireTime = Time.time + fireCooldown;
            magazine--;

            Projectile projectile = Object.Instantiate(projectilePrefab, origin, transform.rotation);
            projectile.SetDefaults(projectileSpeed, projectileLifetime, projectileDamage);
            projectile.SetOwner(transform);
            projectile.gameObject.SetActive(true);
            projectile.Launch(direction.normalized);
            MuzzleFlashFX.Show(muzzle, false);
            recoilAmount = 0.12f;
            StylizedCharacterVisual characterVisual = GetComponentInChildren<StylizedCharacterVisual>(true);
            if (characterVisual != null)
                characterVisual.PlayFire();
            RuntimeGameAudio.PlayWeaponShot((int)weaponKind);
            return true;
        }

        public bool TryFireDirection(Vector3 worldDirection)
        {
            if (axeEquippedAsMelee || weaponKind == WeaponKind.Axe || weaponKind == WeaponKind.AlienSword) return false;
            IsMelee = false;

            if (Time.time < nextFireTime) return false;
            if (magazine <= 0)
            {
                Reload();
                return false;
            }
            EnsureProjectileTemplate();
            if (projectilePrefab == null) return false;

            worldDirection.y = 0f;
            if (worldDirection.sqrMagnitude < 0.001f)
                return false;

            Vector3 direction = worldDirection.normalized;
            transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            Vector3 origin = muzzle != null ? muzzle.position : transform.position + Vector3.up;

            nextFireTime = Time.time + fireCooldown;
            magazine--;

            Projectile projectile = Object.Instantiate(projectilePrefab, origin, transform.rotation);
            projectile.SetDefaults(projectileSpeed, projectileLifetime, projectileDamage);
            projectile.SetOwner(transform);
            projectile.gameObject.SetActive(true);
            projectile.Launch(direction);
            MuzzleFlashFX.Show(muzzle, false);
            recoilAmount = 0.12f;
            StylizedCharacterVisual characterVisual = GetComponentInChildren<StylizedCharacterVisual>(true);
            if (characterVisual != null)
                characterVisual.PlayFire();
            RuntimeGameAudio.PlayWeaponShot((int)weaponKind);
            return true;
        }

        private void Update()
        {
            if (gunRoot != null)
            {
                recoilAmount = Mathf.MoveTowards(recoilAmount, 0f, 5.5f * Time.deltaTime);
                Vector3 target = gunRestLocalPosition + Vector3.back * recoilAmount;
                gunRoot.localPosition = Vector3.Lerp(
                    gunRoot.localPosition,
                    target,
                    1f - Mathf.Exp(-24f * Time.deltaTime));
            }

            if (swordRoot != null && swordRoot.gameObject.activeSelf)
            {
                if (swordSwingTimer > 0f)
                {
                    swordSwingTimer = Mathf.Max(0f, swordSwingTimer - Time.deltaTime);
                    float progress = 1f - swordSwingTimer / AxeSwingDuration;
                    float slash = Mathf.Sin(progress * Mathf.PI) * 115f;
                    swordRoot.localRotation = swordRestLocalRotation * Quaternion.Euler(0f, slash, -slash * 0.18f);
                }
                else swordRoot.localRotation = Quaternion.Slerp(swordRoot.localRotation, swordRestLocalRotation,
                    1f - Mathf.Exp(-22f * Time.deltaTime));
            }

            if (axeRoot != null && axeRoot.gameObject.activeSelf)
            {
                if (axeSwingTimer > 0f)
                {
                    axeSwingTimer = Mathf.Max(0f, axeSwingTimer - Time.deltaTime);
                    float progress = 1f - axeSwingTimer / AxeSwingDuration;
                    float spin = Mathf.Sin(progress * Mathf.PI * 4f) * 155f;
                    axeRoot.localRotation = axeRestLocalRotation * Quaternion.Euler(0f, spin * 0.28f, spin);
                    axeRoot.localPosition = axeRestLocalPosition + new Vector3(
                        Mathf.Sin(progress * Mathf.PI * 4f) * 0.10f,
                        Mathf.Sin(progress * Mathf.PI * 8f) * 0.07f,
                        0f);
                }
                else
                {
                    axeRoot.localRotation = Quaternion.Slerp(axeRoot.localRotation, axeRestLocalRotation,
                        1f - Mathf.Exp(-22f * Time.deltaTime));
                    axeRoot.localPosition = Vector3.Lerp(axeRoot.localPosition, axeRestLocalPosition,
                        1f - Mathf.Exp(-22f * Time.deltaTime));
                }
            }
        }

        private void EnsureWeaponVisual()
        {
            if (gunRoot != null)
                return;

            gunRoot = new GameObject("PlayerRifle").transform;
            gunRoot.SetParent(transform, false);
            gunRestLocalPosition = new Vector3(0.34f, 1.02f, 0.42f);
            gunRoot.localPosition = gunRestLocalPosition;
            gunRoot.localRotation = Quaternion.identity;

            Material receiver = RuntimeMaterialFactory.Create(
                "RifleReceiver", new Color(0.07f, 0.09f, 0.12f));
            Material metal = RuntimeMaterialFactory.Create(
                "RifleMetal", new Color(0.18f, 0.21f, 0.24f));
            Material accent = RuntimeMaterialFactory.Create(
                "RifleAccent", new Color(0.86f, 0.62f, 0.14f));
            Material grip = RuntimeMaterialFactory.Create(
                "RifleGrip", new Color(0.10f, 0.12f, 0.14f));

            CreateWeaponPart("Receiver", PrimitiveType.Cube, gunRoot,
                new Vector3(0f, 0f, 0f), new Vector3(0.22f, 0.18f, 0.56f), receiver);
            CreateWeaponPart("Stock", PrimitiveType.Cube, gunRoot,
                new Vector3(0f, 0.015f, -0.38f), new Vector3(0.16f, 0.14f, 0.34f), grip);
            CreateWeaponPart("Magazine", PrimitiveType.Cube, gunRoot,
                new Vector3(0.01f, -0.15f, 0.03f), new Vector3(0.13f, 0.28f, 0.18f), accent);
            CreateWeaponPart("TopRail", PrimitiveType.Cube, gunRoot,
                new Vector3(0f, 0.13f, 0.08f), new Vector3(0.13f, 0.06f, 0.45f), metal);
            CreateWeaponPart("Barrel", PrimitiveType.Cylinder, gunRoot,
                new Vector3(0f, 0f, 0.58f), new Vector3(0.065f, 0.33f, 0.065f), metal,
                Quaternion.Euler(90f, 0f, 0f));
            CreateWeaponPart("MuzzleBreak", PrimitiveType.Cube, gunRoot,
                new Vector3(0f, 0f, 0.94f), new Vector3(0.12f, 0.10f, 0.10f), accent);
            CreateWeaponPart("FrontGrip", PrimitiveType.Cube, gunRoot,
                new Vector3(0f, -0.12f, 0.43f), new Vector3(0.11f, 0.20f, 0.12f), grip);
            CreateWeaponPart("PistolSlide", PrimitiveType.Cube, gunRoot,
                new Vector3(0f, 0.055f, 0.06f), new Vector3(0.17f, 0.045f, 0.27f), metal);
            CreateWeaponPart("PistolGrip", PrimitiveType.Cube, gunRoot,
                new Vector3(0f, -0.115f, -0.035f), new Vector3(0.095f, 0.16f, 0.13f), grip);

            // Start with the pistol-only parts hidden until the active profile is applied.
            gunRoot.Find("PistolSlide").gameObject.SetActive(false);
            gunRoot.Find("PistolGrip").gameObject.SetActive(false);
            gunRenderers = gunRoot.GetComponentsInChildren<Renderer>(true);
            baseGunMaterials = new Material[gunRenderers.Length];
            for (int i = 0; i < gunRenderers.Length; i++)
                baseGunMaterials[i] = gunRenderers[i] != null ? gunRenderers[i].sharedMaterial : null;

            // A separate, lightweight axe model is shown only while the axe is equipped.
            axeRoot = new GameObject("PlayerAxe").transform;
            axeRoot.SetParent(transform, false);
            axeRestLocalPosition = new Vector3(-0.30f, 0.90f, 0.32f);
            axeRestLocalRotation = Quaternion.Euler(0f, 0f, -12f);
            axeRoot.localPosition = axeRestLocalPosition;
            axeRoot.localRotation = axeRestLocalRotation;
            Material axeHandle = RuntimeMaterialFactory.Create("AxeWoodHandle", new Color(0.31f, 0.17f, 0.08f));
            Material axeSteel = RuntimeMaterialFactory.Create("AxeSteelHead", new Color(0.48f, 0.53f, 0.57f));
            CreateWeaponPart("AxeHandle", PrimitiveType.Cube, axeRoot,
                new Vector3(0f, 0f, 0f), new Vector3(0.075f, 0.075f, 0.78f), axeHandle);
            CreateWeaponPart("AxeHead", PrimitiveType.Cube, axeRoot,
                new Vector3(0.02f, 0.12f, 0.22f), new Vector3(0.12f, 0.30f, 0.22f), axeSteel);
            CreateWeaponPart("AxeBladeEdge", PrimitiveType.Cube, axeRoot,
                new Vector3(0.01f, 0.12f, 0.34f), new Vector3(0.08f, 0.34f, 0.10f), axeSteel);
            axeRoot.gameObject.SetActive(false);
            Collider[] axeColliders = axeRoot.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < axeColliders.Length; i++)
                if (axeColliders[i] != null) Object.Destroy(axeColliders[i]);

            swordRoot = new GameObject("PlayerAlienSword").transform;
            swordRoot.SetParent(transform, false);
            swordRestLocalPosition = new Vector3(0.30f, 0.92f, 0.38f);
            swordRestLocalRotation = Quaternion.Euler(0f, 0f, -8f);
            swordRoot.localPosition = swordRestLocalPosition;
            swordRoot.localRotation = swordRestLocalRotation;
            Material swordGrip = RuntimeMaterialFactory.Create("AlienSwordGrip", new Color(0.08f, 0.11f, 0.16f));
            Material swordBlade = RuntimeMaterialFactory.Create("AlienSwordBlade", new Color(0.12f, 0.86f, 1f));
            CreateWeaponPart("SwordHandle", PrimitiveType.Cube, swordRoot,
                new Vector3(0f, 0f, 0f), new Vector3(0.07f, 0.07f, 0.38f), swordGrip);
            CreateWeaponPart("SwordBlade", PrimitiveType.Cube, swordRoot,
                new Vector3(0f, 0.03f, 0.34f), new Vector3(0.09f, 0.07f, 0.62f), swordBlade);
            swordRoot.gameObject.SetActive(false);
            Collider[] swordColliders = swordRoot.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < swordColliders.Length; i++)
                if (swordColliders[i] != null) Object.Destroy(swordColliders[i]);

            // Prevent the decorative weapon from participating in collision queries.
            Collider[] colliders = gunRoot.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
                if (colliders[i] != null) Object.Destroy(colliders[i]);
        }

        private static void CreateWeaponPart(
            string partName,
            PrimitiveType primitiveType,
            Transform parent,
            Vector3 localPosition,
            Vector3 localScale,
            Material material,
            Quaternion? localRotation = null)
        {
            GameObject part = GameObject.CreatePrimitive(primitiveType);
            part.name = partName;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            if (localRotation.HasValue)
                part.transform.localRotation = localRotation.Value;

            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        public void Reload()
        {
            if (magazine >= magazineSize || reserve <= 0) return;
            int amount = Mathf.Min(magazineSize - magazine, reserve);
            magazine += amount;
            reserve -= amount;
        }

        public void AddReserveAmmo(int amount)
        {
            reserve = Mathf.Clamp(reserve + Mathf.Max(0, amount), 0, 180);
        }

        public bool TryMelee()
        {
            if (player == null || player.IsDefeated) return false;
            bool axeAttack = axeEquippedAsMelee || weaponKind == WeaponKind.Axe;
            bool swordAttack = weaponKind == WeaponKind.AlienSword;
            float cooldown = axeAttack ? 0.42f : (swordAttack ? 0.34f : meleeCooldown);
            float attackRange = axeAttack ? 3.2f : (swordAttack ? 3.5f : meleeRange);
            int attackDamage = axeAttack ? 72 : (swordAttack ? 90 : meleeDamage);
            if (Time.time < nextMeleeTime) return false;
            nextMeleeTime = Time.time + cooldown;
            IsMelee = true;
            if (axeAttack && axeRoot != null) axeSwingTimer = AxeSwingDuration;
            if (swordAttack && swordRoot != null) swordSwingTimer = AxeSwingDuration;

            TargetHealth[] targets = Object.FindObjectsByType<TargetHealth>(FindObjectsSortMode.None);
            Vector3 origin = transform.position;
            Vector3 forward = transform.forward;
            bool hitSomething = false;

            foreach (TargetHealth target in targets)
            {
                if (target == null || target.transform == transform || target.GetComponentInParent<EnemyChase>() == null) continue;
                Vector3 delta = target.transform.position - origin;
                delta.y = 0f;
                float distance = delta.magnitude;
                if (distance <= 0.01f || distance > attackRange) continue;
                if (Vector3.Dot(forward, delta.normalized) < 0.25f) continue;

                target.ApplyDamage(attackDamage, transform);
                hitSomething = true;
            }

            return hitSomething;
        }
    }
}