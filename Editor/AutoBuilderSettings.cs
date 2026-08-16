using UnityEngine;

public class AutoBuilderSettings : ScriptableObject
{
    [Header("Keystore Configuration")]
    public string keystorePath = "";
    public string keystorePassword = "";
    public string keyAlias = "";
    public string keyPassword = "";

    [Header("Google Play API")]
    public string packageName = "";
    public string serviceAccountJsonPath = "";
    public string track = "internal";

    [Header("Build Output Directory")]
    public string buildOutputFolder = "";
    public string buildBaseName = "";

    [Header("Version Management")]
    public int fileBuildNumber = 0;
    public int playVersionCode = 0;
    public string lastBuildDate = "";
}