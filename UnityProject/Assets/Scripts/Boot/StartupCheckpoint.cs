using System.Collections.Generic;
using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    /// <summary>
    /// Lightweight startup breadcrumb for Android crash localization.
    /// Transient checkpoints stay in memory; important lifecycle boundaries
    /// force synchronous PlayerPrefs persistence.
    /// </summary>
    public static class StartupCheckpoint
    {
        private const string Key = "PersiaWar.StartupCheckpoint";
        private const string TimeKey = "PersiaWar.StartupCheckpointTime";
        private const string PreviousKey = "PersiaWar.PreviousStartupCheckpoint";
        private const string PreviousTimeKey = "PersiaWar.PreviousStartupCheckpointTime";

        private static readonly HashSet<string> CriticalCheckpoints = new HashSet<string>
        {
            "BeforeSplashScreen",
            "GameplayLoadStarted",
            "GameplaySceneActivated",
            "GameBootstrapAwake",
            "WorldBuildStarted",
            "WorldBaseBuilt",
            "WorldBuildReady",
            "PlayerComponentsReady",
            "PlayerPreparedForMatch",
            "MatchActivationStarted",
            "MatchCoreReady",
            "MatchStarted",
            "WorldBuildFailed",
            "MatchActivationFailed"
        };

        private static float lastSaveTime = float.NegativeInfinity;

        public static string Last => PlayerPrefs.GetString(Key, "none");
        public static string Previous => PlayerPrefs.GetString(PreviousKey, "none");
        public static string PreviousTime => PlayerPrefs.GetString(PreviousTimeKey, "unknown");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        private static void MarkBeforeSplashScreen()
        {
            string previous = PlayerPrefs.GetString(Key, "none");
            string previousTime = PlayerPrefs.GetString(TimeKey, "unknown");

            PlayerPrefs.SetString(PreviousKey, previous);
            PlayerPrefs.SetString(PreviousTimeKey, previousTime);
            PlayerPrefs.Save();

            Set("BeforeSplashScreen");
        }

        public static void Set(string stage)
        {
            if (string.IsNullOrEmpty(stage))
                stage = "Unknown";

            PlayerPrefs.SetString(Key, stage);
            PlayerPrefs.SetString(TimeKey, System.DateTime.UtcNow.ToString("O"));

            if (CriticalCheckpoints.Contains(stage)
                || Time.realtimeSinceStartup - lastSaveTime >= 0.75f)
            {
                SaveNow();
            }

            Debug.Log("PERSIA_BOOT_CHECKPOINT: " + stage);
        }

        public static void SaveNow()
        {
            PlayerPrefs.Save();
            lastSaveTime = Time.realtimeSinceStartup;
        }
    }
}
