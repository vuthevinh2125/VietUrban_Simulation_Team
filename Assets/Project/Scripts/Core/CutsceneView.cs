using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Trong lúc chiếu đoạn phim (camera riêng của phim đè lên trên), camera chính của game vẫn vẽ lại toàn bộ cảnh bên dưới
/// mà không ai nhìn thấy -> mỗi khung hình bị vẽ hai lần. Mute: cho camera chính không vẽ vật nào (cullingMask = 0) và tắt
/// hậu kỳ của nó; Unmute: trả lại như cũ. Không tắt hẳn camera (enabled = false) vì khi đó Camera.main trả về null,
/// trong khi nhiều đoạn code vẫn hỏi Camera.main lúc đang chiếu phim.
/// </summary>
public static class CutsceneView
{
    private static Camera muted;
    private static int mask;
    private static bool post;

    /// <summary>Cho camera chính ngừng vẽ (gọi ngay sau khi camera phim đã CopyFrom camera chính).</summary>
    public static void Mute(Camera main)
    {
        if (main == null || muted != null) return;
        muted = main;
        mask = main.cullingMask;
        main.cullingMask = 0;
        UniversalAdditionalCameraData d = main.GetUniversalAdditionalCameraData();
        post = d != null && d.renderPostProcessing;
        if (d != null) d.renderPostProcessing = false;
    }

    /// <summary>Trả camera chính về như trước khi Mute.</summary>
    public static void Unmute()
    {
        if (muted != null)
        {
            muted.cullingMask = mask;
            UniversalAdditionalCameraData d = muted.GetUniversalAdditionalCameraData();
            if (d != null) d.renderPostProcessing = post;
        }
        muted = null;
    }
}
