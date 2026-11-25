# BT vs FSM — công việc còn lại sau rà soát

Ngày: 2026-11-25 · Nhánh: `Manh/Dev` · HEAD khi viết: `056d8e6`

Tài liệu này ghi lại **mọi việc còn nợ** sau khi một người rà soát độc lập đọc toàn bộ nhánh
`23ed86a..7ca2b13`. Viết để một phiên làm việc khác (hoặc một người khác) mở ra là làm được ngay,
không cần hỏi lại.

Liên quan:
- Thiết kế: `docs/superpowers/specs/2026-11-23-bt-vs-fsm-comparison-design.md`
- Kế hoạch đã thực hiện: `docs/superpowers/plans/2026-11-23-bt-vs-fsm-comparison.md`
- Số liệu: `docs/benchmark/` (`baseline/`, `extended/`, `extension-diffs.txt`)

---

## 1. Hiện trạng

**Đã xong và đã commit.** Cả 14 việc của kế hoạch gốc. Toàn bộ test xanh ở `056d8e6`:
**228 EditMode**, **33 PlayMode + 1 bỏ qua có chủ ý** (`BenchmarkBatchRun` tự bỏ qua khi không có
file plan).

Kiến trúc đang có:
- `AIBrain` là vật chủ dùng chung (Blackboard, Sensor, nhịp suy nghĩ, `DecisionStats`), lộ ra một
  hàm trừu tượng `Decide`.
- Mọi **hành động** của NPC nằm trong `AIActions` / `ChaserActions` / `RunnerActions`. Hai kiến
  trúc gọi đúng cùng code hành động, chỉ khác ở cách **chọn** hành động.
- `BehaviourTreeBrain` và `FsmBrain` là hai lớp con duy nhất. Mỗi phe có hai bộ não.
- `BrainFactory` + `MatchSetup` chọn loại bộ não cho từng phe lúc spawn. Chơi thường mặc định BT
  cho cả hai phe, có một người chơi.
- Mô phỏng: `SimulationRunner` + menu Editor `ChaseGame/Benchmark`, hoặc `BenchmarkBatchRun` khi
  Editor đóng.

**Cây làm việc sạch**, trừ hai file không phải của phần việc này:
- `ProjectSettings/TimeManager.asset` bị Editor của bạn ghi lại (vẫn 0.02 s). Chưa commit.
- `.vsconfig` chưa theo dõi. Chưa commit.

---

## 2. Kết quả đã đạt được (để không làm lại)

**Hai kiến trúc chơi tương đương.** Trước và sau mở rộng, mọi khoảng tin cậy 95% đều chồng lấn,
trên cả tỉ lệ thắng lẫn thời gian bắt. Đây là tiền đề để so sánh chi phí mở rộng cho công bằng.

**Chi phí thêm một hành vi** (đo từ diff từng commit, đã tách code dùng chung ra trước):

| Bộ não | Cấu trúc trước → sau | Dòng code thêm |
| --- | --- | --: |
| Chaser BT | 8 → 9 node | 76 |
| Chaser FSM | 6 → 11 chuyển tiếp | 76 |
| Runner BT | 5 → 9 node | 39 |
| Runner FSM | 2 → 6 chuyển tiếp | 68 |

**Chuỗi tăng dần N = 2…10:** BT theo 3N−1 (tuyến tính), FSM thuần theo N(N−1) (bậc hai), FSM
any-state theo N (tuyến tính). Có test kiểm tra cả ba mô hình chọn cùng hành vi trong mọi tổ hợp
điều kiện từ mọi trạng thái xuất phát.

---

## 3. Việc còn lại

Xếp theo thứ tự nên làm. Mỗi việc ghi: vấn đề, vì sao quan trọng, cách sửa, cách kiểm chứng.

### 3.1. NGHIÊM TRỌNG — Cột chi phí ns/tick không đo kiến trúc

**Vấn đề.** Trong `docs/benchmark/extended/analysis.md`, bảng chuỗi tăng dần có ba cột
`bt_ns_per_tick`, `fsm_pure_ns_per_tick`, `fsm_any_ns_per_tick`, đặt dưới tiêu đề *"Isolated
selection cost"*, với số 134,4 ns (BT) so với 575,9 ns (FSM thuần) ở N=10.

Con số đó **không phải tính chất của kiến trúc**, mà của *cách viết điều kiện*:

- `Assets/Scripts/AI/Analysis/PriorityModel.cs` cho BT điều kiện một phép thử cờ
  (`bb => Flags(bb)[index]`), dựa vào thứ tự nhánh của `Selector`.
- Nhưng cho FSM thì dùng `TriggerWins`, quét lại toàn bộ cờ ưu tiên cao hơn, tốn O(target) mỗi
  dòng, nên một tick tốn Θ(n²).

**Sai ở đâu.** Bình luận trong `PriorityModel.cs` (khoảng dòng 23–29) viết: *"An FSM transition has
no such ordering to lean on"*. Điều này **sai với `StateMachine` của dự án**:
`Assets/Scripts/AI/Fsm/StateMachine.cs:41-50` lấy **dòng khớp đầu tiên theo thứ tự bảng**, đúng
như `Selector`. Và chính `ChaserFsmBrain.BuildMachine` dựa vào tính chất đó: dòng
`(chase, CanStartSearch, search)` đứng trên `(chase, NoTarget, wander)` nên `Chase→Wander` chỉ nổ
khi không có gì để tìm kiếm.

**Vì sao quan trọng.** Đây là chỗ người chấm sẽ chất vấn, và họ đúng. Một FSM thuần viết kiểu dựa
thứ tự cũng chỉ tốn Θ(n), tức ngang BT. Nếu để nguyên, báo cáo đang nói BT nhanh hơn FSM, trong khi
thực tế đó là hệ quả của cách viết.

**Không bị ảnh hưởng:** số **đếm** chuyển tiếp (N(N−1) thuần, N any-state, 3N−1 node) vẫn đúng và
tối thiểu cho FSM thuần. Đó mới là thứ luận điểm O(N²) cần. Chỉ ba cột ns/tick và
`selection_cost.png` bị ảnh hưởng.

**Cách sửa — chọn một:**

**(a) Thêm mô hình FSM dựa-thứ-tự rồi báo cáo cả hai (nên chọn).** Biến điểm yếu thành phát hiện:
chi phí là lựa chọn cách viết, không phải do kiến trúc.

Thêm `PriorityModel.BuildOrderedFsm(int n, int initial)`: vẫn n(n−1) dòng, nhưng điều kiện dựa vào
thứ tự. Dạng điều kiện đúng, xuất phát từ trạng thái `s` (đã kiểm chứng bằng tay cho cả ba trường
hợp `e < s`, `e == s`, `e > s`, với `e = ExpectedAction(flags)`):

| Dòng | Điều kiện |
| --- | --- |
| `to < s` | `flags[to]` |
| `s < to < n−1` | `flags[to] && !flags[s]` |
| `to == n−1` | không cờ nào bật |

Lưu ý cái bẫy: điều kiện "chỉ `flags[to]`" cho mọi dòng là **sai**, vì `StateMachine.Tick` bỏ qua
dòng có `To == Current`. Với `n=3`, `Current=S0`, `flags=[true,true]` nó sẽ rời S0 sang S1 sai.

Việc kèm theo:
1. Thêm `BuildOrderedFsm` vào `PriorityModelTests`: test số chuyển tiếp bằng n(n−1), và thêm nó vào
   test tương đương `AllThreeModels_AgreeOnEveryFlagCombination_FromEveryStartState` (đổi tên thành
   `AllFourModels_...`).
2. Thêm cột `fsm_ordered_ns_per_tick` vào `BenchmarkAnalysisMenu.WriteScalingSeries`.
3. Chạy lại: `bash tools/benchmark/run-analysis.sh`.
4. Thêm đường thứ tư vào `selection_cost.png` trong `tools/benchmark/summarize.py`.
5. Viết lại bình luận `PriorityModel.cs` và mục chuỗi tăng dần trong `extended/analysis.md`.

**(b) Bỏ ba cột ns/tick và `selection_cost.png`,** chỉ giữ số đếm. Rẻ nhất, vẫn trung thực.

**(c) Giữ số nhưng viết lại cách diễn giải:** nói rõ *"hai mô hình được viết lần lượt theo kiểu tự
chứa và kiểu dựa thứ tự; một bảng FSM dựa thứ tự đạt chi phí ngang cây. Đây là số đo một phong cách
viết FSM phổ biến, không phải giới hạn dưới của FSM."*

**Bắt buộc với cả ba cách:** bình luận ở `PriorityModel.cs:23-29` phải bỏ câu khẳng định FSM "không
có thứ tự để dựa vào", vì `StateMachine` của dự án rõ ràng có.

**Kiểm chứng.** `bash tools/benchmark/run-unity-tests.sh EditMode` xanh, và với cách (a) thì
`Benchmark/results/scaling.csv` có cột mới với `fsm_ordered_ns_per_tick` xấp xỉ `bt_ns_per_tick`.

---

### 3.2. QUAN TRỌNG — Dữ liệu 120 trận không còn khớp với code

**Vấn đề.** Commit `056d8e6` sửa hai lỗi trong phần tìm chỗ nấp của Runner (xem §4). Hành vi nấp
giờ khác, nhưng `docs/benchmark/extended/20261125-1217-full-extended.csv` được chạy bằng code cũ.

**Vì sao quan trọng.** Trong sản phẩm đồ án, code và dữ liệu lệch nhau là chỗ dễ bị hỏi.

**Cách sửa.** Chạy lại lượt sau mở rộng, rồi tạo lại bản tổng hợp:

```powershell
bash tools/benchmark/run-benchmark.sh full-extended bt_bt,fsm_fsm,bt_fsm,fsm_bt 30 3
```

Khoảng 30 phút máy tự chạy, gần như không tốn gì ngoài thời gian chờ. Sau đó:

```powershell
python tools/benchmark/summarize.py Benchmark/results/<file-moi>.csv --scaling Benchmark/results/scaling.csv --static Benchmark/results/static-metrics.csv --out docs/benchmark/extended
```

Rồi cập nhật các con số trong `docs/benchmark/extended/analysis.md`.

**Lưu ý.** Lượt `baseline` **không cần** chạy lại: nó chạy trước khi có hành vi nấp.

---

### 3.3. QUAN TRỌNG — Thiếu 16 file `.meta`

**Vấn đề.** 14 file `.cs` và 2 thư mục mới được commit mà không có `.meta`. Các việc 1–5 làm khi
Editor mở nên có `.meta`; các việc 6–14 làm khi Editor đóng nên không có.

Thiếu: `Assets/Editor/BenchmarkMenu.cs`, `Assets/Editor/BenchmarkAnalysisMenu.cs`,
`Assets/Scripts/AI/DecisionStats.cs`, `Assets/Scripts/AI/Analysis/PriorityModel.cs`, cả bốn file
`Assets/Scripts/Simulation/*.cs`, sáu file test, cộng `.meta` của hai thư mục
`Assets/Scripts/AI/Analysis` và `Assets/Scripts/Simulation`.

**Vì sao quan trọng.** Không có gì vỡ, vì không file nào được scene hay prefab tham chiếu qua GUID.
Nhưng lần mở Editor tới nó sinh cả 16 file với GUID mới và để lại cây làm việc bẩn trong repo đồ án
của bạn. Dự án vẫn luôn theo dõi `.meta` một cách nhất quán.

**Cách sửa.** Mở Editor một lần rồi commit các `.meta` được sinh ra. Hoặc sinh bằng batch mode khi
Editor đóng:

```powershell
& "C:\Program Files\Unity\Hub\Editor\6000.0.72f1\Editor\Unity.exe" -batchmode -quit -projectPath "C:\Users\Admin\Desktop\Manh\UnityProjects\Game" -logFile -
```

Sau đó `git add` **chỉ** các file `.meta`, đừng commit kèm `TimeManager.asset` hay `.vsconfig`.

---

### 3.4. QUAN TRỌNG — Bốn chỗ sai trong số liệu của báo cáo

Bốn việc này chỉ sửa chữ, nhưng đụng vào **con số sẽ nằm trong Chương 4**. Đây là những chỗ người
chấm tìm ra.

**(1) Cột "time to catch all" không phải thời gian bắt hết.**
Trong `baseline/analysis.md` và `extended/analysis.md`, cột đó là trung bình `last_capture_s` trên
**mọi** trận có ít nhất một lần bắt, kể cả trận chỉ bắt được 2–3 trong 4 Runner. Đã kiểm: Chaser BT
ở baseline là 21,91 s (cả 60 trận) so với 22,61 s (56 trận bắt đủ). Lệch nhỏ (~0,7 s, nằm trong
khoảng ±3) nhưng **lệch sai hướng**: một trận chỉ bắt 2 Runner rồi hết giờ lại cho số *đẹp hơn* một
trận bắt đủ 4.

*Sửa:* đổi tên cột thành "thời gian bắt cuối cùng (s)", hoặc lọc chỉ các trận
`runners_caught == runners_total`. Nếu lọc thì phải tính lại mọi số trong cột đó.

**(2) Khoảng tin cậy Wald vô nghĩa ở mức trần.**
`1.96 × sqrt(p(1−p)/n)` cho ra "100,0 ± 0,0" với Runner BT (extended) và `fsm_fsm` (baseline) — một
khoảng tin cậy rộng bằng 0, không có nghĩa gì.

*Sửa:* dùng Wilson hoặc Clopper–Pearson. Runner BT 60/60 theo Wilson là khoảng 94–100%.

**(3) "Mọi khoảng tin cậy chồng lấn" đang gánh một kết luận nó không chứng minh được.**
`extended/analysis.md` viết *"Every interval still overlaps its counterpart. The equivalence held"*.
Khoảng tin cậy chồng lấn **không phải** một phép kiểm định sự khác biệt. Với Chaser thắng 95–100%,
thiết kế thí nghiệm gần như không có khả năng phát hiện khác biệt (vấn đề trần).

*Sửa:* dùng đúng cách diễn đạt của `baseline/analysis.md` (*"The data does not separate the two
architectures on play outcome"*), thêm một câu nói rõ về giới hạn do trần, và dựa vào thời gian bắt
cuối cùng — chỉ số này có biến động thật.

**(4) Lập luận "hành vi mới thật sự chạy" dựa vào một thay đổi không có ý nghĩa thống kê.**
`extended/analysis.md` dẫn tỉ lệ Chaser thắng 92,5% → 97,5%, tức 111/120 so với 117/120. Kiểm định
Fisher cho p ≈ 0,14, **không có ý nghĩa**. Hơn nữa phép so sánh này bị lẫn, vì cả hai phe đều được
thêm hành vi trong cùng một lượt chạy.

*Sửa:* bỏ nửa lập luận dựa vào tỉ lệ thắng. Giữ bằng chứng mạnh và trực tiếp: số đổi hành vi của
Runner 44,8 → 67,6 (+51%) và chi phí mỗi nhịp 0,0209 → 0,0238 ms.

---

### 3.5. QUAN TRỌNG — Hai cách đếm dòng code lẫn vào nhau

**Vấn đề.** Hai bảng trong cùng báo cáo dùng hai định nghĩa khác nhau:
- `extension-diffs.txt` và `analysis.md` dùng số dòng thêm thô của `git diff` (tính cả bình luận và
  dòng trống): 76 / 76 / 39 / 68.
- Cột `loc` trong `static-metrics.csv` loại bỏ dòng trống và dòng `//`.

Thêm nữa, `static-metrics.csv` xếp `Blackboard` vào `team=shared` (27 dòng), nhưng `Blackboard` giờ
chứa `ChaserHadTarget`, `SearchStartedAt`, `SearchPoints`, `SearchIndex`, `CoverPoint` — bộ nhớ chỉ
BT dùng, mà thiết kế §Mục tiêu 2 nói phải tính cho BT, và `extension-diffs.txt` **có** tính cho BT
(+8, +4). Nên bảng tĩnh đang tính thiếu chi phí của BT.

**Cách sửa.** Chọn một định nghĩa, áp cho cả hai bảng, và thêm chú thích về việc `Blackboard` bị
chia hai.

---

### 3.6. QUAN TRỌNG — `summarize.py` sẽ vỡ với matplotlib mới

**Vấn đề.** `tools/benchmark/summarize.py` dùng `ax.boxplot(data, labels=labels)`. Tham số `labels`
bị bỏ từ matplotlib 3.9, đã xóa và thay bằng `tick_labels`.

**Vì sao quan trọng.** Máy chưa cài matplotlib. Khi bạn cài, `pip` sẽ lấy bản mới nhất, và biểu đồ
`capture_time_boxplot.png` sẽ báo lỗi.

**Cách sửa.** Đổi `labels=labels` thành `tick_labels=labels`. Nếu cần chạy được trên cả bản cũ thì
bọc `try/except TypeError`.

---

### 3.7. QUAN TRỌNG — Hai test yếu hơn tên gọi

**(1) `R5_WallNearby_BreaksChaserSight`** (`Assets/Tests/PlayMode/RunnerScenarioTests.cs`) kiểm tra
hai biến bool **độc lập**: "có lúc nào ở trạng thái Hide" và "có lúc nào bị che tầm nhìn". Điều kiện
thứ hai đúng kể cả khi Runner chỉ chạy trốn thường, vì nó chạy xa Chaser ở x=0 và cuối cùng cũng
sang bên kia tường ở x=4. Nên test này **không chứng minh** điểm nấp làm mất tầm nhìn.

*Sửa:* đòi một khung hình mà **đồng thời** `ActiveBehaviour == Hide` và tầm nhìn bị che. Dữ liệu
chẩn đoán cho thấy cả hai kiến trúc đều thỏa ở khoảng t=1,2 s. Mạnh hơn nữa: lấy điểm nấp đã chọn
(`bb.CoverPoint` với BT) và khẳng định `!Sensor.IsSightClearPastBodies(chaserEye, coverPoint, mask)`
ngay lúc vào Hide — nhưng bản FSM giữ điểm đó trong trường riêng của `HideState`, nên cần lộ ra.

**(2) `SearchTimesOut_ReturnsToWander`** (`Assets/Tests/EditMode/ChaserAIBrainTests.cs`) không thể
fail vì lý do như tên gọi. Trong EditMode không có NavMesh nên `GenerateSearchPoints` trả về danh
sách rỗng. Bản FSM: `SearchState.IsOver` đã đúng nhờ `index >= points.Count` trước khi xét thời gian.
Bản BT: `SearchStep` trả `Failure` với danh sách rỗng nên `SearchBranch` rơi xuống Wander bất kể
`bb.Now`. Tức là **cả hai vẫn qua test này dù đặt `searchDurationSeconds` bằng vô cực**.

*Sửa:* đổi tên thành `SearchEnds_ReturnsToWander` cho đúng điều nó kiểm, rồi phủ nhánh hết-thời-gian
ở PlayMode nơi có NavMesh. Muốn vậy cần đặt được `searchDurationSeconds` nhỏ và `searchRadius` lớn,
mà các trường này là `private [SerializeField]` không có setter — nên phải thêm seam cho test hoặc
tạo asset `AIConfig` riêng cho kịch bản.

*Hiện trạng:* nhánh hết-thời-gian của việc tìm kiếm **chưa được test nào phủ**.

---

### 3.8. NHỎ — Danh sách để sau

Người rà soát xếp các mục này là nhỏ. Ghi lại để không mất:

| # | Việc |
| --- | --- |
| 1 | `Sensor.EyeHeight` không ai dùng. `TryFindCoverPoint` vẫn hardcode `const float eye = 0.6f`. Hoặc truyền giá trị thật qua Blackboard như `SightMask`, hoặc bỏ property. |
| 2 | Lệch một nhịp giữa hai kiến trúc khi quét xong: BT biết ngay trong tick và rơi xuống Wander, FSM mất thêm một tick 0,125 s. Không test nào bắt, không ai thấy, nhưng là một chỗ không tương đương thật. Nên viết một câu trong báo cáo. |
| 3 | `RunnerFsmBrain.HideState.HasCover` vừa là điều kiện chuyển tiếp vừa ghi vào `hasPoint`/`point`, và `Tick` dựa vào việc nó vừa được gọi. Hiện đúng trên mọi đường đi, nhưng đổi thứ tự 6 dòng bảng là vỡ âm thầm. Nên có một test "Tick đi tới điểm mà điều kiện vừa tìm được". |
| 4 | `RunnerActions.ThreatNear` ghi vào `bb.IsFleeing` và bị gọi 1–3 lần mỗi tick từ các điều kiện khác nhau. Hiện không sao vì cờ đơn điệu trong một tick, nhưng tính chất đó chưa được viết ra hay test. |
| 5 | `TakeCover` truy cập `bb.NearestThreat` không kiểm null. Chỉ đến được qua điều kiện đã kiểm, giống `EscapeAndFlee` và `AimAndShoot`. Nhưng đây là static public dùng chung nên nên ghi rõ tiền điều kiện. |
| 6 | `C4_TargetBehindWall_GoesToLastSeen` giờ mong manh. Nó đòi khoảng cách tới điểm thấy cuối không tăng (±0,3 m) cho tới khi dưới 1,5 m. Sau mở rộng, Chaser vào Search rồi đi tới điểm quét cách 4 m **quá** điểm thấy cuối. Tính ra đường đi lại gần nhất ≈1,26 m, chỉ cách ngưỡng 0,24 m. Đổi `searchRadius` một chút là đỏ. |
| 7 | `C5_LostBehindWall_SearchesThenWanders` yếu hơn kế hoạch yêu cầu. Kế hoạch đòi thêm "Chaser đến trong vòng 2 m của một điểm quét". Hiện chỉ kiểm thứ tự Search → Wander, nên không phân biệt được "đã quét khu vực" với "đặt cờ Search rồi đứng yên". |
| 8 | `AIActions.TryRandomPoint(Vector3, float, out Vector3)` (bản 2 tham số) không ai gọi. |
| 9 | `MatchSetup.Effective` cấp phát `MatchSetupOptions` mới mỗi lần đọc, và được đọc hai lần cho mỗi nhân vật spawn. Vô hại, nhưng nên cache một default static. |
| 10 | Nên ghi vào báo cáo, **có lợi cho bạn**: FSM Chaser viết tay chỉ cần **11** chuyển tiếp cho 4 trạng thái, trong khi mô hình dự đoán N(N−1) = 12, vì `Wander→Search` thật sự không thể xảy ra (không thể tìm kiếm khi chưa từng thấy mục tiêu). Nói ra thì N(N−1) thành chặn trên trung thực, và chặn trước người đọc đếm tay thấy 11. |
| 11 | `ProjectSettings/EditorSettings.asset` có `m_EnterPlayModeOptionsEnabled: 1` với `m_EnterPlayModeOptions: 0`, nên domain reload vẫn chạy và `MatchSetup.Current` được xóa giữa các lần Play. Nếu bạn tắt "Reload Domain", một lượt mô phỏng bị ngắt sẽ để `MatchSetup.Current` còn sót, và **trận thường kế tiếp sẽ spawn không có người chơi**. Thêm một `[RuntimeInitializeOnLoadMethod]` xóa `MatchSetup.Current` khi không có file plan là chặn được hẳn. |

---

## 4. Những gì đã sửa trong lượt rà soát (đã commit `056d8e6`)

Để không ai làm lại:

**Vòng tìm chỗ nấp.** `RunnerActions.cs` dùng
`Mathf.Lerp(radius * 0.5f, radius, (i % 2 == 0) ? 1f : 0.5f)`. Tham số `t` chỉ nhận `1f` hoặc `0.5f`
nên mọi điểm nấp ở `{radius, 0.75 × radius}`, tức 6 m hoặc 8 m với `coverSearchRadius = 8`, chứ
không phải 4–8 m như bình luận và kế hoạch ghi. Hậu quả: tường cách 2–5 m **không bao giờ** dùng
được làm chỗ nấp. Đã tách thành hàm hình học thuần `CoverCandidateOffsets(samples, radius)` và thêm
4 test EditMode (`CoverCandidateTests`).

**Tầm nhìn khi tìm chỗ nấp.** `Sensor.IsSightClear` dừng ở vật chạm đầu tiên và coi "có nhân vật
chắn" là *thông thoáng*. Với phần tìm chỗ nấp, tia bắn từ mắt Chaser tới điểm ứng viên thường chạm
`CharacterController` của chính Runner trước, nên điểm được che tốt nhất bị tính là "nhìn thấy
được" và bị loại. Đã thêm `Sensor.IsSightClearPastBodies` đi xuyên qua thân nhân vật, **chỉ dùng cho
việc tìm chỗ nấp**. Phần nhận thức giữ nguyên, nên dữ liệu `baseline` không bị ảnh hưởng.

**Script tái tạo dữ liệu.** Trước đây `docs/benchmark/README.md` trỏ tới các script nằm trong
`.superpowers/sdd/...`, mà thư mục đó bị gitignore, nên người clone về không có. Đã đưa vào
`tools/benchmark/`.

---

## 5. Môi trường — những cái bẫy đã gặp

Ghi lại để phiên sau không mất thời gian lần nữa.

**Editor của bạn thường mở, nên batch mode bị chặn.** Cách làm hiện tại: đồng bộ sang một bản sao ở
`%LOCALAPPDATA%\ChaseGameVerify` rồi chạy Unity batch mode ở đó. Script `tools/benchmark/run-unity-tests.sh`
lo việc này. Bản sao chiếm khoảng 2 GB; **xóa được an toàn**, lần chạy sau nó tự dựng lại (mất thêm
vài phút import).

**Verify copy phải khóa khung hình ở 120 fps.** Batch mode chạy khung hình không giới hạn, khiến mỗi
bước di chuyển nhỏ hơn `CharacterController.minMoveDistance` và nhân vật gần như không nhích. Không
khóa thì 3 test PlayMode fail ngay cả trên code gốc. Script tự ghi
`Assets/VerifyHarness/FrameCap.cs` vào bản sao.

**Sandbox che `%LOCALAPPDATA%\ChaseGameVerify` khỏi Python.** `os.listdir` của Python không thấy thư
mục đó, trong khi bash, robocopy và Unity truy cập bình thường. Đây là lý do lượt chạy đầu tiên âm
thầm không làm gì (file plan không được ghi, test tự báo "ignored"). Vì vậy
`run-benchmark.sh` ghi JSON bằng bash, không bằng Python. **Đừng đổi lại.**

**NUnit mặc định 180 giây mỗi test.** Một lượt 10 trận cần lâu hơn, nên lượt 1× đầu tiên bị cắt ở
trận thứ 5. `BenchmarkBatchRun` giờ có `[Timeout(3 * 60 * 60 * 1000)]`.

**`run-unity-tests.sh` coi lượt chạy là xanh khi `failed="0"` và `inconclusive="0"`,** không đòi
`result="Passed"`. Vì `BenchmarkBatchRun` tự bỏ qua khi không có file plan, làm kết quả tổng thành
`"Skipped:Ignored"` dù 33 test đều qua.

**Các fixture PlayMode có sẵn để lại lồng giam.** `JailService` sinh lồng, và fixture nào gây ra thì
cũng không dọn. Lồng có `BoxCollider` và `NavMeshObstacle` nên chặn tầm nhìn và chặn đường đi của
các test sau. `ScenarioArena` tự dọn lồng và trứng còn sót ở cả lúc khởi tạo lẫn lúc kết thúc. Các
fixture cũ vẫn để nguyên, không sửa.

**`dotnet build` không cần Unity.** `tools/benchmark/build.py` sinh bản csproj tạm có đủ file mới
(Editor không tự sinh lại csproj khi nó đang đóng) rồi build cả 4 assembly. Nhanh hơn hẳn việc mở
Unity để kiểm lỗi cú pháp.

**Một test chập chờn có sẵn.** `RunnerFleePlayTests.DecoyClone_MovesIndependently` fail khoảng 1
trên 3 lần, **kể cả trên code gốc** `23ed86a`. Không phải do phần việc này. Nếu gặp đỏ, chạy lại.

---

## 6. Thứ tự đề xuất cho phiên sau

1. **§3.1 cách (a)** — mô hình FSM dựa-thứ-tự. Đây là việc duy nhất ảnh hưởng tới luận điểm chính.
2. **§3.3** — sinh `.meta`, để repo sạch.
3. **§3.6** — sửa một từ trong `summarize.py`, rồi cài `matplotlib` và sinh biểu đồ. Mọi CSV đều còn
   nên không phải mô phỏng lại.
4. **§3.2** — chạy lại lượt sau mở rộng ở chế độ nền (30 phút máy), vì hành vi nấp đã đổi.
5. **§3.4 và §3.5** — sửa các con số và cách diễn giải trong `analysis.md`. Làm sau bước 4 để khỏi
   sửa hai lần.
6. **§3.7** — làm mạnh hai test.
7. **§3.8** — danh sách nhỏ, làm khi còn thời gian. Mục 10 và 11 đáng làm nhất.

Việc 1–3 làm được ngay, không phụ thuộc gì. Việc 5 phải chờ việc 4.
