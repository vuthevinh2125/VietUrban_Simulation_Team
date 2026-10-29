using UnityEngine;

/// <summary>
/// Tiền mặt / thẻ khách đưa ra trên quầy sau khi quét xong (giống Supermarket Simulator):
/// người chơi click vào để mở màn hình thanh toán.
/// </summary>
public class PaymentToken : MonoBehaviour
{
    public CheckoutCounter counter;

    public string Prompt
    {
        get
        {
            if (counter == null) return "";
            return counter.Method == PaymentMethod.Cash
                ? $"[LMB] Take cash {EconomyManager.Format(counter.CashGivenCents / 100f)}  (total {EconomyManager.Format(counter.DueCents / 100f)})"
                : $"[LMB] Take card  (total {EconomyManager.Format(counter.DueCents / 100f)})";
        }
    }

    /// <summary>Tạo vật thể tiền / thẻ đơn giản tại vị trí trên quầy.</summary>
    public static PaymentToken Create(CheckoutCounter counter, PaymentMethod method, Vector3 position, Quaternion rotation)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = method == PaymentMethod.Cash ? "Payment_Cash" : "Payment_Card";
        go.transform.SetPositionAndRotation(position, rotation);
        if (method == PaymentMethod.Cash)
        {
            go.transform.localScale = new Vector3(0.16f, 0.012f, 0.075f);
            RuntimeMaterials.Apply(go.GetComponent<Renderer>(), new Color(0.45f, 0.75f, 0.45f));
        }
        else
        {
            go.transform.localScale = new Vector3(0.086f, 0.006f, 0.054f);
            RuntimeMaterials.Apply(go.GetComponent<Renderer>(), new Color(0.15f, 0.35f, 0.85f));
        }
        go.transform.position += Vector3.up * go.transform.localScale.y * 0.5f;

        // Collider to hơn vật một chút cho dễ bấm
        BoxCollider bc = go.GetComponent<BoxCollider>();
        bc.size = new Vector3(1.3f, 6f, 1.3f);

        int layer = LayerMask.NameToLayer("Interactable");
        if (layer >= 0) go.layer = layer;

        PaymentToken token = go.AddComponent<PaymentToken>();
        token.counter = counter;
        return token;
    }
}
