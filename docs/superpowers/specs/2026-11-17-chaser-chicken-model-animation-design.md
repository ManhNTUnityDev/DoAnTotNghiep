# Thiết kế: Model & animation cho Chaser (gà)

Ngày: 2026-11-17
Trạng thái: chờ duyệt

## 1. Ý định và lý do

Mục tiêu dài hạn là chuyển game sang **góc nhìn thứ nhất cho cả hai vai**, để
hướng bắn quả trứng luôn khớp với tâm ngắm thay vì phải cộng trừ góc cho khớp.

Việc đó đang bị chặn bởi model Chaser. `Rig_Chick_Prop` — model đang gắn trên
`ChaserCharacter.prefab` — chỉ có mesh **cánh và chân**, không có thân. Đó là một
view model dựng cho góc nhìn thứ nhất: hợp khi người chơi tự nhìn xuống mình,
nhưng vô dụng khi **người chơi vào vai Runner và nhìn sang Chaser** — họ thấy một
con gà không thân. Vì `SpawnManager.PickPlayerTeam` tung đồng xu, nửa số trận
người chơi sẽ ở đúng tình huống đó.

Thêm nữa, trong lúc khảo sát phát hiện Chaser hiện **không chạy animation nào
cả**. Nên đây không phải việc "sửa model", mà là dựng đường animation cho con gà
từ đầu.

## 2. Hiện trạng đã xác minh

Các con số dưới đây lấy từ việc parse trực tiếp hai file FBX và bóc đường dẫn
curve trong cả 8 clip, không phải suy đoán.

| Hạng mục | Kết quả |
|---|---|
| `Rig_Chick_Prop.fbx` | rig Generic (`animationType: 2`), `avatarSetup: 1`, 15 geometry (cánh, 2 chân, + prop), 3 skin |
| `Main_Chic_Rig.fbx` | rig Generic, **`avatarSetup: 0` — chưa có Avatar**, 1 geometry `Mesh.031` (2749 verts), 1 skin bám `Armature` |
| Kích thước | **Trùng khớp.** Cả hai X ∈ [−0.165, 0.165]. Main cao Y ∈ [0.003, 0.694]; các mảnh FPP cao tới 0.375 (đúng vì bị cắt thân/đầu) |
| Import settings | Giống nhau (`globalScale: 1`, `useFileScale: 1`, `bakeAxisConversion: 0`) |
| Bộ xương | **Giống hệt nhau đến từng cấp** cho mọi xương được animate |
| 8 clip trong `Assets/Models/Player/` | Mỗi clip animate **46 xương đầy thân** (`spine→spine.001→spine.002→head→beak/mao/Eye`, `shoulder→upper_arm`, `hong→Tail.*`, `spine→thigh→shin`, `IK→Feet→toe.01-03`), lưu đường dẫn **dạng chuỗi** |
| Lệch duy nhất | `Armature/Bone/thigh.L.001`, `thigh.R.001` (xương phụ treo dưới `Bone`, **nằm ngoài chuỗi chân thật** `spine/thigh.L/shin.L`) và mesh `Chic.001` của curve `blendShape.Blink` |

**Kết luận: `Main_Chic_Rig` chạy được nguyên bộ clip hiện có. Không cần vẽ
animation mới.** Chữ "FPP" trong tên clip chỉ nói về góc nhìn của *mesh*, không
phải phạm vi *xương*.

### Những thứ đang hỏng (phát hiện kèm)

1. **Chaser không có Animator Controller.** `ChaserCharacter.prefab` nhét
   `Rig_Chick_Prop.fbx` làm child tên `Visual`, không gán controller, không có
   `CharacterAnimationController`. Con gà đang trượt trên đất ở tư thế tĩnh.
2. **`ChickenFeet.controller` chưa bao giờ được gắn vào project này.** Tham số
   của nó là `Move / Attack / Jump / IsGrounded / IsFalling / Hit / Take` — hàng
   tồn từ asset pack. Trong khi `CharacterAnimationController` set
   `Speed / IsJailed / OnAbility / AbilityID`. Phía Runner thì đúng:
   `NPC.controller` có chính xác 4 tham số này.
3. **`HandleAbilityUsed` bị comment hết phần thân.**
   `CharacterAnimationController.cs:100` chỉ còn một dòng `Debug.Log`; hai lệnh
   `SetInteger(AbilityID)` và `SetTrigger(OnAbility)` đang bị vô hiệu. Nghĩa là
   animation bắn/dùng skill **không bao giờ kích hoạt được**, cho cả hai vai.
4. **Gà không có clip `Run`** — chỉ có `Walk`. Game đuổi bắt mà base speed là
   5 m/s, có SpeedBoost ×1.6 lên 8 m/s.
5. **Animation của Runner cũng đang sai — `NPC.controller` chờ `Speed` chuẩn
   hoá, nhưng được bơm m/s thô.** Blend tree `Locomotion` có
   `m_MinThreshold: 0`, `m_MaxThreshold: 1`, `m_UseAutomaticThresholds: 1` và 3
   mốc **0 / 0.5 / 1** → `Idle` / `Walk(1-39)` / `Run(1-29)`. Với
   `RunnerStats.moveSpeed = 6` và `Speed` là m/s thô, Runner bị ghim ở `Run`
   ngay khi vượt 1 m/s; clip `Walk` của Runner là code chết. Nên câu "phía
   Runner đã xong" ở mục 4 là **sai** — hai vai đang dùng chung *tên* tham số
   với *đơn vị* không tương thích, tức đúng cái anti-goal ở mục 5 lẻn vào bằng
   cửa sau.
6. **`OnAbility` bị ghim (latch).** `NPC.controller` chỉ có **một** transition
   dùng `OnAbility`, và nó đòi `AbilityID == 1`. Thứ tự component trên
   `RunnerCharacter` cho SpeedBoost(0), Decoy(1) — nên mỗi lần Runner dùng
   SpeedBoost là trigger bị set mà không ai tiêu thụ. Bật lại mục 8.7 sẽ **kích
   hoạt lỗi này** cho cả hai vai.

## 3. Goals

- Người chơi vào vai Runner nhìn sang Chaser thấy **một con gà có thân đầy đủ**,
  đi/đứng có animation.
- Người chơi vào vai Chaser, ở góc nhìn thứ nhất, thấy **cánh và chân của mình**
  cử động.
- **Không vẽ animation mới.** Dùng lại nguyên 8 clip hiện có.
- Một Animator Controller duy nhất chạy được trên cả hai model.
- Animation của Chaser dùng **cùng bộ tham số** với Runner
  (`Speed / IsJailed / OnAbility / AbilityID`).

## 4. Non-goals

- Camera góc nhìn thứ nhất — việc kế tiếp, không nằm trong phạm vi này.
- Đổi cách tính hướng bắn trứng — việc sau nữa.
- Vẽ clip `Run` thật bằng tay.
- **Viết lại `NPC.controller`.** Animation Runner không nằm trong phạm vi *dựng*
  — nhưng mục 8.7 và 8.8 sửa script dùng chung, nên hai lỗi của Runner (ghim
  trigger ở 2.6, ghim `Run` ở 2.5) được chữa kèm mà không mở file controller
  nào. Hệ quả: Runner sẽ đi bộ ở tốc độ thấp, đã chấp nhận ở 10.1.
- Dọn ~30 condition mồ côi còn sót trong `NPC.controller` (`Action`, `ActionID`,
  `CageHoldAction`, `Hit`, `ClimbUp`… — tham số không còn tồn tại trên
  controller đó). Có trước việc này, không liên quan, nhưng đừng coi
  `NPC.controller` là "đã xong" nếu sau này phải mở nó ra.
- Hệ thống prop/skin của gà (`BaseballBat`, `SunGlasses`, `Flamingo`…).
- Layer `Wings` + `ChickenFeet.mask` — để lại cho sau nếu muốn flap cánh chồng
  lên movement.

## 5. Anti-goals

Những thứ mà nếu xảy ra thì coi như thất bại, dù tính năng có chạy:

- **Nhân đôi component gameplay.** Không được tạo prefab Chaser thứ hai chứa bản
  copy của `Gun`, `SpeedBoost`, `CharacterAbilities`, `CharacterMovement`…
  Hai bản sẽ lệch nhau ngay lần sửa số đầu tiên.
- **Hard-code tên child hoặc index** của model trong code gameplay
  (`transform.Find("Visual")` và tương tự).
- **Hai bộ tham số animation khác nhau** cho hai vai.
- Để `Main_Chic_Rig` dùng một controller riêng, tách khỏi view model.

## 6. Constraints

- Rig **Generic**, không phải Humanoid → không retarget tự do; clip bind theo
  **đường dẫn xương**, nên phải giữ nguyên tên và cấp của xương.
- Animator phải nằm trên GameObject là **cha trực tiếp của `Armature`**, vì
  đường dẫn trong clip bắt đầu bằng `Armature/...`.
- `CharacterAnimationController` yêu cầu `Animator` **cùng GameObject**, và tìm
  `Character` / `CharacterMovement` / `CharacterAbilities` bằng
  `GetComponentInParent`.
- `Speed` sẽ được **chuẩn hoá về 0–1** (quyết định ở mục 10.1). Hiện tại nó là
  độ lớn vận tốc ngang theo m/s lấy từ `CharacterController.velocity` (damp
  0.1s) — Chaser base 5 m/s, SpeedBoost ×1.6 → 8 m/s; Runner base 6 → 9.6 — còn
  `NPC.controller` thì chờ 0–1 (mục 2.5). Mục 8.8 sửa chỗ này.
- DI bằng VContainer; `SpawnManager` là plain class, nhận `SpawnSettings` do
  `GameLifetimeScope` serialize.
- Chỉ slot 0 của team được chọn là người thật (`isPlayer` trong `SpawnTeam`);
  còn lại là AI.
- `Main_Chic_Rig` **không có BlendShape deformer** → curve `blendShape.Blink`
  không có đích.

## 7. Approaches

Ba cách đã cân nhắc. Đã chốt **B** trong lúc trao đổi.

### A — Một model duy nhất: `Main_Chic_Rig` cho mọi con gà

Tạo Avatar, dựng controller, thay child `Visual` trong `ChaserCharacter`. Bỏ hẳn
`Rig_Chick_Prop`.

- Được: đơn giản nhất, một đường animation, không phân nhánh ở spawn.
- Mất: ở góc nhìn thứ nhất người chơi ngồi trong thân gà đầy đủ → phải tắt
  renderer của chính mình, và lúc đó **không còn thấy cánh/chân của mình**.
  Mất hẳn cảm giác góc nhìn thứ nhất mà bộ asset đã dựng sẵn cho việc đó.

### B — View model + world model (đã chọn)

Nhân vật người chơi giữ `Rig_Chick_Prop` (cánh + chân, nhìn từ trong ra vừa
khít). Chaser do AI điều khiển dùng `Main_Chic_Rig` (thân đầy đủ). Quyết định tại
`SpawnManager`, nơi đã có sẵn cờ `isPlayer`.

- Được: dùng asset đúng mục đích nó được dựng. Người chơi thấy cánh/chân mình,
  đối thủ thấy thân đầy đủ. Không phải vẽ animation nào — cùng một bộ clip chạy
  cho cả hai model vì chung bộ xương.
- Mất: thêm một prefab visual và một nhánh ở spawn.
- Lý do chọn: chi phí thêm chỉ là một prefab, và nó không phá cảm giác góc nhìn
  thứ nhất — vốn là lý do cả việc này bắt đầu. Nó cũng lặp lại đúng pattern
  project đang dùng cho Runner: `RunnerCharacter.prefab` (wrapper gameplay) nhét
  `RunnerNPC.prefab` (model + animator) làm child.

### C — Vẽ animation mới cho `Main_Chic_Rig`

Bỏ. Mục 2 đã chứng minh là không cần.

## 8. Các quyết định về cách làm

Những điểm dưới đây là **quyết định của tôi** (không phải do bạn chốt), ghi ra để
bạn soát lại khi đọc:

### 8.1 Cấu trúc prefab

`ChaserCharacter.prefab` giữ **toàn bộ** component gameplay ở root, không đổi.
Child `Visual` hiện tại được thay bằng **hai** child, mỗi cái là một nested
prefab riêng:

```
ChaserCharacter                 (CharacterController, NavMeshAgent, Character,
                                 CharacterMovement, CharacterAbilities, Gun, SpeedBoost,
                                 CharacterVisualSelector)
├── Visual_FirstPerson          -> ChaserVisual_FPP.prefab   (Rig_Chick_Prop)
│                                  Animator + CharacterAnimationController
└── Visual_World                -> ChaserVisual_World.prefab (Main_Chic_Rig)
                                   Animator + CharacterAnimationController
```

Cả hai dùng **cùng một** Animator Controller asset. Không component gameplay nào
bị nhân đôi.

### 8.2 Chọn model tại spawn

Component mới `CharacterVisualSelector` trên root, hai field serialize
(`firstPersonVisual`, `worldVisual`) và một hàm `Apply(bool isLocalPlayer)`:
giữ một child, **`Destroy` child còn lại** (không chỉ disable — để không còn
Animator nào phải tick và không có `CharacterAnimationController` thứ hai đăng ký
vào `OnAbilityUsed`).

`SpawnManager.SpawnTeam` gọi `Apply(isPlayer)` ngay sau khi spawn. Code gameplay
không bao giờ tra child theo tên — đáp ứng anti-goal.

Đặt trên `Character` nói chung thay vì riêng Chaser, để sau này Runner cũng dùng
được cùng cơ chế mà không phải viết lại.

`SpawnManager` **không phải** đường duy nhất để một Character xuất hiện:
`CharacterFactory.Spawn` là chỗ instantiate thật và được test gọi trực tiếp, còn
`Decoy.Use` (`Decoy.cs:108`) `Instantiate(self.gameObject)` để dựng clone. Nên
`Apply` phải chịu được cả hai trường hợp xấu:

- **Gọi hai lần:** idempotent. Lần thứ hai không được `Destroy` thêm gì nếu đã
  chỉ còn một visual.
- **Không ai gọi:** `Start` tự thu về **world visual** nếu chưa có ai chọn. Mặc
  định này an toàn: một con gà đủ thân không bao giờ *sai*, chỉ là không phải
  góc nhìn thứ nhất. Thiếu fallback này thì một Character lọt qua
  `CharacterFactory` sẽ giữ **cả hai** visual → hai Animator cùng tick và hai
  `CharacterAnimationController` cùng đăng ký `OnAbilityUsed`, làm mọi animation
  skill chạy hai lần.

Nhờ idempotent + fallback, clone của `Decoy` (chụp lại sau khi `Apply` đã chạy,
nên đã chỉ còn một visual) không cần xử lý riêng.

### 8.3 Animator Controller mới

Tạo mới chứ không sửa `ChickenFeet.controller` (controller đó mang cả bộ tham số
và state lạ của asset pack). Controller mới dựng theo khuôn của `NPC.controller`
để hai vai nhất quán:

- Tham số: `Speed` (float), `OnAbility` (trigger), `AbilityID` (int). **Bỏ
  `IsJailed`** — Chaser không bao giờ bị bắt, và `CharacterAnimationController`
  đã có `HasBoolParameter` guard nên không sinh warning.
- Layer `Base`: blend tree 1D theo `Speed`.
- Layer `Ability`: trạng thái bắn, kích bằng `OnAbility`, chọn clip theo
  `AbilityID` (0 = Gun → `Armature_Chic_FPP_Attack`, 1 = SpeedBoost → không có
  clip riêng, bỏ qua).

`AbilityID` hiện là **index trong `GetComponents<IAbility>()`**, nên cùng một số
mang nghĩa khác nhau giữa hai vai (Chaser 0 = Gun; Runner 0 = SpeedBoost), và
đổi thứ tự component trong Inspector sẽ âm thầm remap animation mà không lỗi
biên dịch. **Chấp nhận như hiện tại** cho lần sửa này — đổi sang id ổn định
(enum trên `IAbility` hoặc field serialize) là một thay đổi interface, nên làm
riêng. Ghi lại ở đây vì nó đắt dần lên khi đã có hai controller phụ thuộc vào
index.

### 8.4 Avatar cho `Main_Chic_Rig`

Đổi `avatarSetup` sang "Create From This Model". Clip vẫn bind theo đường dẫn vì
hierarchy trùng khớp, nhưng có Avatar thì Animator hoạt động đúng chuẩn Generic
và tránh phụ thuộc vào hành vi ngầm.

### 8.5 Xử lý thiếu clip `Run`

Blend tree 1D theo `Speed` (đã chuẩn hoá 0–1) với 3 mốc, dùng lại
`Armature_Chic_FPP_Walk` ở mốc cuối với **tốc độ phát nhanh hơn**:

| Mốc | `Speed` | Clip | Tốc độ phát |
|---|---|---|---|
| đứng | 0 | `Armature_Chic_FPP_Idle` | 1.0 |
| đi | 0.625 | `Armature_Chic_FPP_Walk` | 1.0 |
| chạy | 1.0 | `Armature_Chic_FPP_Walk` | 1.6 |

Mốc 0.625 là tỉ lệ base trên max của Chaser: `ChaserStats.moveSpeed = 5` chia
`5 × SpeedBoost.multiplier 1.6 = 8`. Đây là giải pháp tạm; thay bằng clip `Run`
thật sau này chỉ là đổi motion ở mốc thứ ba.

### 8.6 Hai thứ lệch nhỏ

- `thigh.L.001` / `thigh.R.001`: **bỏ qua**, không xử lý. Chúng treo trực tiếp
  dưới `Bone`, ngoài chuỗi chân thật, nên curve bị drop không ảnh hưởng hình.
  Unity không log lỗi cho xương không tìm thấy.
- `blendShape.Blink`: **bỏ qua** ở model world. `Main_Chic_Rig` không có
  BlendShape deformer nên không có đích; con gà world sẽ không nháy mắt. Chấp
  nhận được vì ở khoảng cách runner nhìn sang thì không thấy.

### 8.7 Bật lại `HandleAbilityUsed`, kèm `ResetTrigger`

Bỏ comment hai dòng trong `CharacterAnimationController.HandleAbilityUsed`, xóa
dòng `Debug.Log`, và **thêm `animator.ResetTrigger(OnAbilityHash)` ngay trước**
`SetInteger` / `SetTrigger`:

```csharp
animator.ResetTrigger(OnAbilityHash);
animator.SetInteger(AbilityIDHash, abilityId);
animator.SetTrigger(OnAbilityHash);
```

Không có dòng `ResetTrigger` thì lỗi ghim trigger ở mục 2.6 được kích hoạt: một
skill không có clip (Chaser `AbilityID 1`, Runner `AbilityID 0`) set trigger mà
không transition nào tiêu thụ, trigger nằm chờ, rồi nổ sai thời điểm khi
`AbilityID` đổi sang giá trị có transition. Với `ResetTrigger` đi trước, trigger
cũ bị xóa ở đầu mỗi lần dùng skill nên chỉ fire đúng một lần, đúng lúc, cho cả
hai vai.

Đây là sửa trong script dùng chung, nên nó cũng chữa luôn lỗi phía Runner — một
dòng, không đụng tới `NPC.controller`.

### 8.8 Chuẩn hoá `Speed` về 0–1

Trong `CharacterAnimationController.Update`, chia độ lớn vận tốc ngang cho tốc độ
tối đa hiện tại trước khi bơm vào Animator:

```csharp
float maxSpeed = movement.MaxSpeed;   // BaseSpeed * SpeedMultiplier
float normalized = maxSpeed > 0.01f
    ? Mathf.Clamp01(horizontalVelocity.magnitude / maxSpeed)
    : 0f;
animator.SetFloat(SpeedHash, normalized, 0.1f, Time.deltaTime);
```

`CharacterMovement` hiện chỉ expose `SpeedMultiplier`, còn `BaseSpeed` là
private → cần thêm một property công khai `MaxSpeed` (= `BaseSpeed *
speedMultiplier`) vào `CharacterMovement`.

**Không phải sửa `ICharacterMovement`.** Interface đó chỉ có
`SetMoveDirection` / `MoveTo` / `Stop`, và `CharacterAnimationController` vốn đã
giữ một field `CharacterMovement movement` kiểu cụ thể (serialize trong
Inspector, fallback `GetComponentInParent`), nên đọc `movement.MaxSpeed` trực
tiếp là đủ. Giữ interface nguyên vẹn.

**Lưu ý về chia cho `SpeedMultiplier`:** vì max cũng nhân theo multiplier, khi
SpeedBoost bật thì `Speed` chuẩn hoá *không* tự nhảy lên 1 — nhân vật chạy nhanh
hơn nhưng vẫn ở gần mốc cũ. Đó là hành vi đúng cho blend tree (clip khớp với tốc
độ chân so với thân), và `CharacterMovement` đã đồng bộ `agent.speed` theo
multiplier nên hai bên không lệch.

Sửa này nằm trong script dùng chung nên `NPC.controller` (mốc 0 / 0.5 / 1,
`m_MaxThreshold: 1`, `m_UseAutomaticThresholds: 1`) **đúng ngay mà không phải sửa
file** — chữa luôn lỗi ở mục 2.5. Hệ quả thấy được: Runner sẽ *đi bộ* ở tốc độ
thấp thay vì luôn chạy. Đã được chấp nhận (mục 10.1).

## 9. Để lại cho builder

- Cách dựng blend tree và transition cụ thể trong Animator window (threshold,
  transition duration, exit time) — miễn giữ đúng 3 mốc ở 8.5.
- Đặt tên và vị trí file cho controller mới và hai prefab visual, theo quy ước
  thư mục đang dùng.
- Có cần `Write Defaults` / `Foot IK` hay không trên từng state.
- Vị trí chính xác của `Visual_*` child (local position/rotation) — bám theo
  child `Visual` hiện tại.
- Test: `CharacterVisualSelector.Apply` là hàm thuần về mặt logic chọn, nên viết
  EditMode test cho nó theo đúng pattern các test hiện có trong
  `Assets/Tests/EditMode`. Hai case đáng assert nhất là **không ai gọi** và
  **gọi hai lần** (mục 8.2).

## 10. Hai điểm đã chốt sau review

Bản thiết kế đã qua một lượt review của một builder độc lập. Phần lớn câu hỏi
được trả lời ngay trong tài liệu (mục 2.5, 2.6, 8.2, 8.3, 8.7, 8.8 thêm/sửa sau
review). Hai điểm dưới đây nới phạm vi ra ngoài con gà nên do chủ dự án quyết.

### 10.1 Đơn vị của `Speed` — **chốt: chuẩn hoá 0–1**

Mục 2.5 cho thấy `NPC.controller` chờ 0–1 nhưng đang nhận m/s thô, nên Runner bị
ghim ở `Run` và clip `Walk` của Runner là code chết.

Chuẩn hoá trong `CharacterAnimationController` (mục 8.8): một dòng trong script
dùng chung, `NPC.controller` đúng ngay mà không phải mở, mốc của gà thành
0 / 0.625 / 1. **Đã chấp nhận** hệ quả: Runner sẽ đi bộ ở tốc độ thấp thay vì
luôn chạy.

Phương án còn lại — giữ m/s thô và viết lại blend tree của `NPC.controller`
thành 0 / 6 / 9.6 — bị loại vì nhiều việc hơn mà vẫn phải đụng vào vai Runner.

### 10.2 Người chơi vào vai Chaser — **chốt: chấp nhận tạm**

Camera trong `Game.unity` hiện là `SimpleFollowCamera` với
`offset = (0, 3, −3)` — orbit ~4.2m, pitch ~45°. Scene có 4 điểm spawn Chaser.
Khi đồng xu cho người chơi vào vai Chaser, họ sẽ nhìn **chính nhân vật mình** từ
4.2m sau lưng và thấy một cặp cánh với hai cái chân không thân, trong khi 3
Chaser AI bên cạnh có thân đầy đủ — đúng cái lỗi tài liệu này mở đầu bằng, chỉ
chuyển từ đối thủ sang chính người chơi.

**Chấp nhận** vì camera góc nhìn thứ nhất là việc ngay sau đây, nên hồi quy chỉ
sống đúng một bước. `Apply(bool isLocalPlayer)` giữ nguyên hình dạng một tham số,
và thay đổi này ship được độc lập, không phải land cùng phần camera.
