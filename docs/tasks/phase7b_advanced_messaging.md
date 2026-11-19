# Giai đoạn 7 — Nâng cao: Ưu tiên 2 (Nhắn tin)

> Tài liệu phân rã từ `docs/04_roadmap.md` (Giai đoạn 7, bảng ưu tiên 2) theo yêu cầu ở `AGENTS.md` mục 7.3.
> **Mục tiêu:** hoàn thiện 8 tính năng **Nhắn tin nâng cao (NC)**: phản hồi, thu hồi, chỉnh sửa, thả cảm xúc,
> ghim, chuyển tiếp, tìm kiếm tin nhắn, tắt thông báo từng cuộc.
> **Phụ thuộc:** GĐ 3 (nhắn tin CB + chat nhóm) và GĐ 7 ưu tiên 1 (presence/đang nhập/đã đọc) — **đã xong**.
> **Trạng thái:** 🚧 **Đợt A đã xong (2026-11-20)** — backend + web cho phản hồi, chỉnh sửa, thu hồi, cảm xúc; Đợt B (ghim, chuyển tiếp, tìm kiếm, tắt thông báo) làm tiếp. Xem mục 8.
> **Ước lượng:** 2–3 tuần công. Migration tương ứng: **M7** (`Messaging_AddMessageFeatures`).

---

## 1. Phạm vi

### 1.1. Trong phạm vi (đúng bảng ưu tiên 2 của roadmap)

| # | Yêu cầu roadmap | Hoàn thành khi | Trạng thái |
|---|---|---|---|
| 1 | **Phản hồi** | Chọn "Phản hồi" trên một tin → ô nhập hiện trích dẫn → gửi kèm `replyToMessageId`; bong bóng hiện trích dẫn; bấm trích dẫn nhảy tới tin gốc; tin gốc bị thu hồi vẫn hiển thị an toàn | ✅ backend + web (Đợt A) |
| 2 | **Thu hồi** | Người gửi thu hồi trong 24 giờ → mọi người thấy "Tin nhắn đã bị thu hồi" real-time, không tải lại; preview danh sách hội thoại đổi theo | ✅ backend + web (Đợt A) |
| 3 | **Chỉnh sửa** | Người gửi sửa nội dung trong 24 giờ → mọi người thấy nội dung mới + nhãn "(đã chỉnh sửa)" real-time | ✅ backend + web (Đợt A) |
| 4 | **Thả cảm xúc** | 6 cảm xúc cố định; một người được thả nhiều loại trên một tin; đếm và trạng thái "của tôi" đúng; real-time | ✅ backend + web (Đợt A) |
| 5 | **Ghim tin** | Ghim/bỏ ghim tin còn hiệu lực, tối đa 10/hội thoại; panel ghim; bấm để nhảy tới tin; thu hồi tin đã ghim → tự bỏ ghim | ❌ chưa có |
| 6 | **Chuyển tiếp** | Chọn hội thoại đích → tạo tin mới có nhãn "Chuyển tiếp", giữ `forwarded_from_message_id`; không chuyển tiếp tin đã thu hồi | ❌ chưa có |
| 7 | **Tìm kiếm tin nhắn** | Tìm trong hội thoại đang mở **và** toàn bộ hội thoại tôi là thành viên; gõ không dấu vẫn khớp; bấm kết quả mở đúng tin trong ngữ cảnh | ❌ chưa có |
| 8 | **Tắt thông báo từng cuộc** | Tắt theo 1 giờ/8 giờ/1 tuần/vĩnh viễn và bật lại; **chặn push**; số tin chưa đọc không đổi | ❌ chưa có |

> Điều kiện chung của roadmap: *"Từng tính năng chạy trên web **và** app"*, *"Tìm kiếm chỉ trong hội thoại người dùng là thành viên"*.

### 1.2. Ngoài phạm vi

- Ẩn/hiện trạng thái online, đọc theo cài đặt riêng tư → Ưu tiên 4 (Bảo mật).
- Xoá cứng tệp trên cloud khi thu hồi tin có ảnh → Ưu tiên 6 (Hình ảnh và tệp); ở đây chỉ ẩn tệp khỏi API.
- Lịch sử/chuyển tiếp ra ngoài Comink (email, mã chia sẻ) — không có trong tài liệu.
- Sửa/thu hồi tin hệ thống `System` và tin cuộc gọi `Call` — giữ nguyên bất biến.

---

## 2. Quyết định thiết kế (đã chốt với người dùng 2026-11-20)

| Mã | Vấn đề | Quyết định | Lý do / đánh đổi |
|---|---|---|---|
| **D1** | Thời hạn sửa/thu hồi | **24 giờ** kể từ `created_at`; chỉ **người gửi**; tin `System`/`Call` không sửa; quá hạn trả `MESSAGE_EDIT_WINDOW_EXPIRED` / `MESSAGE_RECALL_WINDOW_EXPIRED` (403) | Thực tế, đủ thời gian cho kịch bản demo; kiểm thử biên 24h dễ. Đổi lại: không sửa được tin cũ khi demo dữ liệu mẫu (chấp nhận, demo dùng tin mới) |
| **D2** | Cảm xúc: một hay nhiều loại/người/tin | **Nhiều loại**, đúng khóa chính `(message_id, user_id, emoji)` của `docs/03` mục 7 #1; **danh mục cố định 6 khoá** `like/love/haha/wow/sad/angry`; API dùng **khoá chữ**, server quy đổi ra emoji lưu ở cột `emoji` | Tránh lỗi URL-encode emoji và lệch variation selector (❤ vs ❤️) giữa web/app; DB vẫn đúng thiết kế. Đổi lại: thêm một ánh xạ khoá→emoji trong backend lẫn client (client hiển thị emoji server trả) |
| **D3** | Ghim: ai được ghim, bao nhiêu | **Mọi thành viên còn hiệu lực**; tối đa **10** tin/hội thoại (đúng `docs/03` mục 7 #4, kiểm tra ở Application); ghim tin đã thu hồi → `MESSAGE_ALREADY_DELETED`; thu hồi tin đã ghim → **tự bỏ ghim** + phát `PinnedMessagesChanged` | Demo đơn giản, không cần ma trận quyền nhóm; tinh chỉnh quyền nhóm (chỉ Owner/Admin) để Ưu tiên 7 (Quản trị) nếu cần |
| **D4** | Chuyển tiếp xử lý ở đâu | **Server** copy nội dung + tệp sang tin mới, đặt `forwarded_from_message_id`; một hội thoại đích mỗi lần gọi; `clientMessageId` do client sinh (idempotent); nguồn phải còn hiệu lực và người gọi phải là thành viên **cả** hội thoại nguồn lẫn đích | Không cho client giả mạo nhãn "chuyển tiếp"; ảnh không phải tải lại. Đổi lại: thêm endpoint và một lượt đọc tin nguồn |
| **D5** | Kỹ thuật tìm kiếm | **`pg_trgm` + `immutable_unaccent`** (đã có từ M0), không dùng `content_search tsvector`; tối thiểu **2 ký tự**; sắp `created_at DESC, id DESC`; con trỏ | Tìm được **một phần từ** tiếng Việt ("họp" trong "cuộc họp"), dùng lại kỹ thuật đã kiểm chứng ở tìm người dùng; cập nhật `docs/02` mục 3.2 và `docs/03` cho khớp |
| **D6** | Phạm vi tìm kiếm + nhảy tới tin | Hai endpoint: **trong hội thoại** và **toàn bộ hội thoại của tôi** (join `conversation_members`); thêm endpoint **context** (~15 tin trước + 15 sau) để mở đúng tin từ kết quả tìm kiếm | Đúng yêu cầu "chỉ trong hội thoại người dùng là thành viên"; context tránh phải tải ngược nhiều trang, dễ kiểm thử |
| **D7** | Tắt thông báo | Preset `Off/OneHour/EightHours/OneWeek/Forever`; lưu `conversation_members.muted_until`; "vĩnh viễn" = mốc **`9999-12-31T23:59:59Z`** (không dùng `'infinity'` vì EF/Npgsql không đọc được vào `DateTimeOffset`); **chỉ chặn push**, số chưa đọc và real-time giữ nguyên | Hành vi quen thuộc, ít bất ngờ. Đổi lại: tài liệu `docs/03` phải ghi rõ mốc vĩnh viễn thay cho `'infinity'` |
| **D8** | Sự kiện real-time | Một event duy nhất **`MessageUpdated`** (payload `MessageDto`) cho sửa/thu hồi/cảm xúc; event **`PinnedMessagesChanged`** (payload `{ conversationId, pinned[] }`) cho ghim/bỏ ghim. Phát theo group hội thoại **và** `user:{id}` từng thành viên như `MessageCreated` | Một hàm "upsert tin theo id" ở client, giảm nhánh cache sai. Đổi lại: thả cảm xúc phát cả tin (vài trăm byte) — chấp nhận ở quy mô đồ án |
| **D9** | Trích dẫn/forward trong DTO | `MessageDto` thêm `replyTo` (ảnh chụp tối thiểu: id, người gửi, preview, loại, `isDeleted`), `forwardedFrom` (id, người gửi gốc), `editedAt`, `deletedAt`, `reactions[]` | Client không phải tự nối dữ liệu; tránh N+1 bằng cách nạp theo lô 2 truy vấn phụ mỗi trang |
| **D10** | Push khi có tương tác mới | Sửa/thu hồi/cảm xúc/ghim **không** tạo push; chỉ tin mới (kể cả tin chuyển tiếp) tạo push, và push bị lọc theo `muted_until` | Tránh spam thông báo; mute có tác dụng thật |

---

## 3. Hợp đồng API (tóm tắt — chi tiết ở `docs/05_api_contract.md` mục 8.12–8.18)

### 3.1. Endpoint mới/ mở rộng

| Method | Đường dẫn | Body | Trả về |
|---|---|---|---|
| `POST` | `/api/conversations/{id}/messages` | **thêm** `replyToMessageId` (uuid?) | `201/200 MessageDto` |
| `PATCH` | `/api/conversations/{id}/messages/{messageId}` | `{ content }` | `200 MessageDto` |
| `DELETE` | `/api/conversations/{id}/messages/{messageId}` | — | `200 MessageDto` đã thu hồi (soft delete, idempotent) |
| `POST` | `/api/conversations/{id}/messages/{messageId}/reactions` | `{ reaction }` | `200 MessageDto` |
| `DELETE` | `/api/conversations/{id}/messages/{messageId}/reactions/{reaction}` | — | `200 MessageDto` |
| `GET` | `/api/conversations/{id}/pinned` | — | `PinnedMessageDto[]` (≤10) |
| `PUT` | `/api/conversations/{id}/pinned/{messageId}` | — | `204` |
| `DELETE` | `/api/conversations/{id}/pinned/{messageId}` | — | `204` |
| `POST` | `/api/conversations/{targetId}/messages/forward` | `{ sourceMessageId, clientMessageId }` | `201 MessageDto` (200 nếu trùng) |
| `GET` | `/api/conversations/{id}/messages/search?q=&limit=&cursor=` | — | `PagedList<MessageSearchResultDto>` |
| `GET` | `/api/messages/search?q=&limit=&cursor=` | — | `PagedList<MessageSearchResultDto>` |
| `GET` | `/api/conversations/{id}/messages/{messageId}/context?limit=` | — | `MessageContextDto` |
| `PUT` | `/api/conversations/{id}/mute` | `{ duration }` | `200 { mutedUntil }` |

**Thứ tự đoạn đường dẫn quan trọng:** `/messages/search` và `/messages/{messageId}/context` phải khai báo **trước** `/messages/{messageId}` (nếu có route trùng) — dùng tham số ràng buộc kiểu (`{messageId:guid}`) để tránh nhầm.

### 3.2. DTO

- `MessageDto` += `replyTo: MessageQuoteDto?`, `forwardedFrom: ForwardedFromDto?`, `editedAt: string?`, `deletedAt: string?`, `reactions: ReactionSummaryDto[]`.
- `MessageQuoteDto { messageId, senderId, senderDisplayName, contentPreview, type, isDeleted }`.
- `ForwardedFromDto { messageId, senderId, senderDisplayName }`.
- `ReactionSummaryDto { reaction, emoji, count, reactedByMe }`.
- `PinnedMessageDto { message: MessageDto, pinnedByUserId, pinnedByDisplayName, pinnedAt }`.
- `MessageSearchResultDto { messageId, conversationId, conversationTitle, conversationType, peerDisplayName?, senderId, senderDisplayName, contentPreview, createdAt }`.
- `MessageContextDto { items: MessageDto[], hasMoreBefore, hasMoreAfter }`.
- `ConversationSummaryDto`/`ConversationDto` += `isMuted`, `mutedUntil`; `ConversationLastMessageDto` += `isDeleted`.

### 3.3. SignalR — hub `/hub/chat` (không thêm method client → server)

| Hướng | Tên | Payload | Gửi cho |
|---|---|---|---|
| Server → client | `MessageUpdated` | `MessageDto` | group `conversation:{id}` + `user:{id}` từng thành viên còn hiệu lực |
| Server → client | `PinnedMessagesChanged` | `{ conversationId, pinned: PinnedMessageDto[] }` | như trên |

### 3.4. Mã lỗi mới

| Mã | HTTP | Ý nghĩa |
|---|---|---|
| `MESSAGE_NOT_OWNED` | 403 | Không phải người gửi nên không sửa/thu hồi được |
| `MESSAGE_EDIT_WINDOW_EXPIRED` | 403 | Quá 24 giờ kể từ khi gửi |
| `MESSAGE_RECALL_WINDOW_EXPIRED` | 403 | Quá 24 giờ kể từ khi gửi |
| `MESSAGE_ALREADY_DELETED` | 400 | Tin đã bị thu hồi (không sửa/react/ghim/forward được) |
| `MESSAGE_NOT_EDITABLE` | 400 | Tin `System`/`Call` không sửa được |
| `MESSAGE_PIN_LIMIT_REACHED` | 400 | Đã ghim đủ 10 tin |
| `MESSAGE_REACTION_INVALID` | 400 | Khoá cảm xúc không thuộc danh mục |

---

## 4. Phân rã công việc

### 4.1. Đợt A — Nền schema + phản hồi, sửa, thu hồi, cảm xúc (backend + web) — ✅ 2026-11-20

- [x] Tài liệu: mục 8.12–8.17 trong `docs/05`, mục 3.3/5.2/6.3/7 trong `docs/03`, `docs/02` mục 3.2 (D5)
- [x] Domain: `MessageActionRules` (cửa sổ 24h, loại tin được sửa, giới hạn ghim), `ReactionCatalog`, `MuteRules` + test biên
- [x] Migration **M7** `Messaging_AddMessageFeatures`: cột `messages.*`, `message_reactions`, `pinned_messages`, `conversation_members.muted_until`, GIN trigram trên `messages.content`
- [x] Entity + cấu hình EF (2 FK tự thân `SET NULL`, reaction, pinned, mute)
- [x] Application: DTO mới, `IMessageService` mở rộng, `IChatNotifier` thêm `MessageUpdated`/`PinnedMessagesChanged`, mã lỗi
- [x] Infrastructure: `MessageEnrichment` (nạp theo lô trích dẫn/cảm xúc, không N+1); `EditAsync`, `RecallAsync`, `AddReactionAsync`, `RemoveReactionAsync`; phát `MessageUpdated`
- [x] Api: endpoint + validator; `SignalRChatNotifier` phát event mới
- [x] Test tích hợp: quyền, cửa sổ 24h đúng biên, idempotent reaction, thu hồi xoá nội dung/tệp/reaction và cập nhật preview, reply tới tin bị thu hồi (`MessageActionsTests` 22 test)
- [x] Web: `api/conversations.ts`, `MessageBubble` (menu, trích dẫn, chip cảm xúc, nhãn đã sửa/đã thu hồi), thanh trả lời/sửa trên ô nhập, cache real-time, i18n vi/en
- [x] Kiểm chứng: `dotnet build` 0 warning / `dotnet test` **289/289**, `npm run lint` 0/0 / `build` sạch, script HTTP + 2 client SignalR **23/23**, Chrome headless CDP **21/21**

### 4.2. Đợt B — Ghim, chuyển tiếp, tìm kiếm, tắt thông báo (backend + web)

- [ ] Application/Infrastructure: `IPinnedMessageService`, `ForwardAsync`, `SearchAsync` (trong hội thoại + toàn bộ), `GetContextAsync`, `MuteAsync`
- [ ] Api + validator; chặn push theo `muted_until`; `ConversationDto`/summary có `isMuted`/`mutedUntil`
- [ ] Web: panel ghim, dialog chuyển tiếp, panel/trang tìm kiếm + nhảy tới tin, menu tắt thông báo ở header; i18n
- [ ] Test tích hợp: giới hạn 10, thu hồi tin đã ghim, quyền forward nguồn/đích, search chỉ trả hội thoại của tôi, context đúng cửa sổ, mute chặn push
- [ ] Kiểm chứng + tái sinh `docs/openapi/comink.v1.json` + `verify.ps1`

### 4.3. Đợt C — App Flutter + hoàn thiện

- [ ] Model/API/realtime: quote, reaction, edited/deleted, pinned, search, mute
- [ ] `chat_page.dart`: menu dài (bottom sheet), thanh trả lời/sửa, chip cảm xúc, panel ghim, màn hình tìm kiếm, sheet tắt thông báo
- [ ] i18n vi/en; `flutter analyze` 0 issue; `flutter test` xanh
- [ ] Kiểm chứng web + app trên cùng hội thoại; rà lại tài liệu; tái sinh OpenAPI lần cuối; ghi kết quả vào mục 8

---

## 5. Kịch bản kiểm chứng thủ công

1. **Phản hồi:** A mở 1–1 với B, phản hồi một tin của B → B thấy trích dẫn real-time; bấm trích dẫn nhảy đúng tin; B thu hồi tin gốc → trích dẫn của A hiện "đã thu hồi", không lỗi.
2. **Chỉnh sửa:** A sửa tin trong 24h → B thấy nội dung mới + "(đã chỉnh sửa)"; B không có menu sửa trên tin của A.
3. **Thu hồi:** A thu hồi tin vừa gửi → cả A và B thấy "Tin nhắn đã bị thu hồi"; danh sách hội thoại của B đổi preview; tin đó không react/ghim/reply được.
4. **Cảm xúc:** A thả ❤️ và 😂 lên cùng một tin; B thả ❤️ → A thấy `❤️ 2`, B thấy chip của mình được đánh dấu; bấm lại để bỏ.
5. **Ghim:** ghim 10 tin thành công, tin thứ 11 báo lỗi; panel ghim mở đúng; thu hồi một tin đã ghim → panel tự cập nhật ở cả hai phía.
6. **Chuyển tiếp:** A chuyển tiếp tin có ảnh sang nhóm → nhóm thấy tin mới có nhãn "Chuyển tiếp" và ảnh; người ngoài hội thoại nguồn không chuyển tiếp được (`404`).
7. **Tìm kiếm:** B gõ không dấu ("hop") tìm được tin có "họp"; kết quả chỉ gồm hội thoại B là thành viên; bấm kết quả mở đúng tin trong ngữ cảnh; người ngoài tìm trong hội thoại người khác → `404`.
8. **Tắt thông báo:** A tắt thông báo hội thoại 1 giờ; B gửi tin → A **không** nhận push nhưng vẫn thấy tin real-time + badge chưa đọc; bật lại → push trở lại.

---

## 6. Rủi ro và ghi chú

| Rủi ro | Cách xử lý |
|---|---|
| Thu hồi tin đã ghim | Trong cùng transaction: xoá `pinned_messages`, reaction; phát `MessageUpdated` + `PinnedMessagesChanged` |
| Reply/forward tới tin đã thu hồi | Trả `MESSAGE_ALREADY_DELETED` khi tạo mới; khi **hiển thị** trích dẫn cũ dùng `isDeleted` để client hiện "đã thu hồi" |
| N+1 khi nạp trích dẫn/cảm xúc | 1 truy vấn trang + 1 truy vấn trích dẫn/forward + 1 truy vấn cảm xúc theo lô |
| Npgsql không dịch được `immutable_unaccent` trên hằng số | Kiểm tra ngay bằng test tích hợp; nếu lỗi, truyền pattern **đã bỏ dấu** tính trong SQL bằng tham số (`SELECT immutable_unaccent(@term)`) hoặc dùng truy vấn có tham số an toàn |
| `muted_until` vĩnh viễn | Mốc `9999-12-31` thay cho `'infinity'` (D7); ghi rõ trong `docs/03` |
| Client cũ chưa biết trường mới | Chỉ **thêm** trường DTO, không đổi tên/kiểu trường cũ; web/app tự cập nhật cùng đợt |
| App chưa theo kịp web | Đợt C làm sau; trong lúc chờ, app vẫn chạy bình thường vì API chỉ thêm trường/endpoint |
| Thời gian app Flutter | Nếu trễ, web đã đủ 8 tính năng; app làm tiếp theo đúng thứ tự roadmap (web trước, app sau) |

---

## 7. Liên kết

- Quy chuẩn chung: `docs/02_tech_stack_decisions.md` mục 3.2 (nhắn tin), 5.2 (thời gian thực).
- Schema: `docs/03_database_design.md` mục 3.3, 4, 5.2, 6.3 (M7), 7 (#1, #4, #7).
- Hợp đồng: `docs/05_api_contract.md` mục 8.12–8.18.
- Roadmap: `docs/04_roadmap.md` Giai đoạn 7, bảng ưu tiên 2.
- Ưu tiên 1 trước đó: `docs/tasks/phase7_advanced_messaging.md`.
- Bài học GĐ 3: `docs/tasks/phase3_messaging.md` mục 2 và 7 (phát sự kiện qua `user:{id}`).

---

## 8. Kết quả triển khai

### 8.1. Đợt A (2026-11-20)

**Tệp đã thay đổi**

| Nhóm | Tệp |
|---|---|
| Domain | `MessageActionRules.cs`, `ReactionCatalog.cs`, `MuteRules.cs` |
| Application | `Dtos/MessagingDtos.cs`, `Abstractions/IConversationService.cs`, `Validators/MessagingValidators.cs`, `ChatEvents.cs`, `Common/ErrorCodes.cs` |
| Infrastructure | `Persistence/Entities/Message.cs`, `MessageReaction.cs` (mới), `PinnedMessage.cs` (mới), `ConversationMember.cs`, `Persistence/Configurations/Messaging/MessagingConfigurations.cs`, `Persistence/ApplicationDbContext.cs`, `Messaging/MessageService.cs`, `Messaging/MessagingMapping.cs`, `Messaging/MessageEnrichment.cs` (mới), `Messaging/PinnedMessagesLoader.cs` (mới), `Messaging/ConversationService.cs`, migration `20261119223004_Messaging_AddMessageFeatures` |
| Api | `Endpoints/MessagingEndpoints.cs`, `Realtime/SignalRChatNotifier.cs`, `Common/ApiResults.cs` |
| Web | `api/conversations.ts`, `lib/chatConnection.ts`, `lib/messageCache.ts`, `lib/reactions.ts` (mới), `hooks/useChatConnection.ts`, `components/chat/MessageBubble.tsx` (mới), `pages/ChatPage.tsx`, `pages/MessagesPage.tsx`, `locales/vi.json`, `locales/en.json` |
| Test | `tests/Comink.Domain.Tests/MessageActionRulesTests.cs` (mới), `tests/Comink.Api.IntegrationTests/MessageActionsTests.cs` (mới), `MessagingFakes.cs`, `IntegrationTestBase.cs` (nâng `max_connections` cho container test) |
| Tài liệu | `docs/03`, `docs/05`, `docs/02`, tệp này |

**Kết quả chạy**

- `dotnet build` — 0 warning, 0 error.
- `dotnet test` — **289/289** pass (Domain 115 · Application 25 · Tích hợp 149), gồm 22 test mới cho phản hồi/sửa/thu hồi/cảm xúc.
- Migration M7 đã áp lên DB dev (`dotnet ef database update`).
- `npm run lint` — 0 warning, 0 error; `npm run build` — thành công (chỉ còn cảnh báo bundle > 1 MB: KI-09).
- Script HTTP + **2 client SignalR thật** (3 tài khoản) — **23/23 PASS**: trích dẫn đúng khi gửi/nhận, sửa real-time, chặn người khác sửa (`403`), sửa rỗng bị chặn, cảm xúc nhiều loại/người + idempotent + huỷ đúng người, khoá cảm xúc lạ bị chặn, người ngoài `404`, thu hồi xoá nội dung/cảm xúc và idempotent, trích dẫn cũ chuyển `isDeleted`, preview danh sách hội thoại đổi theo.
- **Chrome headless + CDP** — **21/21 PASS** (tiếng Anh): nhận tin real-time không tải lại; phản hồi qua menu → trích dẫn đúng; sửa tại chỗ + nhãn "edited"; thả 👍 + chip cảm xúc; thu hồi → khung "Message recalled"; không exception runtime.

**Ghi chú kỹ thuật**

- Cửa sổ 24 giờ so sánh ở server theo `now - created_at > 24h` (đúng biên được phép) — có test.
- Tin bị thu hồi vẫn giữ dòng + tệp trong DB (ẩn ở API) để hiện đúng khung; xoá tệp trên cloud thuộc Ưu tiên 6.
- Container test Postgres được nâng `max_connections=400` vì cả bộ test tạo ~150 database riêng; trước đây chạm giới hạn 100 khi số test tăng.
- Hai script kiểm chứng (`web/verify-a.mjs`, `web/verify-a-ui.mjs`) chỉ dùng tại chỗ, **không commit**.
