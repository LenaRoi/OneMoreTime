using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Eğri duvarı takip eden neon spectrum equalizer. Marker'lardan geçen Catmull-Rom
/// eğrisi boyunca sütunlar dizilir. Tüm bloklar TEK birleşik mesh olarak çizilir
/// (panel başına 1 renderer). Segment yanıp sönmesini Custom/EqualizerBars shader'ı
/// yönetir (script sadece _Levels'ı günceller). Mesh bir kez kurulur ve durur.
/// </summary>
public class AudioEqualizerCurved : MonoBehaviour
{
    public enum VerticalAnchor { Center, Bottom }

    [Header("Ses Kaynağı")]
    [Tooltip("Açık: sahnedeki ORTAK müziği (AudioListener) dinler; bu objenin kendi AudioSource'u susturulur (yankı olmaz). Kapalı: bu objenin kendi AudioSource'u.")]
    public bool useSharedAudio = true;

    [Header("Duvar Yolu (marker'lar)")]
    [Tooltip("Duvar boyunca yerleştirdiğin boş obje marker'ları (soldan sağa sırayla). 2 nokta = düz, 3+ = eğri.")]
    public Transform[] pathPoints;
    [Tooltip("Blokların hangi yöne çıkacağı (duvarın iç yüzüne göre). Ters çevirmek için işaretini değiştir.")]
    public bool faceForward = true;

    [Header("Panel Yüksekliği")]
    [Tooltip("Panelin yüksekliği (tabandan tavana), yerel birim.")]
    public float panelHeight = 2f;
    [Tooltip("Dikey hizalama: Bottom = yol çizgisinden yukarı büyür, Center = çizginin ortasında.")]
    public VerticalAnchor verticalAnchor = VerticalAnchor.Bottom;

    [Header("Izgara")]
    [Range(4, 128)] public int bars = 32;
    [Range(3, 40)] public int segments = 16;
    [Tooltip("Yolun ne kadarını doldursun (1 = baştan sona).")]
    [Range(0.3f, 1f)] public float fillFraction = 0.98f;
    [Tooltip("Bloğun kendi hücresini doldurma oranı (aradaki boşluk).")]
    [Range(0.3f, 1f)] public float blockFill = 0.8f;
    [Tooltip("Blokların dışarı kalınlığı.")]
    public float thickness = 0.03f;
    [Tooltip("Duvardan dışarı küçük kaldırma (z-fighting'i önler).")]
    public float surfaceOffset = 0.01f;

    [Header("Tepki")]
    public float sensitivity = 200f;
    [Range(1f, 30f)] public float attack = 25f;
    [Range(1f, 30f)] public float decay = 8f;

    [Header("Dengeleme (Otomatik Kazanç)")]
    [Range(0f, 1f)] public float autoBalance = 0.8f;
    [Range(0.1f, 5f)] public float balanceFalloff = 0.6f;
    [Range(0f, 0.5f)] public float noiseGate = 0.06f;

    [Header("Renk / Neon")]
    [Tooltip("(Artık kullanılmıyor — render Custom/EqualizerBars shader'ı ile yapılıyor.)")]
    public Material blockMaterial;
    [Range(0f, 0.8f)] public float shadeVariation = 0.35f;
    [Range(1f, 8f)] public float brightness = 2.2f;
    [Tooltip("Sönük (yanmayan) blokları göster. Kapalı = saydam (arka plan görünür).")]
    public bool showUnlit = false;
    [Range(0f, 0.4f)] public float unlitDim = 0.12f;

    [Header("Performans")]
    [Tooltip("Kameraya bu mesafeden uzaktayken güncellenmez/çizilmez. 0 = hep aktif.")]
    public float maxVisibleDistance = 40f;

    private AudioSource audioSource;
    private float[] spectrum;
    private float[] level;
    private float[] barPeak;
    private float[] rawMag;
    private float globalPeak;
    private Color[] barColors;

    private GameObject meshGO;
    private MeshRenderer meshRenderer;
    private Mesh mesh;
    private MaterialPropertyBlock mpb;
    private float[] _levels;
    private int builtBars, builtSegments;
    private Camera cam;
    private bool _warnedNoPath;

    // eğri örnekleme tamponları
    private readonly List<Vector3> _samples = new List<Vector3>();
    private readonly List<float> _cumLen = new List<float>();
    private float _totalLen;

    private static Material _sharedMat;
    static readonly int LevelsID = Shader.PropertyToID("_Levels");
    static readonly int BrightnessID = Shader.PropertyToID("_Brightness");
    static readonly int UnlitDimID = Shader.PropertyToID("_UnlitDim");
    static readonly int ShowUnlitID = Shader.PropertyToID("_ShowUnlit");

    // Birim küp: 6 yüz × 4 köşe = 24 vertex (ışık için düz yüz normalleri). Sıra: +X,-X,+Y,-Y,+Z,-Z
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

    [ContextMenu("Rebuild")]
    public void Build()
    {
        var old = transform.Find("CurvedEqualizer");
        if (old != null) DestroyImmediate(old.gameObject);

        if (pathPoints == null || pathPoints.Length < 2)
        {
            if (!_warnedNoPath) { Debug.LogWarning($"{name}: En az 2 path marker gerekli.", this); _warnedNoPath = true; }
            return;
        }

        spectrum = new float[Mathf.Max(64, Mathf.NextPowerOfTwo(bars * 4))];
        level = new float[bars];
        barPeak = new float[bars];
        rawMag = new float[bars];
        barColors = new Color[bars];
        _levels = new float[128];   // shader _Levels[128] ile aynı boyut
        if (mpb == null) mpb = new MaterialPropertyBlock();

        SampleCurve();

        float dir = faceForward ? 1f : -1f;
        float usableLen = _totalLen * fillFraction;
        float startLen = (_totalLen - usableLen) * 0.5f;      // ortala
        float barPitch = usableLen / bars;
        float height = panelHeight;
        float segPitch = height / segments;
        float blockW = barPitch * blockFill;
        float blockH = segPitch * blockFill;
        float bottom = verticalAnchor == VerticalAnchor.Bottom ? 0f : -height * 0.5f;
        float radialOffset = surfaceOffset + thickness * 0.5f;

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
            float shadeT = bars <= 1 ? 1f : (float)b / (bars - 1);
            float shade = Mathf.Lerp(1f - shadeVariation, 1f, shadeT);
            barColors[b] = new Color(shade, shade, shade, 1f);

            float dist = startLen + (b + 0.5f) * barPitch;
            Vector3 pos, tangent;
            EvaluateAt(dist, out pos, out tangent);

            Vector3 up = Vector3.up;
            Vector3 normal = Vector3.Cross(up, tangent).normalized * dir;   // duvardan dışa
            if (normal.sqrMagnitude < 1e-6f) normal = Vector3.forward * dir;
            Quaternion rot = Quaternion.LookRotation(normal, up);

            for (int s = 0; s < segments; s++)
            {
                float uy = bottom + (s + 0.5f) * segPitch;
                Vector3 center = pos + up * uy + normal * radialOffset;

                int vBase = ci * 24;
                for (int f = 0; f < 6; f++)
                {
                    Vector3 nrm = rot * FNRM[f];
                    for (int c = 0; c < 4; c++)
                    {
                        int idx = vBase + f * 4 + c;
                        verts[idx] = center + rot * Vector3.Scale(FACE[f * 4 + c], scale);
                        normals[idx] = nrm;
                        colors[idx] = barColors[b];
                        uvs[idx] = new Vector2(b, s);
                    }
                }
                int tBase = ci * 36;
                for (int k = 0; k < 36; k++)
                    tris[tBase + k] = vBase + FACE_TRIS[k];

                ci++;
            }
        }

        mesh = new Mesh { name = "CurvedEqualizerMesh" };
        mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;   // çok vertex olabilir
        mesh.vertices = verts;
        mesh.normals = normals;      // sahne ışığı için gerekli
        mesh.colors = colors;
        mesh.uv = uvs;
        mesh.triangles = tris;
        mesh.RecalculateBounds();

        meshGO = new GameObject("CurvedEqualizer");
        meshGO.transform.SetParent(transform, false);
#if UNITY_EDITOR
        if (!Application.isPlaying) meshGO.hideFlags = HideFlags.DontSaveInEditor;
#endif
        var mf = meshGO.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;
        meshRenderer = meshGO.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = SharedMat;
        meshRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;

        // Play modunda sönük başla; editör önizlemesinde panel görünür olsun diye dolu.
        float initLevel = Application.isPlaying ? 0f : segments;
        for (int b = 0; b < bars; b++) _levels[b] = initLevel;

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
        if (meshGO != null) SafeDestroy(meshGO);
        if (mesh != null) SafeDestroy(mesh);
        meshGO = null;
        meshRenderer = null;
        mesh = null;
        builtBars = builtSegments = 0;
    }

    void OnDisable()
    {
        if (meshGO != null) Teardown();
    }

    // Edit modunda Destroy hata verir; oynatmıyorsak DestroyImmediate kullan.
    static void SafeDestroy(Object o)
    {
        if (o == null) return;
        if (Application.isPlaying) Destroy(o);
        else DestroyImmediate(o);
    }

    // Marker'lardan geçen Catmull-Rom eğrisini yoğun örnekle, yay uzunluğunu çıkar
    void SampleCurve()
    {
        _samples.Clear();
        _cumLen.Clear();

        int n = pathPoints.Length;
        var p = new Vector3[n];
        for (int i = 0; i < n; i++)
            p[i] = pathPoints[i] != null ? transform.InverseTransformPoint(pathPoints[i].position) : Vector3.zero;

        const int per = 24;
        for (int seg = 0; seg < n - 1; seg++)
        {
            Vector3 p0 = p[Mathf.Max(seg - 1, 0)];
            Vector3 p1 = p[seg];
            Vector3 p2 = p[seg + 1];
            Vector3 p3 = p[Mathf.Min(seg + 2, n - 1)];
            for (int j = 0; j < per; j++)
                _samples.Add(CatmullRom(p0, p1, p2, p3, (float)j / per));
        }
        _samples.Add(p[n - 1]);

        _totalLen = 0f;
        _cumLen.Add(0f);
        for (int i = 1; i < _samples.Count; i++)
        {
            _totalLen += Vector3.Distance(_samples[i - 1], _samples[i]);
            _cumLen.Add(_totalLen);
        }
    }

    // Yol başından 'dist' uzaklıkta konum ve teğet
    void EvaluateAt(float dist, out Vector3 pos, out Vector3 tangent)
    {
        dist = Mathf.Clamp(dist, 0f, _totalLen);
        int i = 1;
        while (i < _cumLen.Count && _cumLen[i] < dist) i++;
        i = Mathf.Clamp(i, 1, _samples.Count - 1);

        float segLen = _cumLen[i] - _cumLen[i - 1];
        float t = segLen > 1e-6f ? (dist - _cumLen[i - 1]) / segLen : 0f;
        pos = Vector3.Lerp(_samples[i - 1], _samples[i], t);
        tangent = (_samples[i] - _samples[i - 1]).normalized;
    }

    static Vector3 CatmullRom(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float t)
    {
        float t2 = t * t, t3 = t2 * t;
        return 0.5f * ((2f * p1) + (-p0 + p2) * t
            + (2f * p0 - 5f * p1 + 4f * p2 - p3) * t2
            + (-p0 + 3f * p1 - 3f * p2 + p3) * t3);
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

    void Update()
    {
        // Mesafe kapısı: uzaktakiler gizlenir (mesh YIKILMAZ → tekrar yaklaşınca anında gelir).
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

        if (meshGO == null || builtBars != bars || builtSegments != segments)
        {
            if (pathPoints == null || pathPoints.Length < 2) return;   // build spam'ini önle
            Build();
            if (meshGO == null) return;
        }
        if (meshGO != null && !meshGO.activeSelf) meshGO.SetActive(true);

        float[] spec = GetSpectrum();
        float fall = Mathf.Exp(-Time.deltaTime * balanceFalloff);

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

        mpb.SetFloatArray(LevelsID, _levels);
        mpb.SetFloat(BrightnessID, brightness);
        mpb.SetFloat(UnlitDimID, unlitDim);
        mpb.SetFloat(ShowUnlitID, showUnlit ? 1f : 0f);
        meshRenderer.SetPropertyBlock(mpb);
    }

    // Editörde bar'ların izleyeceği GERÇEK eğriyi (pürüzsüz) çiz
    void OnDrawGizmosSelected()
    {
        if (pathPoints == null || pathPoints.Length < 2) return;

        int n = pathPoints.Length;
        var p = new Vector3[n];
        for (int i = 0; i < n; i++) p[i] = pathPoints[i] != null ? pathPoints[i].position : Vector3.zero;

        Gizmos.color = Color.cyan;
        foreach (var pt in p) Gizmos.DrawWireSphere(pt, 0.08f);

        Gizmos.color = new Color(0f, 1f, 1f, 0.9f);
        const int per = 24;
        Vector3 prev = p[0];
        for (int seg = 0; seg < n - 1; seg++)
        {
            Vector3 p0 = p[Mathf.Max(seg - 1, 0)];
            Vector3 p1 = p[seg];
            Vector3 p2 = p[seg + 1];
            Vector3 p3 = p[Mathf.Min(seg + 2, n - 1)];
            for (int j = 1; j <= per; j++)
            {
                Vector3 cur = CatmullRom(p0, p1, p2, p3, (float)j / per);
                Gizmos.DrawLine(prev, cur);
                prev = cur;
            }
        }
    }
}
