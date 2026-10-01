using System;
using System.IO;
using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    /// <summary>
    /// Persists the last startup checkpoint and the latest startup exception so
    /// Android startup failures remain diagnosable across process death.
    /// </summary>
    public static class StartupCheckpoint
    {
        private const string Key = "PersiaWar.StartupCheckpoint";
        private const string TimeKey = "PersiaWar.StartupCheckpointTime";
        private const string ErrorFile = "persiawar_last_startup_error.txt";

        private static bool initialized;

        public static string Last => PlayerPrefs.GetString(Key, "none");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (initialized) return;
            initialized = true;

            try
            {
                AppDomain.CurrentDomain.UnhandledException += HandleUnhandledException;
            }
            catch (Exception ex)
            {
                Debug.LogWarning("PERSIA_BOOT_CHECKPOINT: Unhandled exception hook unavailable: " + ex.Message);
            }

            Set("RuntimeInitialize");
        }

        public static void Set(string stage)
        {
            string value = string.IsNullOrEmpty(stage) ? "unknown" : stage;
            try
            {
                PlayerPrefs.SetString(Key, value);
                PlayerPrefs.SetString(TimeKey, DateTime.UtcNow.ToString("O"));
                PlayerPrefs.Save();
            }
            catch (Exception ex)
            {
                Debug.LogWarning("PERSIA_BOOT_CHECKPOINT: Could not persist stage '" + value + "': " + ex.Message);
            }

            Debug.Log("PERSIA_BOOT_CHECKPOINT: " + value);
        }

        public static void CaptureException(string stage, Exception exception)
        {
            string message = string.IsNullOrEmpty(stage) ? "StartupFailure" : stage;
            Set(message);

            string text = "Stage=" + message + Environment.NewLine
                + "Utc=" + DateTime.UtcNow.ToString("O") + Environment.NewLine;

            if (exception != null)
                text += exception;

            try
            {
                File.WriteAllText(Path.Combine(Application.persistentDataPath, ErrorFile), text);
            }
            catch (Exception writeException)
            {
                Debug.LogWarning("PERSIA_BOOT_CHECKPOINT: Could not persist startup exception: " + writeException.Message);
            }
        }

        private static void HandleUnhandledException(object sender, UnhandledExceptionEventArgs args)
        {
            Exception exception = args.ExceptionObject as Exception;
            CaptureException("UnhandledException", exception);
        }
    }
}
