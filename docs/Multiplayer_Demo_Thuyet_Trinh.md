# Demo hai người chơi và mô tả multiplayer hiện tại

Để backend chạy độc lập với máy cá nhân, dùng [hướng dẫn Railway](../Backend/RAILWAY_DEPLOY.vi.md). Các bước tunnel bên dưới chỉ dành cho demo phát triển và yêu cầu máy host luôn bật.

## Những gì đã chạy

- ASP.NET Core API lưu tài khoản, hồ sơ nhân vật, tiền, túi đồ và các rương đặt trong nhà vào cơ sở dữ liệu. Mua, bán, tạo rương và chuyển vật phẩm dùng yêu cầu có `requestId` và kiểm tra trên server.
- Khi vào `GamePlay`, Unity gửi vị trí, hướng nhìn, tên khu và ngoại hình qua `/api/world/presence/sync` khoảng 10 lần/giây. API xác thực tài khoản, kiểm tra dữ liệu cơ bản, giới hạn số tài khoản đang hiện diện theo `maxPlayers` (mặc định 16) và chỉ trả avatar ở cùng khu. Bản ghi không cập nhật quá 8 giây được bỏ qua và dọn khi có yêu cầu đồng bộ tiếp theo.
- Unity tạo avatar hiển thị cho người khác, nội suy vị trí và hiển thị tên/ngoại hình. Avatar từ xa không tham gia va chạm vật lý. Đây là đồng bộ hiện diện bằng HTTP, chưa phải Unity dedicated server hoặc Netcode.
- Client chấp nhận API cục bộ qua HTTP và API từ xa qua HTTPS. Chỉ Editor/Development Build dùng `-api=https://...` và lưu URL vào `PlayerPrefs`. Bản Release bỏ qua URL thử nghiệm cũ và dùng HTTPS domain được đóng gói trong StartupMenu.prefab; người chơi mở trực tiếp executable.

## Chạy demo qua Internet

1. Từ thư mục gốc repository, chạy `./Backend/start-local.ps1` trên máy chủ. Mở `http://127.0.0.1:5055/api/health/ready` và xác nhận kết quả `ready`.
2. Trên cùng máy, chạy `cloudflared tunnel --url http://127.0.0.1:5055`. Giữ cả API và `cloudflared` đang chạy; lấy URL HTTPS được in ra.
3. Gửi **Development Build** và URL đó cho bạn bè. Họ chạy `Game.exe -api=https://TEN-MIEN.trycloudflare.com`, đăng ký hoặc đăng nhập, rồi vào `GamePlay`.
4. Hai người phải cùng khu công cộng, ví dụ Town, để thấy avatar của nhau. Farm/FarmHouse/FarmCave và các mỏ là khu riêng. `/api/presence/online` cho biết số tài khoản vừa gửi heartbeat; việc thấy avatar là phép kiểm tra riêng cho đồng bộ thế giới.

Cloudflare Quick Tunnel dùng cho thử nghiệm: URL đổi sau mỗi lần tạo tunnel, không có cam kết uptime và giới hạn 200 yêu cầu đang xử lý cùng lúc. Giới hạn này không tương đương 200 người chơi. Chưa có kết quả đo tải 100 client trên máy chủ. Xem [tài liệu Cloudflare](https://developers.cloudflare.com/tunnel/get-started/quick-tunnels/).

## Cách trình bày với hội đồng

> Hệ thống hiện dùng ASP.NET Core API và một cơ sở dữ liệu chung để quản lý tài khoản, tiền, túi đồ và rương của từng người chơi. Khi hai client vào cùng khu, API xác thực phiên đăng nhập và trao đổi vị trí, hướng nhìn, ngoại hình để mỗi client hiển thị người còn lại. Các giao dịch mua, bán và chuyển đồ được kiểm tra trên server. Đây là bản multiplayer thử nghiệm qua HTTP polling; kiểm tra di chuyển, thu hoạch, gieo hạt và vật lý của thế giới trên dedicated server là giai đoạn tiếp theo.

ID ô đất có chữ ký HMAC-SHA256 gắn với chủ sở hữu và được so sánh bằng `FixedTimeEquals`. Chữ ký giúp phát hiện ID bị sửa; nó không mã hóa ID và không thay thế kiểm tra quyền sở hữu trong database. Chỉ số tra cứu hash giúp tìm nhanh, nhưng chưa có benchmark để tuyên bố thời gian phản hồi dưới microsecond.

## Giới hạn phải nói rõ

- Vị trí và tên khu vẫn do client khai báo. API chưa xác minh collider, vận tốc, điểm dịch chuyển hoặc quyền vào khu.
- Thu hoạch, câu cá và vật phẩm rơi còn được mô phỏng cục bộ; chưa có giao dịch server xác nhận rồi cộng vật phẩm online. API gieo hạt chưa trừ hạt trong túi.
- Không có Unity dedicated zone server hoặc kết nối tới cổng `7777` trong luồng hiện tại. Trường `serverHost` và `serverPort` chỉ là metadata.
- Không khẳng định chịu được 100 máy trước khi chạy thử tải, đo CPU, RAM, băng thông, độ trễ và tỷ lệ lỗi trên đúng máy host.
