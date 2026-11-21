using UnityEngine;
using TMPro;
using UnityEngine.EventSystems;

public class SelectionManager : MonoBehaviour
{
    public GameObject interaction_Info_UI;
    private TextMeshProUGUI interaction_text;
    [SerializeField] private float interactDistance = 3f;
    private InteractableObject currentInteractable = null;
    private bool interactionEnabled = true;

    public InteractableObject CurrentInteractable
    {
        get { return currentInteractable; }
    }

    public bool InteractionEnabled
    {
        get { return interactionEnabled; }
    }

    public void SetInteractionEnabled(bool enabled)
    {
        interactionEnabled = enabled;
        if (enabled == false)
        {
            HideInteractionPrompt();
        }
    }

    public void HideInteractionPrompt()
    {
        currentInteractable = null;
        if (interaction_Info_UI != null)
        {
            interaction_Info_UI.SetActive(false);
        }
    }

    private void Start()
    {
        interaction_text = interaction_Info_UI.GetComponent<TextMeshProUGUI>();
    }

    void Update()
    {
        if (interactionEnabled == false)
        {
            HideInteractionPrompt();
            return;
        }

        Ray ray = Camera.main.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0)); //camera.main: property built in tra ve main camera
                                                                    //input.mouseposition: vi tri chuot tren man hinh, dang vector 3
                                                                    //Camera.main.ScreenPointToRay: ham chuyen doi 1 diem 2d tren man hinh thanh 1 ray trong 3d, bat dau tu
                                                                    //camera  va xuyen qua dung diem do tren man hinh
                                                                   
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, interactDistance)) //ham Physics.Raycast ban ray ra va tra ve true/false, neu true thi ghi thong tin va cham vao hit.
        {
            InteractableObject interactable = hit.collider.GetComponentInParent<InteractableObject>();
            if (interactable != null)
            {
                interaction_text.text = interactable.GetInteractionPrompt();
                currentInteractable = interactable;
                bool pointerOverUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
                if (Input.GetMouseButtonDown(0) && interactionEnabled && !pointerOverUi)
                {
                    currentInteractable.Interact();
                }
                interaction_Info_UI.SetActive(true);
            }
            else
            {
                interaction_Info_UI.SetActive(false);
                currentInteractable = null;
            }

        }
        else
        {
            interaction_Info_UI.SetActive(false);
            currentInteractable = null;
        }

    }

    
}
        
 
