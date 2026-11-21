using UnityEngine;

[CreateAssetMenu(fileName = "StaminaConfig", menuName = "Game/Stamina Config")]
public class StaminaConfig : ScriptableObject
{
    [Min(1f)] public float maximumStamina = 100f;
    [Min(0f)] public float walkingDrainPerGameMinute = 0.15f;
    [Min(0f)] public float sprintingDrainPerGameMinute = 0.3f;
    [Min(0f)] public float cookingDrainPerGameMinute = 0.5f;
    [Min(0f)] public float shippingDrainPerGameMinute = 0.5f;
    [Min(0f)] public float restingRecoveryPerGameMinute = 1.5f;
    [Min(0f)] public float sleepingRecoveryPerGameMinute = 3f;
    [Min(1f)] public float sleepTimeMultiplier = 8;
}
