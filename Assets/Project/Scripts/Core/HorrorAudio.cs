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
///  - Funeral(): nhạc đám ma kiểu Việt (kèn bầu luyến láy điệu oán, trống, não bạt, phách, tiu) - lặp ~18.6 s.
///  - OldLaugh(): ông cụ cười khanh khách rồi khò khè, ma mị.   - CrowdLaugh(): cả đám đông cùng cười, vang vọng.
/// Muốn dùng file thu âm thật: đặt FuneralVN / OldLaugh / CrowdLaugh (.ogg / .wav / .mp3) vào Resources/Audio/Horror,
/// code tự dùng file đó thay cho âm tạo bằng code.
/// Prewarm(): tạo dần từng clip (mỗi lần gọi một clip) để không khựng game khi cần.
/// </summary>
public static class HorrorAudio
{
    private const int Rate = 32000;
    private static readonly Dictionary<string, AudioClip> cache = new Dictionary<string, AudioClip>();
    private static System.Random rnd = new System.Random(1234);

    private static float Noise() => (float)rnd.NextDouble() * 2f - 1f;

    private static AudioClip Make(string name, float seconds, System.Func<int, float, float> sample, float gain = 1f, float edge = 0.01f, float reverb = 0f)
    {
        if (cache.TryGetValue(name, out AudioClip c) && c != null) return c;
        int n = Mathf.Max(1, (int)(Rate * seconds));
        var data = new float[n];
        for (int i = 0; i < n; i++) data[i] = sample(i, i / (float)Rate);
        if (reverb > 0f) Reverb(data, reverb);
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
            case 7: Wail(); return false;
            case 8: Whisper(); return false;
            case 9: Sting(); return false;
            case 10: Scream(); return false;
            case 11: Drip(); return false;
            case 12: Breath(); return false;
            case 13: Crack(); return false;
            case 14: Bang(); return false;
            case 15: Funeral(); return false;
            case 16: OldLaugh(); return false;
            case 17: CrowdLaugh(); return false;
            case 18: Dread(); return false;
            default: return true;
        }
    }

    /// <summary>
    /// Nhạc đám ma (rùng rợn): hai kèn lệch tông nhau (rung "è è" như phách), điệu ngũ cung chậm, cuối mỗi câu kèn trườn tụt xuống;
    /// trống cái nặng, thanh la; nền là tiếng ù trầm và tiếng khóc than vọng lại - lặp liền mạch ~12.8 s.
    /// </summary>
    public static AudioClip FuneralBand() => Funeral();

    /// <summary>Bản nhạc đám ma cũ (kèn lệch tông) - giữ lại để tham khảo.</summary>
    private static AudioClip FuneralBandOld()
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

    // ================= Thu âm thật (nếu có) =================
    private static readonly HashSet<string> missing = new HashSet<string>();

    /// <summary>File âm thanh thật trong Resources/Audio/Horror (null nếu không có).</summary>
    private static AudioClip Recorded(string name)
    {
        if (missing.Contains(name)) return null;
        if (cache.TryGetValue("rec_" + name, out AudioClip c) && c != null) return c;
        c = Resources.Load<AudioClip>("Audio/Horror/" + name);
        if (c == null) { missing.Add(name); return null; }
        cache["rec_" + name] = c;
        return c;
    }

    // ================= Bộ lọc / tiếng vang =================
    /// <summary>Bộ lọc cộng hưởng (band-pass) - dùng làm formant giọng người, thân kèn.</summary>
    private class Reso
    {
        private float b0, b2, a1, a2, x1, x2, y1, y2;
        public Reso(float f, float q) { Set(f, q); }
        public void Set(float f, float q)
        {
            float w = 2f * Mathf.PI * Mathf.Clamp(f, 40f, Rate * 0.45f) / Rate;
            float al = Mathf.Sin(w) / (2f * q), a0 = 1f + al;
            b0 = al / a0; b2 = -al / a0; a1 = -2f * Mathf.Cos(w) / a0; a2 = (1f - al) / a0;
        }
        public float Run(float x)
        {
            float y = b0 * x + b2 * x2 - a1 * y1 - a2 * y2;
            x2 = x1; x1 = x; y2 = y1; y1 = y;
            return y;
        }
    }

    /// <summary>Tiếng vang (4 comb + 2 all-pass, kiểu Schroeder), trộn mix.</summary>
    private static void Reverb(float[] d, float mix)
    {
        int[] cd = { 1171, 1373, 1601, 1879 };
        float[] fb = { 0.74f, 0.72f, 0.70f, 0.68f };
        var wet = new float[d.Length];
        for (int k = 0; k < cd.Length; k++)
        {
            var buf = new float[cd[k]]; int p = 0; float lp = 0f;
            for (int i = 0; i < d.Length; i++)
            {
                float o = buf[p];
                lp += (o - lp) * 0.45f;                                  // tiếng vang tối dần
                buf[p] = d[i] + lp * fb[k];
                p = (p + 1) % buf.Length;
                wet[i] += o * 0.25f;
            }
        }
        foreach (int ad in new[] { 389, 137 })
        {
            var buf = new float[ad]; int p = 0;
            for (int i = 0; i < d.Length; i++)
            {
                float o = buf[p], x = wet[i];
                buf[p] = x + o * 0.5f;
                wet[i] = o - x * 0.5f;
                p = (p + 1) % buf.Length;
            }
        }
        for (int i = 0; i < d.Length; i++) d[i] = d[i] * (1f - mix * 0.5f) + wet[i] * mix;
    }

    // ================= Đám ma Việt Nam =================
    /// <summary>Nhạc đám ma kiểu Việt (dùng file thu âm FuneralVN nếu có).</summary>
    public static AudioClip Funeral() => Recorded("FuneralVN") ?? Recorded("Chanting") ?? FuneralVN();   // chưa có kèn thật thì dùng tiếng tụng kinh

    /// <summary>Có file thu âm thật tên này trong Resources/Audio/Horror.</summary>
    public static bool HasRecorded(string name) => Recorded(name) != null;

    /// <summary>
    /// Phường kèn đám ma (tạo bằng code): kèn bầu giọng mũi, luyến láy, rung sâu ở nốt dài theo điệu oán (Hò Xự Xang Xê Cống Liu),
    /// kèn thứ hai theo bè lệch nhẹ; trống "tùng", não bạt "xèng", phách gõ nhịp, tiu "tinh"; tiếng khóc vọng; vang như giữa phố đêm.
    /// </summary>
    public static AudioClip FuneralVN()
    {
        // cao độ (nửa cung) so với Hò = Sol 4; Cống non (oán) ~ 8.6
        float[] deg = { 7, 8.6f, 12, 8.6f, 7,   5, 7, 5, 2,   0, 2, 5, 7, 5,   2, 0, 2,
                        12, 8.6f, 7, 8.6f, 7,   5, 2, 5,   7, 5, 2, 0, -3,   0, 2, 0 };
        float[] len = { 1, 0.5f, 0.5f, 1, 1,   2, 0.5f, 0.5f, 1,   1, 0.5f, 0.5f, 1, 1,   1.5f, 0.5f, 2,
                        1, 1, 0.5f, 0.5f, 1,   1.5f, 0.5f, 2,   1, 0.5f, 0.5f, 1, 1,   1.5f, 0.5f, 2 };
        const float beat = 0.58f, ho = 392f;
        var start = new float[deg.Length];
        float total = 0f;
        for (int i = 0; i < deg.Length; i++) { start[i] = total; total += len[i] * beat; }
        const int H = 11;
        var w = new float[H + 1];
        double ph1 = 0, ph2 = 0, phW = 0;
        float f = ho * Mathf.Pow(2f, deg[deg.Length - 1] / 12f), cymLp = 0f, cymPrev = 0f, wl = 0f;
        int k = 0;
        Reso body = new Reso(1150f, 2.2f), nasal = new Reso(2600f, 3f);
        return Make("FuneralVN", total, (i, t) =>
        {
            while (k + 1 < deg.Length && t >= start[k + 1]) k++;
            float nt = t - start[k], nl = len[k] * beat;
            float target = ho * Mathf.Pow(2f, deg[k] / 12f);
            if (nt < 0.05f && len[k] >= 1f) target *= Mathf.Pow(2f, 1.6f / 12f);          // láy: chạm nốt trên rồi luyến xuống
            f += (target - f) * (nt < 0.08f ? 0.0035f : 0.0012f);
            float depth = nl >= beat * 0.99f ? Mathf.Lerp(0.004f, 0.03f, Mathf.Clamp01((nt - 0.15f) / (nl * 0.7f))) : 0.004f;   // rung sâu dần
            float vib = 1f + depth * Mathf.Sin(2f * Mathf.PI * 5.6f * t);
            ph1 += 2 * System.Math.PI * f * vib / Rate;
            ph2 += 2 * System.Math.PI * f * 1.0075f * vib / Rate;
            if ((i & 31) == 0)
                for (int h = 1; h <= H; h++)
                {
                    float fh = f * h;
                    w[h] = (Mathf.Exp(-Sq((fh - 1150f) / 450f)) + 0.7f * Mathf.Exp(-Sq((fh - 2600f) / 650f)) + 0.18f) / Mathf.Pow(h, 0.55f);
                }
            float s1 = 0f, s2 = 0f;
            for (int h = 1; h <= H; h++) { s1 += Mathf.Sin((float)(ph1 * h)) * w[h]; s2 += Mathf.Sin((float)(ph2 * h)) * w[h]; }
            float tongue = Mathf.Clamp01(nt / 0.025f) * (nt > nl - 0.03f ? Mathf.Clamp01((nl - nt) / 0.03f) * 0.6f + 0.4f : 1f);
            float breath = Noise() * 0.06f;
            float reed = (float)System.Math.Tanh((s1 + 0.55f * s2 + breath) * 1.8f) * tongue;
            reed = reed * 0.6f + body.Run(reed) * 0.5f + nasal.Run(reed) * 0.4f;

            float bt = t % beat; int bi = (int)(t / beat);
            // trống: "tùng" mạnh phách 1 & 3, nhẹ phách 2 & 4
            float drumAmp = bi % 2 == 0 ? 1f : 0.45f;
            float drum = Mathf.Sin(2 * Mathf.PI * (72f + 45f * Mathf.Exp(-bt * 25f)) * bt) * Mathf.Exp(-bt * 7f) * drumAmp;
            // não bạt "xèng" phách 2 & 4
            float cym = 0f;
            if (bi % 2 == 1)
            {
                float n = Noise();
                cymLp = 0.82f * (cymLp + n - cymPrev); cymPrev = n;           // nhiễu cao
                cym = (cymLp * 0.8f + Mathf.Sin(2 * Mathf.PI * 3170f * bt) * 0.25f + Mathf.Sin(2 * Mathf.PI * 4510f * bt) * 0.2f) * Mathf.Exp(-bt * 5.5f);
            }
            // phách gõ mỗi nửa phách
            float ht = t % (beat * 0.5f);
            float clap = Mathf.Sin(2 * Mathf.PI * 2350f * ht) * Mathf.Exp(-ht * 140f) * 0.5f;
            // tiu "tinh" đầu mỗi 8 phách
            float gt = t % (beat * 8f);
            float tiu = (Mathf.Sin(2 * Mathf.PI * 1460f * gt) + 0.6f * Mathf.Sin(2 * Mathf.PI * 2035f * gt)) * Mathf.Exp(-gt * 1.6f) * 0.3f;
            // tiếng khóc vọng lại
            float wt = t % 9.3f;
            float wf = 560f + 140f * Mathf.Sin(wt * 0.8f) - 140f * wt / 9.3f;
            phW += 2 * System.Math.PI * wf * (1f + 0.035f * Mathf.Sin(t * 33f)) / Rate;
            float wv = Mathf.Sin((float)phW) + 0.45f * Mathf.Sin(2 * (float)phW);
            wl += (wv - wl) * 0.2f;
            float wenv = Mathf.Clamp01(wt / 0.7f) * Mathf.Clamp01((5f - wt) / 1.5f);
            return reed * 0.62f + drum * 0.55f + cym * 0.3f + clap * 0.22f + tiu + wl * wenv * 0.1f;
        }, 0.9f, 0.02f, 0.28f);
    }

    private static float Sq(float x) => x * x;

    /// <summary>Một giọng cười: chuỗi âm "hà / hê / hơ" (nguồn thanh quản + hơi thở qua 3 formant).</summary>
    private class Laugher
    {
        private readonly Reso f1 = new Reso(750f, 6f), f2 = new Reso(1200f, 8f), f3 = new Reso(2600f, 9f);
        private double ph;
        private float jit;
        public float Sample(float f0, float voiced, float breath, int vowel, float growl)
        {
            if (vowel != lastVowel)
            {
                lastVowel = vowel;
                if (vowel == 0) { f1.Set(760f, 6f); f2.Set(1180f, 8f); f3.Set(2550f, 9f); }      // a
                else if (vowel == 1) { f1.Set(470f, 6f); f2.Set(1950f, 9f); f3.Set(2650f, 9f); }  // ê
                else { f1.Set(520f, 6f); f2.Set(950f, 7f); f3.Set(2450f, 8f); }                    // ơ
            }
            jit += (Noise() - jit) * 0.02f;
            ph += f0 * (1f + 0.04f * jit) / Rate;
            float saw = (float)(ph - System.Math.Floor(ph)) * 2f - 1f;
            float sub = (float)((ph * 0.5) - System.Math.Floor(ph * 0.5)) * 2f - 1f;   // giọng khàn trầm nửa tần số (ma mị)
            float src = (saw + sub * growl) * voiced + Noise() * breath;
            return f1.Run(src) + f2.Run(src) * 0.65f + f3.Run(src) * 0.35f;
        }
        private int lastVowel = -1;
    }

    /// <summary>Nhạc nền rợn người trong suốt sự kiện (dùng file Dread nếu có).</summary>
    public static AudioClip Dread() => Recorded("Dread") ?? DreadSynth();

    /// <summary>
    /// Nền rợn người (lặp liền mạch 24 s): ù trầm hai nốt lệch nhau đập "u-u-u", nhịp thình thịch rất trầm như tim khổng lồ,
    /// tiếng dây kim loại cao rít nghịch tai phồng lên xẹp xuống, tiếng kim loại cào ken két và hơi thở gió vọng lại.
    /// </summary>
    private static AudioClip DreadSynth()
    {
        const float L = 24f;
        float lp1 = 0f, lp2 = 0f, hp = 0f, prevN = 0f;
        Reso scrape = new Reso(2200f, 14f), scrape2 = new Reso(3300f, 18f);
        return Make("Dread", L, (i, t) =>
        {
            float P(float period) => 2f * Mathf.PI * t / period;          // các chu kỳ chia hết 24 s -> lặp không lộ mối nối
            // ù trầm: 41 Hz + 43 Hz (đập 2 lần / giây), cộng bè quãng năm giảm
            float drone = Mathf.Sin(2 * Mathf.PI * 41f * t) + Mathf.Sin(2 * Mathf.PI * 43f * t) * 0.8f + Mathf.Sin(2 * Mathf.PI * 58f * t) * 0.35f;
            drone *= 0.55f + 0.25f * Mathf.Sin(P(12f));
            // "tim khổng lồ" mỗi 1.5 s
            float bt = t % 1.5f;
            float thump = Mathf.Sin(2 * Mathf.PI * 34f * bt) * Mathf.Exp(-bt * 9f) + 0.6f * Mathf.Sin(2 * Mathf.PI * 30f * (bt - 0.22f)) * (bt > 0.22f ? Mathf.Exp(-(bt - 0.22f) * 9f) : 0f);
            // dây cao rít (tam cung nghịch tai), phồng lên chậm
            float sw = Mathf.Pow(Mathf.Max(0f, Mathf.Sin(P(8f) - 1f)), 2f);
            float strings = (Mathf.Sin(2 * Mathf.PI * 1244f * t + 0.3f * Mathf.Sin(P(0.25f))) + Mathf.Sin(2 * Mathf.PI * 1318f * t) * 0.8f
                             + Mathf.Sin(2 * Mathf.PI * 1760f * t + 0.2f * Mathf.Sin(P(0.2f))) * 0.5f) * sw;
            // kim loại cào: nhiễu qua cộng hưởng hẹp, hai lần mỗi vòng
            float n = Noise();
            hp = 0.9f * (hp + n - prevN); prevN = n;
            float sc1 = Mathf.Clamp01(1f - Mathf.Abs(t - 7f) / 0.9f), sc2 = Mathf.Clamp01(1f - Mathf.Abs(t - 17.5f) / 1.2f);
            scrape.Set(2200f + 900f * Mathf.Clamp01((t - 6.1f) / 1.8f), 14f);
            float metal = scrape.Run(hp) * sc1 * 3f + scrape2.Run(hp) * sc2 * 3f;
            // gió / hơi thở xa
            lp1 += (n - lp1) * 0.02f; lp2 += (lp1 - lp2) * 0.02f;
            float air = lp2 * 6f * (0.4f + 0.6f * Mathf.Pow(Mathf.Sin(P(6f)) * 0.5f + 0.5f, 2f));
            return drone * 0.5f + thump * 0.65f + strings * 0.08f + metal * 0.25f + air * 0.5f;
        }, 0.8f, 0.001f, 0.35f);
    }

    /// <summary>Tiếng tụng kinh (chỉ có khi đặt file Chanting vào Resources/Audio/Horror).</summary>
    public static AudioClip Chanting() => Recorded("Chanting");

    /// <summary>Ông cụ cười kinh dị (dùng file OldLaugh nếu có).</summary>
    public static AudioClip OldLaugh() => Recorded("OldLaugh") ?? OldLaughSynth();

    /// <summary>"Khà... khà..." chậm, rồi dồn dập "hà hà hà hê hê" cao dần, khò khè "hơơơ", hai tiếng "hê... hê" thì thào. ~5.6 s.</summary>
    private static AudioClip OldLaughSynth()
    {
        var syl = new List<(float t, float d, float f0, int v)>
        {
            (0.00f, 0.38f, 96f, 0), (0.72f, 0.36f, 92f, 0),
        };
        for (int s = 0; s < 11; s++) syl.Add((1.35f + s * 0.155f, 0.12f, 108f + s * 8f, s < 6 ? 0 : 1));
        syl.Add((3.15f, 1.05f, 205f, 2));
        syl.Add((4.55f, 0.22f, 150f, 1)); syl.Add((5.05f, 0.25f, 140f, 1));
        var v = new Laugher();
        int cur = 0;
        return Make("OldLaugh", 5.6f, (i, t) =>
        {
            while (cur + 1 < syl.Count && t >= syl[cur + 1].t) cur++;
            var y = syl[cur];
            float lt = t - y.t;
            if (lt < 0f || lt > y.d + 0.05f) return v.Sample(100f, 0f, 0.004f, y.v, 0f);
            float u = lt / y.d;
            float f0 = y.v == 2 ? Mathf.Lerp(y.f0, 120f, u) : y.f0 * (1f - 0.12f * u);
            float asp = lt < 0.045f ? 1f : 0.25f;                                         // "h" bật hơi đầu tiếng
            float env = Mathf.Clamp01(lt / 0.02f) * Mathf.Clamp01((y.d + 0.05f - lt) / 0.08f);
            float voiced = lt < 0.03f ? 0f : env;
            return v.Sample(f0, voiced, asp * env * (y.v == 2 ? 0.9f : 0.6f), y.v, y.v == 2 ? 0.5f : 0.35f);
        }, 0.9f, 0.01f, 0.4f);
    }

    /// <summary>Cả đám đông cùng cười (dùng file CrowdLaugh nếu có).</summary>
    public static AudioClip CrowdLaugh() => Recorded("CrowdLaugh") ?? CrowdLaughSynth();

    /// <summary>12 giọng (nam trầm, nữ the thé) cười lệch nhịp nhau, cao độ trôi dần, vang vọng như giữa phố vắng. ~7 s.</summary>
    private static AudioClip CrowdLaughSynth()
    {
        const int N = 12;
        var r = new System.Random(77);
        var voices = new Laugher[N];
        var f0 = new float[N]; var rate = new float[N]; var off = new float[N]; var gain = new float[N]; var vow = new int[N];
        for (int k = 0; k < N; k++)
        {
            voices[k] = new Laugher();
            bool female = k % 3 == 0;
            f0[k] = female ? 200f + (float)r.NextDouble() * 70f : 95f + (float)r.NextDouble() * 50f;
            rate[k] = 4.2f + (float)r.NextDouble() * 2.2f;
            off[k] = (float)r.NextDouble() * 1.4f;
            gain[k] = 0.6f + (float)r.NextDouble() * 0.5f;
            vow[k] = r.Next(2);
        }
        return Make("CrowdLaugh", 7f, (i, t) =>
        {
            float sum = 0f;
            for (int k = 0; k < N; k++)
            {
                float lt = t - off[k];
                if (lt < 0f || lt > 6.2f) continue;
                float sp = lt * rate[k];
                float u = sp - Mathf.Floor(sp);
                float env = Mathf.Clamp01(u / 0.08f) * Mathf.Clamp01((0.62f - u) / 0.18f);
                float pitch = f0[k] * (1f + 0.25f * Mathf.Sin(lt * 0.9f + k) + 0.1f * (1f - u));
                float fade = Mathf.Clamp01(lt / 0.4f) * Mathf.Clamp01((6.2f - lt) / 1.2f);
                sum += voices[k].Sample(pitch, (u > 0.04f ? env : 0f), (u < 0.06f ? 0.7f : 0.15f) * env, ((int)sp + vow[k]) % 2, 0.3f) * gain[k] * fade;
            }
            return sum;
        }, 0.9f, 0.05f, 0.5f);
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
