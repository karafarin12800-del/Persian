using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public sealed class EnemyChase : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private float moveSpeed = 2.2f;
        [SerializeField] private float stopDistance = 1.6f;
        [SerializeField] private float retargetInterval = 0.5f;

        private float nextRetarget;

        private void Update()
        {
            if (target == null || Time.time < nextRetarget)
                return;

            nextRetarget = Time.time + retargetInterval;
            Vector3 delta = target.position - transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude <= stopDistance * stopDistance)
                return;

            transform.position += delta.normalized * moveSpeed * retargetInterval;
            transform.rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
        }

        public void SetTarget(Transform value)
        {
            target = value;
        }
    }
}
