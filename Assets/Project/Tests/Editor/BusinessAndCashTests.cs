using System.Collections.Generic;
using NUnit.Framework;

/// <summary>
/// Test EditMode cho tính tiền ở quầy (CashMath) và thống kê kinh doanh theo ngày / tuần / tháng (BusinessStats).
/// Ngày 1 của game là thứ Hai 9/2/2026, nên ngày 1-7 là một tuần trọn và ngày 21 là 1/3/2026.
/// </summary>
public class BusinessAndCashTests
{
    // ================= Tiền mặt / thẻ =================
    [Test]
    public void ToCents_RoundsToNearestCent()
    {
        Assert.AreEqual(1999, CashMath.ToCents(19.99f));
        Assert.AreEqual(30, CashMath.ToCents(0.1f + 0.2f));
        Assert.AreEqual(0, CashMath.ToCents(0f));
    }

    [Test]
    public void CashOptions_RoundUpToBanknotes()
    {
        CollectionAssert.AreEqual(new[] { 1300, 1500, 2000, 5000, 10000 }, CashMath.CashOptions(1234));
        CollectionAssert.AreEqual(new[] { 500, 1000, 2000, 5000, 10000 }, CashMath.CashOptions(500));
        foreach (int due in new[] { 1, 99, 1234, 4999, 25001 })
        {
            List<int> o = CashMath.CashOptions(due);
            Assert.IsNotEmpty(o);
            for (int i = 0; i < o.Count; i++)
            {
                Assert.GreaterOrEqual(o[i], due);
                if (i > 0) Assert.Greater(o[i], o[i - 1]);
            }
        }
    }

    // Khách đưa $20.00 cho hoá đơn $12.34 -> phải thối $7.66
    [TestCase(2000, 1234, 766, true, 0)]     // thối đúng
    [TestCase(2000, 1234, 800, true, 34)]    // thối dư 34 xu -> lỗ 34 xu
    [TestCase(2000, 1234, 700, false, 0)]    // thối thiếu -> chưa được xác nhận
    [TestCase(1234, 1234, 0, true, 0)]       // đưa vừa đủ
    public void Change_ConfirmAndLoss(int given, int due, int giving, bool canConfirm, int loss)
    {
        Assert.AreEqual(766, CashMath.ChangeDue(2000, 1234));
        Assert.AreEqual(canConfirm, CashMath.CanConfirm(given, due, giving));
        Assert.AreEqual(loss, CashMath.LossFromChange(given, due, giving));
    }

    [TestCase("12.34", 1234, true)]
    [TestCase("12.3", 1234, false)]
    [TestCase("12.30", 1230, true)]
    [TestCase("", 1234, false)]
    [TestCase("abc", 1234, false)]
    public void CardEntry_MustMatchTheTotal(string entry, int due, bool expected)
    {
        Assert.AreEqual(expected, CashMath.CardEntryMatches(entry, due));
    }

    // ================= Thống kê ngày / tuần / tháng =================
    private static List<BusinessStats.DayRecord> Days(int from, int to)
    {
        var list = new List<BusinessStats.DayRecord>();
        for (int d = from; d <= to; d++)
            list.Add(new BusinessStats.DayRecord { day = d, income = d, expenses = 1f, customers = 10, items = 20 });
        return list;
    }

    [Test]
    public void ByWeek_GroupsMondayToSunday()
    {
        var weeks = BusinessStats.ByWeek(Days(1, 10), 10);
        Assert.AreEqual(2, weeks.Count);
        Assert.AreEqual(7, weeks[0].days.Count);
        Assert.AreEqual(28f, weeks[0].Income, 1e-4);      // 1 + 2 + ... + 7
        Assert.AreEqual(3, weeks[1].days.Count);
        Assert.AreEqual(27f, weeks[1].Income, 1e-4);      // 8 + 9 + 10
        Assert.AreEqual(27f - 3f, weeks[1].Profit, 1e-4);
        Assert.AreEqual(70, weeks[0].Customers);
        StringAssert.StartsWith("Week 09/02", weeks[0].title);
    }

    [Test]
    public void ByMonth_SplitsAtTheEndOfFebruary()
    {
        var months = BusinessStats.ByMonth(Days(1, 25), 12);
        Assert.AreEqual(2, months.Count);
        Assert.AreEqual(20, months[0].days.Count);        // 9-28 February 2026
        Assert.AreEqual(5, months[1].days.Count);         // 1-5 March 2026
        Assert.AreEqual(210f, months[0].Income, 1e-4);    // 1 + ... + 20
        StringAssert.Contains("February 2026", months[0].title);
    }

    [Test]
    public void ByDayAndByWeek_KeepOnlyTheLastPeriods()
    {
        var days = BusinessStats.ByDay(Days(1, 30), 7);
        Assert.AreEqual(7, days.Count);
        Assert.AreEqual(24, days[0].days[0].day);
        Assert.AreEqual(2, BusinessStats.ByWeek(Days(1, 30), 2).Count);
    }

    [Test]
    public void Period_RatingIsWeightedByReviewsAndProductsAddUp()
    {
        var a = new BusinessStats.DayRecord { day = 1, rating = 5f, reviews = 1 };
        var b = new BusinessStats.DayRecord { day = 2, rating = 3f, reviews = 3 };
        a.products.Add(new BusinessStats.ItemCount { item = "Milk", count = 4, revenue = 6f });
        b.products.Add(new BusinessStats.ItemCount { item = "Milk", count = 2, revenue = 3f });
        b.products.Add(new BusinessStats.ItemCount { item = "Coffee", count = 1, revenue = 8f });
        var week = BusinessStats.ByWeek(new List<BusinessStats.DayRecord> { a, b }, 1)[0];
        Assert.AreEqual(3.5f, week.Rating, 1e-4);          // (5 x 1 + 3 x 3) / 4
        var top = week.TopProducts(5);
        Assert.AreEqual("Milk", top[0].item);              // $9 doanh thu
        Assert.AreEqual(6, top[0].count);
        Assert.AreEqual("Coffee", top[1].item);
    }
}
