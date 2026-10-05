using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    /// <summary>
    /// Persistent startup breadcrumb and Android crash/exit diagnostic.
    /// The previous run's checkpoint and Android historical process-exit reason
    /// are recorded on the next launch. Managed errors are appended to the same
    /// report file. Native crashes cannot execute cleanup code, so the previous
    /// checkpoint is intentionally persisted at critical lifecycle boundaries.
    /// </summary>
    public static class StartupCheckpoint
    {
        private const string Key = "PersiaWar.StartupCheckpoint";
        private const string TimeKey = "PersiaWar.StartupCheckpointTime";
        private const string PreviousKey = "PersiaWar.PreviousStartupCheckpoint";
        private const string PreviousTimeKey = "PersiaWar.PreviousStartupCheckpointTime";
        private const string PreviousExitStateKey = "PersiaWar.PreviousExitState";
        private const string ReportFileName = "persiawar_crash_diagnostic.txt";

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
        private static bool diagnosticInitialized;
        private static string lastException;

        public static string Last => PlayerPrefs.GetString(Key, "none");
        public static string Previous => PlayerPrefs.GetString(PreviousKey, "none");
        public static string PreviousTime => PlayerPrefs.GetString(PreviousTimeKey, "unknown");
        public static string PreviousExitState => PlayerPrefs.GetString(PreviousExitStateKey, "unknown");
        public static string PreviousAssessment => PlayerPrefs.GetString("PersiaWar.PreviousAssessment", "unknown");

        public static string DiagnosticSummary =>
            "Previous: " + Previous + " | Exit: " + PreviousExitState + " | Assessment: " + PreviousAssessment;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        private static void MarkBeforeSplashScreen()
        {
            string previous = PlayerPrefs.GetString(Key, "none");
            string previousTime = PlayerPrefs.GetString(TimeKey, "unknown");

            PlayerPrefs.SetString(PreviousKey, previous);
            PlayerPrefs.SetString(PreviousTimeKey, previousTime);
            PlayerPrefs.Save();

            InitializeCrashDiagnostic();
            Set("BeforeSplashScreen");
        }

        private static void InitializeCrashDiagnostic()
        {
            if (diagnosticInitialized)
                return;

            diagnosticInitialized = true;

            string previousCheckpoint = PlayerPrefs.GetString(PreviousKey, "none");
            string previousCheckpointTime = PlayerPrefs.GetString(PreviousTimeKey, "unknown");
            string previousExitState = PlayerPrefs.GetString(PreviousExitStateKey, "unknown");
            string currentExitState = ReadAndroidExitState();

            string report =
                "PERSIA WAR CRASH DIAGNOSTIC\n" +
                "UTC: " + DateTime.UtcNow.ToString("O") + "\n" +
                "Platform: " + Application.platform + "\n" +
                "Unity: " + Application.unityVersion + "\n" +
                "Device: " + SystemInfo.deviceModel + "\n" +
                "OS: " + SystemInfo.operatingSystem + "\n" +
                "MemoryMB: " + SystemInfo.systemMemorySize + "\n" +
                "Graphics: " + SystemInfo.graphicsDeviceName + "\n" +
                "PersistentDataPath: " + Application.persistentDataPath + "\n" +
                "PreviousCheckpoint: " + previousCheckpoint + "\n" +
                "PreviousCheckpointTime: " + previousCheckpointTime + "\n" +
                "PreviousRecordedExitState: " + previousExitState + "\n" +
                "AndroidExitStateNow: " + currentExitState + "\n" +
                "Assessment: " + Classify(previousCheckpoint, currentExitState) + "\n";

            try
            {
                File.WriteAllText(Path.Combine(Application.persistentDataPath, ReportFileName), report);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("PERSIA_CRASH_DIAGNOSTIC: report write failed: " + ex.Message);
            }

            Debug.Log(report);

            Application.logMessageReceivedThreaded += OnLogMessage;
            Application.quitting += OnQuitting;

            PlayerPrefs.SetString(PreviousExitStateKey, currentExitState);
            PlayerPrefs.SetString("PersiaWar.PreviousAssessment", Classify(previousCheckpoint, currentExitState));
            PlayerPrefs.Save();
        }

        private static string Classify(string checkpoint, string exitState)
        {
            string lower = (exitState ?? string.Empty).ToLowerInvariant();

            if (lower.Contains("low_memory") || lower.Contains("lowmemory"))
                return "LIKELY_LOW_MEMORY";
            if (lower.Contains("anr"))
                return "LIKELY_ANR";
            if (lower.Contains("crash_native"))
                return "LIKELY_NATIVE_CRASH";
            if (lower.Contains("crash"))
                return "LIKELY_CRASH";
            if (lower.Contains("excessive_resource"))
                return "LIKELY_EXCESSIVE_RESOURCE_USAGE";

            if (checkpoint == "AndroidGameplayStackReady" || checkpoint == "Android3DPresentationReady")
                return "LAST_RUN_REACHED_FULL_GAMEPLAY_STACK";
            if (checkpoint == "Android3DWorldVisible" || checkpoint == "Android3DWorldValidationComplete")
                return "LAST_RUN_REACHED_WORLD_AND_CAMERA";
            if (checkpoint == "WorldBuildReady" || checkpoint == "PlayerPreparedForMatch")
                return "LAST_RUN_REACHED_WORLD_OR_PLAYER_STAGE";

            return "USE_LAST_CHECKPOINT_TO_LOCALIZE_FAILURE";
        }

        private static string ReadAndroidExitState()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // Unity 2022.3 has no public AndroidApplication.exitState API.
            // Android 11+ exposes the same evidence through ActivityManager's
            // historical process-exit records. Older devices return unavailable.
            try
            {
                AndroidJavaObject activity = new AndroidJavaClass("com.unity3d.player.UnityPlayer")
                    .GetStatic<AndroidJavaObject>("currentActivity");

                if (activity == null)
                    return "unavailable";

                string packageName = activity.Call<string>("getPackageName");
                AndroidJavaObject activityManager =
                    activity.Call<AndroidJavaObject>("getSystemService", "activity");

                if (activityManager == null)
                    return "unavailable";

                AndroidJavaObject reasons = activityManager.Call<AndroidJavaObject>(
                    "getHistoricalProcessExitReasons",
                    packageName,
                    0,
                    5);

                if (reasons == null)
                    return "unavailable";

                int count = reasons.Call<int>("size");
                if (count <= 0)
                    return "none";

                AndroidJavaObject latest = reasons.Call<AndroidJavaObject>("get", 0);
                if (latest == null)
                    return "unknown";

                int reason = latest.Call<int>("getReason");
                string description = latest.Call<string>("getDescription");
                long timestamp = latest.Call<long>("getTimestamp");

                return "reason=" + reason +
                       ";description=" + (description ?? "none") +
                       ";timestampMs=" + timestamp +
                       ";label=" + AndroidExitReasonLabel(reason);
            }
            catch (Exception ex)
            {
                return "unavailable:" + ex.GetType().Name;
            }
#else
            return "editor_or_non_android";
#endif
        }

        private static string AndroidExitReasonLabel(int reason)
        {
            switch (reason)
            {
                case 3: return "LOW_MEMORY";
                case 4: return "CRASH";
                case 5: return "CRASH_NATIVE";
                case 6: return "ANR";
                case 9: return "EXCESSIVE_RESOURCE_USAGE";
                case 10: return "USER_REQUESTED";
                default: return "reason_" + reason;
            }
        }

        private static void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Error && type != LogType.Assert)
                return;

            lastException = string.IsNullOrEmpty(condition) ? "unknown" : condition;

            try
            {
                File.AppendAllText(
                    Path.Combine(Application.persistentDataPath, ReportFileName),
                    "\n--- RUNTIME ERROR ---\n" +
                    "UTC: " + DateTime.UtcNow.ToString("O") + "\n" +
                    "Type: " + type + "\n" +
                    "Message: " + lastException + "\n" +
                    "Stack: " + stackTrace + "\n");
            }
            catch
            {
                // Diagnostic logging must never become a secondary failure.
            }
        }

        private static void OnQuitting()
        {
            try
            {
                File.AppendAllText(
                    Path.Combine(Application.persistentDataPath, ReportFileName),
                    "\n--- NORMAL QUIT ---\n" +
                    "UTC: " + DateTime.UtcNow.ToString("O") + "\n" +
                    "LastException: " + (lastException ?? "none") + "\n");
            }
            catch
            {
            }
        }

        public static string ReportPath
        {
            get { return Path.Combine(Application.persistentDataPath, ReportFileName); }
        }

        public static void Set(string stage)
        {
            if (string.IsNullOrEmpty(stage))
                stage = "Unknown";

            PlayerPrefs.SetString(Key, stage);
            PlayerPrefs.SetString(TimeKey, DateTime.UtcNow.ToString("O"));

            // Building/city checkpoints must survive a native crash exactly at the
            // failing object. The extra Save is limited to startup construction only.
            bool cityConstructionCheckpoint = stage.StartsWith("CITY_", StringComparison.Ordinal);

            if (CriticalCheckpoints.Contains(stage)
                || cityConstructionCheckpoint
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
