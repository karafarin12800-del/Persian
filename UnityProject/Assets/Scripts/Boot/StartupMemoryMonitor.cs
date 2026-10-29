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
                {
                    StartupCheckpoint.RecordMemorySample("TransitionLoading_2s");
                    CapturePeak();
                }
                yield return new WaitForSecondsRealtime(2f);
            }
        }

        public static void SetLoading(bool loading, string transitionLabel = "")
        {
            if (loading)
            {
                long allocated = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / (1024L * 1024L);
                long reserved = UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong() / (1024L * 1024L);
                PlayerPrefs.SetInt("PersiaWar.Memory.PeakAllocatedMB", (int)allocated);
                PlayerPrefs.SetInt("PersiaWar.Memory.PeakReservedMB", (int)reserved);
                PlayerPrefs.SetString("PersiaWar.Memory.PeakTransitionLabel",
                    string.IsNullOrEmpty(transitionLabel) ? "StageTransition" : transitionLabel);
            }
            else
            {
                CapturePeak();
                Debug.Log("PERSIA_TRANSITION_MEMORY_PEAK: " + StartupCheckpoint.TransitionPeakSummary);
            }
            PlayerPrefs.SetInt(LoadingKey, loading ? 1 : 0);
            PlayerPrefs.Save();
            StartupCheckpoint.RecordMemorySample(loading ? "TransitionLoadingStarted" : "TransitionLoadingEnded");
        }

        private static void CapturePeak()
        {
            long allocated = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong() / (1024L * 1024L);
            long reserved = UnityEngine.Profiling.Profiler.GetTotalReservedMemoryLong() / (1024L * 1024L);
            int peakAllocated = PlayerPrefs.GetInt("PersiaWar.Memory.PeakAllocatedMB", 0);
            int peakReserved = PlayerPrefs.GetInt("PersiaWar.Memory.PeakReservedMB", 0);
            PlayerPrefs.SetInt("PersiaWar.Memory.PeakAllocatedMB", Mathf.Max(peakAllocated, (int)allocated));
            PlayerPrefs.SetInt("PersiaWar.Memory.PeakReservedMB", Mathf.Max(peakReserved, (int)reserved));
            PlayerPrefs.Save();
        }
    }
}
