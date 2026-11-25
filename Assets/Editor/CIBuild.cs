using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Build.DataBuilders;
using UnityEditor.AddressableAssets.Settings;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Build Android untuk CI (GitHub Actions / game-ci unity-builder).
/// Dipanggil lewat: buildMethod: CIBuild.BuildAndroid
/// Urutan: cek scene -> build konten Addressables -> build APK.
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

        // 1) Scene di Build Settings harus benar-benar ada di repo.
        var enabled = EditorBuildSettings.scenes.Where(s => s.enabled).ToArray();
        var missing = enabled.Where(s => !File.Exists(s.path)).Select(s => s.path).ToArray();
        if (enabled.Length == 0 || missing.Length > 0)
        {
            foreach (var m in missing)
                Debug.LogError($"[CIBuild] Scene ada di Build Settings tapi filenya tidak ada di repo: {m}");
            if (enabled.Length == 0)
                Debug.LogError("[CIBuild] Tidak ada scene yang aktif di Build Settings.");
            EditorApplication.Exit(1);
            return;
        }
        string[] scenes = enabled.Select(s => s.path).ToArray();

        // 2) Pastikan target Android.
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);

        // 3) Versi dari CI (opsional).
        string versionCode = GetArg("androidVersionCode");
        if (int.TryParse(versionCode, out int code) && code > 0)
            PlayerSettings.Android.bundleVersionCode = code;
        string version = GetArg("buildVersion");
        if (!string.IsNullOrEmpty(version) && version != "none")
            PlayerSettings.bundleVersion = version;

        // 4) Build konten Addressables (game ini memuat scene/data lewat Addressables).
        var settings = AddressableAssetSettingsDefaultObject.Settings;
        if (settings == null)
            throw new Exception("[CIBuild] AddressableAssetSettings tidak ditemukan.");

        int packedIndex = settings.DataBuilders.FindIndex(b => b is BuildScriptPackedMode);
        if (packedIndex < 0)
            throw new Exception("[CIBuild] BuildScriptPackedMode tidak ada di DataBuilders.");
        settings.ActivePlayerDataBuilderIndex = packedIndex;

        AddressableAssetSettings.BuildPlayerContent(out AddressablesPlayerBuildResult addrResult);
        if (!string.IsNullOrEmpty(addrResult.Error))
            throw new Exception("[CIBuild] Build Addressables gagal: " + addrResult.Error);
        Debug.Log("[CIBuild] Addressables selesai dibuild.");

        // 5) Build APK.
        EditorUserBuildSettings.buildAppBundle = false;
        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = Path.Combine(outDir, name + ".apk"),
            target = BuildTarget.Android,
            options = BuildOptions.None
        };

        BuildReport report = BuildPipeline.BuildPlayer(options);
        BuildSummary summary = report.summary;
        Debug.Log($"[CIBuild] Hasil: {summary.result}, ukuran {summary.totalSize / (1024 * 1024)} MB, error {summary.totalErrors}");
        EditorApplication.Exit(summary.result == BuildResult.Succeeded ? 0 : 1);
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
