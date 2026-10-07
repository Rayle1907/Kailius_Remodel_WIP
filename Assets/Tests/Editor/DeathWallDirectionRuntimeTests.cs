using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

public sealed class DeathWallDirectionRuntimeTests
{
    [UnityTest]
    public IEnumerator PhysicsEntryReversesOnceAndShakePausesThenSettles()
    {
        EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
        yield return new EnterPlayMode();
        GameObject root = new GameObject("Wall reversal runtime test");
        root.SetActive(false);
        GameObject playerObject = new GameObject("Player");
        playerObject.layer = LayerMask.NameToLayer("Player");
        playerObject.transform.SetParent(root.transform);
        Rigidbody2D playerBody = playerObject.AddComponent<Rigidbody2D>();
        playerBody.gravityScale = 0f;
        playerObject.AddComponent<BoxCollider2D>();
        PlayerController player = playerObject.AddComponent<PlayerController>();
        player.row = playerObject;
        player.enabled = false; // Keep unrelated input/UI out of this interaction test.
        Stats stats = playerObject.AddComponent<Stats>();
        stats.enabled = false;
        GameObject cameraObject = new GameObject("Camera");
        cameraObject.transform.SetParent(root.transform);
        cameraObject.tag = "MainCamera";
        cameraObject.transform.position = new Vector3(0f, 0f, -10f);
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.orthographic = true;
        camera.orthographicSize = 5f;
        camera.aspect = 2f;
        CameraController follow = cameraObject.AddComponent<CameraController>();
        follow.objetivo = playerObject;
        GameObject wallObject = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Objects/DeathWall.prefab"), root.transform);
        DeathWall wall = wallObject.GetComponent<DeathWall>();
        GameObject checkpoint = new GameObject("Checkpoint");
        checkpoint.transform.SetParent(root.transform);
        SetReference(wall, "playerController", player);
        SetReference(wall, "activationCheckpoint", checkpoint.transform);
        GameObject triggerObject = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Objects/DeathWallDirectionTrigger.prefab"), root.transform);
        DeathWallDirectionTrigger trigger = triggerObject.GetComponent<DeathWallDirectionTrigger>();
        SetReference(trigger, "deathWall", wall);
        SetReference(trigger, "playerController", player);
        SetReference(trigger, "gameplayCamera", follow);

        try
        {
            root.SetActive(true);
            player.GetType().GetMethod("HandleCheckpointTouched", BindingFlags.Instance | BindingFlags.NonPublic)
                .Invoke(player, new object[] { checkpoint.transform });
            yield return FixedSteps(2);
            yield return FixedSteps(2);
            yield return null;
            Assert.That(wall.GetComponent<BoxCollider2D>().offset.x, Is.GreaterThan(0f));
            Assert.That(stats.health, Is.EqualTo(200));

            // Leave and re-enter the real physics volume without another reversal.
            playerBody.position = new Vector2(2f, 0f);
            yield return FixedSteps(2);
            playerBody.position = Vector2.zero;
            yield return FixedSteps(2);
            yield return FixedSteps(2);
            Assert.That(wall.GetComponent<BoxCollider2D>().offset.x, Is.GreaterThan(0f));

            follow.Shake(0.2f, 0.12f);
            yield return null;
            yield return null;
            Assert.That(cameraObject.transform.position.z, Is.EqualTo(-10f));
            Vector3 offset = cameraObject.transform.position - follow.UnshakenPosition;
            Assert.That(offset.magnitude, Is.LessThanOrEqualTo(0.12001f));
            Time.timeScale = 0f;
            yield return null; // Let the scaled clock reach zero before measuring.
            Vector3 pausedPosition = cameraObject.transform.position;
            float pauseEnd = Time.realtimeSinceStartup + 0.05f;
            while (Time.realtimeSinceStartup < pauseEnd)
            {
                yield return null;
            }
            Assert.That(cameraObject.transform.position, Is.EqualTo(pausedPosition));
            Time.timeScale = 1f;
            // This is an EditMode runner entering Play Mode; measure the game
            // clock explicitly rather than relying on coroutine wait handling.
            float shakeEnd = Time.time + 0.3f;
            while (Time.time < shakeEnd)
            {
                yield return null;
            }
            yield return null;
            Assert.That((cameraObject.transform.position - follow.UnshakenPosition).magnitude, Is.LessThan(0.00001f));
            Assert.That(cameraObject.transform.position.z, Is.EqualTo(-10f));
        }
        finally
        {
            Time.timeScale = 1f;
            Object.Destroy(triggerObject);
            Object.Destroy(wallObject);
            Object.Destroy(root);
        }
        yield return new ExitPlayMode();
    }

    private static IEnumerator FixedSteps(int count)
    {
        float end = Time.fixedTime + Time.fixedDeltaTime * count;
        while (Time.fixedTime < end) yield return null;
    }

    private static void SetReference(Object component, string name, Object value)
    {
        SerializedObject serialized = new SerializedObject(component);
        serialized.FindProperty(name).objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }
}
