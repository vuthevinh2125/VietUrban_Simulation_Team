using UnityEngine;

public class CarIndicatorSound : MonoBehaviour
{
    [Header("Audio References")]
    public AudioSource audioSource;
    public AudioClip clickSound;

    [Header("Input Settings")]
    public KeyCode leftKey = KeyCode.Q;   // Phím Q cho xi nhan trái
    public KeyCode rightKey = KeyCode.E;  // Phím E cho xi nhan phải

    void Start()
    {
        if (audioSource == null)
        {
            audioSource = GetComponent<AudioSource>();
            if (audioSource == null)
            {
                audioSource = gameObject.AddComponent<AudioSource>();
            }
        }

        audioSource.clip = clickSound;
        audioSource.loop = true; // Lặp lại tiếng tíc-tắc liên tục
    }

    void Update()
    {
        // Khi bấm Q hoặc E
        if (Input.GetKeyDown(leftKey) || Input.GetKeyDown(rightKey))
        {
            if (audioSource.isPlaying)
            {
                // Nếu đang kêu thì bấm lại sẽ tắt tiếng xi nhan
                audioSource.Stop();
            }
            else
            {
                // Nếu đang tắt thì bật tiếng tíc-tắc lên
                audioSource.Play();
            }
        }
    }
}