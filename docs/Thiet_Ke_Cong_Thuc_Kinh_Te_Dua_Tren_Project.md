# Công thức kinh tế dựa trên project hiện tại

**Bản thiết kế cân bằng v0.3 — cập nhật 24/10/2026.** Tài liệu này thay thế các **con số** về cây, vốn, thuê đất, NPC và máy móc ở GDD v0.1. Bản v0.3 bổ sung công thức áp dụng cho mọi cây thêm về sau. Đây là tham số đề xuất để thử nghiệm, không phải tính năng đã được lập trình.

## 1. Đọc cơ chế đang có trước khi định giá

Đã đối chiếu code và bốn asset cây trong `Assets/Data`:

- `CropPlot.OnNewDay` chỉ tăng giai đoạn khi ô **đã tưới**; sau mỗi lần sang ngày đặt `watered = false`. Trồng ngày 1 và tưới ngày 1 thì cây 4 ngày sẵn sàng **ngày 5**, không phải ngày 4.
- `Crop_Parsnip.asset`: `phaseDays` = `[1,1,1,1]`, thu 1 sản phẩm, không tái sinh.
- `Crop_Potato.asset`: `[1,1,1,2,1]` = 6 ngày, thu ít nhất 1 và có 20% cơ hội cộng thêm sản phẩm liên tiếp. Kỳ vọng gần đúng **1,25 sản phẩm/hạt**, nhưng một vụ cụ thể có thể chỉ ra 1.
- `Crop_GreenBean.asset`: `[1,1,1,3,4]` = 10 ngày; sau đó tái sinh mỗi 3 ngày. Trồng ngày 1 thì thu ngày 11, 14, 17, 20, 23, 26 trong mùa 28 ngày.
- `Crop_Cauliflower.asset`: `[1,2,4,4,1]` = 12 ngày; trồng ngày 1, thu ngày 13.
- `FarmDayClock` mặc định **840 giây/ngày** (14 phút), lưu ngày và phát sự kiện `DayStarted`. HUD đã hiện mùa theo chu kỳ 28 ngày.
- `PlayerFarmingInteractor` có bình tưới 40 lần dùng rồi phải châm nước. `NetworkCharacterStats` có 270 stamina, nhưng thao tác cuốc/trồng/tưới trong luồng đang đọc **chưa trừ stamina**; câu cá có trừ. Thời gian và số lần nhấn là giới hạn lao động có thật ở phiên bản này.
- `FullInventoryMenuUI.currentFunds = 20080` là trường hiển thị mẫu trong prefab; chưa thấy dịch vụ ví tiền hay luồng mua/bán gắn vào `ItemData`. Asset vật phẩm hiện **không chứa giá**. Không được lấy 20.080 G làm vốn khởi đầu thật.
- `CreatePlot` kiểm tra tile đất và vật cản, chưa kiểm tra quyền sử dụng lô. Nếu bật tiền thuê đất mà không thêm kiểm tra sở hữu, người chơi vẫn có thể cuốc đất trên mọi ô hợp lệ của map.
- Chưa thấy luật hết mùa làm cây đậu que chết. Nếu cây tái sinh qua nhiều mùa, mọi mức giá bán cố định hợp lý cho 28 ngày sẽ sinh tiền liên tục. Cần giải quyết trước khi đưa hệ kinh tế vào chạy.

Nguồn: `Assets/Scripts/Runtime/{CropPlot.cs,CropDefinition.cs,FarmDayClock.cs,PlayerFarmingInteractor.cs,NetworkCharacterStats.cs}`, `Assets/Scripts/Runtime/Inventory/UI/FullInventoryMenuUI.cs`, `Assets/Data/Crop_*.asset`.

**Hệ quả thiết kế:** các giá dưới đây dựa vào cây thật trong project. Chúng chỉ có hiệu lực sau khi có ví tiền thật, cửa hàng, vùng đất có chủ, luật hết mùa và quyết toán theo ngày.

## 2. Đơn vị chuẩn và mục tiêu cân bằng

Chọn **một ô củ cải đỏ được trồng, tưới đủ và bán** làm chuẩn vì đây là cây ngắn ngày nhất, không có sản lượng ngẫu nhiên. Giá đề xuất: hạt **20 G**, bán NPC **38 G**, tăng trưởng **4 lần chuyển ngày**.

Lãi gộp một ô một vụ = `38 − 20 = 18 G`. Nếu dùng 24 ô và thu một vụ trước hạn thuê đầu, lãi gộp là `24 × 18 = 432 G`.

Mục tiêu kiểm thử của bản đầu:

1. Người mới trồng 24 ô, không thuê người, không bán dưới giá sàn, vẫn có **ít nhất 90 G** sau kỳ thuê đầu. Số này cho phép sửa sai nhỏ, không biến thất bại thành điều không thể.
2. Ở giá chuẩn, người mới lời khoảng **282 G tuần đầu**, bằng 23,5% vốn 1.200 G. Người chơi chăm 48 ô lời cao hơn vì bỏ thêm tiền hạt và thao tác.
3. Thuê NPC để tự vận hành một lô 24 ô bằng củ cải không tạo thu nhập thụ động đáng kể; tự động hóa cây có đầu tư và rủi ro cao hơn vẫn có thể lời vừa phải.
4. Máy tưới quy mô 48 ô cần khoảng **28 ngày thực sự sử dụng** mới hoàn vốn bằng tiền tiết kiệm so với thuê NPC tưới. Người chơi mua máy để tiết kiệm thời gian hoặc xây chiến lược nhiều mùa, không phải nhận lợi nhuận lớn ngay hôm sau.
5. Giá NPC không leo không giới hạn. Của cải có thể tăng nhờ chơi tốt, nhưng giá hàng NPC và lượng tiền mới sinh ra có trần công khai.

Không đồng nhất “nhiều tiền trong ví” với lạm phát. Trong game, vấn đề cần chống là **tiền NPC tạo ra nhanh hơn khả năng tiêu thụ/tái đầu tư**, khiến mua máy và đất trở nên quá dễ, cùng với giá giao dịch bị đẩy lên không giới hạn.

## 3. Bảng giá để thử nghiệm

| Hạng mục | Giá đề xuất | Lý do |
|---|---:|---|
| Vốn ban đầu | 1.200 G | Trồng 24 ô củ cải tốn 480 G, vẫn còn 720 G dự phòng; trồng đủ 48 ô tốn 960 G và để lại 240 G, lựa chọn có rủi ro hơn. |
| Củ cải đỏ | Hạt 20 G, NPC mua 38 G | 18 G/ô/vụ sau 4 ngày. |
| Khoai tây | Hạt 50 G, NPC mua 65 G/sản phẩm | Kỳ vọng 1,25 sản phẩm → lãi gộp kỳ vọng 31,25 G/ô sau 6 ngày; bán một sản phẩm vẫn có lãi 15 G/ô. |
| Đậu que | Hạt 60 G, NPC mua 30 G/lần thu | Sáu lần thu trong 28 ngày → 120 G/ô/mùa trước công tưới. |
| Súp lơ | Hạt 80 G, NPC mua 145 G | 65 G/ô sau 12 ngày, vốn bị khóa qua kỳ thuê đầu. |
| Thuê khu nền 48 ô | 150 G mỗi 7 ngày | 35% lãi gộp một vụ 24 củ cải; xem công thức dưới. |
| Quyền khai thác lô mở rộng 24 ô đầu | 900 G một lần | Cần tích lũy; không thể mua ngay sau khi nhận 1.200 G. |
| Thuê lô mở rộng 24 ô đầu | 85 G mỗi 7 ngày | Khoảng 20% lãi gộp một vụ củ cải trên lô; đất bỏ trống vẫn có chi phí. |
| NPC nửa ca | 55 G / tối đa 24 thao tác hợp lệ | Hữu ích để phụ việc vừa đủ; có giới hạn công việc. |
| NPC cả ca | 90 G / tối đa 48 thao tác hợp lệ | Rẻ hơn hai nửa ca nhưng buộc cần đủ việc để tận dụng. |
| Máy tưới 48 ô | Mua 2.200 G; 12 G/ngày vận hành | Tiết kiệm thao tác tưới, không cộng sản lượng hay tự bán hàng. |

Giá trên thuộc **bộ thiết kế mới**, không có trong `ItemData` hiện tại. Đừng thay con số prefab 20.080 G bằng 1.200 G rồi xem như hệ kinh tế đã hoạt động.

### 3.1. Công thức áp dụng cho mọi cây thêm về sau

Bốn cây trong bảng chỉ là **bộ hiệu chuẩn**. Project hiện chỉ có bốn `Crop_*.asset`, nhưng `CropDefinition` đã chứa đủ dữ liệu cho nhiều loại cây: thời gian từng giai đoạn, số ngày tái sinh, khoảng sản lượng và cơ hội ra thêm sản phẩm. Công thức dưới đây tạo **giá đề xuất khi thiết kế cây mới**; không tự tăng giá mỗi lần server chạy.

Với cây `i`, người thiết kế xác định:

- `g = tổng phaseDays`: số lần chuyển ngày cần để thu đầu tiên.
- `r = regrowDays`: khoảng cách giữa hai lần thu tiếp theo; bằng 0 nếu phải trồng lại.
- `a, b = minHarvest, maxHarvest`; `p = extraCropChance`.
- `tier = 0…3`: bậc mở khóa theo nội dung, không phụ thuộc thứ hạng giàu có. Bậc cao hơn cho thêm một chút lãi trên **mỗi ô mỗi ngày**, không nhân giá mọi thứ lên gấp nhiều lần.
- `s₀`: ngày sớm nhất cây được phép trồng trong cửa sổ định giá; mặc định ngày 1. Nếu một loại cây chỉ bán từ giữa mùa, dùng ngày mở bán thật.
- `L`: ngày cuối cùng cây được phép tồn tại trong mùa hoặc khu chuyên biệt. Mặc định là ngày 28 và cây chết khi hết mùa.

Sản lượng trung bình đúng theo `CropPlot.TryHarvest`:

```text
q = (a + b) / 2 + Σ[p^j], với j từ 1 đến 20.
```

Phần tổng biểu diễn chuỗi sản phẩm cộng thêm: mỗi lần thành công có cơ hội `p` để thử tiếp, tối đa 20 lần. Khi `p = 0,2` và `a = b = 1`, `q ≈ 1,25`, như khoai tây hiện tại. Khi cây cho 1–2 quả ngẫu nhiên và `p = 0`, `q = 1,5`; **giá bán phải tính trên 1,5 quả kỳ vọng**, không coi mỗi lần thu chỉ có một quả.

Nếu trồng ngày `s`, lần thu đầu ở `s + g`. Số lần thu hợp lệ đến ngày `L`:

```text
Nếu s + g > L: N(s) = 0, cây không kịp chín.
Nếu r = 0 và s + g ≤ L: N(s) = 1.
Nếu r > 0 và s + g ≤ L: N(s) = 1 + floor[(L − s − g) / r].
```

Với đậu que trồng ngày 1: `g = 10`, `r = 3`, `L = 28` nên `N(1) = 6`. Trồng ngày 20 thì `N(20) = 0`; giao diện phải cảnh báo mất hạt trước khi người chơi gieo.

Đặt mục tiêu **lãi gộp G/ô/ngày** cho cây:

```text
T = clamp[4,5 + 0,64 × tier + 0,035 × (g − 4)
          − 0,42 × I(r > 0), 3,5, 6,5].
```

`I(r > 0)` bằng 1 khi cây tái thu hoạch. Cây tái thu thường bớt công trồng lại, nên biên lãi mục tiêu trên ngày được giảm nhẹ. Mốc thấp nhất khoảng 3,5 G và cao nhất **6,5 G/ô/ngày**, tức tối đa khoảng **44% cao hơn** chuẩn củ cải 4,5 G/ô/ngày trước tiền thuê và lao động. Bậc mới mạnh hơn nhưng không phá tỷ lệ kinh tế.

Khoảng thời gian dùng để định giá một cây không tái sinh là `H = g`. Với cây tái sinh, dùng toàn bộ cửa sổ được phép khai thác `H = L − s₀ + 1`. Số lần thu trong công thức là `N(s₀)`, không mặc định `N(1)` nếu cây chỉ bán từ giữa mùa. Mục tiêu lãi gộp mỗi hạt:

```text
M = T × H.
```

Chọn tỷ lệ vốn nằm ở hạt `f`:

- `f = 0,53` cho cây một lần thu và `g < 10`.
- `f = 0,55` cho cây một lần thu và `g ≥ 10`.
- `f = 0,62` nếu sản lượng kỳ vọng mỗi lần thu lớn hơn 1,05 (cần trả vốn nhiều hơn để đổi lấy cơ hội ra nhiều quả).
- `f = 0,33` cho cây tái thu hoạch; trường hợp này ưu tiên hơn quy tắc sản lượng nhiều quả.

Giá hạt và giá NPC mua mỗi sản phẩm:

```text
S = làm_tròn_đến_5_G[M × f / (1 − f)]
P = làm_tròn_đến_1_G[(S + M) / (q × N(s₀))]
```

Với cây một lần thu, `N(s₀) = 1`. Với cây tái sinh, `N(s₀)` tính trong cửa sổ trồng hợp lệ. Do làm tròn, lãi thực tế có thể lệch mục tiêu 1–2 G; script tính ra con số cuối cùng để nhà thiết kế kiểm. Nếu `N(s₀) = 0`, thiết kế cây hoặc lịch bán hạt không hợp lệ và không được đưa vào cửa hàng.

**Đối chiếu cây đang có:**

- Củ cải (`g=4`, `tier=0`, `q=1`): hạt **20 G**, bán **38 G**.
- Khoai tây (`g=6`, `tier=1`, `q≈1,25`): hạt **50 G**, bán **65 G**.
- Đậu que (`g=10`, `r=3`, `tier=0`, `N=6`): hạt **60 G**, bán **30 G/lần thu**.
- Súp lơ (`g=12`, `tier=1`): hạt **80 G**, bán **145 G**.

**Thử ba cây tương lai chỉ để minh họa công thức, chưa có asset trong project:**

- Cây thu một lần, chín sau 8 ngày, bậc 2: hạt **55 G**, bán **102 G**. Lãi 47 G/ô/vụ, khoảng 5,88 G/ô/ngày.
- Cây chín sau 7 ngày, bậc 3, thu 1–2 quả và có 10% cơ hội cộng thêm: hạt **75 G**, bán **75 G/quả**; trung bình khoảng **1,61 quả/vụ**. Giá mỗi quả không cần đẩy lên cao vì một hạt có thể ra nhiều quả.
- Cây chín sau 9 ngày, tái thu mỗi 4 ngày, bậc 2: hạt **75 G**, bán **46 G/lần thu**; trồng ngày 1 thu năm lần trước khi hết mùa.

Các ví dụ này do `docs/balance_economy.py` tính lại được. Nếu một cây được phép sống qua hai mùa, phải dùng số ngày khai thác thực tế và tính lại `N`, `S`, `P`; không giữ giá thiết kế theo 28 ngày rồi cho tái sinh vô hạn.

**Giá công thức chỉ là giá ứng viên.** Trước khi đưa cây vào game, chạy kiểm tra 24/48/144 ô, ngày trồng sớm và muộn, giá NPC thấp nhất, sản lượng thấp nhất, cùng mọi công thức phân bón, nghề, chất lượng và máy chế biến có thể cộng dồn. Đo lợi nhuận **sau hạt, tiền thuê, lương, vật tư và vận hành**, cả kỳ vọng lẫn trường hợp xấu. Nếu cây mới luôn kiếm G/ô/ngày cao hơn cây khác trong cùng bậc mà không có đánh đổi về vốn, thời gian hay nhu cầu thị trường, hạ giá bán hoặc tăng giá hạt trước khi phát hành.

Phần thưởng chất lượng và chế biến phải dùng chung ngân sách lợi nhuận: tổng hệ số NPC trả cho một món có chất lượng sau mọi buff nên có trần đề xuất **1,30 lần** giá cơ sở; một máy chế biến tăng giá 25% phải có vật tư/phí/giới hạn công suất tương ứng. Giao dịch giữa người chơi có thể khác giá NPC vì tiền chỉ đổi chủ, còn số G mới NPC phát ra vẫn tuân thủ hạn mức mua ở mục 8. Hạt trộn ngẫu nhiên cần tính kỳ vọng theo xác suất từng cây và cảnh báo khả năng thu sau ngày cuối mùa.

## 4. Công thức thuê đất gắn với khả năng trả của người mới

Đi từ vụ thực sự thu được trước ngày 7:

```text
Lãi_gộp_cơ_sở = số_ô_người_mới_dự_kiến_trồng × (giá_bán_sàn_chuẩn − giá_hạt)
              = 24 × (38 − 20) = 432 G

Thuê_đất_nền = làm_tròn_đến_5_G(35% × Lãi_gộp_cơ_sở)
              = làm_tròn_đến_5_G(151,2) = 150 G / 7 ngày

Thuê_lô_đầu = làm_tròn_đến_5_G(20% × 24 × 18) = 85 G / 7 ngày
```

“Giá bán sàn chuẩn” trong công thức hiệu chỉnh là **38 G**, mức cơ sở để thiết kế. Giá thực khi dư cung có thể giảm đến 80%, tức khoảng **30 G** sau làm tròn. Với 24 ô, một vụ ở giá 30 G vẫn tạo `24 × (30 − 20) − 150 = 90 G` sau thuê.

Thuê của lô mở rộng thứ `k`:

```text
Thuê_lô(k) = làm_tròn_đến_5_G[85 × (1 + 0,2 × (k − 1))]
```

Bốn lô đầu lần lượt thuê **85, 100, 120, 135 G/kỳ**; cộng đất nền thì tối đa **590 G/kỳ**. Giá nhận lô `k` là `làm_tròn_đến_50_G[900 × 1,55^(k−1)]`, khoảng **900, 1.400, 2.150, 3.350 G**. Trần bốn lô là tham số chế độ cạnh tranh, không phải giới hạn thế giới dài hạn.

Tiền thuê cố định khi hợp đồng được ký cho hết mùa. Đổi giá đất ở mùa sau, không tăng bất ngờ giữa kỳ. Nhận đất giữa kỳ phải tính tiền thuê theo phần ngày còn lại.

**Luật quyết toán:** lúc bắt đầu ngày 8/15/22/29, sau khi đã cho người chơi hoàn thành bán hàng của ngày 7/14/21/28, mới thu kỳ thuê vừa hết. Hoặc thu ở cuối ngày 7 sau toàn bộ giao dịch; triển khai một cách duy nhất để không bị thu hai lần. Nếu chưa đủ tiền, ghi hóa đơn nợ, không cho ví âm.

## 5. Dòng tiền tuần đầu theo đúng ngày trưởng thành

**Phương án người mới — 24 ô củ cải:**

```text
Ngày 1: nhận 1.200; mua 24 hạt × 20 = 480 → còn 720 G.
Ngày 1–4: phải tưới đúng ngày cần để cây tăng trưởng khi chuyển ngày.
Ngày 5: thu 24 củ × 38 = 912 → ví 1.632 G.
Cuối ngày 7: trả thuê 150 → ví 1.482 G.
Lợi nhuận tuần đầu = +282 G.
```

**Nếu giá tụt đến khoảng 80%:** 24 củ × 30 = 720 G, trừ hạt 480 và thuê 150 → **+90 G**, ví cuối 1.290 G.

**Người chăm 48 ô:** hạt 960 G; bán 1.824 G ngày 5; trả thuê 150 → **+714 G**, ví cuối 1.914 G. Phải tưới 48 ô trong bốn ngày; bình hiện chỉ có 40 lượt trước khi châm nước. Đây là nhiều thao tác hơn rõ rệt so với phương án 24 ô.

**24 ô khoai tây:** hạt cần đúng 1.200 G, không còn dự phòng. Thu ngày 7. Kỳ vọng 30 sản phẩm × 65 = 1.950 G; trả thuê 150 → **+600 G**. Nếu toàn bộ hạt chỉ cho một củ, doanh thu 1.560 G và lãi sau thuê còn **+210 G**. Đây là đường chơi mạo hiểm về thanh khoản, giao diện phải cảnh báo rõ.

**Đậu que hoặc súp lơ trồng toàn bộ ngày 1** chưa bán được gì trước tiền thuê ngày 7, nên cần trộn với cây ngắn ngày hoặc giữ tiền. Đây là tác dụng thật của kỳ thuê, không cần tăng giá thuê đến mức bóp nghẹt người mới.

Các phép tính trên giả định thu hoạch và bán được toàn bộ hàng trong hạn mức NPC, không có thất mùa, kỹ năng cộng giá hay chi phí khác. Khi có các hệ đó, phải chạy lại mô hình.

## 6. Công thức lương NPC để không tạo “máy in tiền”

NPC được trả theo **ca thao tác**, không trả một mức ngày quá rẻ rồi làm vô hạn. Mỗi thao tác cuốc, gieo, tưới, thu hoạch chiếm một đơn vị; đường đi và thời gian làm có giới hạn. Bán thành phẩm tự động hoặc bán hàng không nằm trong ca.

Tính lương tối thiểu theo giá trị lô mới:

```text
Lãi_tăng_thêm_lô = sản_lượng_dự_kiến × giá_bán
                 − giá_hạt − thuê_lô − tiền_ca_NPC − vận_hành

Mục tiêu: đối với cây cơ bản, 0 ≤ lãi_tăng_thêm_lô ≤ ~25% lãi_gộp_lô
         khi NPC làm gần như toàn bộ việc và người chơi chịu phí mở đất.
```

Đây là **mục tiêu cân bằng dùng để chọn tham số**, không phải công thức game tự động đổi lương theo vụ thu hoạch. Giá NPC cố định, công khai cả mùa; người chơi giỏi phối hợp công việc vẫn có thể kiếm thêm.

**Thử trên lô 24 ô mới chưa cuốc — củ cải:** ngày 1 cần 72 thao tác: cuốc, gieo, tưới. Chi `90 + 55 = 145 G`. Ba ngày tiếp mỗi ngày tưới 24 ô, `3 × 55 = 165 G`. Ngày 5 thu hoạch 24 ô, 55 G. Tổng lương **365 G**; lãi gộp 432 G; thuê lô 85 G → **−18 G**. Trồng kiểu thụ động hoàn toàn cây rẻ trên lô mới không đáng đầu tư. Người chơi tự làm một phần sẽ có lãi.

**Thử trên lô 24 ô mới — khoai tây:** ngày 1 cũng 145 G; năm ngày tiếp tưới 24 ô, `5 × 55 = 275 G`; ngày 7 thu hoạch 55 G. Tổng **475 G**. Lãi gộp kỳ vọng `24 × (1,25 × 65 − 50) = 750 G`; trừ 85 G thuê và 475 G lương → **+190 G**. Đây là tối đa theo giả định đầu ra trung bình và bán giá chuẩn; 900 G phí nhận đất, tiền hạt và rủi ro sản lượng chưa được hoàn vốn. Nếu chỉ thu 1 củ/hạt, chính lô này lỗ **200 G** sau lương và thuê.

Một người tự cuốc, trồng rồi thuê NPC tưới chỉ trả những ca tưới đã dùng. Không ép người chơi mua trọn gói để che giấu chi phí. Không cho NPC tiếp tục khi đã hết ca, thiếu vật tư hoặc đến ranh giới đất không thuộc quyền sử dụng.

**Điều kiện chống lạm dụng:** một ca tối đa 48 hành động, một người tối đa hai NPC cả ca/ngày trong mùa đầu, không tính thao tác thất bại. NPC không tự nhân giống, không làm việc khi phòng game đã dừng.

## 7. Công thức giá máy để máy có lý do mua nhưng không lời quá nhanh

Máy tưới 48 ô được so với **chi phí thay thế cùng công việc**:

```text
Tiết_kiệm_mỗi_ngày_sử_dụng = 90 G ca NPC tưới 48 ô − 12 G phí máy = 78 G
Số_ngày_hoàn_vốn_tiền_mặt = 2.200 / 78 ≈ 28,21 ngày máy chạy đủ việc
```

Nếu chỉ tưới 24 ô, so với nửa ca 55 G thì tiết kiệm 43 G/ngày, thời gian hoàn vốn khoảng **51,16 ngày sử dụng**. Máy 48 ô vì thế có giá trị cao hơn trên trang trại đủ lớn. Nếu người chơi vốn tự tưới miễn phí, máy **không sinh thêm G trực tiếp**; nó giải phóng thời gian để làm việc khác. Thu nhập từ việc khác phải ghi là thu nhập của hoạt động đó, không cộng giả vào doanh số máy.

Không cho lắp hai máy cùng tưới một ô để nhận lợi ích hai lần. Máy không vận hành khi thiếu tiền phí ngày; cảnh báo trước khi hết tiền. Mua máy không gồm bộ hạt miễn phí.

Khi tính tài sản ròng, máy có thể có giá thanh lý cố định khoảng 40% giá niêm yết nếu còn tốt. Nhưng **dòng tiền** vẫn mất đủ 2.200 G tại thời điểm mua; không dùng giá trị thanh lý để giả rằng người chơi đang đủ tiền thuê đất. Nếu sau này thêm khấu hao, đừng vừa trừ toàn bộ giá mua vào lợi nhuận vừa trừ khấu hao lần nữa.

Với máy chế biến và máy xới, chưa chốt giá chỉ từ cảm tính. Công thức chọn giá là:

```text
Giá_máy ≈ số_ngày_hoàn_vốn_mục_tiêu
         × [lãi_tăng_thêm_tối_đa_mỗi_ngày − phí_vận_hành_mỗi_ngày]
```

Chọn thời gian hoàn vốn khoảng **25–35 ngày thực sự hoạt động** trong chế độ nhiều mùa. Với mùa thi đấu 28 ngày, tài sản mua muộn có thể không hoàn vốn tiền mặt, nhưng vẫn có giá trị thanh lý cuối mùa. Nếu phần lớn người chơi không mua, giảm giá hoặc tăng công dụng thật sau playtest; không tự cho máy nhân sản lượng vô hạn.

## 8. Nguồn cung NPC, thị trường và kiểm soát lượng G mới

Giá bán NPC có thể biến động, nhưng phải có biên và lượng mua được công bố:

```text
Giá_NPC_tuần_sau = làm_tròn_G(
  giá_gốc × clamp(1 + 0,10 × (1 − sản_lượng_bán_tuần_này / nhu_cầu_mục_tiêu),
                  0,80, 1,10)
)
```

Đặt `nhu_cầu_mục_tiêu` ban đầu **48 đơn vị/mặt hàng/người chơi hoạt động/tuần**. Giới hạn biến động tuần kế tiếp tối đa 10% giá gốc, áp dụng sau khi đã tính công thức. Mức 80% là giá sàn của **lô ưu tiên**, không bảo đảm NPC mua vô hạn.

Mỗi người/mặt hàng/tuần:

- 48 sản phẩm đầu bán theo giá NPC công bố.
- 48 sản phẩm tiếp theo chỉ bán với **80% giá công bố**; người chơi có thể giữ hàng hoặc bán cho người khác.
- Vượt 96 sản phẩm, NPC không mua mặt hàng đó đến kỳ mới. Hợp đồng và giao dịch người chơi là kênh khác, có hạn mức riêng.

Tổng ngân sách NPC thu mua thường kỳ mỗi người là **3.500 G/tuần + 600 G/mỗi lô mở rộng**; khi hết ngân sách, NPC ngừng mua thường kỳ. Giao diện luôn hiện ngân sách và số lượng mua còn lại **trước khi người chơi trồng**. Đây là phanh phát hành tiền mới, không áp lên giao dịch giữa người chơi.

Hợp đồng hệ thống dùng quỹ thưởng tách riêng, có trần mỗi người mỗi tuần và yêu cầu giao hàng thật. Không phát G từ đăng nhập, đứng yên, bỏ rồi lập tài khoản mới trong phòng đang chạy. Giao dịch người chơi chỉ chuyển G có sẵn, phí chợ 3% đưa G ra khỏi nền kinh tế.

**Chỉ số cần theo dõi mỗi kỳ:**

```text
G_mới = NPC_mua_hàng + thưởng_hợp_đồng + vốn_người_mới + tiền_cứu_trợ
G_hủy = bán_hạt_vật_tư + thuê_đất + lương_NPC + bán_máy + phí_mở_đất
       + phí_vận_hành + phí_chợ_đấu_giá
Thay_đổi_G_toàn_phòng = G_mới − G_hủy
```

Trong mùa đầu có thể `G_mới > G_hủy` vì người chơi phải tích lũy để mua máy và đất. Theo dõi **G còn lưu thông trên mỗi người hoạt động**, không chỉ tổng G của phòng; mục tiêu thử nghiệm là lượng G trên người tăng đều nhưng giá NPC không vượt biên và khoản mua máy/đất vẫn là quyết định có đánh đổi. Nếu dòng tiền phình quá nhanh, trước tiên tìm nguồn phát G không có giới hạn rồi sửa; không tăng thuê đất bất ngờ giữa mùa.

## 9. Chốt rủi ro cây tái sinh và mùa vụ

Đậu que 30 G/lần thu có lãi vừa phải trong 28 ngày: `6 × 30 − 60 = 120 G/ô` trước công tưới. Nếu cây không hết mùa, sau khi hoàn vốn hạt, nó cho khoảng `30/3 = 10 G/ô/ngày` mãi mãi. Củ cải chỉ cho `18/4 = 4,5 G/ô/ngày` trước công. Đây là lỗ hổng kinh tế lớn nhất hiện tại.

**Quy tắc cần bổ sung:** cây mùa xuân không tiếp tục cho thu hoạch khi bước sang mùa khác; ngày 29 xử lý hết mùa, trừ nhà kính chuyên biệt có số ô và chi phí riêng. Nếu muốn game không giới hạn mùa, phải thêm tuổi thọ tối đa/số lần tái sinh cho đậu que và định giá lại toàn bộ nhóm cây. Dùng cả hai biện pháp ngay sẽ khiến đậu que quá yếu, nên chọn một luật chính.

Với khoai tây, 20% cơ hội thêm củ chạy lặp cho đến thất bại hoặc tối đa 20 lần. Kỳ vọng gần 1,25 nhưng phân phối có đuôi; khi kiểm thử cần chạy hàng nghìn vụ và xem cả trường hợp xấu, không chỉ trung bình.

## 10. Lộ trình đưa công thức vào project

1. **Dữ liệu giá:** tạo catalog kinh tế cho hạt, nông sản, khoáng sản, máy; dùng `itemId` làm khóa. Không buộc UI mẫu hiện tại đóng vai trò nguồn tiền thật.
2. **Ví và sổ giao dịch:** giao dịch nguyên tử, số dư không âm, phân biệt mua, bán NPC, giao dịch người chơi, thuê, nợ. HUD đọc ví thật thay cho `currentFunds` mẫu.
3. **Lịch quyết toán:** gom cập nhật cây và hóa đơn ở một trình tự rõ ràng; cho bán ngày 7 trước khi thu tiền thuê kỳ đó; chống thu hai lần khi tải lại.
4. **Vùng đất:** mỗi ô canh tác xác định chủ quyền khai thác; `CreatePlot`, NPC và máy kiểm tra quyền trước khi làm việc.
5. **Mùa vụ:** xử lý cây hết mùa, đặc biệt cây tái sinh; đóng băng clock khi phòng không chơi.
6. **NPC và máy:** ghi số thao tác thực, công suất, phí vận hành; không trả tiền cho công việc thất bại vì lỗi hệ thống.
7. **Chợ và hợp đồng:** quota NPC theo tuần, giá công bố, khóa tiền/hàng trong giao dịch người chơi.
8. **Playtest:** đo thời gian thực để chăm 24/48 ô, số lần châm bình, lợi nhuận theo kỹ năng người chơi, rồi mới chỉnh lương và giá máy. Nếu 48 ô thủ công vẫn quá dễ, chỉnh giới hạn thời gian/thể lực từ trải nghiệm thực, không chỉ tăng giá máy.

**Ưu tiên MVP:** triển khai các bước 1–5 trước. Khi đó mới có thể đo đúng lợi nhuận và xác định người chơi thật sự muốn thuê NPC hay mua máy đến mức nào.

## 11. Những con số phải được kiểm chứng bằng trận chơi

Script `docs/balance_economy.py` kiểm lại ngày thu và các phép tính chuẩn. Nó chưa mô phỏng đường đi, thời tiết, chất lượng sản phẩm, bán nhiều loại cùng lúc, cấu trúc phòng nhiều người hay hành vi người thật. Những biến này cần dữ liệu playtest.

Chạy tối thiểu các ca: 24/48 ô, chỉ củ cải, chỉ khoai, phối hợp củ cải–đậu que, giá thấp 20%, bỏ tưới một ngày, không thuê, thuê từng ca, mua máy sớm/muộn và mở đất trong lúc thiếu dự trữ. Ghi tiền còn sau mỗi ngày và thời gian người chơi phải dành cho thao tác.

**Chỉ điều chỉnh số sau khi trả lời được:** người mới thất bại vì giá thuê quá cao, vì không biết lịch thu hoạch, hay vì UI che giấu khoản cần trả? Ba nguyên nhân đó cần ba cách sửa khác nhau.
