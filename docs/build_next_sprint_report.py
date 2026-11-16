from pathlib import Path
from docx import Document
from docx.shared import Inches, Pt, RGBColor
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.enum.table import WD_TABLE_ALIGNMENT, WD_CELL_VERTICAL_ALIGNMENT
from docx.oxml import OxmlElement
from docx.oxml.ns import qn

OUT = Path('docs/Bao_cao_tien_do_tiep_theo_Kinh_te_va_Canh_tranh.docx')
d=Document(); sec=d.sections[0]
sec.page_width,sec.page_height=Inches(8.5),Inches(11)
sec.left_margin=sec.right_margin=Inches(.8)
sec.top_margin=Inches(.7);sec.bottom_margin=Inches(.65)
for st in d.styles:
    for el in list(st.element.iter(qn('w:pBdr'))):el.getparent().remove(el)
    for fonts in st.element.iter(qn('w:rFonts')):
        for a in list(fonts.attrib):
            if 'Theme' in a or 'theme' in a:del fonts.attrib[a]
n=d.styles['Normal'];n.font.name='Times New Roman';n.font.size=Pt(11.5)
n.paragraph_format.line_spacing=1.12;n.paragraph_format.space_after=Pt(6)
for name,size in [('Title',17),('Heading 1',13.5),('Heading 2',12)]:
    st=d.styles[name];st.font.name='Times New Roman';st.font.size=Pt(size)
    st.font.bold=True;st.font.color.rgb=RGBColor(0,0,0)
    st.paragraph_format.space_before=Pt(10);st.paragraph_format.space_after=Pt(5)
    st.paragraph_format.keep_with_next=True
def p(t):return d.add_paragraph(t)
def h(t,level=1):return d.add_heading(t,level)
def page(t):d.add_page_break();h(t)
def eq(t):
    para=d.add_paragraph();para.paragraph_format.space_after=Pt(7)
    math=OxmlElement('m:oMath');r=OxmlElement('m:r');txt=OxmlElement('m:t');txt.text=t;r.append(txt);math.append(r);para._p.append(math)
def tab(headers,rows,widths):
    t=d.add_table(rows=1,cols=len(headers));t.autofit=False;t.alignment=WD_TABLE_ALIGNMENT.CENTER
    for i,w in enumerate(widths):t.columns[i].width=Inches(w)
    for row in [headers]+rows:
        cells=t.rows[0].cells if row is headers else t.add_row().cells
        for i,v in enumerate(row):
            c=cells[i];c.width=Inches(widths[i]);c.text=str(v);c.vertical_alignment=WD_CELL_VERTICAL_ALIGNMENT.CENTER
            pr=c._tc.get_or_add_tcPr();b=OxmlElement('w:tcBorders')
            for side in ['top','left','bottom','right']:
                e=OxmlElement('w:'+side);e.set(qn('w:val'),'single');e.set(qn('w:sz'),'4');e.set(qn('w:color'),'D9D9D9');b.append(e)
            pr.append(b)
            mar=OxmlElement('w:tcMar')
            for side,val in [('top','65'),('bottom','65'),('left','80'),('right','80')]:
                e=OxmlElement('w:'+side);e.set(qn('w:w'),val);e.set(qn('w:type'),'dxa');mar.append(e)
            pr.append(mar)
            if row is headers:
                sh=OxmlElement('w:shd');sh.set(qn('w:fill'),'E7EDF2');pr.append(sh)
            for para in c.paragraphs:
                para.paragraph_format.space_after=Pt(1);para.paragraph_format.line_spacing=1.05
                for run in para.runs:run.font.size=Pt(10.5);run.bold=row is headers
        t.rows[-1]._tr.get_or_add_trPr().append(OxmlElement('w:cantSplit'))
    t.rows[0]._tr.get_or_add_trPr().append(OxmlElement('w:tblHeader'))
    p('')

t=d.add_paragraph('BÁO CÁO TIẾN ĐỘ ĐỒ ÁN',style='Title');t.alignment=WD_ALIGN_PARAGRAPH.CENTER
t=p('Giai đoạn tiếp theo về hệ thống kinh tế và cạnh tranh');t.alignment=WD_ALIGN_PARAGRAPH.CENTER;t.runs[0].bold=True
h('1 Thông tin và định hướng của đợt báo cáo')
for a,b in [('Đề tài','Game nông trại 2D nhiều người chơi'),('Sinh viên thực hiện','[Họ tên – Mã số sinh viên – Lớp]'),('Giảng viên phụ trách','[Họ tên giảng viên]'),('Mốc đối chiếu','Kết quả giai đoạn phát triển trước')]:
    q=p('');q.add_run(a+': ').bold=True;q.add_run(b)
p('Tiếp nối các chức năng nhân vật, công cụ, túi đồ và tương tác nông trại trong báo cáo trước, đợt này tập trung phát triển nền tảng trực tuyến, lưu tiến trình và giao dịch kinh tế. Đồng thời, em xây dựng bộ luật cân bằng để chuyển hoạt động làm nông thành cuộc cạnh tranh về quản lý vốn, hiệu quả sản xuất và quyền khai thác đất.')
p('Điểm cốt lõi của thiết kế là mọi người bắt đầu với cùng nguồn lực, sau đó tạo khác biệt bằng lựa chọn cây trồng, lịch thu hoạch, mức dự phòng tiền và quyết định mở rộng. Đất hữu hạn và đấu giá tạo mục tiêu tranh chấp trực tiếp; bảng xếp hạng dựa trên tổng vàng đã kiếm trừ nợ thuê trại chính chưa trả. Người chơi cần quản lý cả khả năng sinh lời lẫn nghĩa vụ thanh toán.')
h('Phạm vi kết quả và mục tiêu')
p('Phần tiến độ dưới đây phản ánh các chức năng đã triển khai: TV trong game lấy bảng hạng từ API theo tổng vàng kiếm được trừ nợ thuê trại chính. Thuê trại chính 150 G sau mỗi 7 ngày game đã được đưa vào backend; bộ vốn khởi đầu 1.200 G, luật đất mở rộng và luật thi đấu mùa vẫn là thiết kế mục tiêu chưa tích hợp đầy đủ. Cách phân chia này giúp đánh giá riêng kết quả lập trình và kết quả thiết kế trò chơi.')
tab(['Trọng tâm','Kết quả hướng tới'],[
('Nền tảng trực tuyến','Tài khoản, hồ sơ nhân vật, dữ liệu cá nhân và hiện diện nhiều người chơi.'),
('Vòng chơi kinh tế','Sản xuất, lưu vật phẩm, mua bán và nhận tiền qua giao dịch được máy chủ xử lý.'),
('Cạnh tranh giữa người chơi','Vốn ngang nhau, chi phí đất công khai, xếp hạng và đấu giá quyền khai thác.')],[1.5,5.4])

page('2 Kết quả phát triển so với báo cáo trước')
h('2.1 Tài khoản và lưu tiến trình',2)
p('Đã bổ sung luồng đăng ký, đăng nhập, tạo hồ sơ nhân vật và khôi phục thông tin theo tài khoản. Hệ thống phía máy chủ lưu tiền, túi đồ, rương và trạng thái chơi; các yêu cầu mua, bán, chuyển vật phẩm được kiểm tra trước khi cập nhật. Đây là bước chuyển từ các thao tác chủ yếu trong màn chơi sang một nền tảng có thể giữ tiến trình qua nhiều phiên sử dụng.')
h('2.2 Hoàn thiện vòng chơi sản xuất và trao đổi',2)
p('Trồng trọt được mở rộng với tăng trưởng theo ngày được tưới, thu hoạch và cây tái thu hoạch. Gieo hạt kiểm tra số lượng hạt; thu hoạch kiểm tra độ chín, khoảng cách và chỗ trống trong túi. Đã có mua bán vật phẩm, thùng giao hàng và quyết toán hàng đã gửi khi chuyển ngày. Ngoài ra, project bổ sung rương, chuyển vật phẩm, chế tạo, mua nâng cấp túi, lưu kết quả câu cá và tương tác tặng quà cho NPC.')
h('2.3 Thế giới và người chơi trực tuyến',2)
p('Đã bổ sung quản lý khu vực, chuyển bản đồ, khu mỏ và các thành phần NPC. Phần nhiều người chơi trao đổi vị trí, hướng nhìn, ngoại hình và khu vực qua máy chủ để hiển thị người khác trong cùng khu. Đây là nền hiện diện trực tuyến; chưa đồng nghĩa mọi hành động vật lý và sản xuất của thế giới đều được mô phỏng chung hoàn toàn trên máy chủ.')
h('2.4 Nền tảng quản lý đất và cạnh tranh',2)
p('Giao diện TV trong nhà đã có các mục bảng xếp hạng, thuê/trả đất và đấu giá đất. Máy chủ có xử lý tiền thuê, ghi nhận quyền giữ đất, đặt giá, hoàn tiền cho người bị vượt giá và chuyển quyền khi kết thúc phiên. Đã có kiểm tra người dùng, số dư, yêu cầu trùng và cập nhật đồng thời. Phần quyền đất này còn cần liên kết xuyên suốt với từng vùng canh tác trên bản đồ để việc thắng đấu giá thực sự mở thêm diện tích sử dụng.')
tab(['Nhóm công việc','Trạng thái đợt này'],[
('Tài khoản, hồ sơ, lưu tiến trình','Đã có xử lý đăng nhập, hồ sơ nhân vật và dữ liệu theo tài khoản.'),
('Trồng trọt, giao dịch, kho đồ','Đã có xử lý phía máy chủ và kết nối từ trò chơi; tiếp tục hoàn thiện cách sử dụng trong Unity.'),
('Xếp hạng, thuê và đấu giá','Bảng hạng theo vàng kiếm được trừ nợ thuê đã triển khai; trại chính có kỳ thuê theo ngày game, lô TV thu trước.'),
('Cân bằng kinh tế và thi đấu mùa','Đã xây dựng mô hình, thông số và ví dụ tính toán; đang chờ tích hợp.')],[2.25,4.65])

page('3 Vốn khởi đầu và nghĩa vụ thuê trang trại')
h('3.1 Nguồn lực khi bắt đầu',2)
p('Theo thiết kế kinh tế mục tiêu, mỗi người chơi mới nhận cố định 1.200 G, quyền sử dụng khu khởi nghiệp 48 ô, công cụ cầm tay và kho cơ bản. Khoản tiền này chỉ được cấp một lần khi khởi tạo người chơi trong mùa; đăng nhập lại tiếp tục số dư đã lưu. Người tham gia mùa cạnh tranh có cùng điểm xuất phát, không mang tiền hoặc tài sản từ mùa cũ vào. Hạt giống phải mua để quyết định đầu tư có ý nghĩa ngay từ đầu.')
p('1.200 G cho phép mua 24 hạt củ cải giá 20 G, chi 480 G và giữ 720 G dự phòng; nếu trồng cả 48 ô thì chi 960 G, chỉ còn 240 G. Người chơi có thể chủ động chọn an toàn hoặc sản xuất nhiều hơn. Đất rộng không tự tạo lợi nhuận: gieo đủ diện tích đòi hỏi vốn, thời gian tưới và đầu ra phù hợp.')
h('3.2 Tiền thuê phải thanh toán',2)
tab(['Phần đất','Quy mô','Phí nhận quyền chuẩn','Thuê mỗi 7 ngày game'],[
('Khu khởi nghiệp','48 ô','Cấp quyền ban đầu','150 G'),
('Lô mở rộng thứ 1','24 ô','900 G','85 G'),
('Lô mở rộng thứ 2','24 ô','1.400 G','100 G'),
('Lô mở rộng thứ 3','24 ô','2.150 G','120 G'),
('Lô mở rộng thứ 4','24 ô','3.350 G','135 G'),
('Tổng khi đủ 4 lô','144 ô','7.800 G cho 4 lô','590 G')],[1.7,.65,2.15,2.4])
p('Phí nhận quyền là khoản trả một lần; tiền thuê là nghĩa vụ lặp lại. Các mức nhận quyền chuẩn dùng cho cấp quyền và định giá tham chiếu; lô đấu giá có giá trúng riêng. Trúng đấu giá không miễn tiền thuê. Giới hạn bốn lô mở rộng và mức thuê tăng theo quy mô giúp hạn chế một người chiếm hết đất mà không chịu chi phí tương ứng.')
h('3.3 Thời điểm thu tiền và tình huống thiếu tiền',2)
p('Trại chính hiện thu 150 G khi bắt đầu ngày 8, 15, 22 và 29 của từng người, sau khi xử lý doanh thu ngày cuối kỳ. Việc thu chỉ diễn ra khi người chơi chuyển ngày trong game, không phát sinh lúc offline. Nếu tích hợp lô mở rộng thứ nhất theo thiết kế mục tiêu, tổng thuê dự kiến là 235 G; cách tính thuê giữa kỳ cho lô mở rộng vẫn cần triển khai.')
p('Khi thiếu tiền thuê trại chính, backend lấy số vàng hiện có, giữ ví tối thiểu bằng 0 và ghi phần thiếu thành nợ. TV cho phép trả dần bằng vàng trong ví; điểm xếp hạng bị trừ bằng nợ còn lại rồi hồi khi trả. Cảnh báo chi tiết, hạn chế mở đất khi nợ cao và cưỡng chế lô mở rộng vẫn thuộc phần cần triển khai tiếp.')

page('4 Định giá nông sản và kiểm chứng dòng tiền')
h('4.1 Công thức mở rộng theo dữ liệu cây trồng',2)
p('Giá được thiết kế theo thời gian tăng trưởng, sản lượng kỳ vọng, số lần tái thu và mùa vụ. Với cây mới, g là số ngày tăng trưởng; r là số ngày tái thu; q là sản lượng trung bình một lần thu; N là số lần thu hợp lệ trong mùa; t là bậc mở khóa từ 0 đến 3. Cây trồng ngày s có lần thu đầu ở s + g, vì phải trải qua đủ lần chuyển ngày được tưới.')
eq('T = clamp(4,5 + 0,64 × t + 0,035 × (g − 4) − 0,42 × I(r > 0); 3,5; 6,5)')
p('T là mức lãi gộp mục tiêu trên một ô trong một ngày, trước tiền thuê và lao động. Đặt M = T × H, trong đó H là thời gian tăng trưởng đối với cây thu một lần, hoặc cửa sổ khai thác trong mùa đối với cây tái thu. Tỷ lệ vốn f được chọn theo nhóm cây: 0,53 hoặc 0,55 cho cây thu một lần; 0,62 nếu sản lượng kỳ vọng lớn hơn 1,05; cây tái thu ưu tiên f = 0,33.')
eq('Giá hạt S = làm tròn đến 5 G [M × f / (1 − f)]')
eq('Giá bán P = làm tròn đến 1 G [(S + M) / (q × N)]')
p('Với cơ chế sản lượng cơ bản từ a đến b và xác suất thêm sản phẩm p, q = (a + b)/2 + Σ(pʲ), j từ 1 đến 20. N phải tính theo ngày trồng và ngày kết thúc mùa; nếu không kịp thu thì không dùng công thức để cấp giá hợp lệ. Khung này mở rộng cho các cây có dữ liệu tương ứng. Cây ăn quả, nông sản nhặt và sản phẩm chế biến cần quy đổi thêm chi phí đầu vào, thời gian khai thác và công suất riêng; chưa coi toàn bộ danh mục đã được cân bằng chỉ từ bốn cây mẫu.')
tab(['Cây mẫu','Giá hạt','Giá bán','Lần thu đầu nếu trồng ngày 1'],[
('Củ cải','20 G','38 G','Ngày 5'),('Khoai tây','50 G','65 G/sản phẩm','Ngày 7; sản lượng kỳ vọng 1,25'),('Đậu que','60 G','30 G/lần thu','Ngày 11; tái thu mỗi 3 ngày'),('Súp lơ','80 G','145 G','Ngày 13')],[1.2,.75,1.6,3.35])
h('4.2 Ví dụ một kỳ kinh doanh',2)
p('Trồng 24 ô củ cải: nhận 1.200 G; mua hạt hết 480 G; tưới đủ bốn lần chuyển ngày; thu ngày 5 và bán được 912 G; trả thuê 150 G sau ngày 7. Ví cuối kỳ là 1.482 G, lợi nhuận 282 G. Nếu giá bán giảm xuống 30 G, lợi nhuận còn 90 G và ví còn 1.290 G. Người chăm 48 ô có thể lãi 714 G ở giá chuẩn nhưng phải dành nhiều vốn và thao tác hơn. Các kết quả giả định bán đủ hàng trong hạn mức, chưa phát sinh chi phí khác.')

page('5 Cơ chế xếp hạng và động lực cạnh tranh')
h('5.1 Bảng xếp hạng theo vàng kiếm được',2)
p('Máy chủ lưu riêng tổng vàng đã kiếm từ bán hàng và giao hàng, số vàng đang có trong ví, cùng nợ thuê trại chính. TV lấy tối đa 10 người theo điểm giảm dần và hiển thị tổng kiếm được, nợ thuê, điểm cùng tên nhân vật. Khi bằng điểm, thứ tự theo tên tài khoản. Số lô đất chỉ là thông tin tham khảo.')
eq('Điểm xếp hạng = tối đa(0, tổng vàng kiếm được − nợ thuê trại chính chưa trả)')
p('Mua hạt, trả tiền thuê đúng hạn hoặc đặt giá đấu chỉ làm giảm vàng trong ví; không làm giảm tổng vàng kiếm được hay điểm. Nếu thiếu tiền thuê khi chuyển sang ngày 8, 15, 22… phần còn thiếu thành nợ và làm giảm điểm. Khi người chơi trả nợ, điểm tăng lại tương ứng. Việc định giá hàng tồn, máy móc hoặc quyền đất chỉ phục vụ phân tích cân bằng, không cộng vào bảng hạng.')
h('5.2 Kết quả mùa và cơ hội cho nhiều chiến thuật',2)
p('Nếu triển khai mùa thi đấu 28 ngày trong giai đoạn tiếp theo, thứ hạng cuối mùa sẽ chốt theo điểm vàng kiếm được trừ nợ thuê còn lại sau khi hoàn tất giao dịch. Đây là quy tắc thiết kế cho chế độ mùa, chưa phải chức năng kết mùa đã triển khai. Danh hiệu có thể thể hiện bằng huy hiệu, biển hiệu và lịch sử thành tích, không cộng năng suất vĩnh viễn cho người thắng.')
p('Có thể ghi nhận thêm hiệu quả lợi nhuận trên ô đất, hoàn thành hợp đồng và phục hồi sau khó khăn dưới dạng thành tích phụ. Người chơi ít đất vẫn có mục tiêu cạnh tranh; mở rộng diện tích chỉ là một chiến thuật. Các thành tích phụ và chỉ số thanh khoản không thay thế bảng xếp hạng chính theo vàng.')
h('5.3 Vì sao cuộc cạnh tranh có chiều sâu',2)
p('Người thận trọng giữ tiền cho kỳ thuê và có thể tham gia phiên đấu giá khi đối thủ thiếu vốn. Người mở rộng chấp nhận chi phí cố định cao để tăng sản lượng, nhưng cần thị trường tiêu thụ. Người chọn cây dài ngày có thể đạt biên lãi tốt song phải chờ qua kỳ thuê đầu. Lựa chọn nào tốt còn phụ thuộc thời gian còn lại của mùa, giá bán và quy mô đối thủ; không có chiến thuật chỉ cần trồng nhiều nhất là luôn thắng.')
p('Để giữ cơ hội đuổi kịp, mùa giải bắt đầu ngang nguồn lực, giới hạn đất mở rộng và không thưởng năng suất theo thứ hạng. Các giao dịch bán hàng và trả nợ cần được máy chủ xác thực để điểm trên bảng hạng phản ánh dữ liệu đã lưu. Những thông số mùa giải chưa được chốt thành luật của bản game hiện tại.')

page('6 Đấu giá quyền khai thác đất trang trại')
h('6.1 Đối tượng đấu giá và điều kiện tham gia',2)
p('Đối tượng đấu giá là quyền khai thác một lô đất hữu hạn, có diện tích và đặc tính được công bố. Người thắng trả phí trúng đấu giá để nhận quyền, đồng thời tiếp tục chịu tiền thuê theo danh mục đất của mình. Hồ sơ phải cho biết giá khởi điểm, tiền thuê sau khi nhận, thời điểm bàn giao, cây hoặc tài sản đi kèm. Chủ đất không mặc nhiên chuyển cả máy móc cá nhân cho người thắng.')
p('Theo thiết kế, phiên được công bố trước ít nhất hai ngày game và nhận giá kín trong một ngày game. Người tham gia phải không có nợ quá hạn, còn hạn mức đất và đủ tiền. Máy chủ giữ toàn bộ giá đặt cùng khoản dự phòng thuê kỳ tiếp theo. Người chơi được nâng giá đã cam kết, không được hạ hoặc rút giá; giá của đối thủ được giữ kín đến khi đóng phiên.')
h('6.2 Chọn người thắng và thanh toán',2)
p('Người đặt cao nhất thắng và trả đúng giá mình đặt. Hòa giá được bốc thăm trên máy chủ và lưu kết quả để đối chiếu, không ưu tiên người có đường truyền nhanh hơn. Người thua nhận lại toàn bộ tiền đã giữ; người thắng được giải phóng dự phòng thuê, nhưng vẫn phải giữ khả năng trả tiền khi đến hạn. Việc thu tiền và chuyển quyền phải cùng thành công, tránh mất tiền mà không nhận đất.')
p('Ví dụ minh họa: A và B đều có 2.000 G và chỉ đang sử dụng khu nền. A đặt 1.000 G, B đặt 1.100 G cho lô mở rộng đầu tiên; mỗi người phải dự phòng 235 G tiền thuê kỳ kế tiếp. A còn 765 G và B còn 665 G được chi trong lúc chờ. Nếu B thắng, A được hoàn phần tiền giữ; B chi 1.100 G và sau khi giải phóng dự phòng còn 900 G trong ví. B có thêm 24 ô nhưng phải mua hạt và chịu tổng tiền thuê 235 G/kỳ. Đây là ví dụ chiến thuật, không phải cam kết giá khởi điểm của mọi lô.')
h('6.3 Đất thanh lý và khả năng quay lại cuộc chơi',2)
p('Khi đấu giá lô của người đang tái cơ cấu, tiền bán phân bổ theo thứ tự: phí đấu giá 5%, trả nợ thuê thực tế, rồi trả phần dư cho chủ cũ. Ví dụ bán 1.100 G và nợ 300 G: phí 55 G, trả nợ 300 G, chủ cũ nhận 745 G. Không thanh lý khi chỉ vừa chậm trả; thiết kế yêu cầu đồng thời đủ ngưỡng nợ hai kỳ, hóa đơn cũ quá hạn ít nhất 14 ngày game và thêm ba ngày thông báo cuối.')
p('Đất nền 48 ô được bảo vệ. Khi phải tái thiết, người chơi tiếp tục lao động và trả nợ dần theo luật công bố. Đấu giá tạo cơ hội cho đối thủ giành tài nguyên nhưng không loại người thua ngay khỏi cuộc chơi. Giá kín, dự phòng thuê và giới hạn đất buộc người mua cân nhắc hiệu quả, thay vì chỉ cố trả cao nhất.')

page('7 Triển khai kỹ thuật và hiện trạng chức năng')
h('7.1 Nền tảng xử lý phía máy chủ',2)
p('Ứng dụng Unity gửi yêu cầu tới dịch vụ ASP.NET Core; máy chủ xác thực người dùng và cập nhật dữ liệu qua cơ sở dữ liệu. Các giao dịch tiền sử dụng kiểm tra số dư, phiên bản dữ liệu và mã yêu cầu để hạn chế xử lý trùng hoặc ghi đè khi nhiều người thao tác. Hướng triển khai này cần được giữ thống nhất cho quyết toán thuê, ký quỹ, xếp hạng và bàn giao đất.')
tab(['Cơ chế','Hiện trạng trong game','Thiết kế kinh tế tiếp theo'],[
('Vốn khởi đầu','500 G và bộ vật phẩm ban đầu.','1.200 G; nguồn lực đầu mùa thống nhất, mua hạt bằng vốn.'),
('Thuê đất','Trại chính 150 G mỗi 7 ngày game; lô TV thu trước 200 G/7 ngày thực, tối đa 3 lô.','85–135 G/lô mở rộng theo ngày game; tối đa 4 lô mở rộng.'),
('Xếp hạng','Top 10 theo vàng kiếm được trừ nợ thuê; TV hiển thị điểm và nợ.','Giữ tiêu chí này khi kết mùa; xác minh sau bán hàng và trả nợ.'),
('Đấu giá','Giá công khai, khởi điểm 1.000 G; tăng ít nhất 100 G; ghi quyền lâu dài.','Giá kín, giữ tiền và dự phòng thuê; chuyển quyền khai thác, vẫn trả thuê.'),
('Quyền sử dụng trên bản đồ','Có dữ liệu quyền đất và các luồng xử lý canh tác.','Liên kết mọi ô canh tác với hợp đồng đất; thực thi hết hạn và bàn giao.')],[1.15,2.75,3.0])
h('7.2 Nền tảng tiến trình nhân vật và chế tạo',2)
p('Máy chủ đã lưu XP cho năm kỹ năng; trang Skills hiển thị cấp độ theo các ngưỡng kinh nghiệm. Hiện việc cộng XP tập trung ở thu hoạch, thu thập tài nguyên và câu cá. Trang Crafting có công thức mặc định, kiểm tra nguyên liệu và gửi yêu cầu chế tạo tới máy chủ; các công thức mở theo cấp kỹ năng chưa được đưa vào luồng này.')
p('Dữ liệu CookingRecipes và Achievements đã có trong project, nhưng chưa có tiến trình thành tựu riêng theo tài khoản hoặc chức năng nấu ăn hoàn chỉnh trong backend. Một số vật dụng chế tạo và đồ ăn cũng chưa gắn đầy đủ với tác dụng trồng trọt, thể lực và buff của nhân vật. Đây là nền để phát triển các đầu mục ở kỳ tiếp theo.')

page('8 Kế hoạch phát triển kỳ tiếp theo')
h('8.1 Mục tiêu',2)
p('Phát triển tiến trình dài hạn cho từng nông dân và biến nông sản, tài nguyên thu thập thành công cụ sản xuất. Thành tựu ghi nhận những việc người chơi đã làm; XP tạo đường nâng cấp kỹ năng; cấp kỹ năng mở công thức; đồ chế tạo và món ăn đem lại tác dụng cụ thể trong nông trại. Tiến trình này được lưu theo tài khoản, phù hợp với game nhiều người chơi có ngày game riêng.')
h('8.2 Các đầu mục thực hiện',2)
tab(['Nội dung','Hướng triển khai','Kết quả dự kiến'],[
('Thành tựu','Xác định mốc trồng, thu hoạch, bán hàng, khai khoáng, câu cá và chế tạo. Máy chủ cộng tiến độ từ hành động đã hoàn tất, chỉ mở thưởng một lần.','Collections hiển thị tiến độ, thành tựu đã đạt và phần thưởng phù hợp như công thức hoặc vật phẩm trang trí.'),
('Kỹ năng nông dân','Hoàn thiện cách cộng XP, cấp độ và phần mở khóa cho Farming, Mining, Foraging, Fishing; bổ sung Combat khi có vòng chiến đấu. Đồng bộ trang Skills với dữ liệu đã lưu.','Cấp kỹ năng có giá trị sử dụng; XP không mất sau khi đăng nhập lại và không tăng từ thao tác lặp không tạo kết quả.'),
('Đồ hỗ trợ trồng trọt','Rà soát CraftingRecipes, mã nguyên liệu và vật phẩm giữa Unity với backend. Mở công thức theo kỹ năng; ưu tiên rương, phân bón, máy tưới và đồ bảo vệ cây.','Chế tạo dùng đúng nguyên liệu; đồ đặt trong trại được lưu theo chủ và tác động lên ô đất, cây trồng tương ứng.'),
('Món ăn và buff','Xây luồng nấu ăn từ CookingRecipes, dùng nông sản trong túi hoặc kho làm nguyên liệu. Gắn món ăn với hồi thể lực và buff hỗ trợ công việc.','Có giao diện nấu ăn, món ăn lưu trong túi và hiệu ứng rõ thời lượng trong phiên chơi.')],[1.25,3.0,2.65])
h('8.3 Liên kết với kinh tế trang trại',2)
p('Người chơi đầu tư tài nguyên để chế tạo công cụ hoặc nấu ăn thay vì chỉ bán nguyên liệu ngay. Thành tựu và kỹ năng mở lựa chọn sản xuất mới, còn tiền thuê và bảng hạng vẫn dùng dữ liệu kinh tế hiện hành. Phần thưởng tiến trình cần tránh cấp vàng trực tiếp để không làm sai thước đo tổng vàng đã kiếm được.')

f=sec.footer.paragraphs[0];f.alignment=WD_ALIGN_PARAGRAPH.CENTER
r=f.add_run('Báo cáo tiến độ đồ án  |  Trang ');r.font.size=Pt(9)
field=OxmlElement('w:fldSimple');field.set(qn('w:instr'),'PAGE');f._p.append(field)
d.core_properties.title='Báo cáo tiến độ về hệ thống kinh tế và cạnh tranh'
d.core_properties.subject='Tiến độ phát triển và thiết kế vốn khởi đầu, thuê đất, xếp hạng, đấu giá'
d.core_properties.author=''
d.save(OUT)
print(OUT)
