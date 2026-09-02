using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public sealed class CameraFollow25D : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 14f, -14f);
        [SerializeField] private float followSpeed = 14f;
        [SerializeField] private float pitch = 48f;
        [SerializeField] private float yaw = 0f;
        [SerializeField] private float fieldOfView = 52f;
        [SerializeField] private float fixedDistance = 19.8f;

        public float Yaw => yaw;
        public Transform Target => target;

        public void SetTarget(Transform value) => target = value;

        private void Awake()
        {
            ApplyCameraSettings();
        }

        // Kept for scene/backward compatibility. Touch input must never rotate the world camera.
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
            desired.y = target.position.y + offset.y;
            transform.position = Vector3.Lerp(transform.position, desired, 1f - Mathf.Exp(-followSpeed * Time.deltaTime));

            Vector3 lookTarget = target.position + Vector3.up * 0.85f;
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