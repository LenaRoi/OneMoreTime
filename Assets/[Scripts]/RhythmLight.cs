using UnityEngine;

/// <summary>
/// Neon light that pulses to the beat and randomly changes color, using the same
/// shared audio spectrum as the equalizers (AudioSpectrumProvider). Attach to a
/// Light (Point/Spot). Intensity spikes on each detected beat and eases back; the
/// color jumps to a new random neon hue (on beat or on a timer) and blends there.
/// Place several around the scene for a rhythmic neon wash.
/// </summary>
[RequireComponent(typeof(Light))]
public class RhythmLight : MonoBehaviour
{
    public enum ColorChange { OnBeat, Timed }

    [Header("Parlaklık (Nabız)")]
    [Tooltip("Sürekli taban parlaklık.")]
    public float baseIntensity = 0.4f;
    [Tooltip("Beat'te eklenen parlaklık.")]
    public float beatIntensity = 3f;
    [Tooltip("Nabzın sönme hızı (yüksek = kısa/keskin flaş).")]
    [Range(1f, 30f)] public float pulseDecay = 9f;
    [Tooltip("Bas enerjisine göre hafif sürekli titreşim.")]
    [Range(0f, 3f)] public float continuousResponse = 0.6f;

    [Header("Renk (Neon, rastgele)")]
    [Tooltip("Renk ne zaman değişsin: her beat'te ya da zamanlayıcıyla.")]
    public ColorChange colorChange = ColorChange.OnBeat;
    [Tooltip("Timed modda kaç saniyede bir renk değişsin.")]
    public float colorInterval = 1.5f;
    [Tooltip("Renk geçiş hızı (yüksek = ani, düşük = yumuşak).")]
    [Range(0.5f, 20f)] public float colorLerpSpeed = 6f;
    [Tooltip("Neon doygunluğu.")]
    [Range(0.5f, 1f)] public float saturation = 0.9f;
    [Tooltip("İzin verilen renk tonları (0-1 hue). Boşsa tüm gökkuşağı.")]
    public Vector2 hueRange = new Vector2(0f, 1f);

    [Header("Beat Algılama")]
    [Range(1.05f, 3f)] public float beatThreshold = 1.4f;
    public float minBeatInterval = 0.12f;

    [Header("Performans")]
    [Tooltip("Kameraya bu mesafeden uzaktayken ışık kapanır (0 = hep açık).")]
    public float maxVisibleDistance = 45f;

    private Light lite;
    private float pulse;
    private float bassLevel;
    private float energyAvg = 0.0001f;
    private float beatTimer;
    private float colorTimer;
    private Color targetColor;
    private Camera cam;

    void Start()
    {
        lite = GetComponent<Light>();
        // Farklı ışıklar farklı başlasın diye rastgele faz
        targetColor = RandomNeon();
        lite.color = RandomNeon();
        colorTimer = Random.value * colorInterval;
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

        DetectBeat();

        // Nabız söner; bas seviyesi yumuşak takip edilir
        pulse = Mathf.Lerp(pulse, 0f, Time.deltaTime * pulseDecay);
        lite.intensity = baseIntensity + bassLevel * continuousResponse + pulse * beatIntensity;

        // Renk zamanlayıcısı (Timed mod)
        if (colorChange == ColorChange.Timed)
        {
            colorTimer -= Time.deltaTime;
            if (colorTimer <= 0f) { targetColor = RandomNeon(); colorTimer = colorInterval; }
        }

        lite.color = Color.Lerp(lite.color, targetColor, Time.deltaTime * colorLerpSpeed);
    }

    void DetectBeat()
    {
        float[] spec = AudioSpectrumProvider.GetShared();
        int bassBins = Mathf.Max(4, spec.Length / 32);
        float bass = 0f;
        for (int i = 0; i < bassBins; i++) bass += spec[i];

        // Görsel için normalize edilmiş bas seviyesi
        bassLevel = Mathf.Lerp(bassLevel, Mathf.Clamp01(bass * 60f), Time.deltaTime * 12f);

        energyAvg = Mathf.Lerp(energyAvg, bass, Time.deltaTime * 3f);
        beatTimer += Time.deltaTime;

        if (bass > energyAvg * beatThreshold && beatTimer >= minBeatInterval && bass > 0.0002f)
        {
            beatTimer = 0f;
            pulse = 1f;
            if (colorChange == ColorChange.OnBeat) targetColor = RandomNeon();
        }
    }

    Color RandomNeon()
    {
        float h = Random.Range(hueRange.x, hueRange.y);
        return Color.HSVToRGB(Mathf.Repeat(h, 1f), saturation, 1f);
    }
}
