using UnityEngine;

/// <summary>
/// Âm thanh tổng hợp lúc chạy (không cần file âm thanh):
///  - Siren(): còi xe ưu tiên kiểu "hú" (wail) lên - xuống, sóng gần vuông qua loa nén như còi thật, có lẫn tiếng "yelp" nhanh.
/// Mỗi clip tạo một lần rồi dùng lại.
/// </summary>
public static class SynthAudio
{
    private const int Rate = 44100;
    private static AudioClip siren;
    /// <summary>Còi xe ưu tiên: 2 chu kỳ "hú" chậm (0.6-1.5 kHz) rồi 1 đoạn "yelp" nhanh, lặp liền mạch (6 s).</summary>
    public static AudioClip Siren()
    {
        if (siren != null) return siren;
        float len = 6f;
        int n = (int)(Rate * len);
        var data = new float[n];
        double phase = 0;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            float f;
            if (t < 4f)
            {
                float u = (t % 2f) / 2f;                                       // hú: lên nhanh, xuống chậm
                f = u < 0.45f ? Mathf.Lerp(620f, 1480f, Mathf.SmoothStep(0f, 1f, u / 0.45f))
                              : Mathf.Lerp(1480f, 620f, Mathf.SmoothStep(0f, 1f, (u - 0.45f) / 0.55f));
            }
            else
            {
                float u = ((t - 4f) % 0.25f) / 0.25f;                          // yelp: quét nhanh 4 lần / giây
                f = Mathf.Lerp(700f, 1550f, u < 0.5f ? u * 2f : (1f - u) * 2f);
            }
            phase += 2 * Mathf.PI * f / Rate;
            float s = Mathf.Sin((float)phase);
            float sq = (float)System.Math.Tanh(4.0 * s);                         // gần vuông như loa còi nén
            float v = sq * 0.55f + Mathf.Sin((float)phase * 2f) * 0.12f + Mathf.Sin((float)phase * 3f) * 0.08f;
            data[i] = v * 0.5f;
        }
        // nối đầu - cuối không bị "tách"
        for (int i = 0; i < 400; i++) { float k = i / 400f; data[i] *= k; data[n - 1 - i] *= k; }
        siren = AudioClip.Create("Siren", n, 1, Rate, false);
        siren.SetData(data, 0);
        return siren;
    }

    /// <summary>Phát một clip 2D một lần.</summary>
    public static void Play2D(AudioClip clip, float volume)
    {
        if (clip == null) return;
        var go = new GameObject("Sfx");
        var src = go.AddComponent<AudioSource>();
        src.clip = clip; src.volume = volume; src.spatialBlend = 0f;
        src.Play();
        Object.Destroy(go, clip.length + 0.1f);
    }

}
