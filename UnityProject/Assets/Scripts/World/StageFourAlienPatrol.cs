using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    /// <summary>
    /// Stage 4-only corridor patrol. Aliens investigate the mothership interior,
    /// detect the player through clear sight lines, engage with EnemyChase, and
    /// return to their patrol route after losing contact.
    /// </summary>
    public sealed class StageFourAlienPatrol : MonoBehaviour
    {
        [SerializeField] private float patrolSpeed = 2.15f;
        [SerializeField] private float turnSpeed = 260f;
        [SerializeField] private float detectionRange = 25f;
        [SerializeField] private float disengageRange = 41f;
        [SerializeField] private float lostContactGrace = 6.5f;
        [SerializeField] private float waypointReachDistance = 1.3f;
        [SerializeField] private float waypointPause = 0.65f;

        private PlayerController player;
        private EnemyChase chase;
        private StageFourWorldBuilder builder;
        private StylizedCharacterVisual visual;
        private Vector3[] route = new Vector3[0];
        private int routeIndex;
        private float pauseUntil;
        private float lastSeenTime;
        private bool engaged;
        private bool configured;

        public void Configure(PlayerController targetPlayer, EnemyChase enemyChase, Vector3[] patrolRoute)
        {
            player = targetPlayer;
            chase = enemyChase;
            builder = GetComponentInParent<StageFourWorldBuilder>();
            visual = GetComponentInChildren<StylizedCharacterVisual>(true);
            route = patrolRoute != null ? patrolRoute : new Vector3[0];
            routeIndex = 0;
            lastSeenTime = -10000f;
            engaged = false;
            configured = player != null && chase != null && route.Length > 0;

            // Stage 4 aliens begin as sentries, not as nine enemies sprinting at the
            // player through walls before the player can explore the ship.
            if (chase != null)
            {
                chase.SetTarget(null);
                chase.enabled = false;
            }
        }

        private void Update()
        {
            if (!configured || player == null || chase == null || builder == null ||
                !builder.IsBuilt || player.IsDefeated)
                return;

            // No patrol movement during the arrival cutscene; the existing stage
            // controller enables combat when that sequence completes.
            if (builder.IsCinematic)
                return;

            float distance = Vector3.Distance(transform.position, player.transform.position);
            bool seesPlayer = distance <= detectionRange && HasClearSightToPlayer();

            if (!engaged && seesPlayer)
                EngagePlayer();

            if (engaged)
            {
                if (seesPlayer)
                    lastSeenTime = Time.time;

                if (!seesPlayer || distance > disengageRange || Time.time - lastSeenTime > lostContactGrace)
                {
                    // Do not let the chase controller keep pushing an alien through a
                    // wall after line of sight is broken. Resume the corridor route.
                    DisengageToPatrol();
                }
                else
                {
                    // Chase is permitted only while the player remains visible through
                    // a clear corridor; blocked sight immediately returns to patrol.
                    if (!chase.enabled) chase.enabled = true;
                    return;
                }
            }

            if (chase.enabled)
            {
                chase.SetTarget(null);
                chase.enabled = false;
            }

            PatrolCorridor();
        }

        private void EngagePlayer()
        {
            engaged = true;
            lastSeenTime = Time.time;
            chase.SetTarget(player.transform);
            chase.enabled = true;
            StartupCheckpoint.Set("StageFourAlienDetectedPlayerAndEngaged");
        }

        private void DisengageToPatrol()
        {
            engaged = false;
            lastSeenTime = -10000f;
            chase.SetTarget(null);
            chase.enabled = false;
            pauseUntil = Time.time + 0.35f;
            StartupCheckpoint.Set("StageFourAlienLostContactReturnedToPatrol");
        }

        private bool HasClearSightToPlayer()
        {
            if (player == null)
                return false;

            Vector3 origin = transform.position + Vector3.up * 1.18f;
            Vector3 destination = player.transform.position + Vector3.up * 1.0f;
            Vector3 delta = destination - origin;
            float distance = delta.magnitude;
            if (distance <= 0.01f)
                return true;

            Vector3 direction = delta / distance;
            // Broad peripheral awareness is intentional for narrow connected halls.
            Vector3 flatDirection = direction;
            flatDirection.y = 0f;
            Vector3 flatForward = transform.forward;
            flatForward.y = 0f;
            if (flatDirection.sqrMagnitude > 0.01f && flatForward.sqrMagnitude > 0.01f &&
                Vector3.Dot(flatForward.normalized, flatDirection.normalized) < -0.45f)
                return false;

            RaycastHit hit;
            if (!Physics.Raycast(origin, direction, out hit, distance + 0.25f,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return true;

            return hit.collider != null &&
                hit.collider.GetComponentInParent<PlayerController>() == player;
        }

        private void LateUpdate()
        {
            if (!configured || builder == null || !builder.IsBuilt)
                return;

            // Keep both patrol and chase movement inside the ship's walkable interior.
            // Room-specific horizontal limits match the built floor plan.
            Vector3 p = transform.position;
            p.z = Mathf.Clamp(p.z, -100f, 127f);
            if (p.z < -77f)          p.x = Mathf.Clamp(p.x, -25f, 25f); // engine room
            else if (p.z < -47f)     p.x = Mathf.Clamp(p.x, -10f, 10f); // aft corridor
            else if (p.z < 47f)      p.x = Mathf.Clamp(p.x, -44f, 44f); // hangar
            else if (p.z < 78f)      p.x = Mathf.Clamp(p.x, -10f, 10f); // forward corridor
            else if (p.z < 103f)     p.x = Mathf.Clamp(p.x, -24f, 24f); // command room
            else                     p.x = Mathf.Clamp(p.x, -10f, 10f); // exit corridor
            transform.position = p;
        }

        private void PatrolCorridor()
        {
            if (route == null || route.Length == 0)
            {
                SetVisualMoving(false, transform.forward);
                return;
            }

            if (Time.time < pauseUntil)
            {
                SetVisualMoving(false, transform.forward);
                return;
            }

            Vector3 destination = route[routeIndex];
            destination.y = transform.position.y;
            Vector3 delta = destination - transform.position;
            delta.y = 0f;

            if (delta.magnitude <= waypointReachDistance)
            {
                routeIndex = (routeIndex + 1) % route.Length;
                pauseUntil = Time.time + waypointPause;
                SetVisualMoving(false, delta.sqrMagnitude > 0.01f ? delta.normalized : transform.forward);
                return;
            }

            Vector3 direction = delta.normalized;
            Quaternion desiredRotation = Quaternion.LookRotation(direction, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation, desiredRotation, turnSpeed * Time.deltaTime);

            // Use three horizontal probes at body height before moving. The old direct
            // transform translation could cross a thin wall in a single patrol update.
            float step = Mathf.Min(patrolSpeed * Time.deltaTime, delta.magnitude);
            if (IsBlockedByWall(direction, step + 0.18f))
            {
                SetVisualMoving(false, direction);
                pauseUntil = Time.time + 0.25f;
                return;
            }

            transform.position = Vector3.MoveTowards(
                transform.position, destination, step);
            SetVisualMoving(true, direction);
        }

        private bool IsBlockedByWall(Vector3 direction, float distance)
        {
            Vector3[] heights =
            {
                transform.position + Vector3.up * 0.42f,
                transform.position + Vector3.up * 0.95f,
                transform.position + Vector3.up * 1.42f
            };

            for (int i = 0; i < heights.Length; i++)
            {
                RaycastHit[] hits = Physics.RaycastAll(
                    heights[i], direction, distance,
                    Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
                for (int j = 0; j < hits.Length; j++)
                {
                    Collider hitCollider = hits[j].collider;
                    if (hitCollider == null ||
                        hitCollider.transform == transform ||
                        hitCollider.transform.IsChildOf(transform) ||
                        hitCollider.GetComponentInParent<PlayerController>() != null)
                        continue;

                    if (hitCollider.GetComponentInParent<EnemyChase>() != null)
                        continue;

                    return true;
                }
            }
            return false;
        }

        private void SetVisualMoving(bool moving, Vector3 facing)
        {
            if (visual == null)
                visual = GetComponentInChildren<StylizedCharacterVisual>(true);
            if (visual == null)
                return;

            if (facing.sqrMagnitude > 0.001f)
                visual.SetFacing(facing.normalized);
            visual.SetMoving(moving);
        }
    }
}
