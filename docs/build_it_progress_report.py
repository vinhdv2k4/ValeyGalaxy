from pathlib import Path
exec(Path('docs/build_next_sprint_report.py').read_text(encoding='utf-8').split("t=d.add_paragraph('BÁO CÁO")[0])
def page(title):
    para=h(title)
    para.paragraph_format.page_break_before=True

OUT=Path('docs/Bao_cao_tien_do_do_an_CNTT_01_11_2026.docx')
d.core_properties.title='Báo cáo tiến độ đồ án công nghệ thông tin'
d.core_properties.subject='Phát triển game nông trại nhiều người chơi và hệ thống kinh tế cạnh tranh'

q=d.add_paragraph('BÁO CÁO TIẾN ĐỘ ĐỒ ÁN CÔNG NGHỆ THÔNG TIN',style='Title');q.alignment=WD_ALIGN_PARAGRAPH.CENTER
q=p('Dự án game nông trại 2D nhiều người chơi');q.alignment=WD_ALIGN_PARAGRAPH.CENTER;q.runs[0].bold=True
h('1 Thông tin kỳ báo cáo và mục tiêu')
for a,b in [('Sinh viên thực hiện','[Họ tên – MSSV – Lớp]'),('Giảng viên hướng dẫn','[Họ tên giảng viên]'),('Kỳ báo cáo','Tiếp nối giai đoạn phát triển trước'),('Phạm vi','Ứng dụng Unity C#, dịch vụ ASP.NET Core và dữ liệu trò chơi')]:
    x=p('');x.add_run(a+': ').bold=True;x.add_run(b)
p('Mục tiêu kỳ này là đưa các cơ chế chơi ở kỳ trước vào một hệ thống có tài khoản, lưu tiến trình và nhiều người chơi; xây dựng nền xử lý giao dịch, sản xuất và đất. Dịch vụ đã lưu trạng thái và xử lý mua bán; người chơi nhìn thấy nhau trong cùng khu; TV trong game đã có bảng xếp hạng theo tổng vàng kiếm được sau khi trừ nợ thuê trại chính, cùng các mục thuê đất và đấu giá. Các thông số cân bằng kinh tế ở phần sau là đề xuất chưa tích hợp hết vào trò chơi.')
h('2 Đối chiếu với báo cáo trước')
tab(['Nội dung','Tình trạng báo cáo trước','Kết quả kỳ này'],[
('Nhân vật và tương tác','Di chuyển, hoạt ảnh, công cụ, tài nguyên.','Bổ sung tài khoản, hồ sơ nhân vật và hiện diện trực tuyến.'),
('Trồng trọt','Cuốc đất, gieo hạt, tưới đang tích hợp.','Thêm lưu cây, tăng trưởng theo ngày, thu hoạch và giao dịch sản phẩm.'),
('Túi đồ và vật phẩm','Có thao tác ô đồ và giao diện.','Thêm lưu trạng thái, rương, chuyển đồ, mua bán và giao hàng.'),
('Cạnh tranh','Chưa có nội dung kinh tế cụ thể.','Đã có bảng xếp hạng theo vàng kiếm được trừ nợ thuê, thuê đất và đấu giá trên TV.')],[1.2,2.55,3.15])
h('3 Công việc và trạng thái')
tab(['Mã','Hạng mục','Tiêu chí đánh giá','Trạng thái'],[
('01','Tài khoản và dữ liệu người chơi','Đăng nhập; tạo nhân vật; tải lại dữ liệu cá nhân.','Đã triển khai'),
('02','Sản xuất và giao dịch','Gieo, tưới, thu hoạch; mua bán; lưu tiền và vật phẩm.','Đã triển khai, tiếp tục hoàn thiện'),
('03','Hiện diện nhiều người','Hiển thị người chơi khác ở cùng khu.','Đã triển khai thử nghiệm'),
('04','Thuê đất, đấu giá, xếp hạng','TV hiển thị Top 10 theo vàng kiếm được trừ nợ thuê; giao dịch kiểm tra số dư, trả đất, hoàn tiền vượt giá.','Đã triển khai'),
('05','Cân bằng kinh tế','Vốn thống nhất; giá nông sản; thuê theo ngày game.','Đề xuất, chưa tích hợp')],[.45,1.65,3.4,1.4])

page('4 Kết quả lập trình trong kỳ')
h('4.1 Dịch vụ tài khoản và trạng thái trò chơi',2)
p('Dịch vụ phía máy chủ quản lý đăng ký, đăng nhập và hồ sơ nhân vật. Trạng thái người chơi gồm tiền, túi đồ, rương và tiến trình được lưu riêng theo tài khoản. Những thao tác thay đổi tài sản sử dụng yêu cầu định danh giao dịch để hạn chế xử lý lặp; cập nhật số dư có kiểm tra phiên bản dữ liệu nhằm phát hiện thay đổi đồng thời. Đây là cơ sở kỹ thuật để nhiều client dùng chung một nền kinh tế.')
h('4.2 Sản xuất, vật phẩm và NPC',2)
p('Luồng gieo hạt kiểm tra loại hạt, số lượng trong túi và khoảng cách tới ô canh tác; tưới cập nhật trạng thái; chuyển ngày chỉ tăng trưởng cây đã được tưới; thu hoạch kiểm tra độ chín và sức chứa túi đồ. Phần kinh tế hiện có mua, bán, thùng giao hàng và quyết toán hàng gửi khi chuyển ngày. Project còn bổ sung rương, chuyển vật phẩm, chế tạo, lưu kết quả câu cá và một số tương tác NPC. Các luồng này đã có mã nguồn kết nối Unity với dịch vụ và cần tiếp tục hoàn thiện trải nghiệm sử dụng trong game.')
h('4.3 Hiện diện và khu vực chung',2)
p('Khi người chơi vào màn chơi, client gửi vị trí, hướng nhìn, ngoại hình và khu vực lên dịch vụ để client khác hiển thị nhân vật tương ứng. Người ở khu khác không xuất hiện trên cùng màn hình. Hệ thống đã có khu vực, dịch chuyển và thành phần NPC, phục vụ việc hình thành một thế giới chung. Hiện việc đồng bộ người chơi chủ yếu là hiện diện; va chạm và toàn bộ hành động gameplay chưa được mô phỏng bởi một máy chủ thế giới chuyên dụng.')
h('4.4 Bảng xếp hạng theo vàng và giao dịch đất',2)
p('Giao diện TV đã có bảng xếp hạng, thuê hoặc trả đất và đấu giá. Server lấy tối đa 10 tài khoản, sắp theo tổng vàng kiếm được trừ nợ thuê trại chính; TV hiển thị hạng, tên nhân vật, điểm, tổng vàng kiếm được và nợ thuê. Mua sắm, trả thuê đúng hạn và đặt giá đấu không làm giảm tổng vàng kiếm được. Trại chính thu 150 G sau mỗi 7 ngày game đã chơi; phần thiếu thành nợ và được trừ khỏi điểm cho tới khi trả. Các lô đất trên TV hiện thu trước 200 G cho 7 ngày thực, tối đa ba lô. Đấu giá có giá khởi điểm 1.000 G, tăng tối thiểu 100 G và hoàn tiền khi bị vượt giá.')

page('5 Thiết kế kỹ thuật của hệ thống cạnh tranh')
h('5.1 Kiến trúc và trách nhiệm',2)
p('Unity phụ trách thao tác và phản hồi hình ảnh. Dịch vụ ASP.NET Core xác thực yêu cầu, kiểm tra điều kiện kinh tế và ghi dữ liệu. Cơ sở dữ liệu giữ trạng thái người chơi, quyền sử dụng đất, giao dịch tiền và phiên đấu giá. Để tránh hai người cùng thuê hoặc cùng thắng một lô, máy chủ cần kiểm tra quyền tại thời điểm ghi và hoàn tất chuyển quyền cùng giao dịch tiền. Dữ liệu gửi từ client là yêu cầu, không được tự quyết định số tiền hoặc chủ đất.')
h('5.2 Luồng giao dịch thuê đất',2)
p('Khi người chơi chọn đất, giao diện hiển thị phí thuê, thời hạn và tiền còn lại. Máy chủ xác thực tài khoản, kiểm tra lô còn trống, giới hạn đất và số dư; sau đó giữ quyền sử dụng lô và trừ tiền trong một giao dịch. Nếu một bước thất bại, giao dịch bị hủy để đất và ví không mâu thuẫn. Việc trả đất chỉ được chấp nhận với chủ hiện tại. Ở thiết kế mục tiêu, còn cần bổ sung hóa đơn tiền thuê định kỳ, lịch thu theo ngày game và xử lý nợ.')
h('5.3 Luồng đấu giá và xếp hạng',2)
p('Đấu giá có một bản ghi cho từng lô và thời điểm đóng phiên. Mỗi giá đặt được kiểm tra số dư và ghi nhận trên máy chủ; tiền của người không còn dẫn đầu được hoàn. Kết thúc phiên, hệ thống quyết toán và cấp quyền cho người thắng. Bảng xếp hạng đọc tổng vàng kiếm được và nợ thuê từ trạng thái người chơi trên máy chủ để mọi người nhìn thấy cùng kết quả. Đề xuất đấu giá kín là phần mở rộng sau này; thước đo xếp hạng vẫn là vàng kiếm được trừ nợ thuê chưa trả.')
h('5.4 Giới hạn tích hợp đang tồn tại',2)
p('Quyền thuê hoặc thắng đấu giá hiện chưa được thực thi xuyên suốt tới từng vùng canh tác của màn chơi; người có quyền cần thật sự được mở diện tích tương ứng. Cây và thuê trại chính cùng chạy theo ngày game của từng người; khi thiết kế mùa cạnh tranh cần so sánh người chơi ở cùng số ngày hoạt động để tránh lệch thời gian chơi.')

page('6 Cơ chế vốn khởi đầu và tiền thuê theo thiết kế mục tiêu')
h('6.1 Điểm xuất phát ngang nhau',2)
p('Bộ thiết kế kinh tế mới xác định mỗi người chơi vào mùa với 1.200 G, quyền sử dụng khu nền 48 ô và bộ công cụ cơ bản. Khoản vốn chỉ cấp một lần khi tạo trạng thái đầu mùa; đăng nhập lại phải tải số dư đã lưu. Hạt giống mua bằng tiền khởi đầu, nên người chơi phải quyết định bao nhiêu vốn được đầu tư và bao nhiêu được giữ làm dự phòng. Khi triển khai chế độ mùa, giá trị khởi đầu 500 G hiện có trong dịch vụ sẽ được thay bằng thông số thống nhất của mùa.')
h('6.2 Nghĩa vụ trả tiền thuê',2)
tab(['Diện tích','Phí nhận quyền chuẩn','Thuê mỗi 7 ngày game'],[
('Khu nền 48 ô','Cấp lúc bắt đầu','150 G'),('Lô mở rộng 1: 24 ô','900 G','85 G'),('Lô mở rộng 2: 24 ô','1.400 G','100 G'),('Lô mở rộng 3: 24 ô','2.150 G','120 G'),('Lô mở rộng 4: 24 ô','3.350 G','135 G'),('Tổng tối đa 144 ô','7.800 G cho 4 lô mở rộng','590 G')],[2.0,2.25,2.65])
p('Trại chính hiện thu 150 G sau khi xử lý doanh thu cuối kỳ, vào đầu ngày 8, 15, 22 và 29 theo ngày game của từng người. Các mức phí nhận quyền và thuê cho lô mở rộng trong bảng là thiết kế mục tiêu chưa tích hợp vào quyền đất trên bản đồ. Nếu tích hợp lô thứ nhất, tổng thuê dự kiến là 235 G mỗi kỳ; người thắng đấu giá cũng phải chịu thuê theo luật mục tiêu.')
h('6.3 Ví dụ có thể kiểm chứng',2)
p('Với 1.200 G, người mới mua 24 hạt củ cải giá 20 G sẽ chi 480 G và giữ 720 G. Nếu tưới đủ, trồng ngày 1 thì thu ngày 5; bán 24 củ ở giá 38 G thu 912 G. Sau khi trả 150 G tiền thuê, ví còn 1.482 G, tức tăng 282 G sau kỳ đầu. Nếu bán chỉ được 30 G mỗi củ, ví còn 1.290 G và lợi nhuận còn 90 G. Người trồng 48 ô có thể lãi 714 G ở giá chuẩn nhưng phải dùng 960 G cho hạt, chăm nhiều hơn và giữ ít tiền dự phòng.')
p('Backend không để ví âm khi thu thuê trại chính: phần thiếu được lưu thành nợ, TV hiển thị khoản nợ và cho phép trả bằng vàng đang có. Nợ còn lại trừ vào điểm bảng hạng, trả nợ thì điểm hồi tương ứng. Cảnh báo chi tiết, hạn chế mở đất và cưỡng chế lô mở rộng vẫn là thiết kế chưa lập trình đầy đủ.')

page('7 Công thức định giá nông sản và tác động cạnh tranh')
h('7.1 Nguyên tắc định giá',2)
p('Giá của cây trồng cần phản ánh thời gian tăng trưởng g, số ngày tái thu r, sản lượng kỳ vọng q, số lần thu hợp lệ N và bậc mở khóa t. Mức lãi gộp mục tiêu T được chặn trong 3,5–6,5 G mỗi ô mỗi ngày. Với thời gian khai thác H, đặt M = T × H; giá hạt S và giá bán cho NPC P được tính từ M, tỷ lệ vốn đặt vào hạt f, q và N. Bộ công thức dùng làm công cụ đề xuất giá khi thêm cây mới; nhà thiết kế vẫn phải kiểm tra rủi ro, mùa vụ, giới hạn mua và chi phí lao động.')
eq('T = clamp(4,5 + 0,64t + 0,035(g − 4) − 0,42I(r > 0); 3,5; 6,5)')
eq('S = làm tròn đến 5 G [M × f / (1 − f)]')
eq('P = làm tròn đến 1 G [(S + M) / (q × N)]')
p('q bao gồm sản lượng cơ bản và xác suất sản phẩm cộng thêm; N chỉ đếm các lần thu trước khi hết mùa. Nếu cây chín sau mùa, N bằng 0 và giá thiết kế không hợp lệ. Cây ăn quả, nông sản nhặt và sản phẩm chế biến cần thêm tuổi thọ, đầu vào hoặc công suất phù hợp. Vì vậy công thức có thể mở rộng cho danh mục nông sản nhưng không được hiểu là toàn bộ vật phẩm hiện có đã được tự động cân bằng.')
tab(['Cây dùng để hiệu chuẩn','Hạt','Bán chuẩn','Đặc điểm chiến thuật'],[
('Củ cải','20 G','38 G','4 ngày; xoay vòng vốn trước kỳ thuê đầu.'),
('Khoai tây','50 G','65 G/củ','6 ngày; có xác suất thêm củ, khóa vốn lâu hơn.'),
('Đậu que','60 G','30 G/lần thu','10 ngày đến vụ đầu; tái thu mỗi 3 ngày.'),
('Súp lơ','80 G','145 G','12 ngày; lãi mỗi vụ cao nhưng cần dự phòng thuê.')],[1.8,.65,1.1,3.35])
h('7.2 Ý nghĩa đối với trận nhiều người',2)
p('Cây ngắn ngày giúp bảo toàn khả năng trả thuê; cây dài ngày có thể lợi hơn nhưng buộc người chơi chờ tiền về. Người mở rộng sớm cần trả phí đất, mua hạt và chuẩn bị thêm tiền thuê; người giữ tiền có thể chờ cơ hội đấu giá. Để lợi nhuận không phình vô hạn, cây tái thu phải có giới hạn mùa và việc NPC mua hàng phải có hạn mức công khai. Những thông số này chưa được chốt thành luật cân bằng chính thức.')

page('8 Xếp hạng và đấu giá đất trong mùa cạnh tranh')
h('8.1 Thước đo thắng cuộc',2)
p('Thước đo xếp hạng của dự án là tổng vàng người chơi đã kiếm được từ bán hàng và giao hàng, trừ khoản nợ thuê trại chính chưa thanh toán; điểm thấp nhất là 0. Số vàng còn trong ví chỉ dùng để chi tiêu và trả nợ. Mua hạt, trả thuê đúng hạn, đặt giá đấu hoặc được hoàn giá đấu không thay đổi điểm. Khi bán hàng, tổng vàng kiếm được tăng; khi thiếu tiền thuê lúc sang ngày 8, 15, 22… điểm giảm bằng phần nợ còn lại và hồi phục khi trả nợ.')
p('Máy chủ lấy tối đa 10 người theo điểm giảm dần. Bảng hạng còn hiển thị tổng vàng kiếm được và nợ thuê để giải thích điểm; số lô đất đang giữ chỉ là thông tin tham khảo. Khi bằng điểm, API sắp theo tên tài khoản để kết quả ổn định. Ngày game của mỗi người chỉ chuyển khi người đó chơi và gửi yêu cầu chuyển ngày; không tự tạo nợ trong lúc offline.')
h('8.2 Luật đấu giá mục tiêu',2)
p('Đất đấu giá là quyền khai thác lô hữu hạn. Hồ sơ công bố diện tích, đặc tính, phí thuê sau khi nhận, giá khởi điểm và thời điểm bàn giao. Phiên được thông báo trước hai ngày game, mở nhận giá kín trong một ngày. Người tham gia phải không có nợ quá hạn, còn hạn mức đất và đủ tiền; máy chủ giữ giá đặt cùng dự phòng cho kỳ thuê tiếp theo. Người tham gia có thể tăng giá nhưng không hạ giá cam kết.')
p('Người đặt cao nhất thắng và trả đúng giá đã đặt; người thua được hoàn toàn bộ khoản giữ. Hòa giá được xử lý bằng cơ chế bốc thăm có lưu kết quả trên máy chủ. Thu tiền, trừ nợ nếu là đất thanh lý và chuyển quyền phải hoàn tất trong một giao dịch nhất quán. Khi đất thanh lý được bán, phí đấu giá 5% được thu trước, sau đó trả nợ thuê thực tế và phần còn lại về chủ cũ. Người thắng vẫn chịu tiền thuê trong các kỳ tiếp theo.')
h('8.3 Tác động tới hành vi người chơi',2)
p('Ví dụ hai người cùng có 2.000 G: A giữ tiền để mua hạt cho vụ sau; B đặt 1.100 G để giành lô đất 24 ô. Nếu thắng, B có tiềm năng sản lượng lớn hơn nhưng còn 900 G trước các khoản mua hạt và cần trả tổng thuê 235 G mỗi kỳ. A không có đất mới nhưng giữ tiền mặt và có thể vượt B về kết quả cuối mùa nếu đầu tư của B không thu hồi kịp. Sự cạnh tranh đến từ lựa chọn và giới hạn nguồn lực, không cần phá hoại tài sản của nhau.')

page('9 Kế hoạch phát triển kỳ tiếp theo')
h('9.1 Cơ sở triển khai',2)
p('Dự án đã lưu kinh nghiệm cho năm kỹ năng và hiển thị cấp độ trong trang nhân vật. Nguồn cộng kinh nghiệm hiện tập trung ở thu hoạch, thu thập tài nguyên và câu cá. Trang chế tạo đã dùng công thức mặc định, kiểm tra nguyên liệu và cập nhật túi đồ qua máy chủ. Bộ dữ liệu có công thức nấu ăn nhưng trò chơi chưa có luồng nấu ăn hoàn chỉnh; mục thành tựu cũng chưa lưu tiến trình riêng cho từng tài khoản.')
h('9.2 Nội dung thực hiện',2)
tab(['Đầu mục','Công việc của kỳ tiếp theo','Sản phẩm dự kiến'],[
('Tiến trình thành tựu','Thiết kế mốc cho trồng trọt, thu hoạch, bán hàng, khai khoáng, câu cá và chế tạo; cập nhật tiến độ từ hành động đã được máy chủ ghi nhận.','Hồ sơ thành tựu lưu theo tài khoản; mục Collections hiện mốc đang làm, mốc đã mở và phần thưởng như công thức hoặc vật phẩm trang trí.'),
('Kinh nghiệm kỹ năng','Hoàn thiện nguồn XP và ngưỡng cấp cho Farming, Mining, Foraging, Fishing; bổ sung Combat khi vòng chiến đấu hoạt động. Gắn cấp kỹ năng với khả năng mở công thức.','Trang Skills hiện XP, cấp và phần thưởng mở khóa; tiến trình được giữ khi đăng nhập lại, không tăng chỉ vì đổi map hay đăng nhập.'),
('Chế tạo đồ hỗ trợ trại','Rà soát mã vật phẩm và nguyên liệu giữa công thức, túi đồ và backend; mở công thức theo cấp kỹ năng hoặc thành tựu. Ưu tiên rương, phân bón, máy tưới và vật dụng bảo vệ cây.','Chế tạo tiêu hao đúng nguyên liệu; đồ đặt trên trang trại được lưu và có tác dụng thực lên ô trồng thuộc quyền người chơi.'),
('Nấu ăn và hiệu ứng đồ ăn','Đưa công thức từ CookingRecipes vào luồng riêng; dùng nông sản đã thu hoạch làm nguyên liệu. Xác định hồi thể lực và hiệu ứng hỗ trợ lao động nông trại.','Giao diện nấu ăn, món ăn trong túi và hiệu ứng khi dùng; thời lượng hiệu ứng gắn với phiên chơi của nhân vật.')],[1.15,3.15,2.6])
h('9.3 Kết quả kỳ sau',2)
p('Đầu ra là một hệ thống tiến trình gắn trực tiếp với vòng chơi: hành động làm nông tạo XP và thành tựu; cấp kỹ năng mở công thức; nguyên liệu từ nông trại được chế thành vật dụng hoặc món ăn giúp người chơi sản xuất hiệu quả hơn. Dữ liệu tiến trình thuộc tài khoản và tiếp tục được khi người chơi đăng nhập lại.')

f=sec.footer.paragraphs[0];f.alignment=WD_ALIGN_PARAGRAPH.CENTER
f.add_run('Báo cáo tiến độ đồ án CNTT  |  Trang ').font.size=Pt(9)
field=OxmlElement('w:fldSimple');field.set(qn('w:instr'),'PAGE');f._p.append(field)
d.save(OUT);print(OUT)
