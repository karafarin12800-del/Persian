using System.Collections.Generic;
using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    /// <summary>
    /// Non-combat Stage 1 alien awareness: independent local patrols, player
    /// perception, obstacle-aware steering and short-range group alert sharing.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StageOneAlienAwareness : MonoBehaviour
    {
        private const float MinPatrolRadius = 10f;
        private const float MaxPatrolRadius = 15f;
        private const float DetectionRadius = 19f;
        private const float DetectionHalfAngle = 78f;
        private const float GroupAlertRadius = 42f;
        private const float GroupAlertDuration = 4.5f;
        private const float PlayerRefreshInterval = 1.0f;
        private const float ThinkInterval = 0.18f;

        private static readonly List<StageOneAlienAwareness> ActiveAgents =
            new List<StageOneAlienAwareness>();
        private static PlayerController[] cachedPlayers = new PlayerController[0];
        private static float nextPlayerRefresh;
        private static Vector3 sharedAlertPosition;
        private static float sharedAlertUntil;

        private readonly Vector3[] patrolPoints = new Vector3[5];
        private Vector3 patrolCenter;
        private Vector3 currentWaypoint;
        private float patrolRadius;
        private float patrolSpeed;
        private float nextThinkTime;
        private float waitUntil;
        private float alertUntil;
        private float alertLookUntil;
        private Vector3 lastKnownPlayerPosition;
        private int waypointIndex = -1;
        private int identitySeed;
        private bool initialized;
        private bool alerted;
        private bool obstacleSideRight;

        public bool IsAlerted => alerted && Time.time < alertUntil;
        public Vector3 PatrolCenter => patrolCenter;
        public float PatrolRadius => patrolRadius;

        private void OnEnable()
        {
            if (!ActiveAgents.Contains(this))
                ActiveAgents.Add(this);
            InitializePatrol();
        }

        private void OnDisable()
        {
            ActiveAgents.Remove(this);
        }

        private void InitializePatrol()
        {
            if (initialized)
                return;

            patrolCenter = transform.position;
            identitySeed = Mathf.Abs(GetInstanceID());
            patrolRadius = Mathf.Lerp(MinPatrolRadius, MaxPatrolRadius,
                Hash01(identitySeed + 17));
            patrolSpeed = Mathf.Lerp(1.35f, 2.15f, Hash01(identitySeed + 43));
            obstacleSideRight = (identitySeed & 1) == 0;

            // Five deterministic waypoints create a varied local patrol loop
            // without touching Unity's global random-number stream.
            for (int i = 0; i < patrolPoints.Length; i++)
            {
                float angle = (i / (float)patrolPoints.Length) * Mathf.PI * 2f
                    + Hash01(identitySeed + 91) * 1.2f;
                float radius = patrolRadius * Mathf.Lerp(0.42f, 0.94f,
                    Hash01(identitySeed + i * 13 + 7));
                patrolPoints[i] = patrolCenter + new Vector3(
                    Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius);
            }

            currentWaypoint = patrolPoints[0];
            waypointIndex = 0;
            initialized = true;
        }

        private void Update()
        {
            if (!initialized)
                InitializePatrol();

            if (Time.time < nextThinkTime)
                return;

            nextThinkTime = Time.time + ThinkInterval;
            RefreshPlayerCache();
            DetectPlayers();

            if (Time.time < alertUntil)
            {
                alerted = true;
                LookToward(lastKnownPlayerPosition);
                return;
            }

            if (Time.time < sharedAlertUntil &&
                HorizontalDistance(transform.position, sharedAlertPosition) <= GroupAlertRadius)
            {
                alerted = true;
                alertUntil = Mathf.Max(alertUntil, sharedAlertUntil);
                lastKnownPlayerPosition = sharedAlertPosition;
                LookToward(lastKnownPlayerPosition);
                return;
            }

            alerted = false;
            PatrolStep();
        }

        private void RefreshPlayerCache()
        {
            if (Time.time < nextPlayerRefresh)
                return;

            nextPlayerRefresh = Time.time + PlayerRefreshInterval;
            cachedPlayers = FindObjectsByType<PlayerController>(
                FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        }

        private void DetectPlayers()
        {
            if (cachedPlayers == null)
                return;

            Vector3 eye = transform.position + Vector3.up * 1.05f;
            Vector3 forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.001f)
                forward = Vector3.forward;
            forward.Normalize();

            PlayerController nearestVisible = null;
            float nearestDistance = DetectionRadius;

            // Evaluate every active PlayerController, not only the local hero.
            for (int i = 0; i < cachedPlayers.Length; i++)
            {
                PlayerController player = cachedPlayers[i];
                if (player == null || !player.gameObject.activeInHierarchy)
                    continue;

                Vector3 targetPoint = player.transform.position + Vector3.up * 0.8f;
                Vector3 toPlayer = targetPoint - eye;
                float distance = toPlayer.magnitude;
                if (distance <= 0.1f || distance > DetectionRadius)
                    continue;

                Vector3 flatDirection = new Vector3(toPlayer.x, 0f, toPlayer.z);
                if (flatDirection.sqrMagnitude > 0.001f &&
                    Vector3.Angle(forward, flatDirection) > DetectionHalfAngle)
                    continue;

                if (!HasClearView(eye, targetPoint, player.transform))
                    continue;

                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestVisible = player;
                }
            }

            if (nearestVisible == null)
                return;

            lastKnownPlayerPosition = nearestVisible.transform.position;
            alertUntil = Time.time + GroupAlertDuration;
            alertLookUntil = alertUntil;
            alerted = true;
            BroadcastGroupAlert(lastKnownPlayerPosition);
        }

        private bool HasClearView(Vector3 eye, Vector3 targetPoint, Transform player)
        {
            Vector3 delta = targetPoint - eye;
            float distance = delta.magnitude;
            if (distance <= 0.05f)
                return true;

            if (Physics.Raycast(eye, delta / distance, out RaycastHit hit,
                distance, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
            {
                return hit.transform == player || hit.transform.IsChildOf(player);
            }
            return true;
        }

        private static void BroadcastGroupAlert(Vector3 position)
        {
            sharedAlertPosition = position;
            sharedAlertUntil = Time.time + GroupAlertDuration;

            // Nearby members share the last-known position, but stay in their
            // own patrol zones and do not converge on or pursue the player.
            for (int i = 0; i < ActiveAgents.Count; i++)
            {
                StageOneAlienAwareness agent = ActiveAgents[i];
                if (agent == null || !agent.isActiveAndEnabled)
                    continue;

                if (HorizontalDistance(agent.transform.position, position) <= GroupAlertRadius)
                {
                    agent.lastKnownPlayerPosition = position;
                    agent.alertUntil = sharedAlertUntil;
                    agent.alertLookUntil = sharedAlertUntil;
                    agent.alerted = true;
                }
            }
        }

        private void PatrolStep()
        {
            if (Time.time < waitUntil)
                return;

            Vector3 position = transform.position;
            Vector3 toWaypoint = currentWaypoint - position;
            toWaypoint.y = 0f;

            if (toWaypoint.magnitude < 1.1f)
            {
                ChooseNextWaypoint();
                waitUntil = Time.time + Mathf.Lerp(0.25f, 1.4f,
                    Hash01(identitySeed + Mathf.FloorToInt(Time.time * 0.2f)));
                return;
            }

            Vector3 direction = toWaypoint.normalized;
            Vector3 steering = GetObstacleAwareDirection(direction);
            Vector3 next = position + steering * patrolSpeed * ThinkInterval;
            next.y = patrolCenter.y;

            Vector3 fromCenter = next - patrolCenter;
            fromCenter.y = 0f;
            if (fromCenter.magnitude > patrolRadius)
                next = patrolCenter + fromCenter.normalized * patrolRadius;

            transform.position = next;
            if (steering.sqrMagnitude > 0.001f)
            {
                Quaternion desired = Quaternion.LookRotation(steering, Vector3.up);
                transform.rotation = Quaternion.Slerp(transform.rotation, desired,
                    1f - Mathf.Exp(-7f * ThinkInterval));
            }
        }

        private Vector3 GetObstacleAwareDirection(Vector3 desired)
        {
            Vector3 origin = transform.position + Vector3.up * 0.65f;
            if (!Physics.Raycast(origin, desired, out RaycastHit hit, 1.8f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return desired;

            if (hit.transform == transform || hit.transform.IsChildOf(transform))
                return desired;

            Vector3 side = obstacleSideRight
                ? Vector3.Cross(Vector3.up, desired).normalized
                : Vector3.Cross(desired, Vector3.up).normalized;

            if (!Physics.Raycast(origin, side, 1.25f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return side;

            side = -side;
            if (!Physics.Raycast(origin, side, 1.25f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return side;

            obstacleSideRight = !obstacleSideRight;
            return Vector3.zero;
        }

        private void ChooseNextWaypoint()
        {
            if (patrolPoints.Length == 0)
                return;

            int next = (waypointIndex + 1 + (identitySeed % 2)) % patrolPoints.Length;
            if (next == waypointIndex)
                next = (next + 1) % patrolPoints.Length;

            waypointIndex = next;
            currentWaypoint = patrolPoints[waypointIndex];
        }

        private void LookToward(Vector3 worldPosition)
        {
            Vector3 direction = worldPosition - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.01f)
                return;

            Quaternion desired = Quaternion.LookRotation(direction.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, desired,
                1f - Mathf.Exp(-8f * ThinkInterval));
        }

        private static float HorizontalDistance(Vector3 a, Vector3 b)
        {
            a.y = 0f;
            b.y = 0f;
            return Vector3.Distance(a, b);
        }

        private static float Hash01(int value)
        {
            unchecked
            {
                int hash = value;
                hash = (hash ^ 61) ^ (hash >> 16);
                hash += hash << 3;
                hash ^= hash >> 4;
                hash *= 0x27d4eb2d;
                hash ^= hash >> 15;
                return (hash & 0x7fffffff) / 2147483647f;
            }
        }
    }
}