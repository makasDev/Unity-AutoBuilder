using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public class AutoBuilder
{
    [MenuItem("Build Tools/Build & Upload to Play Store")]
    public static void BuildAndUpload()
    {
        AutoBuilderSettings settings = AutoBuilderWindow.GetOrCreateSettings();

        if (!EnsurePasswords(settings)) return;

        ConfigureKeystore(settings);
        UpdateBuildNumbers(settings);
        PlayerSettings.Android.bundleVersionCode = settings.playVersionCode;

        string releaseTitle = $"Release {settings.fileBuildNumber} TEST {settings.testPatchNumber}";
        Debug.Log($"[AutoBuilder] Building '{releaseTitle}' | File Name: {settings.buildBaseName}{settings.fileBuildNumber}.aab | Version Code: {settings.playVersionCode}");

        string outputPath = BuildAAB(settings);
        if (outputPath != null)
        {
            Debug.Log($"[AutoBuilder] AAB Build succeeded! Saved to: {outputPath}");
            RunPythonUploadScript(settings, outputPath, releaseTitle);
        }
    }

    [MenuItem("Build Tools/Build Only")]
    public static void BuildOnly()
    {
        AutoBuilderSettings settings = AutoBuilderWindow.GetOrCreateSettings();

        if (!EnsurePasswords(settings)) return;

        ConfigureKeystore(settings);
        UpdateBuildNumbers(settings);
        PlayerSettings.Android.bundleVersionCode = settings.playVersionCode;

        string releaseTitle = $"Release {settings.fileBuildNumber} TEST {settings.testPatchNumber}";
        Debug.Log($"[AutoBuilder] Building '{releaseTitle}' | File Name: {settings.buildBaseName}{settings.fileBuildNumber}.aab | Version Code: {settings.playVersionCode}");

        string outputPath = BuildAAB(settings);
        if (outputPath != null)
        {
            Debug.Log($"[AutoBuilder] Build succeeded! Saved to: {outputPath}");
        }
    }

    [MenuItem("Build Tools/Upload Only")]
    public static void UploadOnly()
    {
        AutoBuilderSettings settings = AutoBuilderWindow.GetOrCreateSettings();

        string buildPath = Path.Combine(settings.buildOutputFolder, $"{settings.buildBaseName}{settings.fileBuildNumber}.aab");
        if (!File.Exists(buildPath))
        {
            Debug.LogError($"[AutoBuilder] No AAB found at: {buildPath}. Build first or check Build Output Directory.");
            return;
        }

        string releaseTitle = $"Release {settings.fileBuildNumber} TEST {settings.testPatchNumber}";
        RunPythonUploadScript(settings, buildPath, releaseTitle);
    }

    private static bool EnsurePasswords(AutoBuilderSettings settings)
    {
        if (string.IsNullOrEmpty(settings.keystorePassword) || string.IsNullOrEmpty(settings.keyPassword))
        {
            Debug.LogWarning("[AutoBuilder] First-time run or missing passwords! Opening setup window...");
            AutoBuilderWindow.ShowWindow();
            return false;
        }
        return true;
    }

    private static void ConfigureKeystore(AutoBuilderSettings settings)
    {
        PlayerSettings.Android.useCustomKeystore = true;
        if (!string.IsNullOrEmpty(settings.keystorePath))
        {
            PlayerSettings.Android.keystoreName = settings.keystorePath;
        }
        PlayerSettings.Android.keystorePass = settings.keystorePassword;
        PlayerSettings.Android.keyaliasName = settings.keyAlias;
        PlayerSettings.Android.keyaliasPass = settings.keyPassword;
    }

    private static string BuildAAB(AutoBuilderSettings settings)
    {
        EditorUserBuildSettings.buildAppBundle = true;
        EditorUserBuildSettings.development = false;

        if (!Directory.Exists(settings.buildOutputFolder))
        {
            Directory.CreateDirectory(settings.buildOutputFolder);
        }

        string fileName = $"{settings.buildBaseName}{settings.fileBuildNumber}.aab";
        string outputPath = Path.Combine(settings.buildOutputFolder, fileName);

        string[] scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();

        if (scenes.Length == 0)
        {
            Debug.LogError("[AutoBuilder] No scenes are enabled in File -> Build Settings!");
            return null;
        }

        UnityEditor.Build.Reporting.BuildReport report = BuildPipeline.BuildPlayer(scenes, outputPath, BuildTarget.Android, BuildOptions.None);

        if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            return outputPath;
        }
        Debug.LogError($"[AutoBuilder] Build failed with result: {report.summary.result}");
        return null;
    }

    private static void UpdateBuildNumbers(AutoBuilderSettings settings)
    {
        string todayStr = DateTime.Now.ToString("yyyy-MM-dd");

        if (settings.lastBuildDate != todayStr)
        {
            // New day: Increment release number, reset test patch counter
            settings.fileBuildNumber++;
            settings.testPatchNumber = 1;
            settings.lastBuildDate = todayStr;
        }
        else
        {
            // Same day: Increment test patch counter
            settings.testPatchNumber++;
        }

        // Play version code increases monotonically with every build attempt
        settings.playVersionCode++;

        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
    }

    private static void RunPythonUploadScript(AutoBuilderSettings settings, string buildPath, string releaseTitle)
    {
        // Resolve package path dynamically for UPM install
        string packagePath = Path.GetFullPath("Packages/com.makasdev.autobuilder/Scripts~/upload_playstore.py");

        // Fallback for direct Assets installation
        if (!File.Exists(packagePath))
        {
            packagePath = Path.GetFullPath("Assets/AutoBuilder/Scripts~/upload_playstore.py");
        }

        if (!File.Exists(packagePath))
        {
            Debug.LogError($"[AutoBuilder] Could not locate upload_playstore.py at: {packagePath}");
            return;
        }

        // Pass arguments with named flags expected by upload_playstore.py
        string arguments = $"\"{packagePath}\" --bundle \"{buildPath}\" --package \"{settings.packageName}\" --json \"{settings.serviceAccountJsonPath}\" --track \"{settings.track}\" --release-name \"{releaseTitle}\"";

        System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo
        {
            // Linux ships `python3`; Windows ships `python`
            FileName = Application.platform == RuntimePlatform.LinuxEditor ? "python3" : "python",
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };

#if UNITY_EDITOR_WIN
        startInfo.CreateNoWindow = true;
#endif

        using (System.Diagnostics.Process process = System.Diagnostics.Process.Start(startInfo))
        {
            string output = process.StandardOutput.ReadToEnd();
            string errors = process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (!string.IsNullOrEmpty(output)) Debug.Log($"[AutoBuilder Upload]: {output}");
            if (!string.IsNullOrEmpty(errors)) Debug.LogError($"[AutoBuilder Upload Error]: {errors}");
        }
    }
}
