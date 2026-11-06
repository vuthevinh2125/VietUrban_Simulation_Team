using NUnit.Framework;

/// <summary>Test EditMode cho phần tính toán của gợi ý nhập hàng (RestockAdvisor).</summary>
public class RestockAdvisorTests
{
    [TestCase(0.5f, 0f)]
    [TestCase(0.9f, 1.2816f)]
    [TestCase(0.95f, 1.6449f)]
    [TestCase(0.2f, -0.8416f)]
    public void NormalQuantile_MatchesStandardTable(float p, float z)
    {
        Assert.AreEqual(z, RestockAdvisor.NormalQuantile(p), 1e-3);
    }

    [Test]
    public void Newsvendor_ServiceLevel_IsMarginOverPrice()
    {
        // Giá nhập 1, giá bán 1.6: thiếu 1 món mất 0.6 lãi, dư 1 món hỏng mất 1 vốn -> 0.6 / 1.6 = 37.5%
        Assert.AreEqual(0.375f, RestockAdvisor.NewsvendorServiceLevel(1.6f, 1f), 1e-4);
        // Hạn 4 ngày: hàng dư còn bán tiếp, rủi ro hỏng ~1/4 -> 0.6 / (0.6 + 0.25) ≈ 70.6%
        Assert.AreEqual(0.6f / 0.85f, RestockAdvisor.NewsvendorServiceLevel(1.6f, 1f, 4f), 1e-4);
        // Lãi càng cao càng nên trữ nhiều
        Assert.Greater(RestockAdvisor.NewsvendorServiceLevel(3f, 1f), RestockAdvisor.NewsvendorServiceLevel(1.6f, 1f));
    }

    [Test]
    public void TargetStock_AddsSafetyStockAboveMedianOnly()
    {
        Assert.AreEqual(10f, RestockAdvisor.TargetStock(10f, 0.5f), 1e-3);       // 50%: đúng bằng dự báo
        Assert.Greater(RestockAdvisor.TargetStock(10f, 0.9f), 10f);               // 90%: thêm hàng dự phòng
        Assert.Less(RestockAdvisor.TargetStock(10f, 0.375f), 10f);                // hàng tươi lãi thấp: trữ ít hơn dự báo
        Assert.AreEqual(0f, RestockAdvisor.TargetStock(0f, 0.9f));
    }

    [TestCase(13f, 12, false, 2)]   // hàng khô: làm tròn lên
    [TestCase(0f, 12, false, 0)]
    [TestCase(-5f, 12, false, 0)]
    [TestCase(5f, 12, true, 0)]     // hàng tươi thiếu dưới nửa thùng: không đặt (tránh dư hàng hỏng)
    [TestCase(8f, 12, true, 1)]
    [TestCase(20f, 12, true, 2)]
    public void Boxes_RoundsBySpoilageRisk(float shortfall, int perBox, bool perishable, int expected)
    {
        Assert.AreEqual(expected, RestockAdvisor.Boxes(shortfall, perBox, perishable));
    }
}
