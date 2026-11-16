# Hệ thống sự kiện và cắt cảnh

## Luồng chạy

1. `EventTriggerListener` nhận thông báo đổi bản đồ, đổi nấc giờ hoặc cập nhật snapshot. Snapshot được cập nhật sau giao dịch doanh thu, khai thác, tặng quà và đóng góp. Controller đợi đến lúc người chơi có thể vào cắt cảnh, rồi mới yêu cầu kiểm tra các điều kiện đã được đánh dấu cần xét.
2. `EventConditionEvaluator` kiểm tra danh sách đã biên dịch với ngữ cảnh ngày, giờ, vị trí, thời tiết, doanh thu, tình bạn, thư đã nhận và tiến độ. Cấu trúc điều kiện được tái sử dụng. Khi xét điều kiện, các phép tra cứu dùng mảng, dictionary hoặc set đã chuẩn bị.
3. `CutsceneRunner` chạy cây lệnh bằng `Task`. `UnityCutsceneStage` tạo diễn viên tạm từ sprite của game, điều khiển người chơi và cung cấp cổng âm thanh, camera. Đồng hồ dừng trong khi chọn nhánh hoặc phát cắt cảnh.
4. Backend kiểm tra ngày đã lưu, vị trí đã đồng bộ và tiến độ của tài khoản trước khi ghi kết quả. Cơ chế giao dịch và mã yêu cầu của `GameStateEndpoints.Change` bảo vệ dữ liệu khi gửi lại. Khung giờ và thời tiết lấy từ đồng hồ Unity cục bộ.

## OOP, SOLID và mẫu thiết kế

- **Đóng gói và State.** `EventRuntimeState` giữ ba trạng thái Normal, Cutscene và Dialogue, với chuyển trạng thái qua phương thức nội bộ. `EventExecutionContext` cung cấp danh sách diễn viên chỉ đọc và giữ lựa chọn của người chơi. Runner luôn trở về Normal sau khi kết thúc hoặc lỗi.
- **Trừu tượng hóa và ISP.** `IEventCondition`, `IEventCommand`, `ICutsceneControllable`, `ICutsceneActor`, `IDialogueDisplayService`, `IAudioService`, `ICameraService` và `IEventProgressTracker` tách các hợp đồng theo mục đích. Tầng lõi dùng `TilePoint` để chạy được từ CLI. Adapter Unity chuyển tọa độ tile sang tọa độ bản đồ.
- **Kế thừa, đa hình và LSP.** Các lớp Time, Location, Friendship, Weather, Flag và Composite triển khai cùng hợp đồng kiểm tra điều kiện qua `EventConditionBase`. Các lớp Move, Face, Speak, Question, Emote, Fade, Sound và Music kế thừa `EventCommandBase`. Runner gọi mỗi lệnh qua `IEventCommand.ExecuteAsync`. Lệnh thực hiện đúng phạm vi của nó và cùng tuân theo cơ chế hủy.
- **SRP.** Repository biên dịch và kiểm tra cấu trúc kịch bản. Listener kết nối nguồn tín hiệu. Evaluator tìm ứng viên. Runner chạy lệnh. Stage quản lý tài nguyên trình diễn. WorldView vẽ các thay đổi đã lưu của nông trại và Community Center. Backend quản lý điều kiện trao quyền và hiệu ứng lâu dài.
- **OCP và Factory.** `EventConditionFactory` và `EventCommandFactory` ánh xạ tên loại JSON thành đối tượng. Để thêm lệnh, triển khai `IEventCommand` rồi đăng ký factory tương ứng tại điểm cấu hình. Runner thực thi lệnh mới qua hợp đồng này.
- **Command và Composite.** `SequentialCommandGroup` chờ từng lệnh. `ParallelCommandGroup` chờ tất cả và hủy các nhánh còn lại nếu một nhánh lỗi. Repository kiểm tra xung đột đối với hội thoại, chuyển động cùng diễn viên, nhạc và hiệu ứng màn hình. Lựa chọn dẫn vào danh sách lệnh của nhánh và chuyển kết quả sang cổng lưu tiến độ.
- **DIP và Observer.** `CutsceneDirector` lắp các cổng Unity và backend bằng constructor injection ở tầng lõi. Controller lấy trạng thái và bỏ qua cắt cảnh qua `IEventService`. Clock, LocationManager và OnlineGameState phát sự kiện để lên lịch xét điều kiện.

## Chạy trong game

- Cắt cảnh buổi sáng bắt đầu khi người chơi vào nông trại trong khung giờ phù hợp. Marnie yêu cầu trời quang. Clint xuất hiện từ sáng sau ngày khai thác quặng đồng đầu tiên. Bản vẽ xuất hiện trong trang chế tạo.
- Đứng gần dấu sản vật trong Farm Cave để thu hoạch. Sau khi chế tạo lò, chọn lò trong túi và nhấn **E** hoặc chuột phải để chọn quặng cần luyện.
- Mở bảng Bundles trong Community Center gần ô **(30, 18)**. Sáu phòng dùng các gói đóng góp được chọn theo vật phẩm mà dự án hỗ trợ. Lễ khánh thành cần đủ sáu phòng và diễn ra ở Town vào ngày quang. Sau lễ, cửa hàng Joja đóng và Community Center dùng bản đồ đã phục hồi.
- Grandpa bắt đầu xét từ **20:00, ngày 1 mùa xuân năm 3**. Có thể gặp vào một tối sau nếu bỏ lỡ. Backend dùng các tiêu chí tiến độ hiện có trong dự án để chấm từ một đến bốn nến, với mốc bốn nến từ 12 điểm theo code decompile.
- Nhấn **K** hoặc nút **Start** trên tay cầm để bỏ qua phần trình diễn. Diễn viên tạm được dọn và người chơi trở lại vị trí an toàn trước cắt cảnh. Nếu nhánh chưa được chọn, engine vẫn hỏi lựa chọn trước khi lưu.

## Dữ liệu và CLI

Tệp `Assets/Resources/Cutscenes/Milestones.json` chứa năm kịch bản. Các trường `actors`, `conditions` và `commands` lần lượt khai báo vị trí bắt đầu, điều kiện và cây lệnh. Tọa độ theo quy ước của `GameLocation.TileCenter`. Các lệnh hỗ trợ `sequence`, `parallel`, `move`, `face`, `speak`, `emote`, `fade`, `sound`, `music` và `question`.

```powershell
dotnet run --project tools/DailyEvents.Cli -- cutscenes validate
dotnet run --project tools/DailyEvents.Cli -- cutscenes list
```

Nội dung dựa trên luồng sự kiện decompile với dàn cảnh theo tài nguyên hiện có. Hang sinh một sản vật mỗi ngày, lò luyện trực tiếp từ túi, các cảnh dùng sprite và tilemap đang có. Hệ thống lưu lựa chọn thú cưng, loại hang, bản vẽ, phòng đã đóng góp và số nến qua dữ liệu tiến độ hiện hành.
