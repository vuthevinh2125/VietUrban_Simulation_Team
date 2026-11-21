using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;

/// <summary>
/// Ngôn ngữ của game (English, Tiếng Việt, 中文, 日本語, 한국어), lưu bằng PlayerPrefs.
///
/// Cách dịch: mọi chữ hiển thị bằng TextMeshPro đi qua một bộ tiền xử lý (ITextPreprocessor) ngay trước khi vẽ, nên code
/// gọi <c>text = "..."</c> giữ nguyên câu gốc (tiếng Anh hoặc tiếng Việt). Bảng dịch nằm ở
/// Resources/Localization/strings.txt (mỗi dòng: câu gốc, rồi 5 bản dịch, cách nhau bằng tab). Tìm theo thứ tự:
///  1. đúng nguyên câu;
///  2. câu có chỗ trống ({0}, {1}...), ví dụ "Thief caught! Recovered {0}." - phần điền vào được dịch tiếp nếu có trong bảng
///     (tên mặt hàng, thời tiết...);
///  3. bỏ thẻ định dạng bao ngoài (&lt;b&gt;...&lt;/b&gt;) rồi dịch phần trong;
///  4. dịch từng dòng, rồi từng đoạn ngăn bằng " · ".
/// Không tìm thấy thì giữ nguyên và (trong Editor / bản Development) ghi câu đó vào loc_missing.txt để bổ sung bảng.
/// Phông: Bungee / Anton / Patrick Hand không có dấu tiếng Việt và chữ Hán / Kana / Hangul -> thêm Be Vietnam Pro và phông
/// Noto Sans của ngôn ngữ đang chọn vào danh sách phông dự phòng chung của TextMeshPro.
/// </summary>
public static class Loc
{
    public enum Lang { En = 0, Vi = 1, Zh = 2, Ja = 3, Ko = 4 }
    public const int Count = 5;
    /// <summary>Tên mỗi ngôn ngữ viết bằng chính ngôn ngữ đó (không dịch).</summary>
    public static readonly string[] NativeNames = { "English", "Tiếng Việt", "中文", "日本語", "한국어" };

    private const string KLang = "set.language";
    public static Lang Current { get; private set; } = Lang.En;
    public static event System.Action OnChanged;

    private sealed class Entry { public readonly string[] t = new string[Count]; }
    private sealed class Template { public Regex re; public Entry e; public string anchor; public int args; }

    private static bool loaded;
    private static readonly Dictionary<string, Entry> exact = new Dictionary<string, Entry>();
    private static readonly List<Template> templates = new List<Template>();
    private static readonly Dictionary<string, string> cache = new Dictionary<string, string>();
    private static readonly HashSet<string> missing = new HashSet<string>();
    public static bool MissingDirty { get; set; }
    public static IEnumerable<string> Missing => missing;

    private static readonly Regex placeholder = new Regex(@"\{(\d+)\}");
    private static readonly Regex wrapper = new Regex(@"^((?:<[^>]+>)+)(.*?)((?:</[^>]+>)+)$", RegexOptions.Singleline);

    // ================= Chọn ngôn ngữ =================
    public static void Load()
    {
        EnsureTable();
        int saved = PlayerPrefs.GetInt(KLang, -1);
        Current = saved >= 0 && saved < Count ? (Lang)saved : FromSystem();
        ApplyFonts();
    }

    private static Lang FromSystem()
    {
        switch (Application.systemLanguage)
        {
            case SystemLanguage.Vietnamese: return Lang.Vi;
            case SystemLanguage.Chinese:
            case SystemLanguage.ChineseSimplified:
            case SystemLanguage.ChineseTraditional: return Lang.Zh;
            case SystemLanguage.Japanese: return Lang.Ja;
            case SystemLanguage.Korean: return Lang.Ko;
            default: return Lang.En;
        }
    }

    public static void Set(Lang lang)
    {
        lang = (Lang)(((int)lang % Count + Count) % Count);
        PlayerPrefs.SetInt(KLang, (int)lang);
        PlayerPrefs.Save();
        if (lang == Current) return;
        Current = lang;
        cache.Clear();
        ApplyFonts();
        OnChanged?.Invoke();
    }

    public static void Next() => Set((Lang)((int)Current + 1));
    public static void Previous() => Set((Lang)((int)Current - 1));
    public static string CurrentName => NativeNames[(int)Current];

    // ================= Dịch =================
    /// <summary>Bản dịch của một câu đã hoàn chỉnh (đã điền số, tên...). Không có trong bảng thì trả lại nguyên câu.</summary>
    public static string T(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        EnsureTable();
        if (cache.TryGetValue(text, out string done)) return done;
        string result = Translate(text, 0, out bool found);
        if (!found && Debug.isDebugBuild && LooksLikeWords(text) && missing.Count < 5000 && missing.Add(text)) MissingDirty = true;
        if (cache.Count > 6000) cache.Clear();
        cache[text] = result;
        return result;
    }

    private static string Translate(string text, int depth, out bool found)
    {
        found = true;
        if (exact.TryGetValue(text, out Entry e)) return Pick(e, text);
        foreach (Template tp in templates)
        {
            if (text.IndexOf(tp.anchor, System.StringComparison.Ordinal) < 0) continue;
            Match m = tp.re.Match(text);
            if (!m.Success) continue;
            string pattern = Pick(tp.e, null);
            if (pattern == null) return text;
            var args = new string[tp.args];
            for (int i = 0; i < tp.args; i++)
            {
                string a = m.Groups["a" + i].Value;
                args[i] = depth < 2 && exact.TryGetValue(a, out Entry ae) ? Pick(ae, a) : a;
            }
            return placeholder.Replace(pattern, pm => { int k = int.Parse(pm.Groups[1].Value); return k < args.Length ? args[k] : pm.Value; });
        }
        if (depth >= 3) { found = false; return text; }

        Match w = wrapper.Match(text);
        if (w.Success && w.Groups[2].Length > 0)
        {
            string inner = Translate(w.Groups[2].Value, depth + 1, out found);
            if (found) return w.Groups[1].Value + inner + w.Groups[3].Value;
        }
        if (text.IndexOf('\n') >= 0) return Join(text.Split('\n'), "\n", depth, out found);
        if (text.Contains(" · ")) return Join(text.Split(new[] { " · " }, System.StringSplitOptions.None), " · ", depth, out found);
        string trimmed = text.Trim();
        if (trimmed.Length > 0 && trimmed.Length < text.Length)
        {
            string t = Translate(trimmed, depth + 1, out found);
            if (found) return text.Substring(0, text.IndexOf(trimmed, System.StringComparison.Ordinal)) + t + text.Substring(text.IndexOf(trimmed, System.StringComparison.Ordinal) + trimmed.Length);
        }
        found = false;
        return text;
    }

    private static string Join(string[] parts, string sep, int depth, out bool found)
    {
        found = true;
        var sb = new StringBuilder();
        for (int i = 0; i < parts.Length; i++)
        {
            if (i > 0) sb.Append(sep);
            if (!LooksLikeWords(parts[i])) { sb.Append(parts[i]); continue; }
            sb.Append(Translate(parts[i], depth + 1, out bool f));
            if (!f) found = false;
        }
        return sb.ToString();
    }

    private static string Pick(Entry e, string fallback)
    {
        string s = e.t[(int)Current];
        return string.IsNullOrEmpty(s) ? fallback : s;
    }

    private static bool LooksLikeWords(string s)
    {
        int letters = 0;
        foreach (char c in s) if (char.IsLetter(c) && ++letters >= 2) return true;
        return false;
    }

    // ================= Bảng dịch =================
    private static void EnsureTable()
    {
        if (loaded) return;
        loaded = true;
        TextAsset ta = Resources.Load<TextAsset>("Localization/strings");
        if (ta == null) { Debug.LogWarning("[Loc] Resources/Localization/strings.txt not found - text stays as written."); return; }
        int rows = 0;
        foreach (string raw in ta.text.Split('\n'))
        {
            string line = raw.TrimEnd('\r');
            if (line.Length == 0 || line.StartsWith("#")) continue;
            string[] cols = line.Split('\t');
            if (cols.Length < 2) continue;
            string src = Unescape(cols[0]);
            if (src == "src") continue;                                  // dòng tiêu đề
            var e = new Entry();
            for (int i = 0; i < Count; i++) e.t[i] = i + 1 < cols.Length ? Unescape(cols[i + 1]) : "";
            if (placeholder.IsMatch(src)) AddTemplate(src, e);
            else exact[src] = e;
            rows++;
        }
        templates.Sort((a, b) => b.anchor.Length.CompareTo(a.anchor.Length));   // mẫu cụ thể hơn (đoạn cố định dài hơn) thử trước
        Debug.Log($"[Loc] {rows} strings ({templates.Count} templates).");
    }

    private static void AddTemplate(string src, Entry e)
    {
        var sb = new StringBuilder("^");
        string longest = "";
        int last = 0, args = 0;
        var seen = new HashSet<int>();
        foreach (Match m in placeholder.Matches(src))
        {
            string lit = src.Substring(last, m.Index - last);
            if (lit.Length > longest.Length) longest = lit;
            sb.Append(Regex.Escape(lit));
            int k = int.Parse(m.Groups[1].Value);
            sb.Append(seen.Add(k) ? $"(?<a{k}>.+?)" : $"\\k<a{k}>");
            args = Mathf.Max(args, k + 1);
            last = m.Index + m.Length;
        }
        string tail = src.Substring(last);
        if (tail.Length > longest.Length) longest = tail;
        sb.Append(Regex.Escape(tail)).Append('$');
        if (longest.Trim().Length < 2) return;                               // mẫu toàn chỗ trống: khớp mọi thứ, bỏ
        templates.Add(new Template { re = new Regex(sb.ToString(), RegexOptions.Singleline | RegexOptions.CultureInvariant), e = e, anchor = longest, args = args });
    }

    /// <summary>Ô trong bảng viết như chuỗi C#: \n xuống dòng, \t tab, \" ngoặc kép, \\ gạch chéo; {{ }} là ngoặc nhọn thật.</summary>
    private static string Unescape(string s)
    {
        if (s.IndexOf('\\') < 0 && s.IndexOf("{{", System.StringComparison.Ordinal) < 0 && s.IndexOf("}}", System.StringComparison.Ordinal) < 0) return s;
        var sb = new StringBuilder(s.Length);
        for (int i = 0; i < s.Length; i++)
        {
            char c = s[i];
            if (c == '\\' && i + 1 < s.Length)
            {
                char n = s[++i];
                sb.Append(n == 'n' ? '\n' : n == 't' ? '\t' : n == 'r' ? '\r' : n);
                continue;
            }
            if ((c == '{' || c == '}') && i + 1 < s.Length && s[i + 1] == c) { sb.Append(c); i++; continue; }
            sb.Append(c);
        }
        return sb.ToString();
    }

    // ================= Phông =================
    private static TMP_FontAsset viet;
    private static readonly TMP_FontAsset[] cjk = new TMP_FontAsset[Count];
    private static readonly string[] cjkFile = { null, null, "NotoSansSC-Medium", "NotoSansJP-Medium", "NotoSansKR-Medium" };

    /// <summary>
    /// Danh sách phông dự phòng chung của TextMeshPro: Be Vietnam Pro (dấu tiếng Việt) và phông Noto của ngôn ngữ đang chọn
    /// (chữ Hán vẽ theo kiểu Trung, Nhật hoặc Hàn tuỳ ngôn ngữ). Phông tạo ở chế độ động: chỉ vẽ những chữ thật sự dùng.
    /// </summary>
    public static void ApplyFonts()
    {
        List<TMP_FontAsset> list = TMP_Settings.fallbackFontAssets;
        if (list == null) { list = new List<TMP_FontAsset>(); TMP_Settings.fallbackFontAssets = list; }
        foreach (TMP_FontAsset f in cjk) if (f != null) list.Remove(f);
        if (viet == null) viet = ShopUI.Body;
        // Thêm vào CUỐI danh sách: các phông dự phòng sẵn có của dự án vẫn được dùng trước như cũ (giữ nguyên cỡ dòng),
        // phông mới chỉ lấp những chữ còn thiếu.
        if (viet != null) { list.Remove(viet); list.Add(viet); }
        int i = (int)Current;
        if (cjkFile[i] != null)
        {
            if (cjk[i] == null)
            {
                Font font = Resources.Load<Font>("Fonts/" + cjkFile[i]);
                if (font != null)
                {
                    try { cjk[i] = TMP_FontAsset.CreateFontAsset(font, 64, 6, UnityEngine.TextCore.LowLevel.GlyphRenderMode.SDFAA, 1024, 1024, AtlasPopulationMode.Dynamic, true); }
                    catch { cjk[i] = null; }
                    if (cjk[i] != null) cjk[i].name = cjkFile[i] + " (runtime)";
                }
                if (cjk[i] == null) Debug.LogWarning("[Loc] Font Resources/Fonts/" + cjkFile[i] + " not found.");
            }
            if (cjk[i] != null) list.Add(cjk[i]);
        }
    }

    /// <summary>Gỡ phông tạo lúc chạy khỏi danh sách dự phòng chung khi thoát (trong Editor danh sách đó là một asset).</summary>
    public static void RemoveRuntimeFonts()
    {
        List<TMP_FontAsset> list = TMP_Settings.fallbackFontAssets;
        if (list == null) return;
        foreach (TMP_FontAsset f in cjk) if (f != null) list.Remove(f);
        if (viet != null) list.Remove(viet);
    }

    // ================= Gắn vào chữ =================
    private sealed class Preprocessor : ITextPreprocessor
    {
        public string PreprocessText(string text) => T(text);
    }
    public static readonly ITextPreprocessor TextPreprocessor = new Preprocessor();

    /// <summary>Bộ "không dịch": cho chữ đã được dịch sẵn bằng <see cref="T"/> hoặc chữ người chơi tự gõ.</summary>
    private sealed class RawPreprocessor : ITextPreprocessor
    {
        public string PreprocessText(string text) => text;
    }
    public static readonly ITextPreprocessor Raw = new RawPreprocessor();

    /// <summary>Cho một ô chữ đi qua bảng dịch (gọi ngay khi tạo chữ để không bị nháy câu gốc một khung hình).</summary>
    public static TText Attach<TText>(TText text) where TText : TMP_Text
    {
        if (text != null && text.textPreprocessor == null) text.textPreprocessor = TextPreprocessor;
        return text;
    }
}
