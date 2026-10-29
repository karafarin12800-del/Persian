using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Profiling;

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
        private const string HistoryKey = "PersiaWar.StartupCheckpointHistory";
        private const string LastExceptionKey = "PersiaWar.LastRuntimeException";
        private const int MaxHistoryEntries = 30;

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
        public static string LastException => PlayerPrefs.GetString(LastExceptionKey, "none");
        public static string CheckpointHistory => PlayerPrefs.GetString(HistoryKey, string.Empty);

        public static string DiagnosticSummary =>
            "Previous: " + Previous + " | Exit: " + PreviousExitState + " | Assessment: " + PreviousAssessment;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        private static void MarkBeforeSplashScreen()
        {
            string previous = PlayerPrefs.GetString(Key, "none");
            string previousTime = PlayerPrefs.GetString(TimeKey, "unknown");
            string memoryFromPreviousRun = MemorySummary;

            PlayerPrefs.SetString(PreviousMemoryKey, memoryFromPreviousRun);
            PlayerPrefs.SetString(PreviousKey, previous);
            PlayerPrefs.SetString(PreviousTimeKey, previousTime);
            PlayerPrefs.Save();

            // Do not call AndroidJava or write crash reports before the splash screen.
            // Unity's Java Activity may not be ready yet at this lifecycle phase.
            Set("BeforeSplashScreen");
        }

        /// <summary>
        /// Call from BootLoader.Start once Unity has created the Android Activity.
        /// The method is idempotent and safe to call more than once.
        /// </summary>
        public static void InitializeDiagnostics()
        {
            InitializeCrashDiagnostic();
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

            Application.logMessageReceived += OnLogMessage;
            Application.quitting += OnQuitting;
            Application.lowMemory -= OnLowMemory;
            Application.lowMemory += OnLowMemory;
            RecordMemorySample("DiagnosticsInitialized");

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
            PlayerPrefs.SetString(LastExceptionKey, lastException.Length > 900 ? lastException.Substring(0, 900) : lastException);
            PlayerPrefs.Save();

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

        private const string AllocatedMemoryKey = "PersiaWar.Memory.AllocatedMB";
        private const string ReservedMemoryKey = "PersiaWar.Memory.ReservedMB";
        private const string MonoMemoryKey = "PersiaWar.Memory.MonoMB";
        private const string SystemMemoryKey = "PersiaWar.Memory.SystemMB";
        private const string MemoryTimeKey = "PersiaWar.Memory.SampleUtc";
        private const string MemoryCheckpointKey = "PersiaWar.Memory.Checkpoint";
        private const string PreviousMemoryKey = "PersiaWar.PreviousMemorySummary";

        public static bool ShouldUseLiteWorld()
        {
            long allocatedMb = Profiler.GetTotalAllocatedMemoryLong() / (1024L * 1024L);
            long reservedMb = Profiler.GetTotalReservedMemoryLong() / (1024L * 1024L);
            int systemMb = SystemInfo.systemMemorySize;
            return systemMb > 0 &&
                (systemMb <= 3072 ||
                 (systemMb <= 4096 && (allocatedMb >= 650 || reservedMb >= 1150)) ||
                 allocatedMb >= Mathf.Max(900, systemMb / 3) ||
                 reservedMb >= Mathf.Max(1500, systemMb / 2));
        }

        public static string PreviousMemorySummary =>
            PlayerPrefs.GetString(PreviousMemoryKey, "not captured from previous run");

        public static string MemorySummary =>
            "Mem MB A/R/Mono/System: " +
            PlayerPrefs.GetInt(AllocatedMemoryKey, -1) + "/" +
            PlayerPrefs.GetInt(ReservedMemoryKey, -1) + "/" +
            PlayerPrefs.GetInt(MonoMemoryKey, -1) + "/" +
            PlayerPrefs.GetInt(SystemMemoryKey, SystemInfo.systemMemorySize) +
            " @ " + PlayerPrefs.GetString(MemoryTimeKey, "unknown") +
            " [" + PlayerPrefs.GetString(MemoryCheckpointKey, "none") + "]";

        public static void RecordMemorySample(string checkpoint, bool persistImmediately = true)
        {
            try
            {
                PlayerPrefs.SetInt(AllocatedMemoryKey, (int)(Profiler.GetTotalAllocatedMemoryLong() / (1024L * 1024L)));
                PlayerPrefs.SetInt(ReservedMemoryKey, (int)(Profiler.GetTotalReservedMemoryLong() / (1024L * 1024L)));
                PlayerPrefs.SetInt(MonoMemoryKey, (int)(Profiler.GetMonoUsedSizeLong() / (1024L * 1024L)));
                PlayerPrefs.SetInt(SystemMemoryKey, SystemInfo.systemMemorySize);
                PlayerPrefs.SetString(MemoryTimeKey, DateTime.UtcNow.ToString("O"));
                PlayerPrefs.SetString(MemoryCheckpointKey, string.IsNullOrEmpty(checkpoint) ? "unknown" : checkpoint);
                if (persistImmediately) PlayerPrefs.Save();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("PERSIA_MEMORY_SAMPLE_FAILED: " + ex.GetType().Name);
            }
        }

        private static void OnLowMemory()
        {
            RecordMemorySample("LOW_MEMORY_WARNING");
            Set("LOW_MEMORY_WARNING");
            PlayerPrefs.SetString(PreviousExitStateKey, "LOW_MEMORY_WARNING");
            PlayerPrefs.SetString("PersiaWar.PreviousAssessment", "LIKELY_LOW_MEMORY");
            PlayerPrefs.Save();
            Debug.LogWarning("PERSIA_LOW_MEMORY_WARNING: " + MemorySummary);
            Resources.UnloadUnusedAssets();
        }

        public static string BuildCopyReport()
        {
            return "PERSIA WAR LAST RUN REPORT\n" +
                "Current checkpoint: " + Last + "\n" +
                "Previous checkpoint: " + Previous + "\n" +
                "Previous time: " + PreviousTime + "\n" +
                "Exit reason: " + PreviousExitState + "\n" +
                "Assessment: " + PreviousAssessment + "\n" +
                "Last exception: " + LastException + "\n" +
                "Previous run memory: " + PreviousMemorySummary + "\n" +
                "Current memory: " + MemorySummary + "\n\n" +
                "Checkpoint history (last 30):\n" + CheckpointHistory + "\n\n" +
                "Diagnostic file: " + ReportPath;
        }

        public static string ReportPath
        {
            get { return Path.Combine(Application.persistentDataPath, ReportFileName); }
        }

        public static void Set(string stage)
        {
            if (string.IsNullOrEmpty(stage))
                stage = "Unknown";

            RecordMemorySample(stage, false);
            PlayerPrefs.SetString(Key, stage);
            PlayerPrefs.SetString(TimeKey, DateTime.UtcNow.ToString("O"));
            string entry = DateTime.UtcNow.ToString("HH:mm:ss") + "  " + stage;
            string history = PlayerPrefs.GetString(HistoryKey, string.Empty);
            string[] prior = history.Split(new[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            int first = Math.Max(0, prior.Length - (MaxHistoryEntries - 1));
            List<string> retained = new List<string>();
            for (int i = first; i < prior.Length; i++) retained.Add(prior[i]);
            retained.Add(entry);
            PlayerPrefs.SetString(HistoryKey, string.Join("\n", retained));

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
