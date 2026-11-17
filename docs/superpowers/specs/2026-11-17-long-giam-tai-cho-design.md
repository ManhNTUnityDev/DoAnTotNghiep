# Thiết kế: Lồng giam tại chỗ (bỏ cơ chế giải cứu)

Ngày: 2026-11-17 · Quy mô: thay đổi nhỏ–trung (một hệ thống, có dọn code cũ)

## 1. Ý định & lý do

Hệ thống bắt giam hiện tại là bản tạm: `CaptureServiceStub` chỉ đặt
`CaptureState = Jailed` và ghi một `CageStub` (class dữ liệu thuần), không có lồng
giam nào hiện hữu trong không gian 3D. Người chơi bị bắt chỉ thấy nhân vật đứng khóa
tại chỗ. Mục tiêu: Runner bị bắt sẽ bị nhốt trong một lồng giam thật dựng ngay tại vị
trí bị bắt, nhìn thấy được trên bản đồ.

Đồng thời nhóm quyết định **bỏ hoàn toàn cơ chế giải cứu**: Runner bị bắt là mất đến
hết trận. Việc này làm một phần code hiện có trở thành vô dụng và phải dọn.

## 2. Mục tiêu

- Runner trúng trứng → sinh lồng giam tại chỗ, nhân vật bị nhốt bên trong, khóa hành
  động, có animation trạng thái bị nhốt.
- Lồng tồn tại đến hết trận, nhìn thấy rõ trên bản đồ (đánh dấu nơi đã xảy ra pha bắt).
- Các nhân vật khác không chạy xuyên qua lồng; NPC biết lách qua lồng mà không bị kẹt.
- Dọn sạch code giải cứu và code chỉ tồn tại để phục vụ giải cứu.
- Luật thắng/thua không đổi (`MatchEvaluator` vốn đã coi bị bắt là mất vĩnh viễn).

## 3. Không làm (non-goals)

- Khu giam trung tâm (đã cân nhắc và loại — xem mục 6).
- Mọi hình thức giải cứu, phá lồng, hồi sinh.
- Camera xem đồng đội khi bị bắt (chỉ sửa lại thông điệp banner cho khớp thực tế).
- Model 3D lồng giam hoàn chỉnh: dùng lồng dựng từ primitive như cách `EggProjectile`
  đang dùng Sphere tạm.
- Carve NavMesh lúc runtime (dùng avoidance thay vì bake lại).

## 4. Anti-goals

- NPC bị kẹt vào lồng hoặc đứng rung quanh lồng.
- Lồng đẩy chính nhân vật bị nhốt bật ra ngoài hoặc kẹt vào thanh lồng.
- Để lại nhánh BT và service chết (canh lồng, điều phối canh lồng) chỉ vì "có sẵn".

## 5. Ràng buộc

- Giữ interface `ICaptureService` để không phá DI (`GameLifetimeScope`) và các điểm
  tiêm sẵn có (`Gun.Configure`, `EggProjectile.Configure`).
- NavMesh đã bake tĩnh cho bản đồ; lồng sinh lúc runtime nên không được dựa vào bake lại.
- Nhân vật dùng `CharacterController` (player) và `NavMeshAgent` ở Mode Agent (NPC).

## 6. Các cách đã cân nhắc

| Cách | Nội dung | Đánh đổi |
| --- | --- | --- |
| **Lồng tại chỗ (chọn)** | Bắt ở đâu, lồng mọc lên ngay đó | Khớp dữ liệu `CageStub.Position` sẵn có, ít thay đổi nhất, bản đồ dần hiện dấu vết các pha bắt |
| Khu giam trung tâm | Dịch Runner bị bắt về khu giam dựng sẵn giữa map | Dễ thấy tiến độ trận, nhưng phải dựng khu giam, xử lý dịch chuyển + `NavMeshAgent.Warp` và sắp chỗ cho nhiều người; **không còn giải cứu nên khu giam không tạo thêm lựa chọn gameplay nào** |
| Lồng là child của nhân vật | Gắn lồng vào chính nhân vật bị bắt | Đơn giản nhưng lồng sống chết theo nhân vật, khó quản lý danh sách lồng và khó đặt `NavMeshObstacle` độc lập |

Người hướng dẫn dự án chọn **lồng tại chỗ**.

## 7. Thiết kế

### 7.1 Prefab lồng — `Assets/Prefabs/Cage.prefab`

- Dựng từ primitive: 4 cột đứng + các thanh ngang + nóc, material kim loại tối, phần
  thân để hở để thấy nhân vật bên trong.
- Một `BoxCollider` bao quanh (chặn nhân vật khác chạy xuyên).
- `NavMeshObstacle` ở chế độ **avoidance (không carve)**: NPC `NavMeshAgent` tự lách
  qua, không cần bake lại NavMesh lúc runtime.
- Component `Cage` (thay `CageStub`): giữ `Occupant`, `Position`; khi khởi tạo gọi
  `Physics.IgnoreCollision` giữa collider lồng và collider của người bị nhốt — lặp lại
  mẫu đã dùng trong `Decoy.SanitizeCloneComponents` để không đẩy/kẹt nhân vật.

### 7.2 `JailService` (thay `CaptureServiceStub`)

- Vẫn implement `ICaptureService`; `ActiveCages` đổi kiểu từ `IReadOnlyList<CageStub>`
  sang `IReadOnlyList<Cage>`.
- `Capture(runner)`:
  1. Bỏ qua nếu `runner` null, không phải `Team.Runner`, hoặc đã `Jailed` (giữ nguyên
     bộ điều kiện của bản stub).
  2. Đặt `runner.CaptureState = Jailed` (setter tự `Stop()` như hiện nay).
  3. Tắt `NavMeshAgent` của nhân vật (nếu có) để agent không tiếp tục steer trong lồng.
  4. `Instantiate` prefab lồng tại vị trí nhân vật, snap xuống mặt đất/NavMesh gần nhất.
  5. Thêm vào `ActiveCages`, phát `OnRunnerCaught`.
- Prefab lồng được cấp qua `SpawnSettings` (đã là nơi gom tham chiếu scene) để
  `JailService` vẫn là lớp thuần, inject được.

### 7.3 Animation & HUD

- Bỏ comment dòng `animator.SetBool(IsJailedHash, ...)` trong
  `CharacterAnimationController` để nhân vật bị nhốt chuyển sang state Jailed đã cấu
  hình trong `NPC.controller`.
- `MatchHudView`: sửa thông điệp `spectatingBanner` từ "Bạn đã bị bắt — đang xem đồng
  đội…" thành thông báo bị bắt và chờ hết trận (không còn giải cứu, cũng chưa có
  camera xem đồng đội).

### 7.4 Dọn code giải cứu

- `RunnerAIBrain`: xoá nhánh rescue trong `BuildTree` cùng `HasJailedTeammate`,
  `GoRescue`, `FindSafeRescueTarget`, và `IsGuardedByThreat` nếu không còn nơi dùng.
  Cây còn lại: *chạy trốn khi bị áp sát* → *tuần tra*.
- `ChaserAIBrain`: xoá nhánh `GuardCage` và mọi tham chiếu `Blackboard.GuardPost`.
  Cây còn lại: *bắn* → *đuổi* → *tuần tra*.
- Xoá `ChaserCoordinator` (+ `.meta`), bỏ `builder.RegisterEntryPoint<ChaserCoordinator>()`
  trong `GameLifetimeScope`, bỏ tham số tương ứng trong `SpawnManager`.
- `AIConfig`: bỏ `rescueThreatRadius` / `RescueThreatRadius`.
- `Blackboard`: bỏ `GuardPost` nếu không còn nơi dùng.
- Test: xoá `ChaserCoordinatorTests`, xoá các test rescue trong `RunnerAIBrainTests` và
  test canh lồng trong `ChaserAIBrainTests`; đổi `CaptureServiceStubTests` thành
  `JailServiceTests` và bổ sung ca: sinh lồng đúng vị trí, không sinh lồng thứ hai cho
  cùng một Runner, nhân vật bị nhốt không nhận lệnh di chuyển.

### 7.5 Thứ tự triển khai

1. Dọn code giải cứu + canh lồng, chạy test cho xanh trước (tách riêng, dễ xem lại).
2. `Cage` + `JailService` + test.
3. Prefab lồng và nối vào `SpawnSettings` trong scene.
4. Animation `IsJailed` + sửa banner HUD.
5. Playtest: bắt 1 Runner, kiểm tra lồng, kiểm tra NPC khác không kẹt quanh lồng.

## 8. Tiêu chí hoàn thành

- Bắn trúng Runner → lồng hiện ra tại chỗ, Runner đứng trong lồng với animation Jailed,
  không trượt, không nhận lệnh.
- 7 NPC còn lại chạy quanh lồng bình thường, không kẹt, không rung.
- Toàn bộ EditMode + PlayMode test xanh, không còn tham chiếu nào tới rescue/guard.

## 9. Việc kéo theo ngoài code

- Cập nhật báo cáo (`docs/bao_cao_tien_do_nhom24_tuan_1_10.md`): coreloop và Tuần 5 đang
  ghi "giải cứu đồng đội"; kế hoạch Tuần 8 đang lấy giải cứu làm nội dung chính — phải
  đổi thành lồng giam tại chỗ + ghi rõ quyết định bỏ giải cứu và lý do; xuất lại .docx.

## 10. Phần để người triển khai tự quyết

- Kích thước/tỉ lệ cụ thể của lồng và material.
- Có thêm hiệu ứng DOTween khi lồng xuất hiện hay không.
- Nếu playtest thấy NPC vẫn vướng lồng: hạ xuống phương án lồng chỉ còn visual (bỏ
  collider chặn), ghi lại lý do trong báo cáo.

## 11. Những chỗ triển khai lệch thiết kế (ghi lại khi làm)

- **Không dựng `Assets/Prefabs/Cage.prefab`**: không mở được Unity Editor trong phiên làm
  việc, nên lồng được dựng bằng code (`Cage.BuildPrimitive`). `SpawnSettings.cagePrefab`
  vẫn còn, bỏ trống thì dùng lồng primitive — gán prefab/model thật sau là xong.
- **Không tắt `NavMeshAgent` của người bị nhốt**: setter `CaptureState` đã gọi
  `movement.Stop()`, hàm này `ResetPath()` và đưa movement về Mode Direction, nên tắt
  agent chỉ thêm rủi ro cho vòng đồng bộ `agent.nextPosition`.
- **Thêm kiểm tra tham số Animator**: `CharacterAnimationController` chỉ gọi
  `SetBool("IsJailed", …)` khi controller có khai báo tham số đó, tránh cảnh báo mỗi
  khung hình trên nhân vật/controller chưa có.
- **Sửa text banner trực tiếp trong `Game.unity`** (một dòng `m_Text`) thành
  "Bạn đã bị bắt — chờ hết trận".
- **Kiểm thử**: chạy được kiểm tra biên dịch ngoài Unity (`dotnet` + DLL của Unity
  6000.0.72f1) cho cả `ChaseGame` và hai assembly test — 0 lỗi. Chưa chạy được Unity
  Test Runner trong phiên này.
