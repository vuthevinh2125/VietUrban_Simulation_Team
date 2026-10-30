# VietUrban Supermarket Simulator

Game mô phỏng quản lý siêu thị góc nhìn thứ nhất, làm bằng **Unity 6 (6000.5.4f1), URP**, lấy cảm hứng và vận hành theo lối chơi của *Supermarket Simulator*: nhập hàng, xếp kệ, đặt giá, thu ngân, mở rộng cửa hàng và thuê nhân viên.

> Final Project - VietUrban_Simulation

---

## Lối chơi

1. **Đặt hàng** trên máy tính ở quầy. Thùng hàng được giao tới trước cửa hàng.
2. **Nhặt thùng**, mang vào và **xếp hàng lên kệ** (kệ khô hoặc tủ lạnh / tủ đông cho hàng tươi).
3. **Đặt giá bán.** Khách so giá trên nhãn kệ với giá thị trường hôm nay: rẻ thì mua thêm, đắt quá thì bỏ đi.
4. **Lật biển OPEN.** Khách vào cửa hàng, xách giỏ hoặc đẩy xe, lấy hàng rồi xếp hàng chờ ở quầy.
5. **Thu ngân:** quét từng món, nhận **tiền mặt** (thối đúng tiền thừa) hoặc **thẻ** (gõ đúng số tiền trên máy POS).
6. Hết giờ thì cửa hàng đóng và hiện **báo cáo cuối ngày** (doanh thu, chi phí, lãi). Sang ngày mới, game tự lưu.
7. Bán hàng được **XP** để lên cấp, dùng tiền mua **giấy phép** mở khoá nhóm hàng mới, mua **nội thất**, **mở thêm quầy**, **thuê nhân viên**.

## Tính năng

| Hệ thống | Mô tả |
|---|---|
| Đặt hàng | Máy tính có danh mục 94+ sản phẩm, giỏ hàng, giao thùng theo lưới ở chỗ giao hàng |
| Kho & kệ | Mỗi kệ một loại hàng, xếp / lấy lại từng món, hàng tự phóng cho vừa ô, thùng hiện số món bên trong |
| Giá bán | Nhãn giá trên kệ, bảng đặt giá (giá nhập, giá thị trường, lãi), giá thị trường dao động mỗi ngày |
| Khách hàng | 13 kiểu nhân vật, đi bộ có hoạt ảnh, giỏ / xe đẩy chứa hàng đã lấy, phản ứng theo giá, xếp hàng ở quầy |
| Thu ngân | Quét hàng, trả tiền mặt (thối tiền từng tờ / xu) hoặc thẻ (máy POS), thối dư bị trừ tiền |
| Ngày làm việc | Đồng hồ chỉ chạy khi mở cửa, biển OPEN / CLOSED, giờ đóng cửa, báo cáo cuối ngày, tiền thuê mặt bằng |
| Tiến trình | Cấp cửa hàng (XP), 10 giấy phép nhóm hàng, mở khoá theo cấp |
| Nội thất | 9 loại kệ / tủ lạnh / tủ đông / sạp / kệ kho; mua, lắp đặt có bóng xem trước, xoay 360°, dời chỗ - kể cả kệ / tủ có sẵn trong cửa hàng |
| Mở rộng | 3 quầy thu ngân thêm (tối đa 4 quầy); **nhà kho** riêng cạnh siêu thị (Lv3); **2 phần mở rộng** siêu thị (Lv4, Lv6) - mua xong tường mở ra, kệ sát tường tự dời ra tường mới |
| Nhân viên | Thu ngân (tự quét + nhận tiền), nhân viên xếp kệ (tự mang thùng đi xếp), **bảo vệ** gác ngoài cửa tự đuổi bắt kẻ trộm; trả lương cuối ngày |
| Trộm & dọn dẹp | Kẻ trộm ôm hàng bỏ chạy (còi chống trộm kêu) - cầm **gậy** đuổi theo đánh trúng để lấy lại tiền; vết bẩn xuất hiện trong cửa hàng, cầm **chổi** quét, bẩn nhiều khách mua ít |
| Giao hàng | Đơn đặt hàng qua điện thoại: nhận đơn, lấy gói hàng, chạy **xe máy / ô tô** tới cột sáng vàng để giao, giao nhanh được tip |
| Thành phố | Khu phố mới phía đông (gấp đôi bản đồ): 8 tuyến đường có tên, ngã tư vạch qua đường, 12 khu nhà, ~70 toà nhà có số nhà để giao hàng, đèn đường tự bật ban đêm |
| Bản đồ | Minimap tròn (xoay theo hướng nhìn) + bản đồ lớn **[M]**: đánh dấu cửa hàng, gói hàng / chỗ giao, kẻ trộm, bảo vệ; vẽ lộ trình đường đi tới chỗ giao hàng |
| Ngày / đêm | Mặt trời mọc / lặn theo giờ trong game, chiều tà ngả cam, tối có ánh trăng, đèn xe tự bật |
| Thời tiết & 4 mùa | Lịch thật (Xuân / Hạ / Thu / Đông theo tháng); mỗi ngày tung thời tiết theo mùa: nắng, nhiều mây, mưa, bão (sấm chớp), sương mù; mưa không lọt vào trong nhà, cây đổi màu theo mùa; mưa bão ít khách hơn nhưng giao hàng được thưởng thêm; HUD hiện ngày, mùa, thời tiết, nhiệt độ |
| Giao thông & người đi bộ | ~14 xe máy / ô tô chạy đúng làn bên phải, rẽ ở ngã tư, dừng khi có xe / người phía trước; người đi bộ dạo vỉa hè, vào ra các toà nhà, mưa và ban đêm thì vắng hơn |
| Thống kê kinh doanh | Tab **Stats** trên máy tính: biểu đồ doanh thu / chi phí / lãi theo **ngày, tuần, tháng**; click cột để xem chi tiết từng khoản, số khách, chi tiêu trung bình, ngày tốt nhất, top 5 sản phẩm; lưu cùng file save |
| Đánh giá cửa hàng | Khách chấm 1-5 sao khi ra về theo 4 tiêu chí: đủ hàng, giá, thời gian chờ ở quầy, vệ sinh; kèm lời nhận xét. Điểm trung bình 50 đánh giá gần nhất hiện trên HUD, ảnh hưởng lượng khách (1★ = −30%, 5★ = +30%); xem chi tiết ở máy tính → Stats → **Reviews** |
| Phương tiện | Tab **Vehicles** trên máy tính: Honda Cub có sẵn, mua thêm Honda C125 ($1,500, Lv3) và ô tô Tocus ($4,500, Lv5) nhanh hơn (29 / 36 / 43 km/h); xe chưa mua đỗ ngoài cửa hàng có bảng FOR SALE; nút gọi xe về cửa hàng; F chỉ lên xe gần nhất |
| Cửa tự động | Cửa kéo trượt sang hai bên, mở khi người chơi / khách tới gần hoặc đang đi về phía cửa, người đi bộ ngoài phố và bảo vệ đứng gác không làm cửa mở |
| Lưu game | Tự lưu khi sang ngày mới, lưu tay trong menu; lưu tiền, cấp, giấy phép, giá, hàng trên kệ, nội thất, nhân viên… |
| Menu | Menu chính, tạm dừng, cài đặt (độ nhạy chuột, âm lượng, đồ hoạ, toàn màn hình, bóng đèn), Game Over |
| Âm thanh | Nhạc nền, tiếng quét mã, tiền, thẻ, cửa tự động, thùng, xếp hàng, click giao diện, lên cấp |
| Hướng dẫn | 6 bước hướng dẫn cho người chơi mới khi bắt đầu New Game |

## Điều khiển

| Phím | Chức năng |
|---|---|
| **W A S D** / Shift | Di chuyển / chạy |
| **Chuột** | Nhìn |
| **E** hoặc **Chuột trái** | Tương tác: dùng máy tính, nhặt thùng, quét hàng, nhận tiền, lật biển |
| Giữ **Chuột trái** (cầm thùng) | Xếp hàng lên kệ |
| Giữ **Chuột phải** (cầm thùng) | Lấy hàng từ kệ bỏ lại vào thùng |
| **E** (cầm thùng) | Đặt thùng xuống / vứt thùng rỗng |
| **Chuột trái** (tay không, nhìn kệ) | Đặt giá bán |
| **F** (nhìn kệ / tủ / nội thất) | Dời chỗ (cả kệ có sẵn) · **F** cạnh xe: lên / xuống xe |
| **1 / 2 / 3** | Tay không / chổi (giữ chuột trái để quét) / gậy (chuột trái để vung) |
| **J / K** | Nhận / từ chối đơn giao hàng |
| **M** | Mở / đóng bản đồ lớn (lăn chuột để phóng to) |
| **V** (đang lái xe) | Đổi góc nhìn thứ nhất / thứ ba |
| Lắp nội thất | Click = đặt · Giữ chuột trái + kéo = xoay · **Q / R** = xoay 90° |
| **Esc** | Đóng bảng đang mở / menu tạm dừng |
| **H** | Ẩn / hiện hướng dẫn |

Phím test (chỉ trong Unity Editor): **F7** +$1000 · **F8** lên 1 cấp · **T** tới giờ đóng cửa · **F6** đổi thời tiết.

## Cài đặt & chạy

1. Mở thư mục bằng **Unity 6000.5.4f1**.
2. Mở scene `Assets/Scenes/SampleScene.unity` rồi bấm Play.
3. Build: **File → Build Profiles → Windows → Build**.

> **Lưu ý:** các model nhân vật Mixamo (`Assets/Project/Prefabs/Characters/*.fbx`) và texture tách ra từ chúng **không có trên GitHub** (quá nặng, bị chặn bởi `.gitignore`). Khi clone về cần chép thư mục `Assets/Project/Prefabs/Characters` từ máy đã có, rồi chạy `Tools → Store Setup → 13. Fix Character Colors`. Nếu thiếu, game vẫn chạy và nhân viên / khách tạm dùng model mặc định.

## Cấu trúc code (`Assets/Project/Scripts`)

| Thư mục | Nội dung chính |
|---|---|
| `Core/` | `UIState` (khoá điều khiển khi mở giao diện), `GameSettings`, `GameSession`, `AudioManager`, `GameDebug` |
| `Economy/` | `EconomyManager` (ví tiền, sổ sách, báo cáo ngày), `BusinessStats` (thống kê ngày / tuần / tháng), `ReviewManager` (đánh giá của khách), `PriceManager`, `MarketPrices`, `StockManager`, `StoreProgress`, `LicenseManager` |
| `System/` | `GameManager` (ngày / giờ / mở cửa), `MarketManager` (đặt hàng), `ShopCatalog`, `ComputerInteract`, `NPCShopper` (AI khách), `StaffManager`, `SaveManager`, `StoreSign`, cửa tự động |
| `Checkout/` | `CheckoutCounter` (hàng chờ, quét hàng), `PaymentPanel` / `PaymentToken` (tiền mặt, thẻ), `LockedCheckout` (quầy mở rộng) |
| `item_Building/` | `Shelf` (ô xếp hàng), `ShelfPriceLabels`, `StorageRack` (kệ kho), nội thất: `FurnitureData`, `FurnitureBox`, `FurniturePlacer`, `PlacedFurniture`, `BuiltInFurniture` |
| `Customer/` | `CustomerSpawner`, `Restocker` (AI nhân viên xếp kệ), `ShoppingCarrier` (giỏ / xe đẩy) |
| `Player/` | `PlayerController`, `ItemPickup` (đôi tay người chơi), `Interactable` (thùng hàng), `PlayerTools` (chổi, gậy) |
| `System/` (mới) | `StoreExpansion`, `DirtManager`, `StoreGuard`, `DeliveryManager`, `DoorSensor`, `DayNightCycle`, `WeatherSystem`, `TrafficSystem`, `PedestrianSystem` |
| `UI/` | `MinimapSystem`, `StatsPanel`, `ComputerLayout`, `MenuSystem`, `ProgressHUD`, `TutorialManager`, `PriceSetPanel`, `LicensePanel`, `FurniturePanel`, `StaffPanel`, `DayReportPanel`, `UIKit` |

Các hệ thống liên lạc qua sự kiện tĩnh (`OnMoneyChanged`, `OnStockChanged`, `OnPriceChanged`, `OnNewDay`…), giao diện bảng phụ được dựng bằng code nên không cần prefab.

## Công cụ trong Editor (`Tools → Store Setup`)

| Menu | Việc làm |
|---|---|
| 1-2. Store Setup | Biến các kệ trong siêu thị thành kệ xếp hàng được, bỏ hàng trang trí |
| 3. Setup Checkout | Gắn quầy thu ngân, điểm khách đứng, chỗ đặt hàng, CustomerSpawner |
| 4-6 | Cửa ra vào, cầu nối NavMesh ở cửa, sửa NavMeshSurface trùng |
| 7 / 7b | Nhập sản phẩm từ gói ModernSupermarket, chụp icon sản phẩm |
| 8 | Tạo biển OPEN / CLOSED |
| 9 | Tạo danh mục nội thất, tự dò tầng kệ |
| 10 / 10b | Model nhân viên, giỏ hàng, xe đẩy |
| 11 | Quầy thu ngân mở rộng (ẩn tới khi mua) |
| 12 | Biến nhân vật Mixamo thành khách hàng |
| 13 | Sửa nhân vật Mixamo bị trắng (tách texture, vật liệu URP) |
| 14 | Lưu chỗ đứng của thu ngân kéo thả lúc đang chơi |
| 15 / 16 | Dựng nhà kho cạnh siêu thị / phần mở rộng ngoài các mảng tường đã chọn (ẩn tới khi mua) |
| 17 | Va chạm 2 mặt cho tường, trần, sàn (hết đi xuyên tường từ ngoài vào) |
| 18 | Cho phép dời kệ / tủ có sẵn (bỏ static batching, NavMeshObstacle) - xong Bake NavMesh |
| 19 | Dựng khu phố mới phía đông bằng POLYGON City Pack (đường, khu nhà, toà nhà, đèn, cây) - xong Bake NavMesh |
| `Tools → Save` | Xoá file lưu / mở thư mục chứa file lưu |

## Tài nguyên sử dụng

- Siêu thị, kệ, sản phẩm: gói **Modern Supermarket**
- Thành phố: **POLYGON City Pack**
- Nhân vật & hoạt ảnh: **Mixamo**
- Âm thanh: **Pixabay** (miễn phí bản quyền)

## Thành viên

- **Vũ Thế Vinh** (Leader)
- **Nguyễn Hải Nam**
