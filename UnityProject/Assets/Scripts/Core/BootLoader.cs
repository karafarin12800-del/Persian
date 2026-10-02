using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace PersiaWar.Unity2D5D
{
    public sealed class BootLoader : MonoBehaviour
    {
        private bool loading;

        private void Awake()
        {
            StartupCheckpoint.Set("BootLoaderAwake");
            Application.targetFrameRate = 60;
            QualitySettings.vSyncCount = 0;
        }

        private void Start()
        {
            if (!loading)
                StartCoroutine(LoadGameplayScene());
        }

        private IEnumerator LoadGameplayScene()
        {
            loading = true;
            StartupCheckpoint.Set("BootSceneReady");
            yield return null;

            const int gameplayBuildIndex = 1;
            if (SceneManager.sceneCountInBuildSettings <= gameplayBuildIndex)
            {
                Debug.LogError("PERSIA_BOOT: PersiaWarPrototype is missing from Build Settings.");
                StartupCheckpoint.Set("BootGameplaySceneMissing");
                yield break;
            }

            StartupCheckpoint.Set("GameplaySceneLoadRequested");
            AsyncOperation load = SceneManager.LoadSceneAsync(gameplayBuildIndex, LoadSceneMode.Single);
            if (load == null)
            {
                Debug.LogError("PERSIA_BOOT: LoadSceneAsync returned null.");
                StartupCheckpoint.Set("GameplaySceneLoadFailed");
                yield break;
            }

            while (!load.isDone)
                yield return null;

            StartupCheckpoint.Set("GameplaySceneActivated");
        }
    }
}
