using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public sealed class TargetHealth : MonoBehaviour
    {
        [SerializeField] private int maxHealth = 100;
        public int CurrentHealth { get; private set; }
        public int MaxHealth => maxHealth;

        private void Awake()
        {
            CurrentHealth = maxHealth;
        }

        public void SetMaxHealth(int value)
        {
            maxHealth = Mathf.Max(1, value);
            CurrentHealth = maxHealth;
        }

        public void ApplyDamage(int amount)
        {
            if (CurrentHealth <= 0) return;
            amount = Mathf.Max(0, amount);
            if (amount == 0) return;

            CurrentHealth = Mathf.Max(0, CurrentHealth - amount);
            if (CurrentHealth > 0) return;

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
                if (GameSession.Instance != null)
                {
                    GameSession.Instance.RegisterEnemyDefeated();
                    GameSession.Instance.AddScore(scoreBonus);
                }

                SpawnDroppedWeapon(enemy);
                BeginEnemyDeathSequence(enemy);
                return;
            }

            Destroy(gameObject);
        }

        private void BeginEnemyDeathSequence(EnemyChase enemy)
        {
            // Keep the enemy body for a short fall-and-fade beat instead of deleting it instantly.
            if (enemy != null)
            {
                enemy.enabled = false;
                Collider[] colliders = enemy.GetComponentsInChildren<Collider>();
                for (int i = 0; i < colliders.Length; i++)
                    if (colliders[i] != null) colliders[i].enabled = false;
                MonoBehaviour[] behaviours = enemy.GetComponentsInChildren<MonoBehaviour>();
                for (int i = 0; i < behaviours.Length; i++)
                    if (behaviours[i] != null && behaviours[i] != this) behaviours[i].enabled = false;
                StartCoroutine(FallAndFadeEnemy(enemy.transform));
            }
            SpawnBloodEffect(transform.position + Vector3.up * 0.35f);
        }

        private System.Collections.IEnumerator FallAndFadeEnemy(Transform body)
        {
            if (body == null) yield break;
            Vector3 startPosition = body.position;
            Quaternion startRotation = body.rotation;
            float elapsed = 0f;
            const float fallDuration = 0.55f;
            while (elapsed < fallDuration && body != null)
            {
                elapsed += Time.deltaTime;
                float t = Mathf.Clamp01(elapsed / fallDuration);
                body.position = startPosition + Vector3.down * (0.45f * t);
                body.rotation = startRotation * Quaternion.Euler(75f * t, 0f, 18f * t);
                yield return null;
            }
            if (body == null) yield break;
            Renderer[] renderers = body.GetComponentsInChildren<Renderer>();
            float fadeElapsed = 0f;
            const float fadeDuration = 1.35f;
            while (fadeElapsed < fadeDuration && body != null)
            {
                fadeElapsed += Time.deltaTime;
                float scale = Mathf.Lerp(1f, 0.08f, fadeElapsed / fadeDuration);
                body.localScale = Vector3.one * scale;
                yield return null;
            }
            if (body != null) Destroy(body.gameObject);
        }

        private static void SpawnBloodEffect(Vector3 position)
        {
            GameObject blood = new GameObject("EnemyDefeatBloodEffect");
            blood.transform.position = position;
            ParticleSystem particles = blood.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.duration = 0.55f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.25f, 0.65f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.0f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.07f, 0.16f);
            main.startColor = new Color(0.62f, 0.025f, 0.035f, 0.9f);
            main.maxParticles = 28;
            var emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 22) });
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 42f;
            shape.radius = 0.12f;
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.material = RuntimeMaterialFactory.Create("EnemyBloodEffectMaterial", new Color(0.62f, 0.025f, 0.035f));
            particles.Play();
            Destroy(blood, 1.6f);
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

            // Add an extra 2x size boost specifically to enemy drops; PickupItem
            // applies its own 2x readable-pickup scale after this, for a clear 4x
            // visible weapon compared with the original tiny dropped model.
            for (int childIndex = 0; childIndex < dropped.transform.childCount; childIndex++)
                dropped.transform.GetChild(childIndex).localScale *= 2f;

            PickupItem pickup = dropped.AddComponent<PickupItem>();
            pickup.ConfigureWeapon(enemy.DroppedWeaponKind, 24);
            Debug.Log("PERSIA_COMBAT: dropped enemy weapon " + enemy.DroppedWeaponKind);
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