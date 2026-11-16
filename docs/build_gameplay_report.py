from pathlib import Path
# Dùng lại định dạng (font, heading, bảng, footer) của báo cáo ngày 01/11.
exec(Path('docs/build_next_sprint_report.py').read_text(encoding='utf-8').split("t=d.add_paragraph('BÁO CÁO")[0])
def page(title):
    para=h(title)
    para.paragraph_format.page_break_before=True

OUT=Path('docs/Bao_cao_tien_do_15_11_2026_Gameplay.docx')

q=d.add_paragraph('BÁO CÁO TIẾN ĐỘ ĐỒ ÁN',style='Title');q.alignment=WD_ALIGN_PARAGRAPH.CENTER
h('1 Thông tin kỳ báo cáo và mục tiêu')
for a,b in [('Sinh viên thực hiện','Lại Thành Vinh 20225954'),('Giảng viên hướng dẫn','Vũ Đức Vượng'),('Kỳ báo cáo','01/11/2026 – 15/11/2026'),('Phạm vi','Ứng dụng Unity C#, dịch vụ ASP.NET Core và dữ liệu trò chơi')]:
    x=p('');x.add_run(a+': ').bold=True;x.add_run(b)
p('Kỳ trước đã hoàn thành nền tảng trực tuyến, lưu tiến trình và hệ thống kinh tế cạnh tranh gồm bảng xếp hạng, thuê đất và đấu giá. Mục tiêu kỳ này là phát triển gameplay: mở rộng vòng chơi hằng ngày từ trồng trọt và mua bán thành nhiều hoạt động liên kết với nhau, gồm chăn nuôi, máy chế biến, khai mỏ và chiến đấu, câu cá, sự kiện theo lịch, lễ hội, cắt cảnh cột mốc và tiến trình kỹ năng. Mọi hành động làm thay đổi tài sản hoặc tiến độ đều được máy chủ kiểm tra rồi mới ghi, theo kiến trúc đã xây dựng ở kỳ trước.')
p('Kỳ này cũng giải quyết giới hạn đã nêu trong báo cáo trước: quyền đất thuê hoặc thắng đấu giá nay quyết định số ô người chơi được canh tác trên bản đồ.')

h('2 Đối chiếu với báo cáo trước')
tab(['Nội dung','Kế hoạch / tình trạng báo cáo trước','Kết quả kỳ này'],[
('Thành tựu','Kế hoạch: lưu tiến trình thành tựu theo tài khoản.','Máy chủ xét thành tựu theo tổng thu nhập, tình bạn, số công thức đã chế tạo, cá đã câu và nông sản đã giao. Trò chơi phát âm báo khi đạt; trang Collections hiển thị kết quả.'),
('Kỹ năng nông dân','Đã lưu XP năm kỹ năng; chưa có phần mở khóa.','Cấp kỹ năng tính theo 10 ngưỡng XP. Người chơi chọn nghề ở cấp 5 và cấp 10 trong màn tổng kết cuối ngày. Nghề làm tăng giá bán nhóm sản phẩm tương ứng; công thức chế tạo mở theo cấp kỹ năng.'),
('Đồ hỗ trợ trồng trọt','Kế hoạch: rương, phân bón, máy tưới, đồ bảo vệ cây.','Đặt được rương, máy tưới, hàng rào, sàn và máy chế biến lên nông trại. Phân bón tăng chất lượng, giữ ẩm và rút ngắn thời gian lớn của cây.'),
('Nấu ăn và buff','Kế hoạch: luồng nấu ăn và hiệu ứng món ăn.','Đã có chức năng ăn vật phẩm để hồi thể lực. Luồng nấu ăn và buff chưa hoàn thành, chuyển sang kỳ sau.'),
('Quyền đất trên bản đồ','Giới hạn: quyền đất chưa gắn với vùng canh tác.','Mỗi người có 15 ô cơ bản, thêm 15 ô cho mỗi lô đang thuê hoặc sở hữu. Khi còn nợ thuê, người chơi không cuốc và gieo thêm được.')],[1.2,2.3,3.4])

h('3 Công việc và trạng thái')
tab(['Mã','Hạng mục','Tiêu chí đánh giá','Trạng thái'],[
('01','Kỹ năng, nghề và thành tựu','XP lên cấp; chọn nghề cấp 5/10; thành tựu lưu theo tài khoản.','Đã triển khai'),
('02','Chăn nuôi và công trình trại','Xây, nâng cấp chuồng; mua vật nuôi; cho ăn; thu sản phẩm.','Đã triển khai'),
('03','Vật thể đặt và máy chế biến','Đặt, nhặt vật thể; nạp nguyên liệu; nhận thành phẩm theo thời gian.','Đã triển khai'),
('04','Trồng trọt nâng cao','Chất lượng nông sản; phân bón; công cụ tác động theo vùng.','Đã triển khai'),
('05','Khai mỏ, chiến đấu, thợ rèn','Xuống tầng lần lượt; quái vật; kho báu; bị hạ gục; nâng cấp công cụ.','Đã triển khai, tiếp tục cân bằng'),
('06','Câu cá','Minigame thanh phao; ghi nhận cá trên máy chủ.','Đã triển khai'),
('07','Lịch, sự kiện ngày, lễ hội, hái lượm','Sự kiện theo ngày mùa; hộp thư; sản vật hái lượm theo ô.','Đã triển khai, tiếp tục bổ sung nội dung'),
('08','Cắt cảnh cột mốc, Community Center, Joja','Engine cắt cảnh; đóng góp gói đồ; lộ trình thay thế.','Đã triển khai'),
('09','NPC và thế giới','Lịch trình, tìm đường, hội thoại; ánh sáng ngày đêm; mưa.','Đã triển khai'),
('10','Gắn quyền đất với ô canh tác','Giới hạn số ô theo lô đất; nợ thuê chặn mở rộng.','Đã triển khai'),
('11','Nấu ăn và buff','Giao diện nấu ăn; hiệu ứng món ăn.','Chưa hoàn thành'),
('12','Kiểm thử tự động','Bộ test backend biên dịch và chạy đạt.','Đang cập nhật')],[.45,1.85,3.2,1.4])

page('4 Kết quả lập trình trong kỳ')
h('4.1 Vòng chơi một ngày, thể lực và tổng kết đêm',2)
p('Đồng hồ trong game chạy theo khung giờ trong ngày, kết hợp với ánh sáng ngày đêm, đèn trong bản đồ và hiệu ứng mưa. Thanh thể lực hiển thị trên HUD; dùng công cụ tiêu hao thể lực, ăn vật phẩm thì hồi lại. Nếu người chơi còn ở ngoài nhà lúc 2 giờ sáng, nhân vật ngất và bị trừ một khoản vàng vào sáng hôm sau. Khi đi ngủ, màn tổng kết đêm liệt kê hàng đã giao và số tiền nhận được, báo kỹ năng lên cấp và yêu cầu chọn nghề nếu vừa đạt cấp 5 hoặc 10.')
h('4.2 Trồng trọt nâng cao và diện tích canh tác',2)
p('Cuốc và bình tưới tác động lên một vùng ô tùy bậc công cụ. Phân bón chia làm ba nhóm: tăng chất lượng, giữ ẩm và tăng tốc. Chất lượng nông sản lúc thu hoạch (thường, bạc, vàng, iridium) được tính trên máy chủ theo cấp Farming và loại phân đã bón. Số ô đang dùng gồm ô đã cuốc và ô có cây, và không được vượt hạn mức theo số lô đất; muốn thêm ô thì phải dọn ô trống hoặc thuê thêm đất trên TV.')
h('4.3 Chăn nuôi và công trình trang trại',2)
p('Người chơi đặt xây chuồng gà, chuồng gia súc và silo qua cửa hàng thợ mộc. Mỗi công trình cần vàng, gỗ, đá và một số ngày thi công. Công trình đã xây có thể nâng cấp, di chuyển, phá dỡ và sơn lại. Vật nuôi mua ở cửa hàng gia súc, được đặt tên và gán vào chuồng. Hằng ngày cần cho ăn cỏ khô từ silo (240 đơn vị mỗi silo) hoặc mở cửa chuồng cho vật nuôi ra ăn cỏ. Người chơi vuốt ve vật nuôi, nhặt trứng, vắt sữa bằng xô và xén lông bằng kéo. Máy chủ kiểm tra người chơi có đứng gần cửa chuồng hoặc vị trí sản phẩm hay không trước khi xử lý.')
h('4.4 Vật thể đặt, máy chế biến và chế tạo',2)
p('Mỗi người đặt tối đa 600 vật thể trong nông trại, nhà và chuồng. Mười loại máy chế biến hoạt động theo luật trong Artisan.json: nhận một lượng đầu vào, sau một số phút trả ra thành phẩm; một số máy cần thêm nhiên liệu. Sản phẩm chế biến từ nông sản có giá tính bằng hệ số nhân và phần cộng thêm trên giá nguyên liệu. Ngoài máy chế biến còn có máy tưới, hàng rào và cổng, sàn lát, máy lấy nhựa gắn trên cây sồi, phong và thông, bom phá đá và cầu thang xuống mỏ. Rương đặt ngoài trại hỗ trợ sắp xếp, gộp chồng và chuyển nhanh vật phẩm.')
h('4.5 Khai mỏ, chiến đấu, thợ rèn và câu cá',2)
p('Mỏ mở theo lịch sự kiện, có 120 tầng và chỉ được xuống lần lượt từng tầng. Mỗi tầng sinh đá, quặng, quái vật và có thể có rương kho báu; thang máy cho phép quay lại những tầng đã qua. Khi bị hạ gục, người chơi tỉnh lại ở cửa mỏ, mất một phần vàng và có thể mất vật phẩm trong túi, trừ công cụ và nhẫn. Thợ rèn nâng cấp công cụ trong một số ngày (người chơi quay lại nhận) và mở geode. Câu cá dùng minigame thanh phao: máy chủ ghi nhận lúc bắt đầu và kết thúc phiên câu, lưu loại cá đã bắt và cộng XP Fishing.')
h('4.6 Kỹ năng, nghề và thành tựu',2)
p('Năm kỹ năng Farming, Mining, Foraging, Fishing và Combat tăng cấp theo XP đã lưu. Ở cấp 5 người chơi chọn một trong hai nghề; ở cấp 10 chọn tiếp một trong hai nghề phát triển từ lựa chọn cấp 5. Nghề làm tăng giá bán nhóm sản phẩm liên quan như trứng và sữa, rau, trái cây, hàng chế biến, cá, thỏi kim loại và đá quý. Thành tựu được xét lại mỗi khi tiến độ thay đổi và chỉ trao một lần.')

page('5 Thiết kế kỹ thuật của gameplay')
h('5.1 Máy chủ quyết định kết quả',2)
p('Unity chỉ gửi yêu cầu hành động như gieo, thu, đặt vật thể, cho ăn hay mở rương kho báu. Máy chủ kiểm tra vị trí người chơi đã đồng bộ, khoảng cách tới đối tượng, ngày game, số dư và vật phẩm trong túi rồi mới ghi thay đổi. Các yêu cầu đều mang mã định danh để khi client gửi lại thì không bị xử lý hai lần. Kết quả ngẫu nhiên như chất lượng nông sản hay vật phẩm mất trong mỏ dùng bộ sinh số lấy hạt giống từ tài khoản, ngày và mã yêu cầu, nên gửi lại một yêu cầu không làm đổi kết quả.')
h('5.2 Nội dung tách thành dữ liệu',2)
p('Phần lớn nội dung gameplay nằm trong tệp dữ liệu thay vì viết cứng trong mã: Artisan.json (máy chế biến, phân bón, hệ số giá), RanchShop.json (vật nuôi, bản vẽ, kích thước công trình), Bundles.json (gói đóng góp), DailyEvents.json (lịch sự kiện và lễ hội), ForageTiles.txt (vị trí hái lượm) và Milestones.json (kịch bản cắt cảnh). Khi thêm nội dung chỉ cần bổ sung dữ liệu, không phải sửa luồng xử lý.')
h('5.3 Engine sự kiện và cắt cảnh',2)
p('Engine cắt cảnh áp dụng các nguyên lý hướng đối tượng. Điều kiện (thời gian, vị trí, tình bạn, thời tiết, cờ, điều kiện tổ hợp) và lệnh (di chuyển, quay hướng, nói, hỏi, biểu cảm, chuyển màn, âm thanh, nhạc) có hợp đồng chung và được tạo qua factory từ tên trong JSON. Nhóm lệnh tuần tự hoặc song song theo mẫu Composite. Runner chạy cây lệnh bất đồng bộ, đồng hồ dừng trong lúc trình diễn, và người chơi có thể bỏ qua. Tầng lõi không phụ thuộc Unity nên có thể kiểm tra kịch bản bằng công cụ dòng lệnh.')
tab(['Thành phần','Trách nhiệm'],[
('EventTriggerListener','Nhận tín hiệu đổi bản đồ, đổi giờ, cập nhật dữ liệu; đánh dấu kịch bản cần xét.'),
('EventConditionEvaluator','Kiểm tra điều kiện đã biên dịch với ngữ cảnh hiện tại.'),
('CutsceneRunner','Chạy lệnh tuần tự hoặc song song; quản lý trạng thái Normal, Cutscene, Dialogue.'),
('UnityCutsceneStage','Tạo diễn viên tạm, điều khiển camera, âm thanh và vị trí người chơi.'),
('Backend tiến độ','Kiểm tra điều kiện trao quyền, lưu lựa chọn và hiệu ứng lâu dài.')],[2.0,4.9])
h('5.4 Công cụ hỗ trợ kiểm tra',2)
p('Bảng quản trị trong game và API quản trị cho phép đổi ngày, thời tiết, tiền, vật phẩm, tình bạn, kích hoạt sự kiện, đặt trạng thái gói đóng góp, lưu và tải lại các mốc kiểm thử. Màn xem trước sự kiện giúp kiểm tra từng lễ hội mà không phải chơi đến đúng ngày.')

page('6 Sự kiện, lễ hội và cột mốc cốt truyện')
h('6.1 Lịch sự kiện hằng ngày và lễ hội',2)
p('Lịch sự kiện xếp hơn 40 mục theo bốn mùa, gồm các mốc mở khu vực (mỏ, sạt lở được dọn), thư gửi tới hộp thư nông trại, mùa hái quả và các lễ hội như lễ hội trứng, vũ hội hoa, lễ hội ẩm thực mùa hè, đêm sứa ánh trăng, hội chợ mùa thu và lễ hội mùa đông. Mỗi lễ hội có hành động riêng được máy chủ kiểm tra, ví dụ trưng bày 9 ô sản vật ở hội chợ hoặc tặng quà bí mật mùa đông. Sản vật hái lượm xuất hiện ở các ô định sẵn theo khu vực và mùa, được nhặt qua máy chủ để tránh nhặt trùng.')
h('6.2 Cắt cảnh cột mốc',2)
tab(['Cột mốc','Nội dung chơi'],[
('Nhận cần câu, mở mỏ, nhận kiếm','Mở hoạt động câu cá, khai mỏ và chiến đấu.'),
('Nhận thú cưng, chọn loại hang','Lựa chọn của người chơi được lưu theo tài khoản.'),
('Bản vẽ lò luyện','Xuất hiện sau lần khai thác quặng đồng đầu tiên; mở công thức lò.'),
('Community Center và Junimo','Mở bảng gói đóng góp sáu phòng.'),
('Lễ khánh thành / mở Joja','Kết thúc một trong hai lộ trình phát triển thị trấn.'),
('Đánh giá cuối năm 2','Chấm 1–4 nến theo tiến độ thu nhập, tình bạn, thành tựu.')],[2.3,4.6])
h('6.3 Hai lộ trình phát triển thị trấn',2)
p('Lộ trình Community Center yêu cầu đóng góp vật phẩm vào các gói của sáu phòng: nhà kho, phòng thủ công, bể cá, phòng lò hơi, két sắt và bảng thông báo. Hoàn thành gói thì nhận thưởng, đủ sáu phòng thì có lễ khánh thành. Lộ trình Joja yêu cầu mua thẻ thành viên 5.000 G rồi trả tiền cho từng dự án phòng. Người chơi phải chọn giữa bỏ công sức và tài nguyên để đóng góp hoặc chi vàng, và lựa chọn này ảnh hưởng tới quỹ tiền trong cuộc cạnh tranh trên bảng xếp hạng.')

page('7 Công thức gameplay chính')
h('7.1 Ngưỡng kinh nghiệm theo cấp kỹ năng',2)
tab(['Cấp','1','2','3','4','5','6','7','8','9','10'],[
('XP tích lũy','100','380','770','1.300','2.150','3.300','4.800','6.900','10.000','15.000')],[1.0]+[.59]*10)
p('Cấp được tính lại từ tổng XP mỗi khi máy chủ trả trạng thái, nên không thể chỉnh cấp riêng lẻ từ phía client. Đăng nhập, đổi bản đồ hay thao tác không cho ra sản phẩm đều không cộng XP.')
h('7.2 Chất lượng nông sản khi thu hoạch',2)
p('Gọi F là cấp Farming (0–10) và k là cấp phân bón chất lượng (0–3). Máy chủ tính:')
eq('p_vàng = 0,2 × (F / 10) + 0,2 × k × ((F + 2) / 12) + 0,01')
eq('p_bạc = min(0,75; 2 × p_vàng)')
p('Nếu dùng phân cấp 3, nông sản có xác suất p_vàng / 2 đạt iridium và không bao giờ ở mức thường. Ví dụ: người mới (F = 0, không bón) chỉ có khoảng 1% ra vàng; F = 5 bón phân cấp 1 cho khoảng 22,7%; F = 10 không bón cho 21%. Đầu tư phân bón giúp người chơi mới bớt thua thiệt về chất lượng so với người đã lên cấp cao.')
h('7.3 Phân bón tăng tốc và phạm vi công cụ',2)
eq('Số ngày lớn mới = max(1; g − ⌈g × s⌉), với s = 0,10 / 0,25 / 0,33')
p('Ví dụ súp lơ 12 ngày: bón loại 10% còn 10 ngày, loại 25% còn 9 ngày. Cây chín sớm hơn có thể thu trước kỳ thuê đất, giúp người chơi xoay vòng vốn nhanh hơn.')
tab(['Bậc công cụ','Thường','Đồng','Thép','Vàng','Iridium'],[
('Số ô tác động khi tụ lực','1','3','5','9','18')],[2.0,.95,.95,.95,.95,1.1])
h('7.4 Rủi ro khi bị hạ gục trong mỏ',2)
eq('Vàng mất = làm tròn xuống 100 G [min(5.000; ngẫu nhiên trong [Ví/20; Ví/4])]')
p('Mỗi vật phẩm có thể mất trong túi bị mất với xác suất 25%; công cụ và nhẫn được giữ lại. Người mang nhiều tiền xuống tầng sâu chịu rủi ro lớn hơn, nên phải cân nhắc giữa giá trị quặng, đá quý và nguy cơ mất vốn dành cho tiền thuê.')

page('8 Hiện trạng và giới hạn')
h('8.1 Phần đã hoàn thiện',2)
p('Vòng chơi đã đủ các hoạt động trồng trọt, chăn nuôi, chế biến, khai mỏ, câu cá, hái lượm, giao tiếp NPC và tham gia sự kiện. Tất cả đều nối với máy chủ, lưu theo tài khoản và tạo ra hàng hóa đưa vào nền kinh tế đã có (bán hàng, giao hàng, xếp hạng theo vàng kiếm được). Quyền đất trên TV nay có tác dụng thực tới diện tích canh tác.')
h('8.2 Giới hạn đang tồn tại',2)
tab(['Vấn đề','Mô tả','Hướng xử lý'],[
('Nấu ăn và buff','Mới có ăn để hồi thể lực; chưa có giao diện nấu và hiệu ứng tạm thời.','Thực hiện trong kỳ sau.'),
('Bộ test backend','Sau khi đổi tham số yêu cầu của chuồng trại, FarmRanchTests chưa được cập nhật nên dự án test chưa biên dịch.','Cập nhật test và chạy lại toàn bộ bộ test.'),
('Cân bằng mỏ và máy chế biến','Giá thành phẩm và độ khó các tầng mỏ chưa được đối chiếu với mô hình kinh tế cạnh tranh.','Mô phỏng dòng tiền có tính các nguồn thu mới.'),
('Thế giới chung','Vật nuôi, máy và cắt cảnh là riêng của từng người; người khác chỉ thấy nhân vật.','Xác định phần nào cần đồng bộ cho người cùng khu.')],[1.5,3.0,2.4])

page('9 Kế hoạch phát triển kỳ tiếp theo')
h('9.1 Mục tiêu',2)
p('Kỳ tới tập trung hoàn thiện chất lượng gameplay: bổ sung phần còn thiếu của hệ tiến trình, cân bằng lại kinh tế sau khi có thêm nhiều nguồn thu, và chuẩn bị bản chạy thử nhiều người chơi.')
h('9.2 Nội dung thực hiện',2)
tab(['Đầu mục','Công việc','Sản phẩm dự kiến'],[
('Nấu ăn và buff','Làm luồng nấu từ CookingRecipes với nguyên liệu trong túi hoặc tủ lạnh; định nghĩa buff tốc độ, may mắn, kỹ năng; lưu thời lượng hiệu ứng.','Giao diện bếp, món ăn trong túi, biểu tượng buff trên HUD.'),
('Cân bằng kinh tế mở rộng','Đưa sản phẩm chăn nuôi, hàng chế biến, khoáng sản và cá vào mô hình dòng tiền; xem lại giá và hạn mức bán.','Bảng giá đã kiểm chứng; không có nguồn thu áp đảo các nguồn khác.'),
('Kiểm thử và ổn định','Sửa bộ test; thêm test cho máy chế biến, chất lượng nông sản, phân bón, giới hạn ô đất.','Bộ test chạy đạt; danh sách lỗi đã xử lý.'),
('Trải nghiệm nhiều người','Hiển thị một số thay đổi chung trong cùng khu; chạy thử với nhiều tài khoản.','Biên bản chạy thử và các chỉnh sửa sau thử nghiệm.')],[1.35,3.1,2.45])
h('9.3 Kết quả kỳ sau',2)
p('Đầu ra là một bản chơi có vòng tiến trình hoàn chỉnh: hoạt động sản xuất tạo XP và thành tựu, cấp kỹ năng mở công thức và nghề, món ăn và vật dụng giúp làm việc hiệu quả hơn. Kinh tế sau khi cân bằng vẫn giữ thước đo xếp hạng theo vàng kiếm được trừ nợ thuê.')

f=sec.footer.paragraphs[0];f.alignment=WD_ALIGN_PARAGRAPH.CENTER
f.add_run('Báo cáo tiến độ đồ án  |  Trang ').font.size=Pt(9)
field=OxmlElement('w:fldSimple');field.set(qn('w:instr'),'PAGE');f._p.append(field)
d.core_properties.title='Báo cáo tiến độ đồ án – giai đoạn phát triển gameplay'
d.core_properties.subject='Chăn nuôi, chế biến, khai mỏ, câu cá, sự kiện, cắt cảnh và tiến trình kỹ năng'
d.save(OUT);print(OUT)
