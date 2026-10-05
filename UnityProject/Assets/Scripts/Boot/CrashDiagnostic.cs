using System;
using System.IO;
using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    /// <summary>
    /// Persistent Android crash/exit breadcrumb collector.
    /// It records the previous startup checkpoint, managed errors, and Android's
    /// historical process-exit reason when the device exposes that API.
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
            return
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
            // Unity 2022.3 does not expose the newer AndroidApplication.exitState API,
            // so use Android 11+ ActivityManager historical process-exit information
            // directly. Older Android versions simply return unavailable.
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
                string timestamp = latest.Call<long>("getTimestamp").ToString();

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
                string path = Path.Combine(Application.persistentDataPath, ReportFileName);
                File.AppendAllText(
                    path,
                    "\n--- RUNTIME ERROR ---\n" +
                    "UTC: " + DateTime.UtcNow.ToString("O") + "\n" +
                    "Type: " + type + "\n" +
                    "Message: " + lastException + "\n" +
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
