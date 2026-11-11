# VietUrban Supermarket Simulator

Game mô phỏng quản lý siêu thị góc nhìn thứ nhất, làm bằng **Unity 6 (6000.5.4f1), URP**, lấy cảm hứng từ lối chơi của *Supermarket Simulator* (nhập hàng, xếp kệ, đặt giá, thu ngân, mở rộng cửa hàng, thuê nhân viên) và đặt trong một khu phố Việt Nam có giao thông, thời tiết, giao hàng bằng xe máy và **Restock Advisor** gợi ý nhập hàng theo dự báo nhu cầu.

> Final Project - COMP1682, University of Greenwich

---

## Lối chơi

1. **New Game** mở bằng đoạn phim mở đầu về cửa hàng của bà Lan (Space để bỏ qua), sau đó là 6 bước hướng dẫn.
2. **Đặt hàng** trên máy tính ở quầy. Thùng hàng được giao tới bãi giao hàng cạnh cửa hàng.
3. **Nhặt thùng**, mang vào và **xếp hàng lên kệ** (kệ khô, tủ lạnh / tủ đông cho hàng tươi).
4. **Đặt giá bán.** Khách so giá trên nhãn kệ với giá thị trường hôm nay: rẻ thì mua thêm, đắt quá thì bỏ đi.
5. **Lật biển OPEN.** Khách đi vào bằng cổng vào, lấy giỏ hoặc xe đẩy, chọn hàng, xếp hàng ở quầy rồi ra bằng cửa ra.
6. **Thu ngân:** quét từng món, nhận **tiền mặt** (thối đúng tiền thừa) hoặc **thẻ** (gõ đúng số tiền trên máy POS).
7. Hết giờ thì cửa hàng đóng và hiện **báo cáo cuối ngày** (doanh thu, chi phí, hàng hỏng, lãi). Sang ngày mới, game tự lưu và trừ tiền thuê mặt bằng.
8. Bán hàng được **XP** để lên cấp, mua **giấy phép** mở khoá nhóm hàng mới, mua **nội thất**, **mở rộng** cửa hàng, **thuê nhân viên**, mua **xe** để giao hàng.
9. Hết tiền trả tiền thuê là **Game Over**: file lưu bị xoá, phải chơi lại từ New Game.

## Tính năng

| Hệ thống | Mô tả |
|---|---|
| Đặt hàng | Máy tính có danh mục 94+ sản phẩm, giỏ hàng; nút **Buy** hiện số thùng và tổng tiền (đỏ khi không đủ tiền), chuột phải vào Buy để xoá giỏ; thùng được giao theo lưới ở bãi giao hàng |
| **Restock Advisor** | Tab **Advisor** trên máy tính: dự báo số bán từ giờ tới hết ngày mai cho từng mặt hàng, học từ chính lịch sử bán của cửa hàng (hệ số thời tiết, nhiệt độ, thứ trong tuần); đề xuất số thùng để đủ hàng cho 90% số ngày, hàng tươi dùng quy tắc newsvendor để cân bằng giữa hết hàng và hàng hỏng; cột **Why** giải thích từng gợi ý; **ADD ALL TO CART** cho cả danh sách vào giỏ |
| Nhu cầu | Nhu cầu mỗi nhóm hàng thay đổi theo giờ, thời tiết, nhiệt độ, mùa và thứ trong tuần; thẻ sản phẩm hiện mức nhu cầu, mỗi sáng có thông báo hàng nào đang chạy |
| Hàng tươi | Thịt, cá, bánh mì, sữa... có hạn dùng tính từ lúc lên kệ (2-4 ngày game); hàng quá hạn đổi màu, khách không mua và chê bẩn; bấm **R** để vứt, nhân viên lau nhà tự dọn; hàng hỏng tính vào báo cáo ngày |
| Kho & kệ | Mỗi kệ một loại hàng, xếp / lấy lại từng món, hàng tự phóng cho vừa ô, thùng hiện số món bên trong; thùng nằm yên thì đứng im, không bị khách đi qua làm đổ |
| Giá bán | Nhãn giá trên kệ, bảng đặt giá (giá nhập, giá thị trường, lãi), giá thị trường dao động mỗi ngày |
| Khách hàng | 5 kiểu khách theo nhịp sống khu phố (sinh viên, dân văn phòng, nội trợ, người già, shipper), mỗi kiểu có số món, độ nhạy giá, độ kiên nhẫn và giờ cao điểm riêng; 13 model nhân vật; giỏ / xe đẩy chỉ có trong cửa hàng; kệ hết hàng thì khách kêu "Sold out?!" và chấm điểm thấp |
| Luồng khách | Đi một chiều: vào bằng cửa gần cổng xoay, ra bằng cửa qua làn thu ngân, không chen qua khu quầy |
| Thu ngân | Quét hàng, trả tiền mặt (thối tiền từng tờ / xu) hoặc thẻ (máy POS), thối dư bị trừ tiền |
| Ngày làm việc | Đồng hồ luôn chạy (kể cả khi đóng cửa), biển OPEN / CLOSED, giờ đóng cửa, báo cáo cuối ngày, tiền thuê mặt bằng |
| Tiến trình | Cấp cửa hàng (XP), 10 giấy phép nhóm hàng; lúc đầu là siêu thị nhỏ, các khu kệ mở dần theo cấp |
| Nội thất | 9 loại kệ / tủ lạnh / tủ đông / sạp / kệ kho; mua, lắp đặt có bóng xem trước, xoay 360°, dời chỗ, kể cả kệ / tủ có sẵn |
| Mở rộng | 3 quầy thu ngân thêm (tối đa 4 quầy); **nhà kho** riêng cạnh siêu thị (Lv3); **2 phần mở rộng** siêu thị (Lv4, Lv6), mua xong tường mở ra, kệ sát tường tự dời ra tường mới |
| Nhân viên | Thu ngân, nhân viên xếp kệ, **bảo vệ** gác cửa tự đuổi bắt kẻ trộm, **nhân viên lau nhà** tự quét vết bẩn; trả lương cuối ngày |
| Trộm & dọn dẹp | Kẻ trộm ôm hàng bỏ chạy (còi chống trộm kêu), cầm **gậy** đuổi theo đánh trúng để lấy lại tiền; vết bẩn xuất hiện trong cửa hàng, cầm **chổi** quét, bẩn nhiều khách mua ít |
| Sự kiện ngẫu nhiên | Mỗi ngày có thể có một sự kiện, mở đầu bằng đoạn cắt cảnh ngắn: **băng trộm** (3 kẻ trộm vào cùng lúc), **thanh tra vệ sinh** (thấy bẩn / hàng hết hạn thì phạt tiền), **đoàn khách du lịch** |
| Giao hàng | Đơn đặt hàng qua điện thoại từ Lv2: nhận đơn, lấy gói hàng, chạy **xe máy / ô tô** tới cột sáng vàng ở 1 trong 73 địa chỉ, giao nhanh được tip |
| Thành phố | Khu phố mới phía đông (gấp đôi bản đồ): 8 tuyến đường có tên, ngã tư vạch qua đường, 12 khu nhà, ~70 toà nhà có số nhà, đèn đường tự bật ban đêm |
| Bản đồ | Minimap tròn (xoay theo hướng nhìn) + bản đồ lớn **[M]**: đánh dấu cửa hàng, gói hàng / chỗ giao, kẻ trộm, bảo vệ; vẽ lộ trình tới chỗ giao |
| Ngày / đêm | Mặt trời mọc / lặn theo giờ trong game, chiều tà ngả cam, tối có ánh trăng, đèn xe tự bật |
| Thời tiết & 4 mùa | Lịch thật (Xuân / Hạ / Thu / Đông theo tháng); mỗi ngày tung thời tiết theo mùa: nắng, nhiều mây, mưa, bão (sấm chớp), sương mù; dự báo thời tiết ngày mai đúng khoảng 80%; mưa không lọt vào trong nhà (vùng mái che chỉnh được bằng **RainShelter**); mưa bão ít khách hơn nhưng giao hàng được thưởng thêm |
| Giao thông & người đi bộ | ~14 xe máy / ô tô chạy đúng làn bên phải, rẽ ở ngã tư, dừng khi có xe / người phía trước; người đi bộ chỉ đi trên vỉa hè và vạch qua đường, vào ra các toà nhà, mưa và ban đêm thì vắng hơn |
| Thống kê kinh doanh | Tab **Stats**: biểu đồ doanh thu / chi phí / lãi theo **ngày, tuần, tháng**; click cột để xem chi tiết, số khách, chi tiêu trung bình, ngày tốt nhất, top 5 sản phẩm |
| Đánh giá cửa hàng | Khách chấm 1-5 sao khi ra về theo 4 tiêu chí: đủ hàng, giá, thời gian chờ, vệ sinh; kèm nhận xét. Điểm trung bình 50 đánh giá gần nhất hiện trên HUD, ảnh hưởng lượng khách (1★ = -30%, 5★ = +30%); xem ở Stats → **Reviews** |
| Phương tiện | Tab **Vehicles**: Honda Cub có sẵn, mua thêm Honda C125 ($1,500, Lv3) và ô tô Tocus ($4,500, Lv5) nhanh hơn (29 / 36 / 43 km/h); xe chưa mua đỗ ngoài cửa hàng có bảng FOR SALE; nút gọi xe về cửa hàng |
| Cửa tự động | Cửa kéo trượt, mở khi người chơi / khách tới gần hoặc đang đi về phía cửa; người đi bộ ngoài phố và bảo vệ đứng gác không làm cửa mở |
| Lưu game | Tự lưu khi sang ngày mới, lưu tay (F5 hoặc menu), nạp lại (F9); lưu tiền, cấp, giấy phép, giá, hàng trên kệ và trong thùng, nội thất, nhân viên, lịch sử bán, **giờ trong ngày** và trạng thái mở / đóng cửa |
| Menu | Menu chính, tạm dừng, cài đặt (độ nhạy chuột, âm lượng, đồ hoạ, toàn màn hình, bóng đèn), Game Over |
| Âm thanh | Nhạc nền, tiếng quét mã, tiền, thẻ, cửa tự động, thùng, xếp hàng, click giao diện, lên cấp |

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
| **R** (nhìn kệ hàng tươi) | Vứt hàng đã hỏng |
| **F** (nhìn kệ / tủ / nội thất) | Dời chỗ (cả kệ có sẵn) · **F** cạnh xe: lên / xuống xe |
| **1 / 2 / 3** | Tay không / chổi (giữ chuột trái để quét) / gậy (chuột trái để vung) |
| **J / K** | Nhận / từ chối đơn giao hàng |
| **M** | Mở / đóng bản đồ lớn (lăn chuột để phóng to) |
| **V** (đang lái xe) | Đổi góc nhìn thứ nhất / thứ ba |
| Lắp nội thất | Click = đặt · Giữ chuột trái + kéo = xoay · **Q / R** = xoay 90° |
| **F5 / F9** | Lưu nhanh / nạp lại file lưu |
| **Space** | Bỏ qua phim mở đầu |
| **Esc** | Đóng bảng đang mở / menu tạm dừng |
| **H** | Ẩn / hiện hướng dẫn |

### Phím test (chỉ trong Unity Editor hoặc bản Development Build)

| Phím | Chức năng |
|---|---|
| **F7** / **F8** | +$1000 / lên 1 cấp |
| **T** | Tới giờ đóng cửa |
| **F6** | Đổi thời tiết |
| **F10** | Hàng tươi trên mọi kệ già thêm 1 ngày |
| **Shift+F10** | Chạy ngay một sự kiện ngẫu nhiên (lần lượt 3 loại) |
| **Ctrl+Shift+F10** | Cho phép xem lại phim của các sự kiện |
| **F11** | Đo FPS 60 giây ở chỗ đang đứng, hiện FPS trung bình và 1% low |
| **Shift+F11 / Ctrl+F11 / Ctrl+Shift+F11** | Đo FPS 60 giây ở 1 cảnh: cửa hàng / phố lúc giông / cửa hàng đông 15 khách |
| **Alt+F11** | Bộ đo đầy đủ: 1 lần khởi động không tính, rồi mỗi cảnh 3 lần; kết quả ghi vào `fps_summary.txt` (kèm CPU, GPU, RAM, độ phân giải), từng khung hình vào `fps_log.csv` |

File lưu và kết quả đo nằm ở `%USERPROFILE%\AppData\LocalLow\DefaultCompany\VietUrban_Simulation` (`Tools → Save → Open Save Folder` mở thư mục này).

## Cài đặt & chạy

1. Mở thư mục bằng **Unity 6000.5.4f1**.
2. Mở scene `Assets/Scenes/SampleScene.unity` rồi bấm Play.
3. Build: **File → Build Profiles → Windows → Build**. Muốn đo FPS bằng Alt+F11 thì tích **Development Build** trước khi build.

> **Lưu ý:** các model nhân vật Mixamo (`Assets/Project/Prefabs/Characters/*.fbx`) và texture tách ra từ chúng **không có trên GitHub** (quá nặng, bị chặn bởi `.gitignore`). Khi clone về cần chép thư mục `Assets/Project/Prefabs/Characters` từ máy đã có, rồi chạy `Tools → Store Setup → 13. Fix Character Colors`. Nếu thiếu, game vẫn chạy và nhân viên / khách tạm dùng model mặc định.

## Kiểm thử

**75 test tự động** (41 hàm test) trong `Assets/Project/Tests/Editor`, chạy bằng **Window → General → Test Runner → EditMode → Run All**:

| File | Nội dung |
|---|---|
| `GameLogicTests` | Luật game: cấp độ, giá, nhu cầu, hạn dùng hàng tươi, đánh giá |
| `BusinessAndCashTests` | Tính tiền thối (`CashMath`, tính bằng xu), gom số liệu ngày / tuần / tháng |
| `RestockAdvisorTests`, `InventoryPolicyTests` | Số thùng gợi ý, mức đủ hàng 90%, quy tắc newsvendor cho hàng tươi |
| `ForecastEvaluationTests` | So sai số dự báo của Advisor với trung bình 7 ngày trên 3 kịch bản mô phỏng |
| `SceneSmokeTests` | 5 test chạy trong scene thật: khởi động đủ hệ thống, đặt hàng, lưu và nạp, mua xe, chốt ngày và Game Over (file lưu thật được sao lưu và trả lại sau test) |

Ngoài ra có 16 test case chức năng (T1-T16) chạy tay theo các bước ghi trong báo cáo, cả 16 đều pass.

## Cấu trúc code (`Assets/Project/Scripts`, 114 script)

| Thư mục | Nội dung chính |
|---|---|
| `Core/` | Phần dùng chung: `UIState` (khoá điều khiển khi mở giao diện), `SaveManager` (lưu / nạp JSON), `GameSettings`, `GameSession`, `AudioManager`, `GameDebug`, `FpsLogger`, `SupermarketScene`, `FoodModeSpawn`, `RuntimeMaterials`, `ModelFit` |
| `Player/` | `PlayerController`, `ItemPickup` (đôi tay người chơi), `Interactable` (thùng hàng), `BoxRest`, `PlayerTools` (chổi, gậy), `ComputerZoom`; `Vehicles/`: `VehicleManager`, `MotorcycleInteraction`, `CarInteraction`, `PlayerCarController`, `VehicleForSale` và âm thanh, đèn của xe |
| `Store/` | `MarketManager` (đặt hàng), `ItemData`, `ProductFit`, `StoreExpansion`, `StoreSign`, `AutoDoor`, `DoorSensor`, `EntranceDoor`, `DirtManager`, `DirtSpot`; `Building/`: `Shelf`, `ShelfPriceLabels`, `StorageRack`, nội thất (`FurnitureData`, `FurnitureBox`, `FurniturePlacer`, `PlacedFurniture`, `BuiltInFurniture`); `Checkout/`: `CheckoutCounter`, `PaymentPanel` / `PaymentToken`, `CashMath`, `LockedCheckout` |
| `Agents/` | AI: `NPCShopper` (khách), `CustomerSpawner`, `CustomerProfile` (5 kiểu khách), `StoreFlow` (luồng vào / ra), `ShoppingCarrier`, `StaffManager`, `Restocker`, `Janitor`, `StoreGuard`, `TrafficSystem`, `PedestrianSystem`, `PedestrianWalker`, `DeliveryRecipient` |
| `Economy/` | `EconomyManager` (ví tiền, sổ sách, báo cáo ngày), `BusinessStats`, `ReviewManager`, `DemandModel` (nhu cầu của game), `DemandForecaster` + `RestockAdvisor` (dự báo và gợi ý nhập hàng, chỉ học từ số bán đã ghi), `Freshness`, `FreshnessManager`, `PriceManager`, `MarketPrices`, `StockManager`, `StoreProgress`, `LicenseManager` |
| `World/` | `GameManager` (ngày / giờ / mở cửa / Game Over), `DayNightCycle`, `WeatherSystem`, `RainShelter`, `NightLight`, `LightShadowBudget`, `DeliveryManager`, `DeliveryAddress`, `PointOfInterest`, `MinimapSystem`, `StoreEvents` (sự kiện ngẫu nhiên) |
| `UI/` | `ComputerInteract`, `ShopCatalog`, `ShopFilterManager`, `ProductCardUI`, `AdvisorPanel`, `CartBadge`, `IntroCutscene`, `StatsPanel`, `VehiclePanel`, `ComputerLayout`, `MenuSystem`, `ProgressHUD`, `TutorialManager`, `PriceSetPanel`, `LicensePanel`, `FurniturePanel`, `StaffPanel`, `ExpansionPanel`, `DayReportPanel`, `SpeechBubble`, `UIKit`, `UIMouseControl` |

Các hệ thống liên lạc qua sự kiện tĩnh (`OnMoneyChanged`, `OnStockChanged`, `OnPriceChanged`, `OnNewDay`...), giao diện bảng phụ được dựng bằng code nên không cần prefab.

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
| 20a / 20b | Phân tích bố cục kệ / chia siêu thị thành các khu mở dần theo cấp |
| 21 | Tìm bãi đất trống gần cửa hàng (bằng phẳng, không trên vỉa hè hay sát đường xe chạy) và dời chỗ giao hàng tới đó |
| `Tools → Save` | Xoá file lưu / mở thư mục chứa file lưu |
| `Tools → Debug` | Kiểm tra lối vào cửa hàng, bản đồ liên thông NavMesh, xuất báo cáo scene |

## Tài nguyên sử dụng

- Siêu thị, kệ, sản phẩm: gói **Modern Supermarket**
- Thành phố: **POLYGON City Pack**
- Nhân vật & hoạt ảnh: **Mixamo**
- Âm thanh: **Pixabay** (miễn phí bản quyền)

## Thành viên

- **Vũ Thế Vinh** (Leader)
- **Nguyễn Hải Nam**
