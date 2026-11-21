using System;
using UnityEngine;

public class PlayerStamina : MonoBehaviour
{
    [SerializeField] private StaminaConfig staminaConfig;
    [SerializeField] private TimeController timeController;
    [SerializeField] private OrderManager orderManager;
    [SerializeField] private DeliveryInventory deliveryInventory;
    [SerializeField] private CharacterController playerCharacterController;
    [SerializeField] private StarterAssets.StarterAssetsInputs playerInputs;

    private DateTime lastGameTime = DateTime.MinValue;
    private bool recovering;
    private bool sleeping;

    public event Action<float, float> StaminaChanged;

    public float Current
    {
        get { return GameSession.Current.stamina; }
    }

    public float Maximum
    {
        get
        {
            if (staminaConfig == null)
            {
                return 100f;
            }
            return staminaConfig.maximumStamina;
        }
    }

    public float SleepTimeMultiplier
    {
        get
        {
            if (staminaConfig == null)
            {
                return 1f;
            }
            return staminaConfig.sleepTimeMultiplier;
        }
    }

    public bool IsFull
    {
        get { return Current >= Maximum; }
    }

    private void Awake()
    {
        if (timeController == null)
        {
            timeController = FindAnyObjectByType<TimeController>();
        }
        if (orderManager == null)
        {
            orderManager = FindAnyObjectByType<OrderManager>();
        }
        if (deliveryInventory == null)
        {
            deliveryInventory = FindAnyObjectByType<DeliveryInventory>();
        }
        if (playerCharacterController == null)
        {
            playerCharacterController = FindAnyObjectByType<CharacterController>();
        }
        if (playerInputs == null && playerCharacterController != null)
        {
            playerInputs = playerCharacterController.GetComponent<StarterAssets.StarterAssetsInputs>();
        }
        ClampCurrentStamina();
    }

    private void Start()
    {
        RaiseStaminaChanged();
    }

    private void Update()
    {
        if (timeController == null || staminaConfig == null)
        {
            return;
        }

        DateTime currentGameTime = timeController.CurrentTime;
        if (currentGameTime == DateTime.MinValue)
        {
            return;
        }
        if (lastGameTime == DateTime.MinValue)
        {
            lastGameTime = currentGameTime;
            return;
        }

        double elapsedGameMinutes = (currentGameTime - lastGameTime).TotalMinutes;
        lastGameTime = currentGameTime;
        if (elapsedGameMinutes <= 0d)
        {
            return;
        }

        float elapsedMinutes = (float)elapsedGameMinutes;
        if (recovering)
        {
            float recoveryPerMinute = staminaConfig.restingRecoveryPerGameMinute;
            if (sleeping)
            {
                recoveryPerMinute = staminaConfig.sleepingRecoveryPerGameMinute;
            }
            ChangeStamina(recoveryPerMinute * elapsedMinutes);
            return;
        }

        float drainPerMinute = GetActiveDrainRate();
        if (drainPerMinute > 0f)
        {
            ChangeStamina(-drainPerMinute * elapsedMinutes);
        }
    }

    public float Restore(float amount)
    {
        if (amount <= 0f || Current >= Maximum)
        {
            return 0f;
        }

        float previousStamina = Current;
        ChangeStamina(amount);
        return Current - previousStamina;
    }

    public void StartRecovery(bool sleep)
    {
        recovering = true;
        sleeping = sleep;
        lastGameTime = DateTime.MinValue;
    }

    public void StopRecovery()
    {
        recovering = false;
        sleeping = false;
        lastGameTime = DateTime.MinValue;
    }

    private float GetActiveDrainRate()
    {
        float drainPerMinute = 0f;

        if (orderManager != null && orderManager.SelectedOrder != null &&
            orderManager.SelectedOrder.OrderStatus == Order.OrderState.Making)
        {
            drainPerMinute += staminaConfig.cookingDrainPerGameMinute;
        }

        if (HasPackageAndIsMoving())
        {
            drainPerMinute += staminaConfig.shippingDrainPerGameMinute;
        }

        if (IsMoving())
        {
            bool isSprinting = playerInputs != null && playerInputs.sprint;
            drainPerMinute += isSprinting
                ? staminaConfig.sprintingDrainPerGameMinute
                : staminaConfig.walkingDrainPerGameMinute;
        }

        return drainPerMinute;
    }

    private bool IsMoving()
    {
        if (playerCharacterController == null)
        {
            return false;
        }

        Vector3 horizontalVelocity = playerCharacterController.velocity;
        horizontalVelocity.y = 0f;
        return horizontalVelocity.sqrMagnitude > 0.01f;
    }

    private bool HasPackageAndIsMoving()
    {
        if (deliveryInventory == null || deliveryInventory.Packages.Count == 0 || playerCharacterController == null)
        {
            return false;
        }

        return IsMoving();
    }

    private void ChangeStamina(float amount)
    {
        PlayerData playerData = GameSession.Current;
        float previousStamina = playerData.stamina;
        playerData.stamina = Mathf.Clamp(previousStamina + amount, 0f, Maximum);
        if (Mathf.Approximately(previousStamina, playerData.stamina))
        {
            return;
        }

        RaiseStaminaChanged();
        if (playerData.stamina <= 0f)
        {
            GameSession.RestartCurrentScene();
        }
    }

    private void ClampCurrentStamina()
    {
        PlayerData playerData = GameSession.Current;
        playerData.stamina = Mathf.Clamp(playerData.stamina, 0f, Maximum);
    }

    private void RaiseStaminaChanged()
    {
        if (StaminaChanged != null)
        {
            StaminaChanged(Current, Maximum);
        }
    }
}
