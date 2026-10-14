using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    /// <summary>Armed offline competitor AI used to populate Stage 2 while online matchmaking is unavailable.</summary>
    public sealed class StageTwoBotCompetitor : MonoBehaviour
    {
        private StageTwoArenaManager arena;
        private float nextThinkTime;
        private float nextAttackTime;
        private Vector3 roamDirection;
        private EnemyChase currentTarget;
        private int number;
        private Transform weaponRoot;
        private Transform muzzle;
        private Material weaponMaterial;
        private Material metalMaterial;
        private Projectile projectileTemplate;

        private const float PreferredRange = 12f;
        private const float MaximumRange = 25f;

        public string CompetitorId => "AI-" + number.ToString("00");

        public void Configure(StageTwoArenaManager manager, int competitorNumber)
        {
            arena = manager;
            number = competitorNumber;
            roamDirection = Random.insideUnitSphere;
            roamDirection.y = 0f;
            if (roamDirection.sqrMagnitude < 0.01f) roamDirection = Vector3.forward;
            roamDirection.Normalize();
            BuildVisibleRifle();
        }

        private void Update()
        {
            if (GameSession.Instance != null && GameSession.Instance.IsFinished) return;

            if (Time.time >= nextThinkTime)
            {
                nextThinkTime = Time.time + Random.Range(0.22f, 0.38f);
                FindTarget();
            }

            if (currentTarget == null)
            {
                Roam();
                return;
            }

            Vector3 delta = currentTarget.transform.position - transform.position;
            delta.y = 0f;
            float distance = delta.magnitude;
            if (distance < 0.01f) return;

            Vector3 direction = delta / distance;
            if (distance > PreferredRange || !HasLineOfSight(currentTarget, distance))
            {
                Vector3 moveDirection = direction;
                if (!HasLineOfSight(currentTarget, distance))
                {
                    Vector3 side = Vector3.Cross(Vector3.up, direction) * (number % 2 == 0 ? 1f : -1f);
                    moveDirection = (side + direction * 0.3f).normalized;
                }

                if (Physics.SphereCast(transform.position + Vector3.up * 0.75f, 0.28f,
                    moveDirection, out RaycastHit obstacle, 1.0f, ~0, QueryTriggerInteraction.Ignore))
                {
                    if (obstacle.collider != null &&
                        !obstacle.collider.transform.IsChildOf(transform) &&
                        !obstacle.collider.transform.IsChildOf(currentTarget.transform))
                    {
                        Vector3 side = Vector3.Cross(Vector3.up, moveDirection) *
                            (number % 2 == 0 ? 1f : -1f);
                        moveDirection = (side + direction * 0.15f).normalized;
                    }
                }

                transform.position += moveDirection * (2.6f * Time.deltaTime);
            }

            transform.rotation = Quaternion.Slerp(transform.rotation,
                Quaternion.LookRotation(direction, Vector3.up),
                1f - Mathf.Exp(-12f * Time.deltaTime));

            // Competitors visibly aim and fire their own projectiles instead of
            // silently subtracting enemy health at point-blank range.
            if (distance <= MaximumRange && HasLineOfSight(currentTarget, distance) &&
                Time.time >= nextAttackTime)
            {
                nextAttackTime = Time.time + Random.Range(0.48f, 0.72f);
                FireAt(currentTarget);
            }
        }

        private void FindTarget()
        {
            EnemyChase[] enemies = FindObjectsByType<EnemyChase>(FindObjectsSortMode.None);
            float nearestDistance = MaximumRange * MaximumRange;
            currentTarget = null;

            for (int i = 0; i < enemies.Length; i++)
            {
                EnemyChase candidate = enemies[i];
                if (candidate == null || !candidate.enabled) continue;

                float distance = (candidate.transform.position - transform.position).sqrMagnitude;
                if (distance >= nearestDistance) continue;

                float worldDistance = Mathf.Sqrt(distance);
                if (!HasLineOfSight(candidate, worldDistance)) continue;

                nearestDistance = distance;
                currentTarget = candidate;
            }
        }

        private bool HasLineOfSight(EnemyChase target, float distance)
        {
            if (target == null) return false;
            Vector3 origin = transform.position + Vector3.up * 1.0f;
            Vector3 destination = target.transform.position + Vector3.up * 0.85f;
            Vector3 delta = destination - origin;
            float length = delta.magnitude;
            if (length <= 0.01f) return true;

            RaycastHit[] hits = Physics.RaycastAll(origin, delta / length, length,
                ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            for (int i = 0; i < hits.Length; i++)
            {
                Collider hit = hits[i].collider;
                if (hit == null || hit.transform.IsChildOf(transform) || hit.transform == transform)
                    continue;
                if (hit.GetComponentInParent<EnemyChase>() == target)
                    return true;
                // Any solid object before the target blocks this competitor's shot.
                return false;
            }
            return false;
        }

        private void FireAt(EnemyChase target)
        {
            if (target == null) return;
            Vector3 origin = muzzle != null ? muzzle.position : transform.position + Vector3.up;
            Vector3 aimPoint = target.transform.position + Vector3.up * 0.82f;
            Vector3 direction = aimPoint - origin;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f) return;

            if (weaponRoot != null)
                weaponRoot.localRotation = Quaternion.Euler(0f, 0f, Random.Range(-1.5f, 1.5f));

            if (projectileTemplate == null)
                CreateProjectileTemplate();
            if (projectileTemplate == null) return;

            Projectile shot = Instantiate(projectileTemplate, origin, Quaternion.LookRotation(direction.normalized));
            shot.SetDefaults(38f, MaximumRange / 38f, 18);
            shot.SetOwner(transform);
            shot.gameObject.SetActive(true);
            shot.Launch(direction.normalized);
        }

        private void BuildVisibleRifle()
        {
            if (weaponRoot != null) return;
            weaponRoot = new GameObject("Competitor_Rifle").transform;
            weaponRoot.SetParent(transform, false);
            weaponRoot.localPosition = new Vector3(0.30f, 1.00f, 0.42f);
            weaponRoot.localRotation = Quaternion.identity;

            weaponMaterial = RuntimeMaterialFactory.Create("CompetitorRifle_" + number,
                number % 3 == 0 ? new Color(0.12f, 0.28f, 0.34f) : new Color(0.10f, 0.12f, 0.15f));
            metalMaterial = RuntimeMaterialFactory.Create("CompetitorRifleMetal_" + number,
                new Color(0.45f, 0.49f, 0.52f));

            CreateWeaponPart("RifleReceiver", new Vector3(0f, 0f, 0.02f),
                new Vector3(0.16f, 0.14f, 0.42f), weaponMaterial);
            CreateWeaponPart("RifleStock", new Vector3(0f, 0f, -0.28f),
                new Vector3(0.12f, 0.11f, 0.22f), weaponMaterial);
            CreateWeaponPart("RifleBarrel", new Vector3(0f, 0.01f, 0.34f),
                new Vector3(0.055f, 0.055f, 0.34f), metalMaterial);
            CreateWeaponPart("RifleMagazine", new Vector3(0f, -0.12f, 0.04f),
                new Vector3(0.085f, 0.18f, 0.10f), metalMaterial);

            GameObject muzzleObject = new GameObject("CompetitorMuzzle");
            muzzle = muzzleObject.transform;
            muzzle.SetParent(weaponRoot, false);
            muzzle.localPosition = new Vector3(0f, 0.01f, 0.53f);
        }

        private void CreateWeaponPart(string partName, Vector3 localPosition, Vector3 localScale, Material material)
        {
            GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = partName;
            part.transform.SetParent(weaponRoot, false);
            part.transform.localPosition = localPosition;
            part.transform.localScale = localScale;
            Collider collider = part.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            Renderer renderer = part.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
        }

        private void CreateProjectileTemplate()
        {
            GameObject template = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            template.name = "CompetitorProjectileTemplate";
            template.transform.localScale = Vector3.one * 0.13f;
            Collider collider = template.GetComponent<Collider>();
            if (collider != null) collider.enabled = false;
            Renderer renderer = template.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = RuntimeMaterialFactory.Create(
                    "CompetitorProjectileMaterial", new Color(1f, 0.72f, 0.18f));
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }
            template.SetActive(false);
            projectileTemplate = template.AddComponent<Projectile>();
        }

        private void Roam()
        {
            roamDirection = Quaternion.Euler(0f, Random.Range(-32f, 32f) * Time.deltaTime, 0f) * roamDirection;
            Vector3 step = roamDirection * (1.1f * Time.deltaTime);
            if (!Physics.SphereCast(transform.position + Vector3.up * 0.7f, 0.28f,
                roamDirection, out RaycastHit hit, 0.8f, ~0, QueryTriggerInteraction.Ignore))
            {
                transform.position += step;
            }
            else
            {
                roamDirection = Vector3.Cross(Vector3.up, roamDirection).normalized *
                    (number % 2 == 0 ? 1f : -1f);
            }

            if (roamDirection.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.Slerp(transform.rotation,
                    Quaternion.LookRotation(roamDirection, Vector3.up),
                    1f - Mathf.Exp(-4f * Time.deltaTime));
        }

        private void OnDestroy()
        {
            if (projectileTemplate != null)
                Destroy(projectileTemplate.gameObject);
        }
    }
}