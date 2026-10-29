using UnityEngine;

/// <summary>
/// Công tắc chung cho log gỡ lỗi và phím test.
///  - Verbose: bật thì các script in log chi tiết (khách, nhân viên, máy tính...). Mặc định TẮT cho Console gọn.
///  - CheatsEnabled: phím test (F7 tiền, F8 cấp, T hết ngày, O mở cửa) chỉ chạy trong Unity Editor / bản Development Build.
/// </summary>
public static class GameDebug
{
    public static bool Verbose = false;
    public static bool CheatsEnabled => Application.isEditor || Debug.isDebugBuild;
}
