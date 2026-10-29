using TMPro;
using UnityEngine;

/// <summary>Thùng nội thất mới mua (giao ở chỗ giao hàng). Nhặt lên rồi chọn chỗ để lắp đặt.</summary>
public class FurnitureBox : MonoBehaviour
{
    public FurnitureData data;

    public static FurnitureBox Spawn(FurnitureData data, Vector3 position, Quaternion rotation)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "FurnitureBox_" + data.displayName;
        go.transform.SetPositionAndRotation(position + Vector3.up * 0.35f, rotation);
        go.transform.localScale = new Vector3(0.9f, 0.7f, 0.9f);
        go.GetComponent<Renderer>().material.color = new Color(0.72f, 0.55f, 0.36f);
        int layer = LayerMask.NameToLayer("Interactable");
        if (layer >= 0) go.layer = layer;

        // Nhãn tên trên nắp thùng
        GameObject label = new GameObject("Label");
        label.transform.SetParent(go.transform, false);
        label.transform.localPosition = new Vector3(0f, 0.51f, 0f);
        label.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        label.transform.localScale = new Vector3(1f / 0.9f, 1f / 0.9f, 1f / 0.7f) * 0.9f;
        TextMeshPro tmp = label.AddComponent<TextMeshPro>();
        tmp.text = data.displayName;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.rectTransform.sizeDelta = new Vector2(0.85f, 0.4f);
        tmp.enableAutoSizing = true;
        tmp.fontSizeMin = 0.1f;
        tmp.fontSizeMax = 2f;
        tmp.color = new Color(0.15f, 0.1f, 0.05f);

        FurnitureBox box = go.AddComponent<FurnitureBox>();
        box.data = data;
        return box;
    }
}
