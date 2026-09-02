using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    /// <summary>
    /// Mobile-friendly 2.5D follow camera. Presentation only: it does not alter
    /// movement or combat coordinates.
    /// </summary>
    public sealed class CameraFollow25D : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private float followSpeed = 12f;
        [SerializeField] private float pitch = 55f;
        [SerializeField] private float yaw = 32f;
        [SerializeField] private float fieldOfView = 50f;
        [SerializeField] private float fixedDistance = 17.2f;
        [SerializeField] private float lookHeight = 0.7f;

        public float Yaw => yaw;
        public Transform Target => target;

        public void SetTarget(Transform value) => target = value;

        private void Awake()
        {
            ApplyCameraSettings();
        }

        // Kept for scene/backward compatibility. The game camera remains fixed
        // so mobile movement and aiming retain their existing behavior.
        public void Rotate(float screenDeltaX) { }
        public void SetPitch(float value) { }
        public void Zoom(float pinchDelta) { }

        private void LateUpdate()
        {
            if (target == null)
            {
                PlayerController player = FindFirstObjectByType<PlayerController>();
                if (player != null) target = player.transform;
                if (target == null) return;
            }

            Quaternion orbit = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 desired = target.position + orbit * Vector3.back * fixedDistance;

            float blend = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, desired, blend);

            Vector3 lookTarget = target.position + Vector3.up * lookHeight;
            transform.rotation = Quaternion.LookRotation(lookTarget - transform.position, Vector3.up);
        }

        private void ApplyCameraSettings()
        {
            Camera cam = GetComponent<Camera>();
            if (cam == null) return;

            cam.orthographic = false;
            cam.fieldOfView = fieldOfView;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 240f;
        }
    }
}
