using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Build Android untuk CI (GitHub Actions / game-ci unity-builder).
/// Dipanggil lewat: buildMethod: CIBuild.BuildAndroid
/// Urutan: cek scene -> build APK.
/// </summary>
public static class CIBuild
{
    private const string DefaultOutputDir = "build/Android";
    private const string DefaultName = "SamuraiVsZombieDTP2";

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

        // 2b) Pengaturan Android wajib: min Android 7.0 (API 24), target API 35, IL2CPP, ARMv7 + ARM64, GLES3.
        ApplyAndroidSettings();

        // 3) Versi dari CI (opsional).
        string versionCode = GetArg("androidVersionCode");
        if (int.TryParse(versionCode, out int code) && code > 0)
            PlayerSettings.Android.bundleVersionCode = code;
        string version = GetArg("buildVersion");
        if (!string.IsNullOrEmpty(version) && version != "none")
            PlayerSettings.bundleVersion = version;

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

    private static void ApplyAndroidSettings()
    {
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel24;
        PlayerSettings.Android.targetSdkVersion = (AndroidSdkVersions)35;
        PlayerSettings.SetScriptingBackend(UnityEditor.Build.NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARMv7 | AndroidArchitecture.ARM64;
        PlayerSettings.Android.forceInternetPermission = true; // wajib untuk MaintenanceGate / Supabase
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.Android, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.Android, new[] { UnityEngine.Rendering.GraphicsDeviceType.OpenGLES3 });
        PlayerSettings.SetApplicationIdentifier(UnityEditor.Build.NamedBuildTarget.Android, "com.dtp.samuraivszombie");

        // Tanda tangan tetap -> APK versi baru bisa menimpa versi lama tanpa "aplikasi tidak terinstal".
        string ksPath = Environment.GetEnvironmentVariable("ANDROID_KEYSTORE_PATH");
        if (string.IsNullOrEmpty(ksPath)) ksPath = "Keystore/dtp.keystore";
        string ksPass = Environment.GetEnvironmentVariable("ANDROID_KEYSTORE_PASS");
        if (string.IsNullOrEmpty(ksPass)) ksPass = "dtp123456";
        string alias = Environment.GetEnvironmentVariable("ANDROID_KEYALIAS_NAME");
        if (string.IsNullOrEmpty(alias)) alias = "dtpkey";
        string aliasPass = Environment.GetEnvironmentVariable("ANDROID_KEYALIAS_PASS");
        if (string.IsNullOrEmpty(aliasPass)) aliasPass = ksPass;

        if (!File.Exists(ksPath))
            throw new Exception("[CIBuild] Keystore tidak ditemukan: " + ksPath);

        PlayerSettings.Android.useCustomKeystore = true;
        PlayerSettings.Android.keystoreName = ksPath;
        PlayerSettings.Android.keystorePass = ksPass;
        PlayerSettings.Android.keyaliasName = alias;
        PlayerSettings.Android.keyaliasPass = aliasPass;

        Debug.Log("[CIBuild] Android: minSdk=24, targetSdk=35, IL2CPP, ARMv7+ARM64, GLES3, keystore=" + ksPath);
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
