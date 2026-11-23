# Giai đoạn 7 — Nâng cao: Ưu tiên 3 (Gọi)

> Tài liệu phân rã từ `docs/04_roadmap.md` (Giai đoạn 7, bảng ưu tiên 3) theo yêu cầu ở `AGENTS.md` mục 7.3.
> **Mục tiêu:** hoàn thiện nhóm **Gọi nâng cao (NC)**:
> **nhận/từ chối cuộc gọi → tắt mic (và camera) → thời lượng → cuộc gọi nhỡ có thông báo**.
> Lịch sử cuộc gọi từng được làm rồi **ẩn hoàn toàn theo chốt 2026-11-23** (backend vẫn giữ API).
> **Phụ thuộc:** GĐ 4 (gọi CB) và GĐ 6 (app Flutter CB) — **đã xong**; GĐ 7 ưu tiên 1–2 — **đã xong**.
> **Trạng thái:** ✅ **đã cài đặt backend + web + app Flutter (2026-11-21), chỉnh cuối 2026-11-23: ẩn hoàn toàn lịch sử cuộc gọi, sửa lỗi media, giao diện gọi hiện avatar/tên khi đã kết nối** — backend **314/314** test; HTTP + SignalR thật **35/35**; web `lint`/`build` sạch + Chrome headless **20/20** (gồm gọi thật 2 tab cùng máy); app `flutter analyze` 0 issue, `flutter test` **31/31**. Còn lại: bấm thử trên máy thật/gọi 2 máy khác nhau. Xem mục 6.
> **Ước lượng:** 2–3 ngày công. **Không cần migration mới** (dùng `calls`, `call_participants` và `NotificationType.MissedCall` có sẵn).

---

## 1. Phạm vi

### 1.1. Trong phạm vi (đúng bảng ưu tiên 3 của roadmap)

| # | Yêu cầu roadmap | Hoàn thành khi | Trạng thái |
|---|---|---|---|
| 1 | **Nhận/từ chối hoạt động** | Người nhận thấy modal cuộc gọi đến, bấm Nghe vào phòng, bấm Từ chối kết thúc phía mình; người gọi thấy trạng thái đúng | 🚧 có sẵn nhưng sai ngữ nghĩa "đã nghe" (xem D1) |
| 2 | **Tắt mic (và camera)** | Nút mic/camera bật/tắt thật trên LiveKit, trạng thái nút phản ánh đúng | ✅ có sẵn từ GĐ 4, chỉ kiểm chứng lại |
| 3 | **Hiển thị thời lượng** | Đồng hồ bắt đầu từ lúc **người nhận** vào phòng; lịch sử/tin nhắn hiện đúng thời lượng | 🚧 sai mốc bắt đầu (D1) |
| 4 | **Cuộc gọi nhỡ có thông báo** | Quá 30 giây không ai nghe → `Missed`; người bị gỡ nhận notification + push, đang mở web/app thấy thông báo ngay | 🚧 có `Missed` nhưng chưa có thông báo |
| 5 | **Lịch sử cuộc gọi** | Ban đầu có tab lịch sử; **chốt lại 2026-11-23: ẩn hoàn toàn giao diện** — cuộc gọi chỉ hiện trong bong bóng chat, preview hội thoại và thông báo nhỡ. Backend vẫn giữ `GET /api/calls` cho tương lai | ✅ đã ẩn theo yêu cầu người dùng |

### 1.2. Ngoài phạm vi

- Badge số cuộc gọi nhỡ **chưa xem** (cần thêm trạng thái đã xem) — chốt **không làm** đợt này.
- Quyền riêng tư `who_can_call`, chặn người dùng → **Ưu tiên 4 (Bảo mật)**.
- Gọi lại ngoài hội thoại, chuyển tiếp/ghi âm cuộc gọi.
- Đánh dấu `Missed` cho người không tham gia cuộc gọi nhóm đã có người nghe (giữ hành vi hiện tại: "Không tham gia").

---

## 2. Quyết định thiết kế (đã chốt với người dùng 2026-11-21)

| Mã | Vấn đề | Quyết định | Lý do / đánh đổi |
|---|---|---|---|
| **D1** | Như thế nào là "đã nghe"? | Chỉ khi **người không phải người gọi** tham gia room (`Joined`/`Left`) mới đặt `answered_at` và chuyển `InProgress`. Người gọi vào room trước chỉ chờ máy | Sửa lỗi thật khiến cuộc gọi không bao giờ `Missed` và đồng hồ chạy sớm. Đổi lại: phải sửa cả `TimeoutRingingCalls`, `EndAsync`, webhook `room_finished` và thêm test |
| **D2** | Đồng hồ/lịch sử bắt đầu từ đâu | Từ `answered_at` (người nhận đầu tiên vào phòng) đến `ended_at`; thiếu mốc → `null` (hiện "—") | Đúng `docs/03` mục 3.4, khớp hành vi ứng dụng gọi thật |
| **D3** | Cuộc gọi nhỡ thông báo thế nào | Tạo **notification `MissedCall`** + push FCM (`type=CallMissed`) cho từng người bị gỡ; web/app đang mở nhận `CallEnded` → toast "Bạn đã bỏ lỡ cuộc gọi từ X" | Dùng bảng/loại thông báo có sẵn từ M4, không migration; app đóng vẫn biết. Chống trùng bằng `entity_id = call_id` |
| **D4** | Ai bị coi là "nhỡ"? | Participant `Missed` (không phải người gọi, không `Joined`/`Declined`/`Left`) khi cuộc gọi kết thúc `Missed` | Khớp `docs/03` mục 3.4 ("cuộc gọi nhỡ = status Missed") |
| **D5** | Lịch sử đặt ở đâu | **Ẩn hoàn toàn giao diện lịch sử cuộc gọi** (chốt lại 2026-11-23): bỏ tab/menu ở web + app; `/calls` chuyển hướng về Tin nhắn; giữ API backend cho tương lai. Cuộc gọi hiện trong bong bóng chat, preview hội thoại và thông báo nhỡ | Người dùng thấy lịch sử chưa cần thiết và muốn UI gọn. Đổi lại: mục "lịch sử cuộc gọi" của roadmap không còn màn hình; có thể bật lại sau vì backend vẫn giữ API |
| **D6** | DTO lịch sử | Chỉ **thêm** trường (`conversationType`, `conversationTitle`, `peer`, `initiatorDisplayName`, `myStatus`, `isMissed`, `answeredAt`, `endedAt`), không đổi/xoá trường cũ | Không phá client cũ; app/web cập nhật cùng đợt |
| **D7** | Tin nhắn loại `Call` hiển thị | Parse `call:{type}:{duration}:{reason}` ở client, dịch sang vi/en (`Cuộc gọi thoại · 39 giây`, `Cuộc gọi nhỡ`, …) | Nội dung đã có sẵn trong DB, không đổi schema/API; sửa lỗi web đang hiện chuỗi thô |
| **D8** | Web tự từ chối sau 30 giây | **Bỏ** hành vi này; chờ server đóng (`CallEnded`), quá ~6 giây không có sự kiện thì tự đóng UI | Giữ đúng ngữ nghĩa "nhỡ" thay vì "từ chối" |

---

## 3. Hợp đồng API (tóm tắt — chi tiết ở `docs/05_api_contract.md` mục 9.8)

### 3.1. Lịch sử cuộc gọi

`GET /api/calls?filter=all|missed&cursor=&limit=`

- `filter` mặc định `all`; giá trị khác → `400 VALIDATION_FAILED`.
- `missed`: chỉ các cuộc gọi mà **tôi** có `call_participants.status = Missed`.
- `CallSummaryDto` bổ sung:
  - `conversationType`: `Direct` | `Group`
  - `conversationTitle`: tên nhóm (null với 1–1)
  - `peer`: `{ userId, displayName, avatarUrl }` (chỉ 1–1)
  - `initiatorDisplayName`
  - `myStatus`: trạng thái participant của tôi
  - `isMissed`: `myStatus == "Missed"`
  - `answeredAt`, `endedAt`
- Giữ nguyên `id`, `conversationId`, `type`, `status`, `endReason`, `createdAt`, `durationSeconds`, `initiatedBy`, `iWasInitiator`.

### 3.2. Thông báo cuộc gọi nhỡ

- Khi cuộc gọi kết thúc `Missed`, backend tạo `notifications.type = MissedCall` cho từng người bị gỡ:
  - `entityType = "Call"`, `entityId = call.id`, `actorId = người gọi`
  - push FCM data: `type=CallMissed`, `callId`, `conversationId`, `callType`
- Không tạo cho người gọi, người đã vào phòng, người đã từ chối.
- Không tạo trùng nếu `FinishAsync` bị gọi lặp.

---

## 4. Phân rã công việc

### 4.1. Backend

- [x] `CallRules`/`CallService`: chỉ người không phải initiator `Joined/Left` mới tính "đã nghe"
- [x] `TimeoutRingingCalls`: vẫn đóng `Missed` khi chỉ người gọi ở trong room
- [x] `EndAsync` trước khi ai nghe → `Cancelled`, không phải `Completed`
- [x] `FinishAsync` khi `Missed`: đánh dấu người còn `Invited` thành `Missed` (trừ người gọi)
- [x] Tạo notification + push `MissedCall` idempotent
- [x] `CallSummaryDto` mở rộng + truy vấn lịch sử nạp tên peer/nhóm, không N+1
- [x] `GET /api/calls?filter=` + validate
- [x] Test tích hợp: người gọi join, timeout khi chỉ caller trong room, huỷ trước khi nghe, lịch sử peer/trạng thái/lọc, notification đúng người/không trùng

### 4.2. Web

- [x] `api/calls.ts`: kiểu `CallSummary` mới, `history(cursor, filter)`
- [x] Ẩn hoàn toàn lịch sử: không còn tab/menu; `/calls` chuyển hướng về `/messages`
- [x] Giữ preview hội thoại cho tin `Call` đã dịch
- [x] `IncomingCallModal`: bỏ tự `decline` khi hết 30 giây; chờ `CallEnded`, tự đóng dự phòng
- [x] `useChatConnection`: toast khi `CallEnded` status `Missed` và tôi không phải người gọi
- [x] Hiển thị tin nhắn `Call` đã dịch (thay chuỗi `call:...`)
- [x] i18n `vi`/`en`; `npm run lint` + `build`

### 4.3. App Flutter

- [x] `CallSummary`/`CallPeer` model + test parse
- [x] Ẩn lịch sử: bỏ tab Cuộc gọi thứ 5; `/calls` chuyển hướng `/chats`
- [x] Push `CallMissed`/notification `MissedCall` mở hội thoại chứa bong bóng cuộc gọi
- [x] Màn hình kết thúc do nhỡ hiện đúng "Cuộc gọi nhỡ"
- [x] Tin nhắn `Call` trong khung chat đã dịch
- [x] i18n vi/en; `flutter analyze` + `flutter test`

### 4.4. Tài liệu & kiểm chứng

- [x] Cập nhật `docs/05_api_contract.md` (9.4, 9.8, 10.3, lịch sử thay đổi); **chưa tick `docs/04_roadmap.md`** (theo AGENTS mục 12, chờ người dùng xác nhận)
- [x] Tái sinh `docs/openapi/comink.v1.json`; chạy `docs/openapi/verify.ps1` — **ĐẠT** (59 path, 70 operation, không drift)
- [x] Kiểm chứng HTTP/SignalR thật: lịch sử, lọc nhỡ, notification, quyền — **35/35 PASS**
- [x] Chrome headless: ẩn lịch sử, modal, tin nhắn Call, gọi thật 2 tab, ẩn dòng "đang chờ" khi đã kết nối — **20/20 PASS**
- [x] Ghi kết quả vào mục 6 của tệp này

---

## 5. Kịch bản kiểm chứng thủ công

1. **Nhận/từ chối:** A gọi B (web). B thấy modal; bấm **Từ chối** → A thấy "Đã từ chối"; hai bên thấy bong bóng cuộc gọi trong chat.
2. **Nhỡ:** A gọi B, không bấm gì. Sau ~30 giây: B thấy toast "Bạn đã bỏ lỡ cuộc gọi từ A" + notification `MissedCall`; bong bóng chat ghi cuộc gọi nhỡ.
3. **Huỷ trước khi nghe:** A gọi rồi kết thúc sau 5 giây → bong bóng `Cancelled`, không có thông báo nhỡ.
4. **Thời lượng:** B nghe, nói 15 giây, A kết thúc → bong bóng chat hiện "Cuộc gọi thoại · 15 giây"; màn hình gọi hiện avatar/tên khi đã kết nối.
5. **Tắt mic:** trong cuộc gọi, B tắt mic → B tự thấy mic tắt, A không nghe tiếng; bật lại bình thường; camera tương tự với cuộc gọi video.
6. **Lịch sử đã ẩn:** web/app không còn tab/menu Cuộc gọi; `/calls` tự chuyển về Tin nhắn. Cuộc gọi chỉ xem qua bong bóng chat + thông báo.
7. **Quyền:** D không thuộc cuộc gọi không xem được chi tiết, gọi `GET /api/calls/{id}` → `404`.

---

## 6. Kết quả triển khai (2026-11-21)

### 6.1. Tệp đã thay đổi

**Backend**

| Tệp | Nội dung |
|---|---|
| `Application/Calls/Dtos/CallDtos.cs` | `CallPeerDto`; `CallSummaryDto` thêm `MyStatus`, `IsMissed`, `ConversationType`, `ConversationTitle`, `Peer`, `InitiatorDisplayName`, `AnsweredAt`, `EndedAt` |
| `Application/Calls/Abstractions/ICallService.cs` | `GetHistoryAsync` thêm `filter` |
| `Infrastructure/Calls/CallService.cs` | Chỉ người không phải người gọi mới đặt `answered_at`/`InProgress`; sửa timeout/end/room_finished; đánh dấu `Missed`; tạo notification + push `MissedCall`; lịch sử nạp peer/tên người gọi + lọc `missed` |
| `Api/Endpoints/CallEndpoints.cs` | Query `filter` cho `GET /api/calls` |
| `tests/.../CallServiceTests.cs` | +5 test: initiator join, timeout khi chỉ caller, huỷ trước khi nghe, lịch sử peer/trạng thái, lọc missed + notification |

**Web**

| Tệp | Nội dung |
|---|---|
| `api/calls.ts` | `CallPeer`/`CallSummary` mới + `history(cursor, filter)` |
| `lib/callFormat.ts` (mới) | Dịch thời lượng, tin nhắn `Call`, nhãn trạng thái lịch sử |
| `pages/MessagesPage.tsx` | Trả lại danh sách hội thoại; vẫn dịch preview tin `Call` |
| `router/router.tsx`, `components/layout/AppHeader.tsx` | Bỏ route/menu **Cuộc gọi**; `/calls` chuyển hướng về Tin nhắn |
| `components/calls/IncomingCallModal.tsx` | Bỏ tự `decline` khi hết 30 giây; tự đóng dự phòng |
| `hooks/useChatConnection.ts` | Toast "Bạn đã bỏ lỡ cuộc gọi từ …" |
| `components/chat/MessageBubble.tsx` | Hiển thị tin nhắn `Call` đã dịch thay chuỗi `call:...` |
| `pages/CallPage.tsx` | Lý do kết thúc/thời lượng đã dịch; chống kết nối trùng StrictMode; nút **Thử lại**; theo dõi người đã vào phòng để ẩn "đang chờ" và hiện avatar/tên |
| `locales/vi.json`, `locales/en.json` | ~45 khoá mới |

**App Flutter**

| Tệp | Nội dung |
|---|---|
| `features/calls/domain/call_models.dart` | `CallPeer`, `CallSummary` |
| `features/calls/domain/call_format.dart` (mới) | Định dạng thời lượng/tin nhắn `Call`/trạng thái lịch sử |
| `features/chats/presentation/chats_page.dart` | Trả lại danh sách hội thoại; vẫn dịch preview tin `Call` |
| `features/shell/home_shell.dart`, `router/app_router.dart` | Bỏ tab Cuộc gọi thứ 5; `/calls` chuyển hướng `/chats` |
| `features/notifications/data/push_service.dart` | Push `CallMissed` → hội thoại chứa bong bóng cuộc gọi |
| `features/notifications/presentation/notifications_page.dart` | `MissedCall` → hội thoại chứa bong bóng cuộc gọi |
| `features/calls/presentation/call_overlay.dart` | Màn hình kết thúc "Cuộc gọi nhỡ"; chỉ hiện "đang chờ" khi chưa có người khác |
| `features/chats/presentation/chat_page.dart` | Tin nhắn `Call` đã dịch |
| `core/i18n/app_localizations.dart` | ~45 khoá vi/en |
| `test/call_models_test.dart` (mới) | 4 test model/format |

**Tài liệu**

| Tệp | Nội dung |
|---|---|
| `docs/05_api_contract.md` | Mục 9.4/9.5/9.8, 10.3, lịch sử thay đổi |
| `docs/openapi/comink.v1.json` | Tái sinh (thêm query `filter` + DTO mới) |
| `docs/tasks/phase7c_advanced_calls.md` | Tệp này |
| `infra/livekit.yaml` | `node_ip`/TURN domain cập nhật theo IP LAN hiện tại `10.12.87.18` (2026-11-23) |

### 6.2. Kết quả chạy

- `dotnet build` — 0 error (1 warning có sẵn ở `MessageService.cs:660`, ngoài phạm vi).
- `dotnet test` — **314/314** pass (Domain 115 · Application 25 · Tích hợp 174), gồm **20/20** `CallServiceTests` với 5 test mới.
- HTTP + **2 client SignalR thật** (A, B + người ngoài C) — **35/35 PASS**:
  - hết 30 giây → `Missed`; B nhận `CallEnded`; lịch sử B lọc `missed` có `isMissed=true`, `myStatus=Missed`, `durationSeconds=null`, `peer` là A; phía A `isMissed=false`, `status=Missed`;
  - B có notification `MissedCall` đúng `callId`, `actorId` là A; nghe sau hạn → `409`;
  - cuộc gọi thứ hai nghe rồi kết thúc → `Completed`, `durationSeconds >= 2`, lịch sử đúng; filter `missed` chỉ còn cuộc gọi nhỡ;
  - filter lạ → `400`; người ngoài không thấy lịch sử/không xem được cuộc gọi (`404`).
- `npm run lint` — 0 warning, 0 error; `npm run build` — thành công (cảnh báo bundle > 1 MB: KI-09).
- **Chrome headless + CDP** — **20/20 PASS** (tiếng Việt, sau khi ẩn lịch sử): đăng nhập form thật; không còn tab/menu Cuộc gọi; `/calls` chuyển hướng Tin nhắn; tin nhắn chat hiện "Cuộc gọi nhỡ" (không còn `call:Audio:`); modal cuộc gọi đến; **gọi thật 2 tab cùng máy**: A gọi → B Nghe → cả hai kết nối media, **dòng "Đang chờ…" biến mất và hiện tên/avatar người kia**, mic tắt/bật được, kết thúc `Completed`; không exception/lỗi console.
- LiveKit log sau khi sửa: `nodeIP: 10.12.87.18`, participant kết nối `connectionType: udp`, **không còn `DUPLICATE_IDENTITY`** hay `could not establish pc connection`.
- `flutter analyze` — **No issues found**; `flutter test` — **31/31** pass.
- `docs/openapi/verify.ps1` — **ĐẠT** (59 path, 70 operation, 43 endpoint app, không drift).

### 6.3. Ghi chú kỹ thuật

1. **Lỗi thật đã sửa:** `participant_joined` của người gọi từng đặt `answered_at`/`InProgress` ngay khi họ vào room chờ máy → cuộc gọi không bao giờ `Missed`, đồng hồ thời lượng chạy sớm, huỷ trước khi nghe bị ghi `Completed`. Nay chỉ người **không phải** người gọi mới tính "đã nghe" (D1).
2. **Web không còn tự từ chối khi hết 30 giây** — trước đây cuộc gọi bị ghi `Declined` thay vì `Missed`, nên người bị gỡ không nhận được thông báo nhỡ (D8).
3. **Không đổi schema**: dùng `NotificationType.MissedCall` và bảng `notifications` có từ M4; notification chống trùng theo `(recipient, type, entity_id)`.
4. **Không làm badge chưa xem**; **lịch sử cuộc gọi đã được ẩn hoàn toàn** theo chốt 2026-11-23 — cuộc gọi nhỡ xem qua thông báo và bong bóng cuộc gọi trong chat.
5. **Người gọi vẫn giữ participant `Joined`** khi vào room — chỉ không đặt `answered_at`; nhờ vậy `EndAsync` phía người gọi trước khi ai nghe vẫn phân biệt được `Cancelled`.

### 6.4. Điều chỉnh cuối 2026-11-23 — ẩn lịch sử, sửa media, giao diện gọi

**Bối cảnh:** người dùng báo lỗi `could not establish pc connection`, sau đó chốt **ẩn hoàn toàn lịch sử cuộc gọi** và yêu cầu sửa giao diện gọi khi cả hai đã vào phòng.

**1. Sửa lỗi media**
- **Nguyên nhân chính:** `infra/livekit.yaml` đang quảng bá `node_ip`/TURN domain cũ `10.104.230.46`, trong khi IP Wi-Fi hiện tại là `10.12.87.18` → ICE không có đường. Đã chạy `infra/set-lan-ip.ps1`, restart LiveKit; log xác nhận `nodeIP 10.12.87.18`.
- **Nguyên nhân phụ:** React StrictMode chạy effect `CallPage` hai lần → LiveKit ghi `DUPLICATE_IDENTITY`. Đã thêm kiểm tra `disposed` sau từng `await` và trước `room.connect`, chỉ giữ một kết nối.
- **UX:** thêm nút **Thử lại** ở màn hình lỗi media.
- **Kiểm chứng thật:** 2 tab Chrome cùng máy (thường + ẩn danh, fake media) gọi thoại thành công, mic tắt/bật được, kết thúc `Completed`; LiveKit log không còn `DUPLICATE_IDENTITY`/lỗi ICE.
- Người dùng đã test TURN 2 máy và tick mục TURN trong `docs/04_roadmap.md` (thay đổi đó do người dùng thực hiện, không nằm trong đợt sửa này).

**2. Ẩn hoàn toàn lịch sử cuộc gọi**
- Web: bỏ tab **Cuộc gọi** khỏi Tin nhắn và mục menu; `/calls` chuyển hướng `/messages`; vẫn giữ `/calls/:callId` cho cuộc gọi đang diễn ra. API backend `GET /api/calls` vẫn còn cho tương lai.
- App: bỏ tab Cuộc gọi thứ 5 và màn hình lịch sử; `/calls` chuyển hướng `/chats`; notification `MissedCall`/`CallMissed` mở đúng hội thoại chứa bong bóng cuộc gọi.
- Cuộc gọi vẫn hiện trong khung chat (bong bóng) và preview hội thoại; không còn nơi xem lịch sử riêng.

**3. Giao diện gọi khi đã kết nối**
- Web theo dõi `ParticipantConnected`/`ParticipantDisconnected`; chỉ hiện "Đang chờ người bên kia vào phòng…" khi **chưa có ai khác**.
- Khi đã có người: cuộc gọi thoại hiện avatar + tên (1–1 hoặc lưới nhóm); cuộc gọi video hiện video, nếu chưa có hình thì hiện avatar + tên.
- App: bỏ dòng "đang chờ" khi đã có người khác; giữ lưới avatar/tên.
- **Kiểm chứng Chrome headless:** **20/20 PASS**, gồm A/B gọi thật 2 tab và kiểm tra dòng "Đang chờ…" biến mất, tên/avatar hiện đúng.

**4. Kết quả chạy lại**
- `npm run lint` — 0 warning/0 error; `npm run build` — thành công (KI-09).
- `flutter analyze` — No issues found; `flutter test` — **31/31**.
- Backend không đổi trong đợt chỉnh này (giữ **314/314** từ 2026-11-21).

### 6.5. Chưa làm / việc tiếp theo

1. **Bấm thử trên máy thật** (Android) cho giao diện gọi + push `CallMissed` khi app đóng — chưa thể tự động hoá trong phiên này.
2. **Gọi thật 2 máy khác nhau**: người dùng đã test TURN 2 máy; vẫn nên thử lại luồng avatar/"đang chờ" trên 2 máy thật.
3. Tick `docs/04_roadmap.md` Giai đoạn 7 ưu tiên 3 sau khi bạn xác nhận kết quả trên thiết bị thật (mục TURN đã được bạn tick).
