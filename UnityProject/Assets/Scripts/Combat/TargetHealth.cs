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
            ring.alignment = LineAlignment.TransformZ;
            ring.widthMultiplier = 0.045f;
            ring.startColor = new Color(color.r, color.g, color.b, 0.9f);
            ring.endColor = new Color(color.r, color.g, color.b, 0f);
            ring.sharedMaterial = energyMaterial;
            ring.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ring.receiveShadows = false;
            // Keep the pulse in a fixed world-horizontal plane; View alignment can tilt
            // the ring with camera movement and make it appear to wobble on the ground.
            ringObject.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            for (int i = 0; i < ring.positionCount; i++)
            {
                float angle = i * Mathf.PI * 2f / ring.positionCount;
                ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * 0.5f, Mathf.Sin(angle) * 0.5f, 0f));
            }
            ringObject.transform.localScale = new Vector3(0.12f, 0.12f, 1f);
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
                pulseTransform.localScale = new Vector3(scale, scale, 1f);
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