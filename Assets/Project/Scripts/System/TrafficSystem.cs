using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Xe chạy trên phố (xe máy + ô tô) theo mạng đường khu phố mới phía đông và đại lộ / đường dọc của thành phố cũ.
///  - Đi bên phải đường, tới ngã tư rẽ ngẫu nhiên (cuối đường cụt thì quay đầu).
///  - Chậm lại / dừng khi phía trước có xe khác, người chơi hoặc người đi bộ.
///  - Mẫu xe lấy từ xe có sẵn trong scene (Honda Cub, C125, Simple Retro Car), bỏ hết script lái của người chơi.
/// Tự tạo khi vào game (chỉ khi có khu phố mới "CityExpansion_East").
/// </summary>
public class TrafficSystem : MonoBehaviour
{
    public static TrafficSystem Instance { get; private set; }

    public int carCount = 14;
    public float cruiseSpeed = 9f;
    public float turnSpeed = 5f;
    public float laneOffset = 3f;

    // ================= Mạng đường =================
    private class Node { public Vector3 pos; public float radius; public readonly List<int> links = new List<int>(); }
    private readonly List<Node> nodes = new List<Node>();

    private class Car
    {
        public Transform t;
        public float yOffset;
        public int from, to;
        public readonly List<Vector3> path = new List<Vector3>();
        public int seg;
        public float speed, blockedFor, ignoreUntil;
    }
    private readonly List<Car> cars = new List<Car>();
    private readonly List<GameObject> templates = new List<GameObject>();
    private static readonly HashSet<Transform> carRoots = new HashSet<Transform>();

    public static bool IsTrafficCar(Transform t)
    {
        foreach (Transform r in carRoots) if (r != null && t.IsChildOf(r)) return true;
        return false;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void AutoCreate()
    {
        CreateIfMissing();
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene s, UnityEngine.SceneManagement.LoadSceneMode m) => CreateIfMissing();

    private static void CreateIfMissing()
    {
        if (Instance == null && GameObject.Find("CityExpansion_East") != null && FindFirstObjectByType<GameManager>() != null)
            new GameObject("TrafficSystem").AddComponent<TrafficSystem>();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => carRoots.Clear();

    void Awake()
    {
        Instance = this;
        BuildNetwork();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        carRoots.Clear();
    }

    void Start()
    {
        FindTemplates();
        if (templates.Count == 0) { Debug.LogWarning("[Traffic] Không có mẫu xe trong scene."); return; }
        for (int i = 0; i < carCount; i++) SpawnCar(i);
    }

    /// <summary>Lưới ngã tư khu mới (3 đường dọc x 5 đường ngang) + đường dọc cũ x=22 + đại lộ cũ chạy về phía tây.</summary>
    private void BuildNetwork()
    {
        float[] xs = { 92f, 160f, 228f };
        float[] zs = { -90f, -42f, 8f, 54f, 98f };
        int[,] grid = new int[xs.Length, zs.Length];
        for (int i = 0; i < xs.Length; i++)
            for (int k = 0; k < zs.Length; k++) grid[i, k] = AddNode(new Vector3(xs[i], 0.1f, zs[k]), 8f);
        for (int i = 0; i < xs.Length; i++)
            for (int k = 0; k < zs.Length; k++)
            {
                if (i + 1 < xs.Length) Link(grid[i, k], grid[i + 1, k]);
                if (k + 1 < zs.Length) Link(grid[i, k], grid[i, k + 1]);
            }
        // Đường dọc cũ (x 14..30) nối 3 đường ngang: Tran Hung Dao, đại lộ, Ly Thuong Kiet
        int a = AddNode(new Vector3(22f, 0.1f, -90f), 8f), b = AddNode(new Vector3(22f, 0.1f, 8f), 8f), c = AddNode(new Vector3(22f, 0.1f, 98f), 8f);
        Link(a, grid[0, 0]); Link(b, grid[0, 2]); Link(c, grid[0, 4]);
        Link(a, b); Link(b, c);
        // Đại lộ cũ chạy qua trước siêu thị về phía tây (cuối đường quay đầu)
        int d = AddNode(new Vector3(-80f, 0.1f, 8f), 1f), e = AddNode(new Vector3(-175f, 0.1f, 8f), 1f);
        Link(b, d); Link(d, e);
    }

    private int AddNode(Vector3 p, float radius)
    {
        nodes.Add(new Node { pos = p, radius = radius });
        return nodes.Count - 1;
    }

    private void Link(int a, int b)
    {
        nodes[a].links.Add(b);
        nodes[b].links.Add(a);
    }

    // ================= Xe =================
    private void FindTemplates()
    {
        foreach (string n in new[] { "Honda Cub", "C125 - PB", "Simple Retro Car" })
        {
            GameObject g = GameObject.Find(n);
            if (g != null) templates.Add(g);
        }
    }

    private void SpawnCar(int index)
    {
        // Xe máy nhiều hơn ô tô (đường phố Việt Nam)
        GameObject template = templates[Random.value < 0.65f ? Random.Range(0, Mathf.Max(1, templates.Count - 1)) : templates.Count - 1];
        float yOffset = template.transform.position.y - GroundY(template.transform.position, 0.1f);

        var holder = new GameObject("_TrafficHolder");
        holder.SetActive(false);
        GameObject go = Instantiate(template, holder.transform);
        StripToShell(go);
        go.transform.SetParent(transform, true);
        Destroy(holder);
        go.name = $"Traffic_{index}_{template.name}";

        var car = new Car { t = go.transform, yOffset = Mathf.Clamp(yOffset, 0f, 1f) };
        // Chọn một đoạn đường ngẫu nhiên để xuất phát
        int from = Random.Range(0, nodes.Count);
        int to = nodes[from].links[Random.Range(0, nodes[from].links.Count)];
        car.from = from; car.to = to;
        BuildEdgePath(car, from, to, -1);
        car.seg = Random.Range(0, Mathf.Max(1, car.path.Count - 2));
        car.t.position = car.path[car.seg] + Vector3.up * car.yOffset;
        car.speed = cruiseSpeed * 0.5f;
        go.SetActive(true);
        carRoots.Add(go.transform);
        cars.Add(car);
    }

    /// <summary>Bỏ hết script / camera / âm thanh / bánh xe vật lý của xe người chơi, chỉ giữ hình + va chạm.</summary>
    private static void StripToShell(GameObject go)
    {
        foreach (Camera c in go.GetComponentsInChildren<Camera>(true)) DestroyImmediate(c.gameObject);
        // Xoá từ cuối lên (script thêm sau thường phụ thuộc script thêm trước qua RequireComponent)
        for (int pass = 0; pass < 3; pass++)
        {
            MonoBehaviour[] all = go.GetComponentsInChildren<MonoBehaviour>(true);
            if (all.Length == 0) break;
            for (int i = all.Length - 1; i >= 0; i--) if (all[i] != null) DestroyImmediate(all[i]);
        }
        foreach (AudioListener l in go.GetComponentsInChildren<AudioListener>(true)) DestroyImmediate(l);
        foreach (AudioSource s in go.GetComponentsInChildren<AudioSource>(true)) DestroyImmediate(s);
        foreach (WheelCollider w in go.GetComponentsInChildren<WheelCollider>(true)) DestroyImmediate(w);
        foreach (Rigidbody rb in go.GetComponentsInChildren<Rigidbody>(true)) { rb.isKinematic = true; rb.useGravity = false; rb.interpolation = RigidbodyInterpolation.None; }
        foreach (Light l in go.GetComponentsInChildren<Light>(true)) l.enabled = false;
    }

    /// <summary>Đường đi trên làn bên phải từ nút from tới nút to, kèm đoạn rẽ (Bezier) từ làn trước đó.</summary>
    private void BuildEdgePath(Car car, int from, int to, int prev)
    {
        car.path.Clear();
        car.seg = 0;
        Node a = nodes[from], b = nodes[to];
        Vector3 dir = (b.pos - a.pos); dir.y = 0f; dir.Normalize();
        Vector3 right = new Vector3(dir.z, 0f, -dir.x);
        Vector3 start = a.pos + dir * a.radius + right * laneOffset;
        Vector3 end = b.pos - dir * b.radius + right * laneOffset;

        if (prev >= 0)
        {
            // Đoạn rẽ trong ngã tư: từ cuối làn đến (prev -> from) tới đầu làn (from -> to)
            Node p = nodes[prev];
            Vector3 inDir = (a.pos - p.pos); inDir.y = 0f; inDir.Normalize();
            Vector3 inRight = new Vector3(inDir.z, 0f, -inDir.x);
            Vector3 inEnd = a.pos - inDir * a.radius + inRight * laneOffset;
            Vector3 ctrl;
            if (Vector3.Dot(inDir, dir) < -0.9f) ctrl = a.pos + inDir * (a.radius + 6f);             // quay đầu
            else if (!LineIntersect(inEnd, inDir, start, dir, out ctrl)) ctrl = (inEnd + start) * 0.5f; // đi thẳng
            for (int i = 1; i <= 8; i++)
            {
                float t = i / 8f;
                car.path.Add((1 - t) * (1 - t) * inEnd + 2 * (1 - t) * t * ctrl + t * t * start);
            }
        }
        else car.path.Add(start);

        float len = Vector3.Distance(start, end);
        int n = Mathf.Max(1, Mathf.CeilToInt(len / 12f));
        for (int i = 1; i <= n; i++) car.path.Add(Vector3.Lerp(start, end, i / (float)n));
        for (int i = 0; i < car.path.Count; i++) car.path[i] = new Vector3(car.path[i].x, GroundY(car.path[i], 0.1f), car.path[i].z);
    }

    private static bool LineIntersect(Vector3 p, Vector3 d, Vector3 q, Vector3 e, out Vector3 hit)
    {
        float den = d.x * e.z - d.z * e.x;
        hit = default;
        if (Mathf.Abs(den) < 1e-3f) return false;
        Vector3 w = q - p;
        float t = (w.x * e.z - w.z * e.x) / den;
        hit = p + d * t;
        return true;
    }

    private static float GroundY(Vector3 p, float fallback)
    {
        // Mặt đường: bỏ qua xe, người
        foreach (RaycastHit h in Physics.RaycastAll(new Vector3(p.x, p.y + 6f, p.z), Vector3.down, 12f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (h.normal.y < 0.8f || IsTrafficCar(h.collider.transform) || h.collider.GetComponentInParent<CharacterController>() != null) continue;
            if (h.collider.attachedRigidbody != null && !h.collider.attachedRigidbody.isKinematic) continue;
            return h.point.y;
        }
        return fallback;
    }

    void Update()
    {
        float dt = Time.deltaTime;
        foreach (Car car in cars)
        {
            if (car.t == null) continue;
            if (car.seg >= car.path.Count - 1) NextEdge(car);

            Vector3 target = car.path[car.seg + 1] + Vector3.up * car.yOffset;
            Vector3 pos = car.t.position;
            Vector3 to = target - pos;
            float dist = to.magnitude;

            // Tốc độ mong muốn: đoạn rẽ chậm, có vật cản phía trước thì giảm / dừng
            bool turning = car.seg < 8 && car.path.Count > 9 && car.seg > 0;
            float want = turning ? turnSpeed : cruiseSpeed;
            float gap = ObstacleAhead(car);
            if (Time.time >= car.ignoreUntil && gap < 12f) want = Mathf.Min(want, Mathf.Max(0f, (gap - 3.5f) * 1.6f));
            car.blockedFor = want < 0.3f ? car.blockedFor + dt : 0f;
            if (car.blockedFor > 8f) { car.ignoreUntil = Time.time + 2.5f; car.blockedFor = 0f; } // kẹt lâu thì nhích qua

            car.speed = Mathf.MoveTowards(car.speed, want, (want < car.speed ? 14f : 4f) * dt);
            float step = car.speed * dt;
            if (dist <= step + 0.05f) { car.t.position = target; car.seg++; }
            else car.t.position = pos + to / dist * step;

            Vector3 flat = new Vector3(to.x, 0f, to.z);
            if (flat.sqrMagnitude > 0.01f)
                car.t.rotation = Quaternion.Slerp(car.t.rotation, Quaternion.LookRotation(flat), 1f - Mathf.Exp(-8f * dt));
        }
    }

    private void NextEdge(Car car)
    {
        Node n = nodes[car.to];
        int next;
        if (n.links.Count == 1) next = n.links[0];                       // đường cụt: quay đầu
        else
        {
            do next = n.links[Random.Range(0, n.links.Count)]; while (next == car.from);
        }
        int prev = car.from;
        car.from = car.to;
        car.to = next;
        BuildEdgePath(car, car.from, car.to, prev);
    }

    /// <summary>Khoảng cách tới xe / người chơi / người đi bộ phía trước (mét), không có thì trả về số lớn.</summary>
    private float ObstacleAhead(Car car)
    {
        Vector3 fwd = car.t.forward; fwd.y = 0f; fwd.Normalize();
        Vector3 origin = car.t.position + Vector3.up * 0.8f;
        float best = 999f;
        foreach (RaycastHit h in Physics.SphereCastAll(origin, 0.9f, fwd, 14f, ~0, QueryTriggerInteraction.Ignore))
        {
            Transform t = h.collider.transform;
            if (t.IsChildOf(car.t)) continue;
            bool stopFor = IsTrafficCar(t) || h.collider.GetComponentInParent<CharacterController>() != null
                           || h.collider.GetComponentInParent<PedestrianWalker>() != null
                           || h.collider.GetComponentInParent<PlayerCarController>() != null;
            if (stopFor && h.distance < best) best = h.distance;
        }
        return best;
    }
}
