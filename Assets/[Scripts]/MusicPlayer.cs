using System.Collections;
using UnityEngine;

/// <summary>
/// Plays the music with a configurable FIRST start time and a separate LOOP start
/// time: on game open it begins at <see cref="firstStartTime"/>, and every time it
/// finishes it restarts from <see cref="loopStartTime"/> (intro skipped on repeats).
/// Waits for the clip's audio data to load before seeking, so the start offset is
/// respected even when Preload Audio Data is off. Attach to the AudioSource that
/// plays the song (shared MusicPlayer).
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class MusicPlayer : MonoBehaviour
{
    [Tooltip("Oyun ilk açıldığında başlayacağı saniye.")]
    [Min(0f)] public float firstStartTime = 12f;

    [Tooltip("Döngüde/deadloop sonrası başlayacağı saniye — intro atlanır.")]
    [Min(0f)] public float loopStartTime = 30f;

    [Tooltip("Şarkı bitince döngüye girsin mi.")]
    public bool loop = true;

    [Tooltip("Start'ta otomatik çal.")]
    public bool playOnStart = true;

    private AudioSource source;
    private bool started;
    private bool busy;          // yükleme/başlatma sürüyor
    private float guardUntil;

    void Awake()
    {
        source = GetComponent<AudioSource>();
        source.playOnAwake = false;   // çalmayı bu script yönetsin
        source.loop = false;          // döngüyü elle yapıyoruz (özel başlangıç noktası)
    }

    void Start()
    {
        if (playOnStart) PlayFrom(firstStartTime);
    }

    void Update()
    {
        // Şarkı bittiyse ve döngü açıksa loopStartTime'dan tekrar başlat
        if (started && loop && !busy && Time.time >= guardUntil && !source.isPlaying)
            PlayFrom(loopStartTime);
    }

    /// <summary>Deadloop (ölüm) animasyonundan sonra çağrılır: müziği loopStartTime'dan yeniden başlatır.</summary>
    public void RestartFromLoop() => PlayFrom(loopStartTime);

    /// <summary>Şarkıyı verilen saniyeden başlatır (yüklemeyi bekler).</summary>
    public void PlayFrom(float time)
    {
        if (busy) return;
        if (source == null) source = GetComponent<AudioSource>();
        if (source.clip == null)
        {
            Debug.LogWarning($"{name}: AudioSource'ta AudioClip yok.", this);
            return;
        }
        StartCoroutine(PlayRoutine(time));
    }

    IEnumerator PlayRoutine(float time)
    {
        busy = true;

        // Ses verisi hazır değilse yükle ve bekle (seek'in çalışması için şart)
        if (source.clip.loadState != AudioDataLoadState.Loaded)
        {
            source.clip.LoadAudioData();
            while (source.clip.loadState == AudioDataLoadState.Loading)
                yield return null;
        }

        source.Play();
        source.timeSamples = Mathf.Clamp(
            Mathf.RoundToInt(time * source.clip.frequency), 0, source.clip.samples - 1);

        started = true;
        guardUntil = Time.time + 0.3f;   // bu süre boyunca "bitti" kontrolü yapma
        busy = false;
    }

    [ContextMenu("Play (First Start Time)")]
    void PlayFirst() => PlayFrom(firstStartTime);

    [ContextMenu("Play (Loop Start Time)")]
    void PlayLoop() => PlayFrom(loopStartTime);
}
