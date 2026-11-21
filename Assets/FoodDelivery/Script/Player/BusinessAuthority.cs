using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class BusinessAuthority : MonoBehaviour
{
    [SerializeField] private PlayerWallet playerWallet;
    [SerializeField] private TMP_Text dialogueText;
    [SerializeField] private Button registerBusinessButton;
    [SerializeField] private Button payReputationFineButton;
    [SerializeField, Min(1)] private int registrationFee = 20000;
    [SerializeField, Min(1)] private int reputationFine = 50000;

    private void Start()
    {
        if (registerBusinessButton != null)
        {
            registerBusinessButton.onClick.AddListener(RegisterBusiness);
        }
        if (payReputationFineButton != null)
        {
            payReputationFineButton.onClick.AddListener(PayReputationFine);
        }
        RefreshDialogue();
    }

    private void OnDestroy()
    {
        if (registerBusinessButton != null)
        {
            registerBusinessButton.onClick.RemoveListener(RegisterBusiness);
        }
        if (payReputationFineButton != null)
        {
            payReputationFineButton.onClick.RemoveListener(PayReputationFine);
        }
    }

    public void RefreshDialogue()
    {
        PlayerData playerData = GameSession.Current;
        UpdateButtonVisibility(playerData);

        if (!playerData.businessRegistered)
        {
            SetDialogueText("Register your business to unlock orders and selling. Fee: " + registrationFee.ToString("N0") + "đ");
            return;
        }
        if (playerData.businessSuspended)
        {
            SetDialogueText("Your business is suspended. Pay the reputation fine to reopen selling. Fee: " + reputationFine.ToString("N0") + "đ. reputation: " + playerData.reputation);
            return;
        }

        SetDialogueText("Your business is active. Reputation: " + playerData.reputation);
    }

    public void RegisterBusiness()
    {
        PlayerData playerData = GameSession.Current;
        if (playerData.businessRegistered)
        {
            RefreshDialogue();
            return;
        }
        if (playerWallet == null || !playerWallet.TrySpend(registrationFee))
        {
            SetDialogueText("Not enough money to register. Fee: " + registrationFee.ToString("N0") + "đ");
            return;
        }

        playerData.businessRegistered = true;
        SaveSystem.SaveGame(playerData);
        UpdateButtonVisibility(playerData);
        SetDialogueText("Business registered. You can now receive orders and trade.");
    }

    public void PayReputationFine()
    {
        PlayerData playerData = GameSession.Current;
        if (!playerData.businessSuspended)
        {
            RefreshDialogue();
            return;
        }
        if (playerWallet == null || !playerWallet.TrySpend(reputationFine))
        {
            SetDialogueText("Not enough money to pay the reputation fine. Fee: " + reputationFine.ToString("N0") + "đ");
            return;
        }

        playerData.businessSuspended = false;
        SaveSystem.SaveGame(playerData);
        UpdateButtonVisibility(playerData);
        SetDialogueText("Fine paid. Selling is available again. Reputation remains " + playerData.reputation + ".");
    }

    private void UpdateButtonVisibility(PlayerData playerData)
    {
        if (registerBusinessButton != null)
        {
            registerBusinessButton.gameObject.SetActive(!playerData.businessRegistered);
        }
        if (payReputationFineButton != null)
        {
            payReputationFineButton.gameObject.SetActive(playerData.businessSuspended);
        }
    }

    private void SetDialogueText(string message)
    {
        if (dialogueText != null)
        {
            dialogueText.text = message;
        }
    }
}
