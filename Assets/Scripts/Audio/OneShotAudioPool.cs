using UnityEngine;

public static class OneShotAudioPool {
    private const int PoolSize = 12;

    private static AudioSource[] sources;
    private static int nextSource;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize() {
        GameObject poolObject = new GameObject("One Shot Audio Pool");
        Object.DontDestroyOnLoad(poolObject);

        sources = new AudioSource[PoolSize];
        for (int i = 0; i < sources.Length; i++) {
            sources[i] = poolObject.AddComponent<AudioSource>();
            sources[i].playOnAwake = false;
        }
    }

    public static void Play(GameObject soundPrefab, Vector3 position) {
        if (soundPrefab == null) {
            return;
        }

        AudioSource template = soundPrefab.GetComponent<AudioSource>();
        if (template == null || template.clip == null) {
            Debug.LogWarning($"Sound prefab '{soundPrefab.name}' has no AudioSource or AudioClip.");
            return;
        }

        AudioSource source = GetAvailableSource();
        source.transform.position = position;
        CopySettings(template, source);
        source.Play();
    }

    private static AudioSource GetAvailableSource() {
        for (int i = 0; i < sources.Length; i++) {
            int index = (nextSource + i) % sources.Length;
            if (!sources[index].isPlaying) {
                nextSource = (index + 1) % sources.Length;
                return sources[index];
            }
        }

        AudioSource source = sources[nextSource];
        nextSource = (nextSource + 1) % sources.Length;
        source.Stop();
        return source;
    }

    private static void CopySettings(AudioSource template, AudioSource source) {
        source.clip = template.clip;
        source.outputAudioMixerGroup = template.outputAudioMixerGroup;
        source.mute = template.mute;
        source.bypassEffects = template.bypassEffects;
        source.bypassListenerEffects = template.bypassListenerEffects;
        source.bypassReverbZones = template.bypassReverbZones;
        source.priority = template.priority;
        source.volume = template.volume;
        source.pitch = template.pitch;
        source.panStereo = template.panStereo;
        source.spatialBlend = template.spatialBlend;
        source.reverbZoneMix = template.reverbZoneMix;
        source.dopplerLevel = template.dopplerLevel;
        source.spread = template.spread;
        source.rolloffMode = template.rolloffMode;
        source.minDistance = template.minDistance;
        source.maxDistance = template.maxDistance;
        source.loop = false;
    }
}
