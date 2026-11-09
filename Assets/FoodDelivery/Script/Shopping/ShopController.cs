using System.Collections.Generic;
using UnityEngine;
public class ShopController : MonoBehaviour
{
    [SerializeField] private List<ItemData> shopItems;
    [SerializeField] private ItemCardUI cardPrefab;
    [SerializeField] private Transform cardsParent;
    [SerializeField] private CartManager cartManager;
    private void Start()
    {
        foreach (ItemData data in shopItems)
        {
            ItemCardUI card = Instantiate(cardPrefab, cardsParent);
            card.Setup(data);
            card.OnAddToCart += HandleAddToCart;
        }
    }
    private void HandleAddToCart(ItemData item, float amount)
    {
        cartManager.TryAddItem(item, amount);
    }
}