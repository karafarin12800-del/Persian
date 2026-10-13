using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    /// <summary>
    /// Stage-two-only tactical movement layered over the existing enemy combat AI.
    /// It adds flanking and short retreats without replacing the proven attack logic.
    /// </summary>
    public sealed class StageTwoAlienTactics : MonoBehaviour
    {
        private Transform target;
        private int archetype;
        private float nextDecision;
        private Vector3 flankDirection;

        public void Configure(Transform player, int alienArchetype)
        {
            target = player;
            archetype = alienArchetype;
            flankDirection = (Random.value < 0.5f ? -1f : 1f) * Vector3.right;
            nextDecision = Time.time + Random.Range(0.2f, 0.8f);
        }

        private void Update()
        {
            if (target == null || !enabled) return;
            EnemyChase combat = GetComponent<EnemyChase>();
            if (combat != null && !combat.enabled) return;

            Vector3 toPlayer = target.position - transform.position;
            toPlayer.y = 0f;
            float distance = toPlayer.magnitude;
            if (distance < 0.01f || distance > 22f) return;

            TargetHealth health = GetComponent<TargetHealth>();
            bool retreat = health != null && health.CurrentHealth < health.MaxHealth * 0.28f;
            Vector3 direction = toPlayer.normalized;
            Vector3 desired;

            if (retreat && distance < 8f)
            {
                desired = -direction;
            }
            else
            {
                if (Time.time >= nextDecision)
                {
                    nextDecision = Time.time + Random.Range(0.8f, 1.6f);
                    float side = Random.value < 0.5f ? -1f : 1f;
                    flankDirection = Vector3.Cross(Vector3.up, direction) * side;
                }

                bool lineBlocked = Physics.Raycast(transform.position + Vector3.up * 0.9f,
                    direction, out RaycastHit sightHit, distance, ~0, QueryTriggerInteraction.Ignore)
                    && sightHit.collider != null
                    && !sightHit.collider.transform.IsChildOf(transform)
                    && !sightHit.collider.transform.IsChildOf(target);

                desired = lineBlocked
                    ? (flankDirection + direction * 0.18f).normalized
                    : (flankDirection + direction * (archetype == 3 ? 0.55f : 0.28f)).normalized;
            }

            float speed = retreat ? 1.35f : (archetype == 3 ? 1.05f : 0.72f);
            Vector3 step = desired * speed * Time.deltaTime;
            if (Physics.SphereCast(transform.position + Vector3.up * 0.8f, 0.28f,
                desired, out RaycastHit obstacle, Mathf.Max(0.15f, step.magnitude + 0.18f),
                ~0, QueryTriggerInteraction.Ignore))
            {
                if (obstacle.collider != null &&
                    !obstacle.collider.transform.IsChildOf(transform) &&
                    !obstacle.collider.transform.IsChildOf(target))
                {
                    Vector3 alternate = Vector3.Cross(Vector3.up, desired);
                    if (Vector3.Dot(alternate, flankDirection) < 0f) alternate = -alternate;
                    step = alternate.normalized * speed * Time.deltaTime;
                }
            }

            transform.position += step;
        }
    }
}
