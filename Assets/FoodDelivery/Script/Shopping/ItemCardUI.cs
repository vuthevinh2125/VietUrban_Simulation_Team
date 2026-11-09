using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
public class ItemCardUI : MonoBehaviour
{
    [Header("Information")]
    [SerializeField] private Image iconImage;
    [SerializeField] private TMP_Text nameText;
    [SerializeField] private TMP_Text priceText;
    [SerializeField] private TMP_Text descriptionText;

    [Header("Quantity (Piece)")]
    [SerializeField] private GameObject quantityGroup;
    [SerializeField] private TMP_Text quantityValueText;
    [SerializeField] private Button decreaseButton;
    [SerializeField] private Button increaseButton;

    [Header("Quantity (Gram)")]
    [SerializeField] private GameObject sliderGroup;
    [SerializeField] private Slider gramSlider;
    [SerializeField] private TMP_Text gramValueText;

    [Header("Action")]
    [SerializeField] private Button addButton;
    public event Action<ItemData, float> OnAddToCart;
    private ItemData _data;
    private float _currentAmount;
    private void Awake()
    {
        decreaseButton.onClick.AddListener(DecreaseQuantity);
        increaseButton.onClick.AddListener(IncreaseQuantity);
        gramSlider.onValueChanged.AddListener(OnSliderChanged);
        addButton.onClick.AddListener(HandleAddClicked);
    }
    private  void DecreaseQuantity()
    {
        ChangeQuantity(-1);
    }
    private void IncreaseQuantity()
    {
        ChangeQuantity(1);
    }
    public void Setup(ItemData data)
    {
        _data = data;       
        nameText.text = data.itemName;
        descriptionText.text = data.description;
        iconImage.sprite = data.icon;
        bool isGram = false;
        if (data.sellType == SellType.Gram)
        {
            isGram = true;
        }
        sliderGroup.SetActive(isGram);
        quantityGroup.SetActive(!isGram);
        _currentAmount = 0;
        if (isGram)
        {
            gramSlider.minValue = 0;
            gramSlider.maxValue = Mathf.Max(1, data.limitPerBuying);
            gramSlider.SetValueWithoutNotify(0);
            UpdateGramDisplay();
            priceText.text = $"Price: {data.basePriceBuy:N0}/gram";
        }
        else
        {
            quantityValueText.text = "0";
            priceText.text = $"Price: {data.basePriceBuy:N0}/piece";
        }
    }
    private void ChangeQuantity(int delta)
    {
        float max;
        if (_data.limitPerBuying > 0)
        {
            max = _data.limitPerBuying;
        }
        else
        {
            max = float.MaxValue;
        }
        _currentAmount = Mathf.Clamp(_currentAmount + delta, 0, max);
        quantityValueText.text = _currentAmount.ToString("0");
    }
    private void OnSliderChanged(float value)
    {
        _currentAmount = value;

        UpdateGramDisplay();
    }
    private void UpdateGramDisplay()
    {
        gramValueText.text = _currentAmount.ToString("0") + "g";
    }
    private void HandleAddClicked()
    {
        if (_currentAmount <= 0)
        {
            return;
        }

        if (_data == null)
        {
            return;
        }
        if (OnAddToCart != null)
        {
            OnAddToCart(_data, _currentAmount);
        }
        _currentAmount = 0;
        if (_data.sellType == SellType.Gram)
        {
            gramSlider.SetValueWithoutNotify(0);
        }
        else
        {
            quantityValueText.text = "0";
        }
        UpdateGramDisplay();
    }
}