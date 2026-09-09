# Thiết kế lõi Game Đuổi Bắt (Chase / Jailbreak Tag)

- **Ngày:** 2026-09-09
- **Trạng thái:** Đã duyệt thiết kế, chờ lập kế hoạch triển khai
- **Phạm vi:** Hệ thống lõi — nhân vật, phân vai, spawn, AI, cơ chế bắt/nhốt/cứu, điều kiện thắng/thua

---

## 1. Tổng quan

Game đối kháng theo chủ đề **đuổi bắt**, single-player thuần: **1 người chơi thật + 7 NPC** trong mỗi trận.

- Mỗi trận có **8 nhân vật**: 4 team ĐUỔI + 4 team TRỐN.
- Vào trận, player được **random role 50/50** (đuổi hoặc trốn) và điều khiển **1** nhân vật trong team đó. 7 con còn lại do AI điều khiển.
- **Không có networking / multiplayer online** — nay và trong tương lai gần.

Ý tưởng lối chơi giống thể loại **"cảnh sát bắt cướp / vượt ngục"**: đuổi dùng súng bắn để nhốt trốn vào lồng; trốn có thể cứu nhau ra khỏi lồng.

### Sự khác biệt giữa 2 role

Đuổi và Trốn **khác hẳn nhau** về model, animation, cách điều khiển, HUD. Tuy nhiên, **cùng một role thì player và NPC giống hệt nhau** — chỉ khác ở "bộ não" điều khiển (input người chơi vs AI). Đây là tiền đề định hình toàn bộ kiến trúc.

---

## 2. Nguyên tắc kiến trúc: tách Body ↔ Brain

Một nhân vật = **Body** (cái thân, biết *làm*) + **Brain** (bộ não, biết *quyết định*). Brain ra lệnh, Body thực thi — không bao giờ ngược lại. Body không biết và không quan tâm ai đang điều khiển nó.

Hệ quả:
- Player chỉ là "con đuổi/trốn có Brain = input người chơi".
- Đổi player ↔ NPC = đổi Brain, không đụng tới Body.
- Sửa 1 skill của role đuổi → sửa 1 chỗ, cả player lẫn NPC đều nhận.

---

## 3. Prefab

Làm **2 prefab, chia theo ROLE** (không chia theo player/npc):

- `ChaserCharacter.prefab` — model đuổi + Body components + bộ Abilities của đuổi. **Không có Brain.**
- `RunnerCharacter.prefab` — model trốn + Body components + bộ Abilities của trốn. **Không có Brain.**

Prefab luôn ở trạng thái "thân thuần". Brain được gắn vào lúc spawn.

---

## 4. Thành phần Body (nằm trên prefab)

| Component | Nhiệm vụ |
|-----------|----------|
| `Character` | Đầu não điều phối trên root. Giữ `Team` (Chaser/Runner), tham chiếu các sub-component. Cung cấp "API" cho Brain gọi: `Move(dir)`, `UseAbility(i)`, `GetCaught()`, `GetRescued()`… Không tự quyết định. |
| `CharacterMovement` | Di chuyển thật (CharacterController hoặc Rigidbody — chốt ở khâu triển khai), áp vận tốc, chạy animation di chuyển. |
| `CharacterAbilities` | Skill riêng theo role (xem mục 6). |
| `CharacterStats` (ScriptableObject) | Chỉ số: tốc độ, cooldown skill… tách khỏi code để chỉnh trong Editor. |

---

## 5. Thành phần Brain (gắn vào lúc spawn — Cách A)

```
CharacterBrain (base, MonoBehaviour, giữ tham chiếu Character)
├─ PlayerBrain            đọc nút move / skill1 / skill2 → gọi Character.Move()/UseAbility()
└─ AIBrain (abstract)     NavMesh + state machine
   ├─ ChaserAI            tìm con trốn Free gần nhất → đuổi → bắn khi tới tầm
   └─ RunnerAI            tìm con đuổi gần nhất → chạy tránh / dùng skill; đi cứu đồng đội bị nhốt
```

- `PlayerBrain` là **1 lớp duy nhất** cho cả 2 role: nó chỉ đọc input chung (di chuyển, skill 1, skill 2). Sự khác biệt skill theo role đã nằm trong `CharacterAbilities`.
- `AIBrain` tách thành **`ChaserAI` / `RunnerAI`** vì tư duy AI của 2 role khác hẳn nhau (đuổi vs tránh/cứu).
- **State machine đơn giản:** đuổi `{Chase → Attack}`; trốn `{Flee ↔ Wander → UseAbility → Rescue}`.
- **Tinh chỉnh AI qua `AIConfig` (ScriptableObject):** tầm nhìn, tốc độ phản ứng, ngưỡng dùng skill… Nhiều con AI dùng chung config, chỉnh trong Editor bất cứ lúc nào mà prefab vẫn thuần thân.

**Cách gắn Brain (Cách A):**
```csharp
CharacterBrain brain = isPlayer
    ? go.AddComponent<PlayerBrain>()
    : go.AddComponent(aiTypeTheoRole);   // ChaserAI hoặc RunnerAI
brain.Initialize(character /*, aiConfig nếu là AI */);
```

---

## 6. Kỹ năng theo role

**Team ĐUỔI:**
- `Gun` — bắn (raycast hoặc projectile — chốt khi triển khai). Bắn trúng con trốn Free → kích hoạt luồng bắt.

**Team TRỐN:**
- `SpeedBoost` — chạy nhanh hơn trong thời gian ngắn.
- `Decoy` — đánh lạc hướng team đuổi (chi tiết cơ chế chốt khi triển khai; ví dụ tạo mồi nhử/tín hiệu giả kéo AI đuổi đi chỗ khác).
- `Rescue` — mở lồng cứu đồng đội đang bị nhốt.

---

## 7. Cơ chế Bắt / Nhốt / Cứu

### Vòng đời con trốn (không loại vĩnh viễn)

```
Free  ──(bị bắn trúng)──►  Jailed (trong lồng)
  ▲                              │
  └────(đồng đội mở lồng cứu)────┘
```

### Luồng bắt
1. Đuổi bắn trúng một con trốn đang **Free**.
2. Con trốn dính **hiệu ứng làm chậm/đóng băng** (stun ngắn).
3. Một **Cage xuất hiện ngay tại chỗ** con trốn trúng đạn, nhốt nó lại (trạng thái **Jailed**, không di chuyển được).
4. `TeamRoster` đánh dấu con đó Jailed → `MatchManager` kiểm tra điều kiện thắng.

Mặc định đã chốt: **bắn trúng 1 phát là bị bắt ngay** (không cần nhiều phát).

### Luồng cứu
1. Một đồng đội đang **Free** tới cạnh Cage.
2. **Đứng giữ vài giây** (thanh tiến trình).
3. Cage mở → con trốn về **Free**, chơi tiếp (Brain cũ hoạt động lại).
4. `TeamRoster` cập nhật.

### Trải nghiệm khi player (trốn) bị nhốt
Camera ở lại lồng của player, chờ được cứu. Được cứu → chơi tiếp. Cả team bị nhốt hết → màn hình thua.

### Thành phần liên quan
- `Cage` — giữ 1 con trốn bị nhốt; có tiến trình mở; báo về hệ thống khi được mở.
- `JailSystem` (có thể gộp trong `MatchManager`) — điều phối bắt/cứu, cập nhật roster, chạy kiểm tra thắng.

---

## 8. Điều kiện thắng / thua

- Có **đồng hồ đếm ngược** mỗi trận (ví dụ 3 phút — chỉnh được).
- **Đuổi thắng:** cả **4 con trốn cùng lúc** ở trạng thái Jailed. (Vì có cứu nhau nên trạng thái dao động — phải nhốt hết cùng lúc mới thắng.)
- **Trốn thắng:** hết giờ mà còn **≥1 con trốn Free**.
- Kiểm tra điều kiện thắng chạy **mỗi khi có sự kiện bắt hoặc cứu** (và khi hết giờ).

---

## 9. Luồng vào trận & Spawn

**Thành phần điều phối:**

| Thành phần | Nhiệm vụ |
|-----------|----------|
| `MatchManager` | Điểm vào duy nhất của trận. Random role, gọi spawner, giữ đồng hồ, phán xử thắng/thua. |
| `SpawnManager` | Chỉ lo spawn: nhận (prefab, danh sách spawn point) → tạo các `Character`. |
| `TeamRoster` | Sau spawn giữ danh sách 4 đuổi + 4 trốn (kèm trạng thái Free/Jailed của trốn) + tham chiếu con của player. AI truy vấn để tìm mục tiêu. |

**Spawn points:** 2 object cha `ChaserSpawns` và `RunnerSpawns` trong scene, mỗi cái 4 con làm điểm xuất phát. `SpawnManager` đọc các con này.

**Trình tự khi vào scene Game (sau Loading):**
```
MatchManager.Start()
 ├─ 1. playerTeam = Random 50/50 (Chaser | Runner)
 ├─ 2. SpawnManager.SpawnTeam(ChaserCharacter, chaserSpawns) → 4 con đuổi
 │     SpawnManager.SpawnTeam(RunnerCharacter, runnerSpawns) → 4 con trốn
 ├─ 3. Chọn 1 con random trong nhóm playerTeam làm player
 ├─ 4. Gắn Brain (Cách A): con player → PlayerBrain; 7 con còn lại → ChaserAI/RunnerAI + AIConfig
 ├─ 5. Đăng ký tất cả vào TeamRoster
 └─ 6. PlayerBrain khởi tạo →
         • CameraController.Follow(playerCharacter)
         • HUD.Bind(playerCharacter, playerTeam)   ← chọn đúng HUD theo role
```

**AI tìm mục tiêu qua `TeamRoster`** (không `FindObjectsOfType` mỗi frame): đuổi hỏi "danh sách trốn Free"; trốn hỏi "danh sách đuổi" và "danh sách lồng cần cứu".

---

## 10. Phụ thuộc setup trong Editor

- **Bake NavMesh** cho map để AI (`ChaserAI`/`RunnerAI`) tìm đường.
- Đặt sẵn 2 nhóm spawn point (`ChaserSpawns`, `RunnerSpawns`), mỗi nhóm ≥ 4 điểm.
- Tạo các asset ScriptableObject: `CharacterStats` (cho từng role), `AIConfig`.
- Prefab `Cage` để `Cage` component/`JailSystem` instantiate lúc bắt.

---

## 11. Chiến lược test

Việc tách Body/Brain giúp test dễ hơn nhiều.

- **EditMode (logic thuần, không cần scene):**
  - Cooldown skill trong `CharacterAbilities`.
  - `TeamRoster`: thêm/xóa/đổi trạng thái Free↔Jailed, truy vấn.
  - `MatchManager`: phán xử thắng/thua đúng theo trạng thái roster (4 Jailed → đuổi thắng; hết giờ còn ≥1 Free → trốn thắng).
- **PlayMode:**
  - Vào trận → đúng 8 nhân vật, đúng **1** `PlayerBrain`, camera bám player.
  - Giả lập 1 cú bắn trúng → con trốn thành Jailed + Cage xuất hiện.
  - Giả lập cứu → con trốn về Free.
  - Nhốt hết 4 → đuổi thắng; hết giờ còn Free → trốn thắng.
- **Mẹo debug nhờ tách Brain:** gắn `AIBrain` lên chính con của player để xem AI tự chơi, hoặc gắn `PlayerBrain` lên con bất kỳ — tiện dò lỗi AI cả 2 role.

---

## 12. Các điểm để lại cho khâu triển khai

- Rigidbody vs CharacterController cho `CharacterMovement`.
- Súng dùng raycast hay projectile.
- Cơ chế cụ thể của `Decoy` (đánh lạc hướng AI đuổi).
- Thời lượng cụ thể: đồng hồ trận, thời gian stun khi trúng đạn, thời gian giữ để mở lồng, cooldown các skill (đưa vào `CharacterStats`/`AIConfig`).
- Thiết kế 2 bộ HUD riêng cho đuổi/trốn.
