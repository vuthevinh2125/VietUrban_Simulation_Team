using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Âm thanh kinh dị ban đêm, tạo bằng code (không cần file âm thanh). Mỗi clip tạo một lần rồi dùng lại:
///  - FuneralBand(): nhạc đám ma rùng rợn - hai kèn lệch tông, trống cái, thanh la, ù trầm, tiếng khóc than vọng lại; lặp ~12.8 s.
///  - Wail() khóc than, Crack() xương kêu rắc, Scream() tiếng thét không phải người, Whisper() thì thầm, Drip() giọt rơi,
///    Breath() tiếng thở khò khè, Bang() đập mạnh vào kính.
///  - DogHowl(): chó tru dài hai hơi.     - Owl(): cú kêu "hù... hù-hù".
///  - Heartbeat(): tim đập thình thịch (lặp).   - Static(): tiếng rè màn hình / radio (lặp).
///  - Sting(): tiếng rít chói nhiều nốt nghịch tai cho cảnh hù.   - PhoneBuzz(): điện thoại rung hai nhịp.
///  - Wind(): gió rít (lặp).
/// Prewarm(): tạo dần từng clip (mỗi lần gọi một clip) để không khựng game khi cần.
/// </summary>
public static class HorrorAudio
{
    private const int Rate = 32000;
    private static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();
    private static System.Random rnd = new System.Random(1234);

    private static float Noise() => (float)rnd.NextDouble() * 2f - 1f;

    private static AudioClip Make(string name, float seconds, System.Func<int, float, float> sample, float gain = 1f, float edge = 0.01f)
    {
        if (cache.TryGetValue(name, out AudioClip c) && c != null) return c;
        int n = Mathf.Max(1, (int)(Rate * seconds));
        var data = new float[n];
        for (int i = 0; i < n; i++) data[i] = sample(i, i / (float)Rate);
        float peak = 1e-4f;
        for (int i = 0; i < n; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
        int fadeN = Mathf.Min(n / 2, (int)(edge * Rate));
        for (int i = 0; i < n; i++)
        {
            float k = gain * 0.9f / peak;
            if (i < fadeN) k *= i / (float)fadeN;
            if (i > n - 1 - fadeN) k *= (n - 1 - i) / (float)Mathf.Max(1, fadeN);
            data[i] *= k;
        }
        c = AudioClip.Create(name, n, 1, Rate, false);
        c.SetData(data, 0);
        cache[name] = c;
        return c;
    }

    private static int prewarmIndex;
    /// <summary>Tạo trước một clip mỗi lần gọi (gọi rải rác qua nhiều frame). Trả về true khi đã tạo xong hết.</summary>
    public static bool Prewarm()
    {
        switch (prewarmIndex++)
        {
            case 0: DogHowl(); return false;
            case 1: Owl(); return false;
            case 2: Heartbeat(); return false;
            case 3: Static(); return false;
            case 4: Sting(); return false;
            case 5: PhoneBuzz(); return false;
            case 6: Wind(); return false;
            case 7: FuneralBand(); return false;
            case 8: Wail(); return false;
            case 9: Crack(); return false;
            case 10: Scream(); return false;
            case 11: Whisper(); return false;
            case 12: Drip(); return false;
            case 13: Breath(); return false;
            case 14: Bang(); return false;
            default: return true;
        }
    }

    /// <summary>
    /// Nhạc đám ma (rùng rợn): hai kèn lệch tông nhau (rung "è è" như phách), điệu ngũ cung chậm, cuối mỗi câu kèn trườn tụt xuống;
    /// trống cái nặng, thanh la; nền là tiếng ù trầm và tiếng khóc than vọng lại - lặp liền mạch ~12.8 s.
    /// </summary>
    public static AudioClip FuneralBand()
    {
        int[] notes = { 7, 8, 7, 5, 3, 5, 7, 3, 2, 3, 0, -1, 0, 0 };
        float[] beats = { 1.5f, 0.5f, 1, 1, 1, 1, 2, 1, 0.5f, 0.5f, 1.5f, 0.5f, 2, 2 };
        float beat = 0.8f, total = 0f;
        foreach (float b in beats) total += b * beat;
        var start = new float[notes.Length];
        float acc = 0f;
        for (int i = 0; i < notes.Length; i++) { start[i] = acc; acc += beats[i] * beat; }
        double ph1 = 0, ph2 = 0, phW = 0;
        float lp = 0f, f1 = 196f, wl = 0f;
        return Make("FuneralBand", total, (i, t) =>
        {
            int k = 0;
            while (k + 1 < notes.Length && t >= start[k + 1]) k++;
            float noteT = t - start[k], noteLen = beats[k] * beat;
            float target = 196f * Mathf.Pow(2f, notes[k] / 12f);
            if (noteLen >= 1.6f && noteT > noteLen * 0.55f) target *= Mathf.Pow(2f, -2.5f * (noteT / noteLen - 0.55f) / 12f * 4f); // trườn tụt xuống cuối câu
            f1 += (target - f1) * 0.002f;
            float vib = 1f + 0.018f * Mathf.Sin(t * 2f * Mathf.PI * 4.6f);
            ph1 += 2 * Mathf.PI * f1 * vib / Rate;
            ph2 += 2 * Mathf.PI * f1 * 1.034f * vib / Rate;                 // kèn thứ hai lệch ~0.6 cung -> nghe chói, rợn
            float Horn(double ph) { float s = (float)ph; return Mathf.Sin(s) * 0.5f + Mathf.Sin(3 * s) * 0.45f + Mathf.Sin(5 * s) * 0.3f + Mathf.Sin(7 * s) * 0.18f + Mathf.Sin(2 * s) * 0.12f; }
            float env = Mathf.Clamp01(noteT / 0.09f) * Mathf.Lerp(1f, 0.65f, noteT / noteLen);
            float horn = (float)System.Math.Tanh((Horn(ph1) + Horn(ph2) * 0.8f) * 1.3f) * env * 0.5f;
            // trống cái nặng mỗi 2 phách
            float dt = t % (beat * 2f);
            float drum = Mathf.Sin(2 * Mathf.PI * (48f + 50f * Mathf.Exp(-dt * 18f)) * dt) * Mathf.Exp(-dt * 4.5f) * 1.1f;
            // thanh la mỗi 4 phách
            float gt = (t + beat) % (beat * 4f);
            lp += (Noise() - lp) * 0.6f;
            float gong = (Mathf.Sin(2 * Mathf.PI * 1180f * gt) * 0.4f + Mathf.Sin(2 * Mathf.PI * 1567f * gt) * 0.3f + lp * 0.5f) * Mathf.Exp(-gt * 3f) * 0.3f;
            // ù trầm (hai nốt cách nửa cung)
            float drone = (Mathf.Sin(2 * Mathf.PI * 55f * t) + Mathf.Sin(2 * Mathf.PI * 58.3f * t)) * 0.16f;
            // tiếng khóc than: giọng nữ "ơ..." trườn lên xuống, vọng xa
            float wt = t % 6.4f;
            float wf = 520f + 160f * Mathf.Sin(wt * 0.9f) - 120f * wt / 6.4f;
            phW += 2 * Mathf.PI * wf * (1f + 0.03f * Mathf.Sin(t * 37f)) / Rate;
            float w = (Mathf.Sin((float)phW) + 0.5f * Mathf.Sin(2 * (float)phW) + 0.25f * Mathf.Sin(3 * (float)phW));
            wl += (w - wl) * 0.25f;                                       // lọc cho mờ, như vọng lại
            float wenv = Mathf.Clamp01(wt / 0.8f) * Mathf.Clamp01((4.6f - wt) / 1.2f) * (t % 12.8f < 6.4f ? 1f : 0.6f);
            return horn + drum + gong + drone + wl * wenv * 0.22f;
        }, 0.9f, 0.03f);
    }

    /// <summary>Tiếng khóc than (giọng nữ, nức nở, trườn giọng xuống), 4.5 s.</summary>
    public static AudioClip Wail()
    {
        double ph = 0; float lp = 0f;
        return Make("Wail", 4.5f, (i, t) =>
        {
            float sob = t < 1.2f ? 1f : 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(t * 5.5f));
            float f = 620f - 90f * t + 70f * Mathf.Sin(t * 2.2f) + (t < 0.4f ? 200f * (0.4f - t) : 0f);
            ph += 2 * Mathf.PI * f * (1f + 0.035f * Mathf.Sin(t * 41f)) / Rate;
            float s = (float)ph;
            float v = Mathf.Sin(s) + 0.6f * Mathf.Sin(2 * s) + 0.35f * Mathf.Sin(3 * s) + 0.15f * Mathf.Sin(4 * s) + Noise() * 0.15f;
            lp += (v - lp) * 0.35f;
            return lp * sob * Mathf.Clamp01(t / 0.15f) * Mathf.Clamp01((4.5f - t) / 0.8f);
        }, 0.7f);
    }

    /// <summary>Tiếng xương kêu "rắc" (khi đầu vặn ngược ra sau).</summary>
    public static AudioClip Crack()
    {
        return Make("Crack", 0.5f, (i, t) =>
        {
            float v = 0f;
            for (int k = 0; k < 4; k++)
            {
                float lt = t - k * 0.045f - (k == 3 ? 0.12f : 0f);
                if (lt < 0f) continue;
                v += Noise() * Mathf.Exp(-lt * 90f) + Mathf.Sin(2 * Mathf.PI * 180f * lt) * Mathf.Exp(-lt * 60f) * 0.6f;
            }
            return v;
        }, 0.9f, 0.002f);
    }

    /// <summary>Tiếng thét không phải người: hai giọng cao trượt lên, khàn rè, 1.8 s.</summary>
    public static AudioClip Scream()
    {
        double p1 = 0, p2 = 0;
        return Make("Scream", 1.8f, (i, t) =>
        {
            float f = 380f + 900f * Mathf.SmoothStep(0f, 1f, t / 0.5f) - 200f * Mathf.Clamp01((t - 0.9f) / 0.9f);
            p1 += 2 * Mathf.PI * f * (1f + 0.05f * Mathf.Sin(t * 70f)) / Rate;
            p2 += 2 * Mathf.PI * f * 1.41f / Rate;
            float v = Mathf.Sin((float)p1) + 0.7f * Mathf.Sin((float)p2) + 0.5f * Mathf.Sin(3 * (float)p1) + Noise() * 0.9f;
            return (float)System.Math.Tanh(v * 2.2f) * Mathf.Clamp01(t / 0.03f) * Mathf.Clamp01((1.8f - t) / 0.6f);
        }, 1f, 0.004f);
    }

    /// <summary>Tiếng thì thầm (nhiễu qua formant, thành từng cụm như chữ), 3 s.</summary>
    public static AudioClip Whisper()
    {
        float b1 = 0f, b2 = 0f, lpA = 0f;
        return Make("Whisper", 3f, (i, t) =>
        {
            float syl = Mathf.Max(0f, Mathf.Sin(t * 2f * Mathf.PI * 3.1f)) * Mathf.Max(0f, Mathf.Sin(t * 2f * Mathf.PI * 0.7f + 0.5f));
            float n = Noise();
            b1 += (n - b1) * 0.18f; b2 += (b1 - b2) * 0.18f;
            lpA += ((n - b2) - lpA) * 0.5f;
            return lpA * syl;
        }, 0.6f, 0.05f);
    }

    /// <summary>Giọt (máu) nhỏ "tách" vang trong phòng vắng.</summary>
    public static AudioClip Drip()
    {
        return Make("Drip", 0.6f, (i, t) =>
        {
            float f = 900f + 1400f * Mathf.Exp(-t * 40f);
            return Mathf.Sin(2 * Mathf.PI * f * t) * Mathf.Exp(-t * 12f) + Mathf.Sin(2 * Mathf.PI * 0.55f * f * (t - 0.12f)) * (t > 0.12f ? Mathf.Exp(-(t - 0.12f) * 18f) * 0.35f : 0f);
        }, 0.5f, 0.002f);
    }

    /// <summary>Tiếng thở nặng nhọc, khò khè sát tai (lặp 3.2 s).</summary>
    public static AudioClip Breath()
    {
        float lp = 0f, lp2 = 0f;
        return Make("Breath", 3.2f, (i, t) =>
        {
            float ph = t % 3.2f;
            float inh = ph < 1.3f ? Mathf.Sin(Mathf.PI * ph / 1.3f) : 0f;
            float exh = ph > 1.5f && ph < 3f ? Mathf.Sin(Mathf.PI * (ph - 1.5f) / 1.5f) : 0f;
            float n = Noise();
            lp += (n - lp) * (inh > 0f ? 0.35f : 0.15f);
            lp2 += (lp - lp2) * 0.3f;
            float rasp = Mathf.Sin(2 * Mathf.PI * 70f * t) * 0.3f * exh;
            return (lp - lp2 * 0.6f) * (inh * 0.8f + exh) + rasp;
        }, 0.7f, 0.02f);
    }

    /// <summary>Đập mạnh vào kính / cửa (một cú).</summary>
    public static AudioClip Bang()
    {
        return Make("Bang", 0.8f, (i, t) =>
            (Mathf.Sin(2 * Mathf.PI * 95f * t) * 1.2f + Noise() * 0.8f * Mathf.Exp(-t * 30f) + Mathf.Sin(2 * Mathf.PI * 2300f * t) * 0.2f * Mathf.Exp(-t * 20f)) * Mathf.Exp(-t * 9f),
            1f, 0.002f);
    }

    /// <summary>Chó tru: hai hơi dài, cao độ trườn lên rồi rơi xuống, có rung.</summary>
    public static AudioClip DogHowl()
    {
        double ph = 0;
        return Make("DogHowl", 5.2f, (i, t) =>
        {
            float local = t < 2.6f ? t : t - 2.7f;
            float len = t < 2.6f ? 2.5f : 2.4f;
            if (local < 0f || local > len) return 0f;
            float u = local / len;
            float f = Mathf.Lerp(460f, 760f, Mathf.SmoothStep(0f, 1f, u / 0.35f)) - (u > 0.55f ? Mathf.Lerp(0f, 260f, (u - 0.55f) / 0.45f) : 0f);
            f *= 1f + 0.02f * Mathf.Sin(local * 2f * Mathf.PI * 6f);
            if (t >= 2.6f) f *= 1.06f;
            ph += 2 * Mathf.PI * f / Rate;
            float s = (float)ph;
            float v = Mathf.Sin(s) + 0.35f * Mathf.Sin(2 * s) + 0.12f * Mathf.Sin(3 * s) + Noise() * 0.03f;
            float env = Mathf.Clamp01(u / 0.12f) * Mathf.Clamp01((1f - u) / 0.25f);
            return v * env;
        }, 0.8f);
    }

    /// <summary>Cú kêu: "hù......... hù-hù".</summary>
    public static AudioClip Owl()
    {
        float[] at = { 0f, 1.25f, 1.6f }, len = { 0.55f, 0.28f, 0.5f };
        return Make("Owl", 2.3f, (i, t) =>
        {
            float v = 0f;
            for (int k = 0; k < at.Length; k++)
            {
                float lt = t - at[k];
                if (lt < 0f || lt > len[k]) continue;
                float u = lt / len[k];
                float f = 385f * (1f - 0.06f * u);
                float env = Mathf.Sin(Mathf.PI * Mathf.Clamp01(u)) * (k == 0 ? 1f : 0.85f);
                v += (Mathf.Sin(2 * Mathf.PI * f * lt) + 0.15f * Mathf.Sin(4 * Mathf.PI * f * lt) + Noise() * 0.04f) * env;
            }
            return v;
        }, 0.6f);
    }

    /// <summary>Tim đập "thình-thịch" (lặp 0.95 s).</summary>
    public static AudioClip Heartbeat()
    {
        return Make("Heartbeat", 0.95f, (i, t) =>
        {
            float a = t, b = t - 0.24f;
            float v = Mathf.Sin(2 * Mathf.PI * 52f * a) * Mathf.Exp(-a * 18f);
            if (b > 0f) v += 0.75f * Mathf.Sin(2 * Mathf.PI * 46f * b) * Mathf.Exp(-b * 16f);
            return v;
        }, 1f, 0.002f);
    }

    /// <summary>Tiếng rè màn hình / radio mất sóng (lặp 2 s): nhiễu trắng + ù 50 Hz + tiếng "lép bép".</summary>
    public static AudioClip Static()
    {
        float hp = 0f, prev = 0f;
        return Make("Static", 2f, (i, t) =>
        {
            float n = Noise();
            hp = 0.85f * (hp + n - prev); prev = n;                      // nhiễu cao (lọc thông cao)
            float crackle = rnd.NextDouble() < 0.0008 ? Noise() * 3f : 0f;
            float hum = Mathf.Sin(2 * Mathf.PI * 50f * t) * 0.25f + Mathf.Sin(2 * Mathf.PI * 150f * t) * 0.08f;
            return hp * 0.5f + hum + crackle;
        }, 0.6f);
    }

    /// <summary>Tiếng rít hù doạ: cụm nốt nghịch tai trượt lên + nhiễu, bật mạnh rồi tắt dần (1.4 s).</summary>
    public static AudioClip Sting()
    {
        double[] ph = new double[5];
        float[] baseF = { 466f, 493f, 698f, 740f, 1108f };
        return Make("Sting", 1.4f, (i, t) =>
        {
            float v = 0f;
            for (int k = 0; k < baseF.Length; k++)
            {
                ph[k] += 2 * System.Math.PI * baseF[k] * (1f + 0.25f * t) / Rate;
                float p = (float)(ph[k] % (2 * System.Math.PI)) / (2 * Mathf.PI);
                v += (p * 2f - 1f) * 0.3f;                                // răng cưa chói
            }
            v += Noise() * 0.5f * Mathf.Exp(-t * 6f);
            float env = Mathf.Clamp01(t / 0.015f) * Mathf.Exp(-t * 1.6f);
            return (float)System.Math.Tanh(v * 1.5f) * env;
        }, 1f, 0.004f);
    }

    /// <summary>Điện thoại rung hai nhịp.</summary>
    public static AudioClip PhoneBuzz()
    {
        return Make("PhoneBuzz", 1.1f, (i, t) =>
        {
            bool on = t < 0.38f || (t > 0.6f && t < 0.98f);
            if (!on) return 0f;
            return (Mathf.Sin(2 * Mathf.PI * 150f * t) * 0.6f + Mathf.Sin(2 * Mathf.PI * 300f * t) * 0.3f + Noise() * 0.1f)
                   * (0.6f + 0.4f * Mathf.Sin(2 * Mathf.PI * 30f * t));
        }, 0.7f);
    }

    /// <summary>Gió rít (lặp 6 s): nhiễu lọc, cường độ lên xuống chậm.</summary>
    public static AudioClip Wind()
    {
        float lp1 = 0f, lp2 = 0f;
        return Make("Wind", 6f, (i, t) =>
        {
            float gust = 0.55f + 0.45f * Mathf.Sin(2 * Mathf.PI * t / 6f) * Mathf.Sin(2 * Mathf.PI * t / 3f + 1f);
            float a = 0.02f + 0.05f * gust;
            lp1 += (Noise() - lp1) * a;
            lp2 += (lp1 - lp2) * a;
            float whistle = Mathf.Sin(2 * Mathf.PI * (620f + 140f * gust) * t) * 0.04f * gust;
            return lp2 * 3f * gust + whistle;
        }, 0.6f, 0.4f);
    }

    /// <summary>Phát 2D một lần.</summary>
    public static AudioSource Play2D(AudioClip clip, float volume = 1f, bool loop = false)
    {
        if (clip == null) return null;
        var go = new GameObject("HorrorSfx_" + clip.name);
        var s = go.AddComponent<AudioSource>();
        s.clip = clip; s.volume = volume; s.loop = loop; s.spatialBlend = 0f; s.ignoreListenerPause = true;
        s.Play();
        if (!loop) Object.Destroy(go, clip.length + 0.1f);
        return s;
    }

    /// <summary>Phát 3D tại một điểm (có thể lặp, gắn theo vật).</summary>
    public static AudioSource Play3D(AudioClip clip, Vector3 at, float volume = 1f, bool loop = false, float minDist = 6f, float maxDist = 120f, Transform follow = null)
    {
        if (clip == null) return null;
        var go = new GameObject("HorrorSfx3D_" + clip.name);
        go.transform.position = at;
        if (follow != null) go.transform.SetParent(follow, true);
        var s = go.AddComponent<AudioSource>();
        s.clip = clip; s.volume = volume; s.loop = loop; s.spatialBlend = 1f;
        s.rolloffMode = AudioRolloffMode.Logarithmic; s.minDistance = minDist; s.maxDistance = maxDist;
        s.Play();
        if (!loop) Object.Destroy(go, clip.length + 0.1f);
        return s;
    }
}
