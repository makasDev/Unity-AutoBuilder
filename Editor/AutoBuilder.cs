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

        // 3. Update Version & Release Numbers
        UpdateBuildNumbers(settings);

        PlayerSettings.Android.bundleVersionCode = settings.playVersionCode;

        string releaseTitle = $"Release {settings.fileBuildNumber} TEST {settings.testPatchNumber}";
        Debug.Log($"[AutoBuilder] Building '{releaseTitle}' | File Name: {settings.buildBaseName}{settings.fileBuildNumber}.aab | Version Code: {settings.playVersionCode}");

        // 4. Configure Output Path
        EditorUserBuildSettings.buildAppBundle = true;
        EditorUserBuildSettings.development = false;

        if (!Directory.Exists(settings.buildOutputFolder))
        {
            Directory.CreateDirectory(settings.buildOutputFolder);
        }

        string fileName = $"{settings.buildBaseName}{settings.fileBuildNumber}.aab";
        string outputPath = Path.Combine(settings.buildOutputFolder, fileName);

        // 5. Gather Enabled Scenes
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
            
            // Execute python uploader with formatted release name
            RunPythonUploadScript(settings, outputPath, releaseTitle, settings.track);
        }
        else
        {
            Debug.LogError($"[AutoBuilder] Build failed with result: {report.summary.result}");
        }
    }

    [MenuItem("Build Tools/Build & Upload to Production")]
    public static void BuildAndUploadProduction()
    {
        AutoBuilderSettings settings = AutoBuilderWindow.GetOrCreateSettings();

        // 1. Validation Check / First Run Setup Prompt
        if (string.IsNullOrEmpty(settings.keystorePassword) || string.IsNullOrEmpty(settings.keyPassword))
        {
            Debug.LogWarning("[AutoBuilder] First-time run or missing passwords! Opening setup window...");
            AutoBuilderWindow.ShowWindow();
            return;
        }

        // 2. Prompt for release notes (with persisted default)
        string notes = ProductionReleaseNotesPrompt.ShowDialog(settings.productionReleaseNotes);
        if (notes == null) // User cancelled
        {
            Debug.Log("[AutoBuilder] Production build cancelled by user.");
            return;
        }

        // Persist the release notes for next time
        settings.productionReleaseNotes = notes;
        EditorUtility.SetDirty(settings);
        AssetDatabase.SaveAssets();

        // 3. Configure Keystore Details (same as test build)
        PlayerSettings.Android.useCustomKeystore = true;
        if (!string.IsNullOrEmpty(settings.keystorePath))
        {
            PlayerSettings.Android.keystoreName = settings.keystorePath;
        }
        PlayerSettings.Android.keystorePass = settings.keystorePassword;
        PlayerSettings.Android.keyaliasName = settings.keyAlias;
        PlayerSettings.Android.keyaliasPass = settings.keyPassword;

        // 4. Update Version & Release Numbers (reuse fileBuildNumber)
        UpdateBuildNumbers(settings);

        PlayerSettings.Android.bundleVersionCode = settings.playVersionCode;

        // Production release name: "Release {fileBuildNumber}" (no TEST suffix)
        string releaseTitle = $"Release {settings.fileBuildNumber}";
        Debug.Log($"[AutoBuilder] Building PRODUCTION '{releaseTitle}' | File Name: {settings.buildBaseName}{settings.fileBuildNumber}.aab | Version Code: {settings.playVersionCode}");

        // 5. Configure Output Path
        EditorUserBuildSettings.buildAppBundle = true;
        EditorUserBuildSettings.development = false;

        if (!Directory.Exists(settings.buildOutputFolder))
        {
            Directory.CreateDirectory(settings.buildOutputFolder);
        }

        string fileName = $"{settings.buildBaseName}{settings.fileBuildNumber}.aab";
        string outputPath = Path.Combine(settings.buildOutputFolder, fileName);

        // 6. Gather Enabled Scenes
        string[] scenes = EditorBuildSettings.scenes
            .Where(s => s.enabled)
            .Select(s => s.path)
            .ToArray();

        if (scenes.Length == 0)
        {
            Debug.LogError("[AutoBuilder] No scenes are enabled in File -> Build Settings!");
            return;
        }

        // 7. Build AAB
        UnityEditor.Build.Reporting.BuildReport report = BuildPipeline.BuildPlayer(scenes, outputPath, BuildTarget.Android, BuildOptions.None);

        if (report.summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            Debug.Log($"[AutoBuilder] PRODUCTION AAB Build succeeded! Saved to: {outputPath}");

            // Execute python uploader with production track
            RunPythonUploadScript(settings, outputPath, releaseTitle, "production", "draft");

            // Show reminder pop-up
            EditorUtility.DisplayDialog(
                "Production Build Uploaded",
                $"Build '{releaseTitle}' has been uploaded to the Production track as a DRAFT release.\n\n" +
                "IMPORTANT: You must now go to Google Play Console → Testing & releases → Production → " +
                "Edit release → Review and rollout to publish this build to users.",
                "OK"
            );
        }
        else
        {
            Debug.LogError($"[AutoBuilder] Production build failed with result: {report.summary.result}");
        }
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

    private static void RunPythonUploadScript(AutoBuilderSettings settings, string buildPath, string releaseTitle, string track = null, string status = "completed")
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

        string targetTrack = track ?? settings.track;

        // Pass arguments with named flags expected by upload_playstore.py
        string arguments = $"\"{packagePath}\" --bundle \"{buildPath}\" --package \"{settings.packageName}\" --json \"{settings.serviceAccountJsonPath}\" --track \"{targetTrack}\" --release-name \"{releaseTitle}\" --status \"{status}\"";

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

/// <summary>
/// Simple EditorWindow for prompting release notes input for production builds.
/// Returns the entered text, or null if user cancelled.
/// </summary>
public class ProductionReleaseNotesPrompt : EditorWindow
{
    private string notes;
    private string result;
    private static ProductionReleaseNotesPrompt instance;

    public static string ShowDialog(string defaultNotes = "")
    {
        instance = GetWindow<ProductionReleaseNotesPrompt>(true, "Production Release Notes", true);
        instance.minSize = new Vector2(400, 200);
        instance.notes = defaultNotes;
        instance.result = null;
        instance.ShowModal();

        // ShowModal is synchronous in Unity Editor - it blocks until window closes
        return instance.result;
    }

    private void OnGUI()
    {
        GUILayout.Label("Enter release notes for this production build:", EditorStyles.boldLabel);
        EditorGUILayout.Space(10);

        notes = EditorGUILayout.TextArea(notes, GUILayout.Height(100), GUILayout.ExpandWidth(true));

        EditorGUILayout.Space(15);

        GUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();

        if (GUILayout.Button("OK", GUILayout.Width(80), GUILayout.Height(30)))
        {
            result = notes;
            Close();
        }

        if (GUILayout.Button("Cancel", GUILayout.Width(80), GUILayout.Height(30)))
        {
            result = null;
            Close();
        }

        GUILayout.EndHorizontal();
    }
}