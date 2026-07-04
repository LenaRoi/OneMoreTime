using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraRewind : MonoBehaviour
{
    [Header("Recording")]
    [SerializeField] private float recordInterval = 0.02f;   // 50 kayıt/sn
    [SerializeField] private float maxRecordTime = 10f;       // Son kaç saniye tutulsun

    [Header("Rewind Süresi (yol uzunluğuna göre)")]
    [Tooltip("En kısa süre (kısa yol).")]
    [SerializeField] private float minDuration = 1f;
    [Tooltip("En uzun süre (uzun yol).")]
    [SerializeField] private float maxDuration = 3.5f;
    [Tooltip("Süre hesabı için hız: yolun kaç birimi 1 saniyeye denk gelsin (yüksek = daha hızlı geri sar).")]
    [SerializeField] private float rewindUnitsPerSecond = 25f;
    [Tooltip("Yumuşak giriş/çıkış (ease-in-out).")]
    [SerializeField] private bool ease = true;

    private class CameraState
    {
        public Vector3 Position;
        public Quaternion Rotation;

        public CameraState(Vector3 pos, Quaternion rot)
        {
            Position = pos;
            Rotation = rot;
        }
    }

    private readonly LinkedList<CameraState> history = new();

    private float timer;
    private bool isRecording = true;
    private bool isRewinding = false;

    void Update()
    {
        if (isRecording)
        {
            timer += Time.deltaTime;

            if (timer >= recordInterval)
            {
                timer = 0f;

                history.AddLast(new CameraState(transform.position, transform.rotation));

                int maxStates = Mathf.CeilToInt(maxRecordTime / recordInterval);

                while (history.Count > maxStates)
                    history.RemoveFirst();
            }
        }

        // Test
        if (Input.GetKeyDown(KeyCode.R))
            StartRewind();
    }

    public void StartRewind()
    {
        if (isRewinding || history.Count < 2)
            return;

        StartCoroutine(RewindRoutine());
    }

    private IEnumerator RewindRoutine()
    {
        isRewinding = true;
        isRecording = false;

        // Geçmişi diziye al: index 0 = en eski (başlangıç), son = en yeni (şu an)
        var states = new List<CameraState>(history);
        history.Clear();

        int n = states.Count;
        if (n < 2)
        {
            isRecording = true;
            isRewinding = false;
            yield break;
        }

        int segments = n - 1;

        // Kat edilen toplam yol uzunluğu → süre (min..max arası)
        float pathLength = 0f;
        for (int k = 1; k < n; k++)
            pathLength += Vector3.Distance(states[k - 1].Position, states[k].Position);
        float duration = Mathf.Clamp(pathLength / Mathf.Max(0.01f, rewindUnitsPerSecond),
                                     minDuration, maxDuration);

        float elapsed = 0f;

        // Süre boyunca yeni -> eski tüm yolu kat et (kare başına gereken kadar segment)
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float p = Mathf.Clamp01(elapsed / duration);
            if (ease) p = Mathf.SmoothStep(0f, 1f, p);

            float f = (1f - p) * segments;               // segments (yeni) -> 0 (eski)
            int i = Mathf.Clamp(Mathf.FloorToInt(f), 0, segments - 1);
            float frac = f - i;

            transform.position = Vector3.Lerp(states[i].Position, states[i + 1].Position, frac);
            transform.rotation = Quaternion.Slerp(states[i].Rotation, states[i + 1].Rotation, frac);

            yield return null;
        }

        // Başlangıç (en eski) noktaya sabitle
        transform.position = states[0].Position;
        transform.rotation = states[0].Rotation;

        isRecording = true;
        isRewinding = false;
    }

    public void ClearHistory()
    {
        history.Clear();
    }
}