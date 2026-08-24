using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;

public static class CodexAndroidBuild
{
    public static void InspectGauntletMobileControls()
    {
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/1_1Gauntlet.unity");
        foreach (var move in UnityEngine.Resources.FindObjectsOfTypeAll<MoveByTouch>())
        {
            if (!move.gameObject.scene.IsValid())
            {
                continue;
            }

            UnityEngine.Debug.Log("MoveByTouch " + GetPath(move.transform)
                + " active=" + move.gameObject.activeInHierarchy
                + " controls=" + RefName(move.ControlesMoviles)
                + " joystick=" + RefName(move.joystick)
                + " menu=" + RefName(move.menu)
                + " stats=" + RefName(move.stats)
                + " row=" + RefName(move.row)
                + " dust=" + RefName(move.dust));
        }

        foreach (var canvas in UnityEngine.Object.FindObjectsByType<UnityEngine.Canvas>())
        {
            UnityEngine.Debug.Log("Canvas " + GetPath(canvas.transform)
                + " active=" + canvas.gameObject.activeInHierarchy
                + " enabled=" + canvas.enabled
                + " renderMode=" + canvas.renderMode
                + " scale=" + canvas.transform.localScale);
        }

        foreach (var go in UnityEngine.Resources.FindObjectsOfTypeAll<UnityEngine.GameObject>())
        {
            if (go.scene.IsValid() && go.name.Contains("ControllesMobiles"))
            {
                UnityEngine.Debug.Log("Controls object " + GetPath(go.transform)
                    + " activeSelf=" + go.activeSelf
                    + " activeInHierarchy=" + go.activeInHierarchy);
            }
        }
    }

    public static void InspectGauntletPlayerObjects()
    {
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/1_1Gauntlet.unity");
        foreach (var go in UnityEngine.Resources.FindObjectsOfTypeAll<UnityEngine.GameObject>())
        {
            if (!go.scene.IsValid())
            {
                continue;
            }

            if (go.CompareTag("Player") || go.name.ToLowerInvariant().Contains("player") || go.GetComponent<PlayerController>() != null || go.GetComponent<PlayerControllerUP>() != null || go.GetComponent<PlayerCombat>() != null)
            {
                UnityEngine.Debug.Log("Player-ish " + GetPath(go.transform)
                    + " tag=" + go.tag
                    + " active=" + go.activeInHierarchy
                    + " PlayerController=" + (go.GetComponent<PlayerController>() != null)
                    + " PlayerControllerUP=" + (go.GetComponent<PlayerControllerUP>() != null)
                    + " MoveByTouch=" + (go.GetComponent<MoveByTouch>() != null)
                    + " PlayerCombat=" + (go.GetComponent<PlayerCombat>() != null)
                    + " Bow=" + (go.GetComponent<Bow>() != null)
                    + " Rigidbody2D=" + (go.GetComponent<UnityEngine.Rigidbody2D>() != null));
            }
        }
    }

    public static void InspectScene1MobileControls()
    {
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/Scene_1.unity");
        InspectMobileControlsInOpenScene();
    }

    public static void CompareMobileControlScenes()
    {
        DumpMobileScene("Assets/Scenes/Scene_1.unity", "ORIGINAL");
        DumpMobileScene("Assets/Scenes/1_1Gauntlet.unity", "GAUNTLET");
    }

    public static void InspectGauntletMissingReferences()
    {
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/1_1Gauntlet.unity");
        foreach (var go in UnityEngine.Resources.FindObjectsOfTypeAll<UnityEngine.GameObject>()
            .Where(go => go.scene.IsValid())
            .OrderBy(go => GetPath(go.transform)))
        {
            var components = go.GetComponents<UnityEngine.Component>();
            for (int i = 0; i < components.Length; i++)
            {
                if (components[i] == null)
                {
                    UnityEngine.Debug.Log("MISSING_SCRIPT " + GetPath(go.transform) + " index=" + i);
                }
            }
        }

        foreach (var behaviour in UnityEngine.Resources.FindObjectsOfTypeAll<UnityEngine.MonoBehaviour>()
            .Where(b => b != null && b.gameObject.scene.IsValid())
            .OrderBy(b => GetPath(b.transform)))
        {
            var type = behaviour.GetType();
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
            {
                if (!typeof(UnityEngine.Object).IsAssignableFrom(field.FieldType))
                {
                    continue;
                }

                var value = field.GetValue(behaviour) as UnityEngine.Object;
                if (value == null)
                {
                    UnityEngine.Debug.Log("MISSING_REF " + GetPath(behaviour.transform)
                        + " component=" + type.Name
                        + " field=" + field.Name);
                }
            }
        }
    }

    public static void CleanGauntletDiagnostics()
    {
        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/1_1Gauntlet.unity");
        foreach (var go in UnityEngine.Resources.FindObjectsOfTypeAll<UnityEngine.GameObject>().Where(go => go.scene.IsValid()).ToArray())
        {
            if (go.name == "CodexSceneStartupLog")
            {
                UnityEngine.Object.DestroyImmediate(go);
                UnityEngine.Debug.Log("Removed CodexSceneStartupLog");
            }
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
    }

    private static void DumpMobileScene(string scenePath, string label)
    {
        UnityEditor.SceneManagement.EditorSceneManager.OpenScene(scenePath);
        UnityEngine.Debug.Log("=== " + label + " " + scenePath + " ===");

        foreach (var player in UnityEngine.Resources.FindObjectsOfTypeAll<UnityEngine.GameObject>()
            .Where(go => go.scene.IsValid())
            .Where(go => go.CompareTag("Player") || go.name == "Player" || go.GetComponent<PlayerController>() != null || go.GetComponent<MoveByTouch>() != null)
            .OrderBy(go => GetPath(go.transform)))
        {
            UnityEngine.Debug.Log("PLAYER " + GetPath(player.transform)
                + " activeSelf=" + player.activeSelf
                + " activeHierarchy=" + player.activeInHierarchy
                + " tag=" + player.tag
                + " layer=" + player.layer);
            DumpComponent<PlayerController>(player);
            DumpComponent<PlayerControllerUP>(player);
            DumpComponent<MoveByTouch>(player);
            DumpComponent<PlayerCombat>(player);
            DumpComponent<Bow>(player);
            DumpComponent<UnityEngine.Rigidbody2D>(player);
            foreach (UnityEngine.Transform child in player.transform)
            {
                UnityEngine.Debug.Log("PLAYER_CHILD " + GetPath(child)
                    + " activeSelf=" + child.gameObject.activeSelf
                    + " comps=" + string.Join(",", child.GetComponents<UnityEngine.Component>().Select(c => c == null ? "NULL" : c.GetType().Name).ToArray()));
            }
        }

        foreach (var move in UnityEngine.Resources.FindObjectsOfTypeAll<MoveByTouch>().Where(c => c.gameObject.scene.IsValid()))
        {
            UnityEngine.Debug.Log("MOVEBYTOUCH " + GetPath(move.transform)
                + " enabled=" + move.enabled
                + " speed=" + move.moveSpeed
                + " jump=" + move.jumpHeight
                + " controls=" + RefPath(move.ControlesMoviles)
                + " controlsActive=" + (move.ControlesMoviles != null && move.ControlesMoviles.activeInHierarchy)
                + " joystick=" + RefPath(move.joystick)
                + " menu=" + RefPath(move.menu)
                + " stats=" + RefPath(move.stats)
                + " row=" + RefPath(move.row)
                + " dust=" + RefPath(move.dust));
        }

        foreach (var controls in UnityEngine.Resources.FindObjectsOfTypeAll<UnityEngine.GameObject>()
            .Where(go => go.scene.IsValid() && (go.name.Contains("Controlles") || go.name.Contains("Joystick") || go.GetComponent<ButtonJump>() != null || go.GetComponent<ButtonAttack>() != null || go.GetComponent<ButtonBow>() != null))
            .OrderBy(go => GetPath(go.transform)))
        {
            UnityEngine.Debug.Log("CONTROL_GO " + GetPath(controls.transform)
                + " activeSelf=" + controls.activeSelf
                + " activeHierarchy=" + controls.activeInHierarchy
                + " comps=" + string.Join(",", controls.GetComponents<UnityEngine.Component>().Select(c => c == null ? "NULL" : c.GetType().Name).ToArray()));
            var jump = controls.GetComponent<ButtonJump>();
            if (jump != null) UnityEngine.Debug.Log("BUTTON_JUMP " + GetPath(controls.transform) + " enabled=" + jump.enabled + " player=" + RefPath(jump.player));
            var attack = controls.GetComponent<ButtonAttack>();
            if (attack != null) UnityEngine.Debug.Log("BUTTON_ATTACK " + GetPath(controls.transform) + " enabled=" + attack.enabled + " player=" + RefPath(attack.player));
            var bow = controls.GetComponent<ButtonBow>();
            if (bow != null) UnityEngine.Debug.Log("BUTTON_BOW " + GetPath(controls.transform) + " enabled=" + bow.enabled + " player=" + RefPath(bow.player));
            var joystick = controls.GetComponent<Joystick>();
            if (joystick != null) UnityEngine.Debug.Log("JOYSTICK " + GetPath(controls.transform) + " enabled=" + joystick.enabled);
        }

        foreach (var eventSystem in UnityEngine.Resources.FindObjectsOfTypeAll<UnityEngine.EventSystems.EventSystem>().Where(c => c.gameObject.scene.IsValid()))
        {
            UnityEngine.Debug.Log("EVENTSYSTEM " + GetPath(eventSystem.transform)
                + " active=" + eventSystem.gameObject.activeInHierarchy
                + " inputModule=" + string.Join(",", eventSystem.GetComponents<UnityEngine.Component>().Select(c => c == null ? "NULL" : c.GetType().Name).ToArray()));
        }
    }

    private static void DumpComponent<T>(UnityEngine.GameObject go) where T : UnityEngine.Component
    {
        var component = go.GetComponent<T>();
        if (component != null)
        {
            UnityEngine.Debug.Log("COMP " + GetPath(go.transform) + " " + typeof(T).Name + " enabled=" + ComponentEnabled(component));
        }
    }

    private static string ComponentEnabled(UnityEngine.Component component)
    {
        var behaviour = component as UnityEngine.Behaviour;
        return behaviour == null ? "n/a" : behaviour.enabled.ToString();
    }

    private static void InspectMobileControlsInOpenScene()
    {
        foreach (var move in UnityEngine.Resources.FindObjectsOfTypeAll<MoveByTouch>())
        {
            if (!move.gameObject.scene.IsValid())
            {
                continue;
            }

            UnityEngine.Debug.Log("MoveByTouch " + GetPath(move.transform)
                + " active=" + move.gameObject.activeInHierarchy
                + " controls=" + RefPath(move.ControlesMoviles)
                + " joystick=" + RefPath(move.joystick)
                + " menu=" + RefPath(move.menu)
                + " stats=" + RefPath(move.stats)
                + " row=" + RefPath(move.row)
                + " dust=" + RefPath(move.dust));
        }
    }

    public static void EnableGauntletMobileControls()
    {
        var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene("Assets/Scenes/1_1Gauntlet.unity");
        var player = FindSceneObject("Player");
        var controls = FindSceneObject("ControllesMobiles");
        var menu = FindSceneObject("MenuIn-Game");
        var stats = FindSceneObject("Estadisticas");
        var row = FindChild(player, "RowPoint");
        var dust = FindChild(player, "DustPS");
        var joystick = controls == null ? null : controls.GetComponentInChildren<Joystick>(true);

        foreach (var go in UnityEngine.Resources.FindObjectsOfTypeAll<UnityEngine.GameObject>())
        {
            if (go.scene.IsValid() && go.name.Contains("ControllesMobiles"))
            {
                go.SetActive(true);
                UnityEngine.Debug.Log("Enabled " + GetPath(go.transform));
            }
        }

        if (player == null)
        {
            throw new Exception("Could not find Player in 1_1Gauntlet.");
        }

        var move = player.GetComponent<MoveByTouch>();
        if (move == null)
        {
            move = player.AddComponent<MoveByTouch>();
        }

        move.moveSpeed = 10f;
        move.jumpHeight = 22f;
        move.row = row;
        move.menu = menu;
        move.stats = stats;
        move.ControlesMoviles = controls;
        move.joystick = joystick;
        move.dust = dust == null ? null : dust.GetComponent<UnityEngine.ParticleSystem>();

        var combat = player.GetComponent<PlayerCombat>();
        var bow = player.GetComponent<Bow>();
        foreach (var jumpButton in UnityEngine.Resources.FindObjectsOfTypeAll<ButtonJump>())
        {
            if (jumpButton.gameObject.scene.IsValid())
            {
                jumpButton.player = move;
            }
        }

        foreach (var attackButton in UnityEngine.Resources.FindObjectsOfTypeAll<ButtonAttack>())
        {
            if (attackButton.gameObject.scene.IsValid())
            {
                attackButton.player = combat;
            }
        }

        foreach (var bowButton in UnityEngine.Resources.FindObjectsOfTypeAll<ButtonBow>())
        {
            if (bowButton.gameObject.scene.IsValid())
            {
                bowButton.player = bow;
            }
        }

        if (!UnityEngine.Resources.FindObjectsOfTypeAll<UnityEngine.EventSystems.EventSystem>().Any(es => es.gameObject.scene.IsValid()))
        {
            var eventSystem = new UnityEngine.GameObject("EventSystem");
            eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
            eventSystem.AddComponent<UnityEngine.EventSystems.StandaloneInputModule>();
            UnityEngine.Debug.Log("Created EventSystem");
        }

        UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
        UnityEditor.SceneManagement.EditorSceneManager.SaveScene(scene);
    }

    public static void BuildGauntlet11()
    {
        const string outputDir = "Builds/Android";
        const string outputPath = outputDir + "/Gauntlet1_1.apk";
        string[] scenes =
        {
            "Assets/Scenes/Menu.unity",
            "Assets/Scenes/1_1Gauntlet.unity",
            "Assets/Scenes/Scene_1.unity",
            "Assets/Scenes/Scene_2.unity",
            "Assets/Scenes/Scene_3.unity",
            "Assets/Scenes/Scene_Final_1.unity"
        };

        Directory.CreateDirectory(outputDir);

        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;

        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.kailius.gauntlet11");
        PlayerSettings.productName = "Gauntlet 1.1";
        PlayerSettings.Android.useCustomKeystore = false;
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outputPath,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.None
        };

        var report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            throw new Exception("Android build failed: " + report.summary.result);
        }
    }

    public static void BuildScene1Only()
    {
        const string outputDir = "Builds/Android";
        const string outputPath = outputDir + "/Scene1Only.apk";
        string[] scenes =
        {
            "Assets/Scenes/Scene_1.unity"
        };

        Directory.CreateDirectory(outputDir);

        EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android);
        EditorUserBuildSettings.androidBuildSystem = AndroidBuildSystem.Gradle;

        PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.kailius.gauntlet11");
        PlayerSettings.productName = "Kailius Scene 1 Test";
        PlayerSettings.Android.useCustomKeystore = false;
        PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
        PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
        PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;

        var options = new BuildPlayerOptions
        {
            scenes = scenes,
            locationPathName = outputPath,
            target = BuildTarget.Android,
            targetGroup = BuildTargetGroup.Android,
            options = BuildOptions.None
        };

        var report = BuildPipeline.BuildPlayer(options);
        if (report.summary.result != UnityEditor.Build.Reporting.BuildResult.Succeeded)
        {
            throw new Exception("Scene 1 Android build failed: " + report.summary.result);
        }
    }

    private static string RefName(UnityEngine.Object obj)
    {
        return obj == null ? "NULL" : obj.name;
    }

    private static string RefPath(UnityEngine.Object obj)
    {
        if (obj == null)
        {
            return "NULL";
        }

        var component = obj as UnityEngine.Component;
        if (component != null)
        {
            return GetPath(component.transform);
        }

        var go = obj as UnityEngine.GameObject;
        if (go != null)
        {
            return GetPath(go.transform);
        }

        return obj.name;
    }

    private static string GetPath(UnityEngine.Transform transform)
    {
        var path = transform.name;
        while (transform.parent != null)
        {
            transform = transform.parent;
            path = transform.name + "/" + path;
        }
        return path;
    }

    private static UnityEngine.GameObject FindSceneObject(string name)
    {
        foreach (var go in UnityEngine.Resources.FindObjectsOfTypeAll<UnityEngine.GameObject>())
        {
            if (go.scene.IsValid() && go.name == name)
            {
                return go;
            }
        }

        return null;
    }

    private static UnityEngine.GameObject FindChild(UnityEngine.GameObject parent, string name)
    {
        if (parent == null)
        {
            return null;
        }

        foreach (var transform in parent.GetComponentsInChildren<UnityEngine.Transform>(true))
        {
            if (transform.name == name)
            {
                return transform.gameObject;
            }
        }

        return null;
    }
}
