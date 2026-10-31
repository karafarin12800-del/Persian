using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    /// <summary>Small wind response for grouped tree crowns; trunk and placement stay fixed.</summary>
    public sealed class FoliageSway : MonoBehaviour
    {
        [SerializeField] private float swayDegrees = 1.35f;
        [SerializeField] private float swaySpeed = 0.72f;
        private Quaternion restRotation;
        private float phase;
        private void Awake()
        {
            restRotation = transform.localRotation;
            phase = Random.value * Mathf.PI * 2f;
        }
        private void Update()
        {
            float wave = Mathf.Sin(Time.time * swaySpeed + phase) * swayDegrees;
            float secondary = Mathf.Sin(Time.time * swaySpeed * 0.63f + phase * 1.7f) * swayDegrees * 0.42f;
            transform.localRotation = restRotation * Quaternion.Euler(secondary, wave * 0.28f, wave);
        }
    }
}
