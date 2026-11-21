using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerInfoUIController : MonoBehaviour
{
    [SerializeField] private PlayerWallet playerWallet;
    [SerializeField] private PlayerStamina playerStamina;
    [SerializeField] private TMP_Text moneyText;
    [SerializeField] private TMP_Text moneyChangeText;
    [SerializeField] private TMP_Text reputationText;
    [SerializeField] private Image reputationFill;
    [SerializeField] private TMP_Text staminaText;
    [SerializeField] private Image staminaFill;
    [SerializeField, Min(0.1f)] private float moneyChangeDisplayDuration = 2f;
    [SerializeField] private Color moneyGainColor = Color.green;
    [SerializeField] private Color moneySpendColor = Color.red;

    private void OnEnable()
    {
        if (playerWallet == null)
        {
            playerWallet = FindAnyObjectByType<PlayerWallet>();
        }
        if (playerStamina == null)
        {
            playerStamina = FindAnyObjectByType<PlayerStamina>();
        }
        if (playerWallet != null)
        {
            playerWallet.MoneyChange += UpdateMoney;
            playerWallet.MoneyTransaction += ShowMoneyChange;
            playerWallet.ReputationChanged += UpdateReputation;
            UpdateMoney(playerWallet.Money);
            UpdateReputation(playerWallet.Reputation);
        }
        if (playerStamina != null)
        {
            playerStamina.StaminaChanged += UpdateStamina;
            UpdateStamina(playerStamina.Current, playerStamina.Maximum);
        }
        if (moneyChangeText != null)
        {
            moneyChangeText.gameObject.SetActive(false);
        }
    }

    private void OnDisable()
    {
        CancelInvoke(nameof(HideMoneyChange));
        if (playerWallet != null)
        {
            playerWallet.MoneyChange -= UpdateMoney;
            playerWallet.MoneyTransaction -= ShowMoneyChange;
            playerWallet.ReputationChanged -= UpdateReputation;
        }
        if (playerStamina != null)
        {
            playerStamina.StaminaChanged -= UpdateStamina;
        }
    }

    private void UpdateMoney(int money)
    {
        if (moneyText != null)
        {
            moneyText.text = money.ToString("N0");
        }
    }

    private void ShowMoneyChange(int amount)
    {
        if (moneyChangeText == null)
        {
            return;
        }

        if (amount > 0)
        {
            moneyChangeText.text = "+" + amount.ToString("N0");
            moneyChangeText.color = moneyGainColor;
        }
        else
        {
            moneyChangeText.text = amount.ToString("N0");
            moneyChangeText.color = moneySpendColor;
        }

        moneyChangeText.gameObject.SetActive(true);
        CancelInvoke(nameof(HideMoneyChange));
        Invoke(nameof(HideMoneyChange), moneyChangeDisplayDuration);
    }

    private void HideMoneyChange()
    {
        if (moneyChangeText != null)
        {
            moneyChangeText.gameObject.SetActive(false);
        }
    }

    private void UpdateReputation(int reputation)
    {
        if (reputationText != null)
        {
            reputationText.text = reputation + "/50";
        }
        if (reputationFill != null)
        {
            reputationFill.fillAmount = Mathf.Clamp01(reputation / 50f);
        }
    }

    private void UpdateStamina(float stamina, float maximumStamina)
    {
        if (staminaText != null)
        {
            staminaText.text = Mathf.CeilToInt(stamina) + "/" + Mathf.CeilToInt(maximumStamina);
        }
        if (staminaFill != null && maximumStamina > 0f)
        {
            staminaFill.fillAmount = Mathf.Clamp01(stamina / maximumStamina);
        }
    }
}
