using System;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Bộ test tự động (EditMode, Unity Test Framework) cho các hàm logic thuần của game:
/// cấp độ / XP, hạn dùng hàng tươi, nhóm hàng và nhu cầu, xác suất mua theo giá, chấm điểm đánh giá, kiểu khách.
/// Chạy: Window > General > Test Runner > EditMode > Run All.
/// </summary>
public class GameLogicTests
{
    // ================= Cấp độ / XP =================
    [Test]
    public void XPForLevel_MatchesDesignedCurve()
    {
        Assert.AreEqual(80, StoreProgress.XPForLevel(1));
        Assert.AreEqual(210, StoreProgress.XPForLevel(2));
        Assert.AreEqual(400, StoreProgress.XPForLevel(3));
        Assert.AreEqual(650, StoreProgress.XPForLevel(4));
        Assert.AreEqual(960, StoreProgress.XPForLevel(5));
    }

    [Test]
    public void XPForLevel_GrowsFasterAtHigherLevels()
    {
        int prevStep = 0;
        for (int l = 1; l < 20; l++)
        {
            int step = StoreProgress.XPForLevel(l + 1) - StoreProgress.XPForLevel(l);
            Assert.Greater(step, prevStep, $"XP step at level {l} should be larger than the previous one");
            prevStep = step;
        }
    }

    [Test]
    public void XPForSale_IsDollarsPlusItems_AtLeastOne()
    {
        Assert.AreEqual(8, StoreProgress.XPForSale(5.01f, 3));
        Assert.AreEqual(1, StoreProgress.XPForSale(0f, 0));
    }

    // ================= Hạn dùng =================
    [TestCase("Meat Tray", 2f)]
    [TestCase("Chicken", 2f)]
    [TestCase("Slice Bread", 3f)]
    [TestCase("Cake", 3f)]
    [TestCase("Milk", 4f)]
    [TestCase("Ham", 4f)]
    [TestCase("Cheese", 6f)]
    [TestCase("Egg Box 12x", 6f)]
    [TestCase("Milk Brick", 0f)]       // sữa hộp tiệt trùng: không hỏng
    [TestCase("Shampoo", 0f)]          // chứa "ham" nhưng không phải thực phẩm
    [TestCase("Frozen Pizza", 0f)]
    [TestCase("Chocolate Spread", 0f)]
    [TestCase("Sugar", 0f)]
    public void ShelfLife_ByProductName(string name, float days)
    {
        Assert.AreEqual(days, Freshness.ShelfLifeDays(name));
    }

    // ================= Nhóm hàng / nhu cầu =================
    [TestCase("Shampoo", ProductGroup.Household)]
    [TestCase("Ham", ProductGroup.Fresh)]
    [TestCase("Cans Pack", ProductGroup.Drinks)]
    [TestCase("Coffee", ProductGroup.HotDrinks)]
    [TestCase("Beer Pack", ProductGroup.Alcohol)]
    [TestCase("Chocolate Spread", ProductGroup.Breakfast)]
    [TestCase("Pet Food", ProductGroup.Pet)]
    [TestCase("Rice Pack", ProductGroup.Pantry)]
    public void GroupOf_ClassifiesByName(string name, ProductGroup expected)
    {
        Assert.AreEqual(expected, DemandModel.GroupOf(name));
    }

    [Test]
    public void DemandMultiplier_AlwaysWithinBounds()
    {
        foreach (ProductGroup g in Enum.GetValues(typeof(ProductGroup)))
            foreach (WeatherSystem.Weather w in Enum.GetValues(typeof(WeatherSystem.Weather)))
                foreach (WeatherSystem.Season s in Enum.GetValues(typeof(WeatherSystem.Season)))
                    foreach (int temp in new[] { 5, 20, 28, 36 })
                        for (int day = 0; day < 7; day++)
                            for (float h = 6f; h <= 22f; h += 0.5f)
                            {
                                float m = DemandModel.Multiplier(g, h, w, s, temp, (DayOfWeek)day);
                                Assert.That(m, Is.InRange(0.4f, 2.5f), $"{g} {w} {s} {temp}C day {day} {h}h");
                            }
    }

    [Test]
    public void DemandMultiplier_HotDayBoostsColdDrinks()
    {
        float hot = DemandModel.Multiplier(ProductGroup.Drinks, 13f, WeatherSystem.Weather.Sunny, WeatherSystem.Season.Summer, 35, DayOfWeek.Wednesday);
        float cool = DemandModel.Multiplier(ProductGroup.Drinks, 13f, WeatherSystem.Weather.Sunny, WeatherSystem.Season.Summer, 20, DayOfWeek.Wednesday);
        Assert.Greater(hot, cool);
    }

    [Test]
    public void PriceTolerance_ClampedBetween085And13()
    {
        Assert.AreEqual(1f, DemandModel.PriceTolerance(1f), 1e-4f);
        Assert.AreEqual(1.3f, DemandModel.PriceTolerance(10f), 1e-4f);
        Assert.AreEqual(0.85f, DemandModel.PriceTolerance(0f), 1e-4f);
    }

    // ================= Giá / xác suất mua =================
    private static ItemData MakeItem(float unitPrice)
    {
        var item = ScriptableObject.CreateInstance<ItemData>();
        item.itemName = "Test item";
        item.unitPrice = unitPrice;
        return item;
    }

    [Test]
    public void MarketPrice_DefaultsToUnitPriceTimesMarkup()
    {
        ItemData item = MakeItem(2.5f);
        Assert.AreEqual(4f, item.MarketPrice, 0.001f);
        UnityEngine.Object.DestroyImmediate(item);
    }

    [Test]
    public void BuyChance_FollowsPriceCurve()
    {
        ItemData item = MakeItem(1f); // giá thị trường 1.60
        Assert.AreEqual(1f, PriceManager.BuyChance(item, 1.60f), 0.01f);    // đúng giá thị trường
        Assert.AreEqual(1f, PriceManager.BuyChance(item, 1.00f), 0.01f);    // rẻ hơn
        Assert.AreEqual(0.8f, PriceManager.BuyChance(item, 1.92f), 0.01f);  // đắt hơn 20%
        Assert.AreEqual(0.15f, PriceManager.BuyChance(item, 2.40f), 0.01f); // đắt hơn 50%
        Assert.AreEqual(0f, PriceManager.BuyChance(item, 2.60f), 0.01f);    // quá đắt
        UnityEngine.Object.DestroyImmediate(item);
    }

    [Test]
    public void BuyChance_NeverIncreasesWithPrice()
    {
        ItemData item = MakeItem(1f);
        float prev = 1f;
        for (float p = 0.5f; p <= 3f; p += 0.05f)
        {
            float c = PriceManager.BuyChance(item, p);
            Assert.LessOrEqual(c, prev + 1e-5f, $"price {p:F2}");
            prev = c;
        }
        UnityEngine.Object.DestroyImmediate(item);
    }

    // ================= Chấm điểm đánh giá =================
    private static ReviewManager.Visit GoodVisit() => new ReviewManager.Visit
    {
        wanted = 3, got = 3, checkedOut = true, queueSeconds = 10f, queueTolerance = 35f,
    };

    [Test]
    public void ReviewScore_PerfectVisit()
    {
        var r = new ReviewManager.Review();
        float overall = ReviewManager.Score(GoodVisit(), r);
        Assert.AreEqual(5f, r.stock, 1e-4f);
        Assert.AreEqual(4.5f, r.price, 1e-4f);
        Assert.AreEqual(5f, r.checkout, 1e-4f);
        Assert.AreEqual(5f, r.clean, 1e-4f);
        Assert.AreEqual(4.85f, overall, 1e-3f);
    }

    [Test]
    public void ReviewScore_DirtAndExpiredFoodLowerCleanliness()
    {
        var clean = new ReviewManager.Review();
        float a = ReviewManager.Score(GoodVisit(), clean);

        ReviewManager.Visit v = GoodVisit();
        v.maxDirt = 5; v.dirtySkips = 1;
        var dirty = new ReviewManager.Review();
        float b = ReviewManager.Score(v, dirty);
        Assert.AreEqual(2.2f, dirty.clean, 1e-4f);
        Assert.Less(b, a);

        ReviewManager.Visit s = GoodVisit();
        s.sawSpoiled = 2;
        var spoiled = new ReviewManager.Review();
        ReviewManager.Score(s, spoiled);
        Assert.AreEqual(3.8f, spoiled.clean, 1e-4f);
    }

    [Test]
    public void ReviewScore_QueuePatienceDependsOnCustomerType()
    {
        ReviewManager.Visit office = GoodVisit();
        office.queueSeconds = 45f; office.queueTolerance = CustomerProfile.Get(CustomerType.OfficeWorker).queueTolerance;
        ReviewManager.Visit retiree = GoodVisit();
        retiree.queueSeconds = 45f; retiree.queueTolerance = CustomerProfile.Get(CustomerType.Retiree).queueTolerance;
        var ro = new ReviewManager.Review(); ReviewManager.Score(office, ro);
        var rr = new ReviewManager.Review(); ReviewManager.Score(retiree, rr);
        Assert.AreEqual(4f, ro.checkout, 1e-4f);  // 5 - (45 - 20) / 25
        Assert.AreEqual(5f, rr.checkout, 1e-4f);  // chưa tới ngưỡng 50 s
    }

    [Test]
    public void ReviewScore_GaveUpQueueIsOneStarForCheckout()
    {
        ReviewManager.Visit v = GoodVisit();
        v.checkedOut = false; v.gaveUpQueue = true;
        var r = new ReviewManager.Review();
        ReviewManager.Score(v, r);
        Assert.AreEqual(1f, r.checkout, 1e-4f);
    }

    // ================= Kiểu khách =================
    [Test]
    public void OfficeWorkers_FewerOnWeekends()
    {
        Assert.Greater(CustomerProfile.PeakWeight(CustomerType.OfficeWorker, 12f, false),
                       CustomerProfile.PeakWeight(CustomerType.OfficeWorker, 12f, true));
    }

    [Test]
    public void Retirees_MoreInTheMorningThanEvening()
    {
        Assert.Greater(Share(CustomerType.Retiree, 9f), Share(CustomerType.Retiree, 19f) * 3f);
    }

    [Test]
    public void Pick_AlwaysReturnsAValidType()
    {
        for (float h = 7f; h <= 22f; h += 0.25f)
            foreach (float r in new[] { 0f, 0.25f, 0.5f, 0.75f, 0.9999f, 1f })
                Assert.IsTrue(Enum.IsDefined(typeof(CustomerType), CustomerProfile.Pick(h, false, r)));
    }

    [Test]
    public void Preference_LikesAndDislikes()
    {
        CustomerProfile student = CustomerProfile.Get(CustomerType.Student);
        Assert.AreEqual(2f, student.Preference(ProductGroup.Snacks));
        Assert.AreEqual(0.4f, student.Preference(ProductGroup.Alcohol), 1e-4f);
        Assert.AreEqual(1f, student.Preference(ProductGroup.Pantry));
    }

    /// <summary>Tỉ lệ một kiểu khách lúc giờ hour (quét đều số ngẫu nhiên 0..1).</summary>
    private static float Share(CustomerType t, float hour)
    {
        int hit = 0, n = 2000;
        for (int i = 0; i < n; i++) if (CustomerProfile.Pick(hour, false, (i + 0.5f) / n) == t) hit++;
        return hit / (float)n;
    }
}
