using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    /// <summary>
    /// Pauses gameplay while the menu is open. UI remains interactive because
    /// Unity continues to process Update while Time.timeScale is zero.
    /// </summary>
    public sealed class GamePauseController : MonoBehaviour
    {
        [SerializeField] private bool startPaused = false;
        private float previousTimeScale = 1f;

        public bool IsPaused { get; private set; }

        private void Awake()
        {
            if (startPaused) Pause();
        }

        public void OpenMenu() => Pause();

        public void CloseMenu() => Resume();

        public void ToggleMenu()
        {
            if (IsPaused) Resume();
            else Pause();
        }

        public void Pause()
        {
            if (IsPaused) return;
            previousTimeScale = Time.timeScale;
            if (previousTimeScale <= 0f) previousTimeScale = 1f;
            Time.timeScale = 0f;
            IsPaused = true;
        }

        public void Resume()
        {
            Time.timeScale = previousTimeScale <= 0f ? 1f : previousTimeScale;
            IsPaused = false;
        }

        private void OnDestroy()
        {
            if (IsPaused) Time.timeScale = previousTimeScale <= 0f ? 1f : previousTimeScale;
        }
    }
}
