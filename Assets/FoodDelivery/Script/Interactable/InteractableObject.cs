using UnityEngine;

public class InteractableObject : MonoBehaviour
{
    public string ItemName;
    public string GetItemName()
    {
        return ItemName;
    }

    public virtual string GetInteractionPrompt()
    {
        return ItemName;
    }

    public virtual bool HandleInteractionKey(KeyCode key)
    {
        return false;
    }

    public virtual void Interact()
    {
        Debug.Log("clicked");
    }
}
