using System.Collections;
using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public sealed class GrenadeController : MonoBehaviour
    {
        [SerializeField] private GameObject grenadePrefab;
        [SerializeField] private Transform throwOrigin;
        [SerializeField] private float throwDistance = 6f;
        [SerializeField] private float explosionRadius = 2.8f;
        [SerializeField] private int damage = 60;
        [SerializeField] private int grenades = 3;
        [SerializeField] private float fuse = 0.8f;

        private PlayerInventory inventory;
        private Transform owner;

        public int Grenades => inventory != null ? inventory.Grenades : grenades;

        private void Awake()
        {
            inventory = GetComponent<PlayerInventory>();
            owner = transform;
            if (throwOrigin == null)
                throwOrigin = transform;
        }

        public bool Throw(Vector2 direction)
        {
            if (direction.sqrMagnitude < 0.0001f)
                return false;

            if (inventory != null)
            {
                if (!inventory.TryConsumeGrenade())
                    return false;
            }
            else
            {
                if (grenades <= 0)
                    return false;
                grenades--;
            }

            Vector2 normalized = direction.normalized;
            Vector3 origin = throwOrigin != null ? throwOrigin.position + Vector3.up * 0.5f : transform.position + Vector3.up * 0.5f;
            Vector3 target = origin + new Vector3(normalized.x, 0f, normalized.y) * throwDistance;
            target.y = 0.2f;
            StartCoroutine(ResolveThrow(origin, target));
            return true;
        }

        private IEnumerator ResolveThrow(Vector3 origin, Vector3 target)
        {
            GameObject grenade = grenadePrefab != null
                ? Instantiate(grenadePrefab, origin, Quaternion.identity)
                : CreateRuntimeGrenade(origin);

            float elapsed = 0f;
            while (elapsed < fuse)
            {
                elapsed += Time.deltaTime;
                if (grenade != null)
                {
                    float t = Mathf.Clamp01(elapsed / fuse);
                    grenade.transform.position = Vector3.Lerp(origin, target, t);
                    grenade.transform.localScale = Vector3.one * (0.18f + 0.06f * Mathf.Sin(t * Mathf.PI));
                }
                yield return null;
            }

            // Make the detonation readable even when no enemy is inside the blast.
            CreateExplosionFeedback(target);

            Collider[] hits = Physics.OverlapSphere(target, explosionRadius, ~0, QueryTriggerInteraction.Collide);
            foreach (Collider hit in hits)
            {
                TargetHealth health = hit.GetComponentInParent<TargetHealth>();
                EnemyChase enemy = hit.GetComponentInParent<EnemyChase>();
                if (health == null || enemy == null) continue;

                float distance = (health.transform.position - target).magnitude;
                if (distance > explosionRadius) continue;

                int scaledDamage = Mathf.RoundToInt(damage * Mathf.Clamp01(1f - distance / explosionRadius));
                health.ApplyDamage(Mathf.Max(1, scaledDamage), owner);
            }

            if (grenade != null)
                Destroy(grenade);
        }

        private void CreateExplosionFeedback(Vector3 center)
        {
#if UNITY_ANDROID
            GameObject flash = AndroidSafeRuntimeFactory.CreateMarker(
                "GrenadeExplosionFlash",
                center,
                new Vector3(explosionRadius * 1.5f, 0.12f, explosionRadius * 1.5f),
                new Color(1f, 0.48f, 0.08f, 0.95f),
                false);
#else
            GameObject flash = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            flash.name = "GrenadeExplosionFlash";
            flash.transform.position = center;
            flash.transform.localScale = new Vector3(explosionRadius * 1.5f, 0.12f, explosionRadius * 1.5f);
            Collider flashCollider = flash.GetComponent<Collider>();
            if (flashCollider != null) Destroy(flashCollider);
            Renderer flashRenderer = flash.GetComponent<Renderer>();
            if (flashRenderer != null)
                flashRenderer.sharedMaterial = RuntimeMaterialFactory.Create(
                    "GrenadeExplosionFlashMaterial", new Color(1f, 0.48f, 0.08f, 0.95f));
#endif
            if (flash != null)
                Destroy(flash, 0.22f);
        }

        private static GameObject CreateRuntimeGrenade(Vector3 position)
        {
#if UNITY_ANDROID
            return AndroidSafeRuntimeFactory.CreateMarker(
                "RuntimeGrenade",
                position,
                Vector3.one * 0.18f,
                new Color(0.32f, 0.90f, 0.28f),
                false);
#else
            GameObject grenade = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            grenade.name = "RuntimeGrenade";
            grenade.transform.position = position;
            grenade.transform.localScale = Vector3.one * 0.2f;

            Collider collider = grenade.GetComponent<Collider>();
            if (collider != null) Destroy(collider);
            return grenade;
#endif
        }
    }
}
