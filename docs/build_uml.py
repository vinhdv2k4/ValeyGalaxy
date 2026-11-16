from pathlib import Path
import math
from reportlab.pdfgen import canvas
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.lib.colors import HexColor
import pypdfium2 as pdfium
from docx import Document
from docx.shared import Inches, Pt
from docx.enum.text import WD_ALIGN_PARAGRAPH

ROOT = Path('docs/UML')
ROOT.mkdir(exist_ok=True)
pdfmetrics.registerFont(TTFont('ArialVN', 'C:/Windows/Fonts/arial.ttf'))
pdfmetrics.registerFont(TTFont('ArialVNB', 'C:/Windows/Fonts/arialbd.ttf'))
W, H = 880, 1000
C = None

def start(name):
    global C
    C = canvas.Canvas(str(ROOT / (name + '.pdf')), pagesize=(W, H))
    C.setStrokeColor(HexColor('#243849')); C.setLineWidth(1.6)
def text(x,y,s,size=17,bold=False,center=False):
    C.setFillColor(HexColor('#172B3A'))
    C.setFont('ArialVNB' if bold else 'ArialVN',size)
    (C.drawCentredString if center else C.drawString)(x,H-y,s)
def line(points,dashed=False):
    C.setDash(6,4) if dashed else C.setDash()
    p=C.beginPath(); p.moveTo(points[0][0],H-points[0][1])
    for x,y in points[1:]:p.lineTo(x,H-y)
    C.drawPath(p); C.setDash()
def arrow(points,dashed=False,triangle=False):
    line(points,dashed)
    x,y=points[-1]; a,b=points[-2]
    ux,uy=(x-a),(y-b); norm=math.hypot(ux,uy); ux/=norm; uy/=norm
    size=14 if triangle else 10
    left=(x-ux*size-uy*size*.5,y-uy*size+ux*size*.5)
    right=(x-ux*size+uy*size*.5,y-uy*size-ux*size*.5)
    if triangle:
        path=C.beginPath();path.moveTo(x,H-y);path.lineTo(left[0],H-left[1]);path.lineTo(right[0],H-right[1]);path.close()
        C.setFillColor(HexColor('#FFFFFF'));C.drawPath(path,fill=1,stroke=1)
    else: line([left,(x,y),right])
def rect(x,y,w,h,fill='#FFFFFF'):
    if fill:C.setFillColor(HexColor(fill))
    C.rect(x,H-y-h,w,h,fill=bool(fill),stroke=1)
def finish(name):
    C.save()
    pdf=pdfium.PdfDocument(str(ROOT/(name+'.pdf')))
    pdf[0].render(scale=2).to_pil().save(ROOT/(name+'.png'))
def ellipse(x,y,w,h,lines):
    C.setFillColor(HexColor('#F4F7FA'));C.ellipse(x,H-y-h,x+w,H-y,fill=1,stroke=1)
    for i,s in enumerate(lines):text(x+w/2,y+h/2+(i-(len(lines)-1)/2)*22+6,s,17,center=True)
def box(x,y,w,name,attrs,methods,abstract=False):
    hh=64 if abstract else 42
    ah=len(attrs)*24+16; mh=len(methods)*24+16
    rect(x,y,w,hh+ah+mh)
    rect(x,y,w,hh,'#E9EFF5')
    if abstract:text(x+w/2,y+22,'«abstract»',14,center=True)
    text(x+w/2,y+hh-15,name,17,True,True)
    for i,a in enumerate(attrs):text(x+12,y+hh+25+i*24,a,14)
    line([(x,y+hh+ah),(x+w,y+hh+ah)])
    for i,m in enumerate(methods):text(x+12,y+hh+ah+25+i*24,m,14)
    return hh+ah+mh

# 1. UML use cases: associations only; no invented include relations.
start('01_Use_Case')
rect(285,60,545,840)
text(555,96,'HỆ THỐNG GAME NÔNG TRẠI',21,True,True)
C.circle(130,H-365,25,stroke=1,fill=0)
line([(130,390),(130,480)]);line([(75,420),(185,420)])
line([(130,480),(85,545)]);line([(130,480),(175,545)])
text(130,582,'Người chơi',21,True,True)
cases=[(145,['Di chuyển nhân vật']),(280,['Quản lý túi đồ']),(415,['Khai thác tài nguyên']),(550,['Cuốc đất, gieo hạt, tưới nước']),(685,['Câu cá'])]
for y,labels in cases:
    line([(185,420),(365,y+45)])
    ellipse(365,y,390,90,labels)
text(440,790,'Trồng trọt: đang hoàn thiện tích hợp.',16)
text(440,820,'Câu cá: đang phát triển.',16)
text(440,850,'Các chức năng mô tả phạm vi hiện tại.',16)
finish('01_Use_Case')

# 2. Actual classes, simplified members and precise inheritance.
start('02_Class_Diagram')
box(35,45,350,'PlayerFarmingInteractor',['+ interactionRadius : float'],['+ TryHitResource(...) : bool'])
box(510,45,325,'ItemData',['+ itemId : string','+ kind : PlayerItemKind','+ maxStackSize : int'],['+ CanStackWith(other) : bool'])
box(35,330,350,'WorldResource',['# health : int','# requiredToolKind : PlayerItemKind','# dropItem : ItemData'],['+ Hit(context) : bool','+ CanHarvest(...) : bool','# SpawnDrops(direction) : void'],True)
box(510,365,325,'ToolData',['+ upgradeLevel : int','+ staminaCost : float'],['+ IsHeavyHitter() : bool'])
box(35,775,350,'FarmTree',['- isStump : bool'],['+ Hit(context) : bool'])
box(510,775,325,'FarmRock',['- spriteRenderer : SpriteRenderer'],['+ Setup(...) : void'])
arrow([(210,167),(210,330)],True);text(225,255,'tác động',15)
arrow([(385,420),(445,420),(445,145),(510,145)])
text(450,122,'dropItem',14);text(483,164,'0..1',13)
arrow([(672,365),(672,215)],triangle=True)
arrow([(210,775),(210,570)],triangle=True)
arrow([(672,775),(672,705),(300,705),(300,570)],triangle=True)
text(45,942,'+ công khai   |   # được bảo vệ   |   - riêng tư',16)
text(45,968,'Tam giác rỗng: kế thừa. Nét đứt: phụ thuộc. Thành viên được rút gọn.',15)
finish('02_Class_Diagram')

# 3. FarmRock uses the base WorldResource hit/break behavior.
start('03_Sequence_Diagram')
xs=[120,350,565,775]
names=[['interactor :','PlayerFarmingInteractor'],['rock :','FarmRock'],['drop :','WorldItemDrop'],['inventory :','PlayerInventory']]
for x,parts in zip(xs,names):
    if x==565:continue
    rect(x-99,50,198,66,'#E9EFF5')
    text(x,76,parts[0],16,center=True);text(x,101,parts[1],13,True,True)
    line([(x,116),(x,554 if x==350 else 920)],True)
arrow([(18,155),(120,155)]);text(22,141,'Yêu cầu dùng cuốc chim',13)
arrow([(120,195),(350,195)]);text(235,183,'Hit(context)',16,center=True)
arrow([(350,225),(403,225),(403,260),(350,260)])
text(432,246,'Kiểm tra công cụ, giảm máu',15)
rect(20,300,830,212,None)
text(40,328,'opt',16,True);text(100,328,'[máu ≤ 0 và có vật phẩm rơi]',16)
arrow([(350,365),(406,365),(406,402),(350,402)])
text(430,383,'OnBreak → SpawnDrops',15)
arrow([(350,449),(482,449)],True);text(416,436,'«create»',14,center=True)
rect(482,415,166,65,'#E9EFF5')
text(565,438,'drop :',16,center=True);text(565,462,'WorldItemDrop',14,True,True)
line([(565,480),(565,874)],True)
text(370,500,'Spawn(item, ...) tạo vật phẩm rơi',14)
arrow([(350,522),(120,522)],True);text(220,511,'true',14,center=True)
line([(341,545),(359,563)]);line([(359,545),(341,563)])
text(374,558,'Đá bị hủy sau khi tạo vật phẩm',14)
rect(20,582,830,307,None)
text(40,610,'opt',16,True);text(100,610,'[vật phẩm đến gần người chơi]',16)
arrow([(565,646),(606,646),(606,676),(565,676)])
text(615,665,'Collect()',15)
arrow([(565,726),(775,726)]);text(670,714,'AddItem(item, amount)',14,center=True)
arrow([(775,765),(565,765)],True);text(670,753,'added : bool',14,center=True)
text(550,817,'[added = true]',14)
arrow([(565,831),(610,831),(610,860),(565,860)])
text(633,853,'Hủy vật phẩm rơi',14)
line([(556,869),(574,887)]);line([(574,869),(556,887)])
text(30,947,'Tiền điều kiện: chọn đúng công cụ và tìm thấy đá trong phạm vi tương tác.',15)
text(30,972,'Luồng rút gọn; thời gian chờ, hoạt ảnh và vòng cập nhật được lược bỏ.',15)
finish('03_Sequence_Diagram')

# Append once to the existing report; source report is left intact.
doc=Document('docs/Bao_cao_tien_do_Sprint_2_tuan.docx')
captions=[
('01_Use_Case','Phụ lục A Sơ đồ chức năng người chơi','Hình A.1  Sơ đồ Use Case của game nông trại','Sơ đồ xác định người chơi là tác nhân chính và mô tả năm nhóm chức năng trong phạm vi phát triển. Các thao tác trồng trọt được nhóm thành một chức năng; trạng thái phát triển được ghi ngay trong sơ đồ.'),
('02_Class_Diagram','Phụ lục B Sơ đồ lớp công cụ và tài nguyên','Hình B.1  Sơ đồ lớp rút gọn của hệ thống tương tác','Lớp tương tác phụ thuộc vào WorldResource để tác động lên tài nguyên. FarmTree và FarmRock kế thừa hành vi chung; ToolData kế thừa dữ liệu của ItemData. Thuộc tính dropItem liên kết tài nguyên với dữ liệu vật phẩm có thể rơi ra. Sơ đồ chỉ thể hiện các thành viên và quan hệ cần thiết cho phạm vi này.'),
('03_Sequence_Diagram','Phụ lục C Trình tự khai thác đá','Hình C.1  Trình tự khai thác và thu thập vật phẩm','Với công cụ hợp lệ, đá nhận tác động và giảm máu. Khi máu về không, hệ thống tạo vật phẩm rơi. Vật phẩm đến gần người chơi sẽ yêu cầu thêm vào túi đồ; chỉ khi thao tác thêm thành công, đối tượng vật phẩm rơi mới bị hủy. Sơ đồ mô tả luồng xử lý, không thay thế kết quả kiểm thử.')]
for name,title,cap,description in captions:
    doc.add_page_break();doc.add_heading(title,1)
    p=doc.add_paragraph(description);p.paragraph_format.space_after=Pt(6)
    pic=doc.add_paragraph();pic.alignment=WD_ALIGN_PARAGRAPH.CENTER
    pic.add_run().add_picture(str(ROOT/(name+'.png')),width=Inches(5.55))
    p=doc.add_paragraph(cap);p.alignment=WD_ALIGN_PARAGRAPH.CENTER
    for r in p.runs:r.italic=True;r.font.size=Pt(10)
doc.save('docs/Bao_cao_Sprint_kem_UML.docx')
print('Created three diagrams and report with UML appendix.')
