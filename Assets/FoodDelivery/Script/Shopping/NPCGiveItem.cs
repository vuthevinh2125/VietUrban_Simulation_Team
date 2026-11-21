using UnityEngine;

public class NPCGiveItem : MonoBehaviour

    
{
    [SerializeField] private Animator givingAnimation;
    [SerializeField] private GameObject shoppingBag;
    public void GiveItem()
    {
        givingAnimation.SetTrigger("GiveItem");
    }
    public void ShowBag()
    {
        shoppingBag.SetActive(true);
    }
    public void HideBag()
    {
        shoppingBag.SetActive(false);
    }
}
