using TMPro;
using UnityEngine;

/// <summary>
/// Gắn vào xe CHƯA MUA: bảng "FOR SALE" nổi trên xe (luôn quay về camera) và thông báo khi người chơi bấm F cạnh xe.
/// VehicleManager tự thêm / gỡ component này.
/// </summary>
public class VehicleForSale : MonoBehaviour
{
    private const float Range = 3.2f;

    private VehicleManager.Entry entry;
    private Transform label;
    private TextMeshPro text;
    private float height = 2f;
    private Transform player;
    private float nextFind;

    public void Setup(VehicleManager.Entry e)
    {
        entry = e;
        if (label == null) Build();
        Refresh();
    }

    private void Build()
    {
        // Chiều cao bảng: ngay trên nóc xe
        float top = transform.position.y + 1.6f;
        bool has = false;
        foreach (Renderer r in GetComponentsInChildren<Renderer>())
        {
            if (r is ParticleSystemRenderer || r is TrailRenderer) continue;
            top = has ? Mathf.Max(top, r.bounds.max.y) : r.bounds.max.y;
            has = true;
        }
        height = top - transform.position.y + 0.55f;

        var go = new GameObject("ForSaleSign"); // không làm con của xe để chữ không bị méo theo scale xe
        label = go.transform;
        text = go.AddComponent<TextMeshPro>();
        text.alignment = TextAlignmentOptions.Center;
        text.fontSize = 3f;
        text.fontStyle = FontStyles.Bold;
        text.outlineWidth = 0.22f;
        text.outlineColor = Color.black;
        text.rectTransform.sizeDelta = new Vector2(4f, 1.6f);
        text.textWrappingMode = TextWrappingModes.NoWrap;
    }

    private void Refresh()
    {
        if (text == null || entry == null) return;
        bool levelOk = StoreProgress.Level >= entry.level;
        string req = levelOk ? "" : $"  <color=#FF8A7A>Lv {entry.level}</color>";
        text.text = $"<color=#FFD54A>FOR SALE</color>\n<size=80%>{entry.name}  {EconomyManager.Format(entry.price)}{req}</size>\n<size=60%><color=#CFE3F5>Buy on the computer > Vehicles</color></size>";
    }

    void OnEnable()
    {
        StoreProgress.OnChanged += Refresh;
    }

    void OnDisable()
    {
        StoreProgress.OnChanged -= Refresh;
    }

    void OnDestroy()
    {
        if (label != null) Destroy(label.gameObject);
    }

    void LateUpdate()
    {
        if (label == null) return;
        label.position = transform.position + Vector3.up * height;
        Camera cam = Camera.main;
        if (cam != null)
        {
            label.rotation = Quaternion.LookRotation(label.position - cam.transform.position);
            // Chỉ hiện khi ở gần (đỡ rối mắt khi chạy xe ngoài phố)
            float d = Vector3.Distance(cam.transform.position, label.position);
            text.enabled = d < 30f;
        }

        if (Input.GetKeyDown(KeyCode.F) && entry != null && !UIState.IsBlockingUIOpen)
        {
            Transform p = Player();
            if (p != null && (p.position - transform.position).sqrMagnitude <= Range * Range)
                ProgressHUD.Toast($"{entry.name} is for sale ({EconomyManager.Format(entry.price)}). Buy it on the computer > Vehicles.");
        }
    }

    private Transform Player()
    {
        if (player != null && player.gameObject.activeInHierarchy) return player;
        if (Time.time < nextFind) return null;
        nextFind = Time.time + 1f;
        PlayerController pc = FindFirstObjectByType<PlayerController>();
        player = pc != null ? pc.transform : null;
        return player;
    }
}
