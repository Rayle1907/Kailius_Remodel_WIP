using UnityEngine;

public static class OneShotAudioPool {
    private const int PoolSize = 12;

    private static GameObject poolObject;
    private static AudioSource[] sources;
    private static int nextSource;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Initialize() {
        if (poolObject == null) {
            poolObject = GameObject.Find("One Shot Audio Pool");
        }
        if (poolObject == null) {
            poolObject = new GameObject("One Shot Audio Pool");
        }
        Object.DontDestroyOnLoad(poolObject);

        AudioSource[] existingSources = poolObject.GetComponents<AudioSource>();
        sources = new AudioSource[PoolSize];
        nextSource = 0;
        for (int i = 0; i < sources.Length; i++) {
            sources[i] = i < existingSources.Length
                ? existingSources[i] : poolObject.AddComponent<AudioSource>();
            sources[i].playOnAwake = false;
        }
    }

    private static void EnsureInitialized() {
        if (poolObject == null || sources == null || sources.Length != PoolSize) {
            Initialize();
            return;
        }

        foreach (AudioSource source in sources) {
            if (source == null) {
                Initialize();
                return;
            }
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
        EnsureInitialized();
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
