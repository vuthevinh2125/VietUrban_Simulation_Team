using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LeaderboardRowUI : MonoBehaviour
{
    [SerializeField] private Image rowBackground;
    [SerializeField] private TMP_Text rankText;
    [SerializeField] private Image medal;
    [SerializeField] private TMP_Text playerNameText;
    [SerializeField] private TMP_Text moneyText;
    [SerializeField] private Sprite goldRow;
    [SerializeField] private Sprite silverRow;
    [SerializeField] private Sprite bronzeRow;
    [SerializeField] private Sprite normalRow;
    [SerializeField] private Sprite goldMedal;
    [SerializeField] private Sprite silverMedal;
    [SerializeField] private Sprite bronzeMedal;

    public void ConfigureSprites(Sprite goldRowSprite, Sprite silverRowSprite, Sprite bronzeRowSprite, Sprite normalRowSprite,
        Sprite goldMedalSprite, Sprite silverMedalSprite, Sprite bronzeMedalSprite)
    {
        goldRow = goldRowSprite; silverRow = silverRowSprite; bronzeRow = bronzeRowSprite; normalRow = normalRowSprite;
        goldMedal = goldMedalSprite; silverMedal = silverMedalSprite; bronzeMedal = bronzeMedalSprite;
    }

    public void SetData(int rank, string playerName, int money, bool preserveBackground = false)
    {
        if (rowBackground == null) rowBackground = GetComponent<Image>();
        if (rankText == null) rankText = transform.Find("RankingGroup/RankText")?.GetComponent<TMP_Text>();
        if (medal == null) medal = transform.Find("RankingGroup/Medal")?.GetComponent<Image>();
        if (playerNameText == null) playerNameText = transform.Find("NameText")?.GetComponent<TMP_Text>();
        if (moneyText == null) moneyText = transform.Find("MoneyText")?.GetComponent<TMP_Text>();

        Sprite rowSprite = rank == 1 ? goldRow : rank == 2 ? silverRow : rank == 3 ? bronzeRow : normalRow;
        Sprite medalSprite = rank == 1 ? goldMedal : rank == 2 ? silverMedal : rank == 3 ? bronzeMedal : null;
        if (!preserveBackground && rowBackground != null && rowSprite != null) rowBackground.sprite = rowSprite;
        if (rankText != null) rankText.text = rank.ToString();
        if (medal != null)
        {
            medal.gameObject.SetActive(medalSprite != null);
            if (medalSprite != null) medal.sprite = medalSprite;
        }
        if (playerNameText != null) playerNameText.text = string.IsNullOrWhiteSpace(playerName) ? "Player" : playerName;
        if (moneyText != null) moneyText.text = money.ToString("N0") + "đ";
    }
}
