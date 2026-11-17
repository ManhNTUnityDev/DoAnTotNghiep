# BÁO CÁO TIẾN ĐỘ ĐỒ ÁN TỐT NGHIỆP — NHÓM 24

## Đề tài: "Nghiên cứu Behavior Tree thích ứng cho điều khiển hành vi NPC trong game."

**Tổng quan về đề tài:** Nghiên cứu kiến trúc Behavior Tree (Cây hành vi) nhằm mô phỏng tư duy ra quyết định linh hoạt cho các nhân vật máy (NPC). Dựa trên cơ sở lý thuyết tìm hiểu được, nhóm em ứng dụng, xây dựng và tích hợp hệ thống AI này vào lập trình và phát triển game có yếu tố đuổi bắt.

**Mục tiêu của đề tài:** Xây dựng được một game đuổi bắt có các NPC (đóng vai trò người đi tìm hoặc người đi trốn) có khả năng tự phản ứng với môi trường, nhận diện người chơi, tìm kiếm đường đi và đưa ra các quyết định (đuổi/chạy trốn) một cách tự nhiên.

**Hai cơ chế thích ứng của đề tài:** Hệ thống di chuyển lai (Hybrid Movement) với hai chế độ cùng chạy trên một thành phần duy nhất — **Mode Direction** (nhận vector hướng từ người chơi) và **Mode Agent** (nhận đường đi NavMesh cho NPC) — cho phép một Prefab nhân vật duy nhất thích ứng với cả hai loại bộ não điều khiển (PlayerBrain / AIBrain) ngay lúc runtime.

---

## Tổng quan sản phẩm game

### 1. Tổng quan

- Là game 3D đuổi bắt phát triển dựa trên Unity Engine (URP), nhắm nền tảng mobile với điều khiển cảm ứng.
- Player được phân chia ngẫu nhiên 1 trong 2 role: đi đuổi (phe Gà / Chaser) hoặc chạy trốn (phe Người / Runner).
- Góc nhìn thứ 3 dạng orbit cho cả hai role: joystick ảo điều khiển di chuyển, vùng cảm ứng riêng để vuốt xoay camera quanh nhân vật.
- Mỗi team có 4 thành viên (gồm cả người chơi và NPC) — tổng 1 player + 7 NPC mỗi trận.

### 2. Coreloop

- Vào trận: hệ thống spawn đủ 4 Chaser và 4 Runner tại các điểm xuất phát của hai phe, người chơi được gán ngẫu nhiên vào một phe.
- Giai đoạn chạy trước (head-start) 10 giây:
  - Phe Gà (Chaser): bị khóa toàn bộ hành động tại điểm xuất phát — không di chuyển, không dùng kỹ năng; vẫn được xoay camera để quan sát hướng chạy của đối phương.
  - Phe Người (Runner): được tự do chạy tản ra khỏi khu vực xuất phát để tìm chỗ trốn.
  - 3 giây cuối hiển thị bộ đếm lớn giữa màn hình `3 → 2 → 1 → READY!` cho cả hai phe.
- Khi hiện `READY!`, toàn bộ Chaser được mở khóa và đồng hồ trận đấu chính 90 giây bắt đầu chạy.
- Trận đấu kết thúc khi xảy ra một trong hai trường hợp:
  - Chaser bắt giam hết toàn bộ Runner trước khi hết giờ ⇒ phe Gà thắng.
  - Hết 90 giây mà vẫn còn Runner tự do ⇒ phe Người thắng.
- Runner trúng trứng bị nhốt trong một lồng giam dựng ngay tại vị trí bị bắt và ở trong đó đến hết trận — không có cơ chế giải cứu, nên số Runner tự do chỉ giảm dần.
- Hiện bảng kết quả (Thắng/Thua theo phe của người chơi) với lựa chọn chơi lại hoặc thoát ra.

### 3. Flow

- Vào trận → Spawn 4v4 + chia role ngẫu nhiên → Giai đoạn chạy trước 10s →
  - Chaser: bị khóa 10s → Đuổi và bắn bắt giam → Bắt hết Runner (Win) / Hết giờ còn Runner tự do (Lose).
  - Runner: chạy trốn 10s → tiếp tục bỏ chạy, dùng kỹ năng thoát thân → Bị bắt hết (Lose) / Còn người chưa bị bắt (Win).
- Kết thúc → Bảng kết quả → Chơi lại hoặc thoát.

### 4. Điều chỉnh so với đề cương ban đầu

| Hạng mục | Đề cương ban đầu | Sản phẩm hiện tại | Lý do điều chỉnh |
| --- | --- | --- | --- |
| Quy mô đội | 1 người đuổi + 4–5 người trốn | 4 Chaser vs 4 Runner (1 player + 7 NPC) | Cân bằng hơn và tạo đủ ngữ cảnh cho NPC xử lý nhiều mục tiêu cùng lúc |
| Góc nhìn | Góc nhìn thứ nhất | Góc nhìn thứ 3 dạng orbit cho cả hai phe | Phù hợp điều khiển cảm ứng mobile; người chơi quan sát được vật cản quanh thân nhân vật |
| Thời gian trốn trước | 30 giây, Gà bị nhốt trong lồng | Head-start 10 giây, Chaser bị khóa hành động tại điểm xuất phát (cấu hình được trên Inspector) | Vòng lặp demo ngắn, thuận tiện cho kiểm thử AI nhiều lượt; cơ chế khóa đạt cùng mục đích mà không cần dựng lồng khởi đầu |
| Thời lượng trận | 5 phút | 90 giây (cấu hình được trên Inspector) | Giữ nhịp trận nhanh trên bản đồ hiện tại và giảm thời gian mỗi lượt playtest AI |
| Vật phẩm hỗ trợ | Nhặt item trên map | Chưa có — hiện mỗi phe dùng 2 kỹ năng sẵn có | Đưa vào kế hoạch Tuần 9 |
| Giải cứu đồng đội | Runner tự do giải thoát đồng đội đang bị nhốt | Bỏ — bị bắt là bị nhốt đến hết trận | Luồng giải cứu dễ làm trận đấu giằng co không kết thúc quanh lồng giam; bỏ đi thì luật thắng/thua rõ ràng (số Runner tự do chỉ giảm) — quyết định ở Tuần 8 |
| Lobby / sảnh chờ | Sảnh chờ riêng + 15 giây xem role | Chưa có scene riêng; hiện gộp vào giai đoạn head-start trong trận | Đưa vào kế hoạch Tuần 9 |

---

## Kế hoạch triển khai dự án

| Giai đoạn | Nội dung | Trạng thái |
| --- | --- | --- |
| **Giai đoạn 1 (Tuần 1 – Tuần 4)** | BT tĩnh chạy ổn cho cả NPC đuổi và NPC chạy (lõi Behavior Tree, Blackboard, cảm biến) | Hoàn thành |
| **Giai đoạn 2 (Tuần 5 – Tuần 7)** | Cả hai cơ chế thích ứng (Mode Direction / Mode Agent) hoạt động trong game; cây hành vi chi tiết hai phe, vòng đời trận đấu, bộ kỹ năng và giao diện | Hoàn thành |
| **Giai đoạn 3 (Tuần 8 – Tuần 10)** | Hoàn thiện gameplay còn thiếu (lồng giam, chức năng NPC, lobby, vật phẩm), kiểm thử, build và báo cáo tổng | Đang triển khai |

*Ghi chú: Giai đoạn 2 ban đầu dự kiến Tuần 5–8. Do khối lượng hoàn thành sớm hơn kế hoạch, nhóm dịch mốc Giai đoạn 2 về Tuần 5–7 và dành trọn Tuần 8–10 cho giai đoạn hoàn thiện.*

---

## BÁO CÁO TIẾN ĐỘ TUẦN 1

**Nội dung tuần 1:** *Nghiên cứu cơ sở lý thuyết Behavior Tree, Đặc tả bài toán Game Đuổi Bắt và Thiết kế kiến trúc tổng thể*

### 1. Mục tiêu trong tuần

- Nghiên cứu tổng quan về các kiến trúc điều khiển AI trong trò chơi điện tử, trọng tâm là **Behavior Tree (Cây hành vi)** và **Blackboard**.
- Phân tích yêu cầu chức năng, phi chức năng và luật chơi của bài toán game đuổi bắt bất đối xứng (Asymmetric Chase Game).
- Thiết kế kiến trúc phần mềm tách biệt **Brain – Body** kết hợp **Dependency Injection (VContainer)** và khởi tạo môi trường dự án Unity.

### 2. Nội dung công việc đã thực hiện

**2.1. Nghiên cứu lý thuyết AI cho NPC**

- Phân tích hạn chế của Máy trạng thái hữu hạn (*Finite State Machine – FSM*): khi số lượng trạng thái N tăng lên (Tuần tra, Đuổi bắt, Bắn, Canh lồng, Chạy trốn, Giải cứu, Phân thân...), số lượng chuyển tiếp trạng thái tăng theo bậc O(N²), gây khó khăn khi bảo trì và mở rộng.
- Nghiên cứu cấu trúc **Behavior Tree (BT)**: tổ chức quyết định theo cấu trúc cây phân cấp định hướng độ ưu tiên từ trái sang phải, bao gồm:
  - *Composite Nodes*: Selector (hoạt động như cổng OR, chọn nhánh con đầu tiên thành công) và Sequence (hoạt động như cổng AND, thực thi tuần tự các nhánh con).
  - *Leaf Nodes*: ConditionNode (kiểm tra điều kiện môi trường) và ActionNode (thực thi hành động).
  - *Trạng thái trả về*: Success, Failure, Running.
- Nghiên cứu kiến trúc bộ nhớ chia sẻ **Blackboard** giúp tách biệt dữ liệu nhận thức (Perception Data) khỏi logic duyệt cây.

**2.2. Đặc tả bài toán Game Đuổi Bắt (ChaseGame)**

- **Phe Đi Săn (Team.Chaser)**: tốc độ cơ bản chậm hơn (5 m/s), sở hữu kỹ năng tấn công tầm xa (Gun bắn trứng) và SpeedBoost, nhiệm vụ bắt giam toàn bộ Runner trong thời gian quy định.
- **Phe Chạy Trốn (Team.Runner)**: tốc độ cơ bản cao hơn (6 m/s), sở hữu kỹ năng thoát thân (SpeedBoost, Decoy tạo phân thân), nhiệm vụ sinh tồn đến hết giờ (90 giây) và giải cứu đồng đội bị nhốt *(cơ chế giải cứu về sau được loại bỏ — xem Tuần 8)*.

**2.3. Thiết kế kiến trúc phần mềm & Khởi tạo dự án**

- Thiết kế mô hình **Brain – Body Separation**:
  - **Body (Character)**: thực thể vật lý trong không gian 3D, cung cấp giao diện nhận lệnh (Move, MoveTo, Stop, UseAbility).
  - **Brain (CharacterBrain)**: bộ não ra quyết định. Một Prefab nhân vật duy nhất có thể gắn PlayerBrain (người chơi điều khiển) hoặc AIBrain (NPC tự động) lúc runtime.
- Cấu hình dự án Unity (URP), phân tách Assembly Definition (`ChaseGame.asmdef`, `ChaseGame.Tests.EditMode.asmdef`, `ChaseGame.Tests.PlayMode.asmdef`) để phục vụ kiểm thử tự động.
- Xây dựng bản đồ 3D có chướng ngại vật và thực hiện Bake dữ liệu tìm đường NavMesh (`Assets/NavMeshData/Game_Map_NavMesh.asset`).

### 3. Kết quả đạt được & Minh chứng

- Hoàn thành đề cương và bản nháp Chương 1 (Tổng quan đề tài & Cơ sở lý thuyết), Chương 2 (Phân tích và thiết kế hệ thống).
- Dự án Unity đã tích hợp VContainer, Unity Input System, DOTween và bản đồ 3D đã Bake NavMesh.

### 4. Khó khăn gặp phải & Giải pháp

- **Vấn đề:** Một số plugin bên thứ ba (như UltimateJoystick phục vụ điều khiển di động) không có file `.asmdef`, dẫn đến việc code trong assembly ChaseGame không thể tham chiếu trực tiếp mà không phá vỡ cấu trúc module.
- **Giải pháp:** Áp dụng mẫu thiết kế **Dependency Inversion & Adapter Pattern**: định nghĩa giao diện `IJoystickInput` bên trong ChaseGame (`Assets/Scripts/Input/IJoystickInput.cs`), và đặt lớp chuyển đổi `UltimateJoystickInput` (`Assets/GameScripts/Input/UltimateJoystickInput.cs`) ở Assembly-CSharp rồi tiêm (inject) qua VContainer.

### 5. Kế hoạch tuần 2

- Xây dựng các lớp thực thể nhân vật (Character, CharacterStats, Team, CaptureState).
- Nghiên cứu và cài đặt cơ chế di chuyển lai (**Hybrid Movement**) kết hợp CharacterController và NavMeshAgent.
- Xây dựng PlayerBrain, CharacterAnimationController và viết bộ Unit Test kiểm thử toán học di chuyển.

---

## BÁO CÁO TIẾN ĐỘ TUẦN 2

**Nội dung tuần 2:** *Xây dựng nền tảng Nhân vật, Điều khiển cơ bản và Cơ chế Di chuyển*

### 1. Mục tiêu trong tuần

- Hiện thực hóa lớp **Character** và cơ chế khóa hành động khi nhân vật rơi vào trạng thái bị bắt.
- Giải quyết bài toán đồng bộ vật lý giữa nhân vật do người chơi điều khiển (dùng vector hướng) và NPC AI (dùng tìm đường NavMesh) trên cùng một thành phần di chuyển.

### 2. Nội dung công việc đã thực hiện

**2.1. Cài đặt thực thể Character và Dữ liệu cấu hình**

- Xây dựng enum `Team` (Chaser, Runner) và `CaptureState` (Free, Jailed).
- Xây dựng ScriptableObject `CharacterStats` và khởi tạo 2 file cấu hình: `ChaserStats.asset` và `RunnerStats.asset`.
- Thiết lập cơ chế **Command Gating** trong Character: khi `CaptureState` chuyển sang Jailed, setter tự động gọi `movement.Stop()` để triệt tiêu quán tính trượt, đồng thời chặn toàn bộ lệnh `Move()`, `MoveTo()`, `UseAbility()`.

**2.2. Phát triển cơ chế Hybrid Movement (hai cơ chế thích ứng của đề tài)**

- Thay vì để NavMeshAgent trực tiếp dịch chuyển Transform của NPC (gây xung đột va chạm với CharacterController), hệ thống tắt quyền cập nhật vị trí/góc xoay tự động của Agent.
- Thiết kế 2 chế độ hoạt động (**Mode Direction** cho Player và **Mode Agent** cho NPC AI):
  - Ở chế độ Mode Agent, NavMeshAgent chỉ tính toán đường đi và vận tốc mong muốn.
  - Vector hướng được làm mượt bằng nội suy với hệ số *steeringSmoothRate* nhằm loại bỏ hiện tượng giật cục do thuật toán tránh vật cản cục bộ (RVO) của NavMesh.

**2.3. Cài đặt PlayerBrain và CharacterAnimationController**

- Chuyển đổi trục điều khiển 2D sang vector di chuyển 3D tương đối theo hướng nhìn của Camera (`ToCameraRelativeMove`).
- Kết nối vận tốc phẳng của CharacterController vào tham số Speed của Animator với damping 0.1s.

### 3. Kết quả đạt được & Minh chứng

- Hoàn thành module `ChaseGame.Characters` và `ChaseGame.Brains`.
- Kiểm thử thành công tìm đường NavMesh bằng công cụ `NavMeshMoveProbe`.

### 4. Khó khăn gặp phải & Giải pháp

- **Vấn đề:** Khi NPC tiến gần đến điểm đích, do vận tốc nhân vật luôn chạy ở tốc độ tối đa (BaseSpeed × speedMultiplier) nên nhân vật bị bước lố qua điểm đích (overshoot) và quay đầu dao động liên tục quanh điểm đích.
- **Giải pháp:** Xây dựng hàm kiểm tra điều kiện tới đích — khi đường đi đã tính xong và khoảng cách còn lại nhỏ hơn ngưỡng dừng, vector hướng được gán về `Vector3.zero` ngay lập tức, giúp NPC dừng lại dứt khoát.

### 5. Kế hoạch tuần 3

- Thiết kế và cài đặt lõi **Behavior Tree Engine** (Node, NodeStatus, Selector, Sequence, ConditionNode, ActionNode).
- Xây dựng cấu trúc dữ liệu Blackboard, AIConfig và lớp cơ sở AIBrain có cơ chế nhịp suy nghĩ định kỳ (TickRateHz) cùng hành vi đi tuần (Wander).

---

## BÁO CÁO TIẾN ĐỘ TUẦN 3

**Nội dung tuần 3:** *Phát triển Core Behavior Tree Engine, Bộ nhớ Blackboard và Khung điều khiển AIBrain*

### 1. Mục tiêu trong tuần

- Xây dựng bộ thư viện **Behavior Tree** thuần C# (không phụ thuộc vào MonoBehaviour), hỗ trợ khai báo cây hành vi trực quan.
- Thiết kế Blackboard và AIConfig để quản lý dữ liệu nhận thức và tham số điều chỉnh AI.
- Xây dựng lớp cơ sở AIBrain điều tiết tần suất ra quyết định và cài đặt thuật toán đi tuần trên NavMesh.

### 2. Nội dung công việc đã thực hiện

**2.1. Xây dựng Core Behavior Tree Engine**

- Định nghĩa enum `NodeStatus` (Success, Failure, Running) và lớp trừu tượng `Node` với phương thức `NodeStatus Tick(Blackboard bb)`.
- **Composite Node – Selector**: duyệt tuần tự danh sách các node con; nếu một node con trả về Success hoặc Running, lập tức ngắt mạch (short-circuit) và trả về trạng thái đó. Chỉ trả về Failure khi tất cả các con đều thất bại.
- **Composite Node – Sequence**: duyệt tuần tự các node con; nếu một node con trả về Failure hoặc Running, lập tức ngắt mạch và trả về trạng thái đó. Chỉ trả về Success khi toàn bộ chuỗi con đều thành công.
- **Leaf Nodes – ConditionNode & ActionNode**: giúp định nghĩa cây hành vi ngắn gọn mà không cần tạo hàng chục class con rời rạc.

**2.2. Thiết kế Blackboard**

- Blackboard đóng vai trò túi dữ liệu trung tâm của mỗi NPC.
- Tạo ScriptableObject `AIConfig.asset` với các thông số chuẩn cho NPC (tầm nhìn 12 m, tầm bắn 8 m, bán kính đe dọa 6 m, bán kính tuần tra 6 m, nhịp suy nghĩ 8 Hz).

**2.3. Phát triển lớp cơ sở AIBrain và Thuật toán Wander**

- **Cơ chế Throttled Decision Tick & Desynchronization**:
  - Thay vì chạy cảm biến và duyệt cây ở mỗi khung hình, AIBrain chỉ thực thi mỗi Δt = 1 / TickRateHz = 0.125s (8 lần/giây).
  - Khởi tạo bộ đếm thời gian ban đầu bằng giá trị ngẫu nhiên để các NPC không cùng suy nghĩ trong một khung hình.
- **Thuật toán đi tuần có định hướng**: khi NPC chưa có Destination hoặc đã đến gần đích, thuật toán tìm một điểm ngẫu nhiên mới trên NavMesh.

### 3. Kết quả đạt được

- Hoàn thiện gói `BehaviourTree` và lớp khung `AIBrain`.

### 4. Khó khăn gặp phải & Giải pháp

- **Vấn đề:** NPC thường chọn điểm ngay sát chân mình hoặc quay ngược đầu liên tục, tạo cảm giác di chuyển thiếu tự nhiên.
- **Giải pháp:** Ưu tiên lấy mẫu theo hướng nhìn hiện tại trước khi fallback sang 360° khi gặp góc tường.

### 5. Kế hoạch tuần 4

- Xây dựng hệ thống quản lý đội hình.
- Phát triển hệ thống cảm biến thị giác Sensor với kiểm tra khuất tầm nhìn và thuật toán chống nhảy mục tiêu.

---

## BÁO CÁO TIẾN ĐỘ TUẦN 4

**Nội dung tuần 4:** *Phát triển Hệ thống Nhận thức & Cảm biến AI*

### 1. Mục tiêu trong tuần

- Xây dựng hệ thống quản lý danh sách nhân vật trong trận đấu để cung cấp dữ liệu đầu vào cho AI.
- Phát triển component cảm biến giúp NPC nhận diện đối phương trong bán kính tầm nhìn và kiểm tra vật cản che khuất bằng Raycast.
- Giải quyết triệt để hiện tượng AI đổi mục tiêu liên tục mỗi tick khi có 2 đối thủ đứng cách đều nhau.

### 2. Nội dung công việc đã thực hiện

**2.1. Xây dựng hệ thống quản lý đội hình**

- Lưu trữ danh sách Chasers và Runners đang có mặt trên bản đồ.

**2.2. Phát triển hệ thống cảm biến thị giác**

- **Trích xuất ứng viên**:
  - Nếu bản thân là `Team.Chaser`: chỉ lấy các Runner đang ở trạng thái Free.
  - Nếu bản thân là `Team.Runner`: lấy danh sách toàn bộ Chasers làm mối đe dọa.
- **Kiểm tra đường nhìn thẳng**: bắn Raycast từ mắt NPC tới mục tiêu. Nếu tia chạm phải Collider của một nhân vật khác, cảm biến coi như **không bị chắn**; khi chạm vào địa hình/tường thực sự mới trả về false.

**2.3. Thuật toán khóa mục tiêu có độ bám dính**

- Khi có 2 mục tiêu A (đang được chọn từ tick trước) và B (mới xuất hiện) ở khoảng cách gần bằng nhau, nếu chỉ so sánh khoảng cách Euclid thuần túy, sai số nhỏ mỗi bước chân sẽ khiến Chaser quay đầu qua lại giữa A và B.
- **Hướng cải tiến — Nới rộng bán kính giữ dấu**: tăng bán kính giữ dấu mục tiêu hiện tại lớn hơn bán kính phát hiện mục tiêu mới.

### 3. Kết quả đạt được

- Phân loại đội, lọc Runner tự do/bị bắt và điều kiện toàn bộ Runner bị bắt.
- Kiểm chứng chọn mục tiêu gần nhất trong bán kính, loại bỏ mục tiêu ngoài tầm nhìn, bỏ qua mục tiêu gần nhưng bị tường chắn.

### 4. Khó khăn gặp phải & Giải pháp

- **Vấn đề:** Khi thực hiện Raycast từ tâm nhân vật, tia Raycast đôi khi va chạm ngay vào chính collider của bản thân NPC hoặc bị đồng đội đi trước mặt che mất tầm nhìn.
- **Giải pháp:** Dịch điểm đầu tia Raycast tiến về phía mục tiêu một khoảng và kiểm tra va chạm để bỏ qua va chạm với các nhân vật khác, đảm bảo chỉ có tường/chướng ngại vật bản đồ mới chặn tầm nhìn.

### 5. Kế hoạch tuần 5

- Thiết kế và cài đặt cây hành vi chi tiết cho **NPC Đi săn (ChaserAIBrain)** và **NPC Chạy trốn (RunnerAIBrain)**.
- Xây dựng bộ kỹ năng và cơ chế bắt giam Runner.

---

## BÁO CÁO TIẾN ĐỘ TUẦN 5

**Nội dung tuần 5:** *Cây hành vi chi tiết cho hai phe, Bộ kỹ năng và Cơ chế bắt giam*

### 1. Mục tiêu trong tuần

- Cài đặt cây hành vi hoàn chỉnh cho **NPC Đi săn**: ưu tiên đuổi mục tiêu nhìn thấy, canh lồng giam khi không thấy ai, tuần tra khi rảnh.
- Cài đặt cây hành vi hoàn chỉnh cho **NPC Chạy trốn**: chạy trốn khi bị áp sát, dùng kỹ năng thoát thân, giải cứu đồng đội bị nhốt khi an toàn.
- Xây dựng hệ thống kỹ năng dùng chung (kích hoạt, hồi chiêu) và cơ chế bắt giam Runner.

### 2. Nội dung công việc đã thực hiện

**2.1. Cây hành vi NPC Đi săn — `ChaserAIBrain`**

- Cấu trúc Selector theo thứ tự ưu tiên:
  1. **Bắn** — Sequence: *mục tiêu đã vào tầm bắn (ShootRange 8 m) và súng đã hồi chiêu* → `AimAndShoot` (vừa bắn vừa tiếp tục áp sát, đồng thời ghi nhớ vị trí mục tiêu).
  2. **Đuổi** — Sequence: *cảm biến thấy Runner tự do trong tầm nhìn (VisionRadius 12 m)* → `ChaseTarget`.
  3. **Canh lồng** — Sequence: *có `GuardPost` được phân công* → `GuardCage` (giữ chốt, ngăn đồng đội đối phương tới giải cứu).
  4. **Tuần tra** — mặc định → `Wander` (đi tuần có định hướng trên NavMesh).
- Ghi nhớ vị trí nhìn thấy cuối: khi mất dấu Runner, Chaser lưu `Blackboard.Destination` = vị trí thấy cuối và tiếp tục tiến tới đó thay vì quay đầu về điểm tuần tra cũ.

**2.2. Cây hành vi NPC Chạy trốn — `RunnerAIBrain`**

- Cấu trúc Selector theo thứ tự ưu tiên:
  1. *Có Chaser trong bán kính đe dọa* (ThreatRadius 6 m) → chạy trốn theo hướng ngược lại và tự kích hoạt kỹ năng thoát thân khi sẵn sàng.
  2. *Có đồng đội đang bị nhốt và đường đến lồng an toàn* → `GoRescue` đi giải cứu.
  3. Mặc định → `Wander`.
- Hàm `FindSafeRescueTarget` kiểm tra bán kính an toàn quanh lồng (RescueThreatRadius 5 m) để Runner không lao vào lồng đang có Chaser canh.

**2.3. Hệ thống kỹ năng và bắt giam**

- Giao diện `IAbility` + `CharacterAbilities` (quản lý danh sách kỹ năng theo index) + `CooldownTimer` (đếm hồi chiêu thuần, kiểm thử được).
- `Gun`: kỹ năng tấn công tầm xa của Chaser, xác định mục tiêu trong tầm và gọi dịch vụ bắt giam.
- `SpeedBoost`: nhân hệ số tốc độ trong thời gian ngắn; `Decoy`: tạo phân thân nghi binh (phiên bản đầu).
- `ICaptureService` + `CaptureServiceStub` + `CageStub`: chuyển `CaptureState` của Runner sang `Jailed` (Body tự động `Stop()` qua setter), ghi nhận lồng giam tại vị trí bị bắt và phát sự kiện `OnRunnerCaught`.
- `ChaserCoordinator`: phân công Chaser canh các lồng giam đang hoạt động theo khoảng cách, có hệ số thiên vị giữ nguyên người đang canh (0.64) để tránh đổi nhiệm vụ liên tục.

### 3. Kết quả đạt được & Minh chứng

- NPC của cả hai phe đã tự hành hoàn chỉnh trong trận: Chaser biết đuổi – bắn – canh lồng – tuần tra; Runner biết trốn – dùng kỹ năng – đi cứu đồng đội.
- Bộ kiểm thử tự động: `ChaserAIBrainTests`, `RunnerAIBrainTests`, `ChaserCoordinatorTests`, `CaptureServiceStubTests` (EditMode) và `ChaserAIPlayTests`, `ChaserCapturePlayTests` (PlayMode).
- Commit minh chứng: `72ae520`, `43cf370`, `b745780`.

### 4. Khó khăn gặp phải & Giải pháp

- **Vấn đề:** Khi Runner lướt qua góc tường, Chaser mất dấu ngay lập tức và quay ngoắt 180° về điểm tuần tra cũ, trông như "bỏ cuộc" giữa pha đuổi.
- **Giải pháp:** Lưu vị trí nhìn thấy cuối vào Blackboard và cho nhánh đuổi tiếp tục dùng điểm đó làm đích cho đến khi tới nơi hoặc thấy lại mục tiêu.
- **Vấn đề:** NPC Runner đi cứu đồng đội một cách "mù quáng", lao thẳng vào lồng đang có Chaser canh và bị bắt thêm.
- **Giải pháp:** Thêm điều kiện `IsGuardedByThreat` — chỉ nhận nhiệm vụ giải cứu khi không có Chaser nào trong bán kính an toàn quanh lồng.

*Ghi chú cập nhật ở Tuần 8: nhánh giải cứu của Runner, nhánh canh lồng của Chaser và `ChaserCoordinator` đã bị loại bỏ khi nhóm chốt bỏ cơ chế giải cứu. Phần mô tả trên giữ lại để phản ánh đúng tiến độ tại thời điểm Tuần 5.*

### 5. Kế hoạch tuần 6

- Xây dựng hệ thống spawn trận đấu đầy đủ 4v4 (1 player + 7 NPC) và nhà máy khởi tạo nhân vật.
- Cài đặt luật thắng/thua, đồng hồ trận đấu và giao diện HUD + bảng kết thúc.

---

## BÁO CÁO TIẾN ĐỘ TUẦN 6

**Nội dung tuần 6:** *Vòng đời trận đấu 4v4, Luật thắng/thua và Giao diện trận đấu*

### 1. Mục tiêu trong tuần

- Khởi tạo được một trận đấu đầy đủ 8 nhân vật (1 người chơi + 7 NPC) với phe của người chơi được chia ngẫu nhiên.
- Cài đặt luật thắng/thua thuần (kiểm thử được bằng unit test) cùng đồng hồ trận đấu.
- Xây dựng HUD trong trận và bảng kết thúc có chơi lại/thoát.

### 2. Nội dung công việc đã thực hiện

**2.1. Hệ thống spawn và khởi tạo nhân vật**

- `SpawnManager` (triển khai `IStartable` của VContainer): spawn lần lượt hai phe tại các điểm xuất phát đặt trong scene — `ChaserSpawn_0..3` và `RunnerSpawn_0..3` (4 vị trí mỗi phe).
- `CharacterFactory`: khởi tạo nhân vật từ Prefab và tiêm phụ thuộc; đúng một nhân vật được gắn `PlayerBrain`, các nhân vật còn lại gắn `AIBrain` — hiện thực hóa trọn vẹn mô hình Brain–Body đã thiết kế ở Tuần 1.
- `SpawnSettings`: gom các tham chiếu scene (Prefab hai phe, điểm spawn, `AIConfig`, camera) để lớp thuần `SpawnManager` có thể inject.
- `TeamRoster`: danh sách hai phe và các truy vấn Runner tự do / bị nhốt (`GetJailedRunners`, `AllRunnersJailed`) làm dữ liệu đầu vào cho AI và cho luật thắng/thua.

**2.2. Luật thắng/thua và đồng hồ**

- `MatchEvaluator` — lớp thuần, không phụ thuộc MonoBehaviour hay `UnityEngine.Time`, viết theo TDD:
  - `Evaluate(freeRunnerCount, totalRunnerCount, timeRemaining)` trả về `ChasersWin` khi không còn Runner tự do, `RunnersWin` khi hết giờ mà vẫn còn Runner tự do, ngược lại là `Ongoing`.
  - Ưu tiên: bắt hết Runner thì Chaser thắng ngay cả khi đồng hồ cũng vừa hết.
  - `ForPlayer(state, playerTeam)` quy đổi kết quả trận thành Thắng/Thua theo phe của người chơi.
- `MatchClock`: lớp thuần định dạng thời gian còn lại thành `mm:ss`.
- `MatchController`: điều phối vòng đời trận — chạy đồng hồ 90 giây (cấu hình trên Inspector), hỏi `MatchEvaluator` mỗi khung hình, đóng băng trận khi kết thúc, xử lý chơi lại và thoát.

**2.3. Giao diện trận đấu**

- `MatchHudView`: đồng hồ đếm ngược, số Runner còn tự do / tổng số, vai trò người chơi.
- `MatchEndView`: kết quả Thắng/Thua kèm nút Chơi lại và Thoát; toàn bộ được nối vào scene `Game.unity`.

**2.4. Cân bằng số liệu**

- Chốt tốc độ cơ bản: Runner 6 m/s nhanh hơn Chaser 5 m/s, bù lại Chaser có kỹ năng tấn công tầm xa.

### 3. Kết quả đạt được & Minh chứng

- Chơi được trọn một trận từ lúc vào scene đến khi ra bảng kết quả.
- Bộ kiểm thử: `MatchEvaluatorTests`, `MatchClockTests`, `SpawnManagerTests`, `CharacterFactoryTests`, `TeamRosterTests`.
- Commit minh chứng: `e0131af`, `9a786cf`, `cdb8bf4`, `36984ad`, `54bf250`, `cda88d8`, `68a30f8`.

### 4. Khó khăn gặp phải & Giải pháp

- **Vấn đề:** `MatchController` hỏi kết quả trận ngay khung hình đầu, trước khi `SpawnManager` kịp tạo xong danh sách nhân vật. Roster rỗng bị hiểu là "đã bắt hết Runner" nên Chaser thắng ngay giây đầu tiên.
- **Giải pháp:** Đưa thêm tham số `totalRunnerCount` vào `Evaluate`; khi tổng số Runner bằng 0 thì trận luôn ở trạng thái `Ongoing`, kèm unit test khóa chặt hành vi này (commit `db9510c`).
- **Vấn đề:** Thoát trận trong lúc đang tạm dừng khiến `Time.timeScale` giữ nguyên giá trị 0, scene nạp sau đó bị đứng hoàn toàn.
- **Giải pháp:** Đặt lại `Time.timeScale = 1` trong hàm `Quit()` trước khi chuyển scene (commit `a426f22`).

### 5. Kế hoạch tuần 7

- Xây dựng hệ thống animation cho NPC và tối ưu toàn bộ chuyển động, góc xoay, chống dao động trạng thái.
- Phát triển camera orbit điều khiển cảm ứng, HUD kỹ năng theo vai trò và hoàn thiện ba kỹ năng Decoy / EggProjectile / SpeedBoost.
- Bổ sung giai đoạn chạy trước (head-start) đầu trận.

---

## BÁO CÁO TIẾN ĐỘ TUẦN 7

**Nội dung tuần 7:** *Animation NPC, Tối ưu hành vi AI, Camera & Điều khiển, Bộ kỹ năng hoàn chỉnh và Lobby head-start*

### 1. Mục tiêu trong tuần

- Hết hiện tượng NPC giật cục, rung lắc tại chỗ và đổi trạng thái liên tục ở rìa tầm nhìn.
- Gắn animation cho NPC theo chuyển động và theo kỹ năng đang dùng.
- Cho người chơi xoay camera bằng cảm ứng và di chuyển theo hướng nhìn camera.
- Hoàn thiện ba kỹ năng (phân thân, bắn trứng, tăng tốc) kèm giao diện nút kỹ năng theo vai trò.
- Bổ sung giai đoạn chạy trước đầu trận kèm bộ đếm `3 – 2 – 1 – READY!`.

### 2. Nội dung công việc đã thực hiện

**2.1. Hệ thống animation NPC**

- `CharacterAnimationController` tách biệt logic và animation: đọc vận tốc phẳng của `CharacterController` đổ vào tham số `Speed` của BlendTree locomotion (damping 0.1s), cùng các tham số `IsJailed`, `OnAbility`, `AbilityID`.
- `CharacterAbilities` / `ICharacterAbilities` phát sự kiện `OnAbilityUsed` để Animator kích hoạt đúng animation kỹ năng.
- Cấu hình lại `NPC.controller` (BlendTree locomotion, state Jailed, state kỹ năng Decoy) và xử lý các transition `AnyState` để tránh nhảy animation liên tục (commit `20e4f3d`).

**2.2. Làm mượt chuyển động và chống dao động trạng thái (state thrashing)**

- `CharacterMovement`: nội suy hướng bằng `Vector3.Slerp` với `steeringSmoothRate = 16`; xoay thân bằng `Quaternion.RotateTowards` với `turnSpeed = 540°/s`; bỏ qua lệnh `SetDestination` nếu đích mới lệch chưa tới 0.15 m so với đích cũ.
- `Sensor`: giữ mục tiêu hiện tại bằng hệ số bám dính (`StickinessSqrBias = 0.64`, `RetainRadiusMultiplier = 1.15`) nên Chaser không đổi qua lại giữa hai Runner đứng gần nhau; Raycast kiểm tra tầm nhìn bỏ qua collider nhân vật, chỉ địa hình/tường mới chắn.
- `RunnerAIBrain`: độ trễ chạy trốn (hysteresis) — bắt đầu trốn khi Chaser vào 6 m nhưng chỉ ngừng trốn khi đã thoát ra ngoài 8.1 m (1.35 × ThreatRadius).
- `RunnerAIBrain.PickBestFleePoint`: quét 7 góc thoát hiểm (0°, ±40°, ±80°, ±120°) trên NavMesh để Runner tự rẽ sang hai bên khi phía sau là tường, không còn lao đầu vào góc chết (commit `5f5f1c6`).

**2.3. Camera orbit và điều khiển theo hướng camera**

- `CameraLookTouchPad`: vùng UI cảm ứng riêng để vuốt xoay camera, khóa theo `activePointerId` nên ngón trái giữ joystick và ngón phải xoay camera hoạt động độc lập; chuẩn hóa độ nhạy theo mật độ điểm ảnh (`referenceDpi = 160`).
- `SimpleFollowCamera`: quay quanh nhân vật với giới hạn góc ngẩng/cúi từ −10° đến 65°, dùng `Physics.SphereCast` tự kéo camera lại gần khi vướng tường.
- `PlayerBrain.ToCameraRelativeMove`: chiếu hướng nhìn camera xuống mặt phẳng XZ, đảm bảo đẩy joystick lên là nhân vật chạy về phía trước màn hình (commit `17dfdbe`).

**2.4. HUD kỹ năng và bộ ba kỹ năng hoàn chỉnh**

- `AbilityButtonView`: tự ẩn/hiện theo vai trò (`ChaserOnly`, `RunnerOnly`, `AnyRole`), hỗ trợ cả cảm ứng và phím tắt, có **Aim Assist** tự hướng góc bắn của Chaser về mục tiêu gần nhất trong hình nón phía trước; `CooldownTimer.NormalizedRemaining` điều khiển vòng hồi chiêu và số giây đếm ngược; thêm nút tạm dừng và `PausePanel` (Tiếp tục / Chơi lại / Thoát) (commit `ddfcc1c`).
- **Decoy — phân thân AI độc lập**: nhân bản nhân vật tại chỗ, gỡ `PlayerBrain`, vô hiệu toàn bộ kỹ năng, `Physics.IgnoreCollision` với bản gốc, gắn `DecoyLure` vòng đời 3 giây; `RunnerAIBrain.ConfigureAsClone` cho phân thân chạy lệch hướng ±55° so với Runner thật, không dùng kỹ năng, không đi cứu đồng đội, không tính vào điều kiện thắng/thua; trúng đạn thì biến mất mà không sinh lồng giam (commit `58d3f94`).
- **EggProjectile — đạn trứng bay**: tốc độ 22 m/s, tầm bắn 8 m, dùng `Physics.SphereCastAll` theo từng đoạn di chuyển mỗi khung hình, bỏ qua collider người bắn và đồng đội (commit `fea98fc`).
- **SpeedBoost cho Chaser**: hệ số 1.6, kéo dài 3 giây, hồi chiêu 6 giây, gắn vào Prefab ở vị trí kỹ năng thứ hai; AI Chaser tự kích hoạt khi đang rượt đuổi và kỹ năng sẵn sàng; thêm nút `Btn_ChaserSpeedBoost` trên HUD (commit `75f5073`).

**2.5. Giai đoạn lobby head-start đầu trận**

- `MatchPhase` (`HeadStart`, `Playing`, `Ended`) và các tham số cấu hình: `headStartDurationSeconds = 10`, `finalCountdownThresholdSeconds = 3`, `readyDisplayDurationSeconds = 0.85`.
- `Character.IsActionLocked` / `CanAct` tách riêng khỏi `CaptureState.Jailed`: khi bị khóa thì tự `Stop()`, chặn `Move`, `MoveTo`, `UseAbility` và `IsAbilityReady`; `AIBrain` bỏ qua cả `sensor.Sense` và `root.Tick` khi `!CanAct` nên AI Chaser đứng yên hoàn toàn.
- `MatchController.SetHeadStartChaserLock` khóa toàn bộ Chaser đầu giai đoạn và mở khóa đồng loạt khi hết 10 giây; đồng hồ trận chính 90 giây chỉ bắt đầu sau đó.
- `MatchHudView`: banner nhiệm vụ theo vai trò ở trên giữa màn hình và bộ đếm lớn giữa màn hình `3` (vàng) → `2` (cam) → `1` (đỏ) → `READY!` (xanh) với hiệu ứng phóng to DOTween, tắt `raycastTarget` để không cản thao tác cảm ứng.
- Cải tiến trải nghiệm: Chaser bị khóa vẫn vuốt được vùng camera để quan sát hướng chạy của Runner (commit `3cd0851`).

### 3. Kết quả đạt được & Minh chứng

- Kết quả chạy kiểm thử gần nhất được ghi nhận trong tài liệu dự án: **119/119 EditMode test pass**, cùng 4 bộ PlayMode test (`HybridMovementPlayTests`, `ChaserAIPlayTests`, `ChaserCapturePlayTests`, `RunnerFleePlayTests`).
- Tài liệu thiết kế & triển khai chi tiết 7 hạng mục tính năng: `docs/feature_design_documentation.md`.
- Trận đấu đã chơi được trọn vòng: chia role → chạy trước 10s → đếm ngược → đuổi bắt 90s → bảng kết quả.

### 4. Khó khăn gặp phải & Giải pháp

- **Vấn đề:** Phân thân Decoy sinh ra đúng vị trí Runner gốc nên hai collider chồng nhau, đẩy nhau bật ra hoặc kẹt tại chỗ ngay khi xuất hiện.
- **Giải pháp:** Gọi `Physics.IgnoreCollision` giữa toàn bộ collider của bản gốc và phân thân, đồng thời cho phân thân nhận ngay một điểm đến lệch góc ±55° để hai bên tách hướng tức thì.
- **Vấn đề:** Đạn trứng bay 22 m/s đi qua Runner giữa hai khung hình mà không ghi nhận trúng (tunneling).
- **Giải pháp:** Thay kiểm tra va chạm tại điểm bằng `Physics.SphereCastAll` quét suốt đoạn di chuyển của mỗi khung hình.
- **Vấn đề:** Ban đầu khóa Chaser trong giai đoạn chạy trước bằng cách đặt `CaptureState = Jailed`, dẫn đến HUD hiện nhầm thông báo "Bạn đã bị bắt" và khóa luôn cả camera của người chơi Chaser.
- **Giải pháp:** Tách thành thuộc tính độc lập `IsActionLocked` / `CanAct`, chỉ chặn di chuyển và kỹ năng, không ảnh hưởng trạng thái bị bắt và không chặn xoay camera.

### 5. Kế hoạch tuần 8

- Thay hệ thống bắt giam tạm (`CaptureServiceStub`, `CageStub`) bằng lồng giam thật dựng tại chỗ; chốt lại việc có giữ cơ chế giải cứu hay không.
- Hoàn thiện các chức năng còn thiếu của NPC: tìm kiếm theo khu vực khi mất dấu, phân vùng tuần tra, trốn sau chướng ngại vật.

---

## BÁO CÁO TIẾN ĐỘ TUẦN 8 *(đang thực hiện)*

**Nội dung tuần 8:** *Lồng giam tại chỗ, loại bỏ cơ chế giải cứu và hoàn thiện chức năng NPC*

### 1. Mục tiêu trong tuần

- Thay hệ thống bắt giam tạm (`CaptureServiceStub` + `CageStub` — chỉ là dữ liệu C#, không có lồng nào trong không gian 3D) bằng lồng giam thật dựng ngay tại vị trí Runner bị bắt.
- Chốt quyết định **bỏ hoàn toàn cơ chế giải cứu**: Runner bị bắt bị nhốt đến hết trận; dọn sạch phần code chỉ tồn tại để phục vụ giải cứu.
- Hoàn thiện các chức năng NPC còn thiếu: Chaser tìm kiếm theo khu vực khi mất dấu, phân vùng tuần tra; Runner chọn điểm trốn sau chướng ngại vật.

### 2. Nội dung công việc đã thực hiện

**2.1. Quyết định bỏ cơ chế giải cứu và dọn code kéo theo**

- Lý do: luồng giải cứu làm trận đấu có thể giằng co không kết thúc quanh lồng giam, trong khi phần cài đặt dở dang (NPC chỉ đi tới lồng, không có bước nào trả Runner về trạng thái tự do) khiến luật thắng/thua mơ hồ. Bỏ giải cứu cho luật trận rõ ràng: số Runner tự do chỉ giảm, không tăng.
- `RunnerAIBrain`: xoá nhánh giải cứu cùng `HasJailedTeammate`, `GoRescue`, `FindSafeRescueTarget`, `IsGuardedByThreat`. Cây còn hai nhánh: *chạy trốn khi bị áp sát* → *tuần tra*.
- `ChaserAIBrain`: xoá nhánh canh lồng — không còn ai đến giải cứu thì đứng canh là mất thời gian. Cây còn ba nhánh: *bắn* → *đuổi* → *tuần tra*.
- Xoá hẳn `ChaserCoordinator` (bộ phân công canh lồng) và `Blackboard.GuardPost`, bỏ đăng ký trong `GameLifetimeScope`, bỏ tham số tương ứng trong `SpawnManager`, bỏ `rescueThreatRadius` khỏi `AIConfig`.
- `MatchEvaluator` không phải sửa: nó vốn đã coi bị bắt là mất vĩnh viễn, nay mới đúng luật chơi.

**2.2. Hệ thống lồng giam tại chỗ**

- `Cage` (thay `CageStub`): component lồng giam thật, giữ `Occupant` và vị trí; có `BoxCollider` 1.6 × 2 × 1.6 m chặn nhân vật khác chạy xuyên, cùng `NavMeshObstacle` ở chế độ **avoidance (không carve)** để NPC tự lách qua mà không phải bake lại NavMesh giữa trận.
- Khi gắn người bị nhốt, lồng gọi `Physics.IgnoreCollision` giữa collider lồng và toàn bộ collider của nhân vật — lặp lại mẫu đã dùng cho phân thân Decoy ở Tuần 7 — nên lồng không đẩy hay làm kẹt chính người bị nhốt.
- `JailService` (thay `CaptureServiceStub`, vẫn hiện thực `ICaptureService` nên không phá DI và các điểm tiêm `Gun.Configure` / `EggProjectile.Configure`): đặt `CaptureState = Jailed` → snap vị trí xuống NavMesh gần nhất → sinh lồng → thêm vào `ActiveCages` → phát `OnRunnerCaught`. Không có đường ngược lại (không giải phóng).
- Dự án chưa có model lồng giam, nên `Cage.BuildPrimitive()` dựng lồng bằng primitive: 4 cột góc, 2 thanh ngang mỗi mặt, một nóc, material kim loại tối — cùng cách Tuần 7 dùng Sphere làm quả trứng tạm. Khi có model thật, chỉ cần gán vào `SpawnSettings.cagePrefab`, không phải sửa dòng code nào.
- Animation: bật lại tham số `IsJailed` của Animator trong `CharacterAnimationController` (trước đây bị comment) kèm kiểm tra tham số có tồn tại trong controller hay không, tránh cảnh báo mỗi khung hình với nhân vật chưa khai báo tham số này.
- HUD: sửa thông điệp khi người chơi bị bắt thành *"Bạn đã bị bắt — chờ hết trận"* (bản cũ ghi "đang xem đồng đội" nhưng camera xem đồng đội chưa được triển khai, và giờ cũng không còn giải cứu để chờ).

**2.3. Kiểm thử**

- `JailServiceTests` (thay `CaptureServiceStubTests`) với 7 ca: bắt Runner tự do thì nhốt + sinh lồng + phát sự kiện; bỏ qua Chaser; bỏ qua Runner đã bị nhốt; bắt hai lần không sinh hai lồng; lồng sinh đúng vị trí; Runner bị nhốt không nhận lệnh (`CanAct == false`); lồng có collider chặn và `NavMeshObstacle` không carve.
- Xoá `ChaserCoordinatorTests` và các ca test giải cứu / canh lồng trong `RunnerAIBrainTests`, `ChaserAIBrainTests`.
- Kiểm tra biên dịch: cả assembly `ChaseGame` và hai assembly test đều biên dịch sạch (0 lỗi, 0 cảnh báo) khi dựng ngoài Unity bằng `dotnet` với bộ DLL của Unity 6000.0.72f1.

### 3. Phần còn lại của tuần 8

- Chaser: thuật toán tìm kiếm theo khu vực quanh vị trí thấy cuối (hiện vẫn quay về tuần tra ngẫu nhiên) và phân vùng tuần tra để 4 Chaser không dồn cục.
- Runner: chọn điểm trốn sau chướng ngại vật (cover point) thay cho việc chỉ chạy theo hướng ngược.
- Gắn `CharacterAnimationController` cho `ChaserCharacter.prefab` (hiện chỉ phe Runner có animation) và dọn trùng lặp hai prefab Runner.
- Playtest: xác nhận lồng không làm NPC kẹt; nếu còn vướng thì hạ xuống phương án lồng chỉ còn phần hiển thị, bỏ collider chặn.

### 4. Khó khăn gặp phải & Giải pháp

- **Vấn đề:** Dự án không có model lồng giam, cũng không thể dựng prefab trong Unity ở phiên làm việc này.
- **Giải pháp:** Dựng lồng bằng primitive trong code (`Cage.BuildPrimitive`) và vẫn hỗ trợ prefab qua `SpawnSettings.cagePrefab` — thay model sau không ảnh hưởng code.
- **Vấn đề:** Lồng sinh lúc runtime nên NPC đã có đường đi sẵn sẽ lao vào lồng; nếu cho lồng carve NavMesh thì dữ liệu bake sẵn bị thay đổi giữa trận.
- **Giải pháp:** Dùng `NavMeshObstacle` ở chế độ avoidance, giữ nguyên NavMesh đã bake; agent tự lách qua lồng.
- **Vấn đề:** Ý định ban đầu là tắt `NavMeshAgent` của người bị nhốt để agent không tiếp tục lái nhân vật.
- **Giải pháp:** Không cần — setter `CaptureState` đã gọi `movement.Stop()`, hàm này reset đường đi và đưa thành phần di chuyển về Mode Direction; tắt agent chỉ thêm rủi ro cho vòng đồng bộ `agent.nextPosition`.

### 5. Kế hoạch tuần 9

- Scene Lobby/sảnh chờ, chia role và màn thông báo vai trò.
- Hệ thống vật phẩm cho Runner (cả NPC dùng được), Menu chính và âm thanh.

---

## KẾ HOẠCH TUẦN 9

**Nội dung tuần 9:** *Hoàn thiện vòng lặp game đầy đủ — Lobby, Vật phẩm, Menu và Âm thanh*

### 1. Mục tiêu

- Dựng đủ vòng lặp một phiên chơi theo đặc tả: từ Menu chính → sảnh chờ → trận đấu → kết quả → quay lại sảnh chờ.
- Bổ sung hệ thống vật phẩm hỗ trợ cho phe Runner mà cả người chơi và NPC đều dùng được.
- Bổ sung âm thanh để trận đấu có phản hồi đầy đủ.

### 2. Nội dung dự kiến

**2.1. Scene Lobby / sảnh chờ**

- Hiển thị 8 slot thành viên (1 người chơi + 7 NPC) chia hai phe, nút Bắt đầu.
- Chia role ngẫu nhiên kèm màn hình thông báo vai trò trước khi vào trận (thay cho việc chỉ hiện vai trò trên HUD như hiện nay).
- Luồng scene: `Menu → Lobby → Game → Kết thúc → Lobby`, dùng lại scene `Loading` đã có.

**2.2. Hệ thống vật phẩm**

- 2–3 loại vật phẩm rải trên bản đồ cho Runner nhặt, ví dụ: làm mới hồi chiêu kỹ năng, ẩn mình ngắn (giảm bán kính bị phát hiện), bẫy làm chậm Chaser.
- `ItemSpawner` rải vật phẩm theo điểm định trước hoặc ngẫu nhiên trên NavMesh, có thời gian hồi sinh.
- Bổ sung nhánh BT `PickupItem` cho `RunnerAIBrain`: NPC đi nhặt vật phẩm khi an toàn (không có Chaser trong bán kính đe dọa) — giữ đúng thứ tự ưu tiên: chạy trốn > nhặt vật phẩm > tuần tra.
- Chaser cũng phản ứng với vật phẩm/bẫy (bị làm chậm, mất dấu khi Runner ẩn mình).

**2.3. Menu chính và âm thanh**

- Menu chính: Chơi, Hướng dẫn (luật chơi + điều khiển), Cài đặt (âm lượng, độ nhạy camera), Thoát.
- Âm thanh: nhạc nền menu/trận đấu, SFX bắn trứng, bị bắt và nhốt vào lồng, nhặt vật phẩm, đếm ngược `3‑2‑1‑READY`, nút UI.

### 3. Tiêu chí hoàn thành

- Chơi liên tiếp hai trận không cần khởi động lại ứng dụng, mọi chuyển scene sạch (không rò `Time.timeScale`, không rò đối tượng giữa các trận).
- NPC Runner nhặt được vật phẩm và sử dụng hiệu quả trong playtest.

### 4. Rủi ro & phương án

- **Rủi ro:** thêm vật phẩm làm lệch cân bằng, Runner gần như không thể bị bắt.
- **Phương án:** đặt số lượng và thời gian hồi sinh vật phẩm thấp ở bản đầu, chốt số liệu trong tuần cân bằng (Tuần 10).

---

## KẾ HOẠCH TUẦN 10

**Nội dung tuần 10:** *Cân bằng, Tối ưu, Build và Báo cáo tổng*

### 1. Mục tiêu

- Chốt số liệu cân bằng, đảm bảo hiệu năng mượt với 8 nhân vật cùng chạy AI và animation.
- Có bản build chạy được trên thiết bị, kèm video demo.
- Hoàn thành báo cáo tổng và slide bảo vệ.

### 2. Nội dung dự kiến

**2.1. Cân bằng và tối ưu**

- Chốt bộ số liệu: tốc độ hai phe, tầm nhìn, bán kính đe dọa, hồi chiêu các kỹ năng, thời lượng trận và thời gian head-start — dựa trên nhiều lượt playtest, ghi lại tỉ lệ thắng của hai phe.
- Tối ưu hiệu năng: kiểm tra chi phí NavMesh và animation của 8 nhân vật, điều chỉnh `TickRateHz` và cơ chế lệch pha tick, giảm số lần Raycast mỗi tick nếu cần, profile trên thiết bị thật.

**2.2. Kiểm thử và sửa lỗi**

- Chạy lại toàn bộ EditMode + PlayMode test; bổ sung test cho các hành vi mới của Tuần 8–9.
- Playtest có kịch bản (mỗi phe, mỗi tình huống: bị bắt, hết giờ, bắt hết) và sửa lỗi phát sinh.

**2.3. Build và tài liệu**

- Build Android (và PC nếu cần cho buổi bảo vệ), kiểm tra điều khiển cảm ứng và tỉ lệ UI trên nhiều kích thước màn hình.
- Báo cáo tổng: hoàn thiện Chương 3 (Triển khai hệ thống) và Chương 4 (Kiểm thử & Đánh giá), trong đó đánh giá hiệu quả của kiến trúc Behavior Tree và hai cơ chế thích ứng so với phương án FSM đã phân tích ở Tuần 1.
- Slide bảo vệ và video demo trận đấu.

### 3. Tiêu chí hoàn thành

- Bản build chạy ổn định trọn một phiên chơi trên thiết bị thật, không lỗi nghiêm trọng.
- Toàn bộ test xanh, báo cáo tổng và slide hoàn chỉnh.

---

## Tổng kết tiến độ

| Tuần | Nội dung | Trạng thái |
| --- | --- | --- |
| 1 | Cơ sở lý thuyết BT/Blackboard, đặc tả bài toán, kiến trúc Brain–Body + DI, bake NavMesh | Hoàn thành |
| 2 | Character, Command Gating, Hybrid Movement (Mode Direction / Mode Agent), PlayerBrain | Hoàn thành |
| 3 | Core Behavior Tree Engine, Blackboard, AIConfig, AIBrain + Wander | Hoàn thành |
| 4 | TeamRoster, Sensor (tầm nhìn + Raycast), khóa mục tiêu có độ bám dính | Hoàn thành |
| 5 | BT chi tiết hai phe, bộ kỹ năng, cơ chế bắt giam | Hoàn thành |
| 6 | Spawn 4v4, luật thắng/thua, đồng hồ 90s, HUD + bảng kết thúc | Hoàn thành |
| 7 | Animation NPC, làm mượt & chống state thrashing, camera orbit, HUD kỹ năng, Decoy/EggProjectile/SpeedBoost, head-start 10s | Hoàn thành |
| 8 | Lồng giam tại chỗ + bỏ cơ chế giải cứu (xong); hoàn thiện chức năng NPC: tìm kiếm theo khu vực, phân vùng tuần tra, cover point | Đang thực hiện |
| 9 | Lobby + chia role, hệ thống vật phẩm, Menu chính, âm thanh | Kế hoạch |
| 10 | Cân bằng, tối ưu, build, kiểm thử tổng, báo cáo & slide | Kế hoạch |
