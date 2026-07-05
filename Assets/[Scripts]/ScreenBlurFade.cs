using System.Collections;
using UnityEngine;

/// <summary>
/// Deadloop (ölüm) animasyonundan sonra ekranı önce BULANIK gösterip yavaşça nete
/// açan tam-ekran blur efekti. Ana Kamera'ya ekle. GameManager.StartGame() içinden
/// <see cref="PlayFocusIn"/> ile tetiklenir. Built-in Render Pipeline image effect
/// (OnRenderImage). Blur 0 iken maliyet ~yok (sadece kopya blit).
/// </summary>
[RequireComponent(typeof(Camera))]
[DisallowMultipleComponent]
public class ScreenBlurFade : MonoBehaviour
{
    [Tooltip("Blur shader. Boşsa 'Hidden/ScreenBlurFade' otomatik bulunur. BUILD için buraya elle atamak güvenlidir.")]
    [SerializeField] private Shader blurShader;

    [Tooltip("Blur pass tekrar sayısı (yüksek = daha yumuşak/güçlü bulanıklık).")]
    [Range(1, 8)] public int iterations = 3;
    [Tooltip("En güçlü anındaki bulanıklık yarıçapı (piksel).")]
    [Range(1f, 10f)] public float maxBlurSize = 4f;
    [Tooltip("Yarı çözünürlükte bulanıklaştır (daha ucuz ve daha yumuşak).")]
    public bool downsample = true;

    private Material mat;
    private float blur01;         // 0 = net, 1 = tam bulanık
    private Coroutine anim;

    void OnEnable()
    {
        if (blurShader == null) blurShader = Shader.Find("Hidden/ScreenBlurFade");
        if (blurShader != null && mat == null)
            mat = new Material(blurShader) { hideFlags = HideFlags.HideAndDontSave };
    }

    void OnDisable()
    {
        if (mat != null) DestroyImmediate(mat);
        mat = null;
    }

    /// <summary>Tam bulanıktan nete <paramref name="duration"/> saniyede aç (deadloop sonrası).</summary>
    public void PlayFocusIn(float duration = 1f)
    {
        if (!isActiveAndEnabled) return;
        if (anim != null) StopCoroutine(anim);
        anim = StartCoroutine(FocusIn(Mathf.Max(0.01f, duration)));
    }

    IEnumerator FocusIn(float duration)
    {
        blur01 = 1f;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;                    // pause/timescale'den bağımsız
            blur01 = 1f - Mathf.SmoothStep(0f, 1f, t / duration);
            yield return null;
        }
        blur01 = 0f;
        anim = null;
    }

    void OnRenderImage(RenderTexture src, RenderTexture dst)
    {
        // Bulanıklık yokken (normal oyun) sadece kopyala — maliyetsiz.
        if (mat == null || blur01 <= 0.001f) { Graphics.Blit(src, dst); return; }

        mat.SetFloat("_BlurSize", maxBlurSize * blur01);

        int w = downsample ? Mathf.Max(1, src.width / 2) : src.width;
        int h = downsample ? Mathf.Max(1, src.height / 2) : src.height;

        RenderTexture a = RenderTexture.GetTemporary(w, h, 0, src.format);
        RenderTexture b = RenderTexture.GetTemporary(w, h, 0, src.format);

        Graphics.Blit(src, a);
        int iters = Mathf.Max(1, iterations);
        for (int i = 0; i < iters; i++)
        {
            Graphics.Blit(a, b, mat, 0);   // yatay
            Graphics.Blit(b, a, mat, 1);   // dikey
        }
        Graphics.Blit(a, dst);

        RenderTexture.ReleaseTemporary(a);
        RenderTexture.ReleaseTemporary(b);
    }
}
