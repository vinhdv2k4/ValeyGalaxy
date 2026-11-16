from docx import Document
from docx.shared import Inches, Pt, RGBColor
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.enum.table import WD_TABLE_ALIGNMENT, WD_CELL_VERTICAL_ALIGNMENT
from docx.oxml import OxmlElement
from docx.oxml.ns import qn

OUT = 'docs/Bao_cao_tien_do_Sprint_2_tuan.docx'
d = Document()
s = d.sections[0]
s.page_width, s.page_height = Inches(8.5), Inches(11)
s.top_margin, s.bottom_margin = Inches(.72), Inches(.65)
s.left_margin = s.right_margin = Inches(.82)
normal = d.styles['Normal']
normal.font.name, normal.font.size = 'Arial', Pt(10)
normal.font.color.rgb = RGBColor(20, 20, 20)
normal.paragraph_format.space_after = Pt(5)
normal.paragraph_format.line_spacing = 1.16
for name, size in [('Title', 16), ('Heading 1', 11.5), ('Heading 2', 10)]:
    st = d.styles[name]
    st.font.name, st.font.size = 'Arial', Pt(size)
    st.font.bold = True
    st.font.color.rgb = RGBColor(0, 0, 0)
    st.paragraph_format.space_before = Pt(10 if name != 'Title' else 0)
    st.paragraph_format.space_after = Pt(4)
    st.paragraph_format.keep_with_next = True

def p(txt, style=None): return d.add_paragraph(txt, style)
def h(txt, level=1): return d.add_heading(txt, level)
def table(headers, rows, widths=None):
    t = d.add_table(rows=1, cols=len(headers))
    t.style = 'Table Grid'
    t.alignment = WD_TABLE_ALIGNMENT.CENTER
    t.autofit = False
    for i, header in enumerate(headers):
        c = t.rows[0].cells[i]
        c.text = header
        if widths: c.width = Inches(widths[i])
        c.vertical_alignment = WD_CELL_VERTICAL_ALIGNMENT.CENTER
        shading = OxmlElement('w:shd'); shading.set(qn('w:fill'), 'DDE7F0'); c._tc.get_or_add_tcPr().append(shading)
        for run in c.paragraphs[0].runs: run.bold = True
    for row in rows:
        cells = t.add_row().cells
        for i, value in enumerate(row):
            cells[i].text = str(value)
            cells[i].vertical_alignment = WD_CELL_VERTICAL_ALIGNMENT.CENTER
            if widths: cells[i].width = Inches(widths[i])
    for row in t.rows:
        for cell in row.cells:
            for para in cell.paragraphs:
                para.paragraph_format.space_after = Pt(2)
                para.paragraph_format.line_spacing = 1.1
                for run in para.runs: run.font.size = Pt(8.5)
    d.add_paragraph().paragraph_format.space_after = Pt(1)
    return t

title = p('BÁO CÁO TIẾN ĐỘ SPRINT HAI TUẦN', 'Title')
title.alignment = WD_ALIGN_PARAGRAPH.CENTER
sub = p('Đồ án game nông trại 2D phát triển bằng Unity')
sub.alignment = WD_ALIGN_PARAGRAPH.CENTER
sub.runs[0].bold = True
table(['Thông tin', 'Nội dung'], [
    ('Sinh viên hoặc nhóm', '[Điền họ tên và mã số sinh viên]'),
    ('Giảng viên hướng dẫn', '[Điền họ tên]'),
    ('Thời gian Sprint', '[Điền ngày bắt đầu] đến [điền ngày kết thúc]'),
    ('Ngày lập báo cáo', '11/10/2026'),
    ('Công nghệ và phạm vi', 'Unity, C#; scene GamePlay, hệ thống gameplay nông trại')
], [1.8, 4.9])

h('Tóm tắt điều hành')
p('Trong Sprint này, project đã hình thành các hệ thống nền tảng cho vòng chơi nông trại: tài nguyên tương tác trên bản đồ, công cụ, vật phẩm và túi đồ. Commit 5bcb1e1 ngày 08/10/2026 là mốc lưu các nhóm chức năng này. Sau mốc đó, bản làm việc tiếp tục phát triển thao tác trồng trọt, điều khiển và hiển thị nhân vật, cùng cơ chế câu cá. Các phần đang sửa chưa được coi là hoàn tất vì chưa có kết quả kiểm thử Unity hoặc bản demo được ghi nhận trong repo. Sprint tiếp theo cần ưu tiên tích hợp, kiểm thử luồng chơi từ đầu đến cuối và lưu một bản ổn định để trình diễn.')

h('1 Thông tin và mục tiêu Sprint')
p('Sprint Goal: tạo nền gameplay để người chơi chọn công cụ, tác động lên tài nguyên hoặc ô đất và nhận phản hồi tương ứng trên bản đồ hoặc trong túi đồ. Mục tiêu mở rộng là bắt đầu phát triển câu cá. Báo cáo sử dụng mã nguồn, scene, prefab và lịch sử Git hiện có làm bằng chứng. Repo không lưu kế hoạch Sprint ban đầu, phân công cá nhân hoặc biên bản nghiệm thu; vì vậy danh sách dưới đây là backlog được tái dựng từ phần việc thực tế, không được trình bày như cam kết ban đầu.')

h('2 Sprint Backlog và trạng thái')
table(['Mã', 'Hạng mục và tiêu chí đầu ra', 'Trạng thái'], [
    ('SP-01', 'Tài nguyên nông trại và mỏ: có lớp tương tác, loại tài nguyên cụ thể và vật phẩm rơi', 'Đã có trong commit'),
    ('SP-02', 'Công cụ và túi đồ: dữ liệu vật phẩm, thao tác ô đồ, thanh công cụ và giao diện', 'Đã có trong commit'),
    ('SP-03', 'Nhân vật: điều khiển, hướng tương tác, hoạt ảnh và kết nối scene', 'Đang chỉnh sửa'),
    ('SP-04', 'Trồng trọt: cuốc đất, gieo hạt, tưới nước và hiển thị trạng thái ô đất', 'Đang chỉnh sửa'),
    ('SP-05', 'Câu cá: phát hiện nước, quăng cần, dữ liệu cá và minigame', 'Đang phát triển')
], [.62, 4.66, 1.42])
p('Trạng thái “đã có trong commit” xác nhận mã nguồn và tài nguyên đã được lưu vào lịch sử Git, chưa thay cho kết quả nghiệm thu chức năng. Hai trạng thái còn lại phản ánh các tệp đang thay đổi hoặc chưa được theo dõi trong bản làm việc hiện tại.')

h('3 Kết quả thực hiện')
h('3.1 Môi trường và khai thác tài nguyên', 2)
p('Project có lớp WorldResource dùng chung cho tài nguyên tương tác, với các thuộc tính như máu, loại công cụ yêu cầu và vật phẩm rơi. Các lớp chuyên biệt xử lý cây, đá, cỏ, cành cây, cỏ dại, bụi rậm và tài nguyên ở khu mỏ. FarmResourceSpawner và MineResourceSpawner đưa tài nguyên vào bản đồ; WorldItemDrop xử lý vật phẩm xuất hiện sau tác động. Cây có trạng thái thân và gốc, cùng xử lý hình ảnh khi đổ. Đây là cơ sở để người chơi dọn nông trại và thu thập nguyên liệu.')
h('3.2 Công cụ và túi đồ', 2)
p('Dữ liệu ItemData và ToolData biểu diễn vật phẩm và công cụ. PlayerInventory cùng InventoryModel hỗ trợ chọn ô, thêm, tiêu thụ và đổi vị trí vật phẩm; các thành phần UI hiển thị thanh công cụ, ô đồ và cửa sổ túi đồ. ItemUseCatalog và hành vi sử dụng hạt giống tách thao tác của vật phẩm khỏi giao diện. Cách tổ chức này giúp nối các nguồn vật phẩm từ môi trường vào một luồng quản lý chung và tạo chỗ mở rộng cho các loại vật phẩm tiếp theo.')
h('3.3 Nhân vật, trồng trọt và câu cá', 2)
p('Bản làm việc hiện tại cập nhật NetworkPlayerController, InputController, CharacterMotor, CameraFollow và các thành phần hoạt ảnh/ngoại hình để điều phối thao tác của nhân vật. CropPlot lưu ba trạng thái đã cuốc, đã gieo và đã tưới; PlayerFarmingInteractor xác định ô đất hoặc tài nguyên mà người chơi tác động. Thư mục Fishing có FishingController, WaterDetector, FishData và BobberBarMinigame. Các thành phần này cho thấy hướng triển khai đã rõ, nhưng cần thử trực tiếp trong scene GamePlay để xác nhận trải nghiệm hoàn chỉnh.')

h('4 Triển khai kỹ thuật')
p('Luồng tương tác chính được chia thành các tầng trách nhiệm. InputController và NetworkPlayerController nhận thao tác, chọn công cụ đang dùng và chuyển yêu cầu tới PlayerFarmingInteractor hoặc FishingController. Các đối tượng đích như CropPlot và WorldResource tự kiểm tra điều kiện, thay đổi trạng thái và tạo kết quả. PlayerInventory nhận hoặc tiêu thụ vật phẩm, còn UI đọc trạng thái túi đồ để cập nhật hình ảnh. Việc tách lớp điều khiển, đối tượng thế giới, dữ liệu vật phẩm và giao diện giúp giảm sự phụ thuộc trực tiếp giữa các hệ thống.')
p('Về dữ liệu, CropPlot dùng các biến trạng thái cho tiến trình làm đất, trồng và tưới. WorldResource định nghĩa hành vi dùng chung, còn các loại tài nguyên kế thừa để thay đổi phản ứng theo công cụ. FishingController giữ trạng thái quá trình câu, trong khi BobberBarMinigame xử lý logic trò chơi nhỏ. Tên “NetworkPlayerController” được giữ theo mã nguồn hiện có; báo cáo này không khẳng định tính năng multiplayer đã hoạt động vì chưa có bằng chứng kiểm thử mạng trong Sprint.')

h('5 Kiểm thử và minh chứng demo')
p('Bằng chứng hiện có là commit Git, mã nguồn C#, scene GamePlay và prefab nhân vật. Chưa tìm thấy báo cáo test case, ảnh chụp demo hoặc video xác nhận các luồng gameplay chạy đạt yêu cầu. Do đó các trường hợp dưới đây là kịch bản cần thực hiện khi nghiệm thu, không phải kết quả Pass đã được ghi nhận.')
table(['Kịch bản', 'Kết quả mong đợi', 'Trạng thái bằng chứng'], [
    ('Dùng công cụ với cây/đá', 'Đúng công cụ gây tác động; tài nguyên thay đổi và có vật phẩm rơi', 'Chưa ghi nhận test'),
    ('Thêm/đổi/dùng vật phẩm', 'Số lượng và ô chọn trong túi đồ cập nhật đúng', 'Chưa ghi nhận test'),
    ('Cuốc, gieo, tưới một ô đất', 'Ô đất đổi trạng thái và hiển thị tương ứng', 'Chưa ghi nhận test'),
    ('Câu tại vùng nước', 'Quăng cần, nhận tín hiệu cá cắn và vào minigame', 'Chưa ghi nhận test')
], [1.7, 3.75, 1.25])

h('6 Vấn đề và rủi ro')
p('Rủi ro lớn nhất hiện tại là tích hợp: nhiều hệ thống có mã nguồn riêng nhưng còn phải xác nhận chúng hoạt động liên tục trong scene. Các thay đổi trồng trọt và câu cá chưa được commit nên khó đối chiếu mốc hoàn thành hoặc khôi phục bản ổn định. Ngoài ra, báo cáo thiếu bằng chứng chạy thực tế. Hướng xử lý là lập checklist theo từng luồng chơi, ghi kết quả kiểm thử và lỗi cụ thể, hoàn thiện cấu hình prefab/scene, sau đó commit theo nhóm chức năng đã xác nhận.')

h('7 Đánh giá tiến độ và nhìn lại Sprint')
p('Theo backlog tái dựng từ project, 2 trong 5 hạng mục đã được lưu trong commit, 2 hạng mục đang chỉnh sửa và 1 hạng mục đang phát triển. Đây là số lượng trạng thái công việc, không phải tỷ lệ hoàn thành chất lượng hay khối lượng công sức. Sprint đã tạo được nền tảng kiến trúc gameplay và nhiều thành phần mã nguồn. Điểm cần cải thiện là bổ sung kiểm thử có ghi nhận, minh chứng demo và chia nhỏ mốc commit để tiến độ dễ theo dõi hơn.')

h('8 Kế hoạch Sprint tiếp theo')
p('Ưu tiên đầu tiên là hoàn thiện luồng cuốc đất – gieo hạt – tưới nước và kiểm tra sự thay đổi trạng thái ô đất. Tiếp theo là kiểm thử vòng câu cá từ chọn vùng nước, quăng cần, chờ cá cắn đến minigame và nhận vật phẩm. Song song, cần kiểm tra tương tác tài nguyên, túi đồ và hoạt ảnh nhân vật trong GamePlay, ghi rõ từng test case đạt hoặc lỗi. Kết quả mong đợi cuối Sprint tiếp theo là một bản demo ổn định, có commit tương ứng và bằng chứng ảnh hoặc video để trình bày với giảng viên.')

h('Phụ lục nguồn đối chiếu')
p('Commit: 5bcb1e1 (08/10/2026). Mã nguồn chính: Assets/Scripts/Runtime/WorldResources; Assets/Scripts/Runtime/Inventory; Assets/Scripts/Runtime/CropPlot.cs; Assets/Scripts/Runtime/PlayerFarmingInteractor.cs; Assets/Scripts/Runtime/Fishing. Scene: Assets/Scenes/GamePlay.unity. Trạng thái bản làm việc được kiểm tra ngày 11/10/2026.')

foot = s.footer.paragraphs[0]
foot.alignment = WD_ALIGN_PARAGRAPH.CENTER
run = foot.add_run('Báo cáo tiến độ Sprint hai tuần  |  Đồ án game nông trại')
run.font.name, run.font.size = 'Arial', Pt(8)
run.font.color.rgb = RGBColor(100, 100, 100)
d.save(OUT)
print(OUT)
