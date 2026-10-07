using UnityEngine;

public sealed class MobileInterruptionPause : MonoBehaviour
{
#if UNITY_IOS && !UNITY_EDITOR
    private bool wasPlaying;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        GameObject host = new GameObject("Mobile Interruption Pause");
        DontDestroyOnLoad(host);
        host.AddComponent<MobileInterruptionPause>();
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
        {
            PlayerPrefs.Save();
            MoveByTouch pausedMovement = FindAnyObjectByType<MoveByTouch>();
            wasPlaying = Time.timeScale > 0f && pausedMovement != null &&
                pausedMovement.ControlesMoviles != null && pausedMovement.ControlesMoviles.activeSelf &&
                pausedMovement.menu != null;
            if (wasPlaying)
            {
                Time.timeScale = 0f;
            }
            return;
        }

        if (!wasPlaying)
        {
            return;
        }

        wasPlaying = false;
        MoveByTouch movement = FindAnyObjectByType<MoveByTouch>();
        if (movement != null)
        {
            movement.settings();
        }
        else
        {
            Time.timeScale = 1f;
        }
    }
#endif
}
