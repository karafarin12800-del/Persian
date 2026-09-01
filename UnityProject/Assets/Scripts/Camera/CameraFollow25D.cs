using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    /// <summary>
    /// Single gameplay camera for the 2.5D world. The world is not split into
    /// separate 2D/3D modes: this camera always renders the same 3D scene.
    /// On mobile the right side of the screen rotates the camera; pinch zooms.
    /// </summary>
    public sealed class CameraFollow25D : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private Vector3 offset = new Vector3(0f, 11f, -11f);
        [SerializeField] private float followSpeed = 14f;
        [SerializeField] private float pitch = 48f;
        [SerializeField] private float yaw = 0f;
        [SerializeField] private float fieldOfView = 52f;
        [SerializeField] private float minPitch = 35f;
        [SerializeField] private float maxPitch = 65f;
        [SerializeField] private float minDistance = 8f;
        [SerializeField] private float maxDistance = 20f;
        [SerializeField] private float rotateSensitivity = 0.18f;
        [SerializeField] private float zoomSensitivity = 0.02f;

        private float distance;

        public float Yaw => yaw;
        public Transform Target => target;

        public void SetTarget(Transform value) => target = value;

        private void Awake()
        {
            distance = Mathf.Clamp(new Vector2(offset.x, offset.z).magnitude, minDistance, maxDistance);
            if (distance < 0.01f) distance = 15f;
            ApplyCameraSettings();
        }

        public void Rotate(float screenDeltaX)
        {
            yaw += screenDeltaX * rotateSensitivity;
        }

        public void SetPitch(float value)
        {
            pitch = Mathf.Clamp(value, minPitch, maxPitch);
        }

        public void Zoom(float pinchDelta)
        {
            distance = Mathf.Clamp(distance - pinchDelta * zoomSensitivity, minDistance, maxDistance);
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                PlayerController player = FindFirstObjectByType<PlayerController>();
                if (player != null) target = player.transform;
                if (target == null) return;
            }

            Quaternion orbit = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 desired = target.position + orbit * Vector3.back * distance;
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
            cam.farClipPlane = 220f;
        }
    }
}
