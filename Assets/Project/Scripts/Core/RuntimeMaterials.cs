using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Vật liệu cho các vật tạo bằng code (thùng nội thất, nhãn giá, tiền / thẻ, bóng mờ lắp đặt...).
/// GameObject.CreatePrimitive dùng vật liệu mặc định kiểu cũ -> khi BUILD ra .exe với URP sẽ bị HỒNG.
/// Ở đây dùng vật liệu URP để sẵn trong Resources/Materials nên luôn được đưa vào bản build.
/// </summary>
public static class RuntimeMaterials
{
    private static Material litBase, ghostBase;
    private static readonly Dictionary<Color, Material> litCache = new Dictionary<Color, Material>();

    /// <summary>Vật liệu URP Lit màu c (dùng chung cho mọi vật cùng màu).</summary>
    public static Material Lit(Color c)
    {
        if (litCache.TryGetValue(c, out Material m) && m != null) return m;
        if (litBase == null) litBase = Resources.Load<Material>("Materials/RuntimeLit");
        m = litBase != null ? new Material(litBase) : new Material(Shader.Find("Universal Render Pipeline/Lit"));
        SetColor(m, c);
        litCache[c] = m;
        return m;
    }

    /// <summary>Vật liệu trong suốt cho bóng mờ khi lắp nội thất (mỗi lần gọi một bản riêng để đổi màu).</summary>
    public static Material Ghost(Color c)
    {
        if (ghostBase == null) ghostBase = Resources.Load<Material>("Materials/RuntimeGhost");
        Material m = ghostBase != null ? new Material(ghostBase) : new Material(Shader.Find("Universal Render Pipeline/Unlit"));
        SetColor(m, c);
        return m;
    }

    public static void SetColor(Material m, Color c)
    {
        if (m == null) return;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        if (m.HasProperty("_Color")) m.SetColor("_Color", c);
    }

    /// <summary>Gán vật liệu Lit màu c cho renderer (thay vật liệu mặc định của primitive).</summary>
    public static void Apply(Renderer r, Color c)
    {
        if (r != null) r.sharedMaterial = Lit(c);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => litCache.Clear();
}
