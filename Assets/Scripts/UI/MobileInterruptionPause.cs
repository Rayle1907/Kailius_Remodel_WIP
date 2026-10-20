using UnityEngine;

public sealed class MobileInterruptionPause : MonoBehaviour
{
#if UNITY_IOS && !UNITY_EDITOR
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        GameObject host = new GameObject("Mobile Interruption Pause");
        DontDestroyOnLoad(host);
        host.AddComponent<MobileInterruptionPause>();
    }

    private void OnApplicationPause(bool paused)
    {
        if (!paused) return;
        PlayerPrefs.Save();
        MoveByTouch movement = FindAnyObjectByType<MoveByTouch>();
        if (Time.timeScale > 0f && movement != null &&
            movement.ControlesMoviles != null && movement.ControlesMoviles.activeSelf &&
            movement.menu != null && movement.GetComponent<InGameSettings>() != null)
        {
            movement.settings();
        }
    }
#endif
}
