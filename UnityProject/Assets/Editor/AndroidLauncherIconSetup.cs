#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

[InitializeOnLoad]
internal sealed class AndroidLauncherIconSetup : IPreprocessBuildWithReport
{
    private const string IconPath = "Assets/Branding/PersiaWarIcon.jpg";

    static AndroidLauncherIconSetup()
    {
        EditorApplication.delayCall += Apply;
    }

    public int callbackOrder => -1000;

    public void OnPreprocessBuild(BuildReport report)
    {
        if (report.summary.platform == BuildTarget.Android)
            Apply();
    }

    [MenuItem("Persia War/Apply Android Launcher Icon")]
    private static void Apply()
    {
        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
        if (icon == null)
        {
            Debug.LogError("[Persia War] Android launcher icon not found at " + IconPath);
            return;
        }

        var sizes = PlayerSettings.GetIconSizesForTargetGroup(BuildTargetGroup.Android, IconKind.Application);
        if (sizes == null || sizes.Length == 0)
            sizes = new[] { 192 };

        var icons = new Texture2D[sizes.Length];
        for (var i = 0; i < icons.Length; i++)
            icons[i] = icon;

        PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Android, icons, IconKind.Application);
        Debug.Log("[Persia War] Android launcher icon applied from " + IconPath);
    }
}
#endif
