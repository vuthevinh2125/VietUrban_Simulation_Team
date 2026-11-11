using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// Kệ / tủ có sẵn trong cửa hàng lúc bắt đầu. Giống nội thất đã mua: nhìn vào bấm [F] để dời chỗ, tự thiết kế lại cửa hàng.
/// Vị trí mới được lưu theo Id (đường dẫn trong Hierarchy, tính lúc vừa nạp scene).
/// Gắn bằng Tools > Store Setup > 18 (bỏ static batching + thay vùng NavMesh bằng NavMeshObstacle để dời được).
/// </summary>
public class BuiltInFurniture : MonoBehaviour
{
    public static readonly List<BuiltInFurniture> All = new List<BuiltInFurniture>();

    public string Id { get; private set; }
    public Vector3 HomePosition { get; private set; }
    public Quaternion HomeRotation { get; private set; }

    public string DisplayName
    {
        get
        {
            string n = name.ToLowerInvariant();
            if (n.Contains("fridge")) return "Fridge";
            if (n.Contains("freezer")) return "Freezer";
            if (n.Contains("shelf")) return "Shelf";
            if (n.Contains("stall")) return "Stall";
            if (n.Contains("buffet")) return "Display cabinet";
            if (n.Contains("table")) return "Table";
            if (n.Contains("counter")) return "Counter";
            return "Fixture";
        }
    }

    void Awake()
    {
        Id = PathOf(transform);
        HomePosition = transform.position;
        HomeRotation = transform.rotation;
        if (!All.Contains(this)) All.Add(this);
    }

    void OnDestroy() => All.Remove(this);

    public static BuiltInFurniture Find(string id) => All.Find(b => b != null && b.Id == id);

    private static string PathOf(Transform t)
    {
        var sb = new StringBuilder();
        for (Transform c = t; c != null; c = c.parent) sb.Insert(0, "/" + c.name + "#" + c.GetSiblingIndex());
        return sb.ToString();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => All.Clear();
}
