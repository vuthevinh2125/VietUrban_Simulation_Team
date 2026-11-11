using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Nhãn giá trên kệ (giống Supermarket Simulator): mỗi tầng kệ có một nhãn trắng ở mép trước,
/// ghi tên hàng + giá bán hiện tại. Kệ trống thì ẩn nhãn.
/// Tự tạo khi chơi (Shelf tự gắn component này), chỉ đặt ở mặt kệ có lối đi phía trước.
/// </summary>
[RequireComponent(typeof(Shelf))]
public class ShelfPriceLabels : MonoBehaviour
{
    [Tooltip("Kích thước nhãn (rộng x cao, mét)")]
    public Vector2 labelSize = new Vector2(0.16f, 0.065f);
    [Tooltip("Nhãn nằm thấp hơn mặt tầng kệ bao nhiêu (mép kệ)")]
    public float belowSurface = 0.035f;
    [Tooltip("Nhãn nhô ra trước hàng ngoài cùng bao nhiêu")]
    public float frontOffset = 0.13f;

    private Shelf shelf;
    private readonly List<GameObject> labels = new List<GameObject>();
    private readonly List<TextMeshPro> texts = new List<TextMeshPro>();
    private ItemData shownItem;
    private float shownPrice = -1f;
    private bool shownVisible = true;
    private Transform root;
    private Vector3 builtPos;
    private Quaternion builtRot = Quaternion.identity;

    void Start()
    {
        shelf = GetComponent<Shelf>();
        Build();
        PriceManager.OnPriceChanged += OnPriceChanged;
        StockManager.OnStockChanged += Refresh;
        MarketPrices.OnChanged += Refresh; // giá mặc định đi theo giá thị trường hôm nay
        Refresh();
    }

    void OnDestroy()
    {
        PriceManager.OnPriceChanged -= OnPriceChanged;
        StockManager.OnStockChanged -= Refresh;
        MarketPrices.OnChanged -= Refresh;
        if (root != null) Destroy(root.gameObject);
    }

    // Kệ bị dời (mở rộng cửa hàng, dời nội thất) -> nhãn đi theo
    void LateUpdate()
    {
        if (root == null || !transform.hasChanged) return;
        transform.hasChanged = false;
        Quaternion r = transform.rotation * Quaternion.Inverse(builtRot);
        root.SetPositionAndRotation(transform.position - r * builtPos, r);
    }

    private void OnPriceChanged(ItemData item, float price)
    {
        if (shelf != null && item == shelf.CurrentItem) Refresh();
    }

    private void Refresh()
    {
        if (shelf == null) return;
        ItemData item = shelf.CurrentItem;
        bool visible = item != null;
        float price = visible ? PriceManager.GetSellPrice(item) : 0f;
        if (visible == shownVisible && item == shownItem && Mathf.Approximately(price, shownPrice)) return;

        shownVisible = visible; shownItem = item; shownPrice = price;
        foreach (GameObject l in labels) if (l != null) l.SetActive(visible);
        if (!visible) return;

        string text = $"<size=55%>{item.itemName}</size>\n<b>{EconomyManager.Format(price)}</b>";
        foreach (TextMeshPro t in texts) if (t != null) t.text = text;
    }

    // ================= Tạo nhãn =================
    private void Build()
    {
        if (shelf.slots == null || shelf.slots.Length == 0) return;
        Transform first = null;
        foreach (Transform s in shelf.slots) if (s != null) { first = s; break; }
        if (first == null) return;

        root = new GameObject($"PriceLabels_{name}").transform; // không làm con của kệ để khỏi bị méo theo scale kệ
        builtPos = transform.position;
        builtRot = transform.rotation;
        Vector3 right = first.right, fwd = first.forward, origin = first.position;

        // Gom ô theo tầng (cùng độ cao)
        var levels = new List<List<Vector3>>();
        foreach (Transform s in shelf.slots)
        {
            if (s == null) continue;
            List<Vector3> level = levels.Find(l => Mathf.Abs(l[0].y - s.position.y) < 0.04f);
            if (level == null) levels.Add(level = new List<Vector3>());
            level.Add(s.position);
        }

        foreach (List<Vector3> level in levels)
        {
            float minA = float.MaxValue, maxA = float.MinValue, minB = float.MaxValue, maxB = float.MinValue, y = 0f;
            foreach (Vector3 p in level)
            {
                Vector3 d = p - origin;
                float a = Vector3.Dot(d, right), b = Vector3.Dot(d, fwd);
                minA = Mathf.Min(minA, a); maxA = Mathf.Max(maxA, a);
                minB = Mathf.Min(minB, b); maxB = Mathf.Max(maxB, b);
                y += p.y;
            }
            y /= level.Count;
            Vector3 mid = origin + right * ((minA + maxA) * 0.5f);
            mid.y = y - belowSurface;

            Vector3 frontPos = mid + fwd * (maxB + frontOffset);
            Vector3 backPos = mid + fwd * (minB - frontOffset);
            bool front = HasAisle(frontPos, fwd), back = HasAisle(backPos, -fwd);
            if (!front && !back) front = true;
            if (front) CreateLabel(frontPos, fwd);
            if (back) CreateLabel(backPos, -fwd);
        }
    }

    /// <summary>Phía trước vị trí này có lối đi (NavMesh) không -> mới cần nhãn.</summary>
    private static bool HasAisle(Vector3 labelPos, Vector3 outward)
    {
        Vector3 probe = labelPos + outward * 0.5f;
        if (!NavMesh.SamplePosition(probe, out NavMeshHit hit, 2.5f, NavMesh.AllAreas)) return false;
        Vector3 d = hit.position - probe;
        d.y = 0f;
        return d.magnitude < 0.35f;
    }

    private void CreateLabel(Vector3 position, Vector3 outward)
    {
        Quaternion rot = Quaternion.LookRotation(-outward); // người xem đứng phía outward, nhìn vào kệ

        GameObject label = new GameObject("PriceLabel");
        label.transform.SetParent(root, false);
        label.transform.SetPositionAndRotation(position, rot);

        GameObject bg = GameObject.CreatePrimitive(PrimitiveType.Quad);
        bg.name = "Background";
        Object.Destroy(bg.GetComponent<Collider>());
        bg.transform.SetParent(label.transform, false);
        bg.transform.localScale = new Vector3(labelSize.x, labelSize.y, 1f);
        Renderer r = bg.GetComponent<Renderer>();
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        RuntimeMaterials.Apply(r, new Color(1f, 0.98f, 0.85f)); // vật liệu URP (không bị hồng khi build)

        GameObject textGo = new GameObject("Text");
        textGo.transform.SetParent(label.transform, false);
        textGo.transform.localPosition = new Vector3(0f, 0f, -0.002f);
        TextMeshPro tmp = textGo.AddComponent<TextMeshPro>();
        tmp.rectTransform.sizeDelta = labelSize * 0.92f;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 0.05f;
        tmp.fontSizeMax = 0.6f;
        tmp.color = new Color(0.1f, 0.1f, 0.1f);
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Ellipsis;

        labels.Add(label);
        texts.Add(tmp);
    }
}
