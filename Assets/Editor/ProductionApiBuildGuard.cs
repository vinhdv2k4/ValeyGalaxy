using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>Prevent shipping an executable that still points to the developer's PC.</summary>
public sealed class ProductionApiBuildGuard : IPreprocessBuildWithReport
{
    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        if ((report.summary.options & BuildOptions.Development) != 0) return;
        var menu = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resources/Startup/StartupMenu.prefab");
        var flow = menu != null ? menu.GetComponent<StartupMenuFlow>() : null;
        if (flow == null || !StartupApiUrl.IsProductionUrl(flow.apiBaseUrl))
            throw new BuildFailedException("Set StartupMenu Api Base Url to your Railway HTTPS domain before a release build. See Backend/RAILWAY_DEPLOY.vi.md.");
    }
}
