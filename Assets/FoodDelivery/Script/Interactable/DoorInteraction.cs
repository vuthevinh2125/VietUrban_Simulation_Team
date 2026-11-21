using System.Collections;
using UnityEngine;

public class DoorInteraction : MonoBehaviour
{
    [SerializeField] private float openAngle = 90f;
    [SerializeField] private AudioSource audioSource;
    [SerializeField] private AudioClip interactAudio;
    private float openSpeed = 2f;
    private float interactDistance = 10f;
    private bool isOpen = false;
    private Quaternion _closedRotation;
    private Quaternion _openRotation;
    private Coroutine _currentCoroutine;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        audioSource = GetComponent<AudioSource>();  
        _closedRotation = transform.rotation;
        _openRotation = Quaternion.Euler(transform.eulerAngles + new Vector3(0, openAngle, 0));

    }

    // Update is called once per frame
    void Update()
    {
        
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, interactDistance))
        {
            if (hit.transform == transform)
            {
                if (Input.GetKeyDown(KeyCode.Mouse0))
                {
                    if (_currentCoroutine != null)
                    {
                        StopCoroutine(_currentCoroutine);
                    }
                    _currentCoroutine = StartCoroutine(ToggleDoor());
                }
            }
        }

    }
    private IEnumerator ToggleDoor()
    {
        isOpen = !isOpen;
        audioSource.PlayOneShot(interactAudio);
        Quaternion targetRotation;
        if (isOpen)
        {
            targetRotation = _openRotation;
        }
        else
        {
            targetRotation = _closedRotation;
        }
        while (Quaternion.Angle(transform.rotation, targetRotation) > 0.01f)
        {
            transform.rotation = Quaternion.Lerp(transform.rotation, targetRotation, Time.deltaTime * openSpeed);
            yield return null;
        }
        transform.rotation = targetRotation;
    }
}
