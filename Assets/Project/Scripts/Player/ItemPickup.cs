using UnityEngine;

/// <summary>
/// "Đôi tay" của người chơi.
///  Tay không : [E] / Chuột trái = nhặt thùng, dùng máy tính
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

    void Update()
    {
        if (playerCamera == null) return;

        if (UIState.IsBlockingUIOpen)
        {
            SetPrompt(null);
            return;
        }

        Ray ray = playerCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0f));
        bool hasHit = Physics.Raycast(ray, out RaycastHit hit, pickupRange, interactableLayer, QueryTriggerInteraction.Ignore);

        if (currentItem == null) HandleEmptyHands(hasHit, hit);
        else HandleHoldingBox(hasHit, hit);
    }

    void OnDisable() => SetPrompt(null);

    // ================= Tay không =================
    private void HandleEmptyHands(bool hasHit, RaycastHit hit)
    {
        if (!hasHit) { SetPrompt(null); return; }

        bool pressed = Input.GetKeyDown(KeyCode.E) || Input.GetMouseButtonDown(0);

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

        Interactable box = hit.collider.GetComponentInParent<Interactable>();
        if (box != null)
        {
            SetPrompt($"[E] Pick up box: {box.DisplayName} ({box.productCount}/{box.Capacity})");
            if (pressed) PickUp(box);
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
        currentItem = box;
        box.OnPickedUp(holdPosition);
        SetPrompt(null);
    }

    private void DropOrDiscard()
    {
        Interactable box = currentItem;
        currentItem = null;
        SetPrompt(null);

        if (box == null) return;
        if (box.IsEmpty) box.Discard();
        else box.OnDropped();
    }

    private void PlaceOnShelf(Shelf shelf)
    {
        GameObject product = Instantiate(currentItem.ProductPrefab);
        if (shelf.TryAddProduct(product, holdPosition, currentItem.itemData))
            currentItem.RemoveOne();
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
        }
    }

    // ================= Gợi ý trên màn hình =================
    private void SetPrompt(string message)
    {
        if (message == lastPrompt) return;
        lastPrompt = message;

        if (GameManager.Instance == null) return;
        if (string.IsNullOrEmpty(message)) GameManager.Instance.HideInteractText();
        else GameManager.Instance.ShowInteractText(message);
    }
}
