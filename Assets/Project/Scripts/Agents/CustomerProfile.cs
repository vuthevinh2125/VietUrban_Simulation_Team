using UnityEngine;

/// <summary>Kiểu khách (giống nhịp sống khu phố Việt Nam).</summary>
public enum CustomerType { Student, OfficeWorker, Homemaker, Retiree, Shipper }

/// <summary>
/// Hồ sơ hành vi của từng kiểu khách: số món muốn mua, độ nhạy giá, tốc độ đi, thời gian xem kệ,
/// độ kiên nhẫn khi xếp hàng, nhóm hàng ưa thích / không thích, và giờ cao điểm.
///  - Sinh viên: ít tiền, nhạy giá, mua ăn vặt / nước; đông giờ trưa và chiều tan học.
///  - Dân văn phòng: vội, ít kiên nhẫn xếp hàng, chịu giá; đông sáng sớm, trưa, chiều tan làm; vắng cuối tuần.
///  - Nội trợ: giỏ lớn, đồ tươi / đồ khô / gia dụng; đông buổi sáng và chiều đi chợ.
///  - Người cao tuổi: đi chậm, xem kỹ, kiên nhẫn, khá nhạy giá; đông buổi sáng.
///  - Shipper: vào nhanh mua nước / đồ ăn vặt, ít nhạy giá; đông trưa và tối.
/// Các hàm PeakWeight / Pick là hàm thuần (không đọc trạng thái game) để viết test tự động.
/// </summary>
public class CustomerProfile
{
    public CustomerType type;
    public string label;
    public int minItems, maxItems;
    public float priceFactor = 1f;     // nhân với maxPriceRatio của khách: dưới 1 = nhạy giá hơn
    public float speedFactor = 1f;     // tốc độ đi
    public float browseFactor = 1f;    // thời gian đứng xem mỗi kệ
    public float queueGiveUp = 20f;    // chờ được vào hàng quá số giây này thì bỏ về
    public float queueTolerance = 35f; // xếp hàng quá số giây này thì bắt đầu trừ điểm "thanh toán" khi đánh giá
    public ProductGroup[] likes = new ProductGroup[0];
    public ProductGroup[] dislikes = new ProductGroup[0];

    /// <summary>Hệ số ưa thích một nhóm hàng: thích x2, không thích x0.4.</summary>
    public float Preference(ProductGroup g)
    {
        foreach (ProductGroup l in likes) if (l == g) return 2f;
        foreach (ProductGroup d in dislikes) if (d == g) return 0.4f;
        return 1f;
    }

    public static readonly CustomerProfile[] All =
    {
        new CustomerProfile
        {
            type = CustomerType.Student, label = "Student", minItems = 1, maxItems = 3,
            priceFactor = 0.85f, speedFactor = 1.1f, browseFactor = 0.9f, queueGiveUp = 20f, queueTolerance = 35f,
            likes = new[] { ProductGroup.Snacks, ProductGroup.Drinks },
            dislikes = new[] { ProductGroup.Alcohol, ProductGroup.Household, ProductGroup.Pet },
        },
        new CustomerProfile
        {
            type = CustomerType.OfficeWorker, label = "Office worker", minItems = 1, maxItems = 4,
            priceFactor = 1.1f, speedFactor = 1.15f, browseFactor = 0.7f, queueGiveUp = 12f, queueTolerance = 20f,
            likes = new[] { ProductGroup.Drinks, ProductGroup.HotDrinks, ProductGroup.Breakfast },
            dislikes = new[] { ProductGroup.Pet },
        },
        new CustomerProfile
        {
            type = CustomerType.Homemaker, label = "Homemaker", minItems = 3, maxItems = 7,
            priceFactor = 0.95f, speedFactor = 1f, browseFactor = 1.2f, queueGiveUp = 25f, queueTolerance = 40f,
            likes = new[] { ProductGroup.Fresh, ProductGroup.Pantry, ProductGroup.Household, ProductGroup.Breakfast },
            dislikes = new[] { ProductGroup.Alcohol },
        },
        new CustomerProfile
        {
            type = CustomerType.Retiree, label = "Retiree", minItems = 2, maxItems = 4,
            priceFactor = 0.9f, speedFactor = 0.75f, browseFactor = 1.4f, queueGiveUp = 30f, queueTolerance = 50f,
            likes = new[] { ProductGroup.HotDrinks, ProductGroup.Fresh, ProductGroup.Pantry },
            dislikes = new[] { ProductGroup.Snacks, ProductGroup.Alcohol },
        },
        new CustomerProfile
        {
            type = CustomerType.Shipper, label = "Shipper", minItems = 1, maxItems = 2,
            priceFactor = 1.15f, speedFactor = 1.2f, browseFactor = 0.6f, queueGiveUp = 10f, queueTolerance = 15f,
            likes = new[] { ProductGroup.Drinks, ProductGroup.Snacks },
            dislikes = new[] { ProductGroup.Household, ProductGroup.Pet, ProductGroup.Fresh },
        },
    };

    public static CustomerProfile Get(CustomerType t)
    {
        foreach (CustomerProfile p in All) if (p.type == t) return p;
        return All[0];
    }

    /// <summary>Trọng số xuất hiện của một kiểu khách theo giờ (8.5 = 8:30) và ngày cuối tuần.</summary>
    public static float PeakWeight(CustomerType t, float hour, bool weekend)
    {
        bool In(float a, float b) => hour >= a && hour < b;
        float w;
        switch (t)
        {
            case CustomerType.Student:
                w = 1f;
                if (In(11f, 13f)) w *= 2f; else if (In(16f, 18.5f)) w *= 2.5f; else if (hour >= 20f) w *= 0.6f;
                if (weekend) w *= 1.3f;
                break;
            case CustomerType.OfficeWorker:
                w = 1f;
                if (In(7f, 9f)) w *= 2f; else if (In(11.5f, 13.5f)) w *= 2.5f; else if (In(17f, 19.5f)) w *= 2f;
                if (weekend) w *= 0.4f;
                break;
            case CustomerType.Homemaker:
                w = 1f;
                if (In(8f, 10.5f) || In(15.5f, 18.5f)) w *= 2f;
                if (weekend) w *= 1.3f;
                break;
            case CustomerType.Retiree:
                w = 0.8f;
                if (In(8f, 11f)) w *= 2.5f; else if (hour >= 18f) w *= 0.3f;
                break;
            default: // Shipper
                w = 0.6f;
                if (In(11f, 14f) || In(18f, 21f)) w *= 1.6f;
                break;
        }
        return w;
    }

    /// <summary>Chọn kiểu khách theo trọng số giờ cao điểm. rand01: số ngẫu nhiên trong [0, 1).</summary>
    public static CustomerType Pick(float hour, bool weekend, float rand01)
    {
        float total = 0f;
        foreach (CustomerProfile p in All) total += PeakWeight(p.type, hour, weekend);
        float r = Mathf.Clamp01(rand01) * total;
        foreach (CustomerProfile p in All)
        {
            r -= PeakWeight(p.type, hour, weekend);
            if (r < 0f) return p.type;
        }
        return All[All.Length - 1].type;
    }

    /// <summary>Chọn kiểu khách cho lúc này trong game.</summary>
    public static CustomerProfile PickNow()
    {
        GameManager gm = GameManager.Instance;
        float hour = gm != null ? gm.ClockHours : 12f;
        System.DayOfWeek dow = GameManager.DateOfDay(gm != null ? gm.currentDay : 1).DayOfWeek;
        bool weekend = dow == System.DayOfWeek.Saturday || dow == System.DayOfWeek.Sunday;
        return Get(Pick(hour, weekend, Random.value));
    }
}
