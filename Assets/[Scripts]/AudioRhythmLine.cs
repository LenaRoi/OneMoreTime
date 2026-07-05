using UnityEngine;

/// <summary>
/// Neon spectrum equalizer panel. Builds a grid of little cube blocks in the
/// LOCAL space of this transform (right = local X, up = local Y, blocks extrude
/// along local Z). Each column is a frequency band; stacked segments light up
/// with the music (rainbow), like a classic equalizer. Attach to an empty child
/// placed flat against a wall (e.g. a corridor tile's left wall): set the panel
/// width/height to the wall area and rotate the transform so local Z faces into
/// the room.
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
    [Tooltip("Blokların materyali. BUILD'de görünmesi için buraya bir materyal ata (kod içi Shader.Find build'de silinebilir). GameManager'daki materyali kullanabilirsin.")]
    public Material blockMaterial;
    [Tooltip("Beyaz tonları arası fark (0 = hepsi saf beyaz, yüksek = bazı sütunlar daha gri).")]
    [Range(0f, 0.8f)] public float shadeVariation = 0.35f;
    [Tooltip("Parlaklık (neon etkisi; Bloom ile daha güçlü görünür).")]
    [Range(1f, 8f)] public float brightness = 2.2f;
    [Tooltip("Sönük (yanmayan) blokları göster. Kapalı = saydam (arka plan görünür).")]
    public bool showUnlit = false;
    [Range(0f, 0.4f)] public float unlitDim = 0.12f;

    [Header("Performans")]
    [Tooltip("Kameraya bu mesafeden uzaktayken güncellenmez ve çizilmez (uzaktakiler FPS düşürmesin). 0 = hep aktif.")]
    public float maxVisibleDistance = 12f;

    private AudioSource audioSource;
    private float[] spectrum;
    private float[] level;         // sütun seviyeleri 0..1
    private float[] barPeak;       // her sütunun son zirvesi (otomatik kazanç)
    private float[] rawMag;        // her sütunun ham büyüklüğü (bu kare)
    private float globalPeak;      // tüm bantların en gür zirvesi
    private MeshRenderer[,] blocks;
    private MaterialPropertyBlock mpb;
    private Color[] barColors;
    private Material[] barMaterials;
    private Transform grid;
    private int builtBars, builtSegments;
    private int[] prevLit;      // her sütunun bir önceki yanan blok sayısı (delta güncelleme)
    private Camera cam;
    private bool culled;

    static readonly int ColorID = Shader.PropertyToID("_Color");
    static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");
    private int _colorProp = Shader.PropertyToID("_Color");

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
        // NOT: Izgarayı burada KURMUYORUZ. Yüzlerce equalizer × 512 küp = onbinlerce
        // obje demek. Bunun yerine oyuncu yaklaşınca kur, uzaklaşınca TAMAMEN yık.
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
        barMaterials = new Material[bars];
        prevLit = new int[bars];
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

        // Eğrilik: sütunlar Y ekseni etrafında bir yay boyunca dizilir.
        // width = yay uzunluğu; bendAngle toplam açı. 0 ise düz.
        float bendRad = bendAngle * Mathf.Deg2Rad;
        bool bent = Mathf.Abs(bendRad) > 1e-4f;
        float radius = bent ? width / bendRad : 0f;

        Vector3 scale = new Vector3(blockW, blockH, thickness);
        // Materyal ata (build'de görünür); yoksa Unlit/Color'a düş
        Material template = blockMaterial != null ? blockMaterial : new Material(Shader.Find("Unlit/Color"));
        _colorProp = template.HasProperty(BaseColorID) ? BaseColorID : ColorID;

        for (int b = 0; b < bars; b++)
        {
            // Beyazın tonları: sütunlar arası hafif gri-beyaz değişim
            float t = bars <= 1 ? 1f : (float)b / (bars - 1);
            float shade = Mathf.Lerp(1f - shadeVariation, 1f, t);
            barColors[b] = new Color(shade, shade, shade, 1f);
            // Sütun başına TEK materyal (blok başına değil) → çok daha az materyal
            barMaterials[b] = new Material(template) { enableInstancing = true };

            // Bu sütunun yay üzerindeki konumu ve dönüşü
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
            // Blokların dışarı (radyal) çıkış yönü
            Vector3 normalDir = barRot * Vector3.forward * dir;

            Color cLit = barColors[b] * brightness;
            Color cDim = barColors[b] * unlitDim;

            for (int s = 0; s < segments; s++)
            {
                float uy = bottom + (s + 0.5f) * segPitch;
                var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
                go.name = $"blk_{b}_{s}";
                var col = go.GetComponent<Collider>();
                if (col != null) DestroyImmediate(col);
                go.transform.SetParent(grid, false);
                go.transform.localPosition = barPos + Vector3.up * uy + normalDir * radialOffset;
                go.transform.localRotation = barRot;
                go.transform.localScale = scale;

                var mr = go.GetComponent<MeshRenderer>();
                mr.sharedMaterial = barMaterials[b];
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                mr.receiveShadows = false;
                // Başlangıç durumu: hepsi "sönük". Yanan renk sabit olduğundan bir
                // kez ayarlanır; çalışırken sadece açık/kapalı değişir (SetPropertyBlock yok).
                if (showUnlit) { mpb.SetColor(_colorProp, cDim); mr.SetPropertyBlock(mpb); }
                else { mpb.SetColor(_colorProp, cLit); mr.SetPropertyBlock(mpb); mr.enabled = false; }
                blocks[b, s] = mr;
            }
            prevLit[b] = 0;
        }

        builtBars = bars;
        builtSegments = segments;
    }

    // Küpleri ve runtime materyalleri TAMAMEN yok et (uzaklaşınca çağrılır).
    void Teardown()
    {
        if (barMaterials != null)
            for (int i = 0; i < barMaterials.Length; i++)
                if (barMaterials[i] != null) Destroy(barMaterials[i]);

        if (grid != null) Destroy(grid.gameObject);
        grid = null;
        blocks = null;
        barMaterials = null;
        prevLit = null;
        builtBars = builtSegments = 0;
        culled = false;
    }

    void OnDisable()
    {
        // Sahne/obje kapanırken sızıntı olmasın.
        if (blocks != null) Teardown();
    }

    void Update()
    {
        // --- Mesafeye göre TEMBEL KURULUM / TAM YIKIM (asıl FPS kazancı burada) ---
        // Uzaktaki equalizer'lar sadece gizlenmez, küpleri komple yok edilir; böylece
        // oyuncu ileri gidince arkadaki onbinlerce küp bellekten/CPU'dan tamamen kalkar.
        if (maxVisibleDistance > 0f)
        {
            if (cam == null) cam = Camera.main;
            if (cam != null)
            {
                Vector3 toObj = transform.position - cam.transform.position;
                float sqrDist = toObj.sqrMagnitude;
                float md = maxVisibleDistance;

                // 1) Çok uzak → yık. (histerezis: yapım menzilinin 1.35 katı)
                float tearDist = md * 1.35f;
                if (sqrDist > tearDist * tearDist)
                {
                    if (blocks != null) Teardown();
                    return;
                }

                // 2) Kameranın ARKASINDA olanları kurma. Duvardaki bar'ları arkanı
                //    dönmüşken çizmenin anlamı yok — koridorda yükün yarısı bu.
                //    Çok yakındakiler (alwaysKeep) yönden bağımsız hep dursun ki
                //    dönerken kenardaki bar aniden kaybolmasın.
                const float alwaysKeep = 8f;
                if (sqrDist > alwaysKeep * alwaysKeep)
                {
                    float facing = Vector3.Dot(toObj.normalized, cam.transform.forward);
                    if (facing < -0.15f)   // belirgin şekilde arkada
                    {
                        if (blocks != null) Teardown();
                        return;
                    }
                }

                // 3) Menzilde ve önde → kurulmadıysa kur.
                if (blocks == null) Build();
            }
        }

        if (blocks == null || builtBars != bars || builtSegments != segments)
            Build();

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

        // 2. GEÇİŞ: dengeli seviye + gürültü kapısı + blokları güncelle
        for (int b = 0; b < bars; b++)
        {
            float absolute = Mathf.Clamp01(rawMag[b] * sensitivity);
            float balanced = Mathf.Clamp01(rawMag[b] / Mathf.Max(barPeak[b], 1e-6f));
            float gate = Mathf.Clamp01(barPeak[b] / (gp * noiseGate + 1e-6f));
            float target = Mathf.Clamp01(Mathf.Lerp(absolute, balanced, autoBalance) * gate);

            float rate = (target > level[b] ? attack : decay) * Time.deltaTime;
            level[b] = Mathf.Lerp(level[b], target, rate);

            // Sadece DURUMU DEĞİŞEN blokları güncelle (her kare hepsini değil)
            int lit = Mathf.RoundToInt(level[b] * segments);
            int prev = prevLit[b];
            if (lit != prev)
            {
                if (lit > prev)
                {
                    Color c = barColors[b] * brightness;
                    for (int s = prev; s < lit; s++)
                    {
                        var mr = blocks[b, s];
                        if (showUnlit) { mpb.SetColor(_colorProp, c); mr.SetPropertyBlock(mpb); }
                        else mr.enabled = true;
                    }
                }
                else
                {
                    Color dim = barColors[b] * unlitDim;
                    for (int s = lit; s < prev; s++)
                    {
                        var mr = blocks[b, s];
                        if (showUnlit) { mpb.SetColor(_colorProp, dim); mr.SetPropertyBlock(mpb); }
                        else mr.enabled = false;
                    }
                }
                prevLit[b] = lit;
            }
        }
    }
}
