using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public sealed class CameraFollow25D : MonoBehaviour
    {
        [SerializeField] private Transform target;
        [SerializeField] private float followSpeed = 12f;
        [SerializeField] private float pitch = 52f;
        [SerializeField] private float yaw = 32f;
        [SerializeField] private float fieldOfView = 50f;
        [SerializeField] private float fixedDistance = 18.5f;
        [SerializeField] private float lookHeight = 0.85f;

        public float Yaw => yaw;
        public Transform Target => target;

        public void SetTarget(Transform value) => target = value;

        private void Awake()
        {
            // CameraFollow is attached during gameplay startup. Keep Awake side-effect free
            // on Android; GameBootstrap owns all Camera property configuration.
            StartupCheckpoint.Set("CameraFollowAwake");
        }

        public void Rotate(float screenDeltaX) { }
        public void SetPitch(float value) { }
        public void Zoom(float pinchDelta) { }

        private void LateUpdate()
        {
            if (target == null)
            {
                PlayerController player = FindFirstObjectByType<PlayerController>();
                if (player != null)
                    target = player.transform;

                if (target == null)
                    return;
            }

            Quaternion orbit = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 desired = target.position + orbit * Vector3.back * fixedDistance;

            float blend = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, desired, blend);

            Vector3 lookTarget = target.position + Vector3.up * lookHeight;
            Vector3 direction = lookTarget - transform.position;
            if (direction.sqrMagnitude > 0.0001f)
                transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
        }
    }
}