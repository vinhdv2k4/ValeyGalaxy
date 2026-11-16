# Đưa GameBackend lên Railway

Tài liệu chuẩn bị ngày 03/11/2026 cho `D:\DoAn\DoAN`. Backend sẽ chạy trên Railway, database trên PostgreSQL có volume; khi triển khai xong, máy cá nhân có thể tắt. Người chơi dùng Internet và mở game trực tiếp. Không dùng tunnel, IP LAN hay máy cá nhân làm host API.

## 1. Những gì đã chuẩn bị và giới hạn cần biết

- Root build context là toàn bộ repository `/`, Dockerfile là `Dockerfile.railway`. `.dockerignore` chỉ cho source backend, hai catalog và 17 JSON mà `.csproj` tham chiếu đi vào build context. Unity Library, game build, database local, secret, hình ảnh và tài liệu không được đưa vào image.
- `dotnet publish` đặt 17 JSON vào `ContentData/`, giữ đúng chữ hoa/thường, gồm `hats.json`, `CraftingRecipes.json`, `NPCGiftTastes.json`. `item-catalog.json` và `fish-catalog.json` cũng được publish.
- Production bắt buộc có PostgreSQL; thiếu hoặc sai biến PG sẽ dừng thay vì tạo SQLite tạm. Không cần volume cho API; volume phải nằm ở dịch vụ PostgreSQL.
- Secret hardcode đã được bỏ khỏi `appsettings.json`. Production yêu cầu `SECURITY_HASH_SECRET` ít nhất 32 ký tự; giữ ổn định và sao lưu riêng trong password manager. Đổi secret ảnh hưởng ID đất đã ký và các hash lookup hiện có.
- Khi có `PORT`, API lắng nghe `http://0.0.0.0:PORT`. Railway kết thúc TLS ở proxy, game gọi HTTPS domain công khai. Không cần mở cổng 5055/7777 trên máy cá nhân.
- Release game bỏ qua PlayerPrefs `ServerApiUrl` và `-api=`, xóa URL cũ; Editor/Development Build vẫn dùng chúng cho thử nghiệm. Có chặn build Release khi prefab còn dùng localhost hoặc URL không phải HTTPS origin.

**.NET cần nâng cấp trước khi vận hành lâu dài:** hiện target `net9.0`, SDK/runtime image `9.0`. Tại ngày kiểm tra, .NET 9 còn được hỗ trợ nhưng kết thúc ngày **10/11/2026**; .NET 10 LTS được hỗ trợ đến **14/11/2028**. Máy hiện có SDK 9.0.304, chưa có SDK 10. Thay đổi này chưa nâng framework. Trước khi đưa vào dùng lâu dài, cài SDK 10, nâng cả hai `.csproj` lên `net10.0`, nâng đồng bộ EF Core/Npgsql/Mvc.Testing và các package liên quan lên bản tương thích được hỗ trợ, đổi cả hai Dockerfile sang SDK/runtime `10.0`, rồi chạy lại build, tests và kiểm tra PostgreSQL. Chỉ đổi Docker image không đủ. [Chính sách Microsoft](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core).

## 2. Chuẩn bị repository GitHub

1. Mở dự án và xem `git status`. Hiện có rất nhiều công việc chưa commit; riêng thư mục `Backend` và một số script/prefab Unity đang chưa được Git theo dõi.
2. Dùng GitHub Desktop hoặc công cụ Git bạn quen để xem từng file và tạo commit riêng cho nguồn cần deploy. Cần `Dockerfile.railway`, `.dockerignore`, `railway.toml`, toàn bộ source/config/catalog của `Backend/GameBackend` và 17 JSON được liệt kê trong `.csproj`. Không đưa `bin`, `obj`, `data`, `.db`, `.env`, mật khẩu hoặc output kiểm tra lên GitHub. Các file kiểm tra và hướng dẫn có thể đưa cùng sau khi xem diff.
3. Push nhánh đã xem xét lên repository GitHub. Remote hiện khai báo `https://github.com/vinhdv2k4/ValeyGalaxy`; kiểm tra đây đúng repo bạn muốn dùng. Không dùng `git add .` hoặc commit toàn bộ thay đổi đang dang dở chỉ để deploy.
4. Các file Unity sửa trong lần chuẩn bị này gồm `StartupMenuFlow.cs`, `StartupApiUrl.cs` và `ProductionApiBuildGuard.cs` cùng `.meta`. Prefab sẽ được cập nhật khi bạn có domain thật; không điền một domain giả để build phát hành.

Railway chỉ thấy commit đã push, không thấy file nằm trên ổ D:. `.dockerignore` giảm Docker build context, không tự loại file khỏi GitHub hoặc tự commit source.

## 3. Tạo project và database

1. Đăng nhập [Railway](https://railway.com). Đây là bước bạn cần tự đăng nhập và cấp quyền tài khoản.
2. Tạo **New Project → Empty Project**, đặt tên ví dụ `ValeyGalaxy`. Chọn environment `production`.
3. Thêm service **Database → PostgreSQL**. Trong hướng dẫn này service có tên **Postgres**; nếu Railway đặt tên khác, dùng đúng tên đó trong các reference bên dưới.
4. Kiểm tra PostgreSQL có volume gắn vào thư mục dữ liệu theo template của Railway. Giữ `PGDATA` và mount path do template tạo; không chuyển nó sang ổ tạm. Chọn cùng region với API, ưu tiên Singapore nếu tài khoản có region này.
5. Giữ database trên private networking; API sẽ dùng host `*.railway.internal`. Không cần tạo public HTTP domain hay mở TCP Proxy cho người chơi. PostgreSQL có tài nguyên và chi phí riêng.
6. Thêm service từ **GitHub Repo**, chọn repo và nhánh vừa push. Cấp Railway GitHub App quyền đọc đúng repo khi được yêu cầu. Nếu lần deploy tự động đầu tiên thất bại vì chưa có Variables, cấu hình đầy đủ bên dưới rồi deploy lại.

[Tài liệu PostgreSQL và biến kết nối](https://docs.railway.com/databases/postgresql).

## 4. Đặt cấu hình service API

Chọn service source GitHub, ví dụ tên `GameBackend`, không chọn service Postgres.

| Trường | Giá trị |
|---|---|
| Root Directory | `/` — gốc repository; không dùng `Backend/GameBackend` |
| Config file | `/railway.toml` nếu dashboard yêu cầu chỉ định |
| Builder | Dockerfile (được khai báo trong `railway.toml`) |
| Dockerfile Path | `Dockerfile.railway` |
| Build Command | Để trống, Dockerfile đã chạy restore/publish |
| Start Command | Để trống, dùng ENTRYPOINT của image |
| Pre-deploy Command | Để trống; hiện schema được khởi tạo trong startup |
| Healthcheck Path | `/api/health/ready` |
| Healthcheck Timeout | `300` giây |
| Replicas | `1`, chỉ một region |
| Restart Policy | On Failure, tối đa 10 lần (trong file cấu hình) |
| Serverless/App Sleeping | Tắt cho đợt chạy ban đầu |
| Public domain target port | `8080`, cùng `PORT` bên dưới |

`railway.toml` lưu builder, Dockerfile, readiness, restart và một replica. Kiểm tra dashboard không cấu hình thêm replica ở region khác. Presence/avatar hiện nằm trong bộ nhớ, chưa hỗ trợ chia tải qua nhiều API process. Trong lúc rolling deploy có thể xuất hiện hai process ngắn hạn; avatar cần đồng bộ lại sau chuyển traffic.

Trong **Variables của API**, thêm:

```dotenv
RAILWAY_DOCKERFILE_PATH=Dockerfile.railway
ASPNETCORE_ENVIRONMENT=Production
DOTNET_ENVIRONMENT=Production
PORT=8080
PGHOST=${{Postgres.PGHOST}}
PGPORT=${{Postgres.PGPORT}}
PGUSER=${{Postgres.PGUSER}}
PGPASSWORD=${{Postgres.PGPASSWORD}}
PGDATABASE=${{Postgres.PGDATABASE}}
SECURITY_HASH_SECRET=<khóa ngẫu nhiên riêng của bạn, tối thiểu 32 ký tự>
```

Dùng **Add Reference** trong Variables để chọn biến từ Postgres. Cú pháp `${{Postgres.…}}` chỉ đúng nếu service thật tên `Postgres`. Không dán nguyên placeholder secret. Tạo một chuỗi ngẫu nhiên ít nhất 32 byte bằng password manager, lưu chuỗi đó ở Railway Variables và password manager; không đưa vào Unity, source hoặc chat. Không đổi secret sau mỗi deploy.

Railway có thể cấp `PORT` tự động; ở đây đặt rõ `8080` để cổng domain/healthcheck/image thống nhất. Code đọc giá trị `PORT` thực tế, không cố định cổng 5055. `PGPORT` thường là `5432`; dùng reference, không đoán khi service có giá trị khác.

Không đặt `GAME_DB_PATH`, `ConnectionStrings__Postgres` hoặc `DATABASE_URL` cho cấu hình này. Code hỗ trợ `ConnectionStrings__Postgres` và ưu tiên nó trước bộ `PG*`; một connection string cũ có thể trỏ nhầm database. Code không tự đọc `DATABASE_URL`. `WORLD_HOST_SECRET` chưa cần vì luồng avatar hiện tại chạy trực tiếp qua API; endpoint host-heartbeat sẽ tắt khi không có secret này.

[Dockerfile tùy chỉnh](https://docs.railway.com/builds/dockerfiles), [config as code](https://docs.railway.com/config-as-code/reference), [monorepo và root directory](https://docs.railway.com/deployments/monorepo).

## 5. Deploy, tạo domain HTTPS và kiểm tra readiness

1. Kiểm tra gói và chi phí ở mục 9 trước khi xác nhận nâng gói hoặc thanh toán. Bạn thực hiện xác nhận chi phí trong tài khoản của mình.
2. Bấm **Deploy/Apply Changes**. Xem Build Logs: phải dùng `Dockerfile.railway` và publish thành công. Xem Deploy Logs: không được có lỗi thiếu PG, secret, JSON, kết nối hoặc schema.
3. Trong API **Settings → Networking → Public Networking → Generate Domain**, chọn target port `8080`. Railway cấp một địa chỉ HTTPS dạng `https://....up.railway.app`. Giữ domain này cho các lần deploy sau; đây không phải tunnel tạm trên máy cá nhân. Không xóa/tạo lại domain sau khi đã phát hành game.
4. Mở `https://DOMAIN/api/health/ready` bằng trình duyệt. Chỉ xác nhận bước này thành công khi HTTP 200 và JSON `{"status":"ready"}`. HTTP 502/503, trang lỗi hay chỉ trạng thái Deployment Active chưa đủ bằng chứng.
5. Có thể dùng domain riêng qua Custom Domain và cấu hình CNAME/TXT đúng như dashboard yêu cầu. Khi đổi domain sau này cần cập nhật/build lại client, hoặc duy trì domain cũ hoạt động.

Healthcheck Railway chỉ kiểm tra khi deploy, không giám sát liên tục sau đó. Có thể dùng dịch vụ uptime bên ngoài cho URL readiness nếu cần vận hành lâu dài. [Public networking](https://docs.railway.com/networking/public-networking), [healthchecks](https://docs.railway.com/deployments/healthchecks).

## 6. Đặt domain vào Unity và build cho người chơi

Sau khi readiness thật đã đạt, tại gốc dự án chạy **một lần trên máy phát triển**:

```powershell
./Backend/set-production-url.ps1 -BaseUrl https://DOMAIN.up.railway.app
```

Script cập nhật đúng `apiBaseUrl` trong cả `Assets/Scripts/Runtime/StartupMenuFlow.cs` và `Assets/Resources/Startup/StartupMenu.prefab`. Dùng URL gốc, không thêm `/api`, path, token hoặc mật khẩu. Nếu không dùng script, sửa trường mặc định trong code và **Api Base Url** trên component StartupMenuFlow của prefab thành cùng URL HTTPS. Đóng Play Mode khi sửa, chờ Unity import rồi kiểm tra prefab. Công cụ Rebuild Startup Prefabs dùng mặc định của StartupMenuFlow; cập nhật cả hai giúp tránh URL quay lại localhost.

1. Mở project bằng Unity **6000.3.11f1**, chờ import/compile. Giải quyết Console errors nếu có.
2. **File → Build Profiles**, chọn Windows x86_64. Kiểm tra scene `Assets/Scenes/Main Menu.unity` ở đầu, `Assets/Scenes/GamePlay.unity` phía sau; hai scene này đang được bật trong EditorBuildSettings.
3. **Tắt Development Build**, Script Debugging. Chọn Build ra một thư mục mới, ví dụ `Builds/ValeyGalaxy-Windows`. Build guard sẽ báo lỗi nếu prefab chưa có HTTPS origin.
4. Nén **toàn bộ thư mục output build**: `Game.exe`, thư mục `Game_Data`, `UnityPlayer.dll`, các DLL/thư mục plugin, MonoBleedingEdge nếu có, UnityCrashHandler nếu được sinh ra và mọi file runtime khác. Nếu dùng IL2CPP, giữ `GameAssembly.dll` và các file đi kèm. Không gửi riêng `.exe`; không gửi source backend, secret hoặc dữ liệu PostgreSQL.
5. Người chơi giải nén toàn bộ vào cùng một thư mục rồi nhấp đúp `.exe`. Không cần cài .NET SDK, chạy backend, PowerShell, `-api` hoặc shortcut đặc biệt. Tên executable/thư mục Data tùy Player Settings.

Release luôn dùng URL trong prefab, kể cả máy từng lưu localhost hoặc tunnel trong PlayerPrefs. Editor/Development Build có thể vẫn dùng URL thử nghiệm; kiểm tra bản Release thực tế để kết luận. Build guard và toàn bộ client chưa được chạy trong Unity Editor ở phiên chuẩn bị này.

## 7. Kiểm tra online và dữ liệu sau restart

Các lệnh dưới dành cho **người triển khai**, không phải yêu cầu đối với người chơi.

```powershell
./Backend/verify-online.ps1 -BaseUrl https://DOMAIN.up.railway.app -Phase BeforeRestart
```

Script hỏi mật khẩu thử nghiệm qua ô nhập ẩn, tạo username/email QA ngẫu nhiên, kiểm tra readiness, đăng ký, đăng nhập, tạo farmer và đặt một rương tiêu thụ gỗ. Nó lưu checkpoint không chứa mật khẩu/token ở `%TEMP%\ValeyGalaxy-Railway\online-checkpoint.json`, ngoài thư mục dự án. Giữ mật khẩu để dùng lại ở bước sau. Dùng tài khoản QA riêng; script không xóa tài khoản và không sửa tài khoản người chơi.

1. Chờ script báo PASS. Trong Railway, **Restart** service API; chờ deploy/healthcheck xong. Không xóa service/database/volume.
2. Chạy tiếp với cùng password:

```powershell
./Backend/verify-online.ps1 -BaseUrl https://DOMAIN.up.railway.app -Phase AfterRestart
```

3. Chỉ PASS khi đăng nhập lại được và farmer, tiền, túi, rương khớp checkpoint. Script không tự restart Railway; bạn cần thực sự bấm Restart giữa hai lần chạy.
4. Thử thêm Restart PostgreSQL trong Railway để xác nhận volume giữ dữ liệu, chờ DB phục hồi rồi chạy lại `AfterRestart`. Sẽ có gián đoạn tạm thời trong lúc DB restart.
5. Mở game Release, đăng nhập tài khoản đã tạo trong game trước restart, xác nhận nhân vật/vật phẩm được tải đúng; bước này kiểm tra thêm đường đi Unity → HTTPS → API → PostgreSQL.

Nếu bị 429 ở auth, dừng thử dồn dập và đợi ít nhất 60 giây; hiện auth giới hạn 10 request/phút theo địa chỉ IP mà API nhìn thấy. Cần đánh giá forwarding/proxy và tải thực trước mở cho nhiều người.

## 8. Thử hai game ở hai mạng và phạm vi multiplayer

1. Cài cùng bản Release đã nhúng domain trên hai máy. Máy A dùng Wi-Fi gia đình, máy B dùng mạng khác hoặc hotspot 4G/5G; không cùng LAN.
2. Tắt backend local, cloudflared và mọi terminal host trên máy phát triển. Có thể tắt hẳn máy phát triển khi hai máy test khác vẫn chạy.
3. Hai người đăng ký hai tài khoản riêng, đăng nhập. Đợi heartbeat; menu có thể hiện 2 online (hoặc nhiều hơn nếu còn người thử khác). Heartbeat 15 giây, cửa sổ online 60 giây; logout xóa phiên hiện tại.
4. Tạo nhân vật, vào GamePlay, cùng đến **Town** và di chuyển. Kiểm tra hai chiều thấy tên, vị trí, hướng nhìn và ngoại hình của người kia. Farm/FarmHouse/FarmCave và các mỏ là khu riêng; đứng cùng tên khu riêng không chứng minh thấy nhau.
5. Thoát/mất mạng một game: avatar cũ hết hạn sau khoảng 8 giây nếu không gửi sync; trạng thái online có thể tồn tại đến 60 giây. Khi thoát, gói leave không bảo đảm gửi xong vì game đang đóng.
6. Khởi động lại API rồi đăng nhập/thử tiếp; avatar sẽ phải xuất hiện lại qua sync. Kiểm tra tiền/túi/rương của từng tài khoản không trộn lẫn.

Code hiện có đăng ký/đăng nhập/logout, session trong database, hồ sơ farmer, presence online, tiền/túi/rương và các API kinh tế/tiến trình theo tài khoản. Đã có API kiểm tra các thao tác mua/bán/craft/chuyển đồ và các luồng trồng trọt/câu cá/nhận tài nguyên; kiểm tra gameplay thực tế vẫn cần client.

Avatar dùng HTTP polling khoảng **10 lần/giây/người**, gửi vị trí/hướng/ngoại hình, chỉ trả người cùng khu công cộng. Mặc định tối đa **16** người đang sync; chưa đo tải công khai. `serverHost=127.0.0.1` và `serverPort=7777` trong world-session hiện là metadata cho hướng host riêng trong tương lai; client hiện dùng HTTP API để sync, không tự mở một Unity dedicated server tại 7777.

Chưa có world server authoritative mô phỏng chung và kiểm chứng chuyển động/collision/warp; vị trí do client báo. Không thể suy ra cây, đá, đồ rơi, NPC, thời gian, combat hoặc toàn bộ tiến trình thế giới đã được đồng bộ giữa hai game chỉ từ việc đăng nhập thành công hoặc thấy avatar. Cần phát triển/kiểm thử cơ chế gameplay chung, chống gian lận, chia world/channel, shared presence qua nhiều replica và đo tải trước tăng số người. Đăng nhập online, thấy avatar và đồng bộ gameplay đầy đủ là ba mức kiểm tra khác nhau.

## 9. Chi phí dự kiến

Tra bảng giá chính thức ngày **03/11/2026**: Hobby tối thiểu **5 USD/tháng**, gồm 5 USD usage; Pro tối thiểu **20 USD/tháng**, gồm 20 USD usage. Tiền tài nguyên vượt phần bao gồm được tính thêm. Backend và PostgreSQL đều tiêu thụ tài nguyên, kể cả khi ít người chơi. [Giá Railway](https://railway.com/pricing), [cách đọc hóa đơn](https://docs.railway.com/pricing/understanding-your-bill).

Đơn giá ứng dụng công bố theo giây: RAM `0.00000386 USD/GB/s` (~10 USD/GB-tháng), CPU `0.00000772 USD/vCPU/s` (~20 USD/vCPU-tháng), volume `0.00000006 USD/GB/s` (~0.15–0.16 USD/GB-tháng), outbound `0.05 USD/GB`. Tổng RAM/CPU phải cộng cả hai service; dùng private PG host để tránh egress database công khai. Đây là ước tính từ đơn giá, chưa phải số đo dự án.

Ví dụ giả định chạy 30 ngày: API + PG trung bình cộng lại **0.75 GB RAM**, **0.1 vCPU**, **1 GB volume**, **5 GB egress** → khoảng **9.9 USD/tháng** usage. Hobby dự kiến khoảng 9.9 USD tổng, không phải 5 + 9.9; chưa gồm backup tăng thêm, thuế hoặc tài nguyên khác. Với tổng 1 GB RAM, 0.2 vCPU, 1 GB volume và 20 GB egress thì khoảng **15.2 USD/tháng**. Polling multiplayer khiến CPU/egress thay đổi mạnh theo số người; không có cam kết 100 người hay mức phí cố định.

Bắt đầu với một replica, theo dõi Usage/Metrics 24–48 giờ và đặt thông báo ngân sách trong Billing/Usage. Xem kỹ sự khác biệt giữa cảnh báo và hard limit: hard limit có thể dừng game khi chạm ngưỡng. Không dựa vào free trial để hứa server online dài hạn. Chỉ bạn xác nhận gói/thanh toán trong tài khoản của mình.

## 10. Sao lưu và bảo trì

1. Vào **Postgres → Backups** (hoặc service settings → Backups), bật **Daily**; thêm Weekly/Monthly theo thời gian lưu cần thiết. Theo tài liệu hiện tại: Daily giữ 6 ngày, Weekly 27 ngày, Monthly 89 ngày.
2. Tạo một backup thủ công đầu tiên, kiểm tra nó xuất hiện và hoàn tất. Đặt lịch xong chưa chứng minh restore hoạt động.
3. Trước thay đổi schema, tạo backup; thử restore trong môi trường thử nghiệm cùng project, không đè production đang có người chơi. Kiểm tra lại readiness, đăng nhập và state sau restore. Backup volume không bảo vệ việc xóa/wipe volume; giữ thêm bản logical dump ngoài Railway nếu cần phục hồi độc lập.
4. Bản dump ngoài hệ thống nên chạy từ job có private networking và PostgreSQL client tương thích, ví dụ `pg_dump -Fc --file=game-backup.dump` với bộ PG* trong môi trường job. Lưu dump trong kho riêng có quyền truy cập hạn chế, tự động chạy hằng ngày và thử `pg_restore` vào database thử nghiệm. Không commit dump hoặc đưa mật khẩu trong dòng lệnh/log. Máy cá nhân không cần chạy thường xuyên cho job này.
5. Backup incremental tính phí theo dung lượng riêng tăng thêm, cùng đơn giá volume. Kiểm tra giới hạn dung lượng/khả năng backup của gói trong dashboard. [Backup Railway](https://docs.railway.com/volumes/backups).
6. Hiện code dùng `EnsureCreated` và SQL bổ sung bảng/cột trong startup, chưa có hệ thống migration đầy đủ. Khởi động với DB PostgreSQL mới; database SQLite local không tự chuyển sang PG. Trước nâng schema trên dữ liệu thật cần migration có kiểm soát, backup và thử trên bản sao.
7. Kiểm tra và cập nhật .NET/package/image định kỳ. Hiện chưa có xác minh email hay quy trình quên mật khẩu gửi email; cần bổ sung trước phát hành rộng rãi.

## 11. Bằng chứng kiểm tra và việc còn chờ

Trong phiên chuẩn bị ngày 03/11/2026: publish Release thành công, 17 JSON khớp SHA-256, 14 kiểm tra cấu hình/URL đạt; toàn bộ tests đạt 30/31, còn lỗi so sánh catalog tĩnh 64 mục với 22 asset Unity. Đăng ký, đăng nhập và state đã qua kiểm tra restart trên SQLite local. Các script dò dữ liệu, log và bản publish tạm đã được dọn khỏi dự án.

Chỉ kết luận Railway sẵn sàng khi chính domain thật trả ready, kiểm tra PostgreSQL trước/sau restart đạt và hai máy đã thử bản Release. Phiên chuẩn bị chưa đăng nhập Railway, chưa tạo tài nguyên trả phí, chưa có domain production, chưa cập nhật URL production vào prefab/code và chưa build Unity Release.
