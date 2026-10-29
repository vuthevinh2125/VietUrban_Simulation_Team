using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Biển OPEN / CLOSED ở cửa (giống Supermarket Simulator): nhìn vào và click để mở / đóng cửa hàng.
/// Không có model riêng thì tự dựng một tấm biển đơn giản (2 mặt chữ).
/// Tạo nhanh: Tools > Store Setup > 8. Create Open-Closed Sign.
/// </summary>
public class StoreSign : MonoBehaviour
{
    public static readonly List<StoreSign> All = new List<StoreSign>();

    [Tooltip("Tự dựng tấm biển nếu object này chưa có hình")]
    public bool buildDefaultVisual = true;
    public Vector2 boardSize = new Vector2(0.7f, 0.32f);
    public Color openColor = new Color(0.2f, 0.85f, 0.3f);
    public Color closedColor = new Color(0.95f, 0.25f, 0.2f);
    public Color closingColor = new Color(1f, 0.65f, 0.2f);

    private readonly List<TextMeshPro> texts = new List<TextMeshPro>();

    public string Prompt
    {
        get
        {
            GameManager gm = GameManager.Instance;
            if (gm == null) return "";
            if (gm.IsClosingTime) return "Closing time - serve the last customers";
            return gm.IsStoreOpen ? "[LMB] Close store" : "[LMB] Open store";
        }
    }

    void OnEnable()
    {
        All.Add(this);
        GameManager.OnStoreOpenChanged += OnStoreChanged;
        GameManager.OnNewDay += OnNewDay;
    }

    void OnDisable()
    {
        All.Remove(this);
        GameManager.OnStoreOpenChanged -= OnStoreChanged;
        GameManager.OnNewDay -= OnNewDay;
    }

    void Start()
    {
        texts.Clear();
        texts.AddRange(GetComponentsInChildren<TextMeshPro>(true));
        if (buildDefaultVisual && GetComponentInChildren<Renderer>() == null) BuildVisual(false);
        TintDefaultParts();
        int layer = LayerMask.NameToLayer("Interactable");
        if (layer >= 0) foreach (Transform t in GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        Refresh();
    }

    public void Interact()
    {
        GameManager gm = GameManager.Instance;
        if (gm != null && gm.CanToggleStore) gm.ToggleStore();
    }

    private void OnStoreChanged(bool open) => Refresh();
    private void OnNewDay(int day) => Refresh();

    private void Refresh()
    {
        GameManager gm = GameManager.Instance;
        bool open = gm != null && gm.IsStoreOpen;
        bool closing = gm != null && gm.IsClosingTime;
        foreach (TextMeshPro t in texts)
        {
            if (t == null) continue;
            t.text = open ? "OPEN" : "CLOSED";
            t.color = open ? openColor : (closing ? closingColor : closedColor);
        }
    }

    void Update()
    {
        // Màu cam lúc hết giờ (GameManager không bắn sự kiện riêng cho lúc này)
        if (texts.Count > 0 && GameManager.Instance != null && GameManager.Instance.IsClosingTime && texts[0].color != closingColor) Refresh();
    }

    private void TintDefaultParts()
    {
        Transform pole = transform.Find("Pole"), board = transform.Find("Board");
        if (pole != null && pole.TryGetComponent(out Renderer pr)) pr.material.color = new Color(0.25f, 0.25f, 0.28f);
        if (board != null && board.TryGetComponent(out Renderer br)) br.material.color = new Color(0.08f, 0.08f, 0.1f);
    }

    /// <summary>Dựng tấm biển mặc định (gọi được cả trong Editor để dễ đặt vị trí).</summary>
    public void BuildVisual(bool editMode)
    {
        texts.Clear();
        // Cột + tấm biển
        GameObject pole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pole.name = "Pole";
        pole.transform.SetParent(transform, false);
        pole.transform.localScale = new Vector3(0.05f, 0.6f, 0.05f);
        pole.transform.localPosition = new Vector3(0f, 0.6f, 0f);
        if (editMode) DestroyImmediate(pole.GetComponent<Collider>());
        else Destroy(pole.GetComponent<Collider>());

        GameObject board = GameObject.CreatePrimitive(PrimitiveType.Cube);
        board.name = "Board";
        board.transform.SetParent(transform, false);
        board.transform.localScale = new Vector3(boardSize.x, boardSize.y, 0.04f);
        board.transform.localPosition = new Vector3(0f, 1.2f + boardSize.y * 0.5f, 0f);

        for (int side = 0; side < 2; side++)
        {
            GameObject tgo = new GameObject(side == 0 ? "Text_Front" : "Text_Back");
            tgo.transform.SetParent(transform, false);
            tgo.transform.localPosition = board.transform.localPosition + new Vector3(0f, 0f, side == 0 ? -0.022f : 0.022f);
            tgo.transform.localRotation = side == 0 ? Quaternion.identity : Quaternion.Euler(0f, 180f, 0f);
            TextMeshPro tmp = tgo.AddComponent<TextMeshPro>();
            tmp.rectTransform.sizeDelta = boardSize * 0.9f;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.enableAutoSizing = true;
            tmp.fontSizeMin = 0.1f;
            tmp.fontSizeMax = 3f;
            tmp.fontStyle = FontStyles.Bold;
            tmp.text = "CLOSED";
            tmp.color = closedColor;
            texts.Add(tmp);
        }
    }
}
