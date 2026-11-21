using UnityEngine;

public class PoliceDialogueInteractable : InteractableObject
{
    [SerializeField] private GameObject dialoguePanelRoot;
    [SerializeField] private GameObject crosshair;
    [SerializeField] private SelectionManager selectionManager;
    [SerializeField] private BusinessAuthority businessAuthority;
    [SerializeField] private MonoBehaviour[] gameplayInputComponents;

    private bool dialogueOpen;
    private CursorLockMode previousCursorLockMode;
    private bool previousCursorVisible;
    private bool previousCrosshairActive;
    private bool previousInteractionEnabled;
    private bool[] previousComponentStates;

    private void Awake()
    {
        if (dialoguePanelRoot != null)
        {
            dialoguePanelRoot.SetActive(false);
        }
    }

    public override string GetInteractionPrompt()
    {
        return "Click to talk with police";
    }

    public override void Interact()
    {
        OpenDialogue();
    }

    public void OpenDialogue()
    {
        if (dialogueOpen || dialoguePanelRoot == null)
        {
            return;
        }

        dialogueOpen = true;
        previousCursorLockMode = Cursor.lockState;
        previousCursorVisible = Cursor.visible;
        if (crosshair != null)
        {
            previousCrosshairActive = crosshair.activeSelf;
            crosshair.SetActive(false);
        }

        if (selectionManager != null)
        {
            previousInteractionEnabled = selectionManager.InteractionEnabled;
            selectionManager.SetInteractionEnabled(false);
        }

        int componentCount = 0;
        if (gameplayInputComponents != null)
        {
            componentCount = gameplayInputComponents.Length;
        }
        previousComponentStates = new bool[componentCount];
        for (int index = 0; index < componentCount; index++)
        {
            MonoBehaviour component = gameplayInputComponents[index];
            if (component != null)
            {
                previousComponentStates[index] = component.enabled;
                component.enabled = false;
            }
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        dialoguePanelRoot.SetActive(true);
        if (businessAuthority != null)
        {
            businessAuthority.RefreshDialogue();
        }
    }

    public void CloseDialogue()
    {
        if (!dialogueOpen)
        {
            return;
        }

        dialogueOpen = false;
        dialoguePanelRoot.SetActive(false);

        if (gameplayInputComponents != null && previousComponentStates != null)
        {
            for (int index = 0; index < gameplayInputComponents.Length; index++)
            {
                MonoBehaviour component = gameplayInputComponents[index];
                if (component != null)
                {
                    component.enabled = previousComponentStates[index];
                }
            }
        }

        if (selectionManager != null)
        {
            selectionManager.SetInteractionEnabled(previousInteractionEnabled);
        }

        if (crosshair != null)
        {
            crosshair.SetActive(previousCrosshairActive);
        }
        Cursor.lockState = previousCursorLockMode;
        Cursor.visible = previousCursorVisible;
    }

    private void OnDisable()
    {
        if (dialogueOpen)
        {
            CloseDialogue();
        }
    }
}
