using UnityEngine;

public class MotorcycleEngineSound : MonoBehaviour
{
    [Header("Audio Components")]
    public AudioSource engineAudioSource;
    public Rigidbody motorcycleRigidbody;

    [Header("Engine Sound Settings")]
    public float minPitch = 0.8f; // Âm thanh trầm nhất khi xe đứng im (Idle)
    public float maxPitch = 2.5f; // Âm thanh thanh/rít nhất khi chạy tốc độ cao
    public float maxSpeed = 20f;  // Tốc độ tối đa của xe (để làm mốc tính toán)

    void Start()
    {
        // Tự động tìm Rigidbody và AudioSource nếu bạn quên kéo vào
        if (motorcycleRigidbody == null) motorcycleRigidbody = GetComponent<Rigidbody>();
        if (engineAudioSource == null) engineAudioSource = GetComponent<AudioSource>();

        // Đảm bảo tiếng động cơ luôn lặp lại và phát ngay khi bắt đầu
        if (engineAudioSource != null)
        {
            engineAudioSource.loop = true;
            if (!engineAudioSource.isPlaying)
            {
                engineAudioSource.Play();
            }
        }
    }

    void Update()
    {
        if (motorcycleRigidbody != null && engineAudioSource != null)
        {
            // 1. Lấy tốc độ hiện tại của xe máy
            float currentSpeed = motorcycleRigidbody.linearVelocity.magnitude;

            // 2. Tính toán tỷ lệ tốc độ (từ 0 đến 1)
            float speedRatio = currentSpeed / maxSpeed;

            // 3. Thay đổi độ trầm bổng (Pitch) dựa theo tốc độ
            // Xe đi càng nhanh, tiếng động cơ càng rít lên cao
            engineAudioSource.pitch = Mathf.Lerp(minPitch, maxPitch, speedRatio);
        }
    }
}