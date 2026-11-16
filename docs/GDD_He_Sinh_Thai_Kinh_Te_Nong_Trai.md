# MIỀN ĐẤT THỊNH VƯỢNG
## Game Design Document — Kinh tế, quyền khai thác đất và cạnh tranh người chơi

**Phiên bản:** 0.1 — thiết kế đề xuất, 21/10/2026.  
**Phạm vi:** kịch bản nền, trải nghiệm người chơi, luật kinh tế và phạm vi bản chơi thử.  
**Trạng thái:** chưa triển khai vào game; toàn bộ giá và chỉ tiêu cân bằng bên dưới là giả thuyết cần kiểm chứng bằng playtest.

> **Hiệu chỉnh theo project (21/10/2026):** các ví dụ thời gian cây, giá khởi đầu, tiền thuê, lương và máy ở mục 5–9 của bản nháp này đã được thay thế bởi [thiết kế công thức kinh tế dựa trên code hiện có](./Thiet_Ke_Cong_Thuc_Kinh_Te_Dua_Tren_Project.md). Bản nháp từng giả định củ cải trưởng thành sau 3 ngày và thu hai vụ trong 7 ngày; `Crop_Parsnip.asset` thực tế cần 4 lần chuyển ngày, nên không dùng ví dụ đó để triển khai hay cân bằng.
>
> **Định hướng server chung:** giả định phòng chơi và đồng hồ dừng cùng phòng ở mục 3 chỉ áp dụng cho nguyên mẫu cũ. Mô hình một thế giới chung, nhiều khu/kênh và cách tính ngày hoạt động được mô tả trong [kiến trúc server và khu chơi](./Kien_Truc_Server_Khu_Kenh_The_Gioi_Chung.md). Đấu giá liên quan đến người chơi cũng phải tuân thủ lịch thông báo của mô hình này.

## 1. Tuyên ngôn thiết kế

> Bạn đến vùng đất này với một túi tiền và một hợp đồng thuê đất. Bạn có thể trở thành người làm thuê, một chủ trại có tiếng, hoặc người điều hành cả nền nông nghiệp của vùng. Nhưng mỗi bảy ngày, sổ nợ lại được mở ra — và mùa vụ tốt chưa chắc đã cứu được một người tiêu tiền sai lúc.

Đây là game mô phỏng nông trại kết hợp cạnh tranh kinh tế. Người chơi trực tiếp cuốc đất, trồng cây, khai khoáng và giao thương; sau đó dùng lợi nhuận để thuê nhân công, mua máy, nhận thêm đất và giành hợp đồng cung ứng.

**Trải nghiệm cốt lõi:** từ người tự làm mọi việc trở thành người tổ chức một doanh nghiệp nông nghiệp. Cảm giác tiến bộ đến từ việc mua được thời gian, tăng năng lực sản xuất và đưa ra quyết định đầu tư đúng.

Ba câu hỏi phải xuất hiện thường xuyên:

1. Tôi cần giữ lại bao nhiêu tiền để sống qua kỳ thanh toán tiếp theo?
2. Khoản đầu tư nào giúp tôi kiếm tiền tốt hơn hoặc tiết kiệm thời gian có giá trị hơn?
3. Tôi nên hợp tác với đối thủ hôm nay hay cạnh tranh để giành lợi thế ngày mai?

Không thể cam kết game hấp dẫn và cân bằng chỉ bằng tài liệu. Bản thiết kế này đưa ra cơ chế, giới hạn và tiêu chí kiểm thử để đạt mục tiêu đó.

## 2. Bối cảnh và kịch bản thế giới

### 2.1. Một vùng đất đang được tái thiết

Sau nhiều năm bỏ hoang, vùng Đồng Xanh mở cửa đón những chủ trại mới. Hội đồng Phát triển giữ quyền sở hữu đất và cấp quyền khai thác có điều kiện. Ngân quỹ của vùng được duy trì bằng tiền thuê đất, phí dịch vụ và các phiên đấu giá.

Mỗi người chơi nhận cùng số vốn khởi nghiệp và một khu đất cơ bản. Tài trợ này chỉ đủ giúp họ bắt đầu, không đủ duy trì một trang trại thiếu hiệu quả mãi mãi. Thị trấn cần thực phẩm, xưởng máy cần khoáng sản, doanh nghiệp cần nguyên liệu đúng hạn. Mỗi nhu cầu tạo ra một cách kiếm tiền khác nhau.

Khu đất rộng nhất không mặc nhiên là trang trại tốt nhất. Một người trồng cây ngắn ngày có thể giữ tiền mặt tốt hơn người sở hữu vườn cây giá trị cao chưa đến ngày thu hoạch. Một người chuyên khai khoáng có thể cung cấp vật tư cho chính đối thủ đang giành hợp đồng nông sản với mình.

### 2.2. Các nhân vật chức năng

- **Bà An — đại diện Hội đồng:** giới thiệu quyền khai thác đất, kỳ thanh toán và thủ tục tái cơ cấu. Bà giải thích luật trước khi áp dụng hình phạt.
- **Ông Lâm — thương lái:** mua sản phẩm cơ bản, công bố nhu cầu thị trường và hướng dẫn hợp đồng giao hàng.
- **Kỹ sư Mai — chủ xưởng máy:** bán máy, sửa chữa, lắp module; mở tuyến phát triển cơ giới hóa.
- **Chú Bình — quản lý lao động:** cung cấp NPC thuê theo ngày, mô tả rõ năng suất và chi phí.
- **Vy — điều hành đấu giá:** công bố hồ sơ khu đất, giá thuê, tài sản đi kèm và điều kiện dự thầu.

Các NPC này là đầu mối dịch vụ, không phải lao động miễn phí. Tương tác hội thoại không mất phí; thuê họ hoặc người do họ giới thiệu thực hiện công việc thì phải trả tiền.

### 2.3. Mạch truyện theo tiến trình

**Hồi I — Tờ hợp đồng đầu tiên, ngày 1–7.** Người chơi học rằng doanh thu không phải lợi nhuận. Họ tự trồng một vụ ngắn ngày, thực hiện một đơn giao hàng và lần đầu trả tiền thuê đất. Cao trào là lựa chọn giữ tiền cho vụ sau hay mua một nâng cấp hấp dẫn.

**Hồi II — Mua lại thời gian, ngày 8–14.** Công việc vượt quá khả năng làm tay. Người chơi thử thuê nhân công, so sánh mua máy với mở đất và nhận hợp đồng có thời hạn. Mối quan hệ với đối thủ bắt đầu có lợi ích kinh tế thật.

**Hồi III — Cái giá của mở rộng, ngày 15–21.** Nhu cầu thị trường dịch chuyển theo dự báo. Một số trang trại dư hàng, một số thiếu nguyên liệu. Hội đồng đưa một khu đất NPC vào đấu giá để dạy luật mà không cần ép một người chơi thật phá sản. Các quyết định đầu tư trước đó bắt đầu trả kết quả.

**Hồi IV — Ai làm chủ mùa vụ, ngày 22–28.** Xuất hiện hợp đồng uy tín và các cơ hội đất đai cuối mùa. Người chơi lựa chọn chốt lợi nhuận, thanh lý tài sản hoặc đầu tư cho đợt thu hoạch cuối. Sau quyết toán ngày 28, bảng thành tích vinh danh cả người giàu nhất và các chuyên gia sản xuất.

Các mốc là nhịp hướng dẫn, không buộc người chơi mua máy hoặc mở đất trước khi đủ tiền. Không dùng sự kiện có kịch bản để cưỡng ép một trang trại đang vận hành tốt phá sản.

## 3. Cấu trúc thời gian và chế độ chơi

### 3.1. Giả định cho bản đầu tiên

- Phòng có **4–8 người**, mỗi người điều hành một trang trại riêng và dùng chung thị trường.
- Một ngày game khoảng **15 phút hoạt động**; bảy ngày tương đương khoảng 105 phút, một mùa 28 ngày khoảng 7 giờ, có lưu và chơi qua nhiều buổi.
- Đồng hồ dùng chung trong phòng. Không có thao tác ngủ để tự ý đẩy thời gian của người khác.
- Chế độ mặc định là phòng bạn bè có lịch chơi chung. Mất kết nối sẽ tạm dừng ngắn; nếu có người rời lâu, lưu và kết thúc buổi, hoặc người đó chủ động chấp thuận tiếp tục mô phỏng trang trại của mình.
- Khi phòng đã dừng thì cây, NPC, tiền thuê và thị trường đều dừng. Không để khoản nợ âm thầm tăng khi không có phiên chơi.
- Máy chủ quyết định thời gian, số dư, giao dịch và kết quả đấu giá.

Đây là lựa chọn phạm vi, không phải mô hình máy chủ MMO chạy liên tục. Nếu sau này xây thế giới luôn hoạt động, cần thiết kế lại chế độ nghỉ, ủy quyền vận hành, hạn trả nợ và lịch đấu giá; không bê nguyên chu kỳ 105 phút sang thế giới đó.

### 3.2. Hai cách chơi dài hạn

**Mùa cạnh tranh:** mọi người bắt đầu bằng điều kiện tương đương, chốt kết quả sau 28 ngày. Mùa sau tạo phiên mới; thành tích, danh hiệu và vật phẩm ngoại hình được giữ. Tiền, đất và máy không chuyển sang mùa xếp hạng mới.

**Thế giới lâu dài:** giữ tiến trình qua nhiều mùa để xây đại nông trại. Bảng giàu có chỉ có ý nghĩa trong thế giới đó; không ghép trực tiếp với phòng mới hoặc mùa thi đấu.

Bản đầu nên chỉ làm mùa cạnh tranh. Các cấp như “tỷ phú” và “lãnh chủ hành tinh” thuộc định hướng thế giới lâu dài, không phải mục tiêu khả thi trong 28 ngày đầu.

## 4. Vòng lặp kinh tế

**Quan sát nhu cầu → lập kế hoạch dòng tiền → mua đầu vào → sản xuất → bán/giao hàng → trả nghĩa vụ → tái đầu tư.**

Vòng lặp mỗi ngày:

1. Xem tiền có thể chi, lịch thu hoạch, việc NPC và dự báo thị trường.
2. Chọn cách dùng thời gian: chăm ruộng, xuống hang, chế biến, giao hàng hoặc thương lượng.
3. Bán hàng hoặc giữ hàng có lý do; kiểm tra tiền dự kiến vào trước kỳ thuê.
4. Kết ngày với báo cáo ngắn: thu, chi, tồn kho và nghĩa vụ sắp đến hạn.

Vòng lặp bảy ngày:

1. Ghi nhận sản phẩm và giao dịch của ngày cuối kỳ.
2. Quyết toán tiền thuê đất và các khoản đến hạn.
3. Cập nhật trạng thái nợ.
4. Công bố hợp đồng, nhu cầu và lịch đấu giá kỳ tiếp theo.

**Quy tắc trải nghiệm:** bán hàng trong ngày 7 phải được tính trước khi thu tiền thuê cuối ngày 7. Tránh mất đất chỉ vì thứ tự xử lý hệ thống không rõ ràng.

## 5. Tiền, quyền sở hữu và vốn khởi nghiệp

### 5.1. Một đồng tiền chính

Dùng **G** cho mọi giao dịch gameplay: hạt giống, vật tư, đất, NPC, máy móc, sửa chữa và đấu giá. Chưa cần thêm tiền tệ phụ.

Không bán G, đất, nhân công mạnh hơn hay quyền ưu tiên đấu giá bằng tiền thật trong chế độ cạnh tranh. Nếu cần thương mại hóa, tách sang ngoại hình không tạo năng suất.

### 5.2. Gói khởi đầu

- 3.000 G tiền mặt.
- 48 ô canh tác thuộc khu khởi nghiệp.
- Công cụ cầm tay cơ bản, không có máy sản xuất miễn phí.
- Kho cơ bản và quyền tiếp cận chợ, hang mỏ, bảng công việc.
- Giá thuê khu khởi nghiệp: **300 G mỗi 7 ngày**.

Hạt giống phải mua, để giao dịch đầu tiên thể hiện đúng tinh thần đầu tư. Nhiệm vụ hướng dẫn chỉ cấp kiến thức và thành tích, không bơm một khoản tiền lớn làm mất ý nghĩa kỳ thuê đầu tiên.

### 5.3. Đất thuê và tài sản mua

Hội đồng sở hữu đất; người chơi trả **phí cấp/chuyển quyền khai thác** để nhận thêm đất và tiếp tục trả tiền thuê định kỳ. Không gọi khoản phí mở đất là mua quyền sở hữu vĩnh viễn.

Người chơi sở hữu máy đã mua, hàng tồn và vật tư. Việc trả tiền mua máy một lần không miễn chi phí vận hành. Vật tư khai thác có thể chế tạo sản phẩm, phụ tùng và module, nhưng không tạo được máy nền hoàn chỉnh.

## 6. Khung giá và cách kiểm soát lợi nhuận

### 6.1. Quy hoạch giá từ một hoạt động chuẩn

Chọn củ cải làm đơn vị tham chiếu: hạt **20 G**, bán chuẩn **42 G**, trưởng thành sau **3 lần chuyển ngày**, mỗi ô thu một sản phẩm. Một vụ tạo lãi gộp **22 G/ô**, chưa trừ tiền thuê, công lao động và vật tư.

Các khoản khác phải được so với lượng lãi người chơi thật sự tạo được, không đặt giá chỉ vì con số trông hợp lý.

Phân biệt rõ:

- **Lãi gộp sản phẩm** = tiền bán − hạt/đầu vào − phí sản xuất trực tiếp.
- **Lợi nhuận hoạt động** = tổng lãi gộp − thuê đất − lương NPC − chi vận hành khác.
- **Dòng tiền cuối kỳ** = tiền đầu kỳ + tiền đã nhận − tiền đã trả, bao gồm mua máy và phí mở đất.
- Mua máy làm giảm tiền mặt nhưng chuyển một phần giá trị sang tài sản. Không được vừa trừ toàn bộ tiền mua máy khỏi tài sản ròng, vừa bỏ qua giá trị thanh lý của nó.

### 6.2. Bộ cây trồng đầu tiên

- **Củ cải:** giống 20 G; bán chuẩn 42 G; 3 ngày; lãi gộp 22 G/vụ, khoảng 7,33 G/ô/ngày. Dành cho xoay vòng tiền và sửa sai.
- **Ngô:** giống 45 G; bán chuẩn 88 G; 5 ngày; lãi gộp 43 G/vụ, khoảng 8,6 G/ô/ngày. Dành cho đơn hàng trung hạn.
- **Hành:** giống 55 G; bán chuẩn 115 G; 6 ngày; lãi gộp 60 G/vụ, khoảng 10 G/ô/ngày. Khóa vốn gần hết một kỳ thuê.
- **Bí:** giống 90 G; bán chuẩn 190 G; 9 ngày; lãi gộp 100 G/vụ, khoảng 11,11 G/ô/ngày. Không kịp tạo tiền cho kỳ thuê đầu nếu trồng ngày 1.

Các tỷ lệ trên chưa xét biến động giá, chi phí chăm sóc, thời gian làm tay hay ngày đất bị trống. Cây dài ngày được lợi nhuận danh nghĩa cao hơn để bù khóa vốn, nhưng phải có giới hạn nhu cầu và rủi ro bán sai thời điểm.

Không thêm quá sớm cây tái sinh vô hạn, buff chồng nhau hoặc chất lượng nhiều cấp: chúng làm khó việc xác định nguồn lợi nhuận vượt chuẩn.

### 6.3. Kịch bản dòng tiền bảy ngày đã tính

Giả định chỉ trồng 24/48 ô để giữ thời gian cho hoạt động khác; không tính thu nhập hang, hợp đồng hoặc quà nhiệm vụ. Giá bán cố định 42 G và không có thất thoát.

- Đầu ngày 1: **3.000 G**.
- Ngày 1 mua 24 hạt: −480 G → còn **2.520 G**.
- Ngày 4 thu hoạch và bán: +1.008 G → **3.528 G**.
- Ngày 4 trồng lại 24 hạt: −480 G → **3.048 G**.
- Ngày 7 bán vụ thứ hai: +1.008 G → **4.056 G**.
- Dự phòng vật tư/dịch vụ đã sử dụng trong kỳ: −80 G → **3.976 G**. Đây là giả định chi tiêu, không phải một loại thuế tự động mới.
- Cuối ngày 7 trả thuê: −300 G → **3.676 G**.

Kết quả: doanh thu **2.016 G**, chi giống **960 G**, chi khác **380 G**, tăng tiền mặt **676 G**.

Nếu thuê NPC giá 45 G/ngày trong 6 ngày, trả thêm 270 G, tiền cuối kỳ còn **3.406 G**, lợi nhuận 406 G. Khoản 270 G phải đổi lấy thời gian để khai khoáng, làm hợp đồng, hoặc giá trị tiện lợi mà người chơi thực sự muốn. Không tự cộng lợi nhuận phụ chưa được tạo ra.

Nếu giá bán giảm 20% còn 33,6 G, lợi nhuận cùng kế hoạch chỉ còn **272,8 G**, hoặc **2,8 G nếu vẫn thuê NPC sáu ngày**. Đây là cảnh báo cân bằng: thuê người trên diện tích quá nhỏ có thể gần như hòa vốn.

Nếu trồng đủ 48 ô, hai vụ tạo 4.032 G doanh thu, 1.920 G chi giống; giả định chi khác tăng lên 160 G và thuê vẫn 300 G thì lợi nhuận là **1.652 G** trước lương. Đây là kịch bản cường độ lao động cao, không được coi là mức mặc định của mọi người chơi.

Các phép tính giữ số thập phân để phân tích; game lưu tiền theo đơn vị nhỏ nhất và chỉ làm tròn tại bước quyết toán đã thống nhất.

### 6.4. Ngân sách an toàn

Hiển thị “tiền có thể đầu tư” bằng:

**Tiền mặt − nghĩa vụ trong 7 ngày tới − chi phí vụ tiếp theo đã lên kế hoạch.**

Đây là lời khuyên trên giao diện, không khóa tiền khỏi người chơi. Nếu mua máy khiến ngân sách âm, hiện số thiếu và ngày đến hạn ngay trong màn mua.

Mục tiêu khởi đầu: người mới dùng một kế hoạch cơ bản vẫn trả được thuê; người giỏi tích lũy nhanh hơn nhờ sử dụng thêm diện tích, thời gian và thị trường. Không đặt tiền thuê ở mức bắt mọi người phải tối ưu tuyệt đối mới sống được.

## 7. Mở rộng đất và chi phí quy mô

Mỗi lô mở rộng thêm **24 ô**. Tối đa **4 lô mở rộng/người/mùa** trong bản đầu.

Giá cấp quyền khai thác trực tiếp, khi thị trường đất còn lô:

- Lô thứ nhất: 1.500 G.
- Lô thứ hai: 2.500 G.
- Lô thứ ba: 4.000 G.
- Lô thứ tư: 6.000 G.

Thuê cơ sở mỗi lô là **180 G/kỳ**. Phụ thu quản lý chỉ áp dụng trên từng lô vượt hai lô mở rộng:

**Tiền thuê lô k = 180 × [1 + 0,25 × max(0, k − 2)].**

Với k là thứ tự lô mở rộng đang giữ, bốn lô lần lượt đóng 180, 180, 225, 270 G. Tổng thuê khi có đủ bốn lô và đất nền là **1.155 G/kỳ**. Khi trả hoặc mất một lô, tính lại thứ tự từ kỳ kế tiếp; không truy thu hồi tố.

Khu đất nhận giữa kỳ trả phần thuê tương ứng số ngày sở hữu; màn xác nhận phải hiện số tiền chính xác. Không thu cả tuần đối với đất vừa nhận cuối ngày 7.

**Thiết kế nguồn cung:** mỗi người có quyền nhận lô mở rộng đầu tiên đến hết ngày 14. Lô thứ hai trở đi đến từ quỹ đất chung có giới hạn. Như vậy người chơi chậm không bị chặn hoàn toàn ngay đầu mùa, nhưng đất tốt vẫn có cạnh tranh.

Lô chuyên biệt có lợi thế nhỏ và nhược điểm rõ: đất giữ ẩm giảm thao tác tưới nhưng thuê cao hơn; đất gần chợ giảm thời gian vận chuyển nhưng ít ô; đất gần hang tiện khai khoáng nhưng không tăng năng suất cây. Chỉ đưa các biến thể này vào sau khi cân bằng lô tiêu chuẩn.

## 8. Lao động NPC: mua thời gian bằng tiền

Không có người giúp việc sản xuất miễn phí. Người chơi vẫn được tự lao động bằng công cụ tay, không phải trả phí cho từng hành động của mình.

Các gói thử nghiệm:

- **Thợ làm đất:** 45 G/ngày; tối đa 48 thao tác cuốc đất hợp lệ.
- **Thợ gieo trồng:** 45 G/ngày; tối đa 48 thao tác gieo; dùng hạt trong kho người thuê.
- **Thợ chăm cây:** 45 G/ngày; tối đa 48 thao tác tưới hoặc chăm sóc hợp lệ.
- **Thợ thu hoạch:** 55 G/ngày; tối đa 48 thao tác thu hoạch và chuyển hàng vào kho.
- **Thợ đa năng:** 90 G/ngày; tối đa 72 thao tác tổng, theo danh sách ưu tiên; không phải 72 thao tác cho từng nghề.

Một ô cần cuốc, gieo và tưới tiêu tốn ba thao tác. Năng suất trên là trần thiết kế, còn cần kiểm thử đường đi và thời gian thao tác để bảo đảm NPC thực hiện được trong ngày.

Lương được đặt cọc trước ngày làm. Không đủ tiền thì không gia hạn, không tạo nợ lương ngầm. Có hợp đồng theo ngày hoặc tối đa bảy ngày; phần chưa làm được hoàn nếu hủy trước khi bắt đầu ngày tương ứng.

Giao diện cho biết công việc, diện tích, vật tư thiếu và thời điểm dự kiến xong. Nếu không có hạt hay đường bị chặn, NPC báo lỗi, không tự mua đồ. Người chơi phải chịu lương cho thời gian đã thuê; lỗi kỹ thuật của game cần cơ chế hoàn phí.

Giới hạn bản đầu: tối đa hai NPC/trại. Mở rộng lên bốn sau khi hệ thống lập lịch ổn định. Nguồn lao động cơ bản không bị một người mua hết; chỉ lao động chuyên gia ở giai đoạn sau mới có nguồn cung cạnh tranh.

## 9. Máy móc: mua năng lực, không chế tạo máy từ quặng

Máy nền chỉ có từ cửa hàng hoặc giao dịch máy cũ hợp lệ. Không có công thức “quặng + gỗ = máy hoàn chỉnh”.

Danh mục đầu tiên:

- **Bộ tưới cơ giới:** mua 2.400 G; phục vụ tối đa 48 ô đúng vùng phủ; vận hành 10 G/ngày sử dụng. Giá trị chính là tiết kiệm thao tác, không tự tăng giá bán cây.
- **Máy chế biến nhỏ:** mua 2.200 G; xử lý tối đa 6 củ cải/ngày. Một củ cải giá chuẩn 42 G thành sản phẩm giá chuẩn 58 G, tốn 4 G phí/vật tư mỗi sản phẩm. Giá trị tăng thêm chuẩn là 12 G/đơn vị, tối đa 72 G/ngày.
- **Máy xới:** mua 1.800 G; tối đa 48 ô/ngày có vận hành; 15 G/ngày sử dụng. Chỉ làm đất, không thay máy gieo hay máy tưới.

Ví dụ hoàn vốn: máy chế biến cần khoảng **31 ngày chạy đủ công suất** để bù 2.200 G chỉ bằng 72 G tăng thêm mỗi ngày, chưa tính sửa chữa hay chênh lệch giá. Trong mùa 28 ngày, nó không nên được quảng bá là luôn sinh lời; giá trị thanh lý, thời điểm mua và hợp đồng chế biến mới quyết định có đáng mua hay không.

Đây là điểm cần playtest: nếu mọi người đều bỏ qua máy vì hoàn vốn quá chậm, giảm giá hoặc tăng giá trị hợp đồng ở phiên bản tiếp theo. Không ngầm tăng năng suất mà bỏ qua ảnh hưởng lạm phát.

Máy và NPC tưới phải có vùng phân công để không tính hai lần lợi ích trên cùng ô. Người chơi thiếu tiền vận hành thì máy dừng, không tạo nợ bất ngờ.

Máy chỉ hao mòn khi hoạt động. Trước ngưỡng bảo dưỡng phải có cảnh báo ít nhất ba ngày sử dụng dự kiến. Bản đầu có thể dùng chi phí vận hành đại diện bảo dưỡng và chưa triển khai hỏng máy để giảm phức tạp.

## 10. Hang mỏ và chế tạo vẫn có vai trò

Hang tạo một nghề kinh tế bổ sung, không phải đường tắt bỏ qua việc mua máy.

Nguyên liệu có ba đầu ra:

1. Bán nguyên liệu cho NPC trong hạn mức hoặc cho người chơi khác.
2. Chế tạo hàng tiêu hao như phân khoáng, vật tư sửa chữa và bộ lọc.
3. Chế tạo module hỗ trợ máy đã mua, ví dụ giảm nhẹ tiêu hao hoặc tăng độ bền.

Module không tự hoạt động nếu thiếu máy nền. Giới hạn cộng dồn để chi phí vận hành không giảm về 0 và không xuất hiện dây chuyền tự sinh tiền.

Ví dụ công thức thử: hai quặng sắt, mỗi quặng có giá bán tham chiếu 15 G, cộng 10 G phí gia công tạo một phụ tùng bán tham chiếu 55 G. Lãi tăng thêm so với bán nguyên liệu là **15 G**, không phải 45 G. Thời gian chế tạo và nhu cầu thị trường tiếp tục giới hạn lợi nhuận.

Người khai khoáng bán phụ tùng cho chủ trại; chủ trại bán lương thực cho người khai khoáng. Vật phẩm tiêu hao biến mất khi dùng, giúp duy trì nhu cầu thay vì chỉ giao dịch một lần rồi thị trường chết.

Thời gian, thể lực, độ sâu và giới hạn mua của NPC cần ngăn khai khoáng trở thành nguồn G vô hạn vượt xa trồng trọt. Thu nhập hang phải được đo theo G/phút sau chi phí, không chỉ theo giá một viên quặng.

## 11. Thị trường, hợp đồng và sự phụ thuộc giữa người chơi

### 11.1. Ba kênh bán hàng

**Thương lái NPC:** nhanh, giá công khai, hạn mức mua mỗi mặt hàng. Hạn mức cơ bản theo trang trại bảo đảm một mức đầu ra tối thiểu; phần mua thêm dùng nhu cầu chung của phòng.

**Chợ người chơi:** người mua đặt hoặc nhận lệnh bán. Phí giao dịch **3%**, tính một lần trên giá trị khớp và thu ở người bán. Hàng và tiền phải được ký quỹ; hủy lệnh chưa khớp trả lại tài sản.

**Hợp đồng cung ứng:** trả tốt hơn bán thông thường nếu giao đủ loại, chất lượng và thời hạn. Một phần hợp đồng riêng giúp người chơi mới tiến lên; hợp đồng giá trị cao là mục tiêu cạnh tranh chung.

Giao dịch người chơi chỉ chuyển tiền đang tồn tại. Bán cho NPC và nhận thưởng hệ thống mới tạo G mới. Cần phân biệt hai loại khi đo lượng tiền trong nền kinh tế.

### 11.2. Biến động giá dễ hiểu

Giá NPC từng ngày nằm trong **80%–120% giá chuẩn**. Công bố giá trước khi người chơi bán; lượng bán hôm nay ảnh hưởng giá ngày mai. Mức thay đổi thông thường không vượt 10% giá chuẩn mỗi ngày.

Giá đi xuống khi lượng giao vượt nhu cầu, đi lên khi thiếu cung. Hạn mức mỗi trại và độ trễ một ngày ngăn người online sớm hưởng toàn bộ giá tốt, đồng thời hạn chế thao túng tức thời.

Khi hết hạn mức, hàng còn lại vẫn lưu kho hoặc bán cho người khác; không có NPC mua vô hạn tại giá sàn. Vì vậy dòng tiền mẫu ở mục 6 chỉ hợp lệ khi hạn mức cho phép bán đủ 48 củ cải trong kỳ. Với phòng đông, quy mô nhu cầu phải tăng theo số người chơi hoạt động.

Dự báo ba ngày gồm nhóm nhu cầu tăng/giảm, không tiết lộ hoàn toàn giá tương lai. Không dùng cú sụt giá không báo trước đủ mạnh để tự nó gây tịch thu tài sản.

### 11.3. Đơn hàng tạo cạnh tranh

Ví dụ: “Giao 40 củ cải trong ba ngày, nhận 1.900 G”. Giá tham chiếu hàng là 1.680 G; phần thưởng tăng thêm chỉ 220 G. Phải tính cả chi phí gieo, vận chuyển và nguy cơ giao trễ để đánh giá hợp đồng.

Hợp đồng cạnh tranh được mở nhận trong một cửa sổ thời gian, không trao hoàn toàn cho người bấm nhanh nhất. Có thể chấm đề nghị theo mức giá yêu cầu và thời điểm giao cam kết, với trọng số công khai; bản đầu dùng lựa chọn giá đơn giản và hòa điểm thì bốc thăm minh bạch.

Người nhận ký quỹ 10% tiền hợp đồng. Giao đủ được hoàn cọc và nhận tiền; thất bại mất cọc, không trừ thêm một khoản nợ ẩn. Hàng giao thiếu chưa được thanh toán phải trả về, tránh vừa mất hàng vừa mất toàn bộ cọc ngoài luật công bố.

Để tránh nhận tất cả rồi giữ chỗ, mỗi người chỉ có một hợp đồng cạnh tranh đang thực hiện. Hợp đồng cơ bản không bị người đứng đầu độc chiếm.

## 12. Tiền thuê, nợ và cưỡng chế

### 12.1. Ví tiền không âm

Tiền mặt tối thiểu là 0. Phần thuê không trả được chuyển thành **công nợ thuê đất** theo từng kỳ và từng lô. Không dùng một ví âm để trộn nợ thuê với tiền mua hàng.

Cuối kỳ, hệ thống thanh toán bằng tiền còn lại sau các khoản ký quỹ đã tồn tại hợp lệ. Hóa đơn chưa đủ tiền chuyển sang nợ. Trả nợ ưu tiên hóa đơn cũ nhất. Nếu đã nợ, không cho tạo ký quỹ đấu giá mới để né thu nợ.

Bản đầu **không có lãi kép và không có phí phạt nợ**, nhằm làm luật dễ hiểu và không gây vòng xoáy phạt. Động lực trả nợ đến từ hạn chế mở rộng và nguy cơ mất quyền khai thác.

### 12.2. Máy trạng thái xử lý nợ

**Bình thường:** không có hóa đơn quá hạn.

**Cảnh báo:** xuất hiện khoản thuê chưa thanh toán. Hiện tiền thiếu, tuổi nợ, nguồn thu sắp về và thời điểm có thể bị xử lý.

**Hạn chế đầu tư:** nợ gốc thuê đạt ít nhất một kỳ tiền thuê hiện tại. Không nhận thêm đất hoặc tham gia đấu giá mua; vẫn được sản xuất, bán hàng, bán tài sản và trả nợ.

**Đủ điều kiện cưỡng chế:** đồng thời thỏa hai điều kiện:

- Nợ gốc thuê còn lại ít nhất bằng **hai kỳ thuê** của danh mục đất tại thời điểm cảnh báo cưỡng chế.
- Hóa đơn cũ nhất còn nợ đã quá hạn **ít nhất 14 ngày game**.

Khi đạt điều kiện, ghi cố định ngưỡng dùng cho hồ sơ này và gửi thông báo cuối **3 ngày game**. Mọi trả nợ trước khi chuyển quyền đều được ghi nhận. Hủy cưỡng chế nếu khoản nợ giảm xuống dưới ngưỡng hoặc không còn hóa đơn quá hạn đủ 14 ngày; trường hợp còn nợ vẫn ở trạng thái hạn chế.

**Tái cơ cấu:** hết thời hạn thông báo mà vẫn vi phạm thì chọn một lô mở rộng đưa ra thanh lý. Không tịch thu tất cả cùng lúc. Hoàn tất một lô rồi mới xét nhu cầu xử lý tiếp.

Ví dụ không thanh toán từ ngày 7: ngày 14 mới quá hạn bảy ngày, chưa đủ điều kiện tuổi nợ; cuối ngày 21 mới đủ 14 ngày, sau đó còn ba ngày thông báo. Như vậy người chơi có thời gian phản ứng và không bị tịch thu ngay sau hai lần quên trả.

### 12.3. Tài sản nào bị xử lý?

Khu khởi nghiệp 48 ô, công cụ tay và kho cơ bản **không bị tịch thu**. Các lô mở rộng bị thu hồi quyền khai thác trước. Người chơi được chọn lô tự nguyện trả/bán trong giai đoạn cảnh báo nếu đáp ứng giá trị cần xử lý.

Máy thuộc sở hữu cá nhân không tự động đi kèm đất. Người chơi có thời gian chuyển máy và thu hoạch cây đã chín. Cây chưa chín bị mất khi bàn giao được thông báo trong hồ sơ; không trả bồi thường vô hạn theo giá mùa vụ dự kiến. Nếu người chơi tự đồng ý bán một gói kèm máy, gói đó phải liệt kê riêng.

Không thể chuyển đất sắp bị cưỡng chế cho bạn bè với giá tượng trưng. Quyền khai thác chỉ chuyển qua hệ thống đất có định giá và nhật ký, không qua quà tặng trực tiếp.

### 12.4. Cơ hội trở lại

Nếu chỉ còn đất khởi nghiệp và vẫn đạt điều kiện cưỡng chế, chuyển sang chương trình tái thiết thay vì game over:

- Ngừng phát sinh tiền thuê mới trên khu nền trong thời gian tái thiết.
- Ghi lại nợ cũ và trả dần bằng **20% doanh thu bán hàng**, tối đa bằng số nợ còn lại.
- Cung cấp công việc NPC trả tiền theo lao động thực tế; không phát tiền miễn phí có thể gửi cho người khác.
- Có gói hạt khẩn cấp chỉ dùng trên đất nền, không bán hoặc chuyển được; giới hạn một gói mỗi kỳ và chỉ khi không đủ đầu vào tối thiểu.
- Không mở đất, đấu giá mua hoặc giao dịch tặng tài sản trong trạng thái này.
- Khi hết nợ, quay lại hợp đồng thuê thông thường từ kỳ tiếp theo.

Tái thiết không làm người chơi biến mất khỏi cuộc chơi, nhưng mất quy mô, thời gian và vị trí cạnh tranh là hậu quả thật. Khoản hỗ trợ không được tính vào điểm đóng góp kinh tế để tránh cố tình phá sản kiếm lợi.

## 13. Đấu giá đất

### 13.1. Đối tượng và người tham gia

Đấu giá **quyền khai thác lô đất**, không phải bán người chơi hoặc chuyển toàn bộ tài khoản. Người mua là các chủ trại đủ điều kiện; NPC Hội đồng có thể thu hồi khi không có người mua, không giả làm đối thủ để đẩy giá.

Hồ sơ hiển thị diện tích, đặc tính, phí thuê sau khi tính danh mục đất của người mua, thời điểm bàn giao, cây/tài sản đi kèm và giá khởi điểm. Không công khai toàn bộ lịch sử tài chính riêng tư của chủ cũ.

### 13.2. Luật phiên đầu tiên

- Công bố trước ít nhất hai ngày game; mở nhận giá kín trong một ngày game.
- Người tham gia không có nợ quá hạn, còn hạn mức đất và đủ tiền mặt.
- Khóa toàn bộ giá đặt cao nhất. Cho nâng giá trong phiên nhưng không rút hoặc hạ giá đã cam kết.
- Kiểm tra sau khi khóa tiền vẫn đủ tiền thuê kỳ kế tiếp đã biết; phần dự trữ này cũng bị giữ đến lúc kết phiên để tránh vượt kiểm tra rồi tiêu hết.
- Người đặt cao nhất thắng và trả **đúng giá mình đặt**. Người thua được hoàn ngay toàn bộ tiền khóa; người thắng được giải phóng phần dự trữ thuê.
- Nếu bằng giá, bốc thăm bằng cơ chế máy chủ có nhật ký; không thưởng cho đường truyền nhanh hơn.
- Chủ cũ không được tự đấu giá mua lại chính lô của mình trong hồ sơ đó.

Giá kín giảm lợi thế thao tác vào giây cuối và dễ hoàn thiện hơn đấu giá trực tiếp có gia hạn. Mức đặt giá của đối thủ không được công khai trước khi đóng phiên.

### 13.3. Tiền thanh lý đi đâu?

Phân bổ theo thứ tự:

1. Phí đấu giá **5%** giá bán, công khai trước phiên.
2. Trả nợ thuê của chủ cũ, tối đa bằng số nợ thực tế.
3. Tiền dư trả lại chủ cũ.

Không có giá bán được ghi nhận thì không tự tạo doanh thu thanh lý. Nếu tiền thu không đủ trả nợ, phần còn lại vẫn là công nợ.

Nếu không có giá hợp lệ, Hội đồng thu hồi quyền khai thác và ghi giảm nợ theo **giá thanh lý bảo thủ đã công bố**, không phát tiền mặt dư. Giá này bằng 50% giá cấp quyền chuẩn của loại lô, không dựa vào giá người chơi vừa thổi lên. Ghi giảm tối đa bằng nợ còn lại. Sau đó Hội đồng có thể mở lại lô vào kỳ sau.

Nếu đấu giá bị hủy do chủ cũ trả đủ điều kiện trước bàn giao, hoàn toàn bộ tiền khóa của người dự thầu. Việc thu tiền thắng, trừ nợ và chuyển quyền phải là một giao dịch nguyên tử, không để vừa mất tiền vừa không nhận đất.

## 14. Xếp hạng vàng kiếm được và danh hiệu

### 14.1. Điểm xếp hạng và các chỉ số tham khảo

**Điểm xếp hạng = max(0, tổng vàng đã kiếm được − nợ thuê trại chính còn lại).** Tổng kiếm được tăng khi bán hàng hoặc nhận tiền giao hàng. Mua vật phẩm, trả tiền thuê đúng hạn và đặt giá đấu chỉ làm đổi vàng trong ví, không làm giảm điểm. Khi không đủ tiền thuê trại chính vào kỳ hạn theo ngày game của người chơi, phần thiếu thành nợ và trừ vào điểm; trả nợ thì điểm hồi tương ứng.

Ngoài điểm chính, hồ sơ có thể hiển thị các chỉ số tham khảo:

- **Tài sản ròng:** mức giàu có có thể quy đổi theo định giá bảo thủ, dùng để phân tích cân bằng chứ không xếp hạng.
- **Thanh khoản:** tiền mặt so với nghĩa vụ trong bảy ngày tới; dùng nhãn an toàn/căng thẳng/rủi ro thay vì buộc công khai ví tiền chi tiết.
- **Uy tín kinh doanh:** tỷ lệ hoàn thành hợp đồng và lịch sử trả đúng hạn.

**Tài sản ròng = tiền mặt + tiền ký quỹ thuộc sở hữu + giá trị thanh lý máy + giá trị bảo thủ hàng tồn + giá trị chuyển quyền đất − tổng nợ.**

Không cộng tiền ký quỹ hai lần nếu số dư hiển thị đã bao gồm nó. NPC thuê không phải tài sản. Cây chưa thu hoạch không được định giá trong bản đầu để tránh sai lệch do thời tiết và thời gian.

Giá định giá đề xuất:

- Máy: 60% giá niêm yết chuẩn × tỷ lệ tình trạng, không theo giá mua bán giữa bạn bè.
- Hàng tồn: 70% mức thấp hơn giữa giá chuẩn và giá NPC bình quân bảy ngày; phiên chưa đủ bảy ngày dùng số ngày đã có.
- Quyền đất: 50% giá cấp quyền chuẩn của loại lô; không lấy giá thắng đấu giá làm giá trị tự động.
- Tài sản chỉ nhận qua hỗ trợ và không thể chuyển nhượng không được cộng vào tài sản ròng.

Giá trị tài sản có thể giảm theo giá thanh lý; phép định giá này không làm đổi điểm bảng hạng. Giao diện cần phân biệt rõ tổng vàng kiếm được, vàng trong ví, nợ thuê và giá trị tài sản.

### 14.2. Thang danh hiệu dài hạn

- Dưới 10.000 G: **Nông dân khởi nghiệp**.
- Từ 10.000 G: **Chủ trại**.
- Từ 50.000 G: **Điền chủ**.
- Từ 200.000 G: **Đại địa chủ**.
- Từ 1.000.000 G: **Triệu phú nông nghiệp**.
- Từ 10.000.000 G: **Ông trùm nông nghiệp**.
- Từ 1.000.000.000 G: **Tỷ phú**.
- **Lãnh chủ hành tinh:** danh hiệu chiến dịch tương lai, cần nội dung liên hành tinh và mục tiêu lãnh thổ; không mở chỉ bằng một ngưỡng tiền trong bối cảnh nông thôn hiện tại.

Đây là thang định hướng cho tiến trình dài hạn. Không tăng giá mọi thứ vô lý chỉ để ép người chơi chạm mốc tỷ phú sớm. Giữ huy hiệu “cấp cao nhất từng đạt”, nhưng cấp tài sản hiện tại có thể giảm.

### 14.3. Kết quả mùa cạnh tranh

Nếu triển khai mùa 28 ngày hoạt động, chốt **điểm tổng vàng kiếm được trừ nợ thuê còn lại** sau khi hoàn tất giao dịch và quyết toán ngày cuối. Chỉ so sánh những người chơi ở cùng số ngày hoạt động. Nếu hòa điểm, dùng thứ tự tên tài khoản như bảng hạng hiện tại; luật đồng hạng cho giải mùa cần được quy định riêng.

Trao thêm giải chuyên môn: tỷ lệ hợp đồng hoàn thành tốt nhất với số đơn tối thiểu; lợi nhuận trên ô đất; giá trị sản phẩm chế biến bán ra; thành tích phục hồi sau tái cơ cấu. Mỗi giải có tiêu chí công khai, không gộp thành một điểm tổng khó hiểu.

Danh hiệu cung cấp ngoại hình, biển hiệu và lịch sử thành tích. Không cộng năng suất vĩnh viễn cho người giàu nhất vì điều đó làm khoảng cách tự khuếch đại.

## 15. Vì sao người chơi cần cạnh tranh và hợp tác?

Các điểm va chạm chủ động là đất tốt hữu hạn, hợp đồng giá trị cao, nhu cầu thị trường dùng chung và phiên đấu giá. Không cần phá cây hay trộm đồ để tạo xung đột.

Các hướng chơi phải có cơ hội tồn tại:

- **Chủ trại thận trọng:** cây ngắn ngày, ít nợ, nhiều tiền dự phòng; có khả năng mua tài sản khi đối thủ thiếu thanh khoản.
- **Người mở rộng:** nhiều đất, sản lượng lớn; phải quản lý nhân công và chi phí cố định.
- **Nhà chế biến:** ít diện tích hơn, mua nguyên liệu của người khác; phụ thuộc chênh lệch giá và công suất máy.
- **Nhà khai khoáng:** khai thác và cung cấp vật tư; phụ thuộc thời gian, thể lực và sức mua.
- **Nhà cung ứng:** đọc nhu cầu, mua bán và hoàn thành hợp đồng; chịu phí và rủi ro tồn kho.

Không tạo “nghề đầu cơ” có lợi nhuận chắc chắn bằng mua cửa hàng rồi bán ngay cho NPC khác. Chênh lệch giá phải tính phí, nhu cầu và thời gian.

Ví dụ tương tác: A chuyên trồng củ cải nhưng thiếu máy; B có máy chế biến nhưng thiếu nguyên liệu; C khai quặng để bán phụ tùng; D giữ tiền để giành lô đất. A và B hợp tác hôm nay, nhưng có thể cạnh tranh hợp đồng ngày mai. D mua được đất vẫn cần nguồn hàng và thời gian, không tự động thắng.

## 16. Chống độc quyền, thông đồng và mất cân bằng

- Quyền lô mở rộng đầu tiên và đầu ra NPC cơ bản bảo vệ khả năng tham gia của người mới.
- Giới hạn bốn lô mở rộng, thuê tăng theo quy mô và số hợp đồng đang giữ hạn chế chiếm hết tài nguyên.
- Không có buff năng suất trực tiếp theo thứ hạng tài sản.
- Mùa cạnh tranh bắt đầu ngang nhau, không cho chuyển tiền và tài sản từ thế giới cũ.
- Không cho quà tặng tiền/tài sản trực tiếp trong mùa xếp hạng; giao dịch qua chợ có ký quỹ và lịch sử.
- Không dùng giá giao dịch giữa người chơi làm giá định giá tài sản, tránh mua bán lòng vòng nâng điểm.
- Tài trợ khởi đầu và gói cứu trợ không chuyển giữa tài khoản. Phòng xếp hạng đóng danh sách sau khi bắt đầu để hạn chế tạo nhân vật mới bơm vốn.
- Thu thập dấu hiệu giao dịch vòng tròn, bán giá bất thường và phối hợp đấu giá để phục vụ kiểm tra; không khẳng định chỉ một giới hạn giá có thể ngăn mọi thông đồng.
- Không thay đổi luật thuê đất giữa mùa để nhắm vào người đang dẫn đầu. Điều chỉnh cân bằng theo phiên bản và áp dụng mùa sau.

Trong phòng bạn bè tự quản, thông đồng vẫn có thể xảy ra. Xếp hạng công khai diện rộng cần thêm xác thực tài khoản, giám sát giao dịch và quy trình xử lý; đó là phạm vi vận hành riêng, chưa được giải quyết chỉ bởi GDD.

## 17. Giao diện và thông tin người chơi cần biết

**HUD:** tiền mặt có thể dùng; nợ thuê; ngày đến hạn; tiền cần giữ; cảnh báo việc NPC/máy dừng.

**Sổ tài chính:** thu chi theo ngày và nguồn; số lời từ bán hàng sau đầu vào; đầu tư tài sản tách riêng; lịch thu hoạch trước kỳ thuê. Dự báo phải ghi rõ giả định giá bán và không bảo đảm bán hết hàng.

**Màn mua đất/máy:** giá mua, chi phí định kỳ, công suất, tiền còn lại và dự trữ thiếu sau mua. Máy tưới hiển thị thao tác tiết kiệm, không hiển thị “lợi nhuận bảo đảm”.

**Màn thuê NPC:** công việc, lương, số ngày, vật tư cần, trần thao tác và vùng làm việc.

**Màn nợ:** hóa đơn cũ nhất, tổng gốc, điều kiện cưỡng chế, số ngày còn lại và nút trả nợ/bán tài sản/xem việc làm.

**Màn đấu giá:** đầy đủ hồ sơ, tiền bị khóa, tiền thuê mới, thời điểm đóng, luật hòa giá và điều kiện hoàn tiền.

Ví dụ thông báo tốt: “Bạn còn thiếu 360 G cho kỳ thuê cuối ngày 14. Vụ củ cải dự kiến thu hoạch ngày 13 có thể mang về 504 G ở giá hiện tại.” Tránh chỉ hiện chữ đỏ “Sắp phá sản” mà không giải thích cách khắc phục.

## 18. Nguồn sinh tiền, nơi tiêu tiền và kiểm soát lạm phát

**Tiền mới sinh ra:** NPC mua hàng, thưởng hợp đồng hệ thống, công việc tái thiết và vốn khởi nghiệp.

**Tiền rời nền kinh tế:** mua hạt/vật tư từ NPC, thuê đất, lương NPC, mua máy từ cửa hàng, phí cấp quyền đất của Hội đồng, gia công, vận hành và phí chợ/đấu giá.

**Tiền chỉ chuyển chủ:** mua bán giữa người chơi, phần tiền đấu giá trả lại chủ đất cũ. Phần trả nợ thuê và phí đấu giá mới là tiền bị loại khỏi lưu thông.

**Tài sản bị tiêu hao:** phân bón, phụ tùng, vật tư; đây là vật phẩm rời hệ thống, cần theo dõi riêng với G.

Không đặt mục tiêu mọi kỳ phải triệt tiêu hết lượng tiền sinh ra: người chơi cần tích lũy để phát triển. Quan sát tốc độ tăng tài sản, sức mua và giá chợ; thay đổi hạn mức NPC, nguồn cung hợp đồng và chi phí ở phiên bản tiếp theo nếu xuất hiện tích lũy quá nhanh.

Không dùng tiền thuê tăng bí mật hoặc giảm giá toàn bộ hàng tồn đột ngột để sửa lạm phát ngay giữa mùa.

## 19. Phạm vi thực hiện và thứ tự xây dựng

### Giai đoạn A — Chứng minh vòng lặp kiếm và tiêu

Một trang trại; đồng hồ ngày; ví; mua hạt; hai loại cây; bán hàng NPC; thuê bảy ngày; sổ thu chi; một NPC đơn nghề; một máy mua bằng tiền. Chơi được hai kỳ thuê liên tiếp và phân biệt tiền mặt với lợi nhuận.

### Giai đoạn B — Chứng minh cạnh tranh

Phòng bốn người; ngày game riêng của từng người; lô mở rộng hữu hạn; giá thị trường có giới hạn; hợp đồng; chợ ký quỹ; xếp hạng theo vàng kiếm được trừ nợ thuê; kết mùa sau 28 ngày hoạt động của mỗi người. Tăng lên tám người chỉ sau khi kiểm tra đồng bộ và quy mô nhu cầu.

### Giai đoạn C — Chứng minh hậu quả và tái thiết

Công nợ theo hóa đơn; cảnh báo; giới hạn đầu tư; cưỡng chế từng lô; đấu giá kín; thanh toán nguyên tử; tái thiết. Dùng một bộ dữ liệu nợ giả lập để kiểm thử sớm thay vì chờ hàng giờ mỗi lần.

### Để sau bản đầu

Vay ngân hàng, lãi suất, bảo hiểm, lao động hiếm, liên minh sở hữu chung, thị trường liên máy chủ, nhiều tiền tệ, MMO liên tục và thuộc địa hành tinh. Các hệ thống này hấp dẫn nhưng làm tăng mạnh nguy cơ lỗi giao dịch và mất cân bằng.

## 20. Tiêu chí kiểm thử trước khi chốt giá

Các mục tiêu dưới đây là điều kiện thử nghiệm đề xuất, chưa phải kết quả đạt được:

1. Ít nhất 90% người mới hoàn thành hướng dẫn và trả được kỳ thuê đầu mà không cần cứu trợ.
2. Người chơi hiểu được số tiền phải giữ lại và giải thích được vì sao mình lãi nhưng thiếu tiền mặt.
3. Không có một cây hoặc nghề luôn mang lại G/phút cao nhất trong mọi trạng thái thị trường sau khi trừ chi phí.
4. Thuê NPC tạo giá trị đo được: thời gian tiết kiệm, sản lượng tăng hoặc mức tiện lợi được người chơi đánh giá đáng tiền.
5. Người dẫn đầu ngày 7 không luôn thắng mùa. Chạy nhiều trận với người có kỹ năng tương đương, không suy luận từ một trận mẫu.
6. Người bị thu hồi một lô vẫn có lựa chọn kiếm tiền khả thi trong kỳ tiếp theo; không tồn tại trạng thái không có tiền, không có hạt và không có việc làm.
7. Không kiếm lời tức thời bằng mua–bán vòng giữa NPC, hoàn tiền, hủy hợp đồng hoặc chuyển phòng.
8. Đặt nhiều giá đồng thời không chi vượt số dư; mất kết nối không nhân đôi tiền; tải lại không thu tiền thuê lần hai.
9. Trả nợ sát thời điểm cưỡng chế, đấu giá không người mua, hòa giá, hủy phiên và chuyển quyền đều cho kết quả xác định.
10. Kiểm tra kịch bản giá thấp 20%, mất một vụ, chỉ canh tác nửa diện tích, thuê NPC dư nhu cầu và mở đất quá sớm.

Chỉ số cần ghi: G tạo mới/G tiêu hủy theo nguồn, lợi nhuận ròng theo nghề và theo phút hoạt động, tiền thuê trên lãi gộp, sử dụng công suất máy/NPC, tỷ lệ nợ và phục hồi, tỷ lệ hợp đồng theo nhóm người chơi, mức tập trung đất, giao dịch bất thường và tỷ lệ bỏ chơi sau thất bại.

Mọi điều chỉnh phải có câu hỏi cụ thể, chẳng hạn: “NPC 45 G/ngày có quá đắt cho trại 24 ô?” hoặc “Máy chế biến 2.200 G có đủ giá trị trong mùa 28 ngày?”. Không chỉ tăng phần thưởng đồng loạt khi người chơi nói game khó.

## 21. Định hướng trải nghiệm cuối cùng

Người chơi nên nhớ mùa đầu bằng những quyết định của chính mình: đã giữ lại tiền thuê thay vì mua máy quá sớm; đã bán quặng để cứu vụ mùa; đã thuê một người chăm cây để kịp giao hàng; đã mua được lô đất tốt trong phiên đấu giá; hoặc đã mất một lô nhưng quay lại nhờ chuyên môn mới.

**Tiền là quyền lựa chọn. Đất là cam kết chi phí. NPC và máy móc là cách mua thời gian. Thị trường là nơi các lựa chọn của người chơi gặp nhau.**

Khi bốn yếu tố này hoạt động cùng nhau, cạnh tranh đến từ khả năng điều hành và thích nghi, còn nông trại vẫn giữ được cảm giác gần gũi, sáng tạo và đáng để chăm sóc.
