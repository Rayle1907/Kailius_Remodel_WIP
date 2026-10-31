using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;

public static class CodexAndroidBuild
{
    public static void BuildGauntlet11()
    {
        const string outputDir = "Builds/Android";
        const string outputPath = outputDir + "/Gauntlet1_1.apk";
        string[] scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .OrderBy(scenePath => scenePath == "Assets/Scenes/Menu.unity" ? 0 :
                scenePath == "Assets/Scenes/1_1Gauntlet.unity" ? 1 : 2)
            .ToArray();

        if (scenes.Length == 0 || scenes[0] != "Assets/Scenes/Menu.unity")
        {
            throw new Exception("Android build stopped: Menu must be the first enabled scene in Build Settings.");
        }

        if (!PlayerSettings.Android.useCustomKeystore ||
            string.IsNullOrWhiteSpace(PlayerSettings.Android.keystoreName) ||
            !File.Exists(PlayerSettings.Android.keystoreName) ||
            string.IsNullOrWhiteSpace(PlayerSettings.Android.keyaliasName))
        {
            throw new Exception("Android release build stopped: configure an existing permanent keystore and alias in Player Settings > Publishing Settings.");
        }

        Directory.CreateDirectory(outputDir);

        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;
        EditorUserBuildSettings.buildAppBundle = false;

        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.kailius.gauntlet11");
        PlayerSettings.productName = "Gauntlet 1.1";
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outputPath,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.CleanBuildCache
        };

        var report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            throw new Exception("Android build failed: " + report.summary.result);
        }
    }

    [MenuItem("Tools/Build/Kailius Playtest APK")]
    public static void BuildScene1Only()
    {
        const string outputDir = "Builds/Android";
        const string outputPath = outputDir + "/Scene1Only.apk";
        string[] scenes =
        {
            "Assets/Scenes/Menu.unity",
            "Assets/Scenes/Scene_1.unity",
            "Assets/Scenes/1_1Gauntlet.unity",
            "Assets/Scenes/1_2Gauntlet.unity",
            "Assets/Scenes/1_3Gauntlet.unity"
        };

        if (!PlayerSettings.Android.useCustomKeystore ||
            string.IsNullOrWhiteSpace(PlayerSettings.Android.keystoreName) ||
            !File.Exists(PlayerSettings.Android.keystoreName) ||
            string.IsNullOrWhiteSpace(PlayerSettings.Android.keyaliasName))
        {
            throw new Exception("Scene 1 release build stopped: configure a permanent keystore and alias in Publishing Settings.");
        }

        Directory.CreateDirectory(outputDir);

        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;

        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.kailius.gauntlet11");
        PlayerSettings.productName = "Kailius";
        EditorUserBuildSettings.buildAppBundle = false;
        PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outputPath,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.CleanBuildCache
        };

        var report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            throw new Exception("Scene 1 Android build failed: " + report.summary.result);
        }
    }

    [MenuItem("Tools/Build/Emulator build")]
    public static void BuildAndRunOnEmulator()
    {
        BuildScene1Only();

        string adbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Android", "Sdk", "platform-tools", "adb.exe");
        if (!File.Exists(adbPath))
        {
            throw new Exception("Emulator install stopped: ADB was not found at " + adbPath + ". Check the Android SDK path in Unity Preferences > External Tools.");
        }

        string[] devices = RunAdb(adbPath, "devices").Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        string emulator = devices
            .Skip(1)
            .Select(line => line.Split(new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            .FirstOrDefault(parts => parts.Length > 1 && parts[0].StartsWith("emulator-") && parts[1] == "device")?[0];

        if (emulator == null)
        {
            throw new Exception("Emulator install stopped: no running, authorized Android emulator was found. Start the emulator and run Tools > Build > Emulator build again.");
        }

        string apkPath = Path.GetFullPath("Builds/Android/Scene1Only.apk");
        RunAdb(adbPath, "-s " + emulator + " install -r \"" + apkPath + "\"");
        RunAdb(adbPath, "-s " + emulator + " shell monkey -p com.kailius.gauntlet11 1");
    }

    private static string RunAdb(string adbPath, string arguments)
    {
        var startInfo = new System.Diagnostics.ProcessStartInfo
        {
            FileName = adbPath,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using (var process = System.Diagnostics.Process.Start(startInfo))
        {
            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();
            if (process.ExitCode != 0)
            {
                throw new Exception("ADB failed: " + (string.IsNullOrWhiteSpace(error) ? output : error));
            }
            return output;
        }
    }

}
