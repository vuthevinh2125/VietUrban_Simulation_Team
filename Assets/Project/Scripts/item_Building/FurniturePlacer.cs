using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

/// <summary>
/// Lắp đặt / dời nội thất (giống Supermarket Simulator):
///  - Cầm thùng nội thất: hiện bóng mờ của món đồ trên sàn, xanh = đặt được, đỏ = vướng.
///    Giữ chuột trái + kéo trái/phải = xoay tự do 360° (2 chiều) · Click chuột trái = đặt
///    [Q]/[R] = xoay 90° · lăn chuột = xoay 15° · [E] = bỏ thùng xuống.
///  - Nhìn vào nội thất mình đã mua, bấm [F] để dời chỗ ([LMB] đặt, [F]/[Esc] huỷ).
/// Tự gắn vào người chơi (cùng object với ItemPickup).
/// </summary>
public class FurniturePlacer : MonoBehaviour
{
    public static FurniturePlacer Instance { get; private set; }

    public float placeRange = 6f;
    public float rotateStep = 90f;
    public float scrollStep = 15f;
    [Tooltip("Độ xoay (độ) cho mỗi đơn vị kéo chuột khi giữ chuột trái")]
    public float dragRotateSpeed = 6f;
    [Tooltip("Kéo chuột quá mức này thì tính là xoay, không phải click đặt")]
    public float dragThreshold = 0.3f;

    private ItemPickup hands;
    private Transform playerRoot;
    private Camera cam;

    private FurnitureData placingData;     // đang lắp món mới
    private FurnitureBox placingBox;
    private Transform moving;              // đang dời món cũ (nội thất đã mua hoặc kệ có sẵn)
    private string movingName;
    private Vector3 movingOldPos;
    private Quaternion movingOldRot;

    private GameObject ghost;
    private Material ghostMat;
    private Bounds localBounds;
    private float yaw;
    private bool valid;
    private Vector3 ghostPos;
    private int startFrame = -1;
    private bool lmbHeld, dragging;
    private float dragAmount;

    public bool IsBusy => placingData != null || moving != null;

    void Awake()
    {
        Instance = this;
        hands = GetComponent<ItemPickup>();
        // Gốc của nhân vật (để bỏ qua va chạm của chính người chơi)
        CharacterController cc = GetComponentInParent<CharacterController>();
        Rigidbody rb = GetComponentInParent<Rigidbody>();
        playerRoot = cc != null ? cc.transform : (rb != null ? rb.transform : transform);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
        if (ghost != null) Destroy(ghost);
    }

    // ================= Bắt đầu =================
    public void BeginPlace(FurnitureBox box)
    {
        if (IsBusy || box == null || box.data == null || box.data.prefab == null) return;
        placingData = box.data;
        placingBox = box;
        startFrame = Time.frameCount;
        box.gameObject.SetActive(false);
        yaw = transform.eulerAngles.y + 180f; // mặt kệ quay về phía người chơi
        CreateGhost(box.data.prefab.transform);
    }

    public void BeginMove(PlacedFurniture furniture)
    {
        if (furniture == null) return;
        BeginMove(furniture.transform, furniture.data != null ? furniture.data.displayName : furniture.name);
    }

    public void BeginMove(BuiltInFurniture furniture)
    {
        if (furniture == null) return;
        BeginMove(furniture.transform, furniture.DisplayName);
    }

    private void BeginMove(Transform furniture, string displayName)
    {
        if (IsBusy || furniture == null) return;
        moving = furniture;
        movingName = displayName;
        startFrame = Time.frameCount;
        movingOldPos = furniture.transform.position;
        movingOldRot = furniture.transform.rotation;
        yaw = furniture.transform.eulerAngles.y;
        CreateGhost(furniture.transform);
        furniture.gameObject.SetActive(false);
    }

    // ================= Mỗi frame =================
    void Update()
    {
        if (!IsBusy) return;
        if (cam == null) cam = hands != null && hands.playerCamera != null ? hands.playerCamera : Camera.main;
        if (UIState.IsBlockingUIOpen || cam == null) return;

        if (Input.GetKeyDown(KeyCode.R)) yaw += rotateStep;
        if (Input.GetKeyDown(KeyCode.Q)) yaw -= rotateStep;
        float scroll = Input.mouseScrollDelta.y;
        if (Mathf.Abs(scroll) > 0.01f) yaw += Mathf.Sign(scroll) * scrollStep;

        UpdateGhost();

        string name = placingData != null ? placingData.displayName : movingName;
        string cancel = placingData != null ? "[E] Drop box" : "[F] Cancel";
        string place = valid ? "[Click LMB] Place" : "<color=#FF6B6B>Can't place here</color>";
        hands.SetPrompt($"{name}\n{place}   [Hold LMB + drag] Rotate   [Q/R] 90°   {cancel}");

        if (Time.frameCount == startFrame) return; // phím vừa dùng để bắt đầu, không tính lần nữa

        // Chuột trái: kéo = xoay tự do, click (không kéo) = đặt
        if (Input.GetMouseButtonDown(0)) { lmbHeld = true; dragging = false; dragAmount = 0f; }
        if (lmbHeld && Input.GetMouseButton(0))
        {
            float dx = Input.GetAxis("Mouse X");
            dragAmount += Mathf.Abs(dx);
            if (!dragging && dragAmount > dragThreshold) dragging = true;
            if (dragging)
            {
                PlayerController.LookLocked = true; // giữ camera đứng yên trong lúc xoay
                yaw += dx * dragRotateSpeed;
            }
        }
        if (lmbHeld && Input.GetMouseButtonUp(0))
        {
            bool click = !dragging;
            lmbHeld = false;
            dragging = false;
            PlayerController.LookLocked = false;
            if (click && valid) { Place(); return; }
        }

        else if (placingData != null && Input.GetKeyDown(KeyCode.E)) CancelPlace();
        else if (moving != null && (Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.Escape))) CancelMove();
    }

    private void UpdateGhost()
    {
        valid = false;
        Ray ray = cam.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        if (!FindFloor(ray, out Vector3 point))
        {
            ghost.SetActive(false);
            return;
        }
        ghost.SetActive(true);
        Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
        ghostPos = point - rot * new Vector3(0f, localBounds.min.y, 0f);
        ghost.transform.SetPositionAndRotation(ghostPos, rot);

        valid = !Blocked(ghostPos, rot) && Vector3.Distance(cam.transform.position, point) <= placeRange;
        RuntimeMaterials.SetColor(ghostMat, valid ? new Color(0.2f, 1f, 0.3f, 0.35f) : new Color(1f, 0.2f, 0.2f, 0.35f));
    }

    /// <summary>Mặt sàn dưới tâm ngắm (bỏ qua bóng mờ, người chơi, trigger).</summary>
    private bool FindFloor(Ray ray, out Vector3 point)
    {
        point = default;
        RaycastHit[] hits = Physics.RaycastAll(ray, placeRange + 2f, ~0, QueryTriggerInteraction.Ignore);
        System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
        foreach (RaycastHit h in hits)
        {
            if (h.collider.transform.IsChildOf(playerRoot)) continue;
            if (h.normal.y < 0.85f) return false; // đụng tường / kệ trước khi tới sàn
            point = h.point;
            return true;
        }
        return false;
    }

    /// <summary>Có vật gì chiếm chỗ (bỏ qua sàn: chỉ xét từ 8cm trên mặt sàn trở lên)?</summary>
    private bool Blocked(Vector3 pos, Quaternion rot)
    {
        Vector3 size = localBounds.size;
        Vector3 half = new Vector3(size.x * 0.5f - 0.02f, (size.y - 0.08f) * 0.5f, size.z * 0.5f - 0.02f);
        Vector3 center = pos + rot * new Vector3(localBounds.center.x, localBounds.min.y + 0.08f + half.y, localBounds.center.z);
        foreach (Collider c in Physics.OverlapBox(center, half, rot, ~0, QueryTriggerInteraction.Ignore))
        {
            if (c.transform.IsChildOf(playerRoot)) continue;          // người chơi
            if (ghost != null && c.transform.IsChildOf(ghost.transform)) continue;
            return true;
        }
        return false;
    }

    // ================= Đặt / huỷ =================
    private void Place()
    {
        Quaternion rot = ghost.transform.rotation;
        if (placingData != null)
        {
            GameObject go = Instantiate(placingData.prefab, ghostPos, rot);
            go.name = placingData.displayName;
            PlacedFurniture pf = go.GetComponent<PlacedFurniture>();
            if (pf == null) pf = go.AddComponent<PlacedFurniture>();
            pf.data = placingData;
            EnsureObstacle(go);
            if (placingBox != null) Destroy(placingBox.gameObject);
            Debug.Log($"[Furniture] Đã lắp {placingData.displayName} tại {ghostPos:F1}.");
        }
        else
        {
            moving.SetPositionAndRotation(ghostPos, rot);
            moving.gameObject.SetActive(true);
            RebuildLabels(moving.gameObject);
            AudioManager.Play("place_item", 0.7f);
        }
        EndMode();
    }

    private void CancelPlace()
    {
        if (placingBox != null)
        {
            Vector3 drop = transform.position + transform.forward * 1.2f;
            placingBox.transform.position = drop + Vector3.up * 0.4f;
            placingBox.gameObject.SetActive(true);
        }
        EndMode();
    }

    private void CancelMove()
    {
        moving.SetPositionAndRotation(movingOldPos, movingOldRot);
        moving.gameObject.SetActive(true);
        EndMode();
    }

    private void EndMode()
    {
        placingData = null;
        placingBox = null;
        moving = null;
        lmbHeld = false;
        dragging = false;
        PlayerController.LookLocked = false;
        if (ghost != null) Destroy(ghost);
        ghost = null;
        hands.SetPrompt(null);
    }

    private static void EnsureObstacle(GameObject go)
    {
        if (go.GetComponent<NavMeshObstacle>() != null) return;
        Bounds b = ProductFit.LocalBounds(go.transform);
        NavMeshObstacle o = go.AddComponent<NavMeshObstacle>();
        o.shape = NavMeshObstacleShape.Box;
        o.center = b.center;
        o.size = b.size;
        o.carving = true;
        o.carveOnlyStationary = true;
    }

    /// <summary>Nhãn giá được tạo theo vị trí kệ -> dời kệ thì tạo lại.</summary>
    private static void RebuildLabels(GameObject go)
    {
        ShelfPriceLabels old = go.GetComponent<ShelfPriceLabels>();
        if (old != null) DestroyImmediate(old);
        if (go.GetComponent<Shelf>() != null) go.AddComponent<ShelfPriceLabels>();
    }

    // ================= Bóng mờ =================
    /// <summary>Bản sao chỉ có hình (không script, không va chạm) của nguồn, phủ vật liệu trong suốt.</summary>
    private void CreateGhost(Transform source)
    {
        if (ghostMat == null) ghostMat = CreateGhostMaterial();
        ghost = new GameObject("FurnitureGhost");
        Matrix4x4 toRoot = source.worldToLocalMatrix;
        foreach (MeshFilter mf in source.GetComponentsInChildren<MeshFilter>(true))
        {
            MeshRenderer mr = mf.GetComponent<MeshRenderer>();
            if (mf.sharedMesh == null || mr == null) continue;
            Matrix4x4 m = toRoot * mf.transform.localToWorldMatrix;
            GameObject part = new GameObject(mf.name, typeof(MeshFilter), typeof(MeshRenderer));
            part.transform.SetParent(ghost.transform, false);
            part.transform.localPosition = m.GetColumn(3);
            part.transform.localRotation = m.rotation;
            part.transform.localScale = m.lossyScale;
            part.GetComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
            MeshRenderer gr = part.GetComponent<MeshRenderer>();
            var mats = new Material[mr.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++) mats[i] = ghostMat;
            gr.sharedMaterials = mats;
            gr.shadowCastingMode = ShadowCastingMode.Off;
        }
        localBounds = ProductFit.LocalBounds(source);
        // Nguồn có thể bị scale ở gốc: bóng mờ nhận cùng scale
        ghost.transform.localScale = source.lossyScale;
        localBounds = new Bounds(Vector3.Scale(localBounds.center, source.lossyScale), Vector3.Scale(localBounds.size, source.lossyScale));
    }

    /// <summary>Vật liệu trong suốt có sẵn trong Resources (luôn có trong bản build, không bị hồng).</summary>
    private static Material CreateGhostMaterial() => RuntimeMaterials.Ghost(new Color(0.2f, 1f, 0.3f, 0.35f));
}
