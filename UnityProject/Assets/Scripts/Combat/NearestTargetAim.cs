using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public sealed class NearestTargetAim : MonoBehaviour
    {
        [SerializeField] private float range = 24f;
        [SerializeField] private LayerMask targetMask = ~0;
        [SerializeField] private WeaponController weapon;
        [SerializeField] private float autoFireInterval = 0.22f;
        [SerializeField] private bool autoFire;

        private float nextFire;

        public TargetHealth CurrentTarget { get; private set; }

        private void Awake()
        {
            if (weapon == null)
                weapon = GetComponent<WeaponController>();
        }

        private void Update()
        {
            CurrentTarget = FindNearestTarget();
            if (!autoFire || CurrentTarget == null || Time.time < nextFire)
                return;

            FireAt(CurrentTarget.transform.position);
        }

        public bool FireAt(Vector3 targetPosition)
        {
            if (weapon == null || Time.time < nextFire)
                return false;

            Vector3 direction = targetPosition - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f)
                return false;

            if (!weapon.TryFire(targetPosition))
                return false;

            nextFire = Time.time + autoFireInterval;
            return true;
        }

        private TargetHealth FindNearestTarget()
        {
            Collider[] hits = Physics.OverlapSphere(transform.position, range, targetMask, QueryTriggerInteraction.Collide);
            TargetHealth best = null;
            float bestDistance = float.PositiveInfinity;

            foreach (Collider hit in hits)
            {
                TargetHealth candidate = hit.GetComponentInParent<TargetHealth>();
                if (candidate == null || !candidate.isActiveAndEnabled || candidate.transform == transform)
                    continue;

                float distance = (candidate.transform.position - transform.position).sqrMagnitude;
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    best = candidate;
                }
            }

            return best;
        }
    }
}
