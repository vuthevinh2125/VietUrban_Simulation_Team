using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Hiện giỏ hàng ngay trên nút Buy của máy tính: "Buy" khi giỏ trống, "Buy" + "3 boxes · $48.00" khi có hàng
/// (chữ đỏ khi không đủ tiền). Chuột phải vào nút Buy để bỏ hết hàng trong giỏ.
/// Trước: giỏ hàng (kể cả hàng thêm từ tab Advisor) không hiện ở đâu, nên bấm Buy với 1 thùng $24 lại báo
/// "Not enough money! Need $2,336" mà người chơi không biết giỏ còn hàng cũ.
/// </summary>
public class CartBadge : MonoBehaviour, IPointerClickHandler
{
    private static readonly Color Gold = new Color(1f, 0.84f, 0.3f), Short = new Color(1f, 0.5f, 0.45f);
    private TMP_Text label;

    public static void Attach(Button buy)
    {
        if (buy != null && buy.GetComponent<CartBadge>() == null) buy.gameObject.AddComponent<CartBadge>();
    }

    void Awake() => label = GetComponentInChildren<TMP_Text>(true);

    void OnEnable()
    {
        MarketManager.OnCartChanged += Refresh;
        EconomyManager.OnMoneyChanged += OnMoney;
        Refresh();
    }

    void OnDisable()
    {
        MarketManager.OnCartChanged -= Refresh;
        EconomyManager.OnMoneyChanged -= OnMoney;
    }

    private void OnMoney(float _) => Refresh();

    private void Refresh()
    {
        if (label == null) return;
        MarketManager m = MarketManager.Instance;
        if (m == null || m.CartBoxCount <= 0) { label.text = "Buy"; return; }
        bool afford = EconomyManager.Instance == null || EconomyManager.Instance.CanAfford(m.CartTotal);
        string hex = ColorUtility.ToHtmlStringRGB(afford ? Gold : Short);
        label.text = $"Buy\n<size=55%><color=#{hex}>{m.CartBoxCount} box{(m.CartBoxCount > 1 ? "es" : "")} · {EconomyManager.Format(m.CartTotal)}</color></size>";
    }

    public void OnPointerClick(PointerEventData e)
    {
        if (e.button != PointerEventData.InputButton.Right) return;
        MarketManager m = MarketManager.Instance;
        if (m == null || m.CartBoxCount <= 0) return;
        m.ClearCart();
        ProgressHUD.Toast("Cart emptied.");
    }
}
