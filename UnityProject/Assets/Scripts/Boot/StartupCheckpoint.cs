using UnityEngine;

namespace PersiaWar.Unity2D5D
{
    /// <summary>
    /// Persists a tiny startup checkpoint so an Android native/engine crash can be
    /// localized on the next launch even when logcat is unavailable.
    /// </summary>
    public static class StartupCheckpoint
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSplashScreen)]
        private static void MarkBeforeSplashScreen()
        {
            PlayerPrefs.SetString(Key, "BeforeSplashScreen");
            PlayerPrefs.SetString(TimeKey, System.DateTime.UtcNow.ToString("O"));
            PlayerPrefs.Save();
            Debug.Log("PERSIA_BOOT_CHECKPOINT: BeforeSplashScreen");
        }
        private const string Key = "PersiaWar.StartupCheckpoint";
        private const string TimeKey = "PersiaWar.StartupCheckpointTime";

        public static string Last => PlayerPrefs.GetString(Key, "none");

        public static void Set(string stage)
        {
            PlayerPrefs.SetString(Key, stage);
            PlayerPrefs.SetString(TimeKey, System.DateTime.UtcNow.ToString("O"));
            PlayerPrefs.Save();
            Debug.Log("PERSIA_BOOT_CHECKPOINT: " + stage);
        }
    }
}
