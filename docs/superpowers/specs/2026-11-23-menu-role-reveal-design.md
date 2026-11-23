# Menu chính, màn thông báo vai trò và quay về Menu — Thiết kế (ngắn)

Ngày: 2026-11-23 · Nhánh: `Manh/Dev`

## Mục đích

Ưu tiên hiện tại của đồ án là **game phải trọn vẹn** trước khi đầu tư thêm vào độ sâu Behavior Tree: người chấm cầm máy phải thấy một vòng chơi có đầu có cuối. Hiện mở app là vào trận ngay, vai trò chỉ là dòng chữ nhỏ trên HUD, và nút "Thoát" tắt hẳn ứng dụng. Thay đổi này thêm Menu chính, thẻ thông báo vai trò đầu trận, và cho kết quả/tạm dừng quay về Menu.

## Luồng

```
Mở app → [Loading] (chạy 1 lần) → [Menu] ──Chơi──> [Game] ──hết trận──> [Kết quả]
                                    ▲                │                  │
                                    └──── Về Menu ───┴──── Về Menu ─────┘
                                                     Chơi lại → nạp lại [Game]
```

- Build Settings: `Loading` (0) → `Menu` (1) → `Game` (2).
- `Loading`: `LoadingBar` nạp `Menu` thay vì `Game`. Chỉ chạy khi mở app; Menu → Game nạp thẳng, không qua Loading.

## Mục tiêu

1. **Scene Menu mới** với 3 nút:
   - **Chơi** → nạp `Game`.
   - **Hướng dẫn** → bật/tắt một panel trong cùng scene: luật chơi tóm tắt (hai phe, 10s chạy trước, 90s, trúng trứng bị nhốt đến hết trận, điều kiện thắng) và cách điều khiển (joystick, vuốt xoay camera, nút kỹ năng). Có nút Đóng.
   - **Thoát** → tắt app (trong Editor: dừng Play).
2. **Thẻ thông báo vai trò** (cách 1 đã chọn): khi vào trận, trước giai đoạn chạy trước 10s, hiện thẻ lớn giữa màn hình, ví dụ *"BẠN LÀ GÀ — Bắt hết phe Người trước khi hết giờ!"* / *"BẠN LÀ NGƯỜI — Trốn đến hết giờ!"*. Trong lúc hiện thẻ, **cả hai phe đều bị khóa hành động**, đồng hồ chạy trước chưa đếm. Hết thời gian thì thẻ ẩn và giai đoạn chạy trước bắt đầu như hiện nay.
3. **Về Menu**: nút "Thoát" ở bảng kết quả và ở menu tạm dừng đổi thành **"Về Menu"**, nạp scene `Menu` sau khi đặt lại `Time.timeScale = 1`. "Chơi lại" giữ nguyên (nạp lại `Game`).

## Không làm (non-goals)

- Màn Cài đặt (âm lượng, độ nhạy), tự chọn phe, sảnh chờ 8 slot, âm thanh, vật phẩm.
- Nút Back của Android.
- Thay đổi thời lượng chạy trước / trận đấu.

## Anti-goals

- Chuyển scene để lại game đứng hình (`timeScale = 0`) hoặc nhân vật/đối tượng thừa từ trận trước.
- Thẻ vai trò làm sai lệch luật chạy trước (Runner được chạy sớm hơn 10s, hoặc Chaser được thả sớm).

## Các cách đã cân nhắc

| Cách | Đánh đổi | Quyết định |
| --- | --- | --- |
| 1. Thẻ vai trò trong scene Game, trước 10s chạy trước | Mỗi trận dài thêm ~2.5s; mọi thứ nằm trong Game, dùng lại cách random vai trò ở `SpawnManager` | **Chọn** (người dùng chọn) |
| 2. Hiện vai trò trên màn Loading | Không tốn thêm thời gian, nhưng phải chuyển việc random vai trò ra khỏi `SpawnManager` và truyền qua scene | Loại |

Luồng `Loading → Menu → Game` (Loading chỉ chạy một lần khi mở app) là yêu cầu của người dùng.

## Hướng xây dựng (đề xuất của Claude — có thể sửa)

- **Pha mới `MatchPhase.RoleReveal`** đứng trước `HeadStart` trong `MatchController`, thời lượng cấu hình trên Inspector (`roleRevealDurationSeconds`, mặc định 2.5s; đặt 0 để bỏ qua — giữ được các test hiện có bằng cách đặt 0). Trong pha này khóa `IsActionLocked = true` cho cả Chaser lẫn Runner; khi chuyển sang `HeadStart` thì áp khóa như hiện nay (chỉ Chaser).
- **Hiển thị**: thêm vào `MatchHudView` một panel thẻ vai trò + `RenderRoleReveal(bool active, Team playerTeam)`, cùng hàm tĩnh thuần `FormatRoleRevealTitle/Subtitle(Team)` để test được như `FormatHeadStartTitle` hiện có. Ẩn nút kỹ năng/joystick không bắt buộc vì nhân vật đã bị khóa.
- **Chuyển scene**: một lớp tĩnh nhỏ `SceneNames` (hằng `Loading`, `Menu`, `Game`) để không rải chuỗi. `MatchController.Quit()` đổi thành `ReturnToMenu()` (đặt lại `timeScale`, nạp `Menu`); sự kiện/nút đổi tên tương ứng trên `MatchHudView`/`MatchEndView`.
- **Menu**: `MainMenuView` (MonoBehaviour, không cần DI) giữ 3 nút + panel Hướng dẫn. Dùng UI cùng kiểu với HUD hiện có (uGUI `Text`/`Button`), font đã hiển thị được tiếng Việt.
- **Test (EditMode)**: pha `RoleReveal` đi trước `HeadStart`; trong pha này cả hai phe bị khóa và đồng hồ chạy trước không giảm; hết thời gian thì chuyển sang `HeadStart` với khóa chỉ Chaser; `RoleRevealDuration = 0` đi thẳng vào `HeadStart`; nhãn thẻ vai trò đúng cho hai phe. Cập nhật test `MatchControllerTests` hiện có nếu bị ảnh hưởng.
- **Kiểm chứng**: biên dịch bằng `dotnet build` (Editor đang mở, batch mode thường bị chặn); dựng scene Menu và nối nút qua Unity Editor/MCP; chạy thử tay vòng Loading → Menu → Game → Kết quả → Menu → Chơi hai trận liên tiếp.

## Để lại cho người làm

- Bố cục, màu sắc, cỡ chữ của Menu và thẻ vai trò (theo phong cách HUD hiện có).
- Nội dung chữ cụ thể của bảng Hướng dẫn.
