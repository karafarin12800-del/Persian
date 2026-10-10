using UnityEngine;

/// <summary>
/// Lightweight procedural audio so the mobile prototype has background music and
/// distinct player/enemy shot effects without requiring downloaded audio assets.
/// </summary>
public sealed class RuntimeGameAudio : MonoBehaviour
{
    private static RuntimeGameAudio instance;
    private const int SampleRate = 22050;
    private AudioSource musicSource;
    private AudioSource effectsSource;
    private AudioClip playerShot;
    private AudioClip pistolShot;
    private AudioClip rifleShot;
    private AudioClip heavyShot;
    private AudioClip enemyShot;

    public static RuntimeGameAudio EnsureInstance()
    {
        if (instance != null) return instance;
        GameObject audioObject = new GameObject("PersiaWarRuntimeAudio");
        DontDestroyOnLoad(audioObject);
        instance = audioObject.AddComponent<RuntimeGameAudio>();
        return instance;
    }

    public static void PlayPlayerShot()
    {
        RuntimeGameAudio audio = EnsureInstance();
        if (audio.effectsSource != null && audio.playerShot != null)
            audio.effectsSource.PlayOneShot(audio.playerShot, 0.75f);
    }

    public static void PlayWeaponShot(int weaponKind)
    {
        RuntimeGameAudio audio = EnsureInstance();
        if (audio.effectsSource == null) return;
        AudioClip clip = weaponKind == 0 ? audio.pistolShot
            : (weaponKind == 2 ? audio.heavyShot : audio.rifleShot);
        if (clip != null)
            audio.effectsSource.PlayOneShot(clip, weaponKind == 2 ? 0.9f : 0.76f);
    }

    public static void PlayEnemyShot()
    {
        RuntimeGameAudio audio = EnsureInstance();
        if (audio.effectsSource != null && audio.enemyShot != null)
            audio.effectsSource.PlayOneShot(audio.enemyShot, 0.55f);
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.spatialBlend = 0f;
        musicSource.volume = 0.18f;

        effectsSource = gameObject.AddComponent<AudioSource>();
        effectsSource.playOnAwake = false;
        effectsSource.loop = false;
        effectsSource.spatialBlend = 0f;
        effectsSource.volume = 0.70f;

        playerShot = BuildShotClip("PersiaWar_PlayerShot", 145f, 1250f, 0.13f);
        pistolShot = BuildShotClip("PersiaWar_PistolShot", 235f, 1850f, 0.085f);
        rifleShot = BuildShotClip("PersiaWar_RifleShot", 145f, 1250f, 0.13f);
        heavyShot = BuildShotClip("PersiaWar_HeavyShot", 72f, 540f, 0.22f);
        enemyShot = BuildShotClip("PersiaWar_EnemyShot", 95f, 760f, 0.16f);
        musicSource.clip = BuildMusicLoop();
        musicSource.Play();
    }

    private void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    private static AudioClip BuildMusicLoop()
    {
        const float duration = 8f;
        int count = Mathf.RoundToInt(SampleRate * duration);
        float[] data = new float[count];
        float[] roots = { 110f, 130.81f, 146.83f, 98f };
        float[] melody = { 440f, 523.25f, 587.33f, 659.25f, 587.33f, 523.25f, 392f, 440f,
                           493.88f, 587.33f, 659.25f, 783.99f, 659.25f, 587.33f, 440f, 493.88f };

        for (int i = 0; i < count; i++)
        {
            float t = i / (float)SampleRate;
            int bar = Mathf.Min(3, (int)(t / 2f));
            float beatPosition = t % 0.5f;
            int note = Mathf.Clamp((int)(t / 0.5f), 0, melody.Length - 1);
            float noteEnvelope = Mathf.Clamp01(Mathf.Min(beatPosition * 9f, (0.5f - beatPosition) * 5f));
            float chord = Mathf.Sin(2f * Mathf.PI * roots[bar] * t) * 0.45f
                        + Mathf.Sin(2f * Mathf.PI * roots[bar] * 1.5f * t) * 0.18f
                        + Mathf.Sin(2f * Mathf.PI * roots[bar] * 2f * t) * 0.12f;
            float lead = Mathf.Sin(2f * Mathf.PI * melody[note] * t) * noteEnvelope * 0.24f;
            float edgeFade = Mathf.Clamp01(Mathf.Min(t / 0.12f, (duration - t) / 0.12f));
            data[i] = Mathf.Clamp((chord * 0.15f + lead) * edgeFade, -0.8f, 0.8f);
        }

        AudioClip clip = AudioClip.Create("PersiaWar_BackgroundLoop", count, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }

    private static AudioClip BuildShotClip(string clipName, float lowFrequency, float highFrequency, float duration)
    {
        int count = Mathf.RoundToInt(SampleRate * duration);
        float[] data = new float[count];
        uint noise = clipName.GetHashCode() == 0 ? 0x1234567u : (uint)clipName.GetHashCode();

        for (int i = 0; i < count; i++)
        {
            float t = i / (float)SampleRate;
            float progress = t / duration;
            float envelope = Mathf.Exp(-progress * 8.5f);
            noise ^= noise << 13;
            noise ^= noise >> 17;
            noise ^= noise << 5;
            float white = ((noise & 0xFFFFu) / 32767.5f) - 1f;
            float low = Mathf.Sin(2f * Mathf.PI * lowFrequency * t) * 0.52f;
            float crack = Mathf.Sin(2f * Mathf.PI * highFrequency * t) * Mathf.Exp(-progress * 24f) * 0.28f;
            data[i] = Mathf.Clamp((white * 0.24f + low + crack) * envelope, -0.95f, 0.95f);
        }

        AudioClip clip = AudioClip.Create(clipName, count, 1, SampleRate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
