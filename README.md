# Unity-AutoBuilder

A modular Unity Editor tool that automates building Android App Bundles (`.aab`), tracking daily incremental build numbers, syncing Google Play `versionCode` metrics, and automatically uploading builds directly to Google Play Console tracks via the Google Play Developer API.

---

## Features

* **One-Click Build & Upload:** Generates signed `.aab` builds and uploads them directly to Google Play Internal Testing (or other tracks) with a single click.
* **Smart Versioning:**
  * Auto-increments local build file numbers (e.g., `AppReleaseX.aab`) on new calendar days while overwriting same-day iteration builds.
  * Continuously increments Google Play `bundleVersionCode` on every build execution to comply with API release requirements.
* **Auto-Discovery Engine:** Scans your Unity project directory to automatically detect keystores, JSON service keys, package names, and product titles.
* **Inspector-Friendly Setup:** Easily configure keystore passwords, target tracks, and build output directories inside a custom Editor window.
* **UPM Ready:** Clean package architecture designed for direct installation via Unity Package Manager.

---

## Prerequisites

Before using this tool, ensure you have installed the following on your development machine:

1. **Unity 2021.3+** (with Android Build Support installed).
2. **Python 3.8+** installed and added to system `PATH`.
3. Required Python dependencies. Open Terminal / Command Prompt and run:

```bash
pip install google-api-python-client google-auth-httplib2 google-auth-oauthlib
```

or with

```bash
pip install -r "Scripts~\requirements.txt"
```

## Installation

### Via Unity Package Manager (Git URL)

1. Open your project in Unity.
2. Go to **Window** $\rightarrow$ **Package Manager**.
3. Click the **`+`** button in the top-left corner and select **Add package from git URL...**
4. Paste the repository URL:
```bash
https://github.com/makasDev/Unity-AutoBuilder.git

```


5. Click **Add**.

---

## Google Play API Setup (One-Time Setup)

To allow automated builds to upload to your Google Play Console account, you need to enable the developer API and create a Service Account:

1. Open the [Google Cloud Console](https://console.cloud.google.com/) linked to your Google Play Console account.
2. Navigate to **APIs & Services** $\rightarrow$ **Library** (or **Browse**) and search for **Google Play Android Developer API**, then click **Enable**.
3. Go to **IAM & Admin** $\rightarrow$ **Service accounts** and click the **+ Create service account** button.
4. Enter a name for the service account and click **Create and continue** (or **Create and close**).
5. Locate your newly created account in the list, click the **three dots** under **Actions**, and select **Manage keys**.
6. Click **Add key** $\rightarrow$ **Create new key**, ensure **JSON** is selected, and click **Create**.
7. Download the `.json` file, rename it to `play_service_account.json`, and place it in the **root directory** of your Unity project.
8. Head to [Google Play Console](https://play.google.com/console) $\rightarrow$ **Users and permissions**:
   * Click **Invite new users** and paste the Service Account email address (e.g., `your-account@project.iam.gserviceaccount.com`).
   * Under **App permissions**, assign your game.
   * Grant rights to **Release to testing tracks** and **Manage testing tracks**.
   * Click **Invite user**.



---

## Configuration

1. In Unity, navigate to the top menu bar: **Build Tools** $\rightarrow$ **Build Settings & Setup**.
2. Click **🔍 Auto-Detect Settings From Project** to automatically detect package names, keystore paths, and product details.
3. Fill in your secure credentials:
* **Keystore Password**
* **Key Password**


4. Verify your target track (default: `internal`) and output folder.
5. Click **Save Settings**. The window will automatically close upon saving.

---

## Usage

1. Open your project in Unity.
2. Go to **Build Tools** $\rightarrow$ **Build & Upload to Play Store**.
3. AutoBuilder will:
* Configure keystore credentials and update build numbers.
* Compile the `.aab` bundle to your designated output folder.
* Execute the background Python uploader to deploy your build directly to Google Play.



---

## Project Structure

```text
Unity-AutoBuilder/
├── package.json
├── README.md
├── LICENSE
├── .gitignore
├── Editor/
│   ├── AutoBuilder.cs
│   ├── AutoBuilderSettings.cs
│   └── AutoBuilderWindow.cs
└── Scripts~/
    ├── upload_playstore.py
    └── requirements.txt

```

---

## License

This project is licensed under the [Apache License 2.0](https://github.com/makasDev/Unity-AutoBuilder?tab=Apache-2.0-1-ov-file).
