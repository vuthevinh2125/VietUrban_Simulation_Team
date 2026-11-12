using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Dòng xe máy chạy trên phố (đặc trưng giao thông Việt Nam) theo mạng đường của VietDistrict:
///  - Đi bên phải, sát lề, mỗi xe một làn lệch ngẫu nhiên; tới ngã tư rẽ ngẫu nhiên, nghiêng xe khi vào cua.
///  - Người lái đội mũ bảo hiểm nhiều màu, có xe chở thêm người hoặc thùng hàng; trời mưa thì mặc áo mưa trùm.
///  - Chậm lại / dừng khi phía trước có xe máy khác, ô tô, người chơi hoặc người đi bộ; kẹt lâu thì "lách" qua.
///  - Thỉnh thoảng bấm còi khi ở gần người chơi.
/// Tự tạo khi vào scene siêu thị có VietDistrict. Mỗi xe một mesh gộp (bảng màu chung) + collider để xe khác thấy.
/// </summary>
public class MotorbikeTraffic : MonoBehaviour
{
    public static MotorbikeTraffic Instance { get; private set; }

    public int bikeCount = 110;
    public Vector2 speedRange = new Vector2(5.5f, 10f);

    private class Bike
    {
        public Transform t;
        public MeshFilter mf;
        public int variant;
        public int from, to;
        public readonly List<Vector3> path = new List<Vector3>();
        public int seg;
        public float speed, cruise, lane, target, blocked, nextCheck, yaw, lean;
    }

    private readonly List<Bike> bikes = new List<Bike>();
    private VietDistrict district;
    private List<int>[] links;
    private Mesh[] dry, rain;
    private bool raining;
    private float nextHorn, nextCacheClear;
    private const int Variants = BikeModels.Variants;
    private static readonly RaycastHit[] hits = new RaycastHit[16];
    private readonly Dictionary<Collider, bool> stopCache = new Dictionary<Collider, bool>();
    private bool real;
    private RealModels.Model[] realRider, realPassenger;
    private Mesh[] helmetLod0, helmetLod1;

    /// <summary>
    /// Mỗi biến thể = 1 mẫu xe máy dựng bằng code (BikeModels: xe số / tay ga, nhiều màu) + người lái là model NPC thật
    /// (tay IK đúng tay lái, chân đúng chỗ để chân) + mũ bảo hiểm nửa đầu; 30% có người ngồi sau.
    /// Không dùng xe của người chơi (C125, Honda Cub).
    /// </summary>
    private void BuildRealVariants()
    {
        realRider = new RealModels.Model[Variants];
        realPassenger = new RealModels.Model[Variants];
        helmetLod0 = new Mesh[Variants];
        helmetLod1 = new Mesh[Variants];
        Color[] helmets =
        {
            new Color(0.85f, 0.1f, 0.1f), new Color(0.95f, 0.95f, 0.95f), new Color(0.1f, 0.3f, 0.7f), new Color(0.95f, 0.8f, 0.1f),
            new Color(0.08f, 0.08f, 0.08f), new Color(0.9f, 0.5f, 0.7f), new Color(0.3f, 0.55f, 0.3f), new Color(0.55f, 0.4f, 0.3f),
        };
        var rnd = new System.Random(2026);
        GameObject[] people = district.peopleModels, mats = district.peopleMaterials;
        for (int v = 0; v < Variants; v++)
        {
            BikeModels.Info bike = BikeModels.Get(v);
            int a = rnd.Next(people.Length);
            realRider[v] = RealModels.Person(people[a], mats != null && a < mats.Length ? mats[a] : null, RealModels.Pose.Ride, RiderIK(bike), bike.scooter ? "scooter" : "wave");
            if (rnd.NextDouble() < 0.3)
            {
                int b = rnd.Next(people.Length);
                realPassenger[v] = RealModels.Person(people[b], mats != null && b < mats.Length ? mats[b] : null, RealModels.Pose.RideBack, PassengerIK(bike), bike.scooter ? "pscooter" : "pwave");
            }
            Color hc = helmets[rnd.Next(helmets.Length)];
            helmetLod0[v] = BikeModels.Helmet(hc, false);
            helmetLod1[v] = BikeModels.Helmet(hc, true);
        }
    }

    public static RealModels.IK RiderIK(BikeModels.Info b) => new RealModels.IK
    {
        on = true,
        handL = b.gripL - b.seat, handR = b.gripR - b.seat, footL = b.footL - b.seat, footR = b.footR - b.seat,
    };

    public static RealModels.IK PassengerIK(BikeModels.Info b) => new RealModels.IK
    {
        on = true,
        handL = new Vector3(-0.17f, 0.02f, 0.3f), handR = new Vector3(0.17f, 0.02f, 0.3f),
        footL = b.pegL - b.rearSeat, footR = b.pegR - b.rearSeat,
    };

    private GameObject SpawnReal(Bike b, int index)
    {
        BikeModels.Info info = BikeModels.Get(b.variant);
        var bikeModel = new RealModels.Model { ok = true };
        bikeModel.lod0.Add(new RealModels.Part { mesh = info.lod0, mats = new[] { VPalette.Material }, local = Matrix4x4.identity });
        bikeModel.lod1.Add(new RealModels.Part { mesh = info.lod1, mats = new[] { VPalette.Material }, local = Matrix4x4.identity });
        GameObject go = RealModels.Spawn(bikeModel, transform, Vector3.zero, Quaternion.identity, "Xe may " + index, true, 0.22f, 0.02f);
        var helmet = new RealModels.Model { ok = true };
        helmet.lod0.Add(new RealModels.Part { mesh = helmetLod0[b.variant], mats = new[] { VPalette.Material }, local = Matrix4x4.identity });
        helmet.lod1.Add(new RealModels.Part { mesh = helmetLod1[b.variant], mats = new[] { VPalette.Material }, local = Matrix4x4.identity });
        void Seat(RealModels.Model m, Vector3 hipsAt)
        {
            if (m == null || !m.ok) return;
            Vector3 off = hipsAt - m.hips;
            RealModels.AddTo(go, m, Matrix4x4.TRS(off, Quaternion.identity, Vector3.one), true);
            RealModels.AddTo(go, helmet, Matrix4x4.TRS(off + m.head + new Vector3(0f, 0.07f, 0.01f), Quaternion.identity, Vector3.one), true);
        }
        Seat(realRider[b.variant], info.seat);
        Seat(realPassenger[b.variant], info.rearSeat);
        return go;
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
        if (Instance == null && SupermarketScene.IsActive && FindAnyObjectByType<VietDistrict>() != null)
            new GameObject("MotorbikeTraffic").AddComponent<MotorbikeTraffic>();
    }

    void Awake() => Instance = this;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (dry != null) foreach (Mesh m in dry) if (m != null) Destroy(m);
        if (rain != null) foreach (Mesh m in rain) if (m != null) Destroy(m);
    }

    void Start()
    {
        district = FindAnyObjectByType<VietDistrict>();
        if (district == null) { enabled = false; return; }
        district.EnsureLayout();
        int n = district.Nodes.Count;
        if (n < 2) { enabled = false; return; }
        links = new List<int>[n];
        for (int i = 0; i < n; i++) links[i] = new List<int>();
        foreach (Vector2Int e in district.Edges)
        {
            links[e.x].Add(e.y);
            links[e.y].Add(e.x);
        }
        real = district.peopleModels != null && district.peopleModels.Length > 0;
        if (real)
        {
            BuildRealVariants();
            bikeCount = Mathf.Min(bikeCount, 90);
        }
        else BuildMeshes();
        raining = WeatherSystem.IsRaining;
        for (int i = 0; i < bikeCount; i++) Spawn(i);
    }

    // ================= Sinh xe =================
    private void Spawn(int index)
    {
        Vector2Int e = district.Edges[Random.Range(0, district.Edges.Count)];
        bool flip = Random.value < 0.5f;
        var b = new Bike
        {
            from = flip ? e.y : e.x,
            to = flip ? e.x : e.y,
            variant = Random.Range(0, Variants),
            cruise = Random.Range(speedRange.x, speedRange.y),
        };
        GameObject go;
        if (real) go = SpawnReal(b, index);
        else
        {
            go = new GameObject("Xe may " + index);
            go.transform.SetParent(transform, false);
            b.mf = go.AddComponent<MeshFilter>();
            b.mf.sharedMesh = raining ? rain[b.variant] : dry[b.variant];
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = VPalette.Material;
        }
        b.t = go.transform;
        var col = go.AddComponent<BoxCollider>();
        col.center = new Vector3(0f, 0.75f, 0f);
        col.size = new Vector3(0.5f, 1.4f, 1.7f);
        var rb = go.AddComponent<Rigidbody>();
        rb.isKinematic = true;
        rb.useGravity = false;
        go.AddComponent<TrafficBike>();
        b.lane = LaneFor(b.from, b.to);
        BuildPath(b, b.from, b.to, -1, b.lane);
        b.seg = Random.Range(1, Mathf.Max(2, b.path.Count - 1));
        Vector3 p = b.path[b.seg - 1];
        Vector3 d = b.path[b.seg] - p;
        b.yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        b.t.SetPositionAndRotation(p, Quaternion.Euler(0f, b.yaw, 0f));
        b.speed = b.cruise;
        b.target = b.cruise;
        b.nextCheck = Time.time + Random.Range(0f, 0.2f);
        bikes.Add(b);
    }

    private float HalfWidth(int a, int c)
    {
        for (int i = 0; i < district.Edges.Count; i++)
        {
            Vector2Int e = district.Edges[i];
            if ((e.x == a && e.y == c) || (e.x == c && e.y == a)) return district.EdgeHalf[i];
        }
        return 5f;
    }

    /// <summary>Làn lệch phải: sát lề, tránh làn ô tô (ô tô chạy cách tim đường 3 m).</summary>
    private float LaneFor(int a, int c)
    {
        float half = HalfWidth(a, c);
        float lo = Mathf.Max(1.2f, Mathf.Min(4.2f, half - 0.8f)), hi = Mathf.Max(lo, half - 0.6f);
        return Random.Range(lo, hi);
    }

    private void BuildPath(Bike b, int from, int to, int prev, float lane)
    {
        b.path.Clear();
        b.seg = 0;
        List<Vector3> nodes = district.Nodes;
        Vector3 A = nodes[from], B = nodes[to];
        float rA = district.NodeRadius[from], rB = district.NodeRadius[to];
        Vector3 dir = B - A; dir.y = 0f; dir.Normalize();
        Vector3 right = new Vector3(dir.z, 0f, -dir.x);
        Vector3 start = A + dir * rA + right * lane;
        Vector3 end = B - dir * rB + right * lane;
        if (prev >= 0)
        {
            Vector3 inDir = A - nodes[prev]; inDir.y = 0f; inDir.Normalize();
            Vector3 inRight = new Vector3(inDir.z, 0f, -inDir.x);
            Vector3 inEnd = A - inDir * rA + inRight * b.lane;
            Vector3 ctrl;
            if (Vector3.Dot(inDir, dir) < -0.9f) ctrl = A + inDir * (rA * 0.4f);
            else if (!Intersect(inEnd, inDir, start, dir, out ctrl)) ctrl = (inEnd + start) * 0.5f;
            for (int i = 1; i <= 8; i++)
            {
                float t = i / 8f;
                b.path.Add((1 - t) * (1 - t) * inEnd + 2 * (1 - t) * t * ctrl + t * t * start);
            }
        }
        else b.path.Add(start);
        float len = Vector3.Distance(start, end);
        int n = Mathf.Max(1, Mathf.CeilToInt(len / 10f));
        for (int i = 1; i <= n; i++) b.path.Add(Vector3.Lerp(start, end, i / (float)n));
        for (int i = 0; i < b.path.Count; i++) b.path[i] = new Vector3(b.path[i].x, VietDistrict.RoadTop, b.path[i].z);
        b.lane = lane;
    }

    private static bool Intersect(Vector3 p, Vector3 d, Vector3 q, Vector3 e, out Vector3 hit)
    {
        float den = d.x * e.z - d.z * e.x;
        hit = default;
        if (Mathf.Abs(den) < 1e-3f) return false;
        Vector3 w = q - p;
        float t = (w.x * e.z - w.z * e.x) / den;
        hit = p + d * t;
        return true;
    }

    private void NextEdge(Bike b)
    {
        List<int> options = links[b.to];
        int next = b.from;
        if (options.Count > 1)
        {
            for (int tries = 0; tries < 6; tries++)
            {
                int c = options[Random.Range(0, options.Count)];
                if (c != b.from) { next = c; break; }
            }
        }
        else if (options.Count == 1) next = options[0];
        int prev = b.from;
        b.from = b.to;
        b.to = next;
        BuildPath(b, b.from, b.to, prev, LaneFor(b.from, b.to));
    }

    // ================= Chạy =================
    void Update()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f || district == null) return;
        bool r = WeatherSystem.IsRaining;
        if (r != raining)
        {
            raining = r;
            if (!real) foreach (Bike b in bikes) b.mf.sharedMesh = raining ? rain[b.variant] : dry[b.variant];
        }
        if (Time.time >= nextCacheClear) { stopCache.Clear(); nextCacheClear = Time.time + 30f; }

        foreach (Bike b in bikes)
        {
            if (Time.time >= b.nextCheck)
            {
                b.nextCheck = Time.time + 0.15f;
                b.target = Obstacle(b);
            }
            b.speed = Mathf.MoveTowards(b.speed, b.target, (b.target < b.speed ? 16f : 4f) * dt);
            Vector3 pos = b.t.position;
            float dist = b.speed * dt;
            int guard = 0;
            while (dist > 0f && guard++ < 8)
            {
                if (b.seg >= b.path.Count) { NextEdge(b); continue; }
                Vector3 target = b.path[b.seg];
                Vector3 to = target - pos;
                float d = to.magnitude;
                if (d <= dist) { pos = target; dist -= d; b.seg++; }
                else { pos += to / d * dist; dist = 0f; }
            }
            Vector3 look = (b.seg < b.path.Count ? b.path[b.seg] : pos) - pos;
            look.y = 0f;
            if (look.sqrMagnitude > 0.0004f)
            {
                float want = Mathf.Atan2(look.x, look.z) * Mathf.Rad2Deg;
                float newYaw = Mathf.MoveTowardsAngle(b.yaw, want, 140f * dt);
                float rate = Mathf.DeltaAngle(b.yaw, newYaw) / dt;
                b.yaw = newYaw;
                b.lean = Mathf.Lerp(b.lean, Mathf.Clamp(-rate * 0.12f * (b.speed / 8f), -20f, 20f), 6f * dt);
            }
            b.t.SetPositionAndRotation(pos, Quaternion.Euler(0f, b.yaw, b.lean));
        }

        if (Time.time >= nextHorn)
        {
            nextHorn = Time.time + Random.Range(2.5f, 7f);
            Camera cam = Camera.main;
            if (cam != null && bikes.Count > 0)
            {
                Bike b = bikes[Random.Range(0, bikes.Count)];
                if ((b.t.position - cam.transform.position).sqrMagnitude < 35f * 35f)
                    AudioManager.PlayAt(Random.value < 0.6f ? "horn_1" : "horn_2", b.t.position, 0.22f);
            }
        }
    }

    /// <summary>Tốc độ mong muốn theo vật cản phía trước.</summary>
    private float Obstacle(Bike b)
    {
        Vector3 fwd = Quaternion.Euler(0f, b.yaw, 0f) * Vector3.forward;
        Vector3 origin = b.t.position + Vector3.up * 0.8f + fwd * 0.95f;
        int count = Physics.SphereCastNonAlloc(origin, 0.32f, fwd, hits, 7f, ~0, QueryTriggerInteraction.Ignore);
        float best = 99f;
        for (int i = 0; i < count; i++)
        {
            RaycastHit h = hits[i];
            Collider c = h.collider;
            if (c == null || c.transform.IsChildOf(b.t)) continue;
            if (!stopCache.TryGetValue(c, out bool stop))
            {
                stop = c.GetComponentInParent<TrafficBike>() != null || TrafficSystem.IsTrafficCar(c.transform)
                       || c.GetComponentInParent<CharacterController>() != null || c.GetComponentInParent<PedestrianWalker>() != null
                       || c.GetComponentInParent<PlayerCarController>() != null || (c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic);
                stopCache[c] = stop;
            }
            if (stop && h.distance < best) best = h.distance;
        }
        bool turning = b.seg < 9 && b.path.Count > 9;
        float cruise = b.cruise * (turning ? 0.65f : 1f);
        if (best > 6f) { b.blocked = 0f; return cruise; }
        b.blocked += 0.15f;
        if (b.blocked > 3f)
        {
            // Kẹt lâu: lách qua (kiểu xe máy Việt Nam)
            if (b.blocked > 4.5f) b.blocked = 0f;
            return cruise * 0.45f;
        }
        return best < 1.4f ? 0f : cruise * (best - 1.4f) / 4.6f;
    }

    // ================= Mesh xe + người lái =================
    private void BuildMeshes()
    {
        dry = new Mesh[Variants];
        rain = new Mesh[Variants];
        Color[] bodies =
        {
            new Color(0.08f, 0.08f, 0.09f), new Color(0.75f, 0.1f, 0.1f), new Color(0.12f, 0.25f, 0.6f), new Color(0.92f, 0.92f, 0.9f),
            new Color(0.6f, 0.62f, 0.65f), new Color(0.85f, 0.6f, 0.75f), new Color(0.35f, 0.25f, 0.2f), new Color(0.2f, 0.5f, 0.35f),
        };
        Color[] helmets = { new Color(0.9f, 0.1f, 0.1f), new Color(0.95f, 0.95f, 0.95f), new Color(0.1f, 0.3f, 0.7f), new Color(0.95f, 0.8f, 0.1f), new Color(0.1f, 0.1f, 0.1f), new Color(0.9f, 0.5f, 0.7f) };
        Color[] shirts =
        {
            new Color(0.95f, 0.95f, 0.95f), new Color(0.2f, 0.35f, 0.7f), new Color(0.75f, 0.15f, 0.15f), new Color(0.3f, 0.55f, 0.35f),
            new Color(0.95f, 0.8f, 0.3f), new Color(0.15f, 0.15f, 0.18f), new Color(0.9f, 0.55f, 0.65f), new Color(0.55f, 0.7f, 0.9f),
        };
        Color[] ponchos = { new Color(0.15f, 0.45f, 0.85f), new Color(0.95f, 0.8f, 0.15f), new Color(0.9f, 0.4f, 0.65f), new Color(0.3f, 0.7f, 0.35f), new Color(0.6f, 0.35f, 0.8f), new Color(0.95f, 0.5f, 0.15f) };
        Color skin = new Color(0.88f, 0.7f, 0.55f), pants = new Color(0.15f, 0.17f, 0.25f), dark = new Color(0.08f, 0.08f, 0.08f);
        var rnd = new System.Random(2026);
        for (int v = 0; v < Variants; v++)
        {
            Color body = bodies[rnd.Next(bodies.Length)], helmet = helmets[rnd.Next(helmets.Length)], shirt = shirts[rnd.Next(shirts.Length)];
            Color poncho = ponchos[rnd.Next(ponchos.Length)];
            bool passenger = rnd.NextDouble() < 0.3, cargo = !passenger && rnd.NextDouble() < 0.3, nonLa = rnd.NextDouble() < 0.12;
            Color helmet2 = helmets[rnd.Next(helmets.Length)], shirt2 = shirts[rnd.Next(shirts.Length)];
            for (int pass = 0; pass < 2; pass++)
            {
                bool wet = pass == 1;
                var mb = new VMeshBuilder();
                void B(Vector3 c, Vector3 e, Vector3 s, Color col) => mb.Add(VShape.Cube, Matrix4x4.TRS(c, Quaternion.Euler(e), s), col);
                void P(Vector3 c, Vector3 e, Vector3 s, Color col) => mb.Add(VShape.Prism, Matrix4x4.TRS(c, Quaternion.Euler(e), s), col);
                void O(Vector3 c, Vector3 e, Vector3 s, Color col) => mb.Add(VShape.Blob, Matrix4x4.TRS(c, Quaternion.Euler(e), s), col);
                // Xe
                P(new Vector3(0f, 0.28f, 0.6f), new Vector3(0f, 0f, 90f), new Vector3(0.54f, 0.09f, 0.54f), dark);
                P(new Vector3(0f, 0.28f, -0.58f), new Vector3(0f, 0f, 90f), new Vector3(0.54f, 0.09f, 0.54f), dark);
                B(new Vector3(0f, 0.55f, -0.12f), Vector3.zero, new Vector3(0.32f, 0.36f, 0.95f), body);
                B(new Vector3(0f, 0.8f, 0.5f), new Vector3(-18f, 0f, 0f), new Vector3(0.4f, 0.62f, 0.1f), body);
                B(new Vector3(0f, 0.36f, 0.18f), Vector3.zero, new Vector3(0.34f, 0.05f, 0.42f), dark);
                B(new Vector3(0f, 0.84f, -0.3f), Vector3.zero, new Vector3(0.3f, 0.1f, 0.62f), dark);
                B(new Vector3(0f, 1.07f, 0.56f), Vector3.zero, new Vector3(0.66f, 0.05f, 0.05f), dark);
                B(new Vector3(0f, 1.02f, 0.64f), Vector3.zero, new Vector3(0.16f, 0.1f, 0.06f), new Color(1f, 0.95f, 0.75f));
                B(new Vector3(0.24f, 1.22f, 0.5f), Vector3.zero, new Vector3(0.1f, 0.06f, 0.02f), dark);
                B(new Vector3(-0.24f, 1.22f, 0.5f), Vector3.zero, new Vector3(0.1f, 0.06f, 0.02f), dark);
                B(new Vector3(0f, 0.7f, -0.62f), Vector3.zero, new Vector3(0.14f, 0.06f, 0.04f), new Color(0.9f, 0.1f, 0.1f));
                B(new Vector3(0f, 0.6f, -0.7f), Vector3.zero, new Vector3(0.18f, 0.11f, 0.02f), new Color(0.95f, 0.95f, 0.95f));
                // Người lái (ngồi, tay cầm lái)
                Rider(mb, -0.2f, wet ? poncho : shirt, nonLa ? new Color(0.92f, 0.86f, 0.62f) : helmet, nonLa, skin, pants, true, wet);
                if (passenger) Rider(mb, -0.62f, wet ? poncho : shirt2, helmet2, false, skin, pants, false, wet);
                if (cargo)
                {
                    B(new Vector3(0f, 1.15f, -0.82f), Vector3.zero, new Vector3(0.62f, 0.52f, 0.5f), new Color(0.68f, 0.52f, 0.32f));
                    B(new Vector3(0f, 1.42f, -0.82f), Vector3.zero, new Vector3(0.64f, 0.02f, 0.52f), new Color(0.2f, 0.2f, 0.2f));
                }
                if (wet) O(new Vector3(0f, 0.95f, 0.15f), Vector3.zero, new Vector3(0.75f, 0.5f, 1.1f), poncho); // vạt áo mưa trùm lên đầu xe
                Mesh m = mb.ToMesh("Xe may " + v + (wet ? " mua" : ""));
                if (wet) rain[v] = m; else dry[v] = m;
            }
        }
    }

    private static void Rider(VMeshBuilder mb, float z, Color shirt, Color hat, bool nonLa, Color skin, Color pants, bool driver, bool wet)
    {
        void B(Vector3 c, Vector3 e, Vector3 s, Color col) => mb.Add(VShape.Cube, Matrix4x4.TRS(c, Quaternion.Euler(e), s), col);
        void O(Vector3 c, Vector3 e, Vector3 s, Color col) => mb.Add(VShape.Blob, Matrix4x4.TRS(c, Quaternion.Euler(e), s), col);
        for (int s = -1; s <= 1; s += 2)
        {
            B(new Vector3(s * 0.13f, 0.95f, z + 0.2f), new Vector3(10f, 0f, 0f), new Vector3(0.15f, 0.14f, 0.44f), pants);
            B(new Vector3(s * 0.15f, 0.62f, z + 0.42f), new Vector3(-10f, 0f, 0f), new Vector3(0.12f, 0.44f, 0.13f), pants);
            if (driver) B(new Vector3(s * 0.24f, 1.28f, z + 0.38f), new Vector3(70f, 0f, 0f), new Vector3(0.1f, 0.5f, 0.12f), shirt);
            else B(new Vector3(s * 0.24f, 1.15f, z + 0.12f), new Vector3(20f, 0f, 0f), new Vector3(0.1f, 0.45f, 0.12f), shirt);
        }
        B(new Vector3(0f, 1.3f, z), new Vector3(10f, 0f, 0f), wet ? new Vector3(0.5f, 0.68f, 0.36f) : new Vector3(0.4f, 0.58f, 0.24f), shirt);
        O(new Vector3(0f, 1.74f, z + 0.06f), Vector3.zero, new Vector3(0.24f, 0.28f, 0.25f), skin);
        if (nonLa) mb.Add(VShape.Cone, Matrix4x4.TRS(new Vector3(0f, 1.95f, z + 0.06f), Quaternion.identity, new Vector3(0.58f, 0.22f, 0.58f)), hat);
        else O(new Vector3(0f, 1.8f, z + 0.04f), Vector3.zero, new Vector3(0.32f, 0.27f, 0.34f), wet ? shirt : hat);
    }
}
