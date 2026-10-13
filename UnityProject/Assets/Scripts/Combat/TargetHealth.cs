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
            Vector3 deathPosition = enemy != null ? enemy.transform.position : transform.position;
            if (enemy != null)
            {
                // Disable combat and collision, but keep the rendered body visible while it falls.
                enemy.enabled = false;
                Collider[] colliders = enemy.GetComponentsInChildren<Collider>();
                for (int i = 0; i < colliders.Length; i++)
                    if (colliders[i] != null) colliders[i].enabled = false;

                MonoBehaviour[] behaviours = enemy.GetComponentsInChildren<MonoBehaviour>();
                for (int i = 0; i < behaviours.Length; i++)
                    if (behaviours[i] != null && behaviours[i] != this) behaviours[i].enabled = false;

                StartCoroutine(FallAndRemoveEnemy(enemy.transform));
            }

            SpawnBloodEffect(deathPosition);
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

        private static void SpawnBloodEffect(Vector3 position)
        {
            // A flat, irregular stain remains on the ground for three seconds.
            GameObject stain = new GameObject("EnemyBloodStain");
            stain.transform.position = new Vector3(position.x, 0.055f, position.z);
            Material stainMaterial = RuntimeMaterialFactory.Create(
                "EnemyBloodStainMaterial", new Color(0.30f, 0.008f, 0.018f));

            CreateBloodStainBlob(stain.transform, stainMaterial,
                new Vector3(0f, 0f, 0f), new Vector3(0.82f, 0.018f, 0.56f), 0f);
            CreateBloodStainBlob(stain.transform, stainMaterial,
                new Vector3(0.24f, 0.002f, 0.10f), new Vector3(0.48f, 0.016f, 0.32f), 24f);
            CreateBloodStainBlob(stain.transform, stainMaterial,
                new Vector3(-0.22f, 0.003f, -0.12f), new Vector3(0.42f, 0.015f, 0.30f), -31f);
            Destroy(stain, 3f);

            // Short impact spray supplements the persistent ground stain.
            GameObject blood = new GameObject("EnemyDefeatBloodParticles");
            blood.transform.position = position + Vector3.up * 0.35f;
            ParticleSystem particles = blood.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.duration = 0.45f;
            main.loop = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.20f, 0.55f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.7f, 1.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.06f, 0.13f);
            main.startColor = new Color(0.62f, 0.025f, 0.035f, 0.9f);
            main.maxParticles = 22;
            var emission = particles.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 18) });
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 42f;
            shape.radius = 0.12f;
            var particleRenderer = particles.GetComponent<ParticleSystemRenderer>();
            particleRenderer.material = RuntimeMaterialFactory.Create(
                "EnemyBloodEffectMaterial", new Color(0.62f, 0.025f, 0.035f));
            particles.Play();
            Destroy(blood, 1.2f);
        }

        private static void CreateBloodStainBlob(
            Transform parent, Material material, Vector3 localPosition, Vector3 localScale, float rotationY)
        {
            GameObject blob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            blob.name = "BloodStainBlob";
            blob.transform.SetParent(parent, false);
            blob.transform.localPosition = localPosition;
            blob.transform.localScale = localScale;
            blob.transform.localRotation = Quaternion.Euler(0f, rotationY, 0f);

            Collider collider = blob.GetComponent<Collider>();
            if (collider != null)
                Destroy(collider);

            Renderer blobRenderer = blob.GetComponent<Renderer>();
            if (blobRenderer != null)
            {
                blobRenderer.sharedMaterial = material;
                blobRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                blobRenderer.receiveShadows = false;
            }
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