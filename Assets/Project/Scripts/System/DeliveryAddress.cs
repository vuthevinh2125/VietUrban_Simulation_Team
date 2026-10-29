using System.Collections.Generic;
using UnityEngine;

/// <summary>Địa chỉ giao hàng của một toà nhà (khu phố mới): tên số nhà + đường, điểm đứng trước cửa trên vỉa hè.</summary>
public class DeliveryAddress : MonoBehaviour
{
    public static readonly List<DeliveryAddress> All = new List<DeliveryAddress>();

    public string address = "1 Main St";
    [Tooltip("Điểm giao hàng (toạ độ thế giới) trên vỉa hè trước toà nhà")]
    public Vector3 doorPoint;

    void OnEnable() { if (!All.Contains(this)) All.Add(this); }
    void OnDisable() => All.Remove(this);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => All.Clear();
}
