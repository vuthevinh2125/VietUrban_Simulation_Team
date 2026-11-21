using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class InventoryUIController : MonoBehaviour
{
    private enum InventoryPage
    {
        Food, Consume, Shipping
    }
    [Header("data")]
    [SerializeField] private ItemDatabase itemDatabase;
    [SerializeField] private DeliveryInventory deliveryInventory;
    [SerializeField] private SelectionManager selectionManager;
    [SerializeField] private PlayerStamina playerStamina;
    [SerializeField, Min(1)] private int maxSlots = 64;
    [Header("tabs and grid")]
    [SerializeField] private Button foodTabButton;
    [SerializeField] private Button consumeTabButton;
    [SerializeField] private Button shippingTabButton;
    [SerializeField] private Transform slotGrid;
    [SerializeField] private InventorySlotUI slotPrefab;
    [Header("panel")]
    [SerializeField] private GameObject inventoryPanelRoot;
    [SerializeField] private Button closeButton;
    [SerializeField] private KeyCode toggleKey = KeyCode.I;
    [Header("item info")]
    [SerializeField] private Image selectedIcon;
    [SerializeField] private TMP_Text infoText;
    [SerializeField] private TMP_Text capacityText;
    [Header("shipping")]
    [SerializeField] private Button deliverButton;
    [SerializeField] private TMP_Text deliverButtonText;
    [SerializeField] private DeliveryTargetMarker deliveryTargetMarker;
    [SerializeField, Min(0.1f)] private float refreshInterval = 0.25f;
    private readonly List<InventorySlotUI> slots = new List<InventorySlotUI>();
    private readonly List<ItemData> visibleItems = new List<ItemData>();
    private readonly List<float> visibleAmounts = new List<float>();
    private readonly List<Order> visiblePackages = new List<Order>();
    private InventoryPage currentPage;
    private ItemData selectedItem;
    private float selectedItemAmount;
    private Order selectedOrder;
    private IngredientStation selectedIngredientStation;
    private float refreshTimer;
    private bool panelOpen;
    private CursorLockMode previousCursorLockMode;
    private bool previousCursorVisible;
    private bool previousInteractionEnabled;
    private void Awake()
    {
        if (playerStamina == null)
        {
            playerStamina = FindAnyObjectByType<PlayerStamina>();
        }
        if (deliveryTargetMarker == null)
        {
            deliveryTargetMarker = FindAnyObjectByType<DeliveryTargetMarker>(FindObjectsInactive.Include);
        }
        if (inventoryPanelRoot != null)
        {
            inventoryPanelRoot.SetActive(false);
        }
        if (foodTabButton != null)
        {
            foodTabButton.onClick.AddListener(ShowFood);
        }
        if (consumeTabButton != null)
        {
            consumeTabButton.onClick.AddListener(ShowConsume);
        }
        if (shippingTabButton != null)
        {
            shippingTabButton.onClick.AddListener(ShowShipping);
        }
        if (deliverButton != null)
        {
            deliverButton.onClick.AddListener(HandleActionButton);
        }
        if (closeButton != null)
        {
            closeButton.onClick.AddListener(ClosePanel);
        }
        CreateSlots();
        ShowFood();
    }
    private void Start()
    {
        if (playerStamina != null)
        {
            playerStamina.StaminaChanged += HandleStaminaChanged;
        }
        if (deliveryInventory != null)
        {
            deliveryInventory.PackagesChanged += HandlePackagesChanged;
            deliveryInventory.SelectedPackageChanged += HandleSelectedPackageChanged;
        }
        RefreshCurrentPage();
    }
    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            TogglePanel();
        }
        if (panelOpen == false)
        {
            return;
        }
        if (currentPage != InventoryPage.Shipping)
        {
            return;
        }
        refreshTimer += Time.unscaledDeltaTime;
        if (refreshTimer < refreshInterval)
        {
            return;
        }
        refreshTimer = 0f;
        UpdatePackageDetails();
        UpdateDeliveryButton();
    }
    private void OnDestroy()
    {
        if (foodTabButton != null)
        {
            foodTabButton.onClick.RemoveListener(ShowFood);
        }
        if (consumeTabButton != null)
        {
            consumeTabButton.onClick.RemoveListener(ShowConsume);
        }
        if (shippingTabButton != null)
        {
            shippingTabButton.onClick.RemoveListener(ShowShipping);
        }
        if (deliverButton != null)
        {
            deliverButton.onClick.RemoveListener(HandleActionButton);
        }
        if (closeButton != null)
        {
            closeButton.onClick.RemoveListener(ClosePanel);
        }
        if (deliveryInventory != null)
        {
            deliveryInventory.PackagesChanged -= HandlePackagesChanged;
            deliveryInventory.SelectedPackageChanged -= HandleSelectedPackageChanged;
        }
        if (playerStamina != null)
        {
            playerStamina.StaminaChanged -= HandleStaminaChanged;
        }
        if (panelOpen == true)
        {
            Cursor.lockState = previousCursorLockMode;
            Cursor.visible = previousCursorVisible;
        }
    }
    public void ShowFood()
    {
        SetPage(InventoryPage.Food);
    }
    public void ShowConsume()
    {
        SetPage(InventoryPage.Consume);
    }
    public void ShowShipping()
    {
        SetPage(InventoryPage.Shipping);
    }

    public void TogglePanel()
    {
        SetPanelOpen(!panelOpen);
    }

    public void ClosePanel()
    {
        SetPanelOpen(false);
    }

    public void SetPanelOpen(bool open)
    {
        if (panelOpen == open)
        {
            return;
        }

        if (open == true)
        {
            previousCursorLockMode = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            if (selectionManager != null)
            {
                previousInteractionEnabled = selectionManager.InteractionEnabled;
                if (currentPage == InventoryPage.Food)
                {
                    SetIngredientStation(selectionManager.CurrentInteractable as IngredientStation);
                }
                selectionManager.SetInteractionEnabled(false);
            }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = previousCursorLockMode;
            Cursor.visible = previousCursorVisible;
            if (selectionManager != null)
            {
                selectionManager.SetInteractionEnabled(previousInteractionEnabled);
            }
        }

        panelOpen = open;
        if (inventoryPanelRoot != null)
        {
            inventoryPanelRoot.SetActive(open);
        }
        if (open == true)
        {
            RefreshCurrentPage();
        }
    }
    public void RefreshCurrentPage()
    {
        ClearSelection();
        visibleItems.Clear();
        visibleAmounts.Clear();
        visiblePackages.Clear();
        if (currentPage == InventoryPage.Food || currentPage == InventoryPage.Consume)
        {
            RefreshItemList();
        }
        else
        {
            RefreshPackageList();
        }

        RenderSlots();
        UpdateCapacityText();
        UpdateDeliveryButton();
    }
    public void SetIngredientStation(IngredientStation station)
    {
        selectedIngredientStation = station;
        UpdateDeliveryButton();
    }
    private void SetPage(InventoryPage page)
    {
        currentPage = page;
        RefreshCurrentPage();
    }
    private void CreateSlots()
    {
        if (slotGrid == null || slotPrefab == null)
        {
            return;
        }
        for (int index = slotGrid.childCount - 1; index >= 0; index--)
        {
            Destroy(slotGrid.GetChild(index).gameObject);
        }
        for (int index = 0; index < maxSlots; index++)
        {
            InventorySlotUI slot = Instantiate(slotPrefab, slotGrid);
            slot.SetEmpty();
            slots.Add(slot);
        }
    }
    private void RefreshItemList()
    {
        if (itemDatabase == null || GameSession.Current == null || GameSession.Current.inventory == null)
        {
            return;
        }
        ItemCategory wantedCategory = ItemCategory.FoodBuy;
        if (currentPage == InventoryPage.Consume)
        {
            wantedCategory = ItemCategory.Consume;
        }
        foreach (InventoryEntries entry in GameSession.Current.inventory)
        {
            if (entry == null || entry.amount <= 0f)
            {
                continue;
            }
            ItemData item = itemDatabase.GetById(entry.itemId);
            if (item == null || item.category != wantedCategory)
            {
                continue;
            }
            visibleItems.Add(item);
            visibleAmounts.Add(entry.amount);
            if (visibleItems.Count >= maxSlots)
            {
                break;
            }
        }
    }
    private void RefreshPackageList()
    {
        if (deliveryInventory == null)
        {
            return;
        }
        foreach (Order order in deliveryInventory.Packages)
        {
            if (order != null && order.OrderStatus == Order.OrderState.Closed)
            {
                visiblePackages.Add(order);
                if (visiblePackages.Count >= maxSlots)
                {
                    break;
                }
            }
        }
    }
    private void RenderSlots()
    {
        int count = slots.Count;
        for (int index = 0; index < count; index++)
        {
            InventorySlotUI slot = slots[index];
            if (currentPage == InventoryPage.Shipping)
            {
                if (index < visiblePackages.Count)
                {
                    Order order = visiblePackages[index];
                    int orderId = order.OrderId;
                    slot.SetOrder(order, delegate { SelectPackage(orderId); });
                    slot.SetHighlighted(selectedOrder != null && selectedOrder.OrderId == orderId);
                }
                else
                {
                    slot.SetEmpty();
                }
            }
            else if (index < visibleItems.Count)
            {
                ItemData item = visibleItems[index];
                float amount = visibleAmounts[index];
                slot.SetItem(item, amount, delegate { SelectItem(item, amount); });
                slot.SetHighlighted(selectedItem == item);
            }
            else
            {
                slot.SetEmpty();
            }
        }
    }
    private void SelectItem(ItemData item, float amount)
    {
        selectedItem = item;
        selectedItemAmount = amount;
        selectedOrder = null;
        UpdateItemDetails();
        RenderSlots();
        UpdateDeliveryButton();
    }
    private void SelectPackage(int orderId)
    {
        if (deliveryInventory == null || !deliveryInventory.SelectPackage(orderId))
        {
            return;
        }
        selectedOrder = deliveryInventory.SelectedPackage;
        selectedItem = null;
        UpdatePackageDetails();
        RenderSlots();
        UpdateDeliveryButton();
    }
    private void UpdateItemDetails()
    {
        if (selectedIcon != null)
        {
            selectedIcon.sprite = null;
            if (selectedItem != null)
            {
                selectedIcon.sprite = selectedItem.icon;
            }
            selectedIcon.enabled = selectedItem != null && selectedItem.icon != null;
        }
        if (infoText != null)
        {
            if (selectedItem == null)
            {
                infoText.text = "select an item";
            }
            else
            {
                string itemDetails = selectedItem.itemName + " - " + FormatAmount(selectedItem, selectedItemAmount);
                if (selectedItem.category == ItemCategory.Consume)
                {
                    itemDetails += "\n+" + selectedItem.staminaRestore.ToString("0.##") + " stamina";
                }
                infoText.text = itemDetails;
            }
        }
        if (deliveryTargetMarker != null)
        {
            deliveryTargetMarker.SetTarget(null);
        }
    }
    private void UpdatePackageDetails()
    {
        if (selectedIcon != null)
        {
            ItemData dish = null;
            if (selectedOrder != null && selectedOrder.Recipe != null)
            {
                dish = selectedOrder.Recipe.DishItem;
            }
            selectedIcon.sprite = null;
            if (dish != null)
            {
                selectedIcon.sprite = dish.icon;
            }
            selectedIcon.enabled = dish != null && dish.icon != null;
        }
        if (infoText != null)
        {
            if (selectedOrder == null)
            {
                infoText.text = "select a package";
            }
            else
            {
                string destination = selectedOrder.CustomerName + " | " + selectedOrder.DeliveryAddress;
                if (selectedOrder.CustomerNpc == null)
                {
                    destination += "\ndelivery location unavailable";
                }
                else if (deliveryInventory != null)
                {
                    float distance = deliveryInventory.DistanceToSelectedCustomer;
                    destination += "\n" + distance.ToString("0.0") + "m away";
                }
                infoText.text = destination;
            }
        }
        if (deliveryTargetMarker != null)
        {
            Transform target = null;
            Transform beamTarget = null;
            if (selectedOrder != null)
            {
                target = selectedOrder.CustomerNpc;
                if (selectedOrder.CustomerResident != null)
                {
                    beamTarget = selectedOrder.CustomerResident.transform;
                }
            }
            deliveryTargetMarker.SetTarget(target, beamTarget);
        }
    }
    private void ClearSelection()
    {
        selectedItem = null;
        selectedItemAmount = 0f;
        selectedOrder = null;
        if (currentPage == InventoryPage.Shipping && deliveryInventory != null)
        {
            deliveryInventory.ClearSelectedPackage();
        }
        if (deliveryTargetMarker != null)
        {
            deliveryTargetMarker.SetTarget(null);
        }
        if (selectedIcon != null)
        {
            selectedIcon.sprite = null;
            selectedIcon.enabled = false;
        }
        if (infoText != null)
        {
            infoText.text = "select an item";
        }
    }
    private void UpdateCapacityText()
    {
        if (capacityText == null)
        {
            return;
        }
        int usedSlots = visibleItems.Count;
        if (currentPage == InventoryPage.Shipping)
        {
            usedSlots = visiblePackages.Count;
        }
        capacityText.text = usedSlots + "/" + maxSlots;
    }
    private void UpdateDeliveryButton()
    {
        bool isShipping = currentPage == InventoryPage.Shipping;
        bool isFood = currentPage == InventoryPage.Food;
        bool canFill = false;
        if (isFood == true && selectedItem != null && selectedIngredientStation != null)
        {
            canFill = selectedIngredientStation.CanFillFromInventory(selectedItem);
        }
        bool canDeliver = false;
        if (isShipping == true && deliveryInventory != null && deliveryInventory.SelectedPackage != null)
        {
            if (deliveryInventory.CanDeliverSelectedPackage == true)
            {
                canDeliver = true;
            }
        }
        bool canConsume = false;
        if (currentPage == InventoryPage.Consume && selectedItem != null && playerStamina != null &&
            selectedItem.staminaRestore > 0f && selectedItemAmount >= 1f && !playerStamina.IsFull)
        {
            canConsume = true;
        }
        if (deliverButton != null)
        {
            deliverButton.gameObject.SetActive(true);
            deliverButton.interactable = canFill || canConsume || canDeliver;
        }
        if (deliverButtonText != null)
        {
            if (isFood)
            {
                deliverButtonText.text = "fill";
            }
            else if (currentPage == InventoryPage.Consume)
            {
                deliverButtonText.text = "use";
            }
            else if (isShipping && deliveryInventory != null && deliveryInventory.SelectedPackage != null)
            {
                if (deliveryInventory.CanDeliverSelectedPackage)
                {
                    deliverButtonText.text = "deliver";
                }
                else if (deliveryInventory.SelectedPackage.CustomerNpc == null)
                {
                    deliverButtonText.text = "no destination";
                }
                else
                {
                    deliverButtonText.text = "go " + deliveryInventory.DistanceToSelectedCustomer.ToString("0.0") + "m";
                }
            }
            else if (isShipping)
            {
                deliverButtonText.text = "select package";
            }
            else
            {
                deliverButtonText.text = "go";
            }
        }
    }
    private void HandleActionButton()
    {
        if (currentPage == InventoryPage.Food)
        {
            if (selectedItem == null || selectedIngredientStation == null ||
                !selectedIngredientStation.CanFillFromInventory(selectedItem))
            {
                return;
            }
            selectedIngredientStation.FillFromInventory();
            SaveSystem.SaveGame(GameSession.Current);
            RefreshCurrentPage();
            return;
        }
        if (currentPage == InventoryPage.Consume)
        {
            if (selectedItem == null || playerStamina == null || selectedItem.staminaRestore <= 0f ||
                playerStamina.IsFull || !GameSession.Current.TryRemove(selectedItem.itemId, 1f))
            {
                return;
            }
            playerStamina.Restore(selectedItem.staminaRestore);
            SaveSystem.SaveGame(GameSession.Current);
            RefreshCurrentPage();
            return;
        }
        if (currentPage != InventoryPage.Shipping || deliveryInventory == null ||
            !deliveryInventory.TryDeliverSelectedPackage())
        {
            return;
        }
        RefreshCurrentPage();
    }
    private void HandlePackagesChanged()
    {
        if (currentPage == InventoryPage.Shipping)
        {
            RefreshCurrentPage();
        }
    }
    private void HandleSelectedPackageChanged(Order order)
    {
        selectedOrder = order;
        if (currentPage == InventoryPage.Shipping)
        {
            UpdatePackageDetails();
            RenderSlots();
            UpdateDeliveryButton();
        }
    }

    private void HandleStaminaChanged(float stamina, float maximumStamina)
    {
        if (panelOpen && currentPage == InventoryPage.Consume)
        {
            UpdateDeliveryButton();
        }
    }

    private string FormatAmount(ItemData item, float amount)
    {
        if (item != null && item.sellType == SellType.Gram)
        {
            if (amount >= 1000f)
            {
                return (amount / 1000f).ToString("0.##") + "kg";
            }
            return amount.ToString("0.##") + "g";
        }
        return "x" + amount.ToString("0.##");
    }
}
