# Giai đoạn 7 — Nâng cao: Ưu tiên 1 (Nhắn tin)

> Tài liệu phân rã từ `docs/04_roadmap.md` (Giai đoạn 7, bảng ưu tiên 1) theo yêu cầu ở `AGENTS.md` mục 7.3.
> **Mục tiêu:** hoàn thiện nhóm **Nhắn tin nâng cao (NC)** theo thứ tự ưu tiên của nhóm:
> **chat nhóm → trạng thái online → "đang nhập" → đã gửi/đã đọc**.
> **Phụ thuộc:** GĐ 3 (nhắn tin CB + chat nhóm) — **đã xong**; GĐ 6 (app Flutter CB) — **đã xong**.
> **Trạng thái:** ✅ **đã cài đặt backend + web + app Flutter của 4 mục (2026-11-17)** — backend 224/224 test, 28/28 kiểm chứng HTTP + 2 client SignalR thật; web lint/build sạch; app `flutter analyze` 0 issue, `flutter test` 19/19. Còn lại: bấm thử bằng mắt trên trình duyệt và trên máy thật. Xem mục 8.
> **Ước lượng:** 1–1,5 tuần. Migration tương ứng: **M6** (`Messaging_AddReadMarkers`).

---

## 1. Phạm vi

### 1.1. Trong phạm vi (đúng bảng ưu tiên 1 của roadmap)

| # | Yêu cầu roadmap | Hoàn thành khi | Trạng thái hiện tại |
|---|---|---|---|
| 1 | **Chat nhóm hoạt động** | Tạo nhóm 3–10 người từ bạn bè, gửi/nhận tin nhóm, hiện tên người gửi | ✅ **đã xong ở GĐ 3 NC** (mục 7 của `phase3_messaging.md`); mục này chỉ còn việc kiểm chứng lại cùng bộ tính năng mới |
| 2 | **Hiển thị online/offline đúng** | Có chấm online/offline ở danh sách hội thoại, đầu khung chat và thành viên nhóm; nhiều tab vẫn đúng; mất mạng/kết nối lại đúng | ❌ chưa có (bảng `conversation_members` chưa có mốc đọc; chưa có presence) |
| 3 | **"Đang nhập" hiện và tự tắt** | Người kia đang gõ → hiện "đang nhập…"; ngừng gõ/gửi tin → tự tắt sau vài giây; nhóm hiện tên người đang gõ | ❌ chưa có |
| 4 | **Đã đọc cập nhật real-time** | Mở khung chat → mốc đọc được lưu; người gửi thấy "Đã đọc" ngay không cần tải lại; danh sách hội thoại có số tin chưa đọc thật | ❌ chưa có (`unreadCount` đang luôn `0`) |

> "Đã gửi" hiểu là tin đã lưu DB và đã phát real-time; **không** làm trạng thái "đã nhận" riêng — theo quyết định #5 của `docs/03_database_design.md` mục 7.

### 1.2. Ngoài phạm vi của ưu tiên này (để các ưu tiên sau)

- Trả lời, thu hồi, chỉnh sửa, thả cảm xúc, ghim, chuyển tiếp, tìm kiếm tin, tắt thông báo từng cuộc → **Ưu tiên 2**.
- Lịch sử cuộc gọi, cuộc gọi nhỡ → **Ưu tiên 3**.
- Cài đặt riêng tư `show_online_status` / `show_read_receipts` (đã có cột trong `user_settings` từ M0 nhưng chưa dùng) → **Ưu tiên 4 (Bảo mật/quyền riêng tư)**. Xem quyết định **D5**.
- Rời nhóm, thêm/xoá thành viên, đổi tên nhóm → chưa có trong phạm vi NC nào của tài liệu; không làm ở đây.

---

## 2. Quyết định thiết kế

| Mã | Vấn đề | Quyết định | Lý do / đánh đổi |
|---|---|---|---|
| **D1** | Presence lưu ở đâu? | **Redis**: mỗi người một **sorted set** `connection id → mốc ping gần nhất`, key có TTL; "online" = còn ít nhất một connection trong cửa sổ 90 giây (SCARD/ZCOUNT), đúng `docs/02_tech_stack_decisions.md` mục 5.2 ("Presence lưu trong Redis với TTL; đếm số kết nối của mỗi người để xử lý nhiều tab/thiết bị"). Redis lỗi → coi như offline, **không** làm sập hub | Nhiều tab/thiết bị cần đếm theo kết nối, không chỉ một cờ `online`. Mốc ping + TTL dọn trạng thái khi client chết mà không gửi disconnect (không cần dọn tay từng connection). Đổi lại: thêm phụ thuộc Redis vào đường presence (đã có sẵn trong hạ tầng từ GĐ 0) |
| **D2** | Ai được thấy trạng thái online của ai? | Chỉ những người **cùng ít nhất một hội thoại còn hiệu lực**. Sự kiện `PresenceChanged` phát qua `user:{id}` của đúng nhóm người này | Không phát toàn cục để tránh lộ trạng thái cho người lạ/admin (tinh thần `docs/02` mục 4.4 và KI-04). Dùng truy vấn DB mỗi lần kết nối/ngắt — quy mô đồ án chấp nhận được; có thể cache sau |
| **D3** | Mốc "đã đọc" lưu thế nào? | Dùng đúng cột đã thiết kế `conversation_members.last_read_message_id` + `last_read_at` (migration **M6**), mỗi thành viên **một mốc**, chỉ **tiến** không lùi | Thiết kế có sẵn ở `docs/03` mục 3.3, tránh bảng "mỗi tin một dòng" phình nhanh. `unreadCount` = số tin có `(created_at, id)` lớn hơn mốc và không phải của chính mình |
| **D4** | "Đang nhập" gửi thế nào? | Tín hiệu **nhẹ đi thẳng qua Hub** (`Typing` client → server), server phát `TypingChanged` cho group hội thoại trừ chính connection gửi. **Không lưu DB**, client throttle 3 giây/lần và tự tắt sau 5 giây ngừng gõ; server chặn tối thiểu 2 giây/người/hội thoại | Đúng `docs/02` mục 5.2 ("Đang nhập: tín hiệu qua Hub, không lưu DB, có throttle phía client"). Không cần REST vì đây là tín hiệu tạm thời |
| **D5** | Có tôn trọng `show_online_status` / `show_read_receipts` ngay không? | **Chưa** — luôn bật, ghi nhận là việc của Ưu tiên 4. Hai cột đã có trong `user_settings` nhưng chưa có entity/endpoint | Tránh làm trước phần quyền riêng tư khi chưa có màn hình cài đặt; tài liệu đã đánh dấu hai cột này **[NC]** thuộc nhóm Bảo mật |

> Các quyết định này được cập nhật vào `docs/05_api_contract.md` (mục 8.7–8.10) và `docs/03_database_design.md` (mục 6.3) trong cùng thay đổi.

---

## 3. Hợp đồng API (tóm tắt — chi tiết ở `docs/05_api_contract.md`)

### 3.1. Rest

| Method | Đường dẫn | Body | Trả về |
|---|---|---|---|
| `POST` | `/api/conversations/{id}/read` | `{ "messageId": uuid }` | `204 No Content` (idempotent, mốc chỉ tiến) |

- Không phải thành viên → `404 CONVERSATION_NOT_FOUND`.
- `messageId` không thuộc hội thoại → `404 MESSAGE_NOT_FOUND`.
- Mốc cũ hơn/bằng mốc hiện tại → vẫn `204`, **không** phát sự kiện (tránh nhiễu khi mở lại khung chat).
- Mốc mới → phát `MessagesRead` tới mọi thành viên còn hiệu lực.

### 3.2. DTO bổ sung (thêm trường, không phá client cũ)

- `ConversationPeerDto`: `+ isOnline: bool`, `+ lastSeenAt: string|null`.
- `ConversationMemberDto`: `+ isOnline`, `+ lastSeenAt`, `+ lastReadMessageId`, `+ lastReadAt`.
- `ConversationSummaryDto.unreadCount`: nay là số thật (trước luôn `0`).

### 3.3. SignalR — hub `/hub/chat`

| Hướng | Tên | Payload | Gửi cho |
|---|---|---|---|
| Server → client | `PresenceChanged` | `{ userId, isOnline, lastSeenAt? }` | Người dùng cùng hội thoại (D2) |
| Server → client | `TypingChanged` | `{ conversationId, userId, isTyping }` | Group `conversation:{id}`, **trừ** connection vừa gửi |
| Server → client | `MessagesRead` | `{ conversationId, userId, messageId, readAt }` | Mọi thành viên còn hiệu lực |
| Client → server | `Typing(conversationId, isTyping)` | — | Chỉ thành viên; không phải thành viên → bỏ qua im lặng |
| Client → server | `Ping()` | — | Làm mới TTL presence (client gửi mỗi 30 giây) |

- Tên hằng số đặt trong `ChatEvents` (Application), không viết tay rải rác.
- Hub **vẫn không** mở method join/leave group; group do server quyết định lúc kết nối (giữ nguyên quyết định GĐ 3).

---

## 4. Phân rã công việc

### 4.1. Rà lại chat nhóm (mục 1 của ưu tiên)

- [x] Tạo nhóm 3–10 người từ bạn bè; người tạo `Owner` (đã có từ GĐ 3)
- [x] Gửi/nhận tin nhóm real-time, hiện tên người gửi (đã có)
- [x] Gọi nhóm ngay trong nhóm (đã có từ GĐ 3 NC)
- [ ] Kiểm chứng lại khi có presence/typing/đọc: nhóm hiện đúng chấm online, "X đang nhập", "Đã xem bởi N" *(backend nhóm đã kiểm chứng bằng 2 client SignalR; phần hiển thị trên trình duyệt chờ bấm thử)*

### 4.2. Database (M6) — ✅

- [x] `conversation_members.last_read_message_id` (uuid, FK→`messages` `SET NULL`) + `last_read_at` (timestamptz)
- [x] Entity `ConversationMember` + cấu hình EF (quan hệ FK khai báo tường minh)
- [x] Migration `Messaging_AddReadMarkers` (`20261116225955_...`); đã áp lên DB dev; đã cập nhật `docs/03` mục 3.3/6.3

### 4.3. Domain — ✅

- [x] `ConversationRules.CanAdvanceReadMarker(...)`: chỉ tiến mốc theo `(created_at, id)`
- [x] Test domain cho quy tắc tiến mốc (bằng nhau → không tiến, cũ hơn → không tiến, mới hơn → tiến)

### 4.4. Application — ✅

- [x] `IPresenceService` (`ConnectAsync` trả "vừa online", `TouchAsync`, `DisconnectAsync` trả `lastSeenAt` khi kết nối cuối đóng, `GetStatusesAsync`)
- [x] `IConversationService`: `MarkReadAsync` + `GetCoMemberUserIdsAsync` + `IsMemberAsync`
- [x] `IChatNotifier`: `MessageReadAsync`, `PresenceChangedAsync`
- [x] DTO mới `PresenceDto`, `ReadReceiptDto`, `MarkReadRequest`, `TypingSignalDto`; mở rộng DTO hội thoại
- [x] Mã lỗi `MESSAGE_NOT_FOUND` + map HTTP 404 trong `ApiResults`

### 4.5. Infrastructure — ✅

- [x] `RedisPresenceService`: sorted set `comink:presence:conns:{userId}` (connection id → mốc ping, TTL 90 s, đếm trong cửa sổ 90 s), `comink:presence:lastseen:{userId}` (TTL 7 ngày); lỗi Redis → log warning + coi như offline
- [x] `ConversationService`: `unreadCount` bằng **một** truy vấn join (không N+1), presence cho peer/thành viên, `MarkReadAsync` (thành viên + tin thuộc hội thoại + `UPDATE ... WHERE` chỉ tiến) phát `MessagesRead`
- [x] DI đăng ký `IPresenceService` (singleton)

### 4.6. Api — ✅

- [x] `ChatHub`: đăng ký/ngắt presence, phát `PresenceChanged`; `Typing`/`Ping`. Kiểm tra quyền `Typing` bằng truy vấn membership (đúng cả với hội thoại tạo **sau** lúc kết nối; server chặn ≥ 2 giây/người/hội thoại bằng `TypingThrottle`)
- [x] `SignalRChatNotifier`: phát `MessagesRead`, `PresenceChanged`
- [x] Endpoint `POST /api/conversations/{id}/read` + validator
- [x] Test: `ChatHubTests` (presence/typing/ping/throttle), tích hợp `ReadReceiptTests` (quyền, chỉ tiến, unread, phát sự kiện)

### 4.7. Web (React) — ✅

- [x] `api/conversations.ts`: thêm trường mới + `markRead`
- [x] `lib/chatConnection.ts` + `hooks/useChatConnection.ts`: nhận `PresenceChanged`/`TypingChanged`/`MessagesRead`, gửi `Ping` mỗi 30 s, cập nhật cache TanStack Query
- [x] Store `typingStore` (tự hết hạn sau 6 s); presence cập nhật trực tiếp vào cache hội thoại/chi tiết
- [x] `MessagesPage`: chấm online/offline + badge chưa đọc
- [x] `ChatPage`: chấm online/offline + "truy cập lần cuối", "đang nhập…" (1–1 và nhóm), gửi `Typing` có throttle, gọi `markRead` khi mở/nhận tin, hiện "Đã gửi"/"Đã đọc"/"Đã xem bởi N"
- [x] i18n `vi`/`en` cho toàn bộ chuỗi mới; trạng thái tải/rỗng/lỗi giữ nguyên
- [x] `npm run lint` (0/0) + `npm run build` (thành công)

### 4.8. App Flutter — ✅

- [x] Model + API `markRead`; cập nhật DTO (`ConversationPeer.isOnline/lastSeenAt/lastRead*`)
- [x] `chat_realtime.dart`: nhận presence/typing/read, gửi typing + ping 30 giây, tự dọn "đang nhập" sau 6 giây
- [x] `chats_page.dart`: chấm online + badge chưa đọc; `chat_page.dart`: đầu khung chat có trạng thái/"đang nhập", "Đã đọc"/"Đã xem bởi N"
- [x] i18n vi/en; `flutter analyze` 0 issue + `flutter test` 19/19

### 4.9. Kiểm chứng

- [x] `dotnet build` 0 warning; `dotnet test` **224/224** xanh (Domain 72 · Application 25 · Tích hợp 127)
- [x] HTTP thật: 3 tài khoản + 1 người ngoài — mốc đọc, unread, quyền 404, chỉ tiến
- [x] 2 client SignalR thật: presence bật/tắt đúng người trong hội thoại, typing đúng người nhận, đọc cập nhật ngay, người ngoài **không** nhận gì (28/28)
- [x] App: `flutter analyze` 0 issue; `flutter test` **19/19**
- [ ] Trình duyệt / máy thật: mở 2 tài khoản, xem chấm online, gõ thấy "đang nhập", mở khung chat thấy "Đã đọc"; ngắt kết nối thấy offline *(chưa có công cụ trình duyệt/thiết bị trong phiên này — cần bạn bấm thử theo mục 5)*
- [x] Cập nhật `docs/05_api_contract.md` (mục 8.9–8.11), `docs/03_database_design.md` (M6), ghi chú trạng thái ở `docs/04_roadmap.md`

---

## 5. Kịch bản kiểm chứng thủ công

1. Đăng nhập **A** (Chrome thường) và **B** (cửa sổ ẩn danh) là bạn bè; mở `/messages` ở cả hai.
2. A thấy B có chấm xanh; đóng cửa sổ B → trong ≤ 5 giây A thấy B offline ("truy cập lần cuối …").
3. Mở lại B, vào khung chat với A; A gõ → B thấy "đang nhập…" rồi tự tắt sau ~5 giây ngừng gõ.
4. A gửi tin → B đang mở khung chat thấy tin ngay; B rời khung chat → A thấy "Đã gửi"; B mở lại khung chat → A thấy "Đã đọc" **không tải lại trang**; danh sách hội thoại của B hết badge chưa đọc.
5. Tạo nhóm 3 người A, B, C: gửi tin, thấy tên người gửi; B gõ → A thấy "B đang nhập"; C mở khung chat → A/B thấy "Đã xem bởi 1".
6. Bấm `window.__cominkChat.disconnect()` ở B → A thấy B offline; `reconnect()` → online lại; tin bị lỡ vẫn đồng bộ như GĐ 3.
7. Người ngoài (D) không nằm trong hội thoại: mở khung chat A–B không nhận được presence/typing/read của A, gọi `POST /read` → `404`.

---

## 6. Rủi ro và ghi chú

| Rủi ro | Cách xử lý |
|---|---|
| Redis chết giữa lúc demo | Presence coi như offline và chỉ log warning; chat/đọc vẫn chạy. Không để exception từ Redis làm hỏng `OnConnectedAsync` |
| Client chết không gửi disconnect → treo "online" | TTL 90 s + client `Ping` 30 s. Truy vấn presence đọc Redis (không đọc cache trong RAM) nên hết TTL là offline ngay ở lần tải danh sách kế tiếp |
| Hai tab cùng mở | Đếm theo **số kết nối**, chỉ tab cuối đóng mới phát offline |
| Nhiều client gửi `markRead` liên tục | Mốc chỉ tiến + client chỉ gửi khi có tin mới và `document.visibilityState === 'visible'`; server có thể thêm rate limit ở Ưu tiên 4 |
| Hội thoại tạo **sau** khi client kết nối (lỗi thật GĐ 3) | Presence/typing/read đều phát qua `user:{id}` (read) hoặc group `conversation:{id}` **chỉ khi** người dùng đã ở trong hội thoại. Với hội thoại vừa tạo, sender và người nhận đều có group `user:{id}` nên tin/presence vẫn tới; typing của người mới tạo chưa vào group hội thoại sẽ **không** tới người kia — chấp nhận và ghi chú, vì khi tải lại trang/khung chat thì group được thêm lại. `MessagesRead` cũng phát qua `user:{id}` nên không bị |

---

## 7. Liên kết

- Quy chuẩn chung: `docs/02_tech_stack_decisions.md` mục 5.2 (thời gian thực), 5.5 (hội thoại).
- Schema: `docs/03_database_design.md` mục 3.3 (`conversation_members.last_read_*`), 6.3 (migration).
- Hợp đồng: `docs/05_api_contract.md` mục 8.
- Roadmap: `docs/04_roadmap.md` Giai đoạn 7, bảng ưu tiên 1.
- Bài học GĐ 3: `docs/tasks/phase3_messaging.md` mục 2 và 7 (phát sự kiện qua `user:{id}`).

---

## 8. Kết quả triển khai (2026-11-17)

### 8.1. Tệp đã thay đổi

**Backend**

| Tệp | Nội dung |
|---|---|
| `Domain/Messaging/ConversationRules.cs` | `CanAdvanceReadMarker` (mốc chỉ tiến theo `(created_at,id)`) |
| `Application/Messaging/Dtos/MessagingDtos.cs` | `MarkReadRequest`, `PresenceDto`, `ReadReceiptDto`, `TypingSignalDto`; mở rộng `ConversationMemberDto`/`ConversationPeerDto` |
| `Application/Messaging/ChatEvents.cs` | `MessagesRead`, `PresenceChanged`, `TypingChanged` |
| `Application/Messaging/Abstractions/(IConversationService\|IPresenceService).cs` | Hợp đồng mới |
| `Application/Messaging/Validators/MessagingValidators.cs` | `MarkReadRequestValidator` |
| `Application/Common/ErrorCodes.cs` + `Api/Common/ApiResults.cs` | `MESSAGE_NOT_FOUND` → 404 |
| `Infrastructure/Persistence/Entities/ConversationMember.cs` + `Configurations/.../MessagingConfigurations.cs` | Hai cột mốc đọc + FK `SET NULL` |
| `Infrastructure/Persistence/Migrations/20261116225955_Messaging_AddReadMarkers*` | Migration **M6** (+ chỉ mục FK) |
| `Infrastructure/Messaging/RedisPresenceService.cs` | Presence Redis (tập connection + TTL, chịu lỗi Redis) |
| `Infrastructure/Messaging/ConversationService.cs` | `MarkReadAsync`, `GetCoMemberUserIdsAsync`, `IsMemberAsync`, `unreadCount` thật, presence trong DTO |
| `Infrastructure/DependencyInjection.cs` | Đăng ký `IPresenceService` |
| `Api/Hubs/ChatHub.cs` | Presence on connect/disconnect, `Typing`, `Ping` |
| `Api/Realtime/TypingThrottle.cs` | Chặn spam "đang nhập" (≥ 2 s/người/hội thoại) |
| `Api/Realtime/SignalRChatNotifier.cs` | Phát `MessagesRead`, `PresenceChanged` |
| `Api/Endpoints/MessagingEndpoints.cs` | `POST /api/conversations/{id}/read` |
| `Api/Program.cs` | Đăng ký `ITypingThrottle` |

**Web**

| Tệp | Nội dung |
|---|---|
| `web/src/api/conversations.ts` | Trường presence/mốc đọc + `markRead` |
| `web/src/lib/chatConnection.ts` | Tên sự kiện mới, tham chiếu kết nối đang hoạt động, `sendTyping`/`sendPresencePing` |
| `web/src/hooks/useChatConnection.ts` | Nhận presence/typing/read, cập nhật cache, ping 30 s, dọn `typingStore` khi mất kết nối |
| `web/src/stores/typingStore.ts` | Trạng thái "đang nhập" tự hết hạn sau 6 s |
| `web/src/components/chat/PresenceDot.tsx` | Chấm online/offline có nhãn cho trình đọc màn hình |
| `web/src/pages/MessagesPage.tsx` | Chấm online + badge chưa đọc |
| `web/src/pages/ChatPage.tsx` | Trạng thái, "đang nhập", gửi typing, đánh dấu đã đọc, "Đã gửi"/"Đã đọc"/"Đã xem bởi N" |
| `web/src/locales/{vi,en}.json` | 10 khoá `chat.*` mới |

**App Flutter**

| Tệp | Nội dung |
|---|---|
| `mobile/lib/features/chats/domain/chat_models.dart` | `ConversationPeer` có `isOnline`, `lastSeenAt`, `lastReadMessageId`, `lastReadAt` |
| `mobile/lib/features/chats/data/chat_realtime.dart` | Nhận `PresenceChanged`/`TypingChanged`/`MessagesRead`; `sendTyping`, `sendPing` (30 s), `markRead`; trạng thái "đang nhập" tự hết hạn |
| `mobile/lib/features/chats/presentation/chats_page.dart` | Chấm online trên avatar + badge tin chưa đọc |
| `mobile/lib/features/chats/presentation/chat_page.dart` | Trạng thái đầu khung chat, "đang nhập", gửi typing có throttle, đánh dấu đã đọc, "Đã gửi"/"Đã đọc"/"Đã xem bởi N" |
| `mobile/lib/core/i18n/app_localizations.dart` | 9 khoá `chat_*` mới (vi/en) |

**Test**

| Tệp | Nội dung |
|---|---|
| `tests/Comink.Domain.Tests/MessagingRulesTests.cs` | +4 test quy tắc tiến mốc |
| `tests/Comink.Api.IntegrationTests/ReadReceiptTests.cs` | 6 test: tiến mốc, phát sự kiện, chỉ tiến, quyền 404, tin khác hội thoại, unread |
| `tests/Comink.Api.IntegrationTests/ChatHubTests.cs` | 12 test: group, presence, typing, ping, throttle |
| `tests/Comink.Api.IntegrationTests/MessagingFakes.cs` | Fake presence/notifier dùng chung |

### 8.2. Kết quả chạy

- `dotnet build` — 0 warning, 0 error.
- `dotnet test` — **224/224** pass (Domain 72 · Application 25 · Tích hợp 127).
- `npm run lint` — 0 warning, 0 error.
- `npm run build` — thành công (còn cảnh báo bundle > 1 MB: KI-09).
- `flutter analyze` — **No issues found**; `flutter test` — **19/19** pass.
- Script kiểm chứng HTTP + SignalR (3 tài khoản thật: alice/bob/carol, 1 người ngoài) — **28/28 PASS**:
  - offline ban đầu → online khi kết nối; alice **và** carol (cùng nhóm) đều nhận; danh sách/chi tiết hội thoại trả `isOnline` đúng;
  - `TypingChanged` tới bob (1–1) và bob+carol (nhóm); carol **không** nhận tín hiệu của hội thoại 1–1 mà cô ấy không thuộc; người ngoài gửi `Typing` **không** được phát;
  - `unreadCount` 2 → 0 sau khi đọc; `MessagesRead` tới người gửi real-time (1–1 + nhóm); mốc cũ trả 204 nhưng không phát/không lùi;
  - người ngoài `404 CONVERSATION_NOT_FOUND`; tin của hội thoại khác `404 MESSAGE_NOT_FOUND`;
  - đóng kết nối cuối → offline kèm `lastSeenAt`, danh sách trả đúng.

### 8.3. Chưa làm / việc tiếp theo

1. **Bấm thử trình duyệt + máy thật** theo mục 5 — phiên này không có công cụ điều khiển trình duyệt/thiết bị.
2. **Tài khoản test**: script tạo `alice_<timestamp>`, `bob_<timestamp>`, `carol_<timestamp>@test.local` (mật khẩu `MatKhau@123`) trong DB dev — thuộc KI-13, có thể xoá khi dọn dữ liệu.
3. Cân nhắc chỉnh **KI-09** (bundle web vượt 1 MB) khi làm tối ưu ở GĐ 8.
