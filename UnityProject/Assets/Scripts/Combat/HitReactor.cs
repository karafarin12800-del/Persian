using System.Collections;
using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    /// <summary>
    /// Lightweight hit stagger and death fall for runtime enemies.
    /// This implementation intentionally depends only on Unity and project core
    /// components so it can be used by the existing Android-safe runtime path.
    /// </summary>
    public sealed class HitReactor : MonoBehaviour
    {
        public const float SettleSeconds = 1.4f;
        private static readonly RaycastHit[] HitBuffer = new RaycastHit[8];

        private Vector3 knockVelocity;
        private float staggerUntil;
        private bool dying;

        public bool IsStaggered => Time.time < staggerUntil;
        public bool IsDying => dying;

        public static HitReactor Ensure(GameObject target)
        {
            if (target == null) return null;
            HitReactor reactor = target.GetComponent<HitReactor>();
            return reactor != null ? reactor : target.AddComponent<HitReactor>();
        }

        public static void NotifyDamaged(GameObject target, int damage)
        {
            if (target == null || target.GetComponent<PlayerController>() != null) return;
            HitReactor reactor = Ensure(target);
            if (reactor == null || reactor.dying) return;

            Vector3 direction = target.transform.position - (PlayerPosition() ?? (target.transform.position - Vector3.forward));
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f)
                direction = Random.insideUnitSphere;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f) direction = Vector3.forward;

            float force = Mathf.Clamp(0.8f + Mathf.Max(0, damage) * 0.035f, 0.8f, 2.2f);
            reactor.Stagger(direction.normalized, force);
        }

        public static float NotifyKilled(GameObject target, int damage)
        {
            if (target == null || target.GetComponent<PlayerController>() != null) return 0f;
            HitReactor reactor = Ensure(target);
            if (reactor == null) return 0f;
            Vector3 direction = target.transform.position - (PlayerPosition() ?? (target.transform.position - Vector3.forward));
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.001f) direction = Vector3.forward;
            float force = Mathf.Clamp(2f + Mathf.Max(0, damage) * 0.05f, 2f, 6f);
            return reactor.PlayDeath(direction.normalized, force);
        }

        private static Vector3? PlayerPosition()
        {
            PlayerController player = FindObjectOfType<PlayerController>();
            return player != null ? player.transform.position : (Vector3?)null;
        }

        public void Stagger(Vector3 direction, float force)
        {
            if (dying) return;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return;
            knockVelocity += direction.normalized * Mathf.Clamp(force, 0f, 3f);
            knockVelocity = Vector3.ClampMagnitude(knockVelocity, 3.5f);
            staggerUntil = Mathf.Max(staggerUntil, Time.time + Mathf.Clamp(force * 0.05f, 0.06f, 0.16f));
        }

        private void LateUpdate()
        {
            if (dying || knockVelocity.sqrMagnitude < 0.0009f) return;
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            Vector3 step = knockVelocity * dt;
            if (CanStep(step))
                transform.position += step;
            else
                knockVelocity = Vector3.zero;
            knockVelocity *= Mathf.Exp(-10f * dt);
        }

        public float PlayDeath(Vector3 direction, float force)
        {
            if (dying) return SettleSeconds;
            dying = true;

            EnemyChase chase = GetComponent<EnemyChase>();
            if (chase != null) chase.enabled = false;

            Collider[] colliders = GetComponentsInChildren<Collider>();
            foreach (Collider item in colliders)
                if (item != null) item.enabled = false;

            StartCoroutine(DeathRoutine(direction, force));
            return SettleSeconds;
        }

        private IEnumerator DeathRoutine(Vector3 direction, float force)
        {
            Transform body = transform;
            Vector3 dir = direction;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.001f) dir = Vector3.forward;
            dir.Normalize();

            Quaternion initialRotation = body.rotation;
            Vector3 axis = Vector3.Cross(Vector3.up, dir);
            if (axis.sqrMagnitude < 0.001f) axis = Vector3.right;
            axis.Normalize();

            float startY = body.position.y;
            float verticalSpeed = Mathf.Clamp(force * 0.28f, 0.7f, 1.8f);
            float travelSpeed = Mathf.Clamp(force * 0.32f, 0.7f, 2.4f);
            float elapsed = 0f;
            const float gravity = 12f;

            while (elapsed < SettleSeconds)
            {
                float dt = Time.deltaTime;
                elapsed += dt;
                Vector3 position = body.position;
                verticalSpeed -= gravity * dt;
                position.y = Mathf.Max(startY, position.y + verticalSpeed * dt);
                if (position.y <= startY + 0.001f && verticalSpeed < 0f)
                    verticalSpeed = 0f;

                Vector3 horizontalStep = dir * travelSpeed * dt;
                if (CanStep(horizontalStep))
                    position += horizontalStep;
                else
                    travelSpeed = 0f;
                travelSpeed *= Mathf.Exp(-2.5f * dt);
                body.position = position;

                float progress = Mathf.Clamp01(elapsed / 0.65f);
                body.rotation = Quaternion.AngleAxis(88f * progress, axis) * initialRotation;
                yield return null;
            }

            Destroy(gameObject);
        }

        private bool CanStep(Vector3 step)
        {
            float distance = step.magnitude;
            if (distance < 0.0001f) return true;
            Vector3 origin = transform.position + Vector3.up * 0.5f;
            int count = Physics.RaycastNonAlloc(origin, step / distance, HitBuffer,
                distance + 0.15f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider hit = HitBuffer[i].collider;
                if (hit == null || hit.transform == transform || hit.transform.IsChildOf(transform)) continue;
                if (hit.GetComponentInParent<EnemyChase>() != null ||
                    hit.GetComponentInParent<PlayerController>() != null) continue;
                return false;
            }
            return true;
        }
    }
}