using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private Camera gameplayCamera;

        private void Awake()
        {
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;

            if (gameplayCamera == null)
                gameplayCamera = Camera.main;

            if (gameplayCamera != null)
            {
                gameplayCamera.orthographic = false;
                gameplayCamera.transform.position = new Vector3(0f, 12f, -12f);
                gameplayCamera.transform.rotation = Quaternion.Euler(45f, 0f, 0f);
            }
        }
    }
}
