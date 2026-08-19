using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public sealed class CameraFollow25D : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 22f, -22f);
        [SerializeField] private float followSpeed = 12f;
        [SerializeField] private float pitch = 45f;
        [SerializeField] private float yaw = 0f;
        [SerializeField] private float fieldOfView = 55f;

        public void SetTarget(Transform value) => target = value;

        private void Awake()
        {
            Camera cam = GetComponent<Camera>();
            if (cam != null)
            {
                cam.orthographic = false;
                cam.fieldOfView = fieldOfView;
                cam.nearClipPlane = 0.1f;
                cam.farClipPlane = 260f;
            }
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                PlayerController player = FindFirstObjectByType<PlayerController>();
                if (player != null) target = player.transform;
                if (target == null) return;
            }

            Vector3 desired = target.position + Quaternion.Euler(0f, yaw, 0f) * offset;
            transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-followSpeed * Time.deltaTime));
            transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }
    }
}
