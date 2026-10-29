using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Theo dõi xem có giao diện "chặn" nào đang mở không (máy tính, menu, game over...).
/// Khi đang mở: nhân vật không di chuyển / xoay camera / cầm thùng, chuột được hiện ra.
/// Mỗi giao diện gọi Open(this) khi mở và Close(this) khi đóng.
/// Tự gỡ khoá nếu script giữ khoá đã bị tắt/huỷ mà quên Close (chống kẹt camera).
/// </summary>
public static class UIState
{
    private static readonly HashSet<object> openers = new HashSet<object>();
    private static readonly List<object> stale = new List<object>();
    private static int lastPruneFrame = -1;

    public static bool IsBlockingUIOpen
    {
        get
        {
            Prune();
            return openers.Count > 0;
        }
    }

    /// <summary>true = vừa có UI mở, false = tất cả UI đã đóng.</summary>
    public static event Action<bool> OnChanged;

    /// <summary>Frame gần nhất có giao diện bị đóng (để Esc đóng bảng không mở luôn menu tạm dừng).</summary>
    public static int LastClosedFrame { get; private set; } = -10;

    public static void Open(object owner)
    {
        if (owner == null) return;
        Prune();
        if (!openers.Add(owner)) return;
        if (openers.Count == 1) Apply(true);
    }

    public static void Close(object owner)
    {
        if (owner == null || !openers.Remove(owner)) return;
        LastClosedFrame = Time.frameCount;
        if (openers.Count == 0) Apply(false);
    }

    public static bool IsOpenedBy(object owner) => owner != null && openers.Contains(owner);

    /// <summary>Đóng hết (dùng khi cần thoát khẩn cấp).</summary>
    public static void CloseAll()
    {
        if (openers.Count == 0) return;
        LastClosedFrame = Time.frameCount;
        openers.Clear();
        Apply(false);
    }

    /// <summary>Bỏ các owner là script Unity đã bị huỷ hoặc đang tắt.</summary>
    private static void Prune()
    {
        if (openers.Count == 0 || lastPruneFrame == Time.frameCount) return;
        lastPruneFrame = Time.frameCount;

        stale.Clear();
        foreach (object o in openers)
        {
            if (o is Behaviour b)
            {
                if (b == null || !b.isActiveAndEnabled) stale.Add(o);
            }
            else if (o is UnityEngine.Object uo && uo == null)
            {
                stale.Add(o);
            }
        }

        if (stale.Count == 0) return;
        foreach (object o in stale)
        {
            openers.Remove(o);
            Debug.LogWarning($"[UIState] Tự gỡ khoá UI bị kẹt: {o}");
        }
        if (openers.Count == 0) Apply(false);
    }

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
        stale.Clear();
        lastPruneFrame = -1;
        OnChanged = null;
    }
}
