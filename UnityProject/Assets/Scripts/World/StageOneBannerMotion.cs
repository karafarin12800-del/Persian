using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    /// <summary>Very light procedural cloth sway for Stage 1's four distant gate banners.</summary>
    public sealed class StageOneBannerMotion : MonoBehaviour
    {
        [SerializeField] private float phase;
        private Quaternion restingRotation;

        private void Awake()
        {
            restingRotation = transform.localRotation;
        }

        public void SetPhase(float value)
        {
            phase = value;
        }

        private void Update()
        {
            float wind = Mathf.Sin(Time.unscaledTime * 1.05f + phase);
            float flutter = Mathf.Sin(Time.unscaledTime * 1.75f + phase * 1.7f);
            transform.localRotation = restingRotation * Quaternion.Euler(0f, flutter * 1.6f, wind * 2.2f);
        }
    }
}
