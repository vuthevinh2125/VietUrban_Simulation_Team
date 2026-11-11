using System.Collections.Generic;
using UnityEngine;

/// <summary>Đèn chỉ bật khi trời tối (đèn đường khu phố mới). DayNightCycle bật / tắt.</summary>
[RequireComponent(typeof(Light))]
public class NightLight : MonoBehaviour
{
    public static readonly List<NightLight> All = new List<NightLight>();
    private Light lightComp;

    void Awake() => lightComp = GetComponent<Light>();
    void OnEnable() { if (!All.Contains(this)) All.Add(this); }
    void OnDisable() => All.Remove(this);

    public void SetOn(bool on)
    {
        if (lightComp != null && lightComp.enabled != on) lightComp.enabled = on;
    }

    public static void SetAll(bool on)
    {
        foreach (NightLight l in All) if (l != null) l.SetOn(on);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => All.Clear();
}
