using UnityEngine;

public class VolumenController : MonoBehaviour
{
    public static float Volume => Mathf.Clamp01(PlayerPrefs.GetFloat("AudioVolume", 1f));

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void RestoreVolume()
    {
        AudioListener.volume = Volume;
    }

    public void SetVolume(float volume)
    {
        ApplyVolume(volume);
    }

    public static void ApplyVolume(float volume)
    {
        volume = Mathf.Clamp01(volume);
        AudioListener.volume = volume;
        PlayerPrefs.SetFloat("AudioVolume", volume);
    }
}
