using UnityEngine;

/// <summary>
/// "Đôi tay" của người chơi.
///  Tay không : [E] / Chuột trái = nhặt thùng, dùng máy tính, đặt giá bán khi nhìn vào kệ có hàng
///  Cầm thùng : Chuột trái (giữ) = xếp lên kệ · Chuột phải (giữ) = lấy lại vào thùng
///              [E] = đặt thùng xuống (thùng rỗng thì vứt đi)
/// </summary>
public class ItemPickup : MonoBehaviour
{
    public Camera playerCamera;
    public Transform holdPosition;
    public float pickupRange = 3f;
    public LayerMask interactableLayer;

    [Header("Stocking (giữ chuột để xếp liên tục)")]
    [Tooltip("Thời gian chờ trước khi bắt đầu lặp")]
    public float repeatStartDelay = 0.35f;
    [Tooltip("Thời gian giữa mỗi lần xếp khi giữ chuột")]
    public float repeatInterval = 0.12f;

    /// <summary>Giữ tương thích với code cũ. Dùng UIState.IsBlockingUIOpen cho code mới.</summary>
    public static bool isComputerOpen => UIState.IsBlockingUIOpen;

    public Interactable HeldBox => currentItem;

    private Interactable currentItem;
    private float nextRepeatTime;
    private string lastPrompt;

    void Awake()
    {
        // Chỉ người chơi thật (có camera) mới lắp nội thất (ComputerUI cũng lỡ gắn một ItemPickup thừa)
        if (playerCamera != null && GetComponent<FurniturePlacer>() == null) gameObject.AddComponent<FurniturePlacer>();
        if (playerCamera != null && GetComponent<PlayerTools>() == null) gameObject.AddComponent<PlayerTools>();
    }

    void Update()
    {
        if (playerCamera == null) return;
        if (FurniturePlacer.Instance != null && FurniturePlacer.Instance.IsBusy) return; // đang lắp / dời nội thất
        if (PlayerTools.Instance != null && PlayerTools.Instance.Current != PlayerTools.Tool.None) return; // đang cầm chổi / gậy

        if (UIState.IsBlockingUIOpen)
        {
            SetPrompt(null);
            return;
        }

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        bool hasHit = Physics.Raycast(ray, out RaycastHit hit, pickupRange, interactableLayer, QueryTriggerInteraction.Ignore);

        if (TryCatchThief()) return; // ưu tiên bắt trộm, kể cả khi đang cầm thùng
        if (currentItem == null && TryMoveBuiltIn(ray, hasHit, hit)) return;
        if (currentItem == null) HandleEmptyHands(hasHit, hit);
        else HandleHoldingBox(hasHit, hit);
    }

    void OnDisable() => SetPrompt(null);

    // ================= Bắt trộm =================
    [Header("Catch thief")]
    public float catchRange = 6f;

    /// <summary>
    /// Nhắm vào kẻ trộm đang chạy (trong hay ngoài cửa hàng) rồi bấm chuột trái / E để bắt.
    /// Tia hình cầu (dễ trúng người đang chạy), xét mọi layer vì khách không ở layer Interactable.
    /// </summary>
    private bool TryCatchThief()
    {
        if (NPCShopper.Active.Count == 0) return false;
        Ray look = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        NPCShopper thief = null;
        foreach (RaycastHit h in Physics.SphereCastAll(look, 0.35f, catchRange, ~0, QueryTriggerInteraction.Ignore))
        {
            NPCShopper n = h.collider.GetComponentInParent<NPCShopper>();
            if (n != null && n.IsFleeingThief) { thief = n; break; }
        }
        // Đứng sát bên cạnh cũng bắt được (không cần nhắm thật chuẩn)
        if (thief == null)
            foreach (NPCShopper n in NPCShopper.Active)
                if (n != null && n.IsFleeingThief && Vector3.Distance(n.transform.position, playerCamera.transform.position) < 2.2f) { thief = n; break; }
        if (thief == null) return false;

        // Tay không thì không bắt được: phải cầm gậy (phím 3) rồi vung trúng kẻ trộm
        SetPrompt("<color=#FF6B6B>Thief! Press [3] to grab the bat, then swing [LMB]!</color>");
        return true;
    }

    /// <summary>
    /// Kệ / tủ có sẵn nằm ở layer thường (không phải Interactable): tia xét mọi layer, trúng kệ có sẵn
    /// (và không có vật Interactable nào gần hơn) thì cho dời bằng [F].
    /// </summary>
    private bool TryMoveBuiltIn(Ray ray, bool hasHit, RaycastHit interactHit)
    {
        if (!Physics.Raycast(ray, out RaycastHit any, pickupRange, ~0, QueryTriggerInteraction.Ignore)) return false;
        if (hasHit && interactHit.distance <= any.distance + 0.05f) return false; // vật Interactable được ưu tiên
        if (((1 << any.collider.gameObject.layer) & interactableLayer.value) != 0) return false;
        BuiltInFurniture builtIn = any.collider.GetComponentInParent<BuiltInFurniture>();
        if (builtIn == null) return false;
        SetPrompt($"{builtIn.DisplayName}   [F] Move");
        if (Input.GetKeyDown(KeyCode.F) && FurniturePlacer.Instance != null)
        {
            SetPrompt(null);
            FurniturePlacer.Instance.BeginMove(builtIn);
        }
        return true;
    }

    // ================= Tay không =================
    private void HandleEmptyHands(bool hasHit, RaycastHit hit)
    {
        bool pressed = Input.GetKeyDown(KeyCode.E) || Input.GetMouseButtonDown(0);

        if (!hasHit) { SetPrompt(null); return; }

        // Biển OPEN / CLOSED
        StoreSign sign = hit.collider.GetComponentInParent<StoreSign>();
        if (sign != null)
        {
            SetPrompt(sign.Prompt);
            if (pressed) sign.Interact();
            return;
        }

        // Nhận tiền mặt / thẻ khách đưa trên quầy -> mở màn hình thanh toán
        PaymentToken payment = hit.collider.GetComponentInParent<PaymentToken>();
        if (payment != null && payment.counter != null)
        {
            SetPrompt(payment.Prompt);
            if (pressed) { SetPrompt(null); PaymentPanel.Open(payment.counter); }
            return;
        }

        // Quét hàng khách đặt trên quầy thu ngân
        CheckoutItem scanItem = hit.collider.GetComponentInParent<CheckoutItem>();
        if (scanItem != null && scanItem.counter != null)
        {
            SetPrompt($"[LMB] Scan {scanItem.DisplayName} {EconomyManager.Format(scanItem.price)}\n{scanItem.counter.GetStatusText()}");
            if (pressed) scanItem.counter.TryScan(scanItem);
            return;
        }

        ComputerZoom zoom = hit.collider.GetComponentInParent<ComputerZoom>();
        ComputerInteract computer = hit.collider.GetComponentInParent<ComputerInteract>();
        if (zoom != null || computer != null)
        {
            SetPrompt("[E] Use computer");
            if (pressed)
            {
                SetPrompt(null);
                if (zoom != null) zoom.ToggleComputer();
                else computer.OpenComputer();
            }
            return;
        }

        // Thùng nội thất mới mua -> nhặt lên để chọn chỗ lắp
        FurnitureBox furnitureBox = hit.collider.GetComponentInParent<FurnitureBox>();
        if (furnitureBox != null)
        {
            SetPrompt($"[E] Pick up furniture: {(furnitureBox.data != null ? furnitureBox.data.displayName : "?")}");
            if (pressed && FurniturePlacer.Instance != null) { SetPrompt(null); AudioManager.Play("box_pickup", 0.8f); FurniturePlacer.Instance.BeginPlace(furnitureBox); }
            return;
        }

        // Kệ kho: tia trúng khung kệ thì lấy thùng đang cất gần điểm nhắm nhất
        StorageRack rack = hit.collider.GetComponentInParent<StorageRack>();
        if (rack != null)
        {
            Interactable storedBox = rack.NearestStored(hit.point);
            if (storedBox != null)
            {
                SetPrompt($"[E] Take box: {storedBox.DisplayName} ({storedBox.productCount}/{storedBox.Capacity})");
                if (pressed) PickUp(storedBox);
                return;
            }
            SetPrompt($"Storage rack  {rack.Capacity - rack.FreeSlots}/{rack.Capacity} boxes   [F] Move");
            if (Input.GetKeyDown(KeyCode.F) && FurniturePlacer.Instance != null)
            {
                PlacedFurniture pfRack = rack.GetComponent<PlacedFurniture>();
                if (pfRack != null) { SetPrompt(null); FurniturePlacer.Instance.BeginMove(pfRack); }
            }
            return;
        }

        Interactable box = hit.collider.GetComponentInParent<Interactable>();
        if (box != null)
        {
            SetPrompt($"[E] Pick up box: {box.DisplayName} ({box.productCount}/{box.Capacity})");
            if (pressed) PickUp(box);
            return;
        }

        // Nhìn vào kệ có hàng: xem giá, bấm để đặt giá bán (như bấm vào nhãn giá trong Supermarket Simulator)
        Shelf shelf = hit.collider.GetComponentInParent<Shelf>();
        PlacedFurniture placed = hit.collider.GetComponentInParent<PlacedFurniture>();
        BuiltInFurniture builtIn = placed == null ? hit.collider.GetComponentInParent<BuiltInFurniture>() : null;
        string moveText = placed != null || builtIn != null ? "   [F] Move" : "";
        if ((placed != null || builtIn != null) && Input.GetKeyDown(KeyCode.F) && FurniturePlacer.Instance != null)
        {
            SetPrompt(null);
            if (placed != null) FurniturePlacer.Instance.BeginMove(placed);
            else FurniturePlacer.Instance.BeginMove(builtIn);
            return;
        }
        if (shelf != null && shelf.CurrentItem != null)
        {
            ItemData it = shelf.CurrentItem;
            SetPrompt($"{it.itemName}  {shelf.Count}/{shelf.Capacity}\n" +
                      $"Price {EconomyManager.Format(PriceManager.GetSellPrice(it))}  (Market {EconomyManager.Format(MarketPrices.Get(it))})\n[LMB] Set price{moveText}");
            if (pressed)
            {
                SetPrompt(null);
                PriceSetPanel.Open(it);
            }
            return;
        }
        if (placed != null)
        {
            SetPrompt($"{(placed.data != null ? placed.data.displayName : placed.name)}{moveText}");
            return;
        }
        if (builtIn != null)
        {
            SetPrompt($"{builtIn.DisplayName}{moveText}");
            return;
        }

        SetPrompt(null);
    }

    // ================= Đang cầm thùng =================
    private void HandleHoldingBox(bool hasHit, RaycastHit hit)
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            DropOrDiscard();
            return;
        }

        string boxInfo = $"{currentItem.DisplayName} ({currentItem.productCount}/{currentItem.Capacity})";

        // Nhìn vào kệ kho: cất thùng
        StorageRack storage = hasHit ? hit.collider.GetComponentInParent<StorageRack>() : null;
        if (storage != null)
        {
            bool free = storage.HasFreeSlot;
            SetPrompt($"{boxInfo}\n{(free ? "[LMB] Store on rack" : "<color=#FF6B6B>Rack is full</color>")}   [E] Drop");
            if (free && Input.GetMouseButtonDown(0))
            {
                Interactable b = currentItem;
                currentItem = null;
                SetPrompt(null);
                if (storage.TryStore(b, hit.point)) AudioManager.Play("box_pickup", 0.5f);
                else { currentItem = b; }
            }
            return;
        }

        Shelf shelf = hasHit ? hit.collider.GetComponentInParent<Shelf>() : null;

        if (shelf == null)
        {
            SetPrompt(currentItem.IsEmpty ? "Empty box   [E] Throw away" : $"{boxInfo}   [E] Drop box");
            return;
        }

        // --- Đang nhìn vào kệ ---
        bool canPlace = shelf.CanPlace(currentItem.itemData, out string placeReason);
        if (canPlace && currentItem.IsEmpty) { canPlace = false; placeReason = "Box is empty"; }
        if (canPlace && currentItem.ProductPrefab == null) { canPlace = false; placeReason = "Box has no product prefab"; }

        bool canTake = shelf.CanTakeInto(currentItem, out string takeReason);

        string placeText = canPlace ? "[LMB] Place" : placeReason;
        string takeText = canTake ? "   [RMB] Take back" : "";
        SetPrompt($"Shelf: {shelf.DisplayName} {shelf.Count}/{shelf.Capacity}   |   Box: {boxInfo}\n{placeText}{takeText}   [E] Drop");

        if (RepeatPressed(0))
        {
            if (canPlace) PlaceOnShelf(shelf);
        }
        else if (RepeatPressed(1))
        {
            if (canTake) TakeFromShelf(shelf);
        }
    }

    /// <summary>true khi vừa bấm, hoặc đang giữ đủ lâu để lặp lại.</summary>
    private bool RepeatPressed(int button)
    {
        if (Input.GetMouseButtonDown(button))
        {
            nextRepeatTime = Time.time + repeatStartDelay;
            return true;
        }
        if (Input.GetMouseButton(button) && Time.time >= nextRepeatTime)
        {
            nextRepeatTime = Time.time + repeatInterval;
            return true;
        }
        return false;
    }

    // ================= Hành động =================
    private void PickUp(Interactable box)
    {
        AudioManager.Play("box_pickup", 0.7f);
        currentItem = box;
        box.OnPickedUp(holdPosition);
        SetPrompt(null);
    }

    private void DropOrDiscard()
    {
        Interactable box = currentItem;
        currentItem = null;
        SetPrompt(null);
        AudioManager.Play("box_pickup", 0.5f);

        if (box == null) return;
        if (box.IsEmpty) box.Discard();
        else box.OnDropped();
    }

    private void PlaceOnShelf(Shelf shelf)
    {
        GameObject product = Instantiate(currentItem.ProductPrefab);
        if (shelf.TryAddProduct(product, holdPosition, currentItem.itemData))
        {
            currentItem.RemoveOne();
            AudioManager.Play("place_item", 0.45f);
        }
        else
            Destroy(product);
    }

    private void TakeFromShelf(Shelf shelf)
    {
        // Thùng rỗng chưa có loại hàng thì nhận loại của kệ
        if (currentItem.itemData == null && currentItem.IsEmpty)
            currentItem.SetItem(shelf.CurrentItem);

        if (shelf.TryTakeProduct(out GameObject product))
        {
            Destroy(product);
            currentItem.AddOne();
            AudioManager.Play("place_item", 0.35f);
        }
    }

    // ================= Gợi ý trên màn hình =================
    public void SetPrompt(string message)
    {
        if (message == lastPrompt) return;
        lastPrompt = message;

        if (GameManager.Instance == null) return;
        if (string.IsNullOrEmpty(message)) GameManager.Instance.HideInteractText();
        else GameManager.Instance.ShowInteractText(message);
    }
}
