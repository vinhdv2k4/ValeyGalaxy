from docx import Document
from docx.shared import Inches, Pt
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.enum.table import WD_TABLE_ALIGNMENT, WD_CELL_VERTICAL_ALIGNMENT
from docx.oxml import OxmlElement
from docx.oxml.ns import qn

OUT = 'docs/Bao_cao_Sprint_2_tuan.docx'
doc = Document()
sec = doc.sections[0]
sec.top_margin = Inches(.75)
sec.bottom_margin = Inches(.7)
sec.left_margin = Inches(.85)
sec.right_margin = Inches(.85)

styles = doc.styles
styles['Normal'].font.name = 'Arial'
styles['Normal'].font.size = Pt(10.5)
styles['Normal'].paragraph_format.space_after = Pt(5)
for name, size in [('Title', 17), ('Heading 1', 12)]:
    s = styles[name]
    s.font.name = 'Arial'
    s.font.size = Pt(size)
    s.font.bold = True
    s.font.color.rgb = None
    s.paragraph_format.space_before = Pt(10)
    s.paragraph_format.space_after = Pt(5)

p = doc.add_paragraph(style='Title')
p.alignment = WD_ALIGN_PARAGRAPH.CENTER
p.add_run('BÁO CÁO KẾT QUẢ SPRINT HAI TUẦN')
p = doc.add_paragraph()
p.alignment = WD_ALIGN_PARAGRAPH.CENTER
p.add_run('Dự án game nông trại phát triển bằng Unity').bold = True

table = doc.add_table(rows=2, cols=2)
table.alignment = WD_TABLE_ALIGNMENT.CENTER
table.style = 'Table Grid'
fields = [('Sinh viên / Nhóm', '........................................................'),
          ('Giảng viên hướng dẫn', '........................................................'),
          ('Thời gian Sprint', 'Từ .......... đến ..........'),
          ('Ngày báo cáo', '11/10/2026')]
for cell, (label, value) in zip([table.cell(0,0),table.cell(0,1),table.cell(1,0),table.cell(1,1)], fields):
    cell.text = f'{label}: {value}'
    cell.vertical_alignment = WD_CELL_VERTICAL_ALIGNMENT.CENTER
    for p in cell.paragraphs:
        p.paragraph_format.space_after = Pt(3)

doc.add_heading('1 Mục tiêu Sprint', 1)
doc.add_paragraph('Xây dựng các cơ chế gameplay nền tảng cho game nông trại: tương tác với tài nguyên trên bản đồ, sử dụng công cụ và túi đồ, thao tác trồng trọt, đồng thời phát triển bước đầu cơ chế câu cá.')

doc.add_heading('2 Công việc đã thực hiện', 1)
items = [
    ('Tài nguyên thế giới', 'Xây dựng các đối tượng cây, đá, cỏ, bụi rậm, cành cây và cỏ dại; xử lý va chạm công cụ, khai thác và tạo vật phẩm rơi. Có hệ thống sinh tài nguyên cho nông trại và khu mỏ.'),
    ('Công cụ và túi đồ', 'Tạo dữ liệu vật phẩm và công cụ; xử lý thêm, tiêu thụ, chọn và đổi vị trí vật phẩm. Xây dựng thanh công cụ, ô vật phẩm và giao diện túi đồ.'),
    ('Nhân vật và tương tác', 'Phát triển điều khiển di chuyển, hướng tương tác, hoạt ảnh sử dụng công cụ và phần hiển thị nhân vật trong scene gameplay.'),
    ('Trồng trọt', 'Phát triển các thao tác cuốc đất, gieo hạt và tưới nước; cập nhật trạng thái và hình ảnh ô đất.'),
    ('Câu cá', 'Đang xây dựng phát hiện vùng nước, quy trình quăng cần, dữ liệu cá và trò chơi nhỏ khi kéo cá.'),
]
for title, body in items:
    p = doc.add_paragraph(style='List Bullet')
    p.add_run(title + ': ').bold = True
    p.add_run(body)

doc.add_heading('3 Kết quả và mức độ hoàn thành', 1)
doc.add_paragraph('Các hệ thống tài nguyên nông trại, công cụ và túi đồ đã được lưu trong commit ngày 08/10/2026. Mã nguồn cho trồng trọt, điều khiển nhân vật và câu cá tiếp tục được chỉnh sửa trong bản làm việc hiện tại. Các phần này cần tích hợp và kiểm thử trong Unity trước khi xác nhận hoàn thành.')

doc.add_heading('4 Khó khăn và hướng xử lý', 1)
doc.add_paragraph('Nhiều cơ chế cùng tác động lên nhân vật, vật phẩm và đối tượng trong bản đồ, nên cần kiểm tra tính nhất quán giữa thao tác, hoạt ảnh và kết quả trong game. Hướng xử lý là kiểm thử từng luồng tương tác trong scene gameplay, sửa lỗi phát sinh và hoàn thiện kết nối giữa các hệ thống.')

doc.add_heading('5 Kế hoạch Sprint tiếp theo', 1)
for item in [
    'Hoàn thiện và kiểm thử cơ chế trồng trọt, đặc biệt là gieo hạt và tưới nước.',
    'Tích hợp và kiểm thử toàn bộ vòng chơi câu cá, từ quăng cần đến nhận vật phẩm.',
    'Kiểm tra hoạt ảnh, giao diện túi đồ và khả năng thu thập vật phẩm trong scene gameplay.',
    'Sửa lỗi, lưu các thay đổi đã kiểm thử và chuẩn bị bản demo cho buổi báo cáo tiếp theo.',
]:
    doc.add_paragraph(item, style='List Bullet')

footer = sec.footer.paragraphs[0]
footer.alignment = WD_ALIGN_PARAGRAPH.CENTER
footer.add_run('Báo cáo Sprint • Dự án game nông trại').font.size = Pt(9)
doc.save(OUT)
print(OUT)
