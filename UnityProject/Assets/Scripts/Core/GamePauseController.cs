using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public sealed class GamePauseController : MonoBehaviour
    {
        private float previousTimeScale = 1f;

        public bool IsPaused => Time.timeScale <= 0.001f;

        public void OpenMenu()
        {
            if (IsPaused) return;
            previousTimeScale = Time.timeScale;
            Time.timeScale = 0f;
        }

        public void CloseMenu()
        {
            Time.timeScale = previousTimeScale > 0.001f ? previousTimeScale : 1f;
        }

        public void ToggleMenu()
        {
            if (IsPaused) CloseMenu();
            else OpenMenu();
        }

        private void OnDisable()
        {
            if (IsPaused) Time.timeScale = 1f;
        }
    }
}
