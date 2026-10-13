using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    /// <summary>Lightweight offline competitor AI used only to fill the Stage 2 lobby.</summary>
    public sealed class StageTwoBotCompetitor : MonoBehaviour
    {
        private StageTwoArenaManager arena;
        private float nextThinkTime;
        private float nextAttackTime;
        private Vector3 roamDirection;
        private int number;

        public string CompetitorId => "AI-" + number.ToString("00");

        public void Configure(StageTwoArenaManager manager, int competitorNumber)
        {
            arena = manager;
            number = competitorNumber;
            roamDirection = Random.insideUnitSphere;
            roamDirection.y = 0f;
            if (roamDirection.sqrMagnitude < 0.01f) roamDirection = Vector3.forward;
            roamDirection.Normalize();
        }

        private void Update()
        {
            if (GameSession.Instance != null && GameSession.Instance.IsFinished) return;

            if (Time.time >= nextThinkTime)
            {
                nextThinkTime = Time.time + Random.Range(0.25f, 0.55f);
                EnemyChase[] enemies = FindObjectsByType<EnemyChase>(FindObjectsSortMode.None);
                EnemyChase nearest = null;
                float nearestDistance = float.MaxValue;
                for (int i = 0; i < enemies.Length; i++)
                {
                    EnemyChase candidate = enemies[i];
                    if (candidate == null || !candidate.enabled) continue;
                    float distance = (candidate.transform.position - transform.position).sqrMagnitude;
                    if (distance < nearestDistance)
                    {
                        nearestDistance = distance;
                        nearest = candidate;
                    }
                }

                if (nearest != null)
                {
                    Vector3 delta = nearest.transform.position - transform.position;
                    delta.y = 0f;
                    float distance = delta.magnitude;
                    if (distance > 2.4f)
                    {
                        Vector3 direction = delta.normalized;
                        // Prefer a side-step when a wall blocks the direct approach.
                        if (Physics.SphereCast(transform.position + Vector3.up * 0.8f, 0.32f,
                            direction, out RaycastHit hit, 1.2f, ~0, QueryTriggerInteraction.Ignore))
                        {
                            Vector3 side = Vector3.Cross(Vector3.up, direction) * (number % 2 == 0 ? 1f : -1f);
                            direction = (side + direction * 0.22f).normalized;
                        }
                        transform.position += direction * Mathf.Min(3.2f * Time.deltaTime, Mathf.Max(0f, distance - 1.9f));
                        transform.rotation = Quaternion.Slerp(transform.rotation,
                            Quaternion.LookRotation(delta.normalized, Vector3.up),
                            1f - Mathf.Exp(-10f * Time.deltaTime));
                    }
                    else if (Time.time >= nextAttackTime)
                    {
                        nextAttackTime = Time.time + Random.Range(0.55f, 0.9f);
                        TargetHealth health = nearest.GetComponent<TargetHealth>();
                        if (health != null) health.ApplyDamage(Random.Range(18, 29), transform);
                    }
                }
                else
                {
                    roamDirection = Quaternion.Euler(0f, Random.Range(-90f, 90f), 0f) * roamDirection;
                    transform.position += roamDirection * (1.4f * Time.deltaTime);
                }
            }
        }
    }
}
