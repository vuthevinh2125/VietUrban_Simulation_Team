using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Vùng có mái che: hạt mưa rơi vào trong hộp này bị xoá (WeatherSystem). Đặt một hoặc nhiều hộp vào scene
/// (Add Component > RainShelter, chỉnh Size, xem khung xanh khi chọn) để thay khung siêu thị mặc định viết sẵn trong
/// WeatherSystem - dời hoặc đổi toà nhà thì chỉ cần chỉnh hộp, không phải sửa code. Hộp thẳng trục (không xoay theo object).
/// Phòng mở rộng và nhà kho vẫn được đo tự động như trước.
/// </summary>
public class RainShelter : MonoBehaviour
{
    public static readonly List<RainShelter> All = new List<RainShelter>();

    [Tooltip("Kích thước hộp (m), tâm là vị trí object. Chiều cao nên tới sát mái.")]
    public Vector3 size = new Vector3(25f, 12f, 33f);

    public Bounds WorldBounds => new Bounds(transform.position, size);

    void OnEnable() { if (!All.Contains(this)) All.Add(this); }
    void OnDisable() => All.Remove(this);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => All.Clear();

    void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.3f, 0.65f, 1f, 0.9f);
        Gizmos.DrawWireCube(transform.position, size);
    }
}
