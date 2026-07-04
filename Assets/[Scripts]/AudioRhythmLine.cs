using UnityEngine;

/// <summary>
/// Neon spectrum equalizer on a cube face. Builds a grid of little cube blocks:
/// each column is a frequency band, stacked segments light up with the music
/// (rainbow colored), like a classic audio equalizer. Auto-fits the chosen face,
/// so scaling the cube keeps it on the surface. Attach to the cube.
/// </summary>
[RequireComponent(typeof(AudioSource))]
public class AudioRhythmLine : MonoBehaviour
{
    public enum Face { Front, Back, Left, Right, Top, Bottom }

    [Header("Yüzey")]
    [Tooltip("Equalizer'ın çizileceği küp yüzeyi.")]
    public Face face = Face.Front;

    [Tooltip("Yüzey boyutunu mesh'ten otomatik oku (küpü ölçeklersen uyum sağlar).")]
    public bool autoFitToMesh = true;
    [Tooltip("autoFitToMesh kapalıysa kullanılacak yerel küp boyutu.")]
    public float manualCubeSize = 1f;

    [Header("Izgara")]
    [Tooltip("Sütun (frekans bandı) sayısı.")]
    [Range(4, 64)] public int bars = 32;
    [Tooltip("Her sütundaki blok (segment) sayısı.")]
    [Range(3, 40)] public int segments = 16;

    [Tooltip("Yüzeyin ne kadarını doldursun (1 = kenardan kenara).")]
    [Range(0.3f, 1f)] public float fillFraction = 0.95f;
    [Tooltip("Bloğun kendi hücresini doldurma oranı (aradaki boşluk).")]
    [Range(0.3f, 1f)] public float blockFill = 0.8f;
    [Tooltip("Blokların yüzeyden dışarı kalınlığı.")]
    public float thickness = 0.03f;
    [Tooltip("Yüzeyden dışarı küçük kaldırma (z-fighting'i önler).")]
    public float surfaceOffset = 0.005f;

    [Header("Tepki")]
    [Tooltip("Sinyal gücü çarpanı (yüksek = çubuklar daha çok yükselir).")]
    public float sensitivity = 200f;
    [Tooltip("Yükselme hızı (anlık zirve).")]
    [Range(1f, 30f)] public float attack = 25f;
    [Tooltip("Düşme hızı (yavaş = daha akıcı iniş).")]
    [Range(1f, 30f)] public float decay = 8f;

    [Header("Dengeleme (Otomatik Kazanç)")]
    [Tooltip("Her sütunu KENDİ son zirvesine göre ölçekler; sessiz bantlar da canlanır, parça hızlanınca dengeli kalır.")]
    [Range(0f, 1f)] public float autoBalance = 0.8f;
    [Tooltip("Zirve takibinin düşme hızı (düşük = zirveyi uzun hatırlar, daha kararlı denge).")]
    [Range(0.1f, 5f)] public float balanceFalloff = 0.6f;
    [Tooltip("Gürültü kapısı: bu eşiğin altındaki bantlar boş kalır (fısıltı/gürültü yükselmesin).")]
    [Range(0f, 0.5f)] public float noiseGate = 0.06f;

    [Header("Renk / Neon")]
    [Tooltip("Gökkuşağı renk taraması başlangıcı (0=kırmızı).")]
    [Range(0f, 1f)] public float hueStart = 0f;
    [Tooltip("Renk taraması genişliği (0.8 ≈ kırmızıdan mora).")]
    [Range(0.2f, 1f)] public float hueRange = 0.85f;
    [Tooltip("Renk parlaklığı (neon etkisi; Bloom ile daha güçlü görünür).")]
    [Range(1f, 8f)] public float brightness = 2.2f;
    [Tooltip("Sönük (yanmayan) blokları göster (koyu). Kapalı = tamamen gizle.")]
    public bool showUnlit = true;
    [Range(0f, 0.4f)] public float unlitDim = 0.12f;

    private AudioSource audioSource;
    private float[] spectrum;
    private float[] level;         // sütun seviyeleri 0..1
    private float[] barPeak;       // her sütunun son zirvesi (otomatik kazanç)
    private float[] rawMag;        // her sütunun ham büyüklüğü (bu kare)
    private float globalPeak;      // tüm bantların en gür zirvesi
    private MeshRenderer[,] blocks;
    private MaterialPropertyBlock mpb;
    private Color[] barColors;
    private Transform grid;
    private int builtBars, builtSegments;
    private Vector3 halfExtents = Vector3.one * 0.5f;

    static readonly int ColorID = Shader.PropertyToID("_Color");

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        mpb = new MaterialPropertyBlock();
        Build();
    }

    void CacheExtents()
    {
        if (autoFitToMesh)
        {
            var mf = GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null) { halfExtents = mf.sharedMesh.bounds.extents; return; }
        }
        halfExtents = Vector3.one * (manualCubeSize * 0.5f);
    }

    void Build()
    {
        CacheExtents();

        // Eski ızgarayı temizle
        var old = transform.Find("Equalizer");
        if (old != null) DestroyImmediate(old.gameObject);

        grid = new GameObject("Equalizer").transform;
        grid.SetParent(transform, false);

        spectrum = new float[Mathf.Max(64, Mathf.NextPowerOfTwo(bars * 4))];
        level = new float[bars];
        barPeak = new float[bars];
        rawMag = new float[bars];
        blocks = new MeshRenderer[bars, segments];
        barColors = new Color[bars];

        Vector3 right, up, normal;
        GetFaceBasis(out right, out up, out normal);
        float halfW = Vector3.Dot(new Vector3(halfExtents.x, halfExtents.y, halfExtents.z), Abs(right));
        float halfV = Vector3.Dot(new Vector3(halfExtents.x, halfExtents.y, halfExtents.z), Abs(up));
        float outN = Vector3.Dot(new Vector3(halfExtents.x, halfExtents.y, halfExtents.z), Abs(normal));

        float width = 2f * halfW * fillFraction;
        float height = 2f * halfV * fillFraction;
        float barPitch = width / bars;
        float segPitch = height / segments;
        float blockW = barPitch * blockFill;
        float blockH = segPitch * blockFill;
        float leftStart = -halfW * fillFraction;
        float bottom = -halfV * fillFraction;
        float outPos = outN + surfaceOffset + thickness * 0.5f;

        Vector3 scale = Abs(right) * blockW + Abs(up) * blockH + Abs(normal) * thickness;
        var shader = Shader.Find("Unlit/Color");

        for (int b = 0; b < bars; b++)
        {
            float hue = hueStart + (bars <= 1 ? 0f : (float)b / (bars - 1)) * hueRange;
            barColors[b] = Color.HSVToRGB(Mathf.Repeat(hue, 1f), 0.9f, 1f);

            float rx = leftStart + (b + 0.5f) * barPitch;
            for (int s = 0; s < segments; s++)
            {
                float uy = bottom + (s + 0.5f) * segPitch;
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = $"blk_{b}_{s}";
                var col = go.GetComponent<Collider>();
                if (col != null) DestroyImmediate(col);
                go.transform.SetParent(grid, false);
                go.transform.localPosition = right * rx + up * uy + normal * outPos;
                go.transform.localScale = scale;

                var mr = go.GetComponent<MeshRenderer>();
                mr.sharedMaterial = new Material(shader);
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                blocks[b, s] = mr;
            }
        }

        builtBars = bars;
        builtSegments = segments;
    }

    void Update()
    {
        if (blocks == null || builtBars != bars || builtSegments != segments)
            Build();

        audioSource.GetSpectrumData(spectrum, 0, FFTWindow.BlackmanHarris);

        float fall = Mathf.Exp(-Time.deltaTime * balanceFalloff);

        // 1. GEÇİŞ: her bandın ham gücü + zirve takibi + en gür bandın zirvesi
        globalPeak *= fall;
        for (int b = 0; b < bars; b++)
        {
            // Logaritmik bant: alt frekanslar daha geniş temsil edilir
            int lo = Mathf.FloorToInt(Mathf.Pow((float)b / bars, 2f) * spectrum.Length);
            int hi = Mathf.FloorToInt(Mathf.Pow((float)(b + 1) / bars, 2f) * spectrum.Length);
            hi = Mathf.Max(hi, lo + 1);
            float sum = 0f;
            for (int i = lo; i < hi && i < spectrum.Length; i++) sum += spectrum[i];
            float mag = sum / (hi - lo);             // ham büyüklük (sensitivity'siz)

            rawMag[b] = mag;
            barPeak[b] = Mathf.Max(mag, barPeak[b] * fall);   // anında yüksel, yavaş düş
            globalPeak = Mathf.Max(globalPeak, barPeak[b]);
        }
        float gp = Mathf.Max(globalPeak, 1e-6f);

        // 2. GEÇİŞ: dengeli seviye + en gür banda göre gürültü kapısı
        for (int b = 0; b < bars; b++)
        {
            float absolute = Mathf.Clamp01(rawMag[b] * sensitivity);
            float balanced = Mathf.Clamp01(rawMag[b] / Mathf.Max(barPeak[b], 1e-6f));

            // Bandın son zirvesi en gür banda kıyasla çok düşükse gölgele (kendi kendini ölçekler)
            float gate = Mathf.Clamp01(barPeak[b] / (gp * noiseGate + 1e-6f));

            float target = Mathf.Clamp01(Mathf.Lerp(absolute, balanced, autoBalance) * gate);

            float rate = (target > level[b] ? attack : decay) * Time.deltaTime;
            level[b] = Mathf.Lerp(level[b], target, rate);

            int lit = Mathf.RoundToInt(level[b] * segments);
            Color c = barColors[b] * brightness;
            Color dim = barColors[b] * unlitDim;

            for (int s = 0; s < segments; s++)
            {
                var mr = blocks[b, s];
                bool on = s < lit;
                if (!on && !showUnlit) { mr.enabled = false; continue; }
                mr.enabled = true;
                mpb.SetColor(ColorID, on ? c : dim);
                mr.SetPropertyBlock(mpb);
            }
        }
    }

    void GetFaceBasis(out Vector3 right, out Vector3 up, out Vector3 normal)
    {
        switch (face)
        {
            case Face.Front: right = Vector3.right; up = Vector3.up; normal = Vector3.forward; break;
            case Face.Back: right = Vector3.left; up = Vector3.up; normal = Vector3.back; break;
            case Face.Left: right = Vector3.forward; up = Vector3.up; normal = Vector3.left; break;
            case Face.Right: right = Vector3.back; up = Vector3.up; normal = Vector3.right; break;
            case Face.Top: right = Vector3.right; up = Vector3.forward; normal = Vector3.up; break;
            default: right = Vector3.right; up = Vector3.back; normal = Vector3.down; break; // Bottom
        }
    }

    static Vector3 Abs(Vector3 v) => new Vector3(Mathf.Abs(v.x), Mathf.Abs(v.y), Mathf.Abs(v.z));
}