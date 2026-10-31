using UnityEngine;

/// <summary>Một vết bẩn trên sàn. Quét bằng chổi (giữ chuột trái) cho tới khi mờ hẳn.</summary>
public class DirtSpot : MonoBehaviour
{
    public float amount = 1f;
    private Renderer rend;
    private MaterialPropertyBlock block;
    private Color baseColor;

    public void Init(Color color)
    {
        rend = GetComponent<Renderer>();
        block = new MaterialPropertyBlock();
        baseColor = color;
        Apply();
    }

    /// <summary>Quét bớt một phần. Trả về true khi đã sạch hẳn. giveXp = false khi nhân viên lau nhà quét.</summary>
    public bool Clean(float delta, bool giveXp = true)
    {
        amount -= delta;
        if (amount <= 0f)
        {
            if (giveXp) StoreProgress.AddXP(2);
            Destroy(gameObject);
            return true;
        }
        Apply();
        return false;
    }

    private void Apply()
    {
        if (rend == null) return;
        Color c = baseColor;
        c.a = baseColor.a * Mathf.Clamp01(amount);
        rend.GetPropertyBlock(block);
        block.SetColor("_BaseColor", c);
        rend.SetPropertyBlock(block);
    }
}
