using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

/// <summary>
/// Vết bẩn trong siêu thị: khi cửa hàng mở, khách thỉnh thoảng làm bẩn sàn (gần chỗ họ đứng / gần kệ).
/// Càng nhiều vết bẩn khách càng ít mua và hay than phiền. Dọn bằng chổi (phím 2, giữ chuột trái).
/// </summary>
public class DirtManager : MonoBehaviour
{
    public static DirtManager Instance { get; private set; }
    private static readonly List<DirtSpot> spots = new List<DirtSpot>();

    public int maxSpots = 14;
    public Vector2 spawnInterval = new Vector2(18f, 35f);
    public Color dirtColor = new Color(0.28f, 0.2f, 0.12f, 0.85f);

    private float nextSpawn;
    private Material material;

    public static int Count { get { spots.RemoveAll(s => s == null); return spots.Count; } }
    /// <summary>Các vết bẩn hiện có (nhân viên lau nhà đọc danh sách này).</summary>
    public static IReadOnlyList<DirtSpot> Spots { get { spots.RemoveAll(s => s == null); return spots; } }

    /// <summary>Hệ số khách chịu mua: mỗi vết bẩn trừ 3%, thấp nhất 60%.</summary>
    public static float SatisfactionFactor => Mathf.Clamp(1f - Count * 0.03f, 0.6f, 1f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Init()
    {
        Create();
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(Scene s, LoadSceneMode m) => Create();
    private static void Create() { if (Instance == null) new GameObject("DirtManager").AddComponent<DirtManager>(); }

    void Awake()
    {
        Instance = this;
        spots.Clear();
        nextSpawn = Time.time + Random.Range(spawnInterval.x, spawnInterval.y);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null || !gm.IsStoreOpen || Time.time < nextSpawn) return;
        nextSpawn = Time.time + Random.Range(spawnInterval.x, spawnInterval.y);
        if (Count >= maxSpots) return;
        if (TryPickPoint(out Vector3 p)) Spawn(p);
    }

    /// <summary>Vết bẩn gần điểm này nhất (trong bán kính).</summary>
    public static DirtSpot Nearest(Vector3 point, float maxDistance)
    {
        DirtSpot best = null;
        float bestD = maxDistance * maxDistance;
        foreach (DirtSpot s in spots)
        {
            if (s == null) continue;
            Vector3 d = s.transform.position - point; d.y *= 0.3f;
            if (d.sqrMagnitude < bestD) { bestD = d.sqrMagnitude; best = s; }
        }
        return best;
    }

    public static bool AnyNear(Vector3 point, float radius) => Nearest(point, radius) != null;

    // ================= Sinh vết bẩn =================
    private bool TryPickPoint(out Vector3 point)
    {
        point = default;
        Vector3 basePos;
        if (NPCShopper.Active.Count > 0 && Random.value < 0.6f)
            basePos = NPCShopper.Active[Random.Range(0, NPCShopper.Active.Count)].transform.position;
        else
        {
            var shelves = StockManager.Shelves;
            if (shelves.Count == 0) return false;
            Shelf sh = shelves[Random.Range(0, shelves.Count)];
            if (sh == null) return false;
            basePos = sh.transform.position;
        }
        Vector2 r = Random.insideUnitCircle * 1.5f;
        Vector3 wanted = basePos + new Vector3(r.x, 0f, r.y);
        if (!NavMesh.SamplePosition(wanted, out NavMeshHit hit, 2f, NavMesh.AllAreas)) return false;
        point = hit.position;
        if (Physics.Raycast(point + Vector3.up * 0.5f, Vector3.down, out RaycastHit floor, 1.5f, ~0, QueryTriggerInteraction.Ignore))
            point.y = floor.point.y;
        return !AnyNear(point, 0.8f);
    }

    public DirtSpot Spawn(Vector3 position)
    {
        if (material == null)
        {
            material = RuntimeMaterials.Ghost(dirtColor);
            Texture2D blob = BlobTexture();
            if (material.HasProperty("_BaseMap")) material.SetTexture("_BaseMap", blob);
            if (material.HasProperty("_MainTex")) material.SetTexture("_MainTex", blob);
        }
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = "DirtSpot";
        Destroy(go.GetComponent<Collider>());
        go.transform.SetPositionAndRotation(position + Vector3.up * 0.012f, Quaternion.Euler(90f, Random.Range(0f, 360f), 0f));
        float s = Random.Range(0.55f, 1.15f);
        go.transform.localScale = new Vector3(s, s * Random.Range(0.7f, 1f), 1f);
        Renderer r = go.GetComponent<Renderer>();
        r.sharedMaterial = material;
        r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        DirtSpot spot = go.AddComponent<DirtSpot>();
        spot.Init(dirtColor);
        spots.Add(spot);
        return spot;
    }

    /// <summary>Vệt loang tròn mép nhoè (tạo bằng code, không cần file ảnh).</summary>
    private static Texture2D BlobTexture()
    {
        const int n = 64;
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
        float seed = Random.Range(0f, 100f);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float dx = (x - n / 2f) / (n / 2f), dy = (y - n / 2f) / (n / 2f);
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float noise = Mathf.PerlinNoise(seed + x * 0.12f, seed + y * 0.12f);
                float a = Mathf.Clamp01((1f - d) * 1.6f + (noise - 0.5f) * 0.9f);
                a = Mathf.SmoothStep(0f, 1f, a);
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, a));
            }
        tex.Apply();
        return tex;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => spots.Clear();
}
