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
            "MainCameraRootActivationStarted",
            "MainCameraRootActivated",
            "MainCameraTargetReady",
            "MainCameraEnableStarted",
            "MainCameraEnabled",
            "CameraFollowEnabled",
            "MatchCoreReady",
            "MatchStarted",
            "CameraFollowIsolated",
            "PostMatchServicesIsolated",
            "WorldBuildFailed",
            "MatchActivationFailed"
        };

        private static float lastSaveTime = float.NegativeInfinity;

        public static string Last => PlayerPrefs.GetString(Key, "none");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        private static void MarkBeforeSplashScreen()
        {
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
