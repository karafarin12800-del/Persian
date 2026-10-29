using System.Collections;
using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    /// <summary>
    /// Samples memory during an inter-stage loading window. Data is persisted in
    /// PlayerPrefs so the next launch can display the last successful sample.
    /// </summary>
    public sealed class StartupMemoryMonitor : MonoBehaviour
    {
        private const string LoadingKey = "PersiaWar.TransitionLoading";
        private static StartupMemoryMonitor instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (instance != null) return;
            GameObject root = new GameObject("PersiaWarStartupMemoryMonitor");
            DontDestroyOnLoad(root);
            instance = root.AddComponent<StartupMemoryMonitor>();
        }

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }
            instance = this;
        }

        private void Start()
        {
            StartCoroutine(SampleWhileLoading());
        }

        private IEnumerator SampleWhileLoading()
        {
            while (true)
            {
                if (PlayerPrefs.GetInt(LoadingKey, 0) != 0)
                    StartupCheckpoint.RecordMemorySample("TransitionLoading_2s");
                yield return new WaitForSecondsRealtime(2f);
            }
        }

        public static void SetLoading(bool loading)
        {
            PlayerPrefs.SetInt(LoadingKey, loading ? 1 : 0);
            PlayerPrefs.Save();
            StartupCheckpoint.RecordMemorySample(loading ? "TransitionLoadingStarted" : "TransitionLoadingEnded");
        }
    }
}
