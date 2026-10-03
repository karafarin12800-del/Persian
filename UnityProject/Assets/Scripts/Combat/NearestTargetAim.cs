using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public sealed class NearestTargetAim : MonoBehaviour
    {
        [SerializeField] private float range = 70f;
        [SerializeField] private LayerMask targetMask = ~0;
        [SerializeField] private WeaponController weapon;
        [SerializeField] private float autoFireInterval = 0.20f;
        [SerializeField] private bool autoFire;
        [SerializeField] private float targetScanInterval = 0.12f;

        private float nextFire;
        private float nextTargetScanTime;
        private readonly Collider[] targetHits = new Collider[64];

#if UNITY_ANDROID
        private static bool androidTargetScanArmed;
        public static void SetAndroidTargetScanArmed(bool armed) => androidTargetScanArmed = armed;
#endif

        public TargetHealth CurrentTarget { get; private set; }

        private void Awake()
        {
            if (weapon == null)
                weapon = GetComponent<WeaponController>();
        }

        private void Update()
        {
#if UNITY_ANDROID
            if (!androidTargetScanArmed)
            {
                CurrentTarget = null;
                return;
            }
#endif
            if (Time.unscaledTime >= nextTargetScanTime)
            {
                nextTargetScanTime = Time.unscaledTime + Mathf.Max(0.05f, targetScanInterval);
                CurrentTarget = FindNearestTarget();
            }

            if (!autoFire || Time.time < nextFire)
                return;

            if (CurrentTarget != null)
            {
                if (FireAt(CurrentTarget.transform.position))
                    nextFire = Time.time + autoFireInterval;
            }
        }

        public bool FireAt(Vector3 targetPosition)
        {
            if (weapon == null || Time.time < nextFire)
                return false;

            Vector3 origin = weapon.Muzzle.position;
            Vector3 target = targetPosition + Vector3.up * 0.75f;
            Vector3 direction = target - origin;
            direction.y = 0f;

            if (direction.sqrMagnitude < 0.001f)
                return false;

            if (!IsPathClear(origin, targetPosition))
                return false;

            if (!weapon.TryFireDirection(direction.normalized))
                return false;

            nextFire = Time.time + autoFireInterval;
            return true;
        }

        public bool FireForward(Vector3 forward)
        {
            if (weapon == null || Time.time < nextFire)
                return false;

            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f)
                return false;

            if (!weapon.TryFireDirection(forward.normalized))
                return false;

            nextFire = Time.time + autoFireInterval;
            return true;
        }

        private TargetHealth FindNearestTarget()
        {
            int hitCount = Physics.OverlapSphereNonAlloc(
                transform.position,
                range,
                targetHits,
                targetMask,
                QueryTriggerInteraction.Ignore);

            TargetHealth best = null;
            float bestDistance = float.PositiveInfinity;

            for (int i = 0; i < hitCount; i++)
            {
                Collider hit = targetHits[i];
                if (hit == null) continue;
                TargetHealth candidate = hit.GetComponentInParent<TargetHealth>();
                if (candidate == null || !candidate.isActiveAndEnabled)
                    continue;

                if (candidate.GetComponentInParent<EnemyChase>() == null)
                    continue;

                float distance = (candidate.transform.position - transform.position).sqrMagnitude;
                if (distance >= bestDistance)
                    continue;

                Vector3 origin = weapon != null ? weapon.Muzzle.position : transform.position + Vector3.up;
                if (!IsPathClear(origin, candidate.transform.position))
                    continue;

                bestDistance = distance;
                best = candidate;
            }

            return best;
        }

        private static bool IsPathClear(Vector3 origin, Vector3 targetPosition)
        {
            Vector3 target = targetPosition + Vector3.up * 0.75f;
            Vector3 direction = target - origin;
            float distance = direction.magnitude;

            if (distance <= 0.01f)
                return true;

            if (!Physics.Raycast(origin, direction.normalized, out RaycastHit hit, distance, ~0, QueryTriggerInteraction.Ignore))
                return true;

            TargetHealth hitTarget = hit.collider.GetComponentInParent<TargetHealth>();
            return hitTarget != null && hitTarget.GetComponentInParent<EnemyChase>() != null;
        }
    }
}
