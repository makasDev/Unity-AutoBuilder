using UnityEditor;
using UnityEngine;
using System.IO;
using System.Linq;

public class AutoBuilderWindow : EditorWindow
{
    private static AutoBuilderSettings settings;
    private const string SETTINGS_PATH = "Assets/Editor/AutoBuilderSettings.asset";

    [MenuItem("Build Tools/Build Settings & Setup")]
    public static void ShowWindow()
    {
        GetWindow<AutoBuilderWindow>("AutoBuilder Setup");
    }

    public static AutoBuilderSettings GetOrCreateSettings()
    {
        if (settings != null) return settings;

        settings = AssetDatabase.LoadAssetAtPath<AutoBuilderSettings>(SETTINGS_PATH);

        if (settings == null)
        {
            settings = ScriptableObject.CreateInstance<AutoBuilderSettings>();

            if (!Directory.Exists("Assets/Editor"))
            {
                Directory.CreateDirectory("Assets/Editor");
            }

            AssetDatabase.CreateAsset(settings, SETTINGS_PATH);
            AssetDatabase.SaveAssets();

            AutoDetectSettings(settings);
            Debug.Log("[AutoBuilder] Created new settings asset and executed auto-detection.");
        }

        return settings;
    }

    private void OnGUI()
    {
        settings = GetOrCreateSettings();

        GUILayout.Label("AutoBuilder Module Configuration", EditorStyles.boldLabel);
        EditorGUILayout.Space(10);

        GUI.backgroundColor = new Color(0.2f, 0.8f, 0.4f);
        if (GUILayout.Button("🔍 Auto-Detect Settings From Project", GUILayout.Height(32)))
        {
            AutoDetectSettings(settings);
            ShowNotification(new GUIContent("Auto-detection complete!"));
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.Space(10);

        SerializedObject serializedSettings = new SerializedObject(settings);
        SerializedProperty prop = serializedSettings.GetIterator();
        prop.NextVisible(true);

        while (prop.NextVisible(false))
        {
            EditorGUILayout.PropertyField(prop, true);
        }

        serializedSettings.ApplyModifiedProperties();

        EditorGUILayout.Space(20);

        if (GUILayout.Button("Save Settings", GUILayout.Height(30)))
        {
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssets();
            Debug.Log("[AutoBuilder] Settings saved successfully.");
            
            // Auto-close the setup window upon saving
            Close();
        }
    }

    public static void AutoDetectSettings(AutoBuilderSettings s)
    {
        string projectRoot = Directory.GetCurrentDirectory();

        if (string.IsNullOrEmpty(s.packageName))
        {
            s.packageName = PlayerSettings.applicationIdentifier;
        }

        if (string.IsNullOrEmpty(s.keyAlias) && !string.IsNullOrEmpty(PlayerSettings.Android.keyaliasName))
        {
            s.keyAlias = PlayerSettings.Android.keyaliasName;
        }

        if (string.IsNullOrEmpty(s.keystorePath))
        {
            if (!string.IsNullOrEmpty(PlayerSettings.Android.keystoreName))
            {
                s.keystorePath = PlayerSettings.Android.keystoreName;
            }
            else
            {
                var keystoreFiles = Directory.GetFiles(projectRoot, "*.keystore", SearchOption.AllDirectories)
                    .Concat(Directory.GetFiles(projectRoot, "*.jks", SearchOption.AllDirectories))
                    .Where(f => !f.Contains("Library") && !f.Contains("Temp"))
                    .ToList();

                if (keystoreFiles.Count > 0)
                {
                    s.keystorePath = Path.GetRelativePath(projectRoot, keystoreFiles[0]);
                }
            }
        }

        if (string.IsNullOrEmpty(s.serviceAccountJsonPath))
        {
            var jsonFiles = Directory.GetFiles(projectRoot, "*.json", SearchOption.TopDirectoryOnly)
                .Concat(Directory.GetFiles(Path.Combine(projectRoot, "Assets"), "*.json", SearchOption.AllDirectories))
                .Where(f => f.ToLower().Contains("service") || f.ToLower().Contains("play") || f.ToLower().Contains("account"))
                .ToList();

            if (jsonFiles.Count > 0)
            {
                s.serviceAccountJsonPath = Path.GetRelativePath(projectRoot, jsonFiles[0]);
            }
        }

        if (string.IsNullOrEmpty(s.track)) s.track = "internal";
        if (string.IsNullOrEmpty(s.buildBaseName)) s.buildBaseName = Application.productName.Replace(" ", "");

        if (s.playVersionCode == 0)
        {
            s.playVersionCode = PlayerSettings.Android.bundleVersionCode > 0 ? PlayerSettings.Android.bundleVersionCode : 1;
        }

        EditorUtility.SetDirty(s);
        AssetDatabase.SaveAssets();
    }
}