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
        [SerializeField] private float targetRefreshInterval = 0.16f;

        private float nextFire;
        private float nextTargetRefresh;
        private static readonly Collider[] OverlapBuffer = new Collider[256];
        private static readonly TargetHealth[] CandidateBuffer = new TargetHealth[128];
        private static readonly float[] CandidateDistanceBuffer = new float[128];

        public TargetHealth CurrentTarget { get; private set; }

        private void Awake()
        {
            if (weapon == null)
                weapon = GetComponent<WeaponController>();
        }

        private void Update()
        {
            // Target discovery is intentionally throttled and uses reusable buffers.
            // The old per-frame OverlapSphere allocated arrays and raycast every
            // candidate; with 80+ Stage 3 hostiles this could stall mobile devices.
            if (Time.time >= nextTargetRefresh)
            {
                nextTargetRefresh = Time.time + Mathf.Clamp(targetRefreshInterval, 0.08f, 0.5f);
                CurrentTarget = FindNearestTarget();
            }

            if (!autoFire || Time.time < nextFire || CurrentTarget == null)
                return;

            if (FireAt(CurrentTarget.transform.position))
                nextFire = Time.time + autoFireInterval;
        }

        public bool FireAt(Vector3 targetPosition)
        {
            if (weapon == null || Time.time < nextFire)
                return false;

            Vector3 origin = weapon.Muzzle.position;
            Vector3 target = targetPosition + Vector3.up * 0.75f;
            Vector3 direction = target - origin;
            direction.y = 0f;

            if (direction.sqrMagnitude < 0.001f || !IsPathClear(origin, targetPosition))
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
            if (forward.sqrMagnitude < 0.001f || !weapon.TryFireDirection(forward.normalized))
                return false;

            nextFire = Time.time + autoFireInterval;
            return true;
        }

        private TargetHealth FindNearestTarget()
        {
            float activeRange = weapon != null ? Mathf.Min(range, weapon.EffectiveRange) : range;
            int overlapCount = Physics.OverlapSphereNonAlloc(
                transform.position, activeRange, OverlapBuffer, targetMask, QueryTriggerInteraction.Ignore);

            int candidateCount = 0;
            Vector3 selfPosition = transform.position;

            // Build a unique, distance-ordered list without allocating a List or LINQ iterator.
            for (int i = 0; i < overlapCount; i++)
            {
                Collider hit = OverlapBuffer[i];
                if (hit == null)
                    continue;

                TargetHealth candidate = hit.GetComponentInParent<TargetHealth>();
                if (candidate == null || !candidate.isActiveAndEnabled ||
                    candidate.CurrentHealth <= 0 ||
                    candidate.GetComponentInParent<EnemyChase>() == null)
                    continue;

                bool duplicate = false;
                for (int j = 0; j < candidateCount; j++)
                {
                    if (CandidateBuffer[j] == candidate)
                    {
                        duplicate = true;
                        break;
                    }
                }
                if (duplicate || candidateCount >= CandidateBuffer.Length)
                    continue;

                float sqrDistance = (candidate.transform.position - selfPosition).sqrMagnitude;
                int insertAt = candidateCount;
                while (insertAt > 0 && CandidateDistanceBuffer[insertAt - 1] > sqrDistance)
                {
                    CandidateBuffer[insertAt] = CandidateBuffer[insertAt - 1];
                    CandidateDistanceBuffer[insertAt] = CandidateDistanceBuffer[insertAt - 1];
                    insertAt--;
                }
                CandidateBuffer[insertAt] = candidate;
                CandidateDistanceBuffer[insertAt] = sqrDistance;
                candidateCount++;
            }

            Vector3 origin = weapon != null ? weapon.Muzzle.position : selfPosition + Vector3.up;
            for (int i = 0; i < candidateCount; i++)
            {
                TargetHealth candidate = CandidateBuffer[i];
                if (candidate != null && candidate.CurrentHealth > 0 &&
                    IsPathClear(origin, candidate.transform.position))
                {
                    ClearCandidateBuffer(candidateCount);
                    return candidate;
                }
            }

            ClearCandidateBuffer(candidateCount);
            return null;
        }

        private static void ClearCandidateBuffer(int count)
        {
            for (int i = 0; i < count; i++)
            {
                CandidateBuffer[i] = null;
                CandidateDistanceBuffer[i] = 0f;
            }
        }

        private static bool IsPathClear(Vector3 origin, Vector3 targetPosition)
        {
            Vector3 target = targetPosition + Vector3.up * 0.75f;
            Vector3 direction = target - origin;
            float distance = direction.magnitude;

            if (distance <= 0.01f)
                return true;

            if (!Physics.Raycast(origin, direction / distance, out RaycastHit hit, distance,
                ~0, QueryTriggerInteraction.Ignore))
                return true;

            TargetHealth hitTarget = hit.collider.GetComponentInParent<TargetHealth>();
            return hitTarget != null && hitTarget.CurrentHealth > 0 &&
                hitTarget.GetComponentInParent<EnemyChase>() != null;
        }
    }
}
