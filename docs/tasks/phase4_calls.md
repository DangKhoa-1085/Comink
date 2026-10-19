# Giai đoạn 4 — Gọi trực tuyến CB (thoại/video 1–1 và nhóm)

> Tài liệu 06 (tiếp), phân rã từ `docs/04_roadmap.md` (Giai đoạn 4) theo `AGENTS.md` mục 7.3.
> **Mục tiêu:** gọi thoại/video 1–1 và gọi nhóm (≥3 người) hoạt động ổn định, có token ngắn hạn, có ghi DB, có UI cuộc gọi đến, kết thúc giải phóng tài nguyên.
> **Phụ thuộc:** GĐ 1 (auth + SignalR) ✅ · GĐ 2 (bạn bè, media) ✅ · GĐ 3 (hội thoại 1–1 + hub) ✅.
> **Trạng thái:** 🚧 **ĐANG LÀM** — D1/D2 đã chốt. **Backend đã xong và kiểm chứng thật** (migration M3 đã áp, 154 test xanh, gọi cuộc gọi thật qua LiveKit, webhook 200, TURN/UDP bật). Web (UI gọi) còn lại.
>
> **Tiến độ (2026-10-19):**
> - ✅ **M3 đã tạo + áp vào DB**: `calls`, `call_participants`, `messages.call_id` (FK `SET NULL`), CHECK cho 4 enum, index `(conversation_id, created_at DESC)` và `(user_id, invited_at DESC)`.
> - ✅ **Domain** `CallRules`: chuyển trạng thái, tính thời lượng, `ResolveEnd` (thứ tự ưu tiên Completed → Missed → Cancelled → Declined → Failed), validate danh sách người mời.
> - ✅ **Infrastructure**: `LiveKitCallProvider` (JWT HS256 + Twirp CreateRoom/DeleteRoom), `CallService` (bắt đầu 1–1/nhóm, cấp token, answer/decline/end, lịch sử, xử lý webhook).
> - ✅ **API**: 7 endpoint `/api/calls` + webhook `/api/webhooks/livekit` (xác thực HS256 + claim `sha256`), mã lỗi đã map vào `ApiResults`.
> - ✅ **Kiểm chứng thật (LiveKit thật):** tạo cuộc gọi → `201 Ringing` + room `comink_…` được tạo; token `ws://localhost:7880` hạn 10 phút; **người ngoài xin token → `404 CALL_NOT_FOUND`**; answer → `InProgress` + `answered_at`; end → `Completed`, `duration`, **`Đã xoá room LiveKit`**; DB có `calls`/`call_participants` + tin nhắn `call:Audio:2:Completed`; **webhook `room_started`/`room_finished` trả 200**.
> - 🐞 **Lỗi #4 (tự phát hiện):** webhook bị **401** vì LiveKit gửi JWT **trần** trong header `Authorization` (không kèm `Bearer `) — đã chấp nhận cả hai dạng. Bẫy phụ: `MemberRole.Owner = 0` + cột default `Member` (cùng lớp lỗi `Visibility` GĐ 2) → đã thêm `HasSentinel`.
> - 🐞 **Lỗi #5:** `room_started` từng bị tính là "đã trả lời" → cuộc gọi không ai nghe sẽ thành `Completed`; nay chỉ `participant_joined` mới đặt `answered_at`.
> - ✅ **TURN (D2)** đã bật: `turn.domain=192.168.1.29`, UDP 3478 + relay 30500–30510 (publish trong compose), `tls_port=0` vì LiveKit bắt buộc TLS cert nếu bật.
> - ✅ **Test**: Domain +14 (`CallRulesTests`), Tích hợp +12 (`CallServiceTests`) ⇒ **`dotnet test` 154/154**, build 0 warning.
> - ⏳ Còn lại: **web UI gọi** (modal cuộc gọi đến, màn hình gọi, nút gọi), thử 2 máy thật + TURN relay (`iceTransportPolicy: 'relay'`), tick mục 5.
> **Migration tương ứng:** **M3** (`calls`, `call_participants`, `messages.call_id`).
> **Người phụ trách:** Dev B (Nhắn tin, Gọi, Push) — `docs/04_roadmap.md` mục 2.

---

## 1. Phạm vi

### 1.1. Trong phạm vi (CB — 8 mục roadmap)

| # | Yêu cầu roadmap | Hướng thiết kế |
|---|---|---|
| 1 | Gọi thoại 1–1, nghe/nói hai chiều | LiveKit room, token `video` grant, client SDK |
| 2 | Gọi video 1–1 hai chiều | Cùng room, bật camera |
| 3 | Gọi nhóm ≥ 3 người (thoại + video) | Cần **D1** — xem mục 2 |
| 4 | Token ngắn hạn; người ngoài không xin được | `POST /api/calls/{id}/token` kiểm tra thành viên trước khi ký JWT |
| 5 | TURN hoạt động giữa hai mạng/NAT | Cần **D2** + sửa `infra/livekit.yaml` + publish cổng TURN |
| 6 | Ghi DB: người gọi, người nhận, loại, thời điểm kết thúc | Bảng `calls` + `call_participants` (M3) |
| 7 | Hiện cuộc gọi đến để người nhận vào | SignalR `CallIncoming` tới `user:{id}` + modal toàn cục ở web |
| 8 | Kết thúc giải phóng tài nguyên, không room treo | `empty_timeout` của LiveKit + `EndRoom` khi người cuối rời + webhook `room_finished` |

### 1.2. Ngoài phạm vi (NC)

Chia sẻ màn hình, ghi âm/ghi hình, chuyển tiếp cuộc gọi, gọi nhóm từ hội thoại chat nhóm (chat nhóm vẫn là NC của GĐ 3), push notification khi app đóng (GĐ 6), lịch sử cuộc gọi thành màn hình riêng (dùng tin nhắn loại `Call` trong khung chat là đủ).

---

## 2. Quyết định cần chốt

| Mã | Vấn đề | Phương án | Trạng thái |
|---|---|---|---|
| **D1** | **Gọi nhóm khi chưa có chat nhóm** (GĐ 3 để chat nhóm là NC) | Tạo `conversation` loại `Group` "ẩn" làm vật chứa cuộc gọi: người gọi chọn ≥2 bạn bè → tạo conversation `Group` (+ thành viên) → tạo `call` gắn vào đó. Conversation `Group` **không hiện trong danh sách chat** (lọc `type = Direct`) nên không kéo theo phạm vi chat nhóm | ✅ **đã chốt (2026-10-19): phương án (a)** |
| **D2** | **TURN giữa hai mạng khác nhau** | Bật TURN với **IP LAN** của máy dev (`turn.domain=<ip-lan>`, `use_external_ip: true`, publish UDP 3478 + TCP 5349) → gọi được giữa hai máy cùng LAN và các trường hợp **sau NAT của router** | ✅ **đã chốt (2026-10-19): bật TURN với IP LAN** |
| D3 | Thời hạn token & đếm giờ | 💡 Token vào room **10 phút** (gọi lại nếu lâu hơn); `empty_timeout=60s`, `departure_timeout=20s`; đổ chuông tối đa **30s** rồi `NoAnswer` | 💡 |
| D4 | Hiển thị lịch sử cuộc gọi | 💡 Ghi một tin nhắn loại `Call` vào hội thoại khi kết thúc (dùng `messages.call_id` của M3) — không thêm màn hình riêng | 💡 |
| D5 | Bảo mật webhook | 💡 Xác thực JWT HS256 của LiveKit bằng `ApiSecret` trước khi xử lý payload; từ chối nếu sai chữ ký | 💡 |

> D1 ảnh hưởng migration + service; D2 ảnh hưởng hạ tầng Docker và khả năng đạt mục 5. Cả hai cần chốt **trước khi code**.

---

## 3. Hợp đồng API (đề xuất — sẽ thành mục 9 của `docs/05_api_contract.md`)

### 3.1. Quy ước

- Mọi endpoint yêu cầu đăng nhập; người ngoài cuộc gọi → **404 `CALL_NOT_FOUND`** (không lộ sự tồn tại).
- Token LiveKit là JWT **ngắn hạn**, ký HS256 bằng `ApiSecret`; backend **không** trả `ApiSecret` cho client.
- Media (âm thanh/hình) đi **trực tiếp** client ↔ LiveKit; backend chỉ cấp token và ghi trạng thái.

### 3.2. Bắt đầu cuộc gọi

`POST /api/calls` — body: `{ "conversationId": uuid, "type": "Audio" | "Video", "participantIds"?: uuid[] }`

- 1–1: bỏ `participantIds`, người nhận là thành viên còn lại của hội thoại `Direct`.
- Nhóm (nếu D1 chọn a): gửi `participantIds` (≥2, phải là bạn bè) → backend tạo conversation `Group` ẩn + `call`.
- Trả `201 CallDto`: `{ id, conversationId, type, status, roomName, initiatedBy, createdAt, participants: [{ userId, displayName, status }] }`
- Phát SignalR `CallIncoming` tới `user:{id}` của từng người được mời.
- Lỗi: `CALL_ALREADY_ACTIVE` (409 — đang có cuộc gọi chưa kết thúc trong hội thoại), `CONVERSATION_NOT_FOUND` (404), `MESSAGING_NOT_FRIENDS` (403, nếu mời người lạ).

### 3.3. Lấy token vào room

`POST /api/calls/{id}/token` → `200 { url, token, roomName, expiresAt }`

- `url` = `LiveKit:Host` (client nối WS tới đó).
- **Chỉ thành viên** của cuộc gọi mới nhận token (mục 4 roadmap); người ngoài → `404 CALL_NOT_FOUND`.
- Cuộc gọi đã kết thúc → `409 CALL_ALREADY_ENDED`.

### 3.4. Chấp nhận / từ chối / kết thúc

| Method | Đường dẫn | Ý nghĩa |
|---|---|---|
| `POST` | `/api/calls/{id}/answer` | Người nhận đồng ý → `status=InProgress`, participant `Joined`, đặt `answered_at`; phát `CallUpdated` |
| `POST` | `/api/calls/{id}/decline` | Từ chối → participant `Declined`; nếu mọi người từ chối → `status=Declined`, `end_reason=Declined` |
| `POST` | `/api/calls/{id}/end` | Kết thúc (người gọi huỷ hoặc người trong cuộc gọi kết thúc) → `status=Completed`, tính `duration_seconds`, gọi `EndRoomAsync`, phát `CallEnded`, ghi tin nhắn `Call` |
| `GET` | `/api/calls/{id}` | Chi tiết (chỉ thành viên) |
| `GET` | `/api/calls?cursor=&limit=` | Lịch sử cuộc gọi của tôi (`call_participants` theo `user_id`, sắp `invited_at DESC`) |

### 3.5. Webhook LiveKit

`POST /api/webhooks/livekit` (không cần access token, **xác thực chữ ký JWT**)

- `room_finished` → đặt `ended_at`, `duration_seconds`, `end_reason=Completed` nếu chưa kết thúc; gọi `EndRoomAsync` phòng trường hợp còn treo; phát `CallEnded`.
- `participant_joined` / `participant_left` → cập nhật `call_participants.status`, `joined_at`/`left_at`.
- `room_started` → đặt `answered_at` nếu chưa có.

### 3.6. SignalR

| Sự kiện | Gửi tới | Payload |
|---|---|---|
| `CallIncoming` | `user:{id}` của người được mời | `CallDto` |
| `CallUpdated` | `user:{id}` của thành viên | `CallDto` |
| `CallEnded` | `user:{id}` của thành viên | `{ callId, status, endReason, durationSeconds }` |

### 3.7. Mã lỗi mới

| Mã | HTTP | Ý nghĩa |
|---|---|---|
| `CALL_NOT_FOUND` | 404 | Không có cuộc gọi **hoặc** không phải thành viên |
| `CALL_ALREADY_ACTIVE` | 409 | Hội thoại đang có cuộc gọi chưa kết thúc |
| `CALL_ALREADY_ENDED` | 409 | Cuộc gọi đã kết thúc, không cấp token |
| `CALL_NOT_RINGING` | 409 | Thao tác chỉ hợp lệ khi đang đổ chuông |
| `CALL_PROVIDER_UNAVAILABLE` | 503 | Không gọi được LiveKit (server chưa chạy) |

---

## 4. Công việc

### 4.1. Database — M3

- [ ] `Call` (id, conversation_id, initiator_id, type, status, room_name UQ, created_at, answered_at, ended_at, duration_seconds, end_reason)
- [ ] `CallParticipant` (call_id, user_id, status, invited_at, joined_at, left_at) — PK ghép
- [ ] `messages.call_id` (FK→calls `SET NULL`) để ghi tin nhắn loại `Call`
- [ ] Enum: `CallType` (`Audio`,`Video`), `CallStatus` (`Ringing`,`InProgress`,`Completed`,`Declined`,`Cancelled`,`Missed`,`Failed`), `CallParticipantStatus` (`Invited`,`Joined`,`Declined`,`Missed`,`Left`), `CallEndReason` (`Completed`,`NoAnswer`,`Declined`,`Cancelled`,`Failed`)
- [ ] CHECK constraint cho cả 4 enum; index `calls(conversation_id, created_at DESC)`, `call_participants(user_id, invited_at DESC)`
- [ ] Cập nhật `docs/03_database_design.md` nếu có sai khác

### 4.2. Domain

- [ ] `Domain/Calls/CallRules.cs`: bảng chuyển trạng thái hợp lệ; ai được `answer`/`decline`/`end`; tính `duration_seconds`; đổ chuông quá 30s ⇒ `NoAnswer`

### 4.3. Application

- [ ] DTO: `CallDto`, `CallParticipantDto`, `CallTokenDto`, `CallSummaryDto`; request `StartCallRequest`
- [ ] `ICallService`, `ICallNotifier` (SignalR, cài ở tầng Api như `IChatNotifier`), mã lỗi mục 3.7
- [ ] `ICallProvider` đã có ở `Application/Common/Interfaces` (dùng lại, không đổi chữ ký trừ khi cần `ttl`)

### 4.4. Infrastructure

- [ ] `LiveKitCallProvider`: tạo/xoá room qua Twirp API (`/twirp/livekit.RoomService/CreateRoom|DeleteRoom`), sinh JWT HS256 (`iss`=ApiKey, `sub`=userId, `video.room`, `roomJoin`, `canPublish`, `ttl`)
- [ ] `CallService`: bắt đầu/exoá trạng thái, kiểm tra thành viên, chống cuộc gọi trùng, ghi tin nhắn `Call` khi kết thúc
- [ ] Đăng ký DI `ICallProvider`, `ICallService`

### 4.5. API

- [ ] `Endpoints/CallEndpoints.cs` (6 endpoint mục 3.2–3.4) + map mã lỗi vào `ApiResults.StatusFor/TitleFor` (⚠️ bẫy GĐ 2: quên là 500)
- [ ] `Endpoints/LiveKitWebhookEndpoints.cs` (mục 3.5) — `AllowAnonymous` + xác thực chữ ký

### 4.6. SignalR

- [ ] `CallIncoming`/`CallUpdated`/`CallEnded` qua `ICallNotifier` (Api) tới `user:{id}` — **không** dùng group hội thoại (bài học GĐ 3: group hội thoại có thể chưa tồn tại lúc kết nối)

### 4.7. Web

- [ ] Thêm `livekit-client` (Apache-2.0), `lib/callConnection.ts` (join/leave, bật/tắt mic–camera, theo dõi participant)
- [ ] Modal **cuộc gọi đến** toàn cục (nghe `CallIncoming`), có nút **Nghe** / **Từ chối** và đổ chuông 30s
- [ ] Màn hình cuộc gọi `/calls/{id}`: video grid, nút mic/camera/kết thúc, đồng hồ thời lượng
- [ ] Nút **Gọi thoại** / **Gọi video** ở trang cá nhân và trong khung chat
- [ ] Xử lý quyền mic/camera bị từ chối (thông báo rõ, `AGENTS.md` mục 8.4 tinh thần)
- [ ] i18n `vi`/`en`; ba trạng thái tải/rỗng/lỗi

### 4.8. Test

- [ ] Domain: `CallRules` (mọi chuyển trạng thái hợp lệ/không hợp lệ, tính thời lượng)
- [ ] Tích hợp: người ngoài **không** lấy được token (404); cuộc gọi trùng trong hội thoại (409); `answer`→`InProgress`; `end`→`Completed` + `duration_seconds` > 0; webhook sai chữ ký bị từ chối (401); webhook `room_finished` đóng cuộc gọi treo
- [ ] Đơn vị cho `LiveKitCallProvider`: JWT có đúng `iss`/`sub`/`video.room`/`exp`; không lộ `ApiSecret`

### 4.9. Hạ tầng & tài liệu

- [ ] Bật TURN theo D2 (`infra/livekit.yaml` + publish cổng trong `docker-compose.yml`)
- [ ] Bật webhook LiveKit trỏ về `http://host.docker.internal:5117/api/webhooks/livekit`
- [ ] `README.md`: mục hướng dẫn gọi (bật LiveKit, quyền camera/mic, HTTPS/`localhost`)
- [ ] Ghi chú trạng thái GĐ 4 vào `docs/04_roadmap.md` khi xong

---

## 5. Tiêu chí hoàn thành (đối chiếu roadmap)

- [ ] Hai tài khoản ở **hai cửa sổ**: gọi thoại 1–1 nghe/nói hai chiều
- [ ] Gọi video 1–1 hai chiều
- [ ] Gọi nhóm 3 người (thoại + video)
- [ ] Người ngoài gọi `POST /api/calls/{id}/token` → **404**
- [ ] TURN: gọi được giữa hai thiết bị sau NAT (theo phương án D2 đã chốt)
- [ ] `calls` + `call_participants` có dữ liệu; `ended_at`, `duration_seconds` đúng
- [ ] Người nhận thấy modal cuộc gọi đến và vào được cuộc gọi
- [ ] Kết thúc cuộc gọi: room không còn trong `GET /twirp/livekit.RoomService/ListRooms`
- [ ] `dotnet build` 0 warning, `dotnet test` xanh, `npm run lint`/`build` sạch

---

## 6. Bẫy và rủi ro đã biết

1. **`getUserMedia` cần secure context**: `http://localhost` được coi là an toàn, nhưng **`http://<ip-lan>:5173` thì KHÔNG** → camera/mic bị chặn. Muốn test từ máy thứ hai phải dùng HTTPS cho Vite (self-signed) hoặc chạy Chrome với `--unsafely-treat-insecure-origin-as-secure=http://<ip>:5173`. **Đây là lý do phổ biến nhất khiến "gọi từ máy khác không lên hình".**
2. **LiveKit trong Docker trên Windows**: UDP 7882 phải được firewall cho phép; `use_external_ip: true` có thể dò ra IP sai trong môi trường Docker Desktop → kiểm tra log LiveKit.
3. **TURN cần domain/IP hợp lệ**: giá trị `localhost` làm container thoát ngay (đã ghi trong `infra/livekit.yaml`).
4. **Webhook từ container**: phải dùng `host.docker.internal`, không dùng `localhost` (trong container `localhost` là chính nó); endpoint webhook phải `AllowAnonymous` nhưng **phải** kiểm chữ ký.
5. **Token phải kiểm thành viên trước**: nếu chỉ dựa vào `callId` do client gửi thì người ngoài sẽ lấy được token (vi phạm mục 4 roadmap).
6. **Room treo**: `empty_timeout` + `EndRoomAsync` khi kết thúc + webhook `room_finished` là ba lớp bảo vệ; thiếu lớp nào cũng có thể còn room.
7. **Cuộc gọi trùng**: phải chặn hai cuộc gọi cùng lúc trong một hội thoại (`CALL_ALREADY_ACTIVE`), tránh rò rỉ room.
8. **`messages.call_id` tạo FK vòng** mới (messages → calls → conversations); dùng `SET NULL`, tránh CASCADE.
9. **Không log token**: `AGENTS.md` mục 9.6 — token LiveKit là bí mật ngắn hạn, không ghi log.
10. **Trạng thái "đang gọi"** phải được đóng khi mất kết nối cả hai phía: dựa vào webhook `room_finished` làm nguồn sự thật, không chỉ dựa vào client gọi `/end`.
