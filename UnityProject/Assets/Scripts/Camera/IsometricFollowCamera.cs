using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    /// <summary>Simple perspective/isometric camera for the prototype world.</summary>
    public sealed class IsometricFollowCamera : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 18f, -18f);
        [SerializeField] private float followSharpness = 10f;
        [SerializeField] private float lookHeight = 0.8f;
        [SerializeField] private float minY = 8f;
        [SerializeField] private float maxY = 28f;

        private void LateUpdate()
        {
            if (target == null)
            {
                GameObject player = GameObject.FindWithTag("Player");
                if (player != null) target = player.transform;
            }

            if (target == null) return;

            Vector3 desired = target.position + offset;
            desired.y = Mathf.Clamp(desired.y, minY, maxY);
            float t = 1f - Mathf.Exp(-followSharpness * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, desired, t);

            Vector3 lookTarget = target.position + Vector3.up * lookHeight;
            transform.rotation = Quaternion.LookRotation(lookTarget - transform.position, Vector3.up);
        }
    }
}
