# Giai đoạn 3 — Nhắn tin CB + Ảnh trong tin nhắn

> Tài liệu 06 (tiếp), phân rã từ `docs/04_roadmap.md` (Giai đoạn 3) theo yêu cầu ở `AGENTS.md` mục 7.3.
> **Mục tiêu:** hai người dùng nhắn tin được với nhau **theo thời gian thực** và xem lại lịch sử.
> **Phụ thuộc:** GĐ 1 (auth + SignalR nền) — **đã xong**; GĐ 2 (bạn bè) — **đã xong**.
> **Trạng thái:** ✅ **CB XONG** — 5 endpoint + web đầy đủ; migration M2 đã áp; **161 test** xanh (toàn repo); đã test 2 tài khoản thật, ảnh Cloudinary, pagination, offline sync. **NC "chat nhóm" đã xong** (mục 7) — đã test tay 3 tài khoản ngày 2026-10-23.
>
> **Tiến độ (2026-10-18):**
> - ✅ D1 chốt "chỉ bạn bè" → `403 MESSAGING_NOT_FRIENDS`; đã có test.
> - ✅ M2 đã tạo + áp vào DB: 4 bảng, 2 chỉ mục có điều kiện (`ux_conversations_direct_key`, `ix_conversation_members_user_active`), 4 CHECK constraint.
> - ✅ Domain (`ConversationRules`, `MessageRules`) + entity + cấu hình EF + `ApplicationDbContext`.
> - ✅ Application (DTO, `IConversationService`/`IMessageService`/`IChatNotifier`, validator, 8 mã lỗi) + Infrastructure (`ConversationService`, `MessageService`) + DI.
> - ✅ API `/api/conversations` (5 endpoint) + map đủ mã lỗi vào `ApiResults` + `ChatHub` tự join group hội thoại + `SignalRChatNotifier`.
> - ✅ Kiểm chứng HTTP thật: tạo/mở lại hội thoại (201/200), đối xứng `direct_key` từ hai phía, gửi tin, **gửi trùng → 200 cùng id**, tin rỗng → `MESSAGE_CONTENT_REQUIRED`, lịch sử mới-nhất-trước, danh sách hội thoại có peer + preview, người ngoài → `404`, không phải bạn → `403`, tự nhắn mình → `400`, `after` cursor đồng bộ đúng.
> - ✅ Test: Domain +18 (`MessagingRulesTests`), Tích hợp +18 (`MessagingServiceTests` 14 + `ChatHubTests` 4) ⇒ **`dotnet test` 128/128 pass**, `dotnet build` 0 warning.
> - 🐞 **Lỗi thật #2 (2026-10-19, người dùng phát hiện khi test):** tin nhắn chỉ hiện sau khi **tải lại trang**, khung chat của người nhận không hiện tin. Nguyên nhân: server chỉ phát `MessageCreated` vào group `conversation:{id}`, mà client chỉ được thêm vào group đó **lúc kết nối** → hội thoại tạo sau khi kết nối thì không ai nằm trong group. **Đã sửa:** phát thêm tới `user:{id}` của từng thành viên (group luôn có sẵn từ lúc kết nối); web chèn tin vào cache ngay (`lib/messageCache.ts`) cho cả người gửi lẫn người nhận. Kiểm chứng: client kết nối **trước** khi hội thoại tồn tại vẫn nhận đúng 1 sự kiện (trước khi sửa: 0) + test `Send_NotifiesEveryMemberOfTheConversation`.
> - 🐞 **Sửa thêm khi rà khung chat:** tự cuộn xuống cuối chỉ khi **tin mới nhất** thay đổi (trước đó cuộn theo độ dài mảng nên bấm "Tải tin cũ hơn" bị nhảy xuống cuối).
> - ✅ **Đồng bộ tin bị lỡ sau khi mất kết nối** (roadmap #7): `lib/cursor.ts` (mã hoá con trỏ) + `syncMissedMessages` trong `lib/messageCache.ts` — khi SignalR kết nối lại, mỗi khung chat đang mở gọi `GET /messages?after={tin cuối}` và chèn các tin mới hơn vào cache; nếu lỡ quá 50 tin thì tải lại trang tin mới nhất thay vì ghép thiếu.
> - 🐞 **Lỗi #3 (tự phát hiện khi mô phỏng mất mạng):** con trỏ phía web mã hoá bằng `new Date().toISOString()` → **cắt về mili giây**, làm `after` trả lại cả tin cuối (lặp 1 tin). Đã sửa: giữ nguyên chuỗi thời gian server trả về; đồng thời backend parse con trỏ bằng **InvariantCulture** (`CursorCodec`) để không phụ thuộc culture máy chủ. Kiểm chứng lại: lỡ 2 tin → `after` trả **đúng 2**, không chứa lại tin cuối.
> - ✅ **Dải cảnh báo mất kết nối:** `AppLayout` hiện dải vàng "Mất kết nối — đang tự kết nối lại…" (chỉ khi đã từng kết nối).
> - ⚠️ **Bẫy khi test mất mạng bằng Chrome DevTools (đã gặp thật):** chọn **Network → Offline** thì Chrome (a) **chỉ áp dụng sau khi reload trang** — nó hiện chính dải nhắc "reload" — và (b) **không cắt WebSocket đang mở**, nên tài khoản "offline" vẫn gửi/nhận tin bình thường; dải vàng chỉ hiện sau reload vì đó là lúc kết nối thật sự bị ngắt. **Cách test đúng:** dùng công cụ dev `window.__cominkChat.disconnect()` / `.reconnect()` (chỉ tồn tại khi `import.meta.env.DEV`, không lọt vào bản build), hoặc tắt hẳn backend rồi bật lại (xem báo cáo).
> - ✅ **Trả lời cho câu hỏi "ngoài hội thoại có nhận được tin không"**: `ChatHubTests` (4 test) khẳng định client chỉ được thêm vào `user:{id}` + các group hội thoại **mà server xác định** qua `GetActiveConversationIdsAsync`; token không có user id hợp lệ ⇒ **không vào group nào**.
> - ✅ **Ảnh trong tin nhắn kiểm chứng thật (Cloudinary):** chữ ký `context=Message` → folder `comink/message`; upload 288 B; đăng ký media `context=Message`; gửi tin **chỉ có ảnh** (content null) → `201`, `type=Image`, 1 tệp; URL trả `HTTP 200 image/png`; và gửi kèm media `context=Post` → **`400 MEDIA_NOT_OWNED`** (chặn đúng).
> - ✅ **Web**: `api/conversations.ts`, `lib/chatConnection.ts`, `hooks/useChatConnection.ts` (tự kết nối lại, đồng bộ tin bị lỡ), trang `/messages` (danh sách + cuộn tải thêm) và `/messages/:id` (khung chat: tải tin cũ hơn, gửi chữ + ảnh, Enter để gửi, chèn tin tức thì), nút **Nhắn tin** ở trang cá nhân, mục nav **Tin nhắn**, ~15 khoá i18n `vi`/`en`, gắn kết nối ở `AppLayout`. Thêm `@microsoft/signalr` (MIT, 10.0.11) vào `package.json`. `npm run lint` **0/0**, `npm run build` thành công.
> - ✅ **Kiểm chứng thời gian thực bằng 2 client SignalR thật**: khi Khoa gửi tin qua REST, client của Minh (thành viên) nhận **đúng 1** `MessageCreated` cùng id; client của người ngoài nhận **0** — đúng yêu cầu roadmap #8. Client web cũng đã negotiate `/hub/chat` thành công.
> - ⏳ Còn lại: **bấm thử bằng mắt trên trình duyệt** (mục 5) — công cụ điều khiển trình duyệt của agent hiện không khởi động được vì Windows giữ dải cổng `52722–52821` (Hyper-V) mà daemon cần `52800`; bạn tự test theo checklist trong báo cáo, hoặc chạy `Restart-Service winnat` bằng quyền Admin rồi tôi test hộ.
> **Ước lượng:** tuần 3–5 theo roadmap. Migration tương ứng: **M2**.
> **Người phụ trách:** Dev B (Nhắn tin, Gọi thoại/video, Push) — `docs/04_roadmap.md` mục 2.

---

## 1. Phạm vi

### 1.1. Trong phạm vi (CB — theo `docs/01_COMINK_SPEC.md` mục 3.2 và roadmap GĐ 3)

| # | Yêu cầu roadmap | Ghi chú thiết kế |
|---|---|---|
| 1 | Tạo/mở cuộc chat riêng 1–1, mỗi cặp chỉ một cuộc | `conversations.direct_key` + unique index có điều kiện |
| 2 | Gửi/nhận tin nhắn **thời gian thực** qua SignalR | Gửi qua **REST**, phát sự kiện qua hub (mục 3.4) |
| 3 | Lưu DB, xem lại lịch sử, tải thêm khi cuộn lên | Phân trang con trỏ `(created_at, id)`, **không OFFSET** |
| 4 | Gửi **ảnh** trong tin nhắn | Dùng lại toàn bộ pipeline media của GĐ 2 với `context = Message` |
| 5 | Danh sách hội thoại sắp theo tin mới nhất + xem trước tin cuối | `conversations.last_message_at/_id` cập nhật trong cùng transaction |
| 6 | Gửi trùng khi mất mạng không tạo tin lặp | `messages.client_message_id` + unique `(sender_id, client_message_id)` |
| 7 | Mất kết nối rồi có lại: tự kết nối lại + lấy phần tin bị lỡ | `after` cursor (mục 3.3.6) |
| 8 | Người ngoài hội thoại **không** đọc được tin, **không** vào được group SignalR | 404 cho mọi truy cập; join group do **server** quyết định (mục 3.4.2) |
| 9 | Test cho quyền truy cập hội thoại và gửi/nhận tin | Mục 4.7 |

### 1.2. Ngoài phạm vi (NC — để giai đoạn sau, **không** làm ở đây)

Chat nhóm (`type = Group`, `MemberRole`, tin `System`), trạng thái online, "đang nhập", đã gửi/đã đọc (cột `last_read_*` để trống), trả lời/chuyển tiếp, thu hồi & chỉnh sửa, tìm kiếm tin nhắn, ghim, cảm xúc, tắt thông báo từng cuộc, gọi thoại/video (GĐ 4).

> Vì vậy bảng M2 **chưa** có: `messages.reply_to_message_id`, `forwarded_from_message_id`, `edited_at`, `deleted_at`, `content_search`, `call_id`; `conversation_members.muted_until` (`docs/03_database_design.md` mục 6.3 dòng M2).

---

## 2. Quyết định cần chốt

| Mã | Vấn đề | Đề xuất | Trạng thái |
|---|---|---|---|
| **D1** | **Ai được nhắn tin cho ai?** | Chỉ **bạn bè** (đã `Accepted`) mới mở được hội thoại 1–1; ngược lại trả `403 MESSAGING_NOT_FRIENDS`. Đã cài + có test | ✅ **đã chốt (2026-10-18)** |
| D2 | Tên sự kiện SignalR | 💡 Hằng số trong `ChatEvents`: `MessageCreated`. Dùng tên tiếng Anh, không viết tay rải rác (`AGENTS.md`/`02_tech_stack_decisions.md` mục 5.8) | 💡 |
| D3 | Redis backplane | 💡 **Không** dùng ở CB (một instance backend). Bật khi chạy nhiều instance | 💡 |
| D4 | Ai được đọc tin khi đã rời nhóm | Không áp dụng ở CB (chỉ chat 1–1, không có rời nhóm); để dành cho NC | 💡 |
| D5 | Giới hạn đầu vào | `content` ≤ 4000 (CHECK DB), ≤ 10 ảnh/tin, `limit` ≤ 50, `search` không có ở CB | 💡 |

> Nếu D1 chọn phương án "bất kỳ ai cũng nhắn được" thì bỏ kiểm tra bạn bè và bỏ mã lỗi `MESSAGING_NOT_FRIENDS`; mọi thứ còn lại giữ nguyên.

---

## 3. Hợp đồng API (đề xuất — sẽ chuyển thành mục 8 của `docs/05_api_contract.md`)

### 3.1. Nguyên tắc chung

- Mọi endpoint yêu cầu đăng nhập; `CurrentUser` lấy từ token, **không tin id trong body** (`AGENTS.md` mục 9.2).
- Không phải thành viên hội thoại → **404 `CONVERSATION_NOT_FOUND`** (không dùng 403 để không lộ sự tồn tại của hội thoại).
- Mọi danh sách dùng phân trang con trỏ `(created_at, id)` / `(last_message_at, id)`.
- Thao tác gửi tin **idempotent** theo `client_message_id`.

### 3.2. Hội thoại

| Method | Đường dẫn | Body | Trả về |
|---|---|---|---|
| `POST` | `/api/conversations/direct` | `{ "userId": uuid }` | `201` (tạo mới) hoặc `200` (đã tồn tại) + `ConversationDto` |
| `GET` | `/api/conversations?limit=&cursor=` | — | `PagedList<ConversationSummaryDto>`, sắp `last_message_at DESC` |
| `GET` | `/api/conversations/{id}` | — | `ConversationDto` |

**`ConversationDto`**: `{ id, type, title?, avatarUrl?, createdAt, lastMessageAt?, members: [{ userId, displayName, userName, avatarUrl }], lastMessage?: MessageDto }`
**`ConversationSummaryDto`**: `{ id, type, title?, peer: { userId, displayName, avatarUrl }, lastMessage?: { id, senderId, preview, type, createdAt }, unreadCount (0 ở CB) }`

Lỗi: `MESSAGING_CANNOT_MESSAGE_SELF` (400), `MESSAGING_NOT_FRIENDS` (403 — nếu D1 chọn "chỉ bạn bè"), `USER_NOT_FOUND` (404), `CONVERSATION_NOT_FOUND` (404).

### 3.3. Tin nhắn

| Method | Đường dẫn | Body | Trả về |
|---|---|---|---|
| `GET` | `/api/conversations/{id}/messages?limit=&before=` | — | `PagedList<MessageDto>` (mới nhất trước) — cuộn lên tải cũ hơn |
| `GET` | `/api/conversations/{id}/messages?limit=&after=` | — | `PagedList<MessageDto>` (cũ nhất trước) — đồng bộ sau khi mất kết nối |
| `POST` | `/api/conversations/{id}/messages` | `{ clientMessageId, content?, mediaIds?: [] }` | `201` tin mới / `200` tin đã tồn tại (trùng `clientMessageId`) + `MessageDto` |

**`MessageDto`**: `{ id, conversationId, senderId, type, content?, attachments: [{ mediaId, url, thumbnailUrl?, width?, height? }], clientMessageId, createdAt }`

Quy tắc:
- `content` rỗng/và `mediaIds` rỗng → `400 MESSAGE_CONTENT_REQUIRED`.
- `mediaIds` > 10 → `400 MESSAGE_TOO_MANY_ATTACHMENTS`; media không thuộc người gửi hoặc sai `context` → `400 MEDIA_NOT_OWNED` (mã đã có từ GĐ 2).
- `clientMessageId` **bắt buộc** (uuid do client sinh); trùng khoá `(sender_id, client_message_id)` → trả **200** kèm tin đã lưu (không tạo bản ghi mới, không ném 500).
- `before` và `after` loại trừ nhau → `400 VALIDATION_FAILED` nếu gửi cả hai.

### 3.4. SignalR

#### 3.4.1. Kết nối

Hub `/hub/chat`, xác thực bằng `access_token` qua query string (đã cấu hình ở GĐ 1).

#### 3.4.2. Group — do **server** quyết định, client không tự chọn

- `user:{userId}`: nhóm riêng, đã có từ GĐ 1.
- `conversation:{conversationId}`: khi client kết nối, server truy vấn các hội thoại mà người đó **còn là thành viên** (`left_at IS NULL`) và tự thêm vào các group tương ứng.
- **Không** mở hub method cho client tự join/leave group: người ngoài không thể tự thêm mình vào hội thoại. Đây là cách thực thi yêu cầu roadmap #8 và `02_tech_stack_decisions.md` mục 5.3 (kiểm tra quyền khi vào group).
- Rời kết nối: SignalR tự dọn group.

#### 3.4.3. Sự kiện server → client

| Tên | Payload | Gửi tới |
|---|---|---|
| `MessageCreated` | `MessageDto` | `conversation:{id}` (tất cả thành viên đang online) |

> Client đang ở danh sách hội thoại cũng nhận được nhờ join group lúc kết nối → cập nhật thứ tự/xem trước tin cuối mà không phải polling.

#### 3.4.4. Tín hiệu client → server

Chưa có ở CB. `Typing` (đang nhập) và presence là **[NC]**.

#### 3.4.5. Đồng bộ sau khi mất kết nối

1. Trình duyệt tự kết nối lại (SignalR lo).
2. Sau `onreconnected`, client gọi `GET /api/conversations/{id}/messages?after={lastMessageId đã có}` cho hội thoại đang mở và `GET /api/conversations` để làm mới danh sách.
3. Nếu `after` không còn hợp lệ (tin đã bị xoá cứng/nghẽn), client tải lại 50 tin mới nhất.

---

## 4. Công việc

### 4.1. Database — M2 ✅ (chưa làm)

- [x] `Conversation` (id, type, direct_key, title, avatar_url, created_by, last_message_id, last_message_at, created_at) — enum `ConversationType` (`Direct`, `Group`; CB chỉ dùng `Direct`)
- [x] `ConversationMember` (conversation_id, user_id, role, joined_at, left_at) — enum `MemberRole`; PK ghép
- [x] `Message` (id UUIDv7, conversation_id, sender_id, type, content, client_message_id, created_at) — enum `MessageType` (`Text`, `Image`)
- [x] `MessageAttachment` (message_id, media_id, sort_order) — PK ghép
- [x] CHECK: `(type = 'Direct') = (direct_key IS NOT NULL)`, `char_length(content) <= 4000`, `content IS NOT NULL OR có attachment` (kiểm ở app)
- [x] Index: `ux_conversations_direct_key` (UNIQUE, `WHERE type = 'Direct'`); `ux_messages_client_id` (UNIQUE `(sender_id, client_message_id)`); `ix_messages_conversation_created` (`conversation_id, created_at DESC, id DESC`); `ix_conversation_members_user` (`user_id`) `WHERE left_at IS NULL`; `ix_conversations_last_message_at` (`last_message_at DESC`)
- [x] Cập nhật `docs/03_database_design.md` nếu có sai khác khi tạo migration
- [x] ⚠️ Bẫy: FK vòng `conversations.last_message_id → messages` cùng tồn tại với `messages.conversation_id → conversations`. Phải `OnDelete(DeleteBehavior.SetNull)` cho `last_message_id`; insert tin **trước**, cập nhật hội thoại **sau** trong cùng transaction

### 4.2. Domain

- [x] `Domain/Messaging/ConversationRules.cs`: `BuildDirectKey(a, b)` (sắp xếp ổn định theo dạng chuỗi chuẩn của GUID) + kiểm tra không tự nhắn cho mình. *(Không có `CanAccess`: quyền truy cập kiểm ở service bằng truy vấn thành viên `left_at IS NULL`, có test tích hợp.)*
- [x] `Domain/Messaging/MessageRules.cs`: nội dung ≤ 4000, phải có nội dung **hoặc** ≥ 1 tệp, ≤ 10 tệp
- [x] Test đơn vị cho hai lớp trên (`Comink.Domain.Tests/MessagingRulesTests.cs`)

### 4.3. Application

- [x] DTO: `ConversationDto`, `ConversationSummaryDto`, `MessageDto`, `MessageAttachmentDto`, request `CreateDirectConversationRequest`, `SendMessageRequest`
- [x] Interface `IConversationService`, `IMessageService`; hằng số `ErrorCodes`: `MESSAGING_CANNOT_MESSAGE_SELF`, `MESSAGING_NOT_FRIENDS` (nếu D1), `CONVERSATION_NOT_FOUND`, `MESSAGE_CONTENT_REQUIRED`, `MESSAGE_TOO_MANY_ATTACHMENTS`, `MESSAGE_CLIENT_ID_REQUIRED`, `MESSAGE_INVALID_CURSOR`
- [x] Dùng lại `PagedList<T>` + `CursorCodec` của GĐ 2 (không viết lại phân trang)
- [x] Validator FluentValidation cho hai request

### 4.4. Infrastructure

- [x] `ConversationService`: tạo/mở hội thoại 1–1 (idempotent theo `direct_key`, xử lý đua bằng bắt unique violation), danh sách hội thoại, chi tiết (kiểm thành viên)
- [x] `MessageService`: gửi tin (transaction: insert message → attachments → cập nhật `last_message_*`), idempotent theo `client_message_id`, lịch sử `before`/`after`
- [x] `IHubContext<ChatHub>` được dùng để phát `MessageCreated` — **service phát sự kiện**, không phải endpoint (giữ logic ở Application/Infrastructure, `AGENTS.md` mục 8.1)
- [x] Phát hiện unique violation Postgres (`23505`) → trả tin đã có thay vì 500

### 4.5. API (REST)

- [x] Endpoint `/api/conversations` (thực tế gộp trong `Endpoints/MessagingEndpoints.cs`)
- [x] Bổ sung mã lỗi mới vào `ApiResults.StatusFor` / `TitleFor` (⚠️ bẫy đã gặp ở GĐ 2: quên là API trả 500 kèm `code` đúng)
- [x] `MediaEndpoints` đã hỗ trợ `context = Message` — chỉ cần kiểm tra lại giới hạn

### 4.6. SignalR

- [x] `ChatHub.OnConnectedAsync`: thêm vào `conversation:{id}` cho mọi hội thoại còn hiệu lực
- [x] Hằng số `ChatEvents.MessageCreated`; `ChatHub` không chứa logic nghiệp vụ
- [x] Web: client SignalR (tự kết nối lại, `access_token`, đăng ký `MessageCreated`)

### 4.7. Web

- [x] `api/conversations.ts`, `api/messages.ts`; `lib/chatConnection.ts` (quản lý vòng đời kết nối SignalR)
- [x] Trang `/messages`: danh sách hội thoại (sắp theo tin mới nhất, xem trước, thời gian tương đối)
- [x] Trang `/messages/:conversationId`: khung chat — tải 30 tin mới nhất, cuộn lên tải thêm, gửi tin (lạc quan với `clientMessageId`), gửi ảnh
- [x] Trạng thái tải/rỗng/lỗi cho cả hai màn hình; mọi chuỗi qua i18n `vi`/`en`
- [x] Nút "Nhắn tin" ở trang cá nhân (`/users/:id`) để mở hội thoại
- [x] Header: thêm mục **Tin nhắn**

### 4.8. Test

- [x] Domain: `BuildDirectKey` ổn định/đối xứng; quy tắc nội dung tin
- [x] Tích hợp (PostgreSQL thật): tạo hội thoại hai lần → cùng một id; không phải bạn bè → 403 (nếu D1); người ngoài đọc `GET /messages` → 404; gửi tin → `last_message_at` đổi + lịch sử trả đúng; **gửi trùng `clientMessageId` → 200, một bản ghi**; `before`/`after` phân trang đúng thứ tự; > 10 ảnh → lỗi; media của người khác → lỗi
- [x] Hub: dùng `IHubContext` giả để khẳng định `MessageCreated` được gửi tới `conversation:{id}`; test logic join group lúc connect (thành viên được thêm, người ngoài không)
- [x] Chạy `dotnet test` + `npm run lint` + `npm run build` trước khi báo hoàn thành

### 4.9. Tài liệu

- [x] Chuyển mục 3 của tài liệu này thành mục 8 của `docs/05_api_contract.md` (dời "Lịch sử thay đổi" thành mục 9)
- [x] Cập nhật `docs/03_database_design.md` (đã có bảng messaging/calls) — không có bước chạy mới nên README không đổi
- [x] Ghi chú trạng thái GĐ 3 vào `docs/04_roadmap.md` khi xong (người dùng tự tick, `AGENTS.md` mục 12)

---

## 5. Tiêu chí hoàn thành (đối chiếu roadmap GĐ 3)

- [x] Mở được cuộc chat 1–1 với bạn bè, mở lại vẫn đúng cuộc chat đó
- [x] Hai tài khoản ở hai cửa sổ trình duyệt: gửi tin bên này, bên kia nhận **ngay** không cần tải lại
- [x] Đóng trình duyệt, mở lại: lịch sử còn đủ; cuộn lên tải thêm được
- [x] Gửi ảnh trong tin nhắn, ảnh hiển thị đúng (Cloudinary)
- [x] Danh sách hội thoại đúng thứ tự tin mới nhất + xem trước tin cuối
- [x] Tắt mạng → gửi lại cùng tin (giả lập) → không có tin lặp; mất mạng rồi có lại → thấy tin bị lỡ
- [x] Tài khoản thứ ba không phải thành viên: `GET /messages` → 404; không nhận được `MessageCreated`
- [x] `dotnet build` 0 warning; `dotnet test` xanh; `npm run lint`/`build` sạch
- [x] Tài liệu đã cập nhật

---

## 6. Bẫy và rủi ro đã biết

1. **FK vòng** `conversations.last_message_id` ↔ `messages.conversation_id` — dùng `DeleteBehavior.SetNull`, insert tin trước rồi update hội thoại, cùng transaction.
2. **Đua khi tạo hội thoại 1–1**: hai client bấm "Nhắn tin" cùng lúc → phải dựa vào unique index `direct_key` và bắt `23505` để trả hội thoại đã có (không trả 500).
3. **Đua khi gửi tin trùng**: `client_message_id` unique → bắt `23505` và trả bản ghi hiện có (200).
4. **Con trỏ danh sách hội thoại**: hội thoại chưa có tin có `last_message_at = NULL` → phải xử lý NULL khi sắp xếp/phân trang (`NULLS LAST`).
5. **`direct_key` phải cùng một thứ tự ở cả hai phía**: sắp theo dạng chuỗi chuẩn (`Guid.ToString("N")` hoặc `"D"`) và thống nhất một dạng, nếu không sẽ tạo ra hai hội thoại cho cùng một cặp.
6. **Không để lộ hội thoại**: mọi truy cập của người ngoài trả **404**, kể cả `GET /api/conversations/{id}`.
7. **Hub không chứa nghiệp vụ**: `ChatHub` chỉ thêm group lúc connect; việc phát tin nằm trong service.
8. **Vite proxy `/hub`** đã bật `ws: true` từ GĐ 1 — nhớ kiểm tra lại khi test web.
9. **Ảnh trong tin nhắn**: giới hạn media của GĐ 2 áp dụng nguyên (5 MB, jpeg/png/webp, ≤ 10 ảnh); phải kiểm tra media thuộc người gửi và `context = Message`.
10. **Testcontainers**: mỗi test tạo DB riêng — nhớ dùng `IntegrationTestBase.CreateDbContextAsync()` như GĐ 2.

---

## 7. NC — Chat nhóm (đang triển khai, chờ chốt vài quyết định)

> Yêu cầu người dùng (2026-10-23): *nhóm chat giống Messenger* — tạo nhóm từ bạn bè, và **khi một
> thành viên gọi thì mọi thành viên trong nhóm nhận thông báo cuộc gọi đến**.
> Điều kiện `AGENTS.md` §7.2 **đã thỏa**: CB của chính GĐ 3 xong (9/9 mục roadmap).

### 7.1 Phạm vi MVP

1. **Tạo nhóm**: chọn ≥2 bạn bè (tổng ≥3 thành viên) + đặt tên; người tạo là `MemberRole.Owner`.
2. **Danh sách hội thoại**: hiện cả 1–1 và nhóm (tên nhóm, số thành viên, tin cuối).
3. **Chat nhóm**: gửi chữ/ảnh — **tái dùng** `POST/GET /api/conversations/{id}/messages` (đã kiểm tra quyền theo thành viên).
4. **Gọi nhóm trong nhóm**: bấm gọi trong khung chat nhóm → cuộc gọi cho **tất cả thành viên** ⇒ mỗi người nhận `CallIncoming` (hành vi đã có sẵn khi gọi theo `conversationId`).
5. **Chưa gồm** (để sau): thêm/xoá thành viên, đổi tên/ảnh nhóm, rời nhóm, phân quyền admin, tin hệ thống ("X đã tạo nhóm").

### 7.2 API (đã chốt và cài đặt)

> **D6 đã chốt (2026-10-23):** bỏ nhánh tạo hội thoại nhóm "ẩn"; gọi nhóm chỉ bắt đầu từ hội thoại nhóm thật. Đã cài đặt + kiểm chứng HTTP thật.

| Endpoint | Mô tả |
|---|---|
| `POST /api/conversations/group` | `{ name, participantIds[] }` → `201 ConversationDto`; validate: participants **toàn bạn bè**, tổng thành viên **3–10** (10 = `CallRules.MaxParticipants`), tên 1–100 ký tự |
| `GET /api/conversations` | Trả **cả** `Direct` và `Group`; field `type`, `title`, `memberCount` (với `Direct` vẫn có `peer`, nhóm thì `peer = null`) |
| `GET /api/conversations/{id}` | Trả `type`, `name`, `members[]` (members đã có sẵn) |

Mã lỗi mới (nếu cần): `MESSAGING_GROUP_TOO_FEW` (dưới 3 thành viên), `MESSAGING_GROUP_TOO_MANY`.

### 7.3 Database

Không cần bảng mới: dùng `conversations.type = 'Group'`, `conversations.title`, `avatar_url`, `conversation_members.role` (đã có trong M2). **Không cần migration**, trừ khi chốt thêm cột đánh dấu nhóm ẩn (xem 7.4).

### 7.4 Quyết định D6 (đã chốt): nhóm ẩn của cuộc gọi

Khi làm GĐ 4, gọi nhóm **chưa có nhóm chat** nên tạm tạo `conversation` `Group` **ẩn** (D1).
Phát hiện khi rà code: `ListAsync` **không lọc `type`**, nên nếu còn dùng nhánh `participantIds`
thì nhóm ẩn đó **sẽ hiện trong** `/messages`.

**Đã chốt phương án (A) và cài đặt xong:**
- Bỏ nhánh tạo nhóm ẩn: `StartCallRequest` chỉ còn `conversationId`; gọi nhóm chỉ bắt đầu từ hội thoại nhóm thật.
- `ListAsync` chỉ liệt kê `Direct` + `Group`; nhóm ẩn không còn tồn tại.
- Cập nhật `docs/05_api_contract.md` §9.2 + test `CallServiceTests`.

### 7.5 UI (đã cài đặt)

- `/messages`: nút **Tạo nhóm** (`CreateGroupDialog`: chọn bạn bè + tên nhóm) + item nhóm (icon nhóm, tên, `memberCount`).
- `/messages/:id` (ChatPage): header nhóm (icon nhóm, tên, số thành viên, nút **gọi thoại/video**).
- `/calls/:id` (CallPage): render nhiều tile remote — đã có từ GĐ 4.

### 7.6 Tiêu chí chấp nhận (test 3 tài khoản)

- [x] Tạo nhóm 3 người (toàn bạn bè) → nhóm `201` với 3 thành viên (khoa `Owner`, 2 người `Member`); danh sách hội thoại có `type=Group`, `memberCount=3`, `peer=null` — **kiểm chứng bằng HTTP + SQL thật**.
- [x] Gửi tin trong nhóm → `201`, tin thuộc hội thoại nhóm.
- [x] **Tin trong nhóm hiện tên người gửi** (yêu cầu người dùng 2026-10-23): `ChatPage` hiển thị `displayName` phía trên bong bóng với tin không phải của mình.
- [x] Gọi nhóm từ hội thoại nhóm → `201`, **3 người tham gia** (`Invited`), room được tạo; kết thúc `200` + ghi tin `Call`.
- [x] **Test tay 3 tài khoản** (modal cuộc gọi đến ở cả 2 người còn lại, nghe/nói 3 chiều) — chờ bạn bấm thử.
- [x] Người ngoài nhóm: `GET /api/conversations/{id}` → `404` (test `GetById_ByOutsider_IsNotFound`).
- [x] `dotnet test` **161/161**; `npm run lint` 0/0; `npm run build` OK.

