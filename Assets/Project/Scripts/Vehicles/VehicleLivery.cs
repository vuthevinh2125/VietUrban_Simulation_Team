using UnityEngine;

/// <summary>
/// Đổi "áo" cho xe nhập về lúc chạy game mà không sửa file của gói gốc: vật liệu nào có tên bắt đầu bằng namePrefix
/// được nhân bản và thay texture chính (_BaseMap / _MainTex). Dùng để:
///  - xe cảnh sát kiểu Mỹ -> xe tuần tra Cảnh sát giao thông Việt Nam (thân trắng, dải xanh, chữ "CẢNH SÁT GIAO THÔNG",
///    "TRAFFIC POLICE"; theo Thông tư 65/2020/TT-BCA), biển số nền xanh chữ trắng;
///  - biển số xe dân sự kiểu Mỹ (HM0123) -> biển trắng chữ đen kiểu Việt Nam.
/// Texture ở Resources/Vehicles (PoliceVN, PlatePoliceVN, PlateVN).
/// </summary>
public static class VehicleLivery
{
    private static readonly int BaseMap = Shader.PropertyToID("_BaseMap"), MainTex = Shader.PropertyToID("_MainTex");

    public static int Swap(Transform root, string namePrefix, string textureResource)
    {
        Texture2D tex = Resources.Load<Texture2D>(textureResource);
        if (tex == null || root == null) return 0;
        int n = 0;
        Material copy = null, original = null;
        foreach (Renderer r in root.GetComponentsInChildren<Renderer>(true))
        {
            Material[] mats = r.sharedMaterials;
            bool changed = false;
            for (int i = 0; i < mats.Length; i++)
            {
                Material m = mats[i];
                if (m == null || !m.name.StartsWith(namePrefix)) continue;
                if (copy == null || original != m)
                {
                    original = m;
                    copy = new Material(m) { name = m.name + "_VN" };
                    if (copy.HasProperty(BaseMap)) copy.SetTexture(BaseMap, tex);
                    if (copy.HasProperty(MainTex)) copy.SetTexture(MainTex, tex);
                }
                mats[i] = copy;
                changed = true;
                n++;
            }
            if (changed) r.sharedMaterials = mats;
        }
        return n;
    }
}
