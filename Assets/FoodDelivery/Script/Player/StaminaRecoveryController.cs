using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class StaminaRecoveryController : MonoBehaviour
{
    [SerializeField] private PlayerStamina playerStamina;
    [SerializeField] private GameObject recoveryPanelRoot;
    [SerializeField] private TMP_Text recoveryStatusText;
    [SerializeField] private Button wakeButton;
    [SerializeField] private GameObject crosshair;
    [SerializeField] private SelectionManager selectionManager;
    [SerializeField] private MonoBehaviour[] gameplayInputComponents;
    [SerializeField] private KeyCode wakeKey = KeyCode.E;

    private bool recovering;
    private bool previousCursorVisible;
    private bool previousCrosshairActive;
    private bool previousInteractionEnabled;
    private CursorLockMode previousCursorLockMode;
    private float previousTimeScale;
    private bool[] previousComponentStates;

    private void Awake()
    {
        if (playerStamina == null)
        {
            playerStamina = FindAnyObjectByType<PlayerStamina>();
        }
        if (selectionManager == null)
        {
            selectionManager = FindAnyObjectByType<SelectionManager>();
        }
        if (recoveryPanelRoot != null)
        {
            recoveryPanelRoot.SetActive(false);
        }
    }

    private void Start()
    {
        if (wakeButton != null)
        {
            wakeButton.onClick.AddListener(WakeUp);
        }
    }

    private void Update()
    {
        if (recovering && Input.GetKeyDown(wakeKey))
        {
            WakeUp();
        }
    }

    private void OnDestroy()
    {
        if (wakeButton != null)
        {
            wakeButton.onClick.RemoveListener(WakeUp);
        }
        if (recovering)
        {
            WakeUp();
        }
    }

    public void StartRest()
    {
        BeginRecovery(false);
    }

    public void StartSleep()
    {
        BeginRecovery(true);
    }

    public void WakeUp()
    {
        if (!recovering)
        {
            return;
        }

        recovering = false;
        if (playerStamina != null)
        {
            playerStamina.StopRecovery();
        }
        Time.timeScale = previousTimeScale;
        SaveSystem.SaveGame(GameSession.Current);
        if (recoveryPanelRoot != null)
        {
            recoveryPanelRoot.SetActive(false);
        }

        RestoreGameplayInput();
        if (selectionManager != null)
        {
            selectionManager.SetInteractionEnabled(previousInteractionEnabled);
        }
        if (crosshair != null)
        {
            crosshair.SetActive(previousCrosshairActive);
        }
        Cursor.lockState = previousCursorLockMode;
        Cursor.visible = previousCursorVisible;
    }

    private void BeginRecovery(bool sleep)
    {
        if (recovering || playerStamina == null)
        {
            return;
        }

        recovering = true;
        previousTimeScale = Time.timeScale;
        previousCursorLockMode = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        if (crosshair != null)
        {
            previousCrosshairActive = crosshair.activeSelf;
            crosshair.SetActive(false);
        }
        if (selectionManager != null)
        {
            previousInteractionEnabled = selectionManager.InteractionEnabled;
            selectionManager.SetInteractionEnabled(false);
        }

        DisableGameplayInput();
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        if (recoveryPanelRoot != null)
        {
            recoveryPanelRoot.SetActive(true);
        }
        if (recoveryStatusText != null)
        {
            if (sleep)
            {
                recoveryStatusText.text = "sleeping - press e or wake up when ready";
            }
            else
            {
                recoveryStatusText.text = "resting - press e or wake up when ready";
            }
        }

        playerStamina.StartRecovery(sleep);
        if (sleep)
        {
            Time.timeScale = previousTimeScale * playerStamina.SleepTimeMultiplier;
        }
    }

    private void DisableGameplayInput()
    {
        int componentCount = 0;
        if (gameplayInputComponents != null)
        {
            componentCount = gameplayInputComponents.Length;
        }
        previousComponentStates = new bool[componentCount];
        for (int index = 0; index < componentCount; index++)
        {
            MonoBehaviour component = gameplayInputComponents[index];
            if (component != null)
            {
                previousComponentStates[index] = component.enabled;
                component.enabled = false;
            }
        }
    }

    private void RestoreGameplayInput()
    {
        if (gameplayInputComponents == null || previousComponentStates == null)
        {
            return;
        }
        for (int index = 0; index < gameplayInputComponents.Length; index++)
        {
            MonoBehaviour component = gameplayInputComponents[index];
            if (component != null)
            {
                component.enabled = previousComponentStates[index];
            }
        }
    }
}
