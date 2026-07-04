using UnityEngine;

/// <summary>
/// Shared audio spectrum for all visualizers. Reads the final mixed output at
/// the AudioListener (i.e. whatever music is playing in the scene) ONCE per
/// frame and caches it, so any number of equalizers can react to the same
/// single audio without each playing its own copy. Requires an AudioListener
/// in the scene (the Main Camera has one by default) and one AudioSource
/// actually playing the music somewhere.
/// </summary>
public static class AudioSpectrumProvider
{
    public const int Size = 512;   // 2'nin kuvveti; tüm görselleştiriciler bunu paylaşır

    static readonly float[] _data = new float[Size];
    static int _frame = -1;

    /// <summary>Bu kareye ait ortak spektrumu döndürür (kare başına tek FFT).</summary>
    public static float[] GetShared(FFTWindow window = FFTWindow.BlackmanHarris)
    {
        if (Time.frameCount != _frame)
        {
            _frame = Time.frameCount;
            AudioListener.GetSpectrumData(_data, 0, window);
        }
        return _data;
    }
}
