using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Theo dõi xem có giao diện "chặn" nào đang mở không (máy tính, menu, game over...).
/// Khi đang mở: nhân vật không di chuyển / xoay camera / cầm thùng, chuột được hiện ra.
/// Mỗi giao diện gọi Open(this) khi mở và Close(this) khi đóng.
/// </summary>
public static class UIState
{
    private static readonly HashSet<object> openers = new HashSet<object>();

    public static bool IsBlockingUIOpen => openers.Count > 0;

    /// <summary>true = vừa có UI mở, false = tất cả UI đã đóng.</summary>
    public static event Action<bool> OnChanged;

    public static void Open(object owner)
    {
        if (owner == null || !openers.Add(owner)) return;
        if (openers.Count == 1) Apply(true);
    }

    public static void Close(object owner)
    {
        if (owner == null || !openers.Remove(owner)) return;
        if (openers.Count == 0) Apply(false);
    }

    public static bool IsOpenedBy(object owner) => owner != null && openers.Contains(owner);

    private static void Apply(bool open)
    {
        Cursor.lockState = open ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = open;
        OnChanged?.Invoke(open);
    }

    // Reset khi bấm Play (kể cả khi tắt Domain Reload trong Enter Play Mode Options)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics()
    {
        openers.Clear();
        OnChanged = null;
    }
}
