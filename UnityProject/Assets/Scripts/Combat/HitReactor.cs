using System.Collections;
using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    /// <summary>
    /// Natural hit reaction without physics bodies: a short stagger + knockback when an
    /// enemy is shot, and a launched, tumbling fall when it dies. Movement is simulated
    /// kinematically with ray checks, so it never pushes bodies through walls and costs
    /// almost nothing on mobile. Attach with HitReactor.Ensure(gameObject).
    /// EnemyChase may check IsStaggered to pause its own movement for a moment.
    /// </summary>
    public sealed class HitReactor : MonoBehaviour
    {
        public const float SettleSeconds = 1.4f;

        private static readonly RaycastHit[] hitBuffer = new RaycastHit[10];

        private Vector3 knockVelocity;
        private float staggerUntil;
        private bool dying;

        public bool IsStaggered
        {
            get { return Time.time < staggerUntil; }
        }

        public bool IsDying
        {
            get { return dying; }
        }

        public static HitReactor Ensure(GameObject target)
        {
            if (target == null) return null;
            HitReactor reactor = target.GetComponent<HitReactor>();
            if (reactor == null) reactor = target.AddComponent<HitReactor>();
            return reactor;
        }

        // ------------------------------------------------------------------ one-call facade

        /// <summary>
        /// Call from TargetHealth.ApplyDamage for every non-lethal hit.
        /// attackerPosition may be Vector3.zero when unknown (a random direction is used).
        /// weaponName is optional (WeaponKind.ToString()); heavier weapons push harder.
        /// </summary>
        public static void NotifyDamaged(GameObject target, Vector3 attackerPosition, float damage, string weaponName)
        {
            if (target == null) return;
            Vector3 center = CenterOf(target);
            Vector3 direction = DirectionFrom(attackerPosition, target.transform.position);
            BloodFX.Hit(center, direction, damage, BloodFX.StyleFor(target.name));
            if (target.GetComponent<PlayerController>() != null) return;   // never shove the player
            float force = ForceForWeapon(weaponName) * Mathf.Clamp(0.7f + damage * 0.01f, 0.7f, 1.8f);
            Ensure(target).Stagger(direction, force);
        }

        /// <summary>
        /// Call when the target dies. Returns how many seconds the body needs to settle;
        /// destroy the object after at least that long (plus whatever fade you already use).
        /// </summary>
        public static float NotifyKilled(GameObject target, Vector3 attackerPosition, float damage,
            string weaponName, bool explosive)
        {
            if (target == null) return 0f;
            Vector3 center = CenterOf(target);
            Vector3 direction = DirectionFrom(attackerPosition, target.transform.position);
            float force = ForceForWeapon(weaponName) * Mathf.Clamp(0.8f + damage * 0.012f, 0.8f, 2.0f);
            if (explosive) force = Mathf.Max(force, 6.5f);
            BloodStyle style = BloodFX.StyleFor(target.name);
            BloodFX.Death(center, direction, force, style);
            if (target.GetComponent<PlayerController>() != null) return 0f;
            return Ensure(target).PlayDeath(direction, force);
        }

        public static float ForceForWeapon(string weaponName)
        {
            switch (ModelLibrary.KindFromName(weaponName))
            {
                case ModelKind.Pistol: return 1.4f;
                case ModelKind.AssaultRifle: return 2.0f;
                case ModelKind.HeavyMachineGun: return 2.8f;
                case ModelKind.Axe: return 3.4f;
                case ModelKind.VoidRifle: return 2.6f;
                case ModelKind.AlienSword: return 3.8f;
                default: return 1.8f;
            }
        }

        private static Vector3 CenterOf(GameObject target)
        {
            Collider collider = target.GetComponent<Collider>();
            if (collider != null) return collider.bounds.center;
            return target.transform.position + Vector3.up * 0.9f;
        }

        private static Vector3 DirectionFrom(Vector3 attackerPosition, Vector3 targetPosition)
        {
            Vector3 direction = targetPosition - attackerPosition;
            direction.y = 0f;
            if (attackerPosition == Vector3.zero || direction.sqrMagnitude < 0.0001f)
            {
                float a = Random.Range(0f, 6.2831853f);
                return new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            }
            return direction.normalized;
        }

        // ------------------------------------------------------------------ stagger

        public void Stagger(Vector3 direction, float force)
        {
            if (dying) return;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return;
            knockVelocity += direction.normalized * force;
            if (knockVelocity.magnitude > 5f) knockVelocity = knockVelocity.normalized * 5f;
            staggerUntil = Mathf.Max(staggerUntil, Time.time + Mathf.Clamp(force * 0.045f, 0.06f, 0.18f));
        }

        private void LateUpdate()
        {
            if (dying) return;
            if (knockVelocity.sqrMagnitude < 0.0009f)
            {
                knockVelocity = Vector3.zero;
                return;
            }
            float dt = Time.deltaTime;
            if (dt <= 0f) return;
            Vector3 allowed;
            if (TryStep(knockVelocity * dt, out allowed))
                transform.position += allowed;
            else
                knockVelocity = Vector3.zero;
            knockVelocity *= Mathf.Exp(-9f * dt);
        }

        // ------------------------------------------------------------------ death launch

        /// <summary>Starts the launched fall. Returns the settle time in seconds.</summary>
        public float PlayDeath(Vector3 direction, float force)
        {
            if (dying) return SettleSeconds;
            dying = true;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f)
            {
                float a = Random.Range(0f, 6.2831853f);
                direction = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a));
            }
            StartCoroutine(DeathRoutine(direction.normalized, Mathf.Clamp(force, 1.5f, 9f)));
            return SettleSeconds;
        }

        private IEnumerator DeathRoutine(Vector3 dir, float force)
        {
            Transform t = transform;
            float groundY = t.position.y;
            Quaternion startRotation = t.rotation;
            Vector3 axis = Vector3.Cross(Vector3.up, dir);
            if (axis.sqrMagnitude < 0.0001f) axis = Vector3.right;
            axis.Normalize();

            float roll = Random.Range(8f, 28f) * (Random.value < 0.5f ? -1f : 1f);
            Vector3 velocity = dir * force;
            float vy = 2.0f + force * 0.35f;
            const float gravity = 14f;
            const float targetAngle = 88f;
            float angle = 0f;
            float elapsed = 0f;
            bool airborne = true;
            int bounces = 0;

            while (elapsed < 2.2f)
            {
                float dt = Time.deltaTime;
                if (dt <= 0f)
                {
                    yield return null;
                    continue;
                }
                elapsed += dt;

                Vector3 position = t.position;
                if (airborne)
                {
                    vy -= gravity * dt;
                    position.y += vy * dt;
                    if (position.y <= groundY && vy < 0f)
                    {
                        position.y = groundY;
                        if (bounces == 0 && Mathf.Abs(vy) > 2.2f)
                        {
                            vy = -vy * 0.28f;
                            bounces++;
                        }
                        else
                        {
                            airborne = false;
                            vy = 0f;
                        }
                    }
                }

                Vector3 allowed;
                if (TryStep(new Vector3(velocity.x, 0f, velocity.z) * dt, out allowed))
                    position += allowed;
                else
                    velocity *= 0.2f;
                velocity *= Mathf.Exp(-(airborne ? 0.4f : 7f) * dt);
                t.position = position;

                angle = Mathf.MoveTowards(angle, targetAngle, (airborne ? 520f : 300f) * dt);
                float blend = angle / targetAngle;
                t.rotation = Quaternion.AngleAxis(angle, axis) *
                             Quaternion.AngleAxis(roll * blend, dir) * startRotation;

                if (!airborne && velocity.sqrMagnitude < 0.01f && angle >= targetAngle - 0.01f) break;
                yield return null;
            }

            t.rotation = Quaternion.AngleAxis(targetAngle, axis) * Quaternion.AngleAxis(roll, dir) * startRotation;
        }

        // ------------------------------------------------------------------ collision check

        private bool TryStep(Vector3 step, out Vector3 allowed)
        {
            allowed = step;
            float distance = step.magnitude;
            if (distance < 0.0001f) return true;

            Vector3 origin = transform.position + Vector3.up * 0.5f;
            int count = Physics.RaycastNonAlloc(origin, step / distance, hitBuffer, distance + 0.35f,
                ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider collider = hitBuffer[i].collider;
                if (collider == null) continue;
                if (collider.transform.IsChildOf(transform)) continue;
                if (hitBuffer[i].normal.y > 0.6f) continue;
                if (collider.GetComponentInParent<TargetHealth>() != null) continue;
                if (collider.GetComponentInParent<PlayerController>() != null) continue;
                allowed = Vector3.zero;
                return false;
            }
            return true;
        }
    }
}
