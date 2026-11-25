using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Build APK Android lewat command line / GitHub Actions (game-ci).
/// Dipanggil lewat: buildMethod: CIBuild.BuildAndroid
/// </summary>
public static class CIBuild
{
    private const string DefaultOutputDir = "build/Android";
    private const string DefaultName = "SamuraiVsZombieDTP";

    public static void BuildAndroid()
    {
        try
        {
            Run();
        }
        catch (Exception e)
        {
            Debug.LogException(e);
            EditorApplication.Exit(1);
        }
    }

    private static void Run()
    {
        string outDir = GetArg("customBuildPath") ?? DefaultOutputDir;
        string name = GetArg("customBuildName") ?? DefaultName;
        Directory.CreateDirectory(outDir);

        // Scene di Build Settings harus benar-benar ada.
        var enabled = EditorBuildSettings.scenes.Where(s => s.enabled).ToArray();
        var missing = enabled.Where(s => !File.Exists(s.path)).Select(s => s.path).ToArray();
        if (enabled.Length == 0 || missing.Length > 0)
        {
            foreach (var m in missing)
                Debug.LogError("[CIBuild] Scene ada di Build Settings tapi filenya tidak ada: " + m);
            if (enabled.Length == 0)
                Debug.LogError("[CIBuild] Tidak ada scene yang aktif di Build Settings.");
            EditorApplication.Exit(1);
            return;
        }
        string[] scenes = enabled.Select(s => s.path).ToArray();

        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

        PlayerSettings.productName = "Samurai vs Zombie DTP";
        PlayerSettings.SetApplicationIdentifier(BuildTargetGroup.Android, "com.dtp.samuraivszombie2");
        PlayerSettings.SetScriptingBackend(BuildTargetGroup.Android, ScriptingImplementation.IL2CPP);
        AndroidSigning.Apply();

        string versionCode = GetArg("androidVersionCode");
        int code;
        if (int.TryParse(versionCode, out code) && code > 0)
            PlayerSettings.Android.bundleVersionCode = code;

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = Path.Combine(outDir, name + ".apk"),
            target = BuildTarget.Android,
            options = BuildOptions.None
        };

#if UNITY_2018_1_OR_NEWER
        var report = BuildPipeline.BuildPlayer(options);
        bool ok = report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded;
        Debug.Log("[CIBuild] Hasil: " + report.summary.result + ", error " + report.summary.totalErrors);
#else
        string error = BuildPipeline.BuildPlayer(options);
        bool ok = string.IsNullOrEmpty(error);
        if (!ok) Debug.LogError("[CIBuild] " + error);
#endif
        EditorApplication.Exit(ok ? 0 : 1);
    }

    private static string GetArg(string name)
    {
        var args = Environment.GetCommandLineArgs();
        for (int i = 0; i < args.Length - 1; i++)
        {
            if (args[i] == "-" + name) return args[i + 1];
        }
        return null;
    }
}
