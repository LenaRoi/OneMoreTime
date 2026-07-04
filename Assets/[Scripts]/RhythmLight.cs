using UnityEngine;

/// <summary>
/// Neon light that pulses to the beat and changes to a random neon color that is
/// SHARED across every RhythmLight (via RhythmSync), so a corridor full of these
/// all show the same color and change together instead of a rainbow mess.
/// Attach to a Light (Point/Spot). Uses the shared audio (AudioSpectrumProvider).
/// </summary>
[RequireComponent(typeof(Light))]
public class RhythmLight : MonoBehaviour
{
    public enum LightMode { SyncedColor, FlowingWave }

    [Header("Mod")]
    [Tooltip("SyncedColor: tüm ışıklar aynı renk. FlowingWave: renk koridor boyunca akar.")]
    public LightMode mode = LightMode.FlowingWave;

    [Header("Başlangıç Rengi")]
    [Tooltip("Başlangıç/temel neon renk. Dalga bu tondan başlar.")]
    [ColorUsage(false, true)] public Color startColor = new Color(0.6f, 0.1f, 1f);

    [Header("Akan Dalga (FlowingWave)")]
    [Tooltip("Koridor yönü (rengin aktığı eksen). Koridor X'te ise (1,0,0), Z'de ise (0,0,1).")]
    public Vector3 waveAxis = Vector3.forward;
    [Tooltip("Dalga sıklığı: dünya biriminde ton kayması. Yüksek = daha sık renk değişimi.")]
    public float waveDensity = 0.06f;
    [Tooltip("Dalganın akma hızı (ton/sn).")]
    public float flowSpeed = 0.15f;

    [Header("Parlaklık (Nabız)")]
    [Tooltip("Sürekli taban parlaklık.")]
    public float baseIntensity = 0.4f;
    [Tooltip("Beat'te eklenen parlaklık.")]
    public float beatIntensity = 3f;
    [Tooltip("Nabzın sönme hızı (yüksek = kısa/keskin flaş).")]
    [Range(1f, 30f)] public float pulseDecay = 9f;
    [Tooltip("Bas enerjisine göre hafif sürekli titreşim.")]
    [Range(0f, 3f)] public float continuousResponse = 0.6f;

    [Header("Renk (Neon — TÜM ışıklarda ortak/senkron)")]
    [Tooltip("Renk ne zaman değişsin: her beat'te ya da zamanlayıcıyla.")]
    public RhythmSync.ColorMode colorChange = RhythmSync.ColorMode.OnBeat;
    [Tooltip("Timed modda kaç saniyede bir renk değişsin.")]
    public float colorInterval = 1.5f;
    [Tooltip("Renk geçiş hızı (yüksek = ani, düşük = yumuşak).")]
    [Range(0.5f, 20f)] public float colorLerpSpeed = 6f;
    [Tooltip("Neon doygunluğu.")]
    [Range(0.5f, 1f)] public float saturation = 0.9f;
    [Tooltip("İzin verilen renk tonları (0-1 hue). (0,1) = tüm gökkuşağı.")]
    public Vector2 hueRange = new Vector2(0f, 1f);

    [Header("Beat Algılama (ortak)")]
    [Range(1.05f, 3f)] public float beatThreshold = 1.4f;
    public float minBeatInterval = 0.12f;

    [Header("Performans")]
    [Tooltip("Kameraya bu mesafeden uzaktayken ışık kapanır (0 = hep açık).")]
    public float maxVisibleDistance = 45f;

    private Light lite;
    private Camera cam;

    void Start()
    {
        lite = GetComponent<Light>();
    }

    void Update()
    {
        // Uzaktaki ışıkları kapat (realtime ışık pahalıdır)
        if (maxVisibleDistance > 0f)
        {
            if (cam == null) cam = Camera.main;
            if (cam != null && (cam.transform.position - transform.position).sqrMagnitude > maxVisibleDistance * maxVisibleDistance)
            {
                if (lite.enabled) lite.enabled = false;
                return;
            }
            if (!lite.enabled) lite.enabled = true;
        }

        // Ortak senkronu besle ve güncelle (kare başına bir kez çalışır)
        Color.RGBToHSV(startColor, out float startHue, out _, out _);
        RhythmSync.StartHue = startHue;
        RhythmSync.FlowSpeed = flowSpeed;
        RhythmSync.BeatThreshold = beatThreshold;
        RhythmSync.MinBeatInterval = minBeatInterval;
        RhythmSync.PulseDecay = pulseDecay;
        RhythmSync.Mode = colorChange;
        RhythmSync.ColorInterval = colorInterval;
        RhythmSync.Saturation = saturation;
        RhythmSync.HueRange = hueRange;
        RhythmSync.Tick();

        // Ortak nabız/bas ile parlaklık
        lite.intensity = baseIntensity + RhythmSync.BassLevel * continuousResponse + RhythmSync.Pulse * beatIntensity;

        // Hedef renk: moda göre
        Color target;
        if (mode == LightMode.FlowingWave)
        {
            // Bu ışığın koridor üzerindeki konumu → ton kayması → akan dalga
            Vector3 axis = waveAxis.sqrMagnitude > 1e-6f ? waveAxis.normalized : Vector3.forward;
            float spatial = Vector3.Dot(transform.position, axis) * waveDensity;
            float hue = RhythmSync.StartHue + RhythmSync.HuePhase + spatial;
            target = Color.HSVToRGB(Mathf.Repeat(hue, 1f), saturation, 1f);
        }
        else
        {
            target = RhythmSync.TargetColor;   // tüm ışıklar aynı renk
        }

        lite.color = Color.Lerp(lite.color, target, Time.deltaTime * colorLerpSpeed);
    }
}
