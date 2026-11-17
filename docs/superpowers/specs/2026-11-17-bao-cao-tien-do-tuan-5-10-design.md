# Thiết kế: Cập nhật báo cáo tiến độ Tuần 5–10 (Nhóm 24)

Ngày: 2026-11-17 · Quy mô: thay đổi nhỏ (một tài liệu, dữ liệu đã thu thập xong)

## 1. Ý định & lý do

Báo cáo tiến độ hiện có (`Downloads/Báo cáo tiến độ - Nhóm 24.md`) dừng ở Tuần 4,
trong khi repo đã có thêm ~40 commit từ 23/09 đến 25/10 (BT chi tiết hai phe, bắt
giam, vòng đời trận đấu, HUD, animation NPC, camera orbit, 3 kỹ năng, lobby
head-start). Cần: (a) viết tiếp phần đã làm thành Tuần 5–7, (b) lập kế hoạch
Tuần 8–10 để ra sản phẩm game hoàn chỉnh với hệ thống NPC đầy đủ chức năng,
(c) sửa những chỗ đặc tả trong báo cáo không còn khớp code.

## 2. Mục tiêu

- Báo cáo Tuần 5, 6, 7 viết đúng văn phong và cấu trúc 5 mục như Tuần 1–4
  (Mục tiêu / Nội dung đã thực hiện / Kết quả & minh chứng / Khó khăn & giải pháp
  / Kế hoạch tuần sau), mọi số liệu lấy từ code thật.
- Kế hoạch Tuần 8–10 đủ chi tiết để thực thi, lấp hết phần còn thiếu của đặc tả.
- Phần đầu báo cáo (tổng quan, coreloop, kế hoạch triển khai) khớp với sản phẩm
  đang chạy; chỗ nào lệch đề cương thì ghi rõ là điều chỉnh có lý do.

## 3. Không làm (non-goals)

- Không sửa file `Đề tài ĐATN.docx.md` (đề cương gốc giữ nguyên làm mốc lịch sử);
  mọi điều chỉnh so với đề cương được ghi trong báo cáo tiến độ.
- Không viết lại Tuần 1–4 (chỉ sửa số liệu sai nếu phát hiện).
- Không viết code trong phạm vi việc này — Tuần 8–10 là kế hoạch, không phải triển khai.
- Không xuất .docx (có thể làm sau khi nội dung được chốt).

## 4. Anti-goals

- Báo cáo kê tính năng không có trong code (ví dụ nhận là đã có vật phẩm, lobby
  nhiều người, hay cơ chế học máy).
- Nhồi toàn bộ phần đã làm vào ít tuần để "lấy chỗ", khiến mốc tuần lệch lịch commit.

## 5. Ràng buộc

- Tổng đồ án 10 tuần; đang ở cuối Giai đoạn 2 → còn 3 tuần (8, 9, 10).
- Hai cơ chế thích ứng (theo định nghĩa của nhóm) = Mode Direction cho Player và
  Mode Agent cho NPC, đã chạy trong game từ Tuần 2–3 ⇒ Giai đoạn 2 coi như đạt.
- Định dạng markdown kiểu docx như file hiện tại (tiêu đề in đậm, escape `\-`,
  `\+`) để dán sang Word không vỡ cấu trúc.

## 6. Các cách đã cân nhắc

| Cách | Nội dung | Đánh đổi |
| --- | --- | --- |
| A (chọn) | Đã làm = Tuần 5‑6‑7 theo 3 mốc commit (23/09, 04/10, 24–25/10); Tuần 8‑10 hoàn thiện | Khớp lịch commit; còn 3 tuần đủ lấp phần thiếu; phải dịch mốc giai đoạn trong kế hoạch gốc |
| B | Đã làm = Tuần 5‑8 (chia 4 tuần) | Giữ nguyên "Giai đoạn 2 = Tuần 5‑8" nhưng chỉ còn 2 tuần ⇒ buộc cắt vật phẩm, lobby riêng |
| C | Nén đã làm vào Tuần 5‑6, còn 4 tuần hoàn thiện | Nhiều thời gian đệm nhất nhưng 2 tuần báo cáo nặng hơn thực tế |

Người hướng dẫn dự án chọn **A**.

## 7. Nội dung đã quyết

### 7.1 Kế hoạch triển khai (cập nhật trong báo cáo)

- Giai đoạn 1 (Tuần 1–4): BT tĩnh chạy ổn cho cả NPC đuổi và NPC chạy. **Đã xong.**
- Giai đoạn 2 (Tuần 5–7): Hai cơ chế thích ứng (Mode Direction / Mode Agent) hoạt
  động trong game, BT chi tiết hai phe, vòng đời trận đấu và kỹ năng. **Đã xong.**
- Giai đoạn 3 (Tuần 8–10): Hoàn thiện gameplay còn thiếu, nội dung, kiểm thử,
  build và báo cáo tổng.

### 7.2 Phân bổ nội dung đã làm

- **Tuần 5** (mốc 23/09): `ChaserAIBrain` (BT đuổi: Selector chase → guard cage →
  wander, ghi nhớ vị trí thấy cuối), `RunnerAIBrain` (flee + kỹ năng thoát thân +
  giải cứu đồng đội), kỹ năng `Gun` + `CaptureServiceStub`/`CageStub` (bắt giam),
  `SpeedBoost`/`Decoy` phiên bản đầu.
- **Tuần 6** (mốc 23/09): `SpawnManager` + `CharacterFactory` + `ChaserCoordinator`
  (trận đủ 4v4 = 1 player + 7 NPC, 4 điểm spawn mỗi phe), cân bằng tốc độ 6 vs 5,
  `MatchEvaluator`/`MatchClock`/`MatchController` (luật thắng/thua, đồng hồ 90s,
  freeze/replay/quit), HUD trong trận + bảng kết thúc.
- **Tuần 7** (mốc 04/10 + 24–25/10): hệ thống animation NPC; làm mượt chuyển động
  và góc xoay (`Slerp` 16/s, `RotateTowards` 540°/s, chặn `SetDestination` dưới
  0.15 m); chống state thrashing (target stickiness, flee hysteresis 6 m → 8.1 m,
  xuyên nhân vật khi xét tầm nhìn); flee quét 7 góc (0°, ±40°, ±80°, ±120°);
  camera orbit qua UI touch pad + di chuyển tương đối theo camera; HUD kỹ năng
  theo vai trò + vòng cooldown + menu pause + aim assist; `Decoy` sinh phân thân
  AI độc lập (3 s); `EggProjectile` (22 m/s, tầm 8 m, SphereCast); `SpeedBoost`
  cho Chaser (1.6×, 3 s, cd 6 s) kèm AI tự kích hoạt; lobby head-start 10 s khóa
  Chaser + đếm ngược 3‑2‑1‑READY. Kiểm thử: 119/119 EditMode pass + 4 bộ PlayMode.

### 7.3 Kế hoạch Tuần 8–10

- **Tuần 8 — Hoàn thiện lõi bắt giam/giải cứu và chức năng NPC**: thay
  `CaptureServiceStub`/`CageStub` bằng `JailSystem` thật (lồng giam prefab, UI "bị
  bắt", khóa hành động); cơ chế giải cứu (Runner đứng trong bán kính lồng N giây →
  đồng đội tự do, Chaser canh lồng cản được); hoàn thiện NPC: Chaser tìm kiếm theo
  khu vực khi mất dấu + phân vùng tuần tra + canh lồng; Runner trốn sau chướng
  ngại (cover point); cập nhật `MatchEvaluator` và bộ test cho luồng giải cứu.
- **Tuần 9 — Vòng lặp trận đấu đầy đủ và nội dung**: scene Lobby/sảnh chờ (8 slot,
  nút bắt đầu, chia role ngẫu nhiên + màn hiện role), hệ thống vật phẩm (2–3 loại
  cho Runner, NPC biết nhặt qua nhánh BT mới), Menu chính + luồng scene
  Menu → Lobby → Game → Kết thúc → Lobby, âm thanh (SFX bắn/bị bắt/giải cứu, nhạc
  nền, UI).
- **Tuần 10 — Cân bằng, kiểm thử, báo cáo**: cân bằng số liệu (tốc độ, cooldown,
  tầm nhìn, thời lượng trận), tối ưu hiệu năng 8 NPC (NavMesh + animation + tick
  rate), build Android/PC, playtest và sửa bug, toàn bộ test xanh, viết báo cáo
  tổng (chương triển khai + chương kiểm thử/đánh giá), slide và video demo.

### 7.4 Sửa đặc tả cho khớp sản phẩm (ghi rõ trong báo cáo là điều chỉnh)

| Điểm | Đề cương / báo cáo cũ | Sản phẩm thực tế | Xử lý |
| --- | --- | --- | --- |
| Quy mô đội | 1 Gà + 4–5 Người (đề cương) / 4v4 (báo cáo) | 4v4, 1 player + 7 NPC | Giữ 4v4, ghi chú đã điều chỉnh từ đề cương |
| Góc nhìn | Thứ nhất (báo cáo) / thứ 3 cho Gà (đề cương) | Thứ 3 orbit cho cả hai phe, điều khiển cảm ứng | Sửa thành thứ 3 orbit, lý do: nền tảng mobile |
| Thời gian trốn trước | 30 s, Gà bị nhốt trong lồng | Head-start 10 s, Chaser bị khóa hành động tại điểm spawn | Ghi 10 s cấu hình được trên Inspector |
| Thời lượng trận | 5 phút | 90 s | Giữ 90 s mặc định (vòng lặp demo ngắn, cấu hình được); nêu lý do |
| Vật phẩm | Có trong coreloop | Chưa có | Đưa vào kế hoạch Tuần 9 |
| Lobby | Sảnh chờ + 15 s xem role | Chưa có scene riêng | Đưa vào kế hoạch Tuần 9 |

## 8. Phần để người viết tự quyết

- Câu chữ từng mục, mức chi tiết số liệu kỹ thuật (giữ ngang Tuần 1–4).
- Mục "Khó khăn & giải pháp" của Tuần 5–7 chọn từ các vấn đề thật đã ghi trong
  `docs/feature_design_documentation.md` (state thrashing, phân thân kẹt vật lý,
  đạn xuyên mục tiêu ở tốc độ cao, Chaser bị khóa vẫn cần xoay camera).

## 9. Đầu ra

`docs/bao_cao_tien_do_nhom24_tuan_1_10.md` trong repo: toàn bộ báo cáo (phần đầu
đã sửa + Tuần 1–4 giữ nguyên + Tuần 5–7 mới + kế hoạch Tuần 8–10), định dạng
markdown kiểu docx để dán sang Word.
