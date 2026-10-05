using System;
using System.IO;
using System.Reflection;
using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    /// <summary>
    /// Persistent Android crash/exit breadcrumb collector.
    /// It does not catch native crashes; instead it records the last checkpoint,
    /// managed exceptions, and Unity/Android exit-state information available on
    /// the next launch. The report is written to persistentDataPath.
    /// </summary>
    public static class CrashDiagnostic
    {
        private const string ReportFileName = "persiawar_crash_diagnostic.txt";
        private const string PreviousCheckpointKey = "PersiaWar.PreviousStartupCheckpoint";
        private const string PreviousCheckpointTimeKey = "PersiaWar.PreviousStartupCheckpointTime";
        private const string PreviousExitStateKey = "PersiaWar.PreviousExitState";

        private static bool initialized;
        private static string lastException;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (initialized)
                return;

            initialized = true;

            string previousCheckpoint = PlayerPrefs.GetString(PreviousCheckpointKey, "none");
            string previousCheckpointTime = PlayerPrefs.GetString(PreviousCheckpointTimeKey, "unknown");
            string previousExitState = PlayerPrefs.GetString(PreviousExitStateKey, "unknown");

            string currentExitState = ReadAndroidExitState();
            string report = BuildLaunchReport(previousCheckpoint, previousCheckpointTime, previousExitState, currentExitState);

            try
            {
                File.WriteAllText(Path.Combine(Application.persistentDataPath, ReportFileName), report);
            }
            catch (Exception ex)
            {
                Debug.LogWarning("PERSIA_CRASH_DIAGNOSTIC: Could not write report: " + ex.Message);
            }

            Debug.Log(report);

            Application.logMessageReceivedThreaded += OnLogMessage;
            Application.quitting += OnQuitting;

            PlayerPrefs.SetString(PreviousExitStateKey, currentExitState);
            PlayerPrefs.Save();
        }

        private static string BuildLaunchReport(
            string previousCheckpoint,
            string previousCheckpointTime,
            string previousExitState,
            string currentExitState)
        {
            string report =
                "PERSIA WAR CRASH DIAGNOSTIC\n" +
                "UTC: " + DateTime.UtcNow.ToString("O") + "\n" +
                "Platform: " + Application.platform + "\n" +
                "Unity: " + Application.unityVersion + "\n" +
                "Device: " + SystemInfo.deviceModel + "\n" +
                "OS: " + SystemInfo.operatingSystem + "\n" +
                "MemoryMB: " + SystemInfo.systemMemorySize + "\n" +
                "Graphics: " + SystemInfo.graphicsDeviceName + "\n" +
                "PreviousCheckpoint: " + previousCheckpoint + "\n" +
                "PreviousCheckpointTime: " + previousCheckpointTime + "\n" +
                "PreviousRecordedExitState: " + previousExitState + "\n" +
                "AndroidExitStateNow: " + currentExitState + "\n" +
                "PreviousRunAssessment: " + Classify(previousCheckpoint, currentExitState) + "\n";

            return report;
        }

        private static string Classify(string checkpoint, string exitState)
        {
            if (!string.IsNullOrEmpty(exitState) && !string.Equals(exitState, "unknown", StringComparison.OrdinalIgnoreCase))
            {
                string lower = exitState.ToLowerInvariant();
                if (lower.Contains("lowmemory") || lower.Contains("memory"))
                    return "LIKELY_LOW_MEMORY";
                if (lower.Contains("anr"))
                    return "LIKELY_ANR";
                if (lower.Contains("crash"))
                    return "LIKELY_CRASH";
            }

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
            try
            {
                Type androidApplicationType = Type.GetType(
                    "UnityEngine.Android.AndroidApplication, UnityEngine.AndroidModule");
                if (androidApplicationType == null)
                    return "unavailable";

                PropertyInfo exitStateProperty = androidApplicationType.GetProperty(
                    "exitState",
                    BindingFlags.Public | BindingFlags.Static);

                if (exitStateProperty == null)
                    return "unavailable";

                object exitState = exitStateProperty.GetValue(null, null);
                return exitState != null ? exitState.ToString() : "none";
            }
            catch (Exception ex)
            {
                return "read_error:" + ex.GetType().Name;
            }
#else
            return "editor_or_non_android";
#endif
        }

        private static void OnLogMessage(string condition, string stackTrace, LogType type)
        {
            if (type != LogType.Exception && type != LogType.Error && type != LogType.Assert)
                return;

            if (string.IsNullOrEmpty(condition))
                condition = "unknown";

            lastException = condition;

            try
            {
                string path = Path.Combine(Application.persistentDataPath, ReportFileName);
                File.AppendAllText(
                    path,
                    "\n--- RUNTIME ERROR ---\n" +
                    "UTC: " + DateTime.UtcNow.ToString("O") + "\n" +
                    "Type: " + type + "\n" +
                    "Message: " + condition + "\n" +
                    "Stack: " + stackTrace + "\n");
            }
            catch
            {
                // Diagnostics must never become the cause of a game failure.
            }
        }

        private static void OnQuitting()
        {
            try
            {
                string path = Path.Combine(Application.persistentDataPath, ReportFileName);
                File.AppendAllText(
                    path,
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
    }
}
