using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.Android;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Process = System.Diagnostics.Process;
using ProcessStartInfo = System.Diagnostics.ProcessStartInfo;

public static class CodexAndroidBuild
{
    private const int DeviceDetectionTimeoutMilliseconds = 15000;
    private const int ApkInstallationTimeoutMilliseconds = 120000;
    private const int AppLaunchTimeoutMilliseconds = 30000;
    private const int ProcessShutdownTimeoutMilliseconds = 5000;

    private static bool emulatorDeploymentInProgress;
    private static Task emulatorDeploymentTask;
    private static volatile string emulatorDeploymentStage;
    private static string lastReportedEmulatorDeploymentStage;

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
            "Assets/Scenes/1_3Gauntlet.unity",
            "Assets/Scenes/Scene_2.unity",
            "Assets/Scenes/2_1Gauntlet.unity",
            "Assets/Scenes/2_2Gauntlet.unity",
            "Assets/Scenes/2_3Gauntlet.unity"
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
        if (emulatorDeploymentInProgress)
        {
            Debug.LogError("Emulator deployment is already running. Wait for it to finish before starting another one.");
            return;
        }

        emulatorDeploymentInProgress = true;
        try
        {
            BuildScene1Only();
        }
        catch (Exception exception)
        {
            emulatorDeploymentInProgress = false;
            Debug.LogError("Emulator deployment failed during Android APK build.\n" + exception);
            return;
        }

        try
        {
            string projectPath = Path.GetFullPath(Path.Combine(Application.dataPath, ".."));
            string apkPath = Path.Combine(projectPath, "Builds", "Android", "Scene1Only.apk");
            if (!File.Exists(apkPath))
            {
                throw new Exception("The Android build succeeded but the APK was not found at " + apkPath + ".");
            }

            string sdkPath = AndroidExternalToolsSettings.sdkRootPath;
            if (string.IsNullOrWhiteSpace(sdkPath))
            {
                sdkPath = Path.Combine(EditorApplication.applicationContentsPath, "PlaybackEngines", "AndroidPlayer", "SDK");
            }

            string adbPath = Path.Combine(sdkPath, "platform-tools", "adb.exe");
            if (!File.Exists(adbPath))
            {
                throw new Exception("ADB was not found in the Android SDK selected in Unity External Tools at " + adbPath + ".");
            }

            emulatorDeploymentStage = "Checking for a running Android emulator";
            lastReportedEmulatorDeploymentStage = null;
            Debug.Log("Android APK build succeeded. Emulator deployment is running in the background.");
            emulatorDeploymentTask = Task.Run(() => DeployToEmulator(adbPath, apkPath));
            EditorApplication.update += PollEmulatorDeployment;
        }
        catch (Exception exception)
        {
            emulatorDeploymentTask = null;
            emulatorDeploymentInProgress = false;
            Debug.LogError("Emulator deployment failed during setup.\n" + exception);
        }
    }

    private static void DeployToEmulator(string adbPath, string apkPath)
    {
        AdbCommandResult deviceResult = RunAdb(
            adbPath,
            "devices",
            "device detection",
            DeviceDetectionTimeoutMilliseconds);

        string[] devices = deviceResult.StandardOutput.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
        string emulator = devices
            .Skip(1)
            .Select(line => line.Split(new[] { '\t', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            .FirstOrDefault(parts => parts.Length > 1 && parts[0].StartsWith("emulator-") && parts[1] == "device")?[0];

        if (emulator == null)
        {
            throw new EmulatorDeploymentException(
                "device detection",
                "No running, authorized Android emulator was found.\n" + FormatAdbOutput(deviceResult.StandardOutput, deviceResult.StandardError));
        }

        emulatorDeploymentStage = "Installing the APK on " + emulator;
        RunAdb(
            adbPath,
            "-s " + QuoteArgument(emulator) + " install -r " + QuoteArgument(apkPath),
            "APK installation",
            ApkInstallationTimeoutMilliseconds);

        emulatorDeploymentStage = "Launching Kailius on " + emulator;
        RunAdb(
            adbPath,
            "-s " + QuoteArgument(emulator) + " shell monkey -p com.kailius.gauntlet11 1",
            "app launch",
            AppLaunchTimeoutMilliseconds);
    }

    private static AdbCommandResult RunAdb(string adbPath, string arguments, string stage, int timeoutMilliseconds)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = adbPath,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };

        using (var process = new Process { StartInfo = startInfo })
        {
            try
            {
                if (!process.Start())
                {
                    throw new EmulatorDeploymentException(stage, "ADB did not start.");
                }
            }
            catch (EmulatorDeploymentException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new EmulatorDeploymentException(stage, "ADB could not start.\n" + exception.Message);
            }

            Task<string> standardOutputTask = process.StandardOutput.ReadToEndAsync();
            Task<string> standardErrorTask = process.StandardError.ReadToEndAsync();

            if (!process.WaitForExit(timeoutMilliseconds))
            {
                try
                {
                    process.Kill();
                }
                catch (Exception)
                {
                }

                bool stopped = process.WaitForExit(ProcessShutdownTimeoutMilliseconds);
                string timeoutStandardOutput = TryReadStream(standardOutputTask);
                string timeoutStandardError = TryReadStream(standardErrorTask);
                string detail = "ADB timed out after " + timeoutMilliseconds + " ms.";
                if (!stopped)
                {
                    detail += " The ADB process did not exit after it was stopped.";
                }

                string timeoutOutput = FormatAdbOutput(timeoutStandardOutput, timeoutStandardError);
                if (timeoutOutput != "ADB returned no output.")
                {
                    detail += "\nADB output:\n" + timeoutOutput;
                }

                throw new EmulatorDeploymentException(stage, detail);
            }

            string output = ReadStream(standardOutputTask, stage);
            string error = ReadStream(standardErrorTask, stage);
            if (process.ExitCode != 0)
            {
                throw new EmulatorDeploymentException(
                    stage,
                    "ADB exited with code " + process.ExitCode + ".\n" + FormatAdbOutput(output, error));
            }

            return new AdbCommandResult(output, error);
        }
    }

    private static string ReadStream(Task<string> streamTask, string stage)
    {
        try
        {
            if (!streamTask.Wait(ProcessShutdownTimeoutMilliseconds))
            {
                throw new EmulatorDeploymentException(stage, "An ADB output stream did not close after the process exited.");
            }

            return streamTask.GetAwaiter().GetResult();
        }
        catch (EmulatorDeploymentException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new EmulatorDeploymentException(stage, "Could not read ADB output.\n" + exception.GetBaseException().Message);
        }
    }

    private static string TryReadStream(Task<string> streamTask)
    {
        try
        {
            if (!streamTask.Wait(ProcessShutdownTimeoutMilliseconds))
            {
                return "ADB output stream did not close before the timeout.\n";
            }

            return streamTask.GetAwaiter().GetResult();
        }
        catch (Exception exception)
        {
            return "ADB output could not be read: " + exception.GetBaseException().Message + "\n";
        }
    }

    private static string FormatAdbOutput(string standardOutput, string standardError)
    {
        string output = string.IsNullOrWhiteSpace(standardOutput) ? string.Empty : "stdout:\n" + standardOutput.Trim();
        string error = string.IsNullOrWhiteSpace(standardError) ? string.Empty : "stderr:\n" + standardError.Trim();
        return string.IsNullOrEmpty(output) && string.IsNullOrEmpty(error)
            ? "ADB returned no output."
            : string.Join("\n", new[] { output, error }.Where(part => !string.IsNullOrEmpty(part)));
    }

    private static string QuoteArgument(string value)
    {
        return "\"" + value.Replace("\"", "\\\"") + "\"";
    }

    private static void PollEmulatorDeployment()
    {
        if (emulatorDeploymentTask == null)
        {
            EditorApplication.update -= PollEmulatorDeployment;
            return;
        }

        if (!string.Equals(emulatorDeploymentStage, lastReportedEmulatorDeploymentStage, StringComparison.Ordinal))
        {
            lastReportedEmulatorDeploymentStage = emulatorDeploymentStage;
            Debug.Log("Emulator deployment: " + emulatorDeploymentStage + ".");
        }

        if (!emulatorDeploymentTask.IsCompleted)
        {
            return;
        }

        EditorApplication.update -= PollEmulatorDeployment;
        Task completedTask = emulatorDeploymentTask;
        emulatorDeploymentTask = null;
        emulatorDeploymentInProgress = false;

        if (completedTask.IsFaulted)
        {
            Exception exception = completedTask.Exception == null
                ? new Exception("The emulator deployment failed without an error message.")
                : completedTask.Exception.GetBaseException();
            Debug.LogError(exception.Message);
        }
        else if (completedTask.IsCanceled)
        {
            Debug.LogError("Emulator deployment was canceled.");
        }
        else
        {
            Debug.Log("Kailius was installed and launched on the Android emulator.");
        }
    }

    private sealed class AdbCommandResult
    {
        public readonly string StandardOutput;
        public readonly string StandardError;

        public AdbCommandResult(string standardOutput, string standardError)
        {
            StandardOutput = standardOutput;
            StandardError = standardError;
        }
    }

    private sealed class EmulatorDeploymentException : Exception
    {
        public EmulatorDeploymentException(string stage, string detail)
            : base("Emulator deployment failed during " + stage + ".\n" + detail)
        {
        }
    }
}
