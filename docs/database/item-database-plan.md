# Kiểm kê item trước khi thiết kế database

Nguồn kiểm kê: `Assets/Content/Data/*.json`, `Assets/Resources/OnlineItems/*.asset` và các dòng tạo stock tường minh trong `Assets/Scripts/GalaxyPlay/**/*.cs`. Danh sách đầy đủ, tìm kiếm/lọc được nằm ở [item-source-index.csv](item-source-index.csv). Các tham chiếu item số xuất hiện trực tiếp trong code shop nằm ở [shop-code-references.csv](shop-code-references.csv). Chạy `ExportItemInventory.ps1` để tạo lại hai CSV sau khi nguồn thay đổi.

## Phạm vi và mức tin cậy

`item-source-index.csv` có **3.046 bản ghi nguồn**, không phải 3.046 item độc nhất. Trong đó **2.207 bản ghi** thuộc 10 nguồn item ứng viên. Chỉ **785** bản ghi ứng viên còn tên và các trường nguồn; **1.422** mới có ID. Các nhóm còn lại là tham chiếu nông trại, công thức, shop, máy hoặc asset online. Một item có thể xuất hiện ở nhiều nguồn, nên không được cộng các nhóm để tính số item game.

- Chỉ có ID: Objects **807**, BigCraftables **182**, Tools **37**, Weapons **67**, Shirts **303**, Pants **18**, Trinkets **8**. Tất cả giá trị nguồn ở các nhóm này là `{}`.
- Còn tên và chuỗi thuộc tính nguồn: Boots **18**, hats **122**, Furniture **645**.

Các nguồn bổ sung: Crops **50** ID hạt giống (giá trị rỗng), Fish **74** mục có tên và dữ liệu câu cá, FruitTrees **8** mục có tham chiếu tên bản địa hóa, CraftingRecipes **150**, CookingRecipes **81**, TailoringRecipes **322**, Fences **5**, FloorsAndPaths **13**, Machines **39**. Công thức, cây, cá và máy không được tự động coi là item riêng nếu chưa đối chiếu ID với item master.

## Dữ liệu shop

`Shops.json` có **77 khóa shop**, nhưng toàn bộ giá trị là `{}`; không có danh sách hàng, giá, điều kiện mở khóa hoặc tồn kho. Danh sách 77 khóa có trong CSV với `kind=shop_definition`. Quét code decompile tìm được **173 dòng** tạo stock bằng ID số trực tiếp, gồm **126 ID khác nhau**; CSV thứ hai ghi file và dòng. Đây là bằng chứng một số mặt hàng có thể được bán, không phải danh mục shop hoàn chỉnh: code còn hàng sinh động, điều kiện theo mùa/sự kiện và các nhánh dùng biến thay vì ID số.

## Item đã chạy trong bản Unity hiện tại

Có **20** `ItemData` tại `Assets/Resources/OnlineItems`: `axe`, `hoe`, `watering_can`, `pickaxe`, `scythe`, `fishing_rod`, `seeds_parsnip`, `seeds_potato`, `seeds_green_bean`, `seeds_cauliflower`, `mixed_seeds`, `parsnip`, `potato`, `green_bean`, `cauliflower`, `wood`, `stone`, `fiber`, `copper_ore`, `iron_ore`. Đây là ID của bản Unity đang phát triển; không được tự động gán bằng ID số trong dữ liệu decompile khi chưa có bảng ánh xạ được xác nhận.

Bốn loại cây đang có asset canh tác trong bản Unity hiện tại: parsnip, potato, green bean và cauliflower. `Crops.json` của nguồn decompile có 50 khóa hạt giống, nhưng không còn giai đoạn tăng trưởng, mùa, sản phẩm thu hoạch hoặc quan hệ hạt giống → nông sản.

37 ID tool của nguồn decompile: `Axe`, `CopperAxe`, `SteelAxe`, `GoldAxe`, `IridiumAxe`; `Hoe`, `CopperHoe`, `SteelHoe`, `GoldHoe`, `IridiumHoe`; `Pickaxe`, `CopperPickaxe`, `SteelPickaxe`, `GoldPickaxe`, `IridiumPickaxe`; `WateringCan`, `CopperWateringCan`, `SteelWateringCan`, `GoldWateringCan`, `IridiumWateringCan`; `BambooPole`, `TrainingRod`, `FiberglassRod`, `IridiumRod`, `AdvancedIridiumRod`; `Pan`, `SteelPan`, `GoldPan`, `IridiumPan`; `CopperTrashCan`, `SteelTrashCan`, `GoldTrashCan`, `IridiumTrashCan`; `Lantern`, `MilkPail`, `ReturnScepter`, `Shears`.

## Thiết kế database trước khi nhập dữ liệu

1. **SourceCatalogRecord** lưu nguyên `source_file`, `family`, `source_id`, tên tìm được và `evidence_status`. Khóa duy nhất `(family, source_id)`. Đây là vùng kiểm kê, cho phép lưu 1.422 ID thiếu thuộc tính mà không giả vờ item đã dùng được.
2. **ItemDefinition** là danh mục item đã xác minh: khóa nội bộ bất biến, namespace nguồn và ID nguồn, tên, loại, stack tối đa, giá cơ sở nếu có bằng chứng, icon tham chiếu, trạng thái `draft/verified/active`, phiên bản dữ liệu. Một item có thể mang nhiều tag (`farm`, `shop`, `tool`, `fish`, `furniture`); shop và nông trại là quan hệ, không phải bảng item trùng lặp.
3. **ItemInstance** lưu thuộc tính riêng của một vật thể không thể gộp stack, như cấp nâng cấp tool, chất lượng, attachment và trạng thái sử dụng. Stack thông thường chỉ tham chiếu `ItemDefinition`.
4. **ShopDefinition** và **ShopOffer** liên kết shop với item, tiền tệ, giá tại shop, giới hạn tồn kho, điều kiện mở khóa, mùa/sự kiện và hiệu lực. Không tạo offer chính thức từ `Shops.json` rỗng; các tham chiếu trong code phải được kiểm chứng từng trường hợp.
5. **CropDefinition**, **Recipe** và **RecipeIngredient** liên kết hạt giống, nông sản và nguyên liệu qua item key đã xác minh. Điều kiện tăng trưởng và công thức chỉ nhập từ nguồn còn dữ liệu thật.
6. **PlayerInventorySlot**, **PlacedChest**, **ChestSlot** là trạng thái người chơi, khóa ngoại về item/instance và chủ sở hữu. Chỉ mục dự kiến: `(player_id, slot)`, `(player_id, item_key)`, `(chest_id, slot)`; mọi chuyển đồ và tiền cần giao dịch nguyên tử và kiểm tra quyền sở hữu.

Không điền tên, giá, stack, shop offer hay quan hệ crop cho các record chỉ có ID. Để hoàn thiện danh mục bản gốc, cần nguồn dữ liệu item/shop/crop có nội dung thật hoặc đối chiếu thủ công đáng tin cậy. Chưa tạo migration hay thay đổi database trong bước kiểm kê này.
