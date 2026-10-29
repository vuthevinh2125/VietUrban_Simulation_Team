using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Xuất báo cáo bố cục scene ra Logs/SceneReport.txt (vị trí kệ, quầy, cửa, NPC, điểm đích, kiểm tra NavMesh).
/// Menu: Tools > Debug > Export Scene Report
/// </summary>
public static class SceneReportTool
{
    [MenuItem("Tools/Debug/Export Scene Report")]
    public static void Export()
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Scene report: " + UnityEngine.SceneManagement.SceneManager.GetActiveScene().path);

        var all = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        Section(sb, "Player", all.Where(t => t.CompareTag("Player")));
        Section(sb, "Shelves", Object.FindObjectsByType<Shelf>(FindObjectsInactive.Include, FindObjectsSortMode.None).Select(s => s.transform),
            t => { var s = t.GetComponent<Shelf>(); return $"slots={s.slots?.Length ?? 0} assigned={(s.assignedItem ? s.assignedItem.itemName : "-")} accepts='{s.acceptsDisplayType}'"; });
        Section(sb, "Computers", Object.FindObjectsByType<ComputerInteract>(FindObjectsInactive.Include, FindObjectsSortMode.None).Select(c => c.transform));
        Section(sb, "AutoDoors", Object.FindObjectsByType<AutoDoor>(FindObjectsInactive.Include, FindObjectsSortMode.None).Select(d => d.transform),
            t => { var c = t.GetComponent<Collider>(); return c ? $"area center={c.bounds.center:F2} size={c.bounds.size:F2} trigger={c.isTrigger}" : "no collider"; });
        Section(sb, "MarketManager / DeliveryPoint", Object.FindObjectsByType<MarketManager>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(m => m.deliveryPoint != null).Select(m => m.deliveryPoint));

        // NPCs
        sb.AppendLine("\n## NavMeshAgents (NPC)");
        foreach (var a in Object.FindObjectsByType<NavMeshAgent>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            var scripts = a.GetComponents<MonoBehaviour>().Where(m => m != null).Select(m => m.GetType().Name);
            var src = PrefabUtility.GetCorrespondingObjectFromSource(a.gameObject);
            sb.AppendLine($"- {Path(a.transform)} pos={a.transform.position:F2} active={a.gameObject.activeInHierarchy} tag={a.tag} " +
                          $"prefab={(src ? AssetDatabase.GetAssetPath(src) : "-")} anim={(a.GetComponentInChildren<Animator>() != null)} " +
                          $"collider={(a.GetComponent<Collider>() != null)} rb={(a.GetComponent<Rigidbody>() != null)} speed={a.speed} radius={a.radius} scripts=[{string.Join(",", scripts)}]");
            var shopper = a.GetComponent<NPCShopper>();
            if (shopper != null)
            {
                sb.AppendLine($"    shopper.shelves=[{string.Join(", ", (shopper.shelves ?? new Transform[0]).Where(s => s).Select(s => s.name + " " + s.position.ToString("F2")))}]");
                sb.AppendLine($"    shopper.checkout={Desc(shopper.checkoutCounter)} exit={Desc(shopper.exitDoor)} items={Desc(shopper.checkoutItemsGroup ? shopper.checkoutItemsGroup.transform : null)}");
            }
        }

        // Named points
        string[] keys = { "point", "pay", "exit", "spawn", "checkout", "cashier", "cachier", "counter", "queue", "door", "entrance", "register", "cash" };
        Section(sb, "Named points (point/pay/exit/spawn/checkout/cashier/counter/queue/door/entrance/register/cash)",
            all.Where(t => t.gameObject.scene.IsValid() && keys.Any(k => t.name.ToLower().Contains(k)) && t.GetComponent<Shelf>() == null).Take(80));

        // Store building candidates
        Section(sb, "Store-ish objects (supermarket/store/shop/market/utility/products)",
            all.Where(t => t.parent == null || t.parent.parent == null)
               .Where(t => new[] { "supermarket", "store", "shop", "market", "utility", "products", "lighting" }.Any(k => t.name.ToLower().Contains(k))).Take(40),
            t => { var r = t.GetComponentsInChildren<Renderer>(); if (r.Length == 0) return ""; var b = r[0].bounds; foreach (var x in r) b.Encapsulate(x.bounds); return $"bounds center={b.center:F1} size={b.size:F1}"; });

        // NavMesh reachability
        sb.AppendLine("\n## NavMesh check");
        var tri = NavMesh.CalculateTriangulation();
        sb.AppendLine($"navmesh vertices={tri.vertices.Length} triangles={tri.indices.Length / 3}");
        Vector3? origin = null;
        var firstAgent = Object.FindFirstObjectByType<NavMeshAgent>();
        if (firstAgent) origin = firstAgent.transform.position;
        var points = new List<Transform>();
        points.AddRange(all.Where(t => t.gameObject.scene.IsValid() && keys.Any(k => t.name.ToLower().Contains(k)) && t.GetComponent<Shelf>() == null).Take(40));
        points.AddRange(Object.FindObjectsByType<Shelf>(FindObjectsInactive.Include, FindObjectsSortMode.None).Select(s => s.transform));
        foreach (var p in points.Distinct())
        {
            string onMesh = NavMesh.SamplePosition(p.position, out NavMeshHit hit, 3f, NavMesh.AllAreas)
                ? $"nearest mesh {Vector3.Distance(p.position, hit.position):F2}m at {hit.position:F2}" : "NO MESH within 3m";
            string path = "-";
            if (origin.HasValue && NavMesh.SamplePosition(origin.Value, out NavMeshHit o, 3f, NavMesh.AllAreas))
            {
                var np = new NavMeshPath();
                NavMesh.CalculatePath(o.position, NavMesh.SamplePosition(p.position, out NavMeshHit h2, 3f, NavMesh.AllAreas) ? h2.position : p.position, NavMesh.AllAreas, np);
                path = np.status.ToString();
            }
            sb.AppendLine($"- {Path(p)} pos={p.position:F2} | {onMesh} | path from '{(firstAgent ? firstAgent.name : "-")}': {path}");
        }

        // Character prefabs
        sb.AppendLine("\n## Character prefabs");
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Project/Prefabs/Characters" }))
        {
            string ap = AssetDatabase.GUIDToAssetPath(guid);
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(ap);
            var comps = go.GetComponentsInChildren<Component>(true).Where(c => c != null).Select(c => c.GetType().Name).Distinct();
            sb.AppendLine($"- {ap}: [{string.Join(",", comps)}]");
        }

        // Layers & tags
        sb.AppendLine("\n## Tags: " + string.Join(", ", UnityEditorInternal.InternalEditorUtility.tags));

        string file = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "Logs", "SceneReport.txt");
        File.WriteAllText(file, sb.ToString());
        Debug.Log("[SceneReport] Đã xuất báo cáo: " + file);
    }

    private static void Section(StringBuilder sb, string title, IEnumerable<Transform> items, System.Func<Transform, string> extra = null)
    {
        sb.AppendLine("\n## " + title);
        foreach (var t in items)
            if (t != null)
                sb.AppendLine($"- {Path(t)} pos={t.position:F2} rotY={t.eulerAngles.y:F0} active={t.gameObject.activeInHierarchy} {(extra != null ? extra(t) : "")}");
    }

    private static string Desc(Transform t) => t == null ? "null" : $"{t.name} {t.position:F2}";

    private static string Path(Transform t)
    {
        var parts = new List<string>();
        for (var p = t; p != null; p = p.parent) parts.Insert(0, p.name);
        return string.Join("/", parts);
    }
}
