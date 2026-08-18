using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public sealed class CameraFollow25D : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 14f, -14f);
        [SerializeField] private float followSpeed = 10f;
        [SerializeField] private float pitch = 45f;
        [SerializeField] private float yaw = 0f;

        public void SetTarget(Transform value) => target = value;

        private void LateUpdate()
        {
            if (target == null) return;
            Vector3 desired = target.position + offset;
            transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-followSpeed * Time.deltaTime));
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }
    }
}
