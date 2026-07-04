using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// "No entry" mark (a circle with an X) for a dead-end wall. It stays hidden
/// until the player walks up to it, then reveals and pulses to the beat using
/// the same shared audio spectrum as the equalizers (AudioSpectrumProvider).
/// Attach to an empty placed flat on the dead-end wall, facing into the corridor
/// (local +Z pointing toward the player / walkable space).
/// </summary>
public class DeadEndMark : MonoBehaviour
{
    [Header("Görünürlük (Oyuncuya göre)")]
    [Tooltip("Her zaman görünür (mesafe/bakış kontrolü yok). Test veya kalıcı işaret için.")]
    public bool alwaysVisible = false;
    [Tooltip("Boş bırakılırsa 'Player' tag'i, yoksa ana kamera otomatik bulunur.")]
    public Transform player;
    [Tooltip("İşaretin belireceği mesafe.")]
    public float triggerDistance = 6f;
    [Tooltip("Belirme/kaybolma hızı.")]
    [Range(0.5f, 20f)] public float revealSpeed = 6f;
    [Tooltip("Sadece oyuncu buraya doğru bakarken/yürürken göster.")]
    public bool requireFacing = true;
    [Tooltip("Bakış toleransı (1 = tam karşıdan, düşük = geniş açı).")]
    [Range(0f, 1f)] public float facingDot = 0.25f;

    [Header("İşaret Şekli")]
    [Tooltip("Çemberin yarıçapı (yerel birim).")]
    public float radius = 0.6f;
    [Tooltip("Çemberdeki nokta sayısı (yumuşaklık).")]
    [Range(12, 96)] public int circleSegments = 48;
    [Tooltip("Çarpının çember içindeki büyüklüğü (0.7 ≈ kenara değer).")]
    [Range(0.3f, 1f)] public float crossScale = 0.68f;
    [Tooltip("Çizgi kalınlığı.")]
    public float lineWidth = 0.12f;
    [Tooltip("Duvardan dışarı kaldırma (z-fighting'i önler).")]
    public float surfaceOffset = 0.02f;

    [Header("Renk / Neon")]
    [Tooltip("Çizgi materyali. BUILD'de görünmesi için buraya bir materyal ata. Gerçek METALİK için Lit (Standard/URP) bir materyal ver; Unlit düz görünür.")]
    public Material lineMaterial;
    [Tooltip("Metalik mor.")]
    public Color color = new Color(0.55f, 0.15f, 0.85f, 1f);
    [Range(1f, 8f)] public float brightness = 2.5f;

    [Header("Stil (Kaligrafi)")]
    [Tooltip("Kalın-ince değişen çizgi + el çizimi dalgalanma.")]
    public bool calligraphy = true;
    [Tooltip("Kalın/ince farkı (0 = düz kalınlık).")]
    [Range(0f, 1f)] public float widthVariation = 0.7f;
    [Tooltip("El çizimi dalgalanma miktarı.")]
    [Range(0f, 0.3f)] public float jitter = 0.08f;
    [Tooltip("Yavaş dönüş hızı (derece/sn). 0 = dönme.")]
    [Range(0f, 180f)] public float spinSpeed = 20f;

    [Header("Jinx Çarpı (graffiti X)")]
    [Tooltip("Çarpı kollarının çemberi ne kadar aşacağı ('üstünü çizme' hissi).")]
    [Range(0f, 0.6f)] public float crossOvershoot = 0.28f;
    [Tooltip("Çarpının ekstra dağınıklığı (Jinx graffiti hissi).")]
    [Range(1f, 4f)] public float crossChaos = 2.2f;
    [Tooltip("Çarpı kollarının çembere göre kalınlık çarpanı.")]
    [Range(1f, 3f)] public float crossThickness = 1.4f;

    [Header("Damla (Sprey Boya)")]
    [Tooltip("Aşağı akan boya damlaları ekle.")]
    public bool drips = true;
    [Tooltip("Damla sayısı.")]
    [Range(0, 8)] public int dripCount = 4;
    [Tooltip("Damla uzunluğu (yarıçap çarpanı).")]
    [Range(0.1f, 1.5f)] public float dripLength = 0.6f;

    [Header("Ritim Nabzı (equalizer ile aynı mantık)")]
    [Tooltip("Beat'te ne kadar büyüsün (0.25 = %25).")]
    [Range(0f, 1f)] public float pulseAmount = 0.25f;
    [Tooltip("Nabzın sönme hızı (yüksek = kısa/keskin).")]
    [Range(1f, 30f)] public float pulseDecay = 9f;
    [Tooltip("Beat eşiği: bas enerjisi ortalamanın kaç katına çıkınca beat.")]
    [Range(1.05f, 3f)] public float beatThreshold = 1.4f;
    [Tooltip("İki beat arası en kısa süre.")]
    public float minBeatInterval = 0.12f;

    private Transform mark;               // ölçeklenen kapsayıcı
    private readonly List<LineRenderer> lines = new List<LineRenderer>();
    private float reveal;                 // 0..1 belirme
    private float pulse;                  // beat nabzı
    private float energyAvg = 0.0001f;
    private float beatTimer;

    void Start()
    {
        if (player == null)
        {
            var p = GameObject.FindWithTag("Player");
            if (p != null) player = p.transform;
            else if (Camera.main != null) player = Camera.main.transform;
        }
        BuildMark();
    }

    void BuildMark()
    {
        var old = transform.Find("DeadEndMark");
        if (old != null) DestroyImmediate(old.gameObject);

        mark = new GameObject("DeadEndMark").transform;
        mark.SetParent(transform, false);
        mark.localPosition = new Vector3(0f, 0f, surfaceOffset);
        lines.Clear();

        // Materyal ata (build'de görünür); yoksa Unlit/Color'a düş
        Color tint = color * brightness;
        Material mat = lineMaterial != null ? new Material(lineMaterial) : new Material(Shader.Find("Unlit/Color"));
        if (mat.HasProperty(Shader.PropertyToID("_BaseColor"))) mat.SetColor("_BaseColor", tint);
        if (mat.HasProperty(Shader.PropertyToID("_Color"))) mat.color = tint;

        float seed = Random.value * 100f;

        // Çember
        var circle = NewLine("Circle", mat, circleSegments + 1, true);
        for (int i = 0; i <= circleSegments; i++)
        {
            float a = (float)i / circleSegments * Mathf.PI * 2f;
            float rr = radius;
            if (calligraphy)   // el çizimi: yarıçapı hafifçe dalgalandır
                rr *= 1f + jitter * (Mathf.PerlinNoise(Mathf.Cos(a) * 1.6f + seed, Mathf.Sin(a) * 1.6f + seed) - 0.5f) * 2f;
            circle.SetPosition(i, new Vector3(Mathf.Cos(a) * rr, Mathf.Sin(a) * rr, 0f));
        }
        if (calligraphy)
            circle.widthCurve = CalligraphyCurve(2, 1f - widthVariation, 1f);   // 2 kalın lob (geniş uçlu kalem)

        // Jinx çarpı: kollar çemberi aşar, dağınık spreylenmiş X
        float d = radius * (1f + crossOvershoot) * 0.70711f;
        NewStroke("Cross1", mat, new Vector3(-d, -d, 0f), new Vector3(d, d, 0f), seed + 5f);
        NewStroke("Cross2", mat, new Vector3(-d, d, 0f), new Vector3(d, -d, 0f), seed + 9f);

        // Sprey damlaları
        AddDrips(mat, d, seed);

        SetVisible(false);
    }

    // Aşağı akan boya damlaları: çarpı uçları ve çember altından
    void AddDrips(Material mat, float d, float seed)
    {
        if (!drips || dripCount <= 0) return;

        // Damla başlangıç noktaları: 4 çarpı ucu + çember altı
        var origins = new List<Vector3>
        {
            new Vector3(-d, -d, 0f), new Vector3(d, -d, 0f),   // alt uçlar (damla için en doğal)
            new Vector3(-d, d, 0f),  new Vector3(d, d, 0f),
        };
        for (int i = 0; i < 3; i++)
        {
            float a = Mathf.Lerp(Mathf.PI * 1.15f, Mathf.PI * 1.85f, (i + 0.5f) / 3f); // çemberin alt yayı
            origins.Add(new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f));
        }

        for (int i = 0; i < dripCount && i < origins.Count; i++)
        {
            float s = seed + i * 13.7f;
            float len = radius * dripLength * (0.6f + Mathf.PerlinNoise(s, 0f) * 0.8f);
            MakeDrip(mat, origins[i], len, lineWidth * 0.9f, s);
        }
    }

    void MakeDrip(Material mat, Vector3 origin, float len, float w, float seed)
    {
        const int pts = 7;
        var lr = NewLine("Drip", mat, pts, false);
        lr.widthMultiplier = w;
        for (int k = 0; k < pts; k++)
        {
            float t = (float)k / (pts - 1);
            float x = origin.x + (Mathf.PerlinNoise(seed, t * 4f) - 0.5f) * w * 2f; // hafif yatay sürüklenme
            float y = origin.y - t * len;                                            // aşağı akış
            lr.SetPosition(k, new Vector3(x, y, 0f));
        }
        lr.widthCurve = DripCurve();   // ince akış + uçta damla topu
    }

    // Damla profili: üstte orta, ortada incelir, uca yakın şişer (damla), en uçta sivri
    static AnimationCurve DripCurve()
    {
        return new AnimationCurve(
            new Keyframe(0f, 0.9f), new Keyframe(0.55f, 0.35f),
            new Keyframe(0.85f, 1f), new Keyframe(1f, 0.05f));
    }

    // Jinx graffiti kolu: kaotik, dalgalı, ucu taşan/incelen spreylenmiş çizgi
    LineRenderer NewStroke(string n, Material mat, Vector3 a, Vector3 b, float seed)
    {
        const int pts = 14;
        var lr = NewLine(n, mat, pts, false);
        lr.widthMultiplier = lineWidth * crossThickness;   // çarpı daha kalın
        Vector3 dir = (b - a);
        Vector3 perp = new Vector3(-dir.y, dir.x, 0f).normalized;
        float amp = radius * jitter * crossChaos;
        for (int k = 0; k < pts; k++)
        {
            float t = (float)k / (pts - 1);
            Vector3 p = Vector3.Lerp(a, b, t);
            if (calligraphy)
            {
                // hem yana (perp) hem boyca kaydırarak spreylenmiş/whippy his
                p += perp * amp * (Mathf.PerlinNoise(t * 3.5f + seed, seed) - 0.5f) * 2f;
                p += dir.normalized * amp * 0.4f * (Mathf.PerlinNoise(seed, t * 3.5f + seed) - 0.5f) * 2f;
            }
            lr.SetPosition(k, p);
        }
        if (calligraphy)
            lr.widthCurve = TaperCurve(1f - widthVariation, 1f);
        return lr;
    }

    // 0..1 boyunca 'cycles' kez kalın-ince (|sin|) genişlik eğrisi
    static AnimationCurve CalligraphyCurve(int cycles, float min, float max)
    {
        var c = new AnimationCurve();
        int steps = cycles * 8;
        for (int i = 0; i <= steps; i++)
        {
            float t = (float)i / steps;
            float w = Mathf.Lerp(min, max, Mathf.Abs(Mathf.Sin(t * Mathf.PI * cycles)));
            c.AddKey(t, w);
        }
        return c;
    }

    // Uçlar ince (min), orta kalın (max)
    static AnimationCurve TaperCurve(float min, float max)
    {
        return new AnimationCurve(
            new Keyframe(0f, min), new Keyframe(0.5f, max), new Keyframe(1f, min));
    }

    LineRenderer NewLine(string n, Material mat, int count, bool loop)
    {
        var go = new GameObject(n);
        go.transform.SetParent(mark, false);
        var lr = go.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = loop;
        lr.positionCount = count;
        lr.widthMultiplier = lineWidth;
        lr.numCornerVertices = 4;
        lr.numCapVertices = 4;
        lr.alignment = LineAlignment.TransformZ;   // duvar düzleminde kalsın (billboard olmasın)
        lr.sharedMaterial = mat;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lines.Add(lr);
        return lr;
    }

    void SetVisible(bool v)
    {
        for (int i = 0; i < lines.Count; i++)
            if (lines[i] != null) lines[i].enabled = v;
    }

    void Update()
    {
        // Player boşsa tekrar bulmayı dene
        if (player == null)
        {
            var pObj = GameObject.FindWithTag("Player");
            if (pObj != null) player = pObj.transform;
            else if (Camera.main != null) player = Camera.main.transform;
        }

        // --- Görünürlük: oyuncu yakında (ve isteğe bağlı bize doğru bakıyor) mu? ---
        float targetReveal;
        if (alwaysVisible || player == null)
        {
            // alwaysVisible: hep göster. player yoksa: gizli takılıp kalmasın diye göster.
            targetReveal = 1f;
        }
        else
        {
            Vector3 toMark = transform.position - player.position;
            bool near = toMark.sqrMagnitude < triggerDistance * triggerDistance;
            bool facing = !requireFacing ||
                          Vector3.Dot(player.forward, toMark.normalized) > facingDot;
            targetReveal = (near && facing) ? 1f : 0f;
        }
        reveal = Mathf.MoveTowards(reveal, targetReveal, Time.deltaTime * revealSpeed);

        bool visible = reveal > 0.001f;
        SetVisible(visible);
        if (!visible) return;

        // --- Ritim nabzı (equalizer ile aynı beat mantığı) ---
        DetectBeat();
        pulse = Mathf.Lerp(pulse, 0f, Time.deltaTime * pulseDecay);

        float scale = reveal * (1f + pulse * pulseAmount);
        mark.localScale = new Vector3(scale, scale, scale);

        // Çılgın his: yavaş dönüş (yüzey normali = local Z etrafında)
        if (spinSpeed != 0f)
            mark.localRotation = Quaternion.Euler(0f, 0f, Time.time * spinSpeed);
    }

#if UNITY_EDITOR
    // Editörde (Play'e basmadan) işareti gör ve konumlandır.
    // Component'in sağ üst köşesindeki ⋮ menüsünden çağır.
    [ContextMenu("Önizle (Editörde göster)")]
    void PreviewInEditor()
    {
        BuildMark();
        if (mark != null)
        {
            mark.gameObject.hideFlags = HideFlags.DontSaveInEditor; // sahneye kaydedilmesin
            SetVisible(true);
            mark.localScale = Vector3.one;
        }
    }

    [ContextMenu("Önizlemeyi Temizle")]
    void ClearPreview()
    {
        var old = transform.Find("DeadEndMark");
        if (old != null) DestroyImmediate(old.gameObject);
    }
#endif

    void DetectBeat()
    {
        float[] spec = AudioSpectrumProvider.GetShared();
        int bassBins = Mathf.Max(4, spec.Length / 32);
        float bass = 0f;
        for (int i = 0; i < bassBins; i++) bass += spec[i];

        energyAvg = Mathf.Lerp(energyAvg, bass, Time.deltaTime * 3f);
        beatTimer += Time.deltaTime;

        if (bass > energyAvg * beatThreshold && beatTimer >= minBeatInterval && bass > 0.0002f)
        {
            beatTimer = 0f;
            pulse = 1f;
        }
    }
}
