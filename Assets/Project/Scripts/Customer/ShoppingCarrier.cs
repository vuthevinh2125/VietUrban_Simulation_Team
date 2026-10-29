using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Giỏ hàng / xe đẩy của khách (giống Supermarket Simulator): khách mua ít thì xách giỏ, mua nhiều thì đẩy xe.
/// Mỗi món lấy từ kệ hiện ra trong giỏ / xe; tới quầy thì bày hết ra quầy.
/// Model lấy từ Resources/Props (Tools > Store Setup > 10).
/// </summary>
public class ShoppingCarrier : MonoBehaviour
{
    [Tooltip("Khách định mua từ chừng này món trở lên thì đẩy xe")]
    public int cartFromItems = 4;
    public Vector3 basketOffset = new Vector3(0.26f, 0.5f, 0.12f);
    public float cartGap = 0.35f;

    private Transform container;
    private Bounds inner;          // lòng giỏ / xe, toạ độ của container
    private bool isCart;
    private readonly List<GameObject> visuals = new List<GameObject>();

    public bool HasContainer => container != null;

    public void Init(int expectedItems)
    {
        isCart = expectedItems >= cartFromItems;
        GameObject prefab = Resources.Load<GameObject>(isCart ? "Props/Cart" : "Props/Basket");
        if (prefab == null && isCart) { isCart = false; prefab = Resources.Load<GameObject>("Props/Basket"); }
        if (prefab == null) return;

        container = Instantiate(prefab, transform, false).transform;
        container.name = isCart ? "Cart" : "Basket";
        Bounds b = ProductFit.LocalBounds(container);
        if (isCart)
        {
            container.localPosition = new Vector3(0f, 0f, cartGap + b.size.z * 0.5f);
            container.localRotation = Quaternion.identity;
        }
        else
        {
            container.localPosition = basketOffset;
            container.localRotation = Quaternion.Euler(0f, 90f, 0f);
        }

        // Lòng chứa hàng: phần trên của giỏ / xe, thu vào cho khỏi xuyên thành
        float floorY = isCart ? b.min.y + b.size.y * 0.45f : b.min.y + b.size.y * 0.12f;
        inner = new Bounds(new Vector3(b.center.x, floorY, b.center.z), new Vector3(b.size.x * 0.7f, b.size.y * 0.4f, b.size.z * 0.6f));
    }

    /// <summary>Thêm 1 món vào giỏ / xe (chỉ để nhìn).</summary>
    public void AddItem(ItemData item)
    {
        if (container == null || item == null) return;
        GameObject prefab = item.ResolveProductPrefab();
        if (prefab == null) return;

        GameObject v = Instantiate(prefab, container, false);
        ProductFit.MakeVisualOnly(v);
        Bounds lb = ProductFit.LocalBounds(v.transform);

        // Mỗi món chiếm 1 ô trong lưới 3 x 2, chồng tầng khi đầy
        int cols = 3, rows = 2;
        int i = visuals.Count;
        int layer = i / (cols * rows), r = i % (cols * rows);
        float cellX = inner.size.x / cols, cellZ = inner.size.z / rows;
        float maxDim = Mathf.Max(lb.size.x, lb.size.z, 0.001f);
        float s = Mathf.Min(1f, Mathf.Min(cellX, cellZ) * 0.95f / maxDim);
        s = Mathf.Min(s, inner.size.y * 0.9f / Mathf.Max(0.001f, lb.size.y));
        v.transform.localScale = Vector3.one * s;
        v.transform.localRotation = Quaternion.Euler(0f, Random.Range(-20f, 20f), 0f);
        Vector3 cell = new Vector3(
            inner.min.x + (r % cols + 0.5f) * cellX,
            inner.min.y + layer * lb.size.y * s * 0.8f,
            inner.min.z + (r / cols + 0.5f) * cellZ);
        v.transform.localPosition = cell - new Vector3(lb.center.x * s, lb.min.y * s, lb.center.z * s);
        visuals.Add(v);
    }

    /// <summary>Bày hết hàng ra quầy: giỏ / xe trống.</summary>
    public void ClearItems()
    {
        foreach (GameObject v in visuals) if (v != null) Destroy(v);
        visuals.Clear();
    }
}
