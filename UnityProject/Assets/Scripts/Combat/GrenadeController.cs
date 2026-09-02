using System.Collections;
using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public sealed class GrenadeController : MonoBehaviour
    {
        [SerializeField] private GameObject grenadePrefab;
        [SerializeField] private Transform throwOrigin;
        [SerializeField] private float throwDistance = 5f;
        [SerializeField] private float explosionRadius = 2.5f;
        [SerializeField] private int damage = 60;
        [SerializeField] private int grenades = 3;
        [SerializeField] private float fuse = 0.8f;

        public int Grenades => grenades;

        public bool Throw(Vector2 direction)
        {
            if (grenades <= 0 || direction.sqrMagnitude < 0.0001f)
                return false;

            grenades--;
            Vector3 origin = throwOrigin != null ? throwOrigin.position : transform.position;
            Vector2 normalized = direction.normalized;
            Vector3 target = origin + new Vector3(normalized.x, 0f, normalized.y) * throwDistance;
            StartCoroutine(ResolveThrow(target));
            return true;
        }

        private IEnumerator ResolveThrow(Vector3 target)
        {
            GameObject grenade = null;
            if (grenadePrefab != null)
                grenade = Instantiate(grenadePrefab, target, Quaternion.identity);

            yield return new WaitForSeconds(fuse);

            Collider[] hits = Physics.OverlapSphere(target, explosionRadius, ~0, QueryTriggerInteraction.Collide);
            foreach (Collider hit in hits)
            {
                TargetHealth health = hit.GetComponentInParent<TargetHealth>();
                if (health != null && hit.CompareTag("Enemy"))
                    health.ApplyDamage(damage);
            }

            if (grenade != null)
                Destroy(grenade);
        }
    }
}
