# So sánh Behavior Tree với FSM cho AI NPC — Thiết kế

Ngày: 2026-11-23 · Nhánh: `Manh/Dev`

## Mục đích

Trọng tâm khi bảo vệ đồ án là hai thứ (người dùng chốt):

1. **Kịch bản test** dựng sẵn, chạy lại được, chứng minh NPC hành xử đúng.
2. **So sánh có số đo** giữa Behavior Tree (kiến trúc đang dùng) và một kiến trúc khác — chọn FSM, đúng phương án đối chiếu đã phân tích lý thuyết ở Tuần 1 (lập luận "số chuyển tiếp tăng O(N²)").

Hiện tại lập luận đó chưa có bằng chứng: cây Chaser chỉ 3 nhánh, cây Runner 2 nhánh — ở quy mô này FSM cũng gọn như BT. Thiết kế này tạo ra bằng chứng: hai bộ não cho mỗi phe, cùng một bộ kịch bản, cùng một thí nghiệm mở rộng, và số liệu từ hàng trăm trận mô phỏng để đưa vào Chương 4.

## Mục tiêu

1. **Bản FSM cho cả hai phe** (`ChaserFsmBrain`, `RunnerFsmBrain`), hành vi tương đương bản BT hiện có:
   - Chaser: Bắn → Đuổi → Tuần tra (bắn khi mục tiêu trong tầm và súng sẵn; đuổi kèm SpeedBoost; nhớ vị trí thấy cuối qua `Destination`).
   - Runner: Chạy trốn (kèm SpeedBoost/Decoy, chọn điểm chạy tốt nhất, hysteresis `FleeExitMultiplier`) → Tuần tra; xử lý cả trường hợp phân thân (`IsClone`).
2. **Thí nghiệm mở rộng**: sau khi hai bản cơ sở chạy đúng và đã commit, thêm **cùng một hành vi mới** vào cả bốn bộ não, mỗi lần một commit riêng, để `git diff --stat` trở thành số liệu "chi phí mở rộng":
   - Chaser: **Tìm quanh chỗ mất dấu** — khi mất mục tiêu, đi tới vị trí thấy cuối rồi quét vài điểm quanh đó trong một khoảng thời gian trước khi về tuần tra. Nếu thấy lại mục tiêu giữa chừng thì quay về đuổi/bắn.
   - Runner: **Nấp sau vật cản** — khi bị đe dọa và có vật cản phù hợp gần đó, chạy tới điểm mà đường nhìn từ Chaser bị che; không có thì chạy trốn như cũ.
   - Đây cũng chính là phần còn dở của Tuần 8.
   - **Cách tách commit** *(Claude quyết định)*: commit đầu tiên chỉ chứa phần dùng chung (hàm hành động mới như tìm điểm quét, tìm chỗ nấp, kiểm tra đường nhìn, và tham số mới trong `AIConfig`). Sau đó mỗi bộ não một commit **chỉ chứa phần ra quyết định**. Nếu không tách, toàn bộ code dùng chung rơi vào commit BT đầu tiên, làm BT trông tốn hơn sai sự thật.
   - **Bộ nhớ của hành vi** (bộ đếm thời gian tìm kiếm, danh sách điểm quét): bên nào cần thêm chỗ chứa trạng thái thì tính vào chi phí của bên đó. Ví dụ trường mới trên `Blackboard` mà chỉ BT dùng thì tính cho BT, trường trong lớp trạng thái FSM thì tính cho FSM. Báo cáo ghi rõ điều này.
3. **Kịch bản tình huống (tầng 1)** — PlayMode test, mỗi kịch bản chạy cho **cả bản BT lẫn bản FSM** của phe tương ứng, kết quả đạt/không đạt.
4. **Mô phỏng nguyên trận (tầng 2)** — chạy tự động nhiều trận 8 AI trên bản đồ thật, ghi CSV, sinh bảng và biểu đồ cho báo cáo.
5. **Số liệu tĩnh**: số trạng thái/chuyển tiếp của FSM, số node của BT, số dòng code mỗi bộ não, diff của thí nghiệm mở rộng.
6. **Chuỗi tăng dần (scaling series)** — người dùng chọn thêm, để minh họa luận điểm O(N²) của Tuần 1 bằng một đường cong chứ không chỉ một điểm dữ liệu (xem §6a).

## Không làm (non-goals)

- Viết báo cáo Chương 4 (thiết kế này chỉ tạo ra dữ liệu và biểu đồ; viết là việc riêng).
- Kiến trúc thứ ba (GOAP, Utility AI…).
- Cho người chơi chọn BT/FSM trong game, hay đưa chế độ mô phỏng vào bản build.
- Vật phẩm, âm thanh, Cài đặt (các việc khác của Tuần 9).
- Phân vùng tuần tra cho Chaser (phần còn lại của Tuần 8, để sau).

## Anti-goals

- **FSM viết yếu để BT thắng dễ.** FSM phải viết đúng chuẩn, đủ hành vi như BT. Nếu ở quy mô nhỏ FSM gọn hơn, số liệu sẽ nói vậy và báo cáo ghi nhận.
- So sánh mà hai bên khác nhau ở nhiều chỗ ngoài bộ não (khác cảm biến, khác nhịp tick, khác cách di chuyển) — kết luận sẽ không đứng vững.
- Mô phỏng làm hỏng chế độ chơi thường (người chơi vẫn vào Menu → Game như hiện nay, không thấy gì khác).
- Tăng tốc mô phỏng tới mức vật lý sai (xuyên tường) làm số liệu lệch so với trận thật.

## Các cách đã cân nhắc

| Cách | Đánh đổi | Quyết định |
| --- | --- | --- |
| Chỉ lập luận trên giấy (sơ đồ FSM tương đương) | Nhanh, nhưng không có số đo | Loại (người dùng chọn viết FSM thật) |
| So sánh chỉ phe Chaser | Ít việc hơn, Chaser có nhiều trạng thái hơn | Loại — người dùng chọn **cả hai phe** |
| Chỉ kịch bản tình huống, không mô phỏng trận | Rẻ hơn, nhưng chỉ chứng minh "đúng", không có tỉ lệ thắng/hiệu năng | Loại — người dùng chọn **mô phỏng nguyên trận** |
| **A. Tách phần dùng chung khỏi `AIBrain`; FSM kiểu state-pattern + bảng chuyển tiếp tường minh; mô phỏng trong scene `Game`, bật bằng menu Editor** | Tốn công tái cấu trúc `AIBrain`; đổi lại chỉ một biến số thay đổi, chuyển tiếp đếm được từ code, chạy trên map và luật thật | **Chọn** (người dùng chọn) |
| B. FSM `enum` + `switch` một lớp; mô phỏng bằng PlayMode test | Code ngắn hơn; nhưng chuyển tiếp rải trong `if` khó đếm, dễ bị coi là FSM viết ẩu; test dài dễ timeout, khó trình diễn | Loại |

## Thiết kế

### 1. Tách lõi dùng chung khỏi `AIBrain`

Hiện `AIBrain` (`Assets/Scripts/AI/AIBrain.cs`) vừa là "vật chủ" (Blackboard, Sensor, nhịp tick lệch pha, `Wander`, `TryRandomPoint`, `PlanarDistance`) vừa gắn chặt với BT (`BuildTree`, `root.Tick`).

- Tách thành lớp cơ sở chung (tên đề xuất `AIBrain` giữ nguyên vai trò vật chủ) với một điểm quyết định trừu tượng kiểu `Decide(Blackboard)`; `Update` giữ nguyên: bỏ qua khi `!CanAct`, cộng dồn theo `TickRateHz`, gọi `sensor.Sense` rồi `Decide`.
- Bản BT: lớp con dựng cây một lần, `Decide` = `root.Tick(bb)`. `ChaserAIBrain`/`RunnerAIBrain` giữ tên (để test cũ và `Decoy` không phải đổi nhiều).
- Bản FSM: lớp con giữ trạng thái hiện tại + bảng chuyển tiếp; `Decide` = đánh giá chuyển tiếp từ trạng thái hiện tại, đổi trạng thái nếu cần (gọi `Exit`/`Enter`), rồi `Tick` trạng thái.
- Các hàm hành động (ví dụ `AimAndShoot`, `ChaseTarget`, `EscapeAndFlee`, `PickBestFleePoint`, `ThreatNear`) được đưa ra chỗ dùng chung để **hai bản gọi đúng cùng một đoạn code hành động** — chỉ khác cách chọn hành động nào chạy. Điều này đảm bảo cuộc so sánh chỉ đo phần ra quyết định.
- `TickForTests()` có ở cả hai bản để EditMode test cũ chạy được cho cả hai.

### 2. FSM

- Mỗi trạng thái là một lớp nhỏ với `Enter / Tick / Exit`.
- Mỗi bộ não FSM khai báo **một bảng chuyển tiếp duy nhất**: danh sách `(từ trạng thái, điều kiện, tới trạng thái)`, có thể có chuyển tiếp "từ bất kỳ" nếu cần. Số dòng của bảng = số chuyển tiếp đưa vào báo cáo; nên có cách đếm/in ra tự động (ví dụ một hàm trả về danh sách chuyển tiếp để test và công cụ số liệu đọc).
- Lý do chọn kiểu này: đây là cách viết FSM chuẩn được dạy và dùng thực tế, nên không bị coi là "FSM viết ẩu", và số chuyển tiếp đếm được trực tiếp từ code.

### 3. Chọn bộ não lúc spawn

- `SpawnManager.AttachAIBrain` chọn lớp theo một cấu hình "loại bộ não cho mỗi phe" (`BT` hoặc `FSM`). Mặc định cả hai phe = `BT`, nên chơi thường không đổi gì.
- **Phân thân Decoy** (`Abilities/Decoy.cs`, hiện cứng `RunnerAIBrain`): dùng cùng loại bộ não với nhân vật tạo ra nó. *(Claude quyết định: nếu phân thân luôn dùng BT thì trận "FSM Runner" vẫn có một phần BT, làm bẩn số liệu.)* `Decoy.SanitizeCloneComponents` phải giữ đúng loại bộ não Runner tương ứng thay vì kiểm tra `is RunnerAIBrain`.

### 4. Kịch bản tình huống (tầng 1)

PlayMode test trên mặt phẳng NavMesh dựng lúc chạy (giống `ChaserAIPlayTests` hiện có), cộng vật cản khi cần. Mỗi kịch bản được tham số hóa theo loại bộ não và chạy cho cả hai.

Chaser:
- C1. Runner trong tầm nhìn nhưng ngoài tầm bắn → Chaser tiến lại gần.
- C2. Runner trong tầm bắn, súng sẵn → bắn và bắt được.
- C3. Không thấy ai → tuần tra (di chuyển, không đứng yên).
- C4. Runner khuất sau tường → Chaser đi tới vị trí thấy cuối, không quay ngoắt về điểm tuần tra cũ.
- C5 *(sau thí nghiệm mở rộng)*. Mất dấu → tìm quanh vị trí thấy cuối trong khoảng thời gian cấu hình rồi mới về tuần tra; thấy lại thì đuổi tiếp.

Runner:
- R1. Chaser vào bán kính đe dọa → chạy xa ra, khoảng cách tăng.
- R2. Bị đe dọa và kỹ năng sẵn → dùng SpeedBoost/Decoy.
- R3. Bị dồn vào góc tường → vẫn chọn được điểm chạy hợp lệ trên NavMesh, không đứng kẹt.
- R4. Ở mép bán kính đe dọa → không bị giật qua lại giữa chạy trốn và tuần tra (hysteresis).
- R5 *(sau thí nghiệm mở rộng)*. Có vật cản gần → chạy tới điểm bị che khỏi tầm nhìn Chaser.

Kịch bản C1–C4, R1–R4 phải đạt cho cả bốn bộ não **trước** thí nghiệm mở rộng — đó là bằng chứng hai bản tương đương. Test EditMode hiện có của `ChaserAIBrainTests`/`RunnerAIBrainTests` cũng nên chạy được cho bản FSM nếu chuyển sang tham số hóa không tốn kém.

### 5. Mô phỏng nguyên trận (tầng 2)

- **Bật bằng mục menu trong Editor** (ví dụ `ChaseGame/Benchmark/Run`), mở scene `Game` và vào Play. Chế độ này chỉ có trong Editor, không vào build.
- **Một phiên mô phỏng** giữ danh sách trận cần chạy và sống qua các lần nạp scene. Mỗi trận:
  - Không có người chơi: cả 8 nhân vật đều là AI (`SpawnManager` bỏ qua bước chọn người chơi khi mô phỏng). HUD/camera phải chạy được khi `roster.PlayerCharacter == null`.
  - Bỏ thẻ vai trò (`RoleRevealDurationSeconds = 0`); giữ nguyên head-start 10s và trận 90s như luật thật.
  - Đặt `Random.InitState(seed)` và ghi seed vào CSV để tra cứu. *(Claude quyết định)*: seed **không** nhằm phát lại y hệt một trận, vì pha tick ngẫu nhiên, `Wander` và `deltaTime` thay đổi theo khung hình. Các cặp đấu và lượt 1× với 3× được so theo **phân bố** kết quả trên nhiều trận, không so từng trận một.
  - Chạy nhanh khoảng **3×** (`Time.timeScale`).
  - Khi trận kết thúc (`MatchController` đang đặt `timeScale = 0`), ghi một dòng CSV rồi nạp lại `Game` cho trận tiếp theo (dùng thời gian thực, không phụ thuộc `timeScale`, đi qua `PrepareSceneExit`).
- **Cặp đấu**: Chaser BT × Runner BT, FSM × FSM, BT × FSM, FSM × BT. Mặc định khoảng **30 trận mỗi cặp** (≈ 1 giờ chạy ở 3×); số trận và tốc độ chỉnh được.
- **Mỗi dòng CSV** (tối thiểu): cặp đấu, seed, phe thắng, số Runner bị bắt, thời điểm từng lần bắt (hoặc lần đầu/lần cuối), thời gian trận thực tế, thời gian mỗi tick trung bình và lớn nhất theo từng phe (đo bằng `Stopwatch` quanh `Decide`, ghi riêng với `Sense`). Con số này gồm cả phần hành động dùng chung (`MoveTo`, `NavMesh.SamplePosition`…), nên chỉ dùng để chứng minh "cả hai đều đủ nhẹ cho 8 NPC". *(Claude quyết định)*: chi phí **riêng phần chọn hành vi** đo bằng một benchmark EditMode riêng: chạy cây BT và bảng chuyển tiếp FSM hàng trăm nghìn lần với hành động giả (không làm gì), trên các trạng thái Blackboard dựng sẵn, số lần đổi hành vi mỗi phút theo từng phe (chỉ báo giật trạng thái). Ghi vào `Application.persistentDataPath` hoặc thư mục trong project (ngoài `Assets/`).
- **Kiểm tra tốc độ**: chạy một nhóm nhỏ trận ở 1× với cùng seed và so phân bố kết quả với 3×; nếu lệch đáng kể thì hạ tốc độ.

### 6. Số liệu và biểu đồ

- Một script Python (đã có Python trên máy, dùng cho xuất .docx) đọc CSV và in bảng tổng hợp + vẽ biểu đồ PNG: tỉ lệ thắng theo cặp đấu, phân bố thời gian bắt, thời gian quyết định mỗi tick, số lần đổi hành vi.
- Số liệu tĩnh trong cùng bảng: số trạng thái/chuyển tiếp FSM, số node BT, số dòng code từng bộ não (trước và sau mở rộng), `git diff --stat` của từng commit mở rộng.

### 6a. Chuỗi tăng dần

- Công cụ tĩnh, chỉ chạy trong EditMode (test hoặc mục menu Editor), **không đưa vào game**.
- Với N = 2…10 hành vi có thứ tự ưu tiên (mô hình giống cây hiện tại: mỗi hành vi có một điều kiện kích hoạt, hành vi ưu tiên cao hơn được chạy trước; hành vi cuối là mặc định), dựng tự động:
  - một cây BT tương ứng (Selector của các nhánh điều kiện → hành động), rồi đếm số node;
  - một FSM tương ứng dùng **đúng lớp FSM và bảng chuyển tiếp của §2**, sinh đủ chuyển tiếp để hành vi giống hệt bản BT ở mọi tổ hợp điều kiện, rồi đếm số chuyển tiếp.
- **Kiểm tra tương đương** *(Claude quyết định)*: với mỗi N, duyệt mọi tổ hợp đúng/sai của N−1 điều kiện, đánh giá cả hai bản từ mọi trạng thái xuất phát của FSM, khẳng định chúng chọn cùng hành vi. Nếu thiếu bước này, người chấm có thể cho rằng FSM được sinh ra cố tình to hơn cần thiết.
- Báo cáo ghi rõ giả thiết của mô hình: nếu dùng chuyển tiếp "từ bất kỳ trạng thái" (any-state) thì số chuyển tiếp FSM giảm. Vì vậy công cụ xuất **cả hai biến thể** (FSM thuần và FSM có any-state), cạnh số node BT. Như vậy kết luận trung thực, không thổi phồng.
- Đầu ra: CSV (N, số node BT, số chuyển tiếp FSM thuần, số chuyển tiếp FSM any-state), được script Python ở §6 vẽ thành biểu đồ đường.

### 7. Thứ tự làm (đề xuất)

1. Tách lõi dùng chung + chuyển bản BT sang lõi mới; test cũ vẫn xanh (không đổi hành vi).
2. Bản FSM hai phe + chọn bộ não lúc spawn + Decoy.
3. Kịch bản C1–C4, R1–R4 cho cả hai bản → **commit mốc "cơ sở"**.
4. Mô phỏng + CSV + script số liệu; chạy lượt cơ sở.
5. Thí nghiệm mở rộng: bốn commit riêng (BT Chaser, FSM Chaser, BT Runner, FSM Runner) + kịch bản C5, R5.
6. Chạy lại mô phỏng sau mở rộng; xuất bảng/biểu đồ.
7. Chuỗi tăng dần (§6a) và benchmark chi phí chọn hành vi. Hai việc này chỉ cần lớp FSM ở bước 2, nên có thể làm sớm hơn nếu tiện.

### 8. Kiểm chứng

- Biên dịch bằng `dotnet build` (Editor thường đang mở, batch mode bị chặn).
- Chạy EditMode/PlayMode test và mô phỏng qua Unity Editor đang mở (MCP).
- Chơi thử chế độ thường một trận để chắc người chơi không thấy thay đổi.

## Để lại cho người làm

- Tên lớp, tên file cụ thể, cách tổ chức thư mục (gợi ý: `Assets/Scripts/AI/Fsm/`).
- Thông số của hai hành vi mới (thời gian tìm kiếm, số điểm quét, bán kính tìm chỗ nấp, cách xác định "bị che") — đưa vào `AIConfig`.
- Camera khi không có người chơi (nhìn toàn map hay bám một NPC).
- Định dạng chính xác của CSV và biểu đồ.
