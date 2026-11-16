from pathlib import Path
exec(Path('docs/build_sprint_progress_final.py').read_text(encoding='utf-8').split('title = p(')[0])

normal.font.name = 'Times New Roman'
normal.font.size = Pt(12)
normal.paragraph_format.line_spacing = 1.15
normal.paragraph_format.space_after = Pt(6)
normal.paragraph_format.widow_control = True
for name in ['Title', 'Heading 1', 'Heading 2']:
    d.styles[name].font.name = 'Times New Roman'
d.styles['Heading 1'].font.size = Pt(13)
d.styles['Heading 2'].font.size = Pt(12)
d.core_properties.title = 'Báo cáo tiến độ Sprint hai tuần'
d.core_properties.subject = 'Đồ án phát triển game nông trại 2D bằng Unity'
d.core_properties.author = ''
for style in d.styles:
    for border in list(style.element.iter(qn('w:pBdr'))):
        border.getparent().remove(border)
    if style.type == 1:
        rpr = style.element.find(qn('w:rPr'))
        if rpr is not None:
            fonts = rpr.find(qn('w:rFonts'))
            if fonts is not None:
                for key in ['asciiTheme', 'hAnsiTheme', 'eastAsiaTheme', 'cstheme']:
                    fonts.attrib.pop(qn('w:' + key), None)

title = p('BÁO CÁO TIẾN ĐỘ SPRINT HAI TUẦN', 'Title')
title.alignment = WD_ALIGN_PARAGRAPH.CENTER
sub = p('Đồ án phát triển game nông trại 2D bằng Unity')
sub.alignment = WD_ALIGN_PARAGRAPH.CENTER
sub.runs[0].bold = True

h('1 Thông tin chung')
for label, value in [('Sinh viên thực hiện', '[Họ tên – Mã số sinh viên]'), ('Lớp', '[Tên lớp]'), ('Giảng viên phụ trách', '[Họ tên giảng viên]'), ('Thời gian thực hiện', '[Ngày bắt đầu] – [Ngày kết thúc]'), ('Thời lượng Sprint', '02 tuần')]:
    line = p('')
    line.add_run(label + ': ').bold = True
    line.add_run(value)
p('Báo cáo trình bày kết quả xây dựng các chức năng gameplay nền tảng của đồ án trong đợt thực hiện vừa qua. Nội dung tập trung vào điều khiển nhân vật, tương tác với môi trường, quản lý vật phẩm, trồng trọt và phát triển chức năng câu cá; đồng thời xác định những nội dung cần tiếp tục hoàn thiện.')

h('2 Mục tiêu Sprint')
p('Mục tiêu chính là xây dựng các thành phần phục vụ hoạt động của người chơi trong nông trại. Nhân vật cần có cơ chế di chuyển, lựa chọn công cụ và tương tác với các đối tượng trên bản đồ. Kết quả tương tác được thể hiện thông qua thay đổi trạng thái đối tượng, hoạt ảnh và vật phẩm thu được. Trên nền tảng đó, đợt phát triển mở rộng sang thao tác làm đất, gieo hạt, tưới nước và cơ chế câu cá.')

h('3 Các hạng mục thực hiện')
table(['Hạng mục', 'Nội dung triển khai', 'Mức độ hiện tại'], [
    ('Nhân vật', 'Di chuyển, hướng thao tác, camera và hoạt ảnh sử dụng công cụ', 'Đã triển khai; tiếp tục tinh chỉnh'),
    ('Tài nguyên', 'Cây, đá, cỏ, cành cây và vật phẩm rơi khi khai thác', 'Đã triển khai logic cơ bản'),
    ('Công cụ và túi đồ', 'Dữ liệu vật phẩm, chọn ô, thêm, sử dụng và đổi vị trí vật phẩm', 'Đã triển khai logic và giao diện'),
    ('Trồng trọt', 'Cuốc đất, gieo hạt, tưới nước và trạng thái ô đất', 'Đang hoàn thiện tích hợp'),
    ('Câu cá', 'Vùng nước, quăng cần, dữ liệu cá và trò chơi nhỏ kéo cá', 'Đang phát triển')
], [1.25, 3.72, 1.89])

d.add_page_break()
h('4 Kết quả thực hiện')
h('4.1 Điều khiển và hiển thị nhân vật', 2)
p('Đã xây dựng các thành phần tiếp nhận thao tác, xử lý di chuyển và xác định hướng tương tác của nhân vật. Camera được thiết kế để theo nhân vật trong quá trình di chuyển. Hệ thống hoạt ảnh và phần hiển thị công cụ được kết nối với hành động của người chơi, tạo nền tảng cho các thao tác làm nông và khai thác. Nội dung đang tiếp tục được tinh chỉnh là sự phối hợp giữa hướng thao tác, thời điểm phát hoạt ảnh và kết quả tương tác.')
h('4.2 Tài nguyên và tương tác với môi trường', 2)
p('Đã triển khai các loại tài nguyên trong nông trại như cây, đá, cỏ, cành cây và cỏ dại, cùng thành phần bố trí tài nguyên trên bản đồ. Các đối tượng có thể kiểm tra loại công cụ được sử dụng, nhận tác động và tạo vật phẩm rơi theo quy định. Đối với cây, đã bổ sung xử lý phần thân, gốc cây và hiệu ứng khi cây đổ. Phần tài nguyên khu mỏ cũng đã có thành phần sinh đá và tài nguyên phục vụ việc mở rộng hoạt động khai thác.')
h('4.3 Công cụ và quản lý vật phẩm', 2)
p('Đã xây dựng dữ liệu vật phẩm và công cụ, kết hợp với hệ thống quản lý túi đồ. Các thao tác được triển khai gồm chọn ô sử dụng, thêm vật phẩm, tiêu thụ số lượng và đổi vị trí giữa các ô. Phần giao diện có thanh công cụ, lưới túi đồ và hiển thị trạng thái ô được chọn. Hệ thống này là đầu mối liên kết vật phẩm thu được từ môi trường với những vật phẩm người chơi sử dụng trong hoạt động trồng trọt.')
h('4.4 Trồng trọt', 2)
p('Đã triển khai logic cho chuỗi thao tác cuốc đất, gieo hạt và tưới nước. Mỗi ô đất lưu trạng thái đã làm đất, đã gieo và đã tưới; phần hiển thị được cập nhật theo trạng thái tương ứng. Hiện tại, công việc tập trung vào hoàn thiện kết nối giữa công cụ, hạt giống, hoạt ảnh và ô đất. Phạm vi kết quả của đợt này dừng ở các thao tác khởi đầu của trồng trọt; chưa xác nhận hoàn thiện toàn bộ quá trình sinh trưởng và thu hoạch cây trồng.')
h('4.5 Câu cá', 2)
p('Đã bắt đầu xây dựng cơ chế xác định vùng nước, điều khiển thao tác quăng cần, dữ liệu cá, hiệu ứng mặt nước và trò chơi nhỏ khi kéo cá. Chức năng đang được phát triển để kết nối các bước thành một quá trình thống nhất. Nội dung cần hoàn thiện tiếp theo là kiểm tra chuyển trạng thái trong quá trình câu, kết quả thành công hoặc thất bại và việc đưa vật phẩm nhận được vào túi đồ.')

d.add_page_break()
h('5 Phương pháp triển khai kỹ thuật')
h('5.1 Tổ chức các thành phần chức năng', 2)
p('Hệ thống được tổ chức thành các nhóm phụ trách điều khiển nhân vật, tương tác với môi trường, dữ liệu vật phẩm và giao diện. Bộ phận điều khiển nhận thao tác từ người chơi và chuyển yêu cầu tới chức năng phù hợp. Đối tượng được tác động tự kiểm tra điều kiện và cập nhật trạng thái; kết quả sau đó được phản ánh qua hình ảnh hoặc túi đồ. Cách tổ chức này giúp điều chỉnh một chức năng mà hạn chế ảnh hưởng tới các phần còn lại.')
h('5.2 Quản lý dữ liệu và trạng thái', 2)
p('Dữ liệu vật phẩm được tổ chức bằng ScriptableObject của Unity để tách thông tin cấu hình khỏi xử lý hành vi. Các loại tài nguyên dùng chung một lớp nền, sau đó bổ sung phản ứng riêng cho từng đối tượng. Ô đất lưu trạng thái phục vụ làm đất, gieo hạt và tưới nước. Hệ thống câu cá sử dụng các trạng thái để điều phối từng giai đoạn của quá trình câu, trong khi phần trò chơi nhỏ được xử lý riêng để thuận tiện hiệu chỉnh.')
h('5.3 Luồng xử lý tương tác', 2)
p('Khi người chơi sử dụng công cụ, hệ thống xác định công cụ đang được chọn và đối tượng trong phạm vi tương tác. Sau khi kiểm tra điều kiện, đối tượng nhận tác động và thay đổi trạng thái. Nếu thao tác tạo ra hoặc tiêu thụ vật phẩm, số lượng trong túi đồ được cập nhật tương ứng. Hoạt ảnh và hiệu ứng cung cấp phản hồi trực quan cho hành động đó. Luồng này được dùng làm cơ sở để kiểm tra sự nhất quán giữa thao tác, dữ liệu và hình ảnh trong game.')

h('6 Kiểm thử và nội dung trình diễn')
p('Các chức năng đã có phần triển khai cần được kiểm thử tích hợp trong màn chơi để xác nhận hoạt động của toàn bộ luồng tương tác. Kết quả nghiệm thu sẽ được tổng hợp theo các tình huống dưới đây. Tại thời điểm lập báo cáo, chưa có bảng kết quả kiểm thử tổng hợp để kết luận mức độ ổn định của từng chức năng.')
table(['Tình huống kiểm tra', 'Yêu cầu cần đạt'], [
    ('Di chuyển và dùng công cụ', 'Nhân vật phản hồi đúng; hướng thao tác và hoạt ảnh phù hợp.'),
    ('Khai thác cây hoặc đá', 'Công cụ tác động đúng đối tượng; trạng thái và vật phẩm rơi được cập nhật.'),
    ('Quản lý túi đồ', 'Thêm, đổi vị trí và sử dụng vật phẩm không làm sai số lượng.'),
    ('Cuốc đất, gieo hạt, tưới nước', 'Ô đất chuyển trạng thái đúng và hiển thị tương ứng.'),
    ('Câu cá', 'Các giai đoạn được nối đúng; xử lý rõ kết quả và nhận vật phẩm.')
], [2.05, 4.81])

d.add_page_break()
h('7 Nội dung cần hoàn thiện và rủi ro')
p('Nội dung cần ưu tiên là sự phối hợp giữa các hệ thống đã triển khai. Một thao tác như gieo hạt đồng thời liên quan đến công cụ đang chọn, số lượng vật phẩm, ô đất, hướng nhân vật và hoạt ảnh. Nếu một thành phần cập nhật sai thời điểm, dữ liệu và hình ảnh có thể không thống nhất. Biện pháp xử lý là kiểm tra từng chuỗi thao tác hoàn chỉnh, ghi lại điều kiện phát sinh lỗi và kiểm tra lại sau khi điều chỉnh.')
p('Đối với câu cá, việc chuyển giữa các giai đoạn và xử lý kết thúc thao tác cần được rà soát kỹ để tránh tình trạng nhân vật bị giữ trong một hành động. Đối với giao diện, cần kiểm tra thao tác chọn và sử dụng vật phẩm khi túi đồ được mở hoặc đóng. Đây là các rủi ro tích hợp cần kiểm soát trong đợt tiếp theo, chưa được xem là những lỗi đã được xác nhận.')

h('8 Đánh giá và kế hoạch Sprint tiếp theo')
h('8.1 Đánh giá kết quả Sprint', 2)
p('Kết quả đạt được là đã hình thành các thành phần chính cho hoạt động trong nông trại, bao gồm điều khiển nhân vật, công cụ, túi đồ và tài nguyên tương tác. Chức năng trồng trọt đã có các thao tác cơ bản, còn câu cá đã có các thành phần xử lý ban đầu. Những kết quả này tạo cơ sở cho bước tích hợp và hoàn thiện trải nghiệm chơi. Mức độ hoàn thành hiện được đánh giá theo từng chức năng; chưa quy đổi thành tỷ lệ phần trăm khi chưa có kết quả nghiệm thu đầy đủ.')
h('8.2 Bài học và hướng cải thiện', 2)
p('Việc phân chia trách nhiệm giữa dữ liệu, xử lý hành vi và giao diện giúp cấu trúc chương trình dễ theo dõi và mở rộng. Trong các đợt tiếp theo, cần gắn mỗi hạng mục với một tiêu chí nghiệm thu cụ thể và kiểm thử ngay khi hoàn thành phần triển khai. Việc lưu kết quả kiểm tra, ảnh chụp hoặc video trình diễn sẽ giúp đánh giá tiến độ rõ hơn và phát hiện sớm những vấn đề ảnh hưởng tới luồng chơi.')
h('8.3 Kế hoạch thực hiện tiếp theo', 2)
p('Ưu tiên trước hết là hoàn thiện chuỗi cuốc đất, gieo hạt và tưới nước, bảo đảm trạng thái ô đất và số lượng vật phẩm được cập nhật thống nhất. Tiếp theo là hoàn thiện vòng chơi câu cá, từ xác định vùng nước đến xử lý kết quả và nhận vật phẩm. Đồng thời, tiếp tục tinh chỉnh hoạt ảnh, camera và giao diện để các thao tác của người chơi có phản hồi rõ ràng.')
p('Sau khi hoàn thiện các luồng chính, tiến hành kiểm thử tích hợp theo các tình huống đã xác định, ghi nhận lỗi và chuẩn bị phiên bản trình diễn. Đầu ra dự kiến của Sprint tiếp theo gồm phiên bản gameplay có thể trình diễn, bảng kết quả kiểm thử và danh sách nội dung còn tồn tại để giảng viên phụ trách theo dõi, đánh giá.')

for t in d.tables:
    for row in t.rows:
        trPr = row._tr.get_or_add_trPr()
        trPr.append(OxmlElement('w:cantSplit'))
        for c in row.cells:
            borders = OxmlElement('w:tcBorders')
            for side in ['top', 'left', 'bottom', 'right']:
                el = OxmlElement('w:' + side)
                el.set(qn('w:val'), 'single'); el.set(qn('w:sz'), '4'); el.set(qn('w:color'), 'D9D9D9')
                borders.append(el)
            c._tc.get_or_add_tcPr().append(borders)
            for para in c.paragraphs:
                for run in para.runs: run.font.name, run.font.size = 'Times New Roman', Pt(10.5)
    repeat = OxmlElement('w:tblHeader')
    t.rows[0]._tr.get_or_add_trPr().append(repeat)

footer = s.footer.paragraphs[0]
footer.alignment = WD_ALIGN_PARAGRAPH.CENTER
footer.add_run('Báo cáo tiến độ Sprint  |  Trang ').font.size = Pt(9)
field = OxmlElement('w:fldSimple'); field.set(qn('w:instr'), 'PAGE'); footer._p.append(field)
d.save(OUT)
print(OUT)
