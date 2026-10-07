using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

public sealed class DeathWallDirectionTests
{
    private GameObject playerObject;
    private GameObject cameraObject;
    private GameObject wallObject;
    private GameObject triggerObject;
    private GameObject checkpointObject;
    private PlayerController player;
    private Stats stats;
    private CameraController cameraController;
    private DeathWall wall;
    private DeathWallDirectionTrigger trigger;
    private Rigidbody2D wallBody;
    private BoxCollider2D wallCollider;

    [SetUp]
    public void SetUp()
    {
        playerObject = new GameObject("Test player");
        player = playerObject.AddComponent<PlayerController>();
        stats = playerObject.AddComponent<Stats>();
        playerObject.AddComponent<BoxCollider2D>();
        cameraObject = new GameObject("Test camera");
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 5f;
        camera.aspect = 2f;
        cameraController = cameraObject.AddComponent<CameraController>();
        wallObject = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Objects/DeathWall.prefab"));
        wall = wallObject.GetComponent<DeathWall>();
        SetReference(wall, "playerController", player);
        Call(wall, "Awake");
        SetField(wall, "gameplayCamera", camera);
        Call(wall, "OnEnable");
        wallBody = wall.GetComponent<Rigidbody2D>();
        wallCollider = wall.GetComponent<BoxCollider2D>();
        checkpointObject = new GameObject("Test checkpoint");
        SetReference(wall, "activationCheckpoint", checkpointObject.transform);
        triggerObject = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Objects/DeathWallDirectionTrigger.prefab"));
        trigger = triggerObject.GetComponent<DeathWallDirectionTrigger>();
        SetReference(trigger, "deathWall", wall);
        SetReference(trigger, "playerController", player);
        SetReference(trigger, "gameplayCamera", cameraController);
        Call(trigger, "Awake");
        Call(trigger, "OnEnable");
    }

    [TearDown]
    public void TearDown()
    {
        Object.DestroyImmediate(triggerObject);
        Object.DestroyImmediate(wallObject);
        Object.DestroyImmediate(checkpointObject);
        Object.DestroyImmediate(cameraObject);
        Object.DestroyImmediate(playerObject);
    }

    [Test]
    public void ReversalMirrorsLethalRegionAndGradientOnBothSides()
    {
        TouchCheckpoint();
        Assert.That(wall.TryReverseDirection(), Is.True);
        Assert.That(wallBody.position.x, Is.EqualTo(10f).Within(0.001f));
        Assert.That(wallCollider.offset.x, Is.GreaterThan(0f));
        SpriteRenderer fade = wallObject.transform.Find("FadeGradient").GetComponent<SpriteRenderer>();
        Assert.That(fade.flipX, Is.True);
        Assert.That(fade.transform.localPosition.x, Is.LessThan(0f));
        Assert.That(wall.TryReverseDirection(), Is.True);
        Assert.That(wallBody.position.x, Is.EqualTo(-10f).Within(0.001f));
        Assert.That(wallCollider.offset.x, Is.LessThan(0f));
        Assert.That(fade.flipX, Is.False);
        Assert.That(fade.transform.localPosition.x, Is.GreaterThan(0f));
    }

    [Test]
    public void ReversalPreservesHeadStartWhenPlayerIsOutsideCamera()
    {
        TouchCheckpoint();
        playerObject.transform.position = new Vector3(30f, 0f, 0f);
        Assert.That(wall.TryReverseDirection(), Is.True);
        Assert.That(wallBody.position.x, Is.EqualTo(38f).Within(0.001f));
    }

    [Test]
    public void TriggerIgnoresInactiveWallDeadPlayerAndNonPlayer()
    {
        EnterTrigger();
        Assert.That(wallCollider.enabled, Is.False);
        TouchCheckpoint();
        BoxCollider2D nonPlayer = checkpointObject.AddComponent<BoxCollider2D>();
        Call(trigger, "OnTriggerEnter2D", nonPlayer);
        Assert.That(wallBody.position.x, Is.LessThan(0f));
        stats.health = 0;
        EnterTrigger();
        Assert.That(wallBody.position.x, Is.LessThan(0f));
        stats.health = 200;
        EnterTrigger();
        Assert.That(wallBody.position.x, Is.GreaterThan(0f));
    }

    [Test]
    public void TriggerFiresOnceAndRespawnRestoresCheckpointState()
    {
        TouchCheckpoint();
        EnterTrigger();
        EnterTrigger();
        Assert.That(wallBody.position.x, Is.GreaterThan(0f));
        Respawn();
        Assert.That(wallBody.position.x, Is.EqualTo(-8f).Within(0.001f));
        EnterTrigger();
        Assert.That(wallBody.position.x, Is.GreaterThan(0f));
        GameObject nextCheckpoint = new GameObject("Next checkpoint");
        nextCheckpoint.transform.SetParent(checkpointObject.transform, false);
        Call(player, "HandleCheckpointTouched", nextCheckpoint.transform);
        Respawn();
        Assert.That(wallBody.position.x, Is.EqualTo(8f).Within(0.001f));
        EnterTrigger();
        Assert.That(wallBody.position.x, Is.EqualTo(8f).Within(0.001f));
    }

    [Test]
    public void ShakeOffsetDoesNotMoveWallGeometryAndClearsOnDisable()
    {
        TouchCheckpoint();
        Vector2 position = wallBody.position;
        Vector2 size = wallCollider.size;
        Vector3 offset = new Vector3(0.1f, -0.08f, 0f);
        SetField(cameraController, "shakeOffset", offset);
        cameraObject.transform.position += offset;
        Call(wall, "LateUpdate");
        Assert.That(wallBody.position, Is.EqualTo(position));
        Assert.That(wallCollider.size, Is.EqualTo(size));
        Call(cameraController, "OnDisable");
        Assert.That(cameraObject.transform.position, Is.EqualTo(Vector3.zero));
        Assert.That(cameraController.UnshakenPosition, Is.EqualTo(Vector3.zero));
    }

    private void TouchCheckpoint()
    {
        Call(player, "HandleCheckpointTouched", checkpointObject.transform);
    }

    private void Respawn()
    {
        player.reSpawn();
    }

    private void EnterTrigger()
    {
        Call(trigger, "OnTriggerEnter2D", playerObject.GetComponent<BoxCollider2D>());
    }

    private static void SetReference(Object component, string name, Object value)
    {
        SerializedObject serialized = new SerializedObject(component);
        serialized.FindProperty(name).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    // EditMode does not run runtime lifecycle/physics callbacks automatically.
    private static void Call(object component, string name, params object[] arguments)
    {
        component.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(component, arguments);
    }

    private static void SetField(object component, string name, object value)
    {
        component.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(component, value);
    }
}
