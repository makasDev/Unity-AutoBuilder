using UnityEditor;
using UnityEngine;
using System.Linq;
using System.IO;
using System;

public class AutoBuilder 
{
    [MenuItem("Build Tools/Build & Upload to Play Store")]
    public static void BuildAndUpload()
    {
        AutoBuilderSettings settings = AutoBuilderWindow.GetOrCreateSettings();

        // 1. Validation Check / First Run Setup Prompt
        if (string.IsNullOrEmpty(settings.keystorePassword) || string.IsNullOrEmpty(settings.keyPassword))
        {
            Debug.LogWarning("[AutoBuilder] First-time run or missing passwords! Opening setup window...");
            AutoBuilderWindow.ShowWindow();
            return;
        }

        // 2. Configure Keystore Details
        PlayerSettings.Android.useCustomKeystore = true;
        if (!string.IsNullOrEmpty(settings.keystorePath))
        {
            PlayerSettings.Android.keystoreName = settings.keystorePath;
        }
        PlayerSettings.Android.keystorePass = settings.keystorePassword;
        PlayerSettings.Android.keyaliasName = settings.keyAlias;
        PlayerSettings.Android.keyaliasPass = settings.keyPassword;

        // 3. Update Version Numbers
        UpdateBuildNumbers(settings);

        PlayerSettings.Android.bundleVersionCode = settings.playVersionCode;

        Debug.Log($"[AutoBuilder] File Name: {settings.buildBaseName}{settings.fileBuildNumber}.aab | Version Code: {settings.playVersionCode}");

        // 4. Configure Output Path
        EditorUserBuildSettings.buildAppBundle = true;
        EditorUserBuildSettings.development = false;

        if (!Directory.Exists(settings.buildOutputFolder))
        {
            Directory.CreateDirectory(settings.buildOutputFolder);
        }

        string fileName = $"{settings.buildBaseName}{settings.fileBuildNumber}.aab";
        string outputPath = Path.Combine(settings.buildOutputFolder, fileName);

        // 5. Gather Scenes
        string[] scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();

        if (scenes.Length == 0)
        {
            Debug.LogError("[AutoBuilder] No scenes are enabled in File -> Build Settings!");
            return;
        }

        // 6. Build AAB
        UnityEditor.Build.Reporting.BuildReport report = BuildPipeline.BuildPlayer(scenes, outputPath, BuildTarget.Android, BuildOptions.None);
        
        if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            Debug.Log($"[AutoBuilder] AAB Build succeeded! Saved to: {outputPath}");
            
            // UPDATE THIS LINE: Add settings.fileBuildNumber as the 3rd argument
            RunPythonUploadScript(settings, outputPath, settings.fileBuildNumber); 
        }
        else
        {
            Debug.LogError($"[AutoBuilder] Build failed with result: {report.summary.result}");
        }
    }

    private static void UpdateBuildNumbers(AutoBuilderSettings settings)
    {
        string todayStr = DateTime.Now.ToString("yyyy-MM-dd");

        if (settings.lastBuildDate != todayStr)
        {
            settings.fileBuildNumber++;
            settings.lastBuildDate = todayStr;
        }

        // Play version code increases with every single build attempt
        settings.playVersionCode++;

        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();
    }

    private static void RunPythonUploadScript(AutoBuilderSettings settings, string buildPath, int fileBuildNumber)
    {
        string packagePath = Path.GetFullPath("Packages/com.makasdev.autobuilder/Scripts~/upload_playstore.py");
        
        if (!File.Exists(packagePath))
        {
            packagePath = Path.GetFullPath("Assets/AutoBuilder/Scripts~/upload_playstore.py");
        }
    
        if (!File.Exists(packagePath))
        {
            Debug.LogError($"[AutoBuilder] Could not locate upload_playstore.py at: {packagePath}");
            return;
        }
    
        // Pass --build-number matching your current file build count (e.g. 63)
        string arguments = $"\"{packagePath}\" --bundle \"{buildPath}\" --package \"{settings.packageName}\" --json \"{settings.serviceAccountJsonPath}\" --track \"{settings.track}\" --build-number \"{fileBuildNumber}\"";
    
        System.Diagnostics.ProcessStartInfo startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = "python",
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
    
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