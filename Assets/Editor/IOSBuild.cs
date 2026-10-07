using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering;

public static class CodexIOSBuild
{
    [MenuItem("Build/Export iOS Development Xcode Project")]
    public static void BuildForDevice()
    {
        string[] scenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => scene.path)
            .OrderBy(path => path == "Assets/Scenes/Menu.unity" ? 0 :
                path == "Assets/Scenes/1_1Gauntlet.unity" ? 1 : 2)
            .ToArray();

        if (scenes.Length < 2 || scenes[0] != "Assets/Scenes/Menu.unity" ||
            scenes[1] != "Assets/Scenes/1_1Gauntlet.unity")
        {
            throw new Exception("iOS build stopped: Menu and 1_1Gauntlet must be enabled as the first two scenes.");
        }

        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS);
        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.iOS, "com.kailius.gauntlet11");
        PlayerSettings.productName = "Gauntlet 1.1";
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

        const string outputPath = "Builds/iOS/Gauntlet";
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath));
        BuildReport report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outputPath,
            target = BuildTarget.iOS,
            targetGroup = BuildTargetGroup.iOS,
            options = BuildOptions.Development | BuildOptions.CleanBuildCache
        });

        if (report.summary.result != BuildResult.Succeeded)
        {
            throw new Exception("iOS build failed: " + report.summary.result);
        }
    }
}
