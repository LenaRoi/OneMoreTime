using UnityEngine;

/// <summary>
/// Neon spectrum equalizer panel. Tüm bloklar TEK birleşik mesh olarak çizilir
/// (panel başına 1 renderer, 1 draw call) — yüzlerce küp GameObject yerine.
/// Segmentlerin yanıp sönmesini Custom/EqualizerBars shader'ı yönetir: script her
/// kare sadece sütun başına "yanan segment sayısı"nı (_Levels) MaterialPropertyBlock
/// ile verir. Mesh bir kez kurulur ve durur; Unity'nin kendi frustum culling'i
/// görünmeyenleri bedavaya eler (pop-in yok). Bir duvara dik boş child'a ekle.
/// </summary>
public class AudioRhythmLine : MonoBehaviour
{
    public enum VerticalAnchor { Center, Bottom }

    [Header("Ses Kaynağı")]
    [Tooltip("Açık: sahnedeki ORTAK müziği (AudioListener) dinler; bu objenin kendi AudioSource'u susturulur (yankı olmaz). Kapalı: bu objenin kendi AudioSource'u.")]
    public bool useSharedAudio = true;

    [Header("Panel (Duvar) Boyutu — yerel birim")]
    [Tooltip("Panelin genişliği (koridor boyunca).")]
    public float panelWidth = 4f;
    [Tooltip("Panelin yüksekliği (tabandan tavana).")]
    public float panelHeight = 2f;
    [Tooltip("Blokların hangi yöne çıkacağı: açık = yerel +Z, kapalı = yerel -Z. Duvarın iç yüzüne göre seç.")]
    public bool faceForward = true;
    [Tooltip("Dikey hizalama: Bottom = tabandan yukarı büyür (duvar için ideal), Center = ortalı.")]
    public VerticalAnchor verticalAnchor = VerticalAnchor.Bottom;
    [Tooltip("Yatay eğrilik (derece). 0 = düz duvar. + sola, - sağa büker; dönen koridor duvarına uydurmak için (örn. 90° dönüş ≈ 90).")]
    [Range(-180f, 180f)] public float bendAngle = 0f;

    [Header("Izgara")]
    [Tooltip("Sütun (frekans bandı) sayısı.")]
    [Range(4, 64)] public int bars = 32;
    [Tooltip("Her sütundaki blok (segment) sayısı.")]
    [Range(3, 40)] public int segments = 16;

    [Tooltip("Panelin ne kadarını doldursun (1 = kenardan kenara).")]
    [Range(0.3f, 1f)] public float fillFraction = 0.95f;
    [Tooltip("Bloğun kendi hücresini doldurma oranı (aradaki boşluk).")]
    [Range(0.3f, 1f)] public float blockFill = 0.8f;
    [Tooltip("Blokların dışarı kalınlığı.")]
    public float thickness = 0.03f;
    [Tooltip("Duvardan dışarı küçük kaldırma (z-fighting'i önler).")]
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
    [Tooltip("Gürültü kapısı: en gür banda kıyasla bu oranın altındaki bantlar boş kalır.")]
    [Range(0f, 0.5f)] public float noiseGate = 0.06f;

    [Header("Renk / Neon")]
    [Tooltip("(Artık kullanılmıyor — render Custom/EqualizerBars shader'ı ile yapılıyor.)")]
    public Material blockMaterial;
    [Tooltip("Beyaz tonları arası fark (0 = hepsi saf beyaz, yüksek = bazı sütunlar daha gri).")]
    [Range(0f, 0.8f)] public float shadeVariation = 0.35f;
    [Tooltip("Parlaklık (neon etkisi; Bloom ile daha güçlü görünür).")]
    [Range(1f, 8f)] public float brightness = 2.2f;
    [Tooltip("Sönük (yanmayan) blokları göster. Kapalı = saydam (arka plan görünür).")]
    public bool showUnlit = false;
    [Range(0f, 0.4f)] public float unlitDim = 0.12f;

    [Header("Performans")]
    [Tooltip("Kameraya bu mesafeden uzaktayken güncellenmez/çizilmez. 0 = hep aktif. (Yakındakiler zaten Unity tarafından frustum ile elenir.)")]
    public float maxVisibleDistance = 12f;

    private AudioSource audioSource;
    private float[] spectrum;
    private float[] level;         // sütun seviyeleri 0..1
    private float[] barPeak;       // her sütunun son zirvesi (otomatik kazanç)
    private float[] rawMag;        // her sütunun ham büyüklüğü (bu kare)
    private float globalPeak;      // tüm bantların en gür zirvesi
    private Color[] barColors;

    private GameObject meshGO;
    private MeshRenderer meshRenderer;
    private Mesh mesh;
    private MaterialPropertyBlock mpb;
    private float[] _levels;       // shader'a giden: sütun başına yanan segment sayısı
    private int builtBars, builtSegments;
    private Camera cam;

    private static Material _sharedMat;
    static readonly int LevelsID = Shader.PropertyToID("_Levels");
    static readonly int BrightnessID = Shader.PropertyToID("_Brightness");
    static readonly int UnlitDimID = Shader.PropertyToID("_UnlitDim");
    static readonly int ShowUnlitID = Shader.PropertyToID("_ShowUnlit");

    // Birim küp: 6 yüz × 4 köşe = 24 vertex (ışık için düz yüz normalleri gerekli).
    // Sıra: +X, -X, +Y, -Y, +Z, -Z
    static readonly Vector3[] FACE =
    {
        new Vector3( .5f,-.5f,-.5f), new Vector3( .5f, .5f,-.5f), new Vector3( .5f, .5f, .5f), new Vector3( .5f,-.5f, .5f), // +X
        new Vector3(-.5f,-.5f, .5f), new Vector3(-.5f, .5f, .5f), new Vector3(-.5f, .5f,-.5f), new Vector3(-.5f,-.5f,-.5f), // -X
        new Vector3(-.5f, .5f,-.5f), new Vector3(-.5f, .5f, .5f), new Vector3( .5f, .5f, .5f), new Vector3( .5f, .5f,-.5f), // +Y
        new Vector3(-.5f,-.5f, .5f), new Vector3(-.5f,-.5f,-.5f), new Vector3( .5f,-.5f,-.5f), new Vector3( .5f,-.5f, .5f), // -Y
        new Vector3( .5f,-.5f, .5f), new Vector3( .5f, .5f, .5f), new Vector3(-.5f, .5f, .5f), new Vector3(-.5f,-.5f, .5f), // +Z
        new Vector3(-.5f,-.5f,-.5f), new Vector3(-.5f, .5f,-.5f), new Vector3( .5f, .5f,-.5f), new Vector3( .5f,-.5f,-.5f), // -Z
    };
    static readonly Vector3[] FNRM =
    {
        Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back,
    };
    static readonly int[] FACE_TRIS =
    {
        0,1,2, 0,2,3,      4,5,6, 4,6,7,      8,9,10, 8,10,11,
        12,13,14, 12,14,15, 16,17,18, 16,18,19, 20,21,22, 20,22,23,
    };

    static Material SharedMat
    {
        get
        {
            if (_sharedMat == null)
            {
                var sh = Shader.Find("Custom/EqualizerBars");
                _sharedMat = new Material(sh);
            }
            return _sharedMat;
        }
    }

    void Start()
    {
        if (useSharedAudio)
        {
            var src = GetComponent<AudioSource>();
            if (src != null) { src.playOnAwake = false; src.Stop(); src.enabled = false; }
        }
        else
        {
            audioSource = GetComponent<AudioSource>();
        }
        mpb = new MaterialPropertyBlock();
        // Mesh'i burada KURMUYORUZ — oyuncu yaklaşınca bir kez kur, sonra hep dursun.
    }

    // Ortak (paylaşılan) veya kendi AudioSource'undan spektrum
    float[] GetSpectrum()
    {
        if (useSharedAudio) return AudioSpectrumProvider.GetShared();
        if (audioSource == null) audioSource = GetComponent<AudioSource>();
        if (audioSource == null) return AudioSpectrumProvider.GetShared();
        audioSource.GetSpectrumData(spectrum, 0, FFTWindow.BlackmanHarris);
        return spectrum;
    }

    void Build()
    {
        var old = transform.Find("Equalizer");
        if (old != null) DestroyImmediate(old.gameObject);

        spectrum = new float[Mathf.Max(64, Mathf.NextPowerOfTwo(bars * 4))];
        level = new float[bars];
        barPeak = new float[bars];
        rawMag = new float[bars];
        barColors = new Color[bars];
        _levels = new float[128];   // shader _Levels[128] ile aynı boyut
        if (mpb == null) mpb = new MaterialPropertyBlock();

        float dir = faceForward ? 1f : -1f;
        float width = panelWidth * fillFraction;
        float height = panelHeight * fillFraction;
        float barPitch = width / bars;
        float segPitch = height / segments;
        float blockW = barPitch * blockFill;
        float blockH = segPitch * blockFill;
        float bottom = verticalAnchor == VerticalAnchor.Bottom ? 0f : -height * 0.5f;
        float radialOffset = surfaceOffset + thickness * 0.5f;

        float bendRad = bendAngle * Mathf.Deg2Rad;
        bool bent = Mathf.Abs(bendRad) > 1e-4f;
        float radius = bent ? width / bendRad : 0f;

        Vector3 scale = new Vector3(blockW, blockH, thickness);

        int cubeCount = bars * segments;
        var verts = new Vector3[cubeCount * 24];
        var normals = new Vector3[cubeCount * 24];
        var colors = new Color[cubeCount * 24];
        var uvs = new Vector2[cubeCount * 24];
        var tris = new int[cubeCount * 36];
        int ci = 0;

        for (int b = 0; b < bars; b++)
        {
            float t = bars <= 1 ? 1f : (float)b / (bars - 1);
            float shade = Mathf.Lerp(1f - shadeVariation, 1f, t);
            barColors[b] = new Color(shade, shade, shade, 1f);

            float frac = (b + 0.5f) / bars;
            Vector3 barPos;
            Quaternion barRot;
            if (bent)
            {
                float theta = (frac - 0.5f) * bendRad;
                barPos = new Vector3(radius * Mathf.Sin(theta), 0f, radius * (Mathf.Cos(theta) - 1f));
                barRot = Quaternion.Euler(0f, theta * Mathf.Rad2Deg, 0f);
            }
            else
            {
                barPos = new Vector3((frac - 0.5f) * width, 0f, 0f);
                barRot = Quaternion.identity;
            }
            Vector3 normalDir = barRot * Vector3.forward * dir;

            for (int s = 0; s < segments; s++)
            {
                float uy = bottom + (s + 0.5f) * segPitch;
                Vector3 center = barPos + Vector3.up * uy + normalDir * radialOffset;

                int vBase = ci * 24;
                for (int f = 0; f < 6; f++)
                {
                    Vector3 n = barRot * FNRM[f];
                    for (int c = 0; c < 4; c++)
                    {
                        int idx = vBase + f * 4 + c;
                        verts[idx] = center + barRot * Vector3.Scale(FACE[f * 4 + c], scale);
                        normals[idx] = n;
                        colors[idx] = barColors[b];
                        uvs[idx] = new Vector2(b, s);   // shader: bar & segment index
                    }
                }
                int tBase = ci * 36;
                for (int k = 0; k < 36; k++)
                    tris[tBase + k] = vBase + FACE_TRIS[k];

                ci++;
            }
        }

        mesh = new Mesh { name = "EqualizerMesh" };
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;   // çok vertex olabilir
        mesh.vertices = verts;
        mesh.normals = normals;      // sahne ışığı için gerekli
        mesh.colors = colors;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateBounds();

        meshGO = new GameObject("Equalizer");
        meshGO.transform.SetParent(transform, false);
        var mf = meshGO.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;
        meshRenderer = meshGO.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = SharedMat;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;

        // Sabit ayarları mpb'ye bir kez yaz (level'lar her kare güncellenir).
        mpb.SetFloat(BrightnessID, brightness);
        mpb.SetFloat(UnlitDimID, unlitDim);
        mpb.SetFloat(ShowUnlitID, showUnlit ? 1f : 0f);
        mpb.SetFloatArray(LevelsID, _levels);
        meshRenderer.SetPropertyBlock(mpb);

        builtBars = bars;
        builtSegments = segments;
    }

    void Teardown()
    {
        if (meshGO != null) Destroy(meshGO);
        if (mesh != null) Destroy(mesh);
        meshGO = null;
        meshRenderer = null;
        mesh = null;
        builtBars = builtSegments = 0;
    }

    void OnDisable()
    {
        if (meshGO != null) Teardown();
    }

    void Update()
    {
        // Mesafe kapısı: uzaktakiler için ne mesh çiz ne de spektrum hesapla.
        // (Mesh YIKILMAZ; sadece gizlenir → tekrar yaklaşınca ANINDA gelir, pop-in yok.)
        if (maxVisibleDistance > 0f)
        {
            if (cam == null) cam = Camera.main;
            if (cam != null)
            {
                float md = maxVisibleDistance * 1.35f;
                if ((cam.transform.position - transform.position).sqrMagnitude > md * md)
                {
                    if (meshGO != null && meshGO.activeSelf) meshGO.SetActive(false);
                    return;
                }
            }
        }

        if (meshGO == null || builtBars != bars || builtSegments != segments) Build();
        if (meshGO != null && !meshGO.activeSelf) meshGO.SetActive(true);

        float[] spec = GetSpectrum();
        float fall = Mathf.Exp(-Time.deltaTime * balanceFalloff);

        // 1. GEÇİŞ: her bandın ham gücü + zirve takibi + en gür bandın zirvesi
        globalPeak *= fall;
        for (int b = 0; b < bars; b++)
        {
            int lo = Mathf.FloorToInt(Mathf.Pow((float)b / bars, 2f) * spec.Length);
            int hi = Mathf.FloorToInt(Mathf.Pow((float)(b + 1) / bars, 2f) * spec.Length);
            hi = Mathf.Max(hi, lo + 1);
            float sum = 0f;
            for (int i = lo; i < hi && i < spec.Length; i++) sum += spec[i];
            float mag = sum / (hi - lo);

            rawMag[b] = mag;
            barPeak[b] = Mathf.Max(mag, barPeak[b] * fall);
            globalPeak = Mathf.Max(globalPeak, barPeak[b]);
        }
        float gp = Mathf.Max(globalPeak, 1e-6f);

        // 2. GEÇİŞ: dengeli seviye → sütun başına yanan segment sayısı (_Levels)
        for (int b = 0; b < bars; b++)
        {
            float absolute = Mathf.Clamp01(rawMag[b] * sensitivity);
            float balanced = Mathf.Clamp01(rawMag[b] / Mathf.Max(barPeak[b], 1e-6f));
            float gate = Mathf.Clamp01(barPeak[b] / (gp * noiseGate + 1e-6f));
            float target = Mathf.Clamp01(Mathf.Lerp(absolute, balanced, autoBalance) * gate);

            float rate = (target > level[b] ? attack : decay) * Time.deltaTime;
            level[b] = Mathf.Lerp(level[b], target, rate);

            _levels[b] = Mathf.RoundToInt(level[b] * segments);
        }

        // Tek çağrı: shader tüm segmentleri _Levels'a göre yakar/söndürür.
        mpb.SetFloatArray(LevelsID, _levels);
        mpb.SetFloat(BrightnessID, brightness);
        mpb.SetFloat(UnlitDimID, unlitDim);
        mpb.SetFloat(ShowUnlitID, showUnlit ? 1f : 0f);
        meshRenderer.SetPropertyBlock(mpb);
    }
}
