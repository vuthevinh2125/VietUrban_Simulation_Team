using UnityEngine;

public class PlayerCleaner : MonoBehaviour
{
    [Header("Cài đặt dọn dẹp")]
    public float cleanRange = 3f;       
    public float timeToClean = 2f;      
    private float currentCleanTime = 0f;

    [Header("Camera góc nhìn thứ nhất")]
    public Camera playerCamera;         // Tia nhìn bắn ra từ đây

    void Update()
    {
        // Khi người chơi nhấn GIỮ chuột trái (hoặc bấm phím tương tác)
        if (Input.GetMouseButton(0))
        {
            TryClean();
        }
        else
        {
            // Nhả chuột ra thì reset quá trình lau (lau chưa xong mà bỏ dở thì phải lau lại từ đầu)
            currentCleanTime = 0f;
        }
    }

    private void TryClean()
    {
        // Bắn một tia Raycast từ giữa màn hình (vị trí Camera) hướng thẳng tới trước
        Ray ray = new Ray(playerCamera.transform.position, playerCamera.transform.forward);
        RaycastHit hit;

        // Vẽ tia đỏ trong tab Scene để bạn dễ debug (không hiện trong tab Game)
        Debug.DrawRay(playerCamera.transform.position, playerCamera.transform.forward * cleanRange, Color.red);

        // Nếu tia này va trúng một vật thể trong cự ly cleanRange
        if (Physics.Raycast(ray, out hit, cleanRange))
        {
            // Kiểm tra xem vật thể đó có dán mác (Tag) là rác/vết bẩn không
            if (hit.collider.CompareTag("Dirt"))
            {
                currentCleanTime += Time.deltaTime;
                Debug.Log($"Đang lau... {(currentCleanTime / timeToClean * 100):F0}%");

                // Nếu giữ chuột đủ lâu
                if (currentCleanTime >= timeToClean)
                {
                    Debug.Log("Đã lau sạch bong!");
                    Destroy(hit.collider.gameObject); // Xóa vết bẩn khỏi màn hình
                    currentCleanTime = 0f;            // Reset biến để lau vết khác
                }
            }
            else
            {
                // Nhìn trúng vật khác (kệ hàng, tường...) thì không tính
                currentCleanTime = 0f;
            }
        }
        else
        {
            currentCleanTime = 0f;
        }
    }
}