from docx import Document
from docx.shared import Inches, Pt, RGBColor
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.enum.table import WD_CELL_VERTICAL_ALIGNMENT

doc = Document()
sec = doc.sections[0]
sec.page_width, sec.page_height = Inches(8.5), Inches(11)
sec.top_margin = sec.bottom_margin = Inches(.75)
sec.left_margin = sec.right_margin = Inches(.85)
base = doc.styles['Normal']
base.font.name, base.font.size = 'Arial', Pt(10.5)
base.paragraph_format.line_spacing = 1.2
base.paragraph_format.space_after = Pt(7)
for name, size in [('Title', 17), ('Heading 1', 12), ('Heading 2', 10.5)]:
    st = doc.styles[name]
    st.font.name, st.font.size, st.font.bold = 'Arial', Pt(size), True
    st.font.color.rgb = RGBColor(0, 0, 0)
    st.paragraph_format.space_before = Pt(12 if name != 'Title' else 0)
    st.paragraph_format.space_after = Pt(5)
    st.paragraph_format.keep_with_next = True

p = doc.add_paragraph(style='Title')
p.alignment = WD_ALIGN_PARAGRAPH.CENTER
p.add_run('BÁO CÁO KẾT QUẢ SPRINT HAI TUẦN')
p = doc.add_paragraph('Dự án game nông trại phát triển bằng Unity')
p.alignment = WD_ALIGN_PARAGRAPH.CENTER
p.runs[0].bold = True
p.paragraph_format.space_after = Pt(15)

table = doc.add_table(rows=2, cols=2)
table.style = 'Light Shading Accent 1'
fields = [('Sinh viên hoặc nhóm', '[Điền tên]'), ('Giảng viên hướng dẫn', '[Điền tên]'),
          ('Thời gian Sprint', '[Điền ngày bắt đầu và kết thúc]'), ('Ngày báo cáo', '11/10/2026')]
for cell, (label, value) in zip([table.cell(0,0), table.cell(0,1), table.cell(1,0), table.cell(1,1)], fields):
    cell.vertical_alignment = WD_CELL_VERTICAL_ALIGNMENT.CENTER
    cell.text = ''
    para = cell.paragraphs[0]
    para.add_run(label + ': ').bold = True
    para.add_run(value)

def heading(text, level=1): doc.add_heading(text, level)
def para(text): doc.add_paragraph(text)

heading('1 Tóm tắt kết quả')
para('Trong Sprint hai tuần này, em tập trung xây dựng các cơ chế tạo nên vòng chơi cơ bản của một game nông trại: điều khiển nhân vật, tương tác với tài nguyên trên bản đồ, sử dụng công cụ, quản lý vật phẩm và thao tác trên ô đất. Bên cạnh đó, em bắt đầu phát triển hệ thống câu cá. Kết quả chính là các hệ thống tài nguyên, công cụ và túi đồ đã có mã nguồn cùng tài nguyên đi kèm trong phiên bản Git ngày 08/10/2026. Các thay đổi mới hơn về trồng trọt, nhân vật và câu cá đang ở bản làm việc hiện tại, cần tiếp tục tích hợp và kiểm thử trong Unity.')

heading('2 Mục tiêu và phạm vi Sprint')
para('Mục tiêu của Sprint là chuyển các thành phần đồ họa và dữ liệu đã chuẩn bị thành những tương tác có thể sử dụng trong gameplay. Người chơi cần di chuyển trong bản đồ, chọn công cụ từ túi đồ, tác động lên đối tượng phù hợp và nhận kết quả trực quan như hoạt ảnh hoặc vật phẩm rơi. Công việc tập trung vào mã nguồn Unity C# và scene gameplay. Báo cáo phản ánh trạng thái project hiện có; những tính năng chưa được kiểm thử đầy đủ sẽ được nêu rõ.')

heading('3 Nội dung triển khai')
heading('3.1 Tương tác với môi trường và tài nguyên', 2)
para('Em xây dựng lớp nền cho các tài nguyên có thể tương tác và triển khai các đối tượng như cây, đá, cỏ, bụi rậm, cành cây và cỏ dại. Từng đối tượng kiểm tra công cụ được sử dụng, tiếp nhận tác động, cập nhật hình ảnh hoặc trạng thái và tạo vật phẩm rơi khi phù hợp. Hệ thống sinh tài nguyên đưa các đối tượng vào nông trại và khu mỏ. Riêng cây có xử lý trạng thái gốc cây và hiệu ứng khi bị đốn, giúp kết quả khai thác dễ nhận biết hơn.')
heading('3.2 Công cụ, vật phẩm và túi đồ', 2)
para('Em tổ chức dữ liệu vật phẩm và công cụ thành các lớp riêng để thuận tiện mở rộng. Hệ thống túi đồ hỗ trợ chọn ô, đổi vị trí, thêm và tiêu thụ vật phẩm; phần giao diện gồm thanh công cụ, các ô vật phẩm và cửa sổ túi đồ. Việc tách dữ liệu, hành vi sử dụng và phần hiển thị giúp kết nối kết quả khai thác hoặc trồng trọt với túi đồ mà không dồn toàn bộ logic vào nhân vật.')
heading('3.3 Nhân vật và thao tác trồng trọt', 2)
para('Phần điều khiển nhân vật đang được cập nhật để xử lý hướng di chuyển, hướng thao tác, camera theo nhân vật và hoạt ảnh sử dụng công cụ. Đối với trồng trọt, mã nguồn hiện có các thao tác cuốc đất, gieo hạt và tưới nước. Ô đất theo dõi trạng thái đã cuốc, đã trồng và đã tưới để thay đổi hình ảnh. Những thay đổi này đang ở bản làm việc và cần kiểm thử trực tiếp trong scene để xác nhận các trạng thái chuyển đúng theo hành động của người chơi.')
heading('3.4 Phát triển bước đầu cơ chế câu cá', 2)
para('Em đã tạo các thành phần mã nguồn cho phát hiện vùng nước, điều khiển quá trình câu, dữ liệu cá, hiệu ứng mặt nước và trò chơi nhỏ khi kéo cá. Đây là phần đang phát triển. Báo cáo chỉ xác nhận các thành phần đã được viết trong project, chưa kết luận toàn bộ vòng chơi câu cá đã hoàn chỉnh hoặc ổn định trong bản demo.')

heading('4 Kết quả có thể đối chiếu')
para('Commit 5bcb1e1 ngày 08/10/2026 ghi nhận nhóm chức năng tài nguyên nông trại, cây, công cụ và túi đồ. Trong bản làm việc hiện tại còn có thay đổi ở scene GamePlay, prefab nhân vật, hệ thống trồng trọt, điều khiển và thư mục Fishing. Việc tách hai trạng thái này giúp phân biệt phần đã lưu vào lịch sử dự án với phần vẫn đang hoàn thiện. Chưa có cơ sở để đưa ra tỷ lệ hoàn thành vì repo không ghi nhận tiêu chí nghiệm thu và kết quả kiểm thử đầy đủ.')

heading('5 Khó khăn và hướng xử lý')
para('Khó khăn chính nằm ở việc kết nối nhiều hệ thống: thao tác phải chọn đúng vật thể, công cụ phải phù hợp với loại tài nguyên, giao diện phải cập nhật khi vật phẩm thay đổi và hoạt ảnh phải phản ánh đúng hành động. Vì vậy, một tính năng có thể đã có mã nguồn nhưng trải nghiệm trong scene chưa nhất quán. Em sẽ kiểm thử theo từng luồng hoàn chỉnh, từ chọn công cụ đến tương tác và nhận kết quả, sau đó xử lý các lỗi tích hợp được phát hiện.')

heading('6 Kế hoạch Sprint tiếp theo')
para('Ưu tiên tiếp theo là hoàn thiện và kiểm thử cơ chế cuốc đất, gieo hạt, tưới nước; tích hợp vòng chơi câu cá; kiểm tra hoạt ảnh và giao diện túi đồ khi nhân vật nhận hoặc sử dụng vật phẩm. Sau khi các luồng chính hoạt động ổn định trong scene GamePlay, em sẽ lưu các thay đổi đã kiểm thử, chuẩn bị bản demo và ghi lại lỗi còn tồn tại để báo cáo ở Sprint kế tiếp.')

heading('7 Minh chứng trong project')
para('Các thư mục mã nguồn liên quan gồm Assets/Scripts/Runtime/WorldResources, Assets/Scripts/Runtime/Inventory và Assets/Scripts/Runtime/Fishing. Các tệp chính cho trồng trọt và tương tác là CropPlot.cs, PlayerFarmingInteractor.cs; scene sử dụng là Assets/Scenes/GamePlay.unity. Commit đối chiếu: 5bcb1e1, ngày 08/10/2026.')

footer = sec.footer.paragraphs[0]
footer.alignment = WD_ALIGN_PARAGRAPH.CENTER
run = footer.add_run('Báo cáo kết quả Sprint hai tuần  |  Dự án game nông trại')
run.font.name, run.font.size = 'Arial', Pt(8.5)
run.font.color.rgb = RGBColor(110, 110, 110)
doc.save('docs/Bao_cao_Sprint_2_tuan.docx')
