# Thiết kế & Triển khai: Gắn lại cánh gà vào thân và bổ sung nhịp chạy (Chaser)

Ngày: 2026-11-18 (cập nhật triển khai: 2026-11-19)
Trạng thái: đã hoàn thành & kiểm chứng
Quy mô: thay đổi nhỏ – vừa (3 clip animation, 1 visual prefab, 1 editor builder)

## 1. Ý định và lý do

Khi gà di chuyển, cánh của nó bay lơ lửng phía trước thân thay vì dính trên
lưng. Người chơi vào vai Runner nhìn sang Chaser sẽ thấy một con gà có cánh rời
ra khỏi người — lỗi hiển thị rõ ràng, không thể bỏ qua.

Nguyên nhân **không phải** rotation của animation, mà là **position key của
xương vai bị ghi cứng sai vị trí** trong clip.

## 2. Hiện trạng đã xác minh

Đo bằng `execute_code` trên Editor đang mở (sample clip lên rig thật), không
phải suy đoán:

| | `shoulder.L` localPosition | `upper_arm.L` (không gian nhân vật) |
|---|---|---|
| Bind pose của rig | `(-0.0836, 0.0427, 0.0028)` | z = 0.056 |
| Key trong clip FPP | `(-0.1260, 0.2738, 0.6212)` | z = **0.716** |

- Khoảng cách `upper_arm.L` → `spine.002`: **0.752 m** khi chạy clip, trong khi
  bind pose chỉ **0.189 m**. Cánh bị đẩy ra trước ~0.66 m.
- Mọi xương khác trong clip (`head`, `spine.002`, `thigh.*`, `Tail.*`,
  `beak/mao/Eye`) **khớp bind pose chính xác**. Chỉ riêng `shoulder.L/R` lệch.
  Đây là offset cố định do người dựng anim kéo vai ra trước, không phải
  animation — cả 3 clip chỉ có 2 key giống hệt nhau.
- Lệch **y hệt nhau** trong cả `Armature_Chic_FPP_Idle`, `_Walk`, `_Attack`. Lỗi
  không riêng lúc đi; lúc di chuyển mới dễ thấy.
- Mesh thân `Chic` của `Main_Chic_Rig` skin nặng nhất vào `upper_arm.L/R`
  (weight 407/414) và `shoulder.L/R` (166/161) → mesh cánh đi theo xương vai.
- `Chic.controller` được **cả hai** `ChaserVisual_World` và `ChaserVisual_FPP`
  dùng chung, nên offset này cũng chính là thứ đang đẩy cánh vào khung hình
  camera FPP (mắt ở y = 0.55, z = 0).

Lưu ý: ghi chép của phiên trước nói "mesh cánh của `Main_Chic_Rig` skin vào
`spine`, không phải `upper_arm`, nên sửa clip FPP là an toàn cho world model" —
**điều đó sai**, đã kiểm chứng lại bằng bảng bone weight ở trên.

## 3. Mục tiêu

- World model: cánh dính thân đúng bind pose, ở mọi state (Idle / Walk /
  Attack).
- FPP view model: cánh vẫn nằm trong khung hình nhưng **kéo lại gần**, từ
  z ≈ 0.70 xuống **z = 0.35**.
- Động tác đập/vẫy cánh (rotation) giữ nguyên, không đụng tới.

## 4. Không làm (non-goals)

- Không dựng lại animation, không export lại FBX.
- Không đụng `Chic_FPP_Hit/Take`, `Armature_Chic_FPP_Fall/Fly` và
  `ChickenFeet.controller` — bộ này chỉ được tham chiếu trong `Scenes/Test.unity`,
  không nằm trong scene Game.
- Không sửa offset của xương `spine` (clip đặt thân thấp hơn bind ~2 cm) và
  `Armature/Ctrl` (z = 0.83, xương điều khiển không có mesh). Vô hại, để nguyên.
- Không thêm clip `Run`, không sửa các lỗi animation khác đã ghi trong
  `2026-11-17-chaser-chicken-model-animation-design.md`.

## 5. Anti-goal

Làm hỏng góc nhìn FPP: nếu sau khi sửa, người chơi điều khiển Chaser không còn
thấy cánh mình trong khung hình, thì việc này coi như thất bại kể cả khi world
model đã đúng.

## 6. Ràng buộc

- Hai visual dùng chung một `AnimatorController` và một bộ clip. Không được
  tách controller (đó là hướng A đã bị loại).
- Rig FPP (`Rig_Chick_Prop`) và rig world (`Main_Chic_Rig`) có **bind pose giống
  hệt nhau** ở toàn bộ chuỗi `spine → spine.002 → shoulder → upper_arm`, nên một
  giá trị transform là đủ để diễn tả offset.
- Mesh cánh FPP (`Chic.030`) **chỉ** skin vào `shoulder`/`upper_arm`, không dính
  chân (`Chic.031`/`Chic.032`) → dịch 2 xương vai không ảnh hưởng gì khác.

## 7. Các hướng đã cân nhắc

| Hướng | Nội dung | Đánh đổi |
|---|---|---|
| **A** | Nhân bản 3 clip thành bộ world (key vai về bind) + `Chic_World.controller` riêng cho `ChaserVisual_World`; sửa key vai trong 3 clip FPP | Thêm 4 asset; mỗi lần chỉnh anim gà về sau phải sửa ở hai nơi |
| **B — đã chọn** | Gỡ hẳn position curve của `shoulder.L/R` khỏi 3 clip; đưa offset viewmodel vào `ChaserVisual_FPP.prefab` dưới dạng override transform | Nếu sau này muốn anim dịch chuyển vai theo thời gian thì phải thêm curve lại |
| C | Script `LateUpdate` ép `localPosition` của vai về bind trên world visual | Tốn mỗi frame mỗi nhân vật; che giấu dữ liệu sai thay vì sửa nó. Loại |

**Lý do chọn B (người dùng chốt):** giữ một bộ clip duy nhất, và biến offset
viewmodel thành một giá trị transform trong Inspector — lần sau muốn chỉnh
"gần/xa" thì kéo thẳng trên prefab, không phải sửa curve rồi test lại.

## 8. Phần "làm thế nào" đã chốt

1. **Ba clip** `Assets/Models/Player/Anim_FPP/Armature_Chic_FPP_{Idle,Walk,Attack}.anim`:
   xoá position curve của đúng hai đường dẫn

   - `Armature/Bone/spine/spine.001/spine.002/shoulder.L`
   - `Armature/Bone/spine/spine.001/spine.002/shoulder.R`

   Xoá bằng `AnimationUtility.SetEditorCurve(clip, binding, null)` cho
   `m_LocalPosition.x/y/z` của mỗi đường dẫn, **không sửa tay file YAML** — dữ
   liệu nằm ở hai nơi (`m_PositionCurves` và `m_EditorCurves`) và API này dọn cả
   hai. Rotation / scale / các xương khác giữ nguyên.

2. **`Assets/Prefabs/Visuals/ChaserVisual_FPP.prefab`**: override localPosition
   của hai xương vai

   - `shoulder.L` = `(-0.1025, 0.1457, 0.2785)`
   - `shoulder.R` = `( 0.1025, 0.1457, 0.2785)`

   Con số lấy từ việc nội suy tuyến tính giữa bind pose và giá trị clip cũ, giải
   ra hệ số k = 0.4458 để `upper_arm` rơi đúng z = 0.350; đã verify bằng cách
   set thử rồi đo lại: `upper_arm.L` = `(-0.1388, 0.4011, 0.3500)`.

3. **`ChaserVisual_World.prefab`**: không đụng gì. Không còn curve ghi đè thì
   xương vai tự giữ bind pose của FBX.

## 9. Để lại cho người làm

- Quyết định làm bước 1 và 2 bằng `execute_code` trên Editor đang mở hay bằng
  một script `[MenuItem]` dùng một lần — miễn là `AssetDatabase.SaveAssets()`
  được gọi và file `.anim` / `.prefab` hiện lên trong `git status`.
- Cách verify: sample lại từng clip lên cả hai rig và đo
  - world: khoảng cách `upper_arm.L` → `spine.002` phải về khoảng **0.19 m**
    (hiện 0.75 m), cho cả 3 clip;
  - FPP: `upper_arm.L/R` phải ở **z ≈ 0.35**, y ≈ 0.40.
- Chụp lại scene view của world model lúc chạy `Walk` để đối chiếu bằng mắt.

---

## 10. Nhật ký triển khai & hai phát hiện bổ sung (2026-11-19)

Khi áp dụng bước 1–2 ở mục 8 và kiểm tra trực quan trong Unity Editor, phát hiện
hai vấn đề thực tế cần xử lý tiếp để hoàn thiện hiển thị của Chaser:

### 10.1 Phát hiện 1: Xương vai (`shoulder.L/R`) trong clip FPP còn bị xoay ngược lên đầu (~121°)

- **Hiện tượng**: Sau khi chỉ xoá `m_LocalPosition` của `shoulder.L/R`, cánh gà
  không còn bay lơ lửng phía trước nhưng lại bị **đẩy cao lên sát hai bên má /
  đầu gà** (`upper_arm.L` ở `y = 0.397 m, z = +0.056 m`), thay vì nằm ở giữa
  phần thân trắng như Bind Pose của FBX (`y = 0.239 m, z = -0.062 m`).
- **Nguyên nhân**:
  - Trên rig gốc `Main_Chic_Rig.fbx`, gốc của `shoulder.L/R` đặt ở sát gốc cổ
    (`y = 0.330 m, z = 0.056 m`), còn `upper_arm.L/R` (nơi giữ 525/539 đỉnh
    mesh cánh) nằm ở đầu mút của xương `shoulder` (dài `0.1139 m`).
  - Ở **Bind Pose của FBX**, `shoulder.L` có `localEuler = (334.2°, 188.2°, 212.8°)`
    (`quat = (0.9281, -0.2900, -0.1297, -0.1941)`), hướng **chúc xuống dưới và
    ra sau lưng**, đặt `upper_arm.L` đúng vào giữa thân trắng.
  - Ở **3 clip FPP (`Idle`, `Walk`, `Attack`)**, ngoài việc kéo `localPosition`
    ra trước, người dựng anim còn xoay cứng `shoulder.L/R.localRotation` ngược
    lên trên ~121° (`localEuler = (1.3°, 213.8°, 334.2°)`, `quat = (-0.2167,
    0.9319, 0.0540, -0.2859)`) để dựng cánh đứng lên trước camera FPP. Trong
    `Walk` và `Attack`, curve `m_LocalRotation` của `shoulder.L/R` hoàn toàn
    tĩnh (chỉ có 2 key đầu–cuối giống hệt nhau).
  - Thêm vào đó, góc cơ sở `t = 0` của `upper_arm.L/R` trong clip FPP
    (`euler = (28.5°, 7.1°, 292.2°)`) được đặt theo hệ trục khi vai đã lật
    ngược; nếu chỉ trả `shoulder.L/R` về Bind Pose mà giữ nguyên góc cơ sở
    `upper_arm.L/R` của FPP thì chóp cánh xanh sẽ cụp vào trong bụng trắng.
- **Cách giải quyết (theo đúng kiến trúc Hướng B)**:
  1. Xoá thêm curve `m_LocalRotation.x/y/z/w` của `shoulder.L` và `shoulder.R`
     khỏi cả 3 clip `Armature_Chic_FPP_{Idle,Walk,Attack}.anim` bằng
     `AnimationUtility.SetEditorCurve(clip, binding, null)`.
  2. Neo lại góc xoay cơ sở (`t = 0`) của `upper_arm.L/R` về đúng Bind Pose của
     FBX (`euler = (34.1°, 339.6°, 295.4°)` cho L và `(34.1°, 20.4°, 64.6°)` cho
     R), đồng thời bảo toàn nguyên vẹn biên độ xoay tương đối (`delta = inv(q0) * qi`)
     của nhịp thở trong clip `Idle`.
  3. Đưa góc xoay FPP của `shoulder.L/R` thành transform override trên
     `Assets/Prefabs/Visuals/ChaserVisual_FPP.prefab` (cùng với `localPosition`),
     và cập nhật luôn vào `Assets/Editor/ChaserVisualSetup.cs` để nếu chạy lại
     menu `ChaseGame/Rebuild Chaser Visuals` thì override này vẫn được tái tạo:
     - `shoulder.L`: `localPosition = (-0.1025, 0.1457, 0.2785)`,
       `localRotation = (-0.2167, 0.9319, 0.0540, -0.2859)` (`euler = (1.3°, 213.8°, 334.2°)`)
     - `shoulder.R`: `localPosition = (0.1025, 0.1457, 0.2785)`,
       `localRotation = (0.2167, 0.9319, 0.0540, 0.2859)` (`euler = (1.3°, 146.2°, 25.8°)`)

### 10.2 Phát hiện 2: Clip `Armature_Chic_FPP_Walk` gốc không có chuyển động thân, đầu và cánh

- **Hiện tượng**: Trong Play Mode, dù `Chic.controller` trên các Chaser NPC đã
  nhận `Speed ≈ 0.97 – 1.00` và phát `Armature_Chic_FPP_Walk`, người chơi nhìn
  từ xa gần như không thấy con gà có animation chạy/đi.
- **Nguyên nhân**: Clip `Armature_Chic_FPP_Walk.anim` (dài `0.5333s`, 16 frame ở
  30 fps, gồm 2 bước chân trái `0 → 0.2667s` và phải `0.2667 → 0.5333s`) vốn
  được dựng cho FPP viewmodel nên chỉ diễn hoạt đôi chân nhỏ sát đất (`IK.L/R`,
  `thigh.L/R`, `shin.L/R` với sải chân `~10.7 cm`) và lông đuôi (`Tail.*`).
  Toàn bộ các xương `spine`, `spine.001`, `spine.002`, `head`, `mao.001` và
  `upper_arm.L/R` chỉ có 2 keyframe đầu–cuối giống hệt nhau (biên độ dịch chuyển
  bằng `0.0000 m` / `0.0°`). Khi nhìn từ xa, phần bụng trắng tròn che khuất đôi
  chân ngắn khiến 90% hình khối con gà trơ cứng như tượng trượt trên mặt đất.
- **Cách giải quyết**: Bổ sung chuyển động nhịp chạy 9 mốc keyframe mượt
  (`t = 0, 2, 4, 6, 8, 10, 12, 14, 16` frame) vào `Armature_Chic_FPP_Walk.anim`,
  đồng bộ pha với 2 bước chân trong mỗi chu kỳ `0.5333s`:
  1. **Vẫy và xòe cánh (`upper_arm.L/R`)**: 2 nhịp vẫy mỗi chu kỳ khớp với đỉnh
     bật của từng bước chân (`t = 0.1333s` và `t = 0.4000s`) — nâng cánh quanh
     trục X từ `-12°` lên `+28°` (biên độ `40°`), mở cánh ra ngoài thân quanh
     trục Z từ `6°` lên `26°` (trái `+Z`, phải `-Z`), và xoay nhẹ tán lông `±14°`
     quanh trục Y.
  2. **Nhún thân & lắc lư ngang (`spine` & `spine.002`)**:
     - Nhún dọc (`spine.localPosition.z`, tương ứng trục +Y thế giới): hạ thấp
       `-0.8 cm` khi chân tiếp đất (`t = 0, 0.2667, 0.5333s`) và bật cao
       `+3.4 cm` (tổng biên độ `4.2 cm`) ở giữa bước (`t = 0.1333, 0.4000s`),
       kèm dao động tiến/lùi `±1.0 cm` và đổ ngực về trước `-5° ± 6°`
       (`spine.002` nhún ngực `5° ± 8°`).
     - Lắc lư ngang (chicken waddle, 1 chu kỳ trái–phải): dịch ngang
       `spine.localPosition.x` `±1.6 cm`, nghiêng thân (roll) `±7°` và xoay
       hông (yaw) `±7°`.
  3. **Gật đầu & rung mào (`head` & `mao.001`)**:
     - `head`: gật đầu 2 nhịp mỗi chu kỳ (`±14°` pitch, trễ pha `0.4 rad` so với
       nhịp bật thân) kèm giữ cân bằng hướng nhìn (`±6°` yaw, `±5°` roll).
     - `mao.001`: mào đỏ rung lắc theo quán tính trễ pha (`±15°` pitch, `±14°`
       roll).

### 10.3 Bảng số liệu đã kiểm chứng sau khi hoàn tất

| Prefab | Clip (`t = 0`) | Khoảng cách `upper_arm.L` → `spine.002` | Vị trí `upper_arm.L` (local root) | `shoulder.L` localEuler |
|---|---|---|---|---|
| `ChaserVisual_World.prefab` | `Idle` / `Walk` / `Attack` | **0.1595 m** | `(-0.1444, 0.2386, -0.0623)` | `(334.2°, 188.2°, 212.8°)` (Bind Pose) |
| `ChaserVisual_FPP.prefab` | `Idle` / `Walk` / `Attack` | **0.4184 m** | `(-0.1388, 0.4010, 0.3500)` | `(1.3°, 213.8°, 334.2°)` (FPP Override) |

