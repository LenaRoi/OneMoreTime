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
    public float lineWidth = 0.05f;
    [Tooltip("Duvardan dışarı kaldırma (z-fighting'i önler).")]
    public float surfaceOffset = 0.02f;

    [Header("Renk / Neon")]
    public Color color = Color.red;
    [Range(1f, 8f)] public float brightness = 2.5f;

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
    private LineRenderer circle, cross1, cross2;
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

        var shader = Shader.Find("Unlit/Color");
        var mat = new Material(shader) { color = color * brightness };

        // Çember
        circle = NewLine("Circle", mat, circleSegments + 1, true);
        for (int i = 0; i <= circleSegments; i++)
        {
            float a = (float)i / circleSegments * Mathf.PI * 2f;
            circle.SetPosition(i, new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0f));
        }

        // Çarpı (iki köşegen)
        float d = radius * crossScale * 0.70711f;   // 45° köşe
        cross1 = NewLine("Cross1", mat, 2, false);
        cross1.SetPosition(0, new Vector3(-d, -d, 0f));
        cross1.SetPosition(1, new Vector3(d, d, 0f));
        cross2 = NewLine("Cross2", mat, 2, false);
        cross2.SetPosition(0, new Vector3(-d, d, 0f));
        cross2.SetPosition(1, new Vector3(d, -d, 0f));

        SetVisible(false);
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
        return lr;
    }

    void SetVisible(bool v)
    {
        if (circle != null) circle.enabled = v;
        if (cross1 != null) cross1.enabled = v;
        if (cross2 != null) cross2.enabled = v;
    }

    void Update()
    {
        // --- Görünürlük: oyuncu yakında (ve isteğe bağlı bize doğru bakıyor) mu? ---
        float targetReveal = 0f;
        if (player != null)
        {
            Vector3 toMark = transform.position - player.position;
            bool near = toMark.sqrMagnitude < triggerDistance * triggerDistance;
            bool facing = !requireFacing ||
                          Vector3.Dot(player.forward, toMark.normalized) > facingDot;
            if (near && facing) targetReveal = 1f;
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
    }

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
