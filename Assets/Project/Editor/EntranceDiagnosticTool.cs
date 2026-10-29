using System.IO;
using System.Linq;
using System.Text;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Soi khu lối vào: NavMesh có thông qua từng cửa không, vật gì đang chắn trong khung cửa.
/// Menu: Tools > Debug > Diagnose Store Entrances  -> Logs/EntranceDiag.txt
/// </summary>
public static class EntranceDiagnosticTool
{
    [MenuItem("Tools/Debug/Diagnose Store Entrances")]
    private static void Run()
    {
        var sb = new StringBuilder("# Entrance diagnostic\n");

        NavMeshSurface surface = Object.FindFirstObjectByType<NavMeshSurface>();
        if (surface != null)
        {
            NavMeshBuildSettings s = NavMesh.GetSettingsByID(surface.agentTypeID);
            sb.AppendLine($"Surface '{surface.name}': agentRadius={s.agentRadius} height={s.agentHeight} step={s.agentClimb} slope={s.agentSlope} " +
                          $"collect={surface.collectObjects} geometry={surface.useGeometry} layers={surface.layerMask.value} data={(surface.navMeshData ? surface.navMeshData.name : "NONE")}");
        }

        foreach (EntranceDoor door in Object.FindObjectsByType<EntranceDoor>(FindObjectsSortMode.None))
        {
            Bounds db = default;
            bool any = false;
            foreach (Renderer r in door.GetComponentsInChildren<Renderer>())
            {
                if (!any) { db = r.bounds; any = true; } else db.Encapsulate(r.bounds);
            }
            if (!any) continue;

            // Pháp tuyến cửa = trục mỏng nhất (ngang) của khung cửa
            Vector3 normal = db.size.x < db.size.z ? Vector3.right : Vector3.forward;
            Vector3 along = normal == Vector3.right ? Vector3.forward : Vector3.right;
            sb.AppendLine($"\n## Door '{door.name}' center={db.center:F2} size={db.size:F2} normal={normal}");

            // Quét 3 đường xuyên qua cửa: giữa, lệch trái, lệch phải
            float halfWidth = Vector3.Dot(db.extents, along);
            foreach (float off in new[] { -halfWidth * 0.5f, 0f, halfWidth * 0.5f })
            {
                var line = new StringBuilder();
                for (float d = -3f; d <= 3.01f; d += 0.25f)
                {
                    Vector3 p = new Vector3(db.center.x, db.min.y + 0.1f, db.center.z) + along * off + normal * d;
                    bool on = NavMesh.SamplePosition(p, out NavMeshHit h, 0.35f, NavMesh.AllAreas) && Mathf.Abs(h.position.y - p.y) < 0.4f;
                    line.Append(on ? '#' : '.');
                }
                sb.AppendLine($"  offset {off,5:F2}: [{line}]   (# = có NavMesh, . = không; trái = phía -normal, phải = +normal, mỗi ký tự 25cm)");
            }

            // Đường đi 2.5m bên này -> 2.5m bên kia
            Vector3 a0 = new Vector3(db.center.x, db.min.y, db.center.z) - normal * 2.5f;
            Vector3 b0 = new Vector3(db.center.x, db.min.y, db.center.z) + normal * 2.5f;
            string status = "no mesh at ends";
            if (NavMesh.SamplePosition(a0, out NavMeshHit ha, 1.5f, NavMesh.AllAreas) && NavMesh.SamplePosition(b0, out NavMeshHit hb, 1.5f, NavMesh.AllAreas))
            {
                var path = new NavMeshPath();
                NavMesh.CalculatePath(ha.position, hb.position, NavMesh.AllAreas, path);
                status = $"{path.status} corners={path.corners.Length} end={(path.corners.Length > 0 ? path.corners[path.corners.Length - 1].ToString("F2") : "-")} target={hb.position:F2}";
            }
            sb.AppendLine($"  path through door: {status}");

            // Vật thể nằm trong khung cửa
            Bounds gap = new Bounds(db.center, new Vector3(normal == Vector3.right ? 1.2f : db.size.x, db.size.y, normal == Vector3.forward ? 1.2f : db.size.z));
            sb.AppendLine("  objects inside the doorway volume:");
            foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (!r.enabled || r.transform.IsChildOf(door.transform)) continue;
                if (!r.bounds.Intersects(gap)) continue;
                if (r.bounds.max.y < gap.min.y + 0.05f) continue; // sàn nằm dưới
                bool ignored = r.GetComponentsInParent<NavMeshModifier>(true).Any(m => m.ignoreFromBuild);
                string path = Path(r.transform);
                sb.AppendLine($"   - {path} | bounds min={r.bounds.min:F2} max={r.bounds.max:F2} | layer={LayerMask.LayerToName(r.gameObject.layer)} | navmeshIgnored={ignored} | mesh={(r.GetComponent<MeshFilter>() && r.GetComponent<MeshFilter>().sharedMesh ? r.GetComponent<MeshFilter>().sharedMesh.name : "-")}");
            }
        }

        // Điểm sinh khách
        CustomerSpawner spawner = Object.FindFirstObjectByType<CustomerSpawner>();
        if (spawner != null && spawner.spawnPoint != null)
        {
            Vector3 sp = spawner.spawnPoint.position;
            bool on = NavMesh.SamplePosition(sp, out NavMeshHit h, 1f, NavMesh.AllAreas);
            sb.AppendLine($"\nSpawn point {sp:F2}: onNavMesh={on} {(on ? "nearest " + h.position.ToString("F2") : "")}");
        }

        string file = System.IO.Path.Combine(Directory.GetCurrentDirectory(), "Logs", "EntranceDiag.txt");
        File.WriteAllText(file, sb.ToString());
        Debug.Log("[Entrance Diag] " + file);
    }

    private static string Path(Transform t)
    {
        var parts = new System.Collections.Generic.List<string>();
        for (var p = t; p != null; p = p.parent) parts.Insert(0, p.name);
        return string.Join("/", parts);
    }
}
