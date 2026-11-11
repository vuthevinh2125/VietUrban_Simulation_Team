using UnityEngine;
using TMPro;

[RequireComponent(typeof(Rigidbody))]
public class PlayerCarController : MonoBehaviour
{
    [Header("Car Settings")]
    public float normalSpeed = 15f;
    public float boostSpeed = 25f;
    public float turnSpeed = 100f;
    public float acceleration = 10f;
    public float brakeForce = 20f;

    [Header("System")]
    public bool isDriving = false;
    public TextMeshProUGUI speedText;
    public GameObject[] leftSignals;
    public GameObject[] rightSignals;
    public float blinkRate = 0.4f;

    [Header("Effects & Audio")]
    public TrailRenderer[] skidMarks;
    public AudioSource engineAudio;
    public float minEnginePitch = 0.8f;
    public float maxEnginePitch = 2.5f;
    public AudioSource skidAudio;

    private Rigidbody rb;
    private float currentSpeed = 0f;
    private float displaySpeedKMH = 0f; // Tốc độ hiển thị đã được làm mượt
    private int lastSpeedKMH = -1;

    // Đưa các biến nhận Input ra ngoài để truyền từ Update sang FixedUpdate
    private float moveInput;
    private float steerInput;
    private bool isBraking;
    private bool isBoosting;
    private bool isSkidding;

    private bool leftSignalOn = false;
    private bool rightSignalOn = false;
    private float blinkTimer;
    private bool blinkState;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        rb.freezeRotation = true;
    }

    void Update()
    {
        // 1. NẾU KHÔNG LÁI XE: Cập nhật UI và mờ dần âm thanh
        if (!isDriving)
        {
            if (speedText != null) speedText.text = "";
            HandleSkidMarks(false);

            // Âm thanh mờ dần chuyển sang Update để mượt mà hơn
            if (engineAudio != null)
            {
                engineAudio.pitch = Mathf.Lerp(engineAudio.pitch, 0f, Time.deltaTime * 2f);
                engineAudio.volume = Mathf.Lerp(engineAudio.volume, 0f, Time.deltaTime * 2f);
                if (engineAudio.volume < 0.05f && engineAudio.isPlaying) engineAudio.Stop();
            }
            return;
        }

        // 2. NẾU ĐANG LÁI XE: Chỉ đọc Input (Không dùng vật lý ở đây)
        moveInput = Input.GetAxis("Vertical");
        steerInput = Input.GetAxis("Horizontal");
        isBoosting = Input.GetKey(KeyCode.LeftShift);
        isBraking = Input.GetKey(KeyCode.Space);

        // Tính toán tốc độ lên UI
        if (speedText != null)
        {
            float targetSpeedKMH = rb.linearVelocity.magnitude * 3.6f;

            // Làm mượt tốc độ thay đổi theo thời gian thực (tránh nhảy số đột ngột)
            displaySpeedKMH = Mathf.Lerp(displaySpeedKMH, targetSpeedKMH, Time.deltaTime * 10f);

            int currentSpeedKMH = Mathf.RoundToInt(displaySpeedKMH);

            // Chỉ cập nhật chữ khi giá trị làm tròn thay đổi thực sự
            if (currentSpeedKMH != lastSpeedKMH)
            {
                if (currentSpeedKMH < 1) currentSpeedKMH = 0;

                lastSpeedKMH = currentSpeedKMH;
                speedText.text = currentSpeedKMH + " KM/H";
            }
        }

        // Xử lý hiệu ứng Hình ảnh & Âm thanh ngay trong Update để không bị giật
        isSkidding = isBraking && Mathf.Abs(currentSpeed) > 2f;
        HandleSkidMarks(isSkidding);
        HandleAudio(isSkidding);

        // Xử lý Xi-nhan
        HandleTurnSignals();
    }

    void FixedUpdate()
    {
        // 3. XỬ LÝ VẬT LÝ: Chỉ dùng các biến Input đã lưu để di chuyển Rigidbody
        if (!isDriving)
        {
            // Xe đang đỗ: Trượt từ từ cho đến khi dừng hẳn
            currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, brakeForce * Time.fixedDeltaTime);

            if (currentSpeed <= 0.1f)
            {
                currentSpeed = 0f;
                rb.isKinematic = true; // Khóa cứng
            }
            else
            {
                Vector3 stopVel = transform.forward * currentSpeed;
                rb.linearVelocity = new Vector3(stopVel.x, rb.linearVelocity.y, stopVel.z);
            }
            return;
        }

        // --- KHI ĐANG LÁI XE ---
        if (rb.isKinematic) rb.isKinematic = false;

        float targetSpeed = isBoosting ? boostSpeed : normalSpeed;

        // Tính toán Gia tốc & Phanh
        if (isBraking)
        {
            currentSpeed = Mathf.MoveTowards(currentSpeed, 0f, brakeForce * Time.fixedDeltaTime);
        }
        else
        {
            currentSpeed = Mathf.MoveTowards(currentSpeed, targetSpeed * moveInput, acceleration * Time.fixedDeltaTime);
        }

        // Xoay xe
        if (Mathf.Abs(currentSpeed) > 0.1f)
        {
            float direction = Mathf.Sign(currentSpeed);
            // Dùng steerInput thay vì nhận trực tiếp Input trong FixedUpdate
            Quaternion turnRotation = Quaternion.Euler(0f, steerInput * turnSpeed * direction * Time.fixedDeltaTime, 0f);
            rb.MoveRotation(rb.rotation * turnRotation);
        }

        // Tiến/Lùi
        Vector3 moveVelocity = transform.forward * currentSpeed;
        rb.linearVelocity = new Vector3(moveVelocity.x, rb.linearVelocity.y, moveVelocity.z);
    }

    // Tách riêng logic xi-nhan cho code gọn gàng
    void HandleTurnSignals()
    {
        if (Input.GetKeyDown(KeyCode.Q))
        {
            leftSignalOn = !leftSignalOn;
            rightSignalOn = false;
            SetSignals(rightSignals, false);
        }
        if (Input.GetKeyDown(KeyCode.E))
        {
            rightSignalOn = !rightSignalOn;
            leftSignalOn = false;
            SetSignals(leftSignals, false);
        }

        if (leftSignalOn || rightSignalOn)
        {
            blinkTimer += Time.deltaTime;
            if (blinkTimer >= blinkRate)
            {
                blinkTimer = 0f;
                blinkState = !blinkState;
                if (leftSignalOn) SetSignals(leftSignals, blinkState);
                if (rightSignalOn) SetSignals(rightSignals, blinkState);
            }
        }
        else
        {
            SetSignals(leftSignals, false);
            SetSignals(rightSignals, false);
        }
    }

    void HandleSkidMarks(bool state)
    {
        foreach (TrailRenderer trail in skidMarks)
        {
            if (trail != null) trail.emitting = state;
        }
    }

    void HandleAudio(bool skiddingStatus)
    {
        if (engineAudio != null)
        {
            if (!engineAudio.isPlaying) engineAudio.Play();

            engineAudio.volume = Mathf.Lerp(engineAudio.volume, 1f, Time.deltaTime * 5f);
            float speedRatio = Mathf.Abs(currentSpeed) / boostSpeed;
            float targetPitch = Mathf.Lerp(minEnginePitch, maxEnginePitch, speedRatio);
            engineAudio.pitch = Mathf.Lerp(engineAudio.pitch, targetPitch, Time.deltaTime * 5f);
        }

        if (skidAudio != null)
        {
            if (skiddingStatus && !skidAudio.isPlaying)
            {
                skidAudio.Play();
            }
            else if (!skiddingStatus && skidAudio.isPlaying)
            {
                skidAudio.Stop();
            }
        }
    }

    void SetSignals(GameObject[] signals, bool state)
    {
        foreach (GameObject signal in signals)
        {
            if (signal != null) signal.SetActive(state);
        }
    }
}