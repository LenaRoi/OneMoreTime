using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraRewind : MonoBehaviour
{
    [Header("Recording")]
    [SerializeField] private float recordInterval = 0.02f;   // 50 kayıt/sn
    [SerializeField] private float maxRecordTime = 10f;       // Son kaç saniye tutulsun

    [SerializeField] private float rewindSpeed = 3f;

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

        while (history.Count > 1)
        {
            CameraState from = history.Last.Value;
            history.RemoveLast();

            CameraState to = history.Last.Value;

            float elapsed = 0f;

            while (elapsed < recordInterval)
            {
                elapsed += Time.deltaTime * rewindSpeed;

                float t = Mathf.Clamp01(elapsed / recordInterval);

                transform.position = Vector3.Lerp(from.Position, to.Position, t);
                transform.rotation = Quaternion.Slerp(from.Rotation, to.Rotation, t);

                yield return null;
            }

            transform.position = to.Position;
            transform.rotation = to.Rotation;
        }

        history.Clear();

        isRecording = true;
        isRewinding = false;
    }

    public void ClearHistory()
    {
        history.Clear();
    }
}