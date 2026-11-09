using UnityEngine;

public class PhoneUIController : MonoBehaviour
{
    [SerializeField] private GameObject phoneRoot;
    [SerializeField] private GameObject homeScreen;
    [SerializeField] private GameObject appGrid;
    [SerializeField] private GameObject vietUrbanFoodScreen;
    [SerializeField] private GameObject crosshair;
    [SerializeField] private KeyCode toggleKey = KeyCode.O;

    private bool isPhoneOpen;
    private bool previousCrosshairActive;
    private CursorLockMode previousCursorLockMode;
    private bool previousCursorVisible;

    private void Start()
    {
        isPhoneOpen = false;
        if (phoneRoot != null)
        {
            phoneRoot.SetActive(false);
        }
        if (homeScreen != null)
        {
            homeScreen.SetActive(true);
        }
        if (appGrid != null)
        {
            appGrid.SetActive(true);
        }
        if (vietUrbanFoodScreen != null)
        {
            vietUrbanFoodScreen.SetActive(false);
        }
    }

    private void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            TogglePhone();
        }
    }

    private void OnDestroy()
    {
        if (isPhoneOpen)
        {
            Cursor.lockState = previousCursorLockMode;
            Cursor.visible = previousCursorVisible;
        }
    }

    public void TogglePhone()
    {
        SetPhoneOpen(!isPhoneOpen);
    }

    public void OpenVietUrbanFood()
    {
        if (!isPhoneOpen)
        {
            return;
        }

        if (homeScreen != null)
        {
            homeScreen.SetActive(false);
        }
        if (appGrid != null)
        {
            appGrid.SetActive(false);
        }
        if (vietUrbanFoodScreen != null)
        {
            vietUrbanFoodScreen.SetActive(true);
        }
    }

    public void ReturnToHomeScreen()
    {
        if (homeScreen != null)
        {
            homeScreen.SetActive(true);
        }
        if (appGrid != null)
        {
            appGrid.SetActive(true);
        }
        if (vietUrbanFoodScreen != null)
        {
            vietUrbanFoodScreen.SetActive(false);
        }
    }

    public void SetPhoneOpen(bool open)
    {
        if (phoneRoot == null || isPhoneOpen == open)
        {
            return;
        }

        if (open)
        {
            previousCursorLockMode = Cursor.lockState;
            previousCursorVisible = Cursor.visible;
            if (crosshair != null)
            {
                previousCrosshairActive = crosshair.activeSelf;
                crosshair.SetActive(false);
            }
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        else
        {
            Cursor.lockState = previousCursorLockMode;
            Cursor.visible = previousCursorVisible;
            if (crosshair != null)
            {
                crosshair.SetActive(previousCrosshairActive);
            }
        }

        isPhoneOpen = open;
        phoneRoot.SetActive(open);
    }
}
