using UnityEngine;
using UnityEngine.InputSystem;

public class ShopOpener : MonoBehaviour
{
    [Header("UI")]
    [SerializeField] private GameObject shopRoot;
    [SerializeField] private GameObject crosshair;

    [Header("Interact")]
    [SerializeField] private Camera playerCamera;
    [SerializeField] private float interactDistance = 10f;
    [SerializeField] private LayerMask raycastMask = ~0;

    [Header("Lock the control when open")]
    [SerializeField] private MonoBehaviour[] disableWhileOpen;

    [Header("Process payment")]
    [SerializeField] private CartManager cartManager;
    [SerializeField] private NPCGiveItem npc;
    private bool purchasedThisPhase;
    public static bool AnyOpen;
    public static int LastCloseFrame = -1;

    private bool isOpen;
    public bool IsOpen
    {
        get
        {
            return isOpen;
        }
        private set
        {
            isOpen = value;
        }
    }

    private CursorLockMode previousLockState;
    private bool previousCursorVisible;


    private void Awake()
    {
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }
        if (shopRoot != null)
        {
            shopRoot.SetActive(false);
        }
        AnyOpen = false;
    }
    private void Update()
    {
        if (PauseMenuUI.IsPaused) {
            return;
        }
        if (IsOpen)
        {
            if (EscapePressed())
            {
                Close();
            }
            return;
        }
        if (ClickPressed())
        {
            if (IsLookingAtThisShop())
            {
                Open();
            }
        }
    }
    private void HandleCheckout()
    {
        purchasedThisPhase = true;
    }
    private bool IsLookingAtThisShop()
    {
        if (playerCamera == null)
        {
            return false;
        }
        Vector3 screenPoint;
        if (Cursor.lockState == CursorLockMode.Locked)
        {
            screenPoint = new Vector3(Screen.width / 2f, Screen.height / 2f, 0f);
        }
        else
        {
            screenPoint = PointerPosition();
        }
        Ray ray = playerCamera.ScreenPointToRay(screenPoint);
        RaycastHit hit;
        bool hitSomething = Physics.Raycast(ray, out hit, interactDistance, raycastMask, QueryTriggerInteraction.Ignore);
        if (hitSomething)
        {
            ShopOpener shop = hit.collider.GetComponentInParent<ShopOpener>();
            if (shop == this)
            {
                return true;
            }
        }
        return false;
    }
    public void Open()
    {
        if (IsOpen)
        {
            return;
        }
        IsOpen = true;
        AnyOpen = true;
        previousLockState = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        shopRoot.SetActive(true);
        if (crosshair != null)
        {
            crosshair.SetActive(false);
        }
        SetControlsEnabled(false);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        purchasedThisPhase = false;
        cartManager.OnCheckout += HandleCheckout;
    }
    public void Close()
    {
        if (!IsOpen)
        {
            return;
        }
        IsOpen = false;
        AnyOpen = false;
        LastCloseFrame = Time.frameCount;
        shopRoot.SetActive(false);
        if (crosshair != null)
        {
            crosshair.SetActive(true);
        }
        SetControlsEnabled(true);
        Cursor.lockState = previousLockState;
        Cursor.visible = previousCursorVisible;
        cartManager.OnCheckout -= HandleCheckout;
        if (purchasedThisPhase == true && npc != null)
        {
            npc.GiveItem();
            purchasedThisPhase = false;
        }
        
    }
    private void SetControlsEnabled(bool enabled)
    {
        foreach (MonoBehaviour behaviour in disableWhileOpen)
        {
            if (behaviour != null)
            {
                behaviour.enabled = enabled;
            }
        }
    }
    private static bool ClickPressed()
    {
        if (Mouse.current != null)
        {
            return Mouse.current.leftButton.wasPressedThisFrame;
        }

        return false;
    }
    private static bool EscapePressed()
    {
        if (Keyboard.current != null)
        {
            return Keyboard.current.escapeKey.wasPressedThisFrame;
        }

        return false;
    }
    private static Vector2 PointerPosition()
    {
        if (Mouse.current != null)
        {
            return Mouse.current.position.ReadValue();
        }

        return Vector2.zero;
    }
}