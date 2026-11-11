using UnityEngine;

public class CarHorn : MonoBehaviour
{
    public AudioSource audioSource;
    public AudioClip hornSound;
    public KeyCode hornKey = KeyCode.C;

    void Update()
    {
        if (Input.GetKeyDown(hornKey))
        {
            if (audioSource != null && hornSound != null)
            {
                audioSource.PlayOneShot(hornSound);
            }
        }
    }
}