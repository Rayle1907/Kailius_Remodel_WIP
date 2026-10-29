using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;

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

}
