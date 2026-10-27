using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public sealed class AndroidMobileControlsBuildProcessor : IProcessSceneWithReport
{
    public int callbackOrder => 0;

    public void OnProcessScene(Scene scene, BuildReport report)
    {
        if (report == null) return;
        bool isAndroid = report.summary.platform == BuildTarget.Android;
        bool isIos = report.summary.platform == BuildTarget.iOS;
        if (!isAndroid && !isIos)
        {
            return;
        }

        foreach (var canvas in scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Canvas>(true)))
        {
            if (isIos && canvas.isRootCanvas && canvas.renderMode != RenderMode.WorldSpace &&
                canvas.GetComponent<MobileSafeArea>() == null)
            {
                canvas.gameObject.AddComponent<MobileSafeArea>();
            }

            if (canvas.isRootCanvas &&
                (scene.name == "Menu" || canvas.name == "MenuIn-Game"))
            {
                var legacy = canvas.GetComponent<MobilePrivacyControls>();
                if (legacy != null) UnityEngine.Object.DestroyImmediate(legacy);
                var instances = canvas.GetComponentsInChildren<MobilePrivacyControls>(true);
                if (instances.Length > 1) throw new BuildFailedException("Menu canvas contains duplicate privacy controls.");
                var privacy = instances.SingleOrDefault();
                if (privacy == null)
                {
                    var prefab = AssetDatabase.LoadAssetAtPath<MobilePrivacyControls>("Assets/Prefabs/UI/PrivacyControls.prefab");
                    if (prefab == null || !prefab.HasRequiredReferences)
                        throw new BuildFailedException("PrivacyControls prefab is missing or has incomplete UI references.");
                    privacy = UnityEngine.Object.Instantiate(prefab, canvas.transform, false);
                    privacy.name = "PrivacyControls";
                }
                if (!privacy.HasRequiredReferences)
                    throw new BuildFailedException("Privacy controls have incomplete UI references.");
            }
        }

        if (isIos)
        {
            foreach (var button in scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Button>(true)))
            {
                for (int index = 0; index < button.onClick.GetPersistentEventCount(); ++index)
                {
                    if (button.onClick.GetPersistentMethodName(index) == "QuitGame")
                    {
                        button.gameObject.SetActive(false);
                        break;
                    }
                }
            }
        }

        if (scene.name == "Menu")
        {
            return;
        }

        if (isAndroid && scene.name == "Scene_1" &&
            System.IO.Path.GetFileName(report.summary.outputPath) == "Scene1Only.apk")
        {
            var exits = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Portal>(true))
                .Where(portal => portal.nextSceneName == "Scene_2")
                .ToArray();
            if (exits.Length != 1)
            {
                throw new BuildFailedException("Scene 1 playtest requires exactly one Scene 2 exit portal.");
            }
            exits[0].nextSceneName = "Menu";
            Debug.Log("SCENE 1 PLAYTEST: Scene 2 exit redirected to Menu in build data.");
        }

        try
        {
            ConfigureAndValidate(scene, isAndroid);
        }
        catch (BuildFailedException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new BuildFailedException("Mobile controls setup failed in scene '" + scene.name + "': " + exception.Message);
        }
    }

    private static void ConfigureAndValidate(Scene scene, bool isAndroid)
    {
        var sceneName = scene.name;
        var player = FindUniqueObject(scene, "Player");
        var controls = FindUniqueObject(scene, "ControllesMobiles");

        if (!player.activeInHierarchy)
        {
            Fail(sceneName, "Player is inactive.");
        }

        controls.SetActive(true);
        if (!controls.activeInHierarchy)
        {
            Fail(sceneName, "ControllesMobiles is inactive after setup.");
        }

        var move = player.GetComponent<MoveByTouch>();
        if (move == null)
        {
            Fail(sceneName, "Player is missing its configured MoveByTouch component; movement settings cannot be inferred safely.");
        }

        move.enabled = true;
        var joystick = RequireSingle(controls.GetComponentsInChildren<Joystick>(true), sceneName, "Joystick");
        ActivatePath(controls.transform, joystick.gameObject);
        joystick.enabled = true;
        if (!joystick.gameObject.activeInHierarchy)
        {
            Fail(sceneName, "Joystick is inactive after setup.");
        }

        move.ControlesMoviles = controls;
        move.joystick = joystick;
        move.row = FindUniqueChild(player, "RowPoint", sceneName);
        move.menu = FindUniqueObject(scene, "MenuIn-Game");
        if (move.stats == null)
        {
            var hudObjects = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
                .Where(transform => transform.name == "Estadisticas" || transform.name == "OpenWorldHud")
                .Select(transform => transform.gameObject)
                .ToArray();
            move.stats = RequireSingle(hudObjects.Select(hud => hud.transform).ToArray(), sceneName, "stats HUD").gameObject;
        }
        else if (move.stats.scene != scene)
        {
            Fail(sceneName, "MoveByTouch.stats must reference a HUD object in the same scene.");
        }
        var dustObject = FindUniqueChild(player, "DustPS", sceneName);
        move.dust = dustObject.GetComponent<ParticleSystem>();
        if (move.dust == null)
        {
            Fail(sceneName, "Player/DustPS is missing its ParticleSystem component.");
        }

        var combat = player.GetComponent<PlayerCombat>();
        if (combat == null || !combat.enabled)
        {
            Fail(sceneName, "Player is missing an enabled PlayerCombat component.");
        }

        var canvases = controls.GetComponentsInChildren<Canvas>(true);
        if (canvases.Length == 0)
        {
            Fail(sceneName, "ControllesMobiles has no Canvas.");
        }

        foreach (var canvas in canvases)
        {
            ActivatePath(controls.transform, canvas.gameObject);
            canvas.enabled = true;
            var raycaster = canvas.GetComponent<GraphicRaycaster>();
            if (raycaster == null)
            {
                Fail(sceneName, "Canvas '" + GetPath(controls.transform, canvas.transform) + "' is missing a GraphicRaycaster.");
            }

            raycaster.enabled = true;
            if (!canvas.gameObject.activeInHierarchy)
            {
                Fail(sceneName, "Canvas '" + GetPath(controls.transform, canvas.transform) + "' is inactive after setup.");
            }
        }

        var jumpButton = RequireSingle(controls.GetComponentsInChildren<ButtonJump>(true), sceneName, "ButtonJump");
        jumpButton.player = move;
        ConfigureActionButton(controls.transform, jumpButton, sceneName, "ButtonJump");

        var attackButton = RequireSingle(controls.GetComponentsInChildren<ButtonAttack>(true), sceneName, "ButtonAttack");
        attackButton.player = combat;
        ConfigureActionButton(controls.transform, attackButton, sceneName, "ButtonAttack");

        var eventSystem = RequireAtMostOne(scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<EventSystem>(true)).ToArray(), sceneName, "EventSystem");
        if (eventSystem == null)
        {
            var eventSystemObject = new GameObject("EventSystem");
            SceneManager.MoveGameObjectToScene(eventSystemObject, scene);
            eventSystem = eventSystemObject.AddComponent<EventSystem>();
        }

        eventSystem.gameObject.SetActive(true);
        eventSystem.enabled = true;
        var inputModule = eventSystem.GetComponents<BaseInputModule>().FirstOrDefault(module => module.enabled);
        if (inputModule == null)
        {
            inputModule = eventSystem.gameObject.AddComponent<StandaloneInputModule>();
        }

        inputModule.gameObject.SetActive(true);
        inputModule.enabled = true;
        if (!eventSystem.gameObject.activeInHierarchy || !inputModule.gameObject.activeInHierarchy)
        {
            Fail(sceneName, "EventSystem or its input module is inactive after setup.");
        }

        Debug.Log((isAndroid ? "ANDROID" : "IOS") + " BUILD PREFLIGHT: " + sceneName +
            " mobile controls repaired in build data and input checks passed.");
    }

    private static void ConfigureActionButton<T>(Transform controlsRoot, T handler, string sceneName, string label)
        where T : Behaviour
    {
        ActivatePath(controlsRoot, handler.gameObject);
        handler.enabled = true;
        var button = handler.GetComponent<Button>();
        if (button == null)
        {
            Fail(sceneName, label + " is missing its Unity UI Button component.");
        }

        button.enabled = true;
        button.interactable = true;
        if (!handler.gameObject.activeInHierarchy ||
            button.targetGraphic == null || !button.targetGraphic.raycastTarget)
        {
            Fail(sceneName, label + " cannot receive pointer input after setup.");
        }
    }

    private static GameObject FindUniqueObject(Scene scene, string objectName)
    {
        var matches = scene.GetRootGameObjects()
            .SelectMany(root => root.GetComponentsInChildren<Transform>(true))
            .Where(transform => transform.name == objectName)
            .Select(transform => transform.gameObject)
            .ToArray();

        if (matches.Length != 1)
        {
            Fail(scene.name, "Expected exactly one '" + objectName + "' object, found " + matches.Length + ".");
        }

        return matches[0];
    }

    private static GameObject FindUniqueChild(GameObject parent, string childName, string sceneName)
    {
        var matches = parent.GetComponentsInChildren<Transform>(true)
            .Where(transform => transform != parent.transform && transform.name == childName)
            .Select(transform => transform.gameObject)
            .ToArray();

        if (matches.Length != 1)
        {
            Fail(sceneName, "Expected exactly one Player/" + childName + " object, found " + matches.Length + ".");
        }

        return matches[0];
    }

    private static T RequireSingle<T>(T[] components, string sceneName, string label) where T : Component
    {
        if (components.Length != 1)
        {
            Fail(sceneName, "Expected exactly one " + label + " component, found " + components.Length + ".");
        }

        return components[0];
    }

    private static T RequireAtMostOne<T>(T[] components, string sceneName, string label) where T : Component
    {
        if (components.Length > 1)
        {
            Fail(sceneName, "Expected at most one " + label + " component, found " + components.Length + ".");
        }

        return components.Length == 0 ? null : components[0];
    }

    private static void ActivatePath(Transform root, GameObject target)
    {
        var current = target.transform;
        while (current != null)
        {
            current.gameObject.SetActive(true);
            if (current == root)
            {
                return;
            }

            current = current.parent;
        }
    }

    private static string GetPath(Transform root, Transform target)
    {
        var path = target.name;
        while (target.parent != null && target.parent != root.parent)
        {
            target = target.parent;
            path = target.name + "/" + path;
        }

        return path;
    }

    private static void Fail(string sceneName, string message)
    {
        throw new BuildFailedException("Mobile build stopped in scene '" + sceneName + "': " + message);
    }
}
