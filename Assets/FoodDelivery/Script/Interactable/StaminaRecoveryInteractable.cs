using UnityEngine;

public class StaminaRecoveryInteractable : InteractableObject
{
    [SerializeField] private StaminaRecoveryController recoveryController;
    [SerializeField] private bool isSleepPoint;

    public override string GetInteractionPrompt()
    {
        if (isSleepPoint)
        {
            return "click to sleep";
        }
        return "click to rest";
    }

    public override void Interact()
    {
        if (recoveryController == null)
        {
            return;
        }
        if (isSleepPoint)
        {
            recoveryController.StartSleep();
        }
        else
        {
            recoveryController.StartRest();
        }
    }
}
