using UnityEngine;

public class InteractableObject : MonoBehaviour
{
    public string ItemName;
    public string GetItemName()
    {
        return ItemName;
    }

    public virtual void Interact()
    {
        Debug.Log("clicked");
    }
}
