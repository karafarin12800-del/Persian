using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public sealed class CameraFollow25D : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 12f, -12f);
        [SerializeField] private float followSpeed = 8f;

        public void SetTarget(Transform value) => target = value;

        private void LateUpdate()
        {
            if (target == null) return;
            Vector3 desired = target.position + offset;
            transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-followSpeed * Time.deltaTime));
            transform.rotation = Quaternion.Euler(45f, 0f, 0f);
        }
    }
}
