using UnityEngine;
using System.Collections; // Required for Coroutines

[RequireComponent(typeof(Rigidbody))]
public class Interactable : MonoBehaviour
{
    public string promptMessage = "Pick up box";

    [Header("Item Data Reference")]
    public ItemData itemData; // Liên kết tới file dữ liệu ItemData chứa shelfScale

    [Header("Crate Inventory")]
    public GameObject productPrefab;
    public int productCount = 10;
    public GameObject[] visualItems;

    [Header("Flap Animation (Rigged)")]
    public Transform flap1; public Vector3 flap1OpenRot;
    public Transform flap2; public Vector3 flap2OpenRot;
    public Transform flap3; public Vector3 flap3OpenRot;
    public Transform flap4; public Vector3 flap4OpenRot;

    private bool isOpened = false;
    private Rigidbody rb;
    private Collider coll;

    // Các biến để "nhớ" góc quay ban đầu (trạng thái đóng nắp)
    private Quaternion f1Closed, f2Closed, f3Closed, f4Closed;

    void Awake()
    {
        rb = GetComponent<Rigidbody>();
        coll = GetComponent<Collider>();

        // Lưu lại vị trí đóng nắp mặc định ngay khi vừa vào game
        if (flap1 != null) f1Closed = flap1.localRotation;
        if (flap2 != null) f2Closed = flap2.localRotation;
        if (flap3 != null) f3Closed = flap3.localRotation;
        if (flap4 != null) f4Closed = flap4.localRotation;

        UpdateVisuals();
    }

    public void OnPickedUp(Transform holdPosition)
    {
        rb.isKinematic = true;
        coll.enabled = false;
        transform.SetParent(holdPosition);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;

        // Nếu hộp chưa mở thì chạy Animation Mở
        if (!isOpened)
        {
            StopAllCoroutines();
            StartCoroutine(OpenFlapsAnimation());
            isOpened = true;
        }
    }

    public void OnDropped()
    {
        transform.SetParent(null);
        rb.isKinematic = false;
        coll.enabled = true;

        // Nếu hộp đang mở thì chạy Animation Đóng
        if (isOpened)
        {
            StopAllCoroutines();
            StartCoroutine(CloseFlapsAnimation());
            isOpened = false;
        }
    }

    public void UpdateVisuals()
    {
        for (int i = 0; i < visualItems.Length; i++)
        {
            if (visualItems[i] != null) visualItems[i].SetActive(i < productCount);
        }
    }

    
    public void TakeItemOut(Transform spawnPosition)
    {
        if (productCount > 0 && itemData != null && itemData.cratePrefab != null)
        {
            productCount--;
            UpdateVisuals();

            // Sinh ra item thực tế trên tay nhân vật hoặc tại vị trí chỉ định
            // Thay vì dùng cratePrefab (thùng), ta phải dùng productPrefab (hộp sữa/ngũ cốc thực tế)
            GameObject spawnedItem = Instantiate(productPrefab, spawnPosition.position, spawnPosition.rotation);

            // Gán lại scale chuẩn từ ItemData
            spawnedItem.transform.SetParent(null);
            if (itemData != null)
            {
                spawnedItem.transform.localScale = itemData.shelfScale;
            }

            // Tách khỏi thùng (nếu cần) và gán lại scale chuẩn bằng kích thước gốc (shelfScale)
            spawnedItem.transform.SetParent(null);
            spawnedItem.transform.localScale = itemData.shelfScale;

            Debug.Log($"[Crate] Took item out. Remaining count: {productCount}");
        }
    }

    private IEnumerator OpenFlapsAnimation()
    {
        float elapsed = 0f;
        float duration = 0.4f;

        Quaternion f1Start = flap1 != null ? flap1.localRotation : Quaternion.identity;
        Quaternion f2Start = flap2 != null ? flap2.localRotation : Quaternion.identity;
        Quaternion f3Start = flap3 != null ? flap3.localRotation : Quaternion.identity;
        Quaternion f4Start = flap4 != null ? flap4.localRotation : Quaternion.identity;

        Quaternion f1End = f1Closed * Quaternion.Euler(flap1OpenRot);
        Quaternion f2End = f2Closed * Quaternion.Euler(flap2OpenRot);
        Quaternion f3End = f3Closed * Quaternion.Euler(flap3OpenRot);
        Quaternion f4End = f4Closed * Quaternion.Euler(flap4OpenRot);

        while (elapsed < duration)
        {
            float progress = elapsed / duration;
            if (flap1 != null) flap1.localRotation = Quaternion.Lerp(f1Start, f1End, progress);
            if (flap2 != null) flap2.localRotation = Quaternion.Lerp(f2Start, f2End, progress);
            if (flap3 != null) flap3.localRotation = Quaternion.Lerp(f3Start, f3End, progress);
            if (flap4 != null) flap4.localRotation = Quaternion.Lerp(f4Start, f4End, progress);

            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private IEnumerator CloseFlapsAnimation()
    {
        float elapsed = 0f;
        float duration = 0.4f;

        Quaternion f1Start = flap1 != null ? flap1.localRotation : Quaternion.identity;
        Quaternion f2Start = flap2 != null ? flap2.localRotation : Quaternion.identity;
        Quaternion f3Start = flap3 != null ? flap3.localRotation : Quaternion.identity;
        Quaternion f4Start = flap4 != null ? flap4.localRotation : Quaternion.identity;

        while (elapsed < duration)
        {
            float progress = elapsed / duration;
            if (flap1 != null) flap1.localRotation = Quaternion.Lerp(f1Start, f1Closed, progress);
            if (flap2 != null) flap2.localRotation = Quaternion.Lerp(f2Start, f2Closed, progress);
            if (flap3 != null) flap3.localRotation = Quaternion.Lerp(f3Start, f3Closed, progress);
            if (flap4 != null) flap4.localRotation = Quaternion.Lerp(f4Start, f4Closed, progress);

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (flap1 != null) flap1.localRotation = f1Closed;
        if (flap2 != null) flap2.localRotation = f2Closed;
        if (flap3 != null) flap3.localRotation = f3Closed;
        if (flap4 != null) flap4.localRotation = f4Closed;
    }
}