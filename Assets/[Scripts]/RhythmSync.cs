using UnityEngine;

/// <summary>
/// Shared rhythm state for all RhythmLights so they stay in sync: one global beat
/// pulse, bass level, and a single random neon color that ALL lights follow.
/// Updated once per frame (first light that ticks drives it); reads the shared
/// audio spectrum from AudioSpectrumProvider.
/// </summary>
public static class RhythmSync
{
    public enum ColorMode { OnBeat, Timed }

    // Paylaşılan çıktı (tüm ışıklar bunu okur)
    public static Color TargetColor = Color.white;   // senkron mod rengi
    public static float Pulse;
    public static float BassLevel;
    public static float HuePhase;                     // akan dalga için sürekli ton fazı

    // Başlangıç tonu (Inspector'daki başlangıç renginden)
    public static float StartHue;
    public static float FlowSpeed = 0.15f;            // dalga akış hızı (ton/sn)
    public static float BeatHueBump = 0.04f;          // beat'te ekstra ton kayması

    // Ayarlar (ışıklar her kare kendi değerlerini yazar; hepsi aynıysa sorun olmaz)
    public static float BeatThreshold = 1.4f;
    public static float MinBeatInterval = 0.12f;
    public static float PulseDecay = 9f;
    public static ColorMode Mode = ColorMode.OnBeat;
    public static float ColorInterval = 1.5f;
    public static float Saturation = 0.9f;
    public static Vector2 HueRange = new Vector2(0f, 1f);

    static int _frame = -1;
    static float _energyAvg = 0.0001f;
    static float _beatTimer;
    static float _colorTimer;
    static bool _init;

    /// <summary>Kare başına bir kez günceller (birden çok çağrı güvenli).</summary>
    public static void Tick()
    {
        if (Time.frameCount == _frame) return;
        _frame = Time.frameCount;

        if (!_init) { TargetColor = Color.HSVToRGB(Mathf.Repeat(StartHue, 1f), Saturation, 1f); _init = true; }

        float[] spec = AudioSpectrumProvider.GetShared();
        int bassBins = Mathf.Max(4, spec.Length / 32);
        float bass = 0f;
        for (int i = 0; i < bassBins; i++) bass += spec[i];

        BassLevel = Mathf.Lerp(BassLevel, Mathf.Clamp01(bass * 60f), Time.deltaTime * 12f);
        _energyAvg = Mathf.Lerp(_energyAvg, bass, Time.deltaTime * 3f);
        Pulse = Mathf.Lerp(Pulse, 0f, Time.deltaTime * PulseDecay);
        _beatTimer += Time.deltaTime;

        // Akan dalga fazı sürekli ilerler (beat'te hafif hızlanır)
        HuePhase += FlowSpeed * Time.deltaTime;

        bool beat = bass > _energyAvg * BeatThreshold && _beatTimer >= MinBeatInterval && bass > 0.0002f;
        if (beat)
        {
            _beatTimer = 0f;
            Pulse = 1f;
            HuePhase += BeatHueBump;
            if (Mode == ColorMode.OnBeat) TargetColor = RandomNeon();   // TEK renk, herkes takip eder
        }

        if (Mode == ColorMode.Timed)
        {
            _colorTimer -= Time.deltaTime;
            if (_colorTimer <= 0f) { TargetColor = RandomNeon(); _colorTimer = ColorInterval; }
        }
    }

    static Color RandomNeon()
    {
        float h = Random.Range(HueRange.x, HueRange.y);
        return Color.HSVToRGB(Mathf.Repeat(h, 1f), Saturation, 1f);
    }
}
