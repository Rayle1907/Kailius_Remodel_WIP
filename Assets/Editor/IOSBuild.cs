using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

public static class IOSBuild
{
    [MenuItem("Build/Export iOS project")]
    public static void BuildForDevice()
    {
        string[] scenes =
        {
            "Assets/Scenes/Menu.unity",
            "Assets/Scenes/Scene_1.unity",
            "Assets/Scenes/1_1Gauntlet.unity",
            "Assets/Scenes/1_2Gauntlet.unity",
            "Assets/Scenes/1_3Gauntlet.unity"
        };
        foreach (string scene in scenes)
        {
            if (!File.Exists(scene) || !EditorBuildSettings.scenes.Any(item => item.enabled && item.path == scene))
            {
                throw new BuildFailedException("iOS export requires an enabled scene: " + scene);
            }
        }

        if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS))
        {
            throw new BuildFailedException("Could not switch to the iOS build target.");
        }
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, "com.kailius.gauntlet11");
        PlayerSettings.productName = "Kailius";
        PlayerSettings.bundleVersion = "2";
        if (!int.TryParse(PlayerSettings.iOS.buildNumber, out int previousBuildNumber) || previousBuildNumber < 0)
        {
            throw new BuildFailedException("The iOS build number must be a non-negative integer.");
        }
        PlayerSettings.iOS.buildNumber = Math.Max(2, checked(previousBuildNumber + 1)).ToString();
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.iOS, ScriptingImplementation.IL2CPP);
        PlayerSettings.iOS.targetDevice = iOSTargetDevice.iPhoneAndiPad;
        PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
        PlayerSettings.iOS.targetOSVersionString = "15.0";
        PlayerSettings.defaultInterfaceOrientation = UIOrientation.AutoRotation;
        PlayerSettings.allowedAutorotateToPortrait = false;
        PlayerSettings.allowedAutorotateToPortraitUpsideDown = false;
        PlayerSettings.allowedAutorotateToLandscapeLeft = true;
        PlayerSettings.allowedAutorotateToLandscapeRight = true;
        PlayerSettings.SetUseDefaultGraphicsAPIs(BuildTarget.iOS, false);
        PlayerSettings.SetGraphicsAPIs(BuildTarget.iOS, new[] { GraphicsDeviceType.Metal });

        const string outputPath = "Builds/iOS/Kailius";
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outputPath,
            target = BuildTarget.iOS,
            targetGroup = BuildTargetGroup.iOS,
            options = BuildOptions.CleanBuildCache,
            extraScriptingDefines = new[] { "KAILIUS_IOS_TESTER" }
        });

        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new Exception("iOS build failed: " + report.summary.result);
        }
    }
}
