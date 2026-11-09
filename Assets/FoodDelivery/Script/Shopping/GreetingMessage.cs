using TMPro;
using UnityEngine;

public class GreetingMessage : MonoBehaviour
{
    [Header("UI Reference")]
    public GameObject speechBubble;
    public TextMeshProUGUI dialogueText;

    [Header("Content")]
    [TextArea]
    public string greetingMessage = "We've got fresh fruits today, what can I get you?";

    private void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            speechBubble.SetActive(true);
            dialogueText.text = greetingMessage;
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            speechBubble.SetActive(false);
        }
    }
}
