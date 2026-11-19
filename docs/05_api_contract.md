# Comink — Hợp đồng API (API Contract)

> Tài liệu 05. Theo `docs/02_tech_stack_decisions.md` mục 4.1: **hợp đồng API chốt trước, code sau**.
> Phạm vi tài liệu: endpoint, DTO, mã lỗi, sự kiện SignalR dùng chung giữa backend, web và app.
> Trạng thái: **Giai đoạn 1 đã chốt** (2026-10-15). Bổ sung dần theo từng giai đoạn.

---

## 1. Quy ước chung

| Hạng mục | Quy ước |
|---|---|
| Tiền tố | `/api/...` |
| Định dạng | JSON, `camelCase` |
| Thời gian | chuỗi ISO-8601 UTC (`2026-10-15T10:00:00Z`) |
| Xác thực | `Authorization: Bearer <access_token>` |
| Phân trang | con trỏ `(created_at, id)`: tham số `cursor` + `limit` (mặc định 20, tối đa 50). **Không dùng offset** |
| Lỗi | RFC 7807 ProblemDetails, có thêm trường `code` (mã lỗi) và `traceId` |
| Ngôn ngữ | Backend **không** trả câu chữ hiển thị; client dịch từ `code` (`AGENTS.md` mục 3) |

> **Bản máy đọc (Mốc A):** `docs/openapi/comink.v1.json` — OpenAPI 3.1, **45 path**, có security scheme
> `Bearer` (JWT); endpoint công khai không gắn security. Cách tái sinh: `docs/openapi/README.md`.
> Khi đổi endpoint/DTO, cập nhật tài liệu này trước rồi mới tái sinh file OpenAPI.

### 1.1. Định dạng lỗi

```json
{
  "type": "https://comink.local/errors/AUTH_INVALID_CREDENTIALS",
  "title": "Invalid credentials",
  "status": 401,
  "code": "AUTH_INVALID_CREDENTIALS",
  "traceId": "00-...",
  "errors": { "email": ["..."] }
}
```

`errors` chỉ có khi lỗi validate (`400`).

Lỗi do framework sinh (chưa xác thực, thiếu quyền, không tìm thấy…) cũng được bổ sung trường `code` theo bảng ở mục 5, nên client luôn có mã để dịch thay vì phải đoán theo HTTP status:

| Mã HTTP | `code` |
|---|---|
| 400 | `VALIDATION_FAILED` |
| 401 | `UNAUTHORIZED` |
| 403 | `FORBIDDEN` |
| 404 | `NOT_FOUND` |
| 500 | `INTERNAL_ERROR` |

### 1.2. Cấu trúc phân trang

```json
{
  "items": [ ... ],
  "nextCursor": "MjAyNi0xMC0xNVQxMDowMDowMFo|3f1c...",
  "hasMore": true
}
```

---

## 2. Xác thực — `/api/auth`

### `POST /api/auth/register` — đăng ký

Không cần xác thực.

Request:

```json
{
  "userName": "khoa.nguyen",
  "email": "khoa@example.com",
  "password": "MatKhau@123",
  "displayName": "Nguyễn Khoa"
}
```

Ràng buộc (kiểm tra ở backend bằng FluentValidation):

| Trường | Ràng buộc |
|---|---|
| `userName` | 3–32 ký tự; chỉ `a–z A–Z 0–9 . _ - @` |
| `email` | hợp lệ, tối đa 256 ký tự |
| `password` | ≥ 8 ký tự, có chữ hoa, chữ thường, số và ký tự đặc biệt (khớp cấu hình Identity) |
| `displayName` | 1–64 ký tự |

Phản hồi `201 Created`: `AuthResponse` (xem 2.5).

Lỗi: `VALIDATION_FAILED` (400), `AUTH_EMAIL_ALREADY_EXISTS` (409), `AUTH_USERNAME_ALREADY_EXISTS` (409).

### `POST /api/auth/login` — đăng nhập

Request (cho phép đăng nhập bằng **email hoặc `userName`**):

```json
{
  "emailOrUserName": "khoa@example.com",
  "password": "MatKhau@123",
  "deviceName": "Chrome trên Windows"
}
```

Phản hồi `200 OK`: `AuthResponse`.

Lỗi: `AUTH_INVALID_CREDENTIALS` (401 — dùng chung cho sai tài khoản **và** sai mật khẩu, không tiết lộ tài khoản có tồn tại), `AUTH_ACCOUNT_LOCKED` (403), `VALIDATION_FAILED` (400).

### `POST /api/auth/refresh` — làm mới token

Request:

```json
{ "refreshToken": "<chuỗi>" }
```

Phản hồi `200 OK`: `AuthResponse` (refresh token **mới**, token cũ bị thu hồi).

Lỗi: `AUTH_REFRESH_TOKEN_INVALID` (401 — không tồn tại/hết hạn/đã thu hồi), `AUTH_REFRESH_TOKEN_REUSED` (401 — token thuộc một họ đã bị thu hồi; **thu hồi toàn bộ họ**).

> Áp dụng `family_id`: mọi token xoay vòng từ một lần đăng nhập chung một họ. Phát hiện dùng lại token cũ ⇒ thu hồi cả họ (coi như bị đánh cắp) — xem `docs/03_database_design.md` mục 3.1.

### `POST /api/auth/logout` — đăng xuất

Cần xác thực. Request:

```json
{ "refreshToken": "<chuỗi>" }
```

Phản hồi `204 No Content`. Thu hồi đúng refresh token đó. Gọi lại lần nữa vẫn trả `204` (idempotent).

### 2.5. `AuthResponse`

```json
{
  "accessToken": "<jwt>",
  "accessTokenExpiresAt": "2026-10-15T10:15:00Z",
  "refreshToken": "<chuỗi>",
  "refreshTokenExpiresAt": "2026-11-14T10:00:00Z",
  "user": {
    "id": "0199...",
    "userName": "khoa.nguyen",
    "email": "khoa@example.com",
    "displayName": "Nguyễn Khoa",
    "roles": ["User"]
  }
}
```

> Client lưu `refreshToken` ở nơi an toàn (mobile: `flutter_secure_storage`). Access token ngắn hạn (mặc định 15 phút).

---

## 3. Hồ sơ — `/api/users`

### `GET /api/users/me` — hồ sơ của chính mình

Cần xác thực. Phản hồi `200 OK`:

```json
{
  "id": "0199...",
  "userName": "khoa.nguyen",
  "email": "khoa@example.com",
  "displayName": "Nguyễn Khoa",
  "bio": "Sinh viên CNTT",
  "avatarUrl": null,
  "coverUrl": null,
  "location": null,
  "dateOfBirth": null,
  "followerCount": 0,
  "followingCount": 0,
  "friendCount": 0,
  "language": "vi",
  "roles": ["User"],
  "createdAt": "2026-10-15T10:00:00Z"
}
```

### `PATCH /api/users/me/profile` — sửa hồ sơ

Cần xác thực. Chỉ gửi trường muốn đổi (bỏ trống = giữ nguyên):

```json
{ "displayName": "Khoa Nguyễn", "bio": "Yêu thích C#", "location": "Hà Nội", "dateOfBirth": "2004-05-01" }
```

Ràng buộc: `displayName` 1–64; `bio` ≤ 300; `location` ≤ 100; `dateOfBirth` không ở tương lai.

Phản hồi `200 OK`: cùng cấu trúc `GET /api/users/me`.

### `PATCH /api/users/me/settings` — sửa cài đặt (chỉ `language` ở GĐ 1)

```json
{ "language": "en" }
```

Ràng buộc: `language` ∈ {`vi`, `en`}. Phản hồi `200 OK`: như `GET /api/users/me`.

---

## 4. Quản trị — `/api/admin`

Yêu cầu role `Admin` (trả `403 FORBIDDEN` nếu không đủ quyền).

> **Cập nhật Giai đoạn 5 (2026-10-24):** bổ sung khóa/mở khóa tài khoản, hai trường
> `lockedReason`/`lockedAt` trong danh sách, và sự kiện SignalR `AccountLocked`.
> Migration **M5** (`Admin_AddLockedByForeignKey`) thêm FK `users.locked_by → users(id)`
> `ON DELETE SET NULL` + chỉ mục — **không đổi hình dạng API**.

### 4.1. `GET /api/admin/users` — danh sách tài khoản

Tham số: `cursor`, `limit`, `search` (tùy chọn, tìm theo email/user_name/display_name).

Phản hồi `200 OK`: phân trang `AdminUserSummary`:

```json
{
  "id": "0199...",
  "userName": "khoa.nguyen",
  "email": "khoa@example.com",
  "displayName": "Nguyễn Khoa",
  "accountStatus": "Locked",
  "lockedReason": "Vi phạm quy định cộng đồng",
  "lockedAt": "2026-10-24T06:00:00Z",
  "createdAt": "2026-10-15T10:00:00Z"
}
```

`accountStatus` ∈ `Active` · `Locked` · `Deleted`. `lockedReason`/`lockedAt` là `null` khi tài
khoản đang `Active`. **Lý do khóa chỉ trả cho admin**, không có trong hồ sơ công khai.

### 4.2. `POST /api/admin/users/{id}/lock` — khóa tài khoản

Body **tùy chọn** (bỏ trống = khóa không kèm lý do):

```json
{ "reason": "Vi phạm quy định cộng đồng" }
```

- `reason` tùy chọn, tối đa **500** ký tự; chuỗi rỗng/toàn khoảng trắng được lưu thành `null`.
- `200 OK` + `AdminUserSummary` với `accountStatus = Locked`.
- **Idempotent:** khóa lại tài khoản đã `Locked` trả `200`, **không** ghi đè lý do/thời điểm khóa ban đầu.
- Tác dụng phụ khi khóa thành công:
  1. Thu hồi **toàn bộ** refresh token còn hiệu lực của người dùng.
  2. Phát `AccountLocked` tới `user:{id}` và **ngắt** mọi kết nối SignalR đang mở.
  3. Access token cũ bị từ chối ở request kế tiếp (`401 UNAUTHORIZED`) vì server kiểm tra lại
     `account_status` ở mọi request đã xác thực.
- Lỗi: `USER_NOT_FOUND` (404), `ADMIN_CANNOT_LOCK_SELF` (400), `VALIDATION_FAILED` (400 — lý do
  quá 500 ký tự hoặc body JSON không đọc được), `FORBIDDEN` (403), `UNAUTHORIZED` (401).

### 4.3. `POST /api/admin/users/{id}/unlock` — mở khóa tài khoản

Không cần body.

- `200 OK` + `AdminUserSummary` với `accountStatus = Active`; `lockedReason`, `lockedAt` (và
  `locked_by` trong DB) được xóa.
- **Idempotent:** mở khóa tài khoản đang `Active` trả `200` mà không đổi dữ liệu.
- Lỗi: `USER_NOT_FOUND` (404), `FORBIDDEN` (403), `UNAUTHORIZED` (401).

### 4.4. SignalR — sự kiện quản trị

| Sự kiện | Gửi tới | Payload | Khi nào |
|---|---|---|---|
| `AccountLocked` | `user:{id}` | `{ userId }` | Ngay sau khi admin khóa tài khoản, **trước** khi server ngắt kết nối |

> Client nhận sự kiện phải xóa phiên và về trang đăng nhập. Việc ngắt kết nối (`Abort`) là lớp bảo
> vệ thứ hai — không phụ thuộc client có xử lý sự kiện hay không (`docs/tasks/phase5_admin.md` D2).
> Người dùng đang giữ refresh token đã bị thu hồi sẽ nhận `AUTH_REFRESH_TOKEN_REUSED` khi thử làm
> mới token (token đã bị đánh dấu thu hồi được coi như dấu hiệu bị đánh cắp — hành vi có từ GĐ 1).

---

## 5. Mã lỗi

| Mã | HTTP | Ý nghĩa |
|---|---|---|
| `VALIDATION_FAILED` | 400 | Dữ liệu đầu vào không hợp lệ (kèm `errors`) — **hoặc** body JSON không đọc được (sai định dạng, sai kiểu, thiếu trường bắt buộc); trường hợp này không có `errors` |
| `AUTH_INVALID_CREDENTIALS` | 401 | Sai tài khoản hoặc mật khẩu |
| `AUTH_ACCOUNT_LOCKED` | 403 | Tài khoản bị admin khóa (`account_status = Locked`) — trả khi đăng nhập **đúng mật khẩu**, hoặc khi làm mới token của tài khoản vừa bị khóa |
| `AUTH_REFRESH_TOKEN_INVALID` | 401 | Refresh token không hợp lệ/hết hạn/đã thu hồi |
| `AUTH_REFRESH_TOKEN_REUSED` | 401 | Phát hiện dùng lại refresh token cũ; đã thu hồi cả họ |
| `AUTH_EMAIL_ALREADY_EXISTS` | 409 | Email đã được dùng |
| `AUTH_USERNAME_ALREADY_EXISTS` | 409 | Tên đăng nhập đã được dùng |
| `FORBIDDEN` | 403 | Không đủ quyền |
| `UNAUTHORIZED` | 401 | Thiếu/token không hợp lệ |
| `NOT_FOUND` | 404 | Không tìm thấy tài nguyên |
| `USER_NOT_FOUND` | 404 | Không tìm thấy người dùng *(GĐ 5 — khóa/mở khóa tài khoản)* |
| `ADMIN_CANNOT_LOCK_SELF` | 400 | Admin không được tự khóa tài khoản của chính mình *(GĐ 5)* |
| `INTERNAL_ERROR` | 500 | Lỗi không mong đợi |

Mã lỗi là **hằng số tiếng Anh** ở backend; web/app dịch sang `vi`/`en`.

> **Ghi chú (2026-10-18):** lỗi framework khi đọc body (`BadHttpRequestException` với status 400) trước đây trả `500 INTERNAL_ERROR`; đã sửa thành `400 VALIDATION_FAILED` qua `ExceptionStatusMapper` (`backend/src/Comink.Api/Common/ExceptionStatusMapper.cs`). Chỉ map đúng 400; các mã 4xx khác (413, 415…) vẫn trả 500 và **chưa có mã lỗi riêng** — cần bổ sung khi làm phần bảo mật/hardening.
>
> **Quy ước `title` là hàm của `code`:** `title` được suy ra từ `code` bằng `ApiResults.ApplyProblemDefaults` (trước đây lỗi 400 do framework có title `Bad Request` còn lỗi validate có `Validation failed`). Nay lỗi **cùng mã luôn cùng tiêu đề** — ví dụ cả hai loại 400 đều là `Validation failed`; 404 do framework là `Not found`, còn 404 bài viết là `Post not found` (theo `POST_NOT_FOUND`). Client vẫn **chỉ dựa vào `code`**, không dựa vào `title`.

---

## 6. SignalR

| Hạng mục | Quy ước |
|---|---|
| Đường dẫn hub | `/hub/chat` (đặt tên cụ thể khi làm GĐ 3; GĐ 1 chỉ dựng hub nền) |
| Xác thực | access token qua query string `access_token` (bắt buộc với WebSocket — `docs/02_tech_stack_decisions.md` mục 1.4) |
| Nhóm | `user:{userId}` cho mỗi người dùng; nhóm theo hội thoại thêm ở GĐ 3 |
| Kiểm tra quyền | Server kiểm tra quyền khi vào nhóm, không chỉ ở REST |

**Sự kiện GĐ 1:** chưa có nghiệp vụ. Chỉ cần chứng minh kết nối được và token hợp lệ.

---

## 7. Mạng xã hội (Giai đoạn 2)

> Trạng thái: **mục 7.1–7.3 đã cài đặt và kiểm chứng**; mục 7.4–7.6 mới chốt hợp đồng, chưa cài đặt
> (chi tiết và lý do ở `docs/tasks/phase2_social.md` mục 3).
> Mọi endpoint dưới đây cần `Authorization: Bearer <access_token>`.

### 7.1. Tìm người dùng và hồ sơ công khai — `/api/users`

| Method | Đường dẫn | Mục đích |
|---|---|---|
| `GET` | `/api/users?search=&cursor=&limit=` | Tìm người dùng; `search` **bắt buộc** 1–100 ký tự, gõ **không dấu** vẫn khớp (`immutable_unaccent` + `pg_trgm`) |
| `GET` | `/api/users/{id}` | Hồ sơ công khai + quan hệ với người xem + số liệu |

Thứ tự kết quả tìm kiếm: `created_at DESC, id DESC` (để phân trang con trỏ thống nhất với mọi danh sách khác).

```json
{
  "id": "0199...", "userName": "khoa.test", "displayName": "Khoa Nguyễn",
  "avatarUrl": null, "bio": "Sinh viên CNTT",
  "relationship": { "friendship": "Friends", "following": true, "followedBy": false }
}
```

`friendship` ∈ `None` · `OutgoingPending` · `IncomingPending` · `Friends`.
`GET /api/users/{id}` trả thêm `coverUrl`, `location`, `followerCount`, `followingCount`, `friendCount`, `createdAt`. Người không tồn tại hoặc bị khóa → `NOT_FOUND`.

> **Tài khoản Admin là tài khoản ẩn (bổ sung GĐ 5, chốt 2026-10-24):**
> - Không xuất hiện trong `GET /api/users?search=` với **bất kỳ ai**, kể cả Admin khác.
> - `GET /api/users/{id}` và `GET /api/users/{id}/posts` trả `404 NOT_FOUND` cho người dùng
>   thường; chỉ **chính tài khoản đó** hoặc **một Admin khác** xem được.
> - Người dùng thường gửi lời mời kết bạn/theo dõi/mở chat 1–1/mời vào nhóm tới Admin →
>   `404 NOT_FOUND` (ẩn sự tồn tại, không dùng 403).
> - Trang quản trị `GET /api/admin/users` **không** bị ảnh hưởng — admin vẫn thấy đủ tài khoản.
> - Phạm vi: chỉ chặn **khám phá và hành động mới**. Quan hệ cũ (nếu đã có), danh sách bạn bè/
>   người theo dõi và nội dung đã tồn tại không bị lọc (xem giới hạn ở `docs/tasks/phase5_admin.md` mục 8).

Lỗi: `VALIDATION_FAILED` (400 — từ khóa rỗng/quá dài), `NOT_FOUND` (404).

### 7.2. Bạn bè hai chiều — `/api/friendships`

| Method | Đường dẫn | Mục đích |
|---|---|---|
| `POST` | `/api/friendships/requests` | Gửi lời mời; body `{ "addresseeId": "uuid" }` → `201` `FriendshipDto` |
| `GET` | `/api/friendships/requests/incoming` | Lời mời tôi nhận được |
| `GET` | `/api/friendships/requests/outgoing` | Lời mời tôi đã gửi |
| `POST` | `/api/friendships/requests/{id}/accept` | Chấp nhận (chỉ người nhận) → `200` |
| `DELETE` | `/api/friendships/requests/{id}` | Từ chối / thu hồi lời mời → `204` |
| `GET` | `/api/friendships` | Danh sách bạn bè |
| `DELETE` | `/api/friendships/{userId}` | Hủy kết bạn → `204` (idempotent) |

`FriendshipDto`: `{ id, user: UserSummaryDto, status: "Pending"|"Accepted", createdAt, respondedAt }`

Quy tắc đã cài đặt:

1. Mỗi cặp người chỉ một dòng (chỉ mục duy nhất `ux_friendships_pair`); gửi trùng → **idempotent**, trả lại lời mời cũ.
2. Gửi khi **đã là bạn** → `409 SOCIAL_ALREADY_FRIENDS`.
3. Gửi khi **đối phương đã mời trước** → **tự động chấp nhận**, trả `200`/`201` với `status = Accepted`.
4. Tự kết bạn → `400 SOCIAL_CANNOT_FRIEND_SELF`.
5. Chấp nhận/từ chối lời mời không phải của mình → `403 SOCIAL_NOT_REQUEST_ADDRESSEE` (không tồn tại → `404 SOCIAL_FRIEND_REQUEST_NOT_FOUND`).
6. Chấp nhận lại → `200` (idempotent); `friend_count` của cả hai tăng/giảm cùng transaction.
7. Gửi lời mời tới tài khoản Admin khi người gửi là người dùng thường → `404 NOT_FOUND` (ẩn tài khoản); Admin gửi cho Admin vẫn hợp lệ.

### 7.3. Theo dõi một chiều — `/api/follows` và `/api/users/{id}/followers|following`

| Method | Đường dẫn | Mục đích |
|---|---|---|
| `POST` | `/api/follows/{userId}` | Theo dõi → `204` (idempotent) |
| `DELETE` | `/api/follows/{userId}` | Bỏ theo dõi → `204` (idempotent) |
| `GET` | `/api/users/{id}/followers` | Người theo dõi |
| `GET` | `/api/users/{id}/following` | Đang theo dõi |

Tự theo dõi → `400 SOCIAL_CANNOT_FOLLOW_SELF`; người dùng không tồn tại → `404 NOT_FOUND`.
Người dùng thường theo dõi tài khoản Admin → `404 NOT_FOUND` (ẩn tài khoản); Admin theo dõi Admin vẫn hợp lệ.
`follower_count` (người được theo dõi) và `following_count` (người theo dõi) cập nhật cùng transaction.
Vì bảng `follows` không có cột id riêng, con trỏ phân trang dùng `(created_at, user_id)`.

### 7.4. Bài viết, bảng tin — ✅ đã cài đặt

| Method | Đường dẫn | Mục đích |
|---|---|---|
| `POST` | `/api/posts` | Tạo bài → `201` `PostDto` |
| `GET` | `/api/posts/{id}` | Xem một bài (kiểm tra quyền xem) |
| `PATCH` | `/api/posts/{id}` | Sửa (chỉ tác giả) → `200` |
| `DELETE` | `/api/posts/{id}` | Xóa mềm (chỉ tác giả) → `204` |
| `GET` | `/api/feed?cursor=&limit=` | Bảng tin: bạn bè + người đang theo dõi + chính mình |
| `GET` | `/api/users/{id}/posts?cursor=&limit=` | Bài trên trang cá nhân (lọc theo quyền xem) |

Request tạo bài: `{ "content": "...", "visibility": "Friends", "mediaIds": ["uuid"], "sharedPostId": null }`.

`PostDto`: `{ id, author: UserSummaryDto, content, visibility, media: [{id,url,thumbnailUrl,width,height}], sharedPost: PostDto|null, likeCount, commentCount, shareCount, likedByMe, isMine, createdAt, updatedAt }`

Quy tắc đã cài đặt:

- `content` ≤ 5000; được rỗng **chỉ khi** có `mediaIds` hoặc `sharedPostId`, nếu không → `400 POST_CONTENT_REQUIRED`.
- `visibility` ∈ `Public` · `Friends` · `OnlyMe`; bỏ trống → lấy `user_settings.default_post_visibility` (mặc định `Friends`).
- Bảng tin sắp xếp `created_at DESC, id DESC`; **không** chứa bài của người không phải bạn bè/đang theo dõi (kể cả bài `Public` của họ).
- Chia sẻ bài → tăng `share_count` của bài gốc; bài gốc bị xóa hẳn thì `shared_post_id` về null.
- Sửa/xóa bài của người khác → `403 FORBIDDEN`.

Bài không có quyền xem → **`404 POST_NOT_FOUND`** (không dùng 403 để không tiết lộ sự tồn tại). Quy tắc xem: `Public` — mọi người; `Friends` — bạn bè hoặc tác giả; `OnlyMe` — chỉ tác giả; `Hidden`/đã xóa coi như không tồn tại.

### 7.5. Thích và bình luận — ✅ đã cài đặt

| Method | Đường dẫn | Mục đích |
|---|---|---|
| `POST` / `DELETE` | `/api/posts/{id}/likes` | Thích / bỏ thích → `204` (idempotent, cập nhật `like_count`) |
| `POST` | `/api/posts/{id}/comments` | Tạo bình luận / trả lời → `201` `CommentDto` |
| `GET` | `/api/posts/{id}/comments` | Bình luận gốc (thứ tự **cũ trước**) |
| `GET` | `/api/comments/{id}/replies` | Các trả lời (thứ tự cũ trước) |
| `PATCH` / `DELETE` | `/api/comments/{id}` | Sửa (chỉ tác giả) / xóa mềm (tác giả **hoặc** chủ bài) |

`CommentDto`: `{ id, postId, parentCommentId, author: UserSummaryDto, content, replyCount, createdAt, updatedAt }`

Quy tắc đã cài đặt:

- Chỉ **một cấp trả lời**: `parentCommentId` phải là bình luận gốc, sai → `400 COMMENT_REPLY_DEPTH_EXCEEDED`; bình luận không thuộc bài → `404 COMMENT_NOT_FOUND`.
- Đếm: `posts.comment_count` tăng/giảm theo **mọi** bình luận; `comments.reply_count` chỉ đếm trả lời trực tiếp.
- Xóa bình luận gốc → các trả lời bị xóa mềm theo và `comment_count` giảm tương ứng; `GET /api/comments/{id}/replies` của bình luận đã xóa → `404 COMMENT_NOT_FOUND`.
- Không xem được bài → `404 POST_NOT_FOUND` cho cả thích và bình luận.

### 7.6. Media — ✅ đã cài đặt

| Method | Đường dẫn | Mục đích |
|---|---|---|
| `POST` | `/api/media/signature` | Lấy thông tin upload (Cloudinary: chữ ký HMAC; Local: endpoint backend) |
| `POST` | `/api/media/local-upload?context=Post` | **Chỉ khi dev dùng đĩa cục bộ**: nhận tệp `multipart/form-data` (trường `file`) |
| `POST` | `/api/media` | Đăng ký tài sản sau khi tải lên → `201` `MediaAssetDto` |

`POST /api/media/signature` request: `{ "context": "Post", "resourceType": "Image" }` → trả
`{ provider, uploadUrl, signature, apiKey, cloudName, timestamp, uploadPreset, folder }`.

- `provider = "Cloudinary"`: `uploadUrl = https://api.cloudinary.com/v1_1/{cloud}/auto/upload`; client POST `multipart/form-data` với `file`, `api_key`, `timestamp`, `signature`, `folder` (và `upload_preset` nếu cấu hình).
- `provider = "Local"` (dev, thiếu khoá Cloudinary): `uploadUrl = /api/media/local-upload?context=Post`; client POST `multipart/form-data` trường `file` kèm access token → trả `{ provider, providerPublicId, url, mimeType, sizeBytes }`.

Ràng buộc (kiểm tra ở **cả** client và server):

| Loại | Giới hạn |
|---|---|
| Định dạng ảnh | `image/jpeg`, `image/png`, `image/webp` → sai: `400 MEDIA_UNSUPPORTED_TYPE` |
| Dung lượng mỗi ảnh | ≤ 5 MB → sai: `400 MEDIA_TOO_LARGE` |
| Số ảnh mỗi bài | ≤ 10 → sai: `400 POST_TOO_MANY_MEDIA` |
| Ảnh trong bài | phải thuộc chính người đăng, `context = Post` → sai: `400 POST_MEDIA_NOT_OWNED` |

Đăng ký lại cùng `providerPublicId` của cùng người dùng → trả về bản đã có (idempotent).
Tệp lưu cục bộ được phục vụ tại `/uploads/...` **chỉ khi** chạy dev không có khoá Cloudinary.

### 7.7. Mã lỗi mới

| Mã | HTTP | Ý nghĩa |
|---|---|---|
| `SOCIAL_CANNOT_FRIEND_SELF` | 400 | Không thể kết bạn với chính mình |
| `SOCIAL_CANNOT_FOLLOW_SELF` | 400 | Không thể tự theo dõi mình |
| `SOCIAL_ALREADY_FRIENDS` | 409 | Đã là bạn bè |
| `SOCIAL_FRIEND_REQUEST_NOT_FOUND` | 404 | Lời mời không tồn tại / đã xử lý |
| `SOCIAL_NOT_REQUEST_ADDRESSEE` | 403 | Không phải người nhận lời mời |
| `POST_NOT_FOUND` | 404 | Không tìm thấy bài hoặc không có quyền xem *(7.4)* |
| `POST_CONTENT_REQUIRED` | 400 | Bài rỗng: cần nội dung, ảnh hoặc bài chia sẻ *(7.4)* |
| `POST_TOO_MANY_MEDIA` | 400 | Quá số ảnh cho phép *(7.4)* |
| `POST_MEDIA_NOT_OWNED` | 400 | Ảnh không thuộc người đăng *(7.4)* |
| `COMMENT_NOT_FOUND` | 404 | Không tìm thấy bình luận *(7.5)* |
| `COMMENT_REPLY_DEPTH_EXCEEDED` | 400 | Chỉ cho phép trả lời 1 cấp *(7.5)* |
| `MEDIA_UNSUPPORTED_TYPE` | 400 | Định dạng tệp không hỗ trợ *(7.6)* |
| `MEDIA_TOO_LARGE` | 400 | Tệp vượt dung lượng *(7.6)* |
| `MEDIA_NOT_FOUND` | 404 | Không tìm thấy tài sản media *(7.6)* |

---

## 8. Nhắn tin (Giai đoạn 3)

> Trạng thái: **mục 8.2–8.7 đã cài đặt ở backend và kiểm chứng bằng HTTP thật (2026-10-18)**; web đang làm. Quyết định **D1 đã chốt: chỉ bạn bè mới mở được hội thoại 1–1** (`docs/tasks/phase3_messaging.md` mục 2).
> **Cập nhật 2026-11-17 (GĐ 7 — Ưu tiên 1):** đã bổ sung **mục 8.9–8.10** — trạng thái online, "đang nhập", đã gửi/đã đọc; `unreadCount` nay là số thật; DTO hội thoại có thêm trường presence/mốc đọc. Đã cài đặt backend + web, kiểm chứng bằng 2 client SignalR thật + HTTP (mục 8.11); app Flutter làm tiếp.
> **Cập nhật 2026-11-20 (GĐ 7 — Ưu tiên 2, Đợt A):** đã bổ sung **mục 8.12–8.17** — phản hồi, chỉnh sửa, thu hồi, thả cảm xúc; DTO tin nhắn có `replyTo`/`forwardedFrom`/`editedAt`/`deletedAt`/`reactions`; sự kiện `MessageUpdated`. Đã cài đặt backend + web và kiểm chứng thật (HTTP + SignalR 23/23, Chrome headless 21/21).
> **Cập nhật 2026-11-20 (GĐ 7 — Ưu tiên 2, Đợt B):** bổ sung **mục 8.18–8.22** — ghim, chuyển tiếp, tìm kiếm + ngữ cảnh, tắt thông báo; sự kiện `PinnedMessagesChanged`; DTO hội thoại có `isMuted`/`mutedUntil`. Kiểm chứng thật: HTTP + SignalR 27/27, Chrome headless 26/26; OpenAPI tái sinh (59 path).

### 8.1. Quy ước riêng

| Quy ước | Giá trị |
|---|---|
| Không phải thành viên hội thoại | `404 CONVERSATION_NOT_FOUND` cho **mọi** endpoint (không dùng 403 — tránh lộ sự tồn tại của hội thoại) |
| Phân trang | Con trỏ: tin nhắn `(created_at, id)`, danh sách hội thoại `(last_message_at, id)`; **không** OFFSET |
| Gửi tin idempotent | `clientMessageId` (uuid) do client sinh; trùng khoá → trả `200` kèm tin đã lưu, không tạo bản ghi mới |
| Giới hạn | `content` ≤ 4000 ký tự; ≤ 10 tệp/tin; `limit` ≤ 50 (mặc định 20) |
| Ai được nhắn cho ai | **D1 chưa chốt**; nếu chọn "chỉ bạn bè" thì trả `403 MESSAGING_NOT_FRIENDS` |

### 8.2. Mở/tạo hội thoại 1–1

`POST /api/conversations/direct`

| Trường | Kiểu | Bắt buộc | Ghi chú |
|---|---|---|---|
| `userId` | uuid | ✔ | Người muốn nhắn cùng |

- `201 Created` — tạo mới; `200 OK` — hội thoại đã tồn tại (idempotent theo `direct_key`).
- Trả `ConversationDto`: `{ id, type, title?, avatarUrl?, createdAt, lastMessageAt?, members: [{ userId, displayName, userName, avatarUrl, isOnline, lastSeenAt, lastReadMessageId, lastReadAt }], lastMessage?, isMuted, mutedUntil }`
- Lỗi: `MESSAGING_CANNOT_MESSAGE_SELF` (400), `MESSAGING_NOT_FRIENDS` (403 — nếu D1 chọn "chỉ bạn bè"), `USER_NOT_FOUND` (404).
- Mở chat 1–1 với tài khoản Admin khi người gửi là người dùng thường → `404 NOT_FOUND` (ẩn tài khoản); Admin với Admin vẫn hợp lệ.

### 8.2b. Tạo nhóm chat (NC GĐ 3)

`POST /api/conversations/group`

| Trường | Kiểu | Bắt buộc | Ghi chú |
|---|---|---|---|
| `name` | string(1–100) | ✔ | Tên nhóm |
| `participantIds` | uuid[] | ✔ | Danh sách bạn bè được mời (không cần gửi kèm chính mình; gửi kèm sẽ bị bỏ qua) |

- `201 Created` kèm `ConversationDto` (người tạo là `MemberRole.Owner`, người mời là `Member`).
- Tổng thành viên = `participantIds` (đã khử trùng) + người tạo, phải trong **3–10**.
- Lỗi: `MESSAGING_GROUP_TOO_FEW` (400), `MESSAGING_GROUP_TOO_MANY` (400), `MESSAGING_NOT_FRIENDS` (403), `USER_NOT_FOUND` (404), `VALIDATION_FAILED` (400 — tên rỗng/quá dài).
- Mời tài khoản Admin vào nhóm khi người tạo là người dùng thường → `404 NOT_FOUND` (ẩn tài khoản).
- Gọi nhóm dùng chính hội thoại này: `POST /api/calls` với `conversationId` (xem 9.2) — **không** còn `participantIds` (quyết định D6).

### 8.3. Danh sách hội thoại

`GET /api/conversations?limit=&cursor=`

- Sắp xếp `last_message_at DESC` (`NULLS LAST` cho hội thoại chưa có tin).
- Trả cả hội thoại **1–1** và **nhóm**.
- Trả `PagedList<ConversationSummaryDto>`: `{ id, type, title?, peer?: { userId, userName, displayName, avatarUrl, isOnline, lastSeenAt } | null, lastMessage?: { id, senderId, preview, type, createdAt, isDeleted }, unreadCount, memberCount, isMuted, mutedUntil }` — `peer` chỉ có với `type = "Direct"`; nhóm dùng `title` + `memberCount`. `isMuted`/`mutedUntil` là trạng thái tắt thông báo của **chính người gọi** (NC Ưu tiên 2, mục 8.22).
- `unreadCount` (từ GĐ 7): số tin của **người khác** có `(created_at, id)` lớn hơn mốc đọc của tôi (mục 8.9). Trước GĐ 7 trường này luôn `0`.

### 8.4. Chi tiết hội thoại

`GET /api/conversations/{id}` → `ConversationDto` (cùng hình dạng 8.2, mỗi `member` có thêm `isOnline`, `lastSeenAt`, `lastReadMessageId`, `lastReadAt` — mục 8.10; hội thoại có thêm `isMuted`, `mutedUntil` của chính người gọi — mục 8.22). Người ngoài → `404 CONVERSATION_NOT_FOUND`.

### 8.5. Lịch sử tin nhắn

`GET /api/conversations/{id}/messages?limit=&before=|after=`

| Tham số | Ý nghĩa |
|---|---|
| `before` | Con trỏ `(created_at, id)` — lấy **cũ hơn** (cuộn lên). Trả về mới nhất trước |
| `after` | Con trỏ — lấy **mới hơn** (đồng bộ sau khi mất kết nối). Trả về cũ nhất trước |
| (không có) | 50 tin mới nhất |

- Gửi cả `before` và `after` → `400 VALIDATION_FAILED`.
- Trả `PagedList<MessageDto>`: `{ id, conversationId, senderId, type, content?, attachments: [{ mediaId, url, thumbnailUrl?, width?, height? }], clientMessageId, createdAt, replyTo?, forwardedFrom?, editedAt?, deletedAt?, reactions[] }` — các trường từ `replyTo` trở đi thuộc NC Ưu tiên 2 (mục 8.12–8.16).

### 8.6. Gửi tin nhắn

`POST /api/conversations/{id}/messages`

| Trường | Kiểu | Bắt buộc | Ghi chú |
|---|---|---|---|
| `clientMessageId` | uuid | ✔ | Client sinh; dùng chống gửi trùng |
| `content` | string? | ✖ | ≤ 4000; rỗng khi tin chỉ có ảnh |
| `mediaIds` | uuid[]? | ✖ | ≤ 10; media phải do người gửi tải lên với `context = Message` |

- `201 Created` tin mới; `200 OK` nếu trùng `clientMessageId` (trả tin đã lưu).
- Lỗi: `MESSAGE_CONTENT_REQUIRED` (400 — không có cả nội dung lẫn ảnh), `MESSAGE_TOO_MANY_ATTACHMENTS` (400), `MEDIA_NOT_OWNED` (400), `CONVERSATION_NOT_FOUND` (404).
- Phát sự kiện `MessageCreated` tới group `conversation:{id}` (xem 8.7).

### 8.7. SignalR — hub `/hub/chat`

| Hạng mục | Quy ước |
|---|---|
| Xác thực | `access_token` qua query string (đã có từ GĐ 1) |
| Group người dùng | `user:{userId}` (đã có) |
| Group hội thoại | `conversation:{conversationId}` — **server tự thêm** khi client kết nối, dựa trên danh sách thành viên còn hiệu lực (`left_at IS NULL`). **Không** có hub method để client tự join/leave |
| Sự kiện server → client | `MessageCreated` (payload `MessageDto`) gửi tới **`user:{id}` của từng thành viên** *và* group `conversation:{id}`. Phải phát theo `user:{id}` vì client chỉ được thêm vào group hội thoại **lúc kết nối** — hội thoại tạo sau khi kết nối sẽ không có ai trong group (lỗi thật 2026-10-19: phải tải lại trang mới thấy tin). Client ở cả hai group nhận trùng không sao: handler chèn tin theo `id` rồi làm mới dữ liệu |
| Tín hiệu client → server | Từ GĐ 7: `Typing(conversationId, isTyping)` — chỉ thành viên, server phát `TypingChanged` cho group hội thoại **trừ** connection gửi; chặn tối thiểu 2 giây/người/hội thoại. `Ping()` — làm mới TTL presence (client gọi mỗi 30 giây). Chi tiết mục 8.10 |
| Đồng bộ sau khi mất kết nối | Client kết nối lại → gọi `GET /api/conversations/{id}/messages?after={lastMessageId}` và `GET /api/conversations` để làm mới; tải lại `GET /api/conversations/{id}` để cập nhật trạng thái online |
| Quyền | Người ngoài không nằm trong group hội thoại ⇒ **không nhận** `MessageCreated`; mọi truy cập REST trả 404. Presence/typing/đã đọc cũng chỉ tới người cùng hội thoại (mục 8.10) |

### 8.8. Mã lỗi mới (GĐ 3)

| Mã | HTTP | Ý nghĩa |
|---|---|---|
| `CONVERSATION_NOT_FOUND` | 404 | Không tìm thấy hội thoại **hoặc** người dùng không phải thành viên |
| `MESSAGING_CANNOT_MESSAGE_SELF` | 400 | Không thể tự mở hội thoại với chính mình |
| `MESSAGING_NOT_FRIENDS` | 403 | Chưa là bạn bè nên chưa nhắn tin được *(chỉ khi D1 chọn "chỉ bạn bè")* |
| `MESSAGING_GROUP_TOO_FEW` | 400 | Nhóm dưới 3 thành viên (NC GĐ 3) |
| `MESSAGING_GROUP_TOO_MANY` | 400 | Nhóm quá 10 thành viên (NC GĐ 3) |
| `MESSAGE_CONTENT_REQUIRED` | 400 | Tin không có nội dung và cũng không có tệp |
| `MESSAGE_TOO_MANY_ATTACHMENTS` | 400 | Quá 10 tệp trong một tin |
| `MESSAGE_CLIENT_ID_REQUIRED` | 400 | Thiếu `clientMessageId` |
| `MESSAGE_INVALID_CURSOR` | 400 | Con trỏ phân trang không hợp lệ |
| `MESSAGE_NOT_FOUND` | 404 | Tin không tồn tại **hoặc** không thuộc hội thoại đang đánh dấu đã đọc *(GĐ 7)* |

### 8.9. Đánh dấu đã đọc (NC GĐ 7)

`POST /api/conversations/{id}/read`

| Trường | Kiểu | Bắt buộc | Ghi chú |
|---|---|---|---|
| `messageId` | uuid | ✔ | Tin cuối cùng người dùng đã xem |

- `204 No Content` — kể cả khi mốc đã bằng hoặc **mới hơn** tin gửi lên (idempotent, **chỉ tiến**).
- Mốc lưu ở `conversation_members.last_read_message_id` + `last_read_at` (migration **M6**); so sánh theo `(created_at, id)` như con trỏ phân trang.
- Khi mốc tiến thật: phát `MessagesRead` (mục 8.10) tới mọi thành viên còn hiệu lực (`user:{id}` + group hội thoại).
- Lỗi: `CONVERSATION_NOT_FOUND` (404 — không phải thành viên), `MESSAGE_NOT_FOUND` (404 — tin không thuộc hội thoại), `VALIDATION_FAILED` (400 — thiếu `messageId`).

### 8.10. Presence, "đang nhập", đã đọc (NC GĐ 7)

**Trường bổ sung trong DTO**

| DTO | Trường mới | Ý nghĩa |
|---|---|---|
| `ConversationPeerDto` | `isOnline` (bool), `lastSeenAt` (string?) | Người còn lại của chat 1–1 |
| `ConversationMemberDto` | `isOnline`, `lastSeenAt`, `lastReadMessageId` (uuid?), `lastReadAt` (string?) | Thành viên; mốc đọc dùng để hiển thị "Đã đọc"/"Đã xem bởi N" |
| `ConversationSummaryDto` | `unreadCount` — nay là số thật | Xem 8.3 |

**Presence**

- Lưu trạng thái trong **Redis**: mỗi người một sorted set connection id → mốc ping gần nhất, key TTL 90 giây; "online" = còn connection trong cửa sổ 90 giây. Client gửi `Ping` mỗi 30 giây. Nhiều tab/thiết bị: chỉ kết nối đầu → online, kết nối cuối đóng → offline.
- Server → client: `PresenceChanged` `{ userId, isOnline, lastSeenAt }`, phát qua `user:{id}` cho **những người cùng ít nhất một hội thoại còn hiệu lực** (không phát toàn cục).
- Redis không sẵn sàng → coi như offline, **không** làm hỏng chat.

**"Đang nhập"**

| Hướng | Tên | Payload |
|---|---|---|
| Client → server | `Typing(conversationId, isTyping)` | — (server kiểm tra membership) |
| Server → client | `TypingChanged` | `{ conversationId, userId, isTyping }` — group `conversation:{id}` **trừ** connection gửi |

- Không lưu DB; client throttle ~3 giây/lần, tự gửi `isTyping=false` sau 5 giây ngừng gõ; server chặn tối thiểu 2 giây/người/hội thoại.

**Đã đọc**

| Hướng | Tên | Payload |
|---|---|---|
| Server → client | `MessagesRead` | `{ conversationId, userId, messageId, readAt }` — mọi thành viên còn hiệu lực |

- "Đã gửi" = tin đã lưu DB và đã phát real-time (không có trạng thái "đã nhận" riêng — quyết định #5 của `docs/03_database_design.md` mục 7).
- Ghi chú: tín hiệu `TypingChanged` dùng group hội thoại (client chỉ vào group lúc kết nối), nên với hội thoại **tạo sau** khi client kết nối, tín hiệu gõ của người mới chỉ tới người kia sau khi tải lại khung chat. Tin nhắn/đã đọc/presence không bị vì phát thêm qua `user:{id}`.

### 8.11. Kiểm chứng GĐ 7 — Ưu tiên 1 (2026-11-17)

- Backend: `dotnet test` **224/224** (Domain 72 · Application 25 · Tích hợp 127, gồm `ReadReceiptTests` 6 + `ChatHubTests` 12), `dotnet build` 0 warning.
- HTTP + **2 client SignalR thật** (script Node, 3 tài khoản): **28/28** — offline ban đầu → online khi kết nối (alice + carol cùng nhận), `TypingChanged` đúng người nhận/kể cả nhóm và **không** tới người ngoài hội thoại, `unreadCount` 2→0, `MessagesRead` real-time (1–1 và nhóm), mốc cũ không lùi/không phát lại, người ngoài `404 CONVERSATION_NOT_FOUND`, tin khác hội thoại `404 MESSAGE_NOT_FOUND`, offline có `lastSeenAt`.
- Web: `npm run lint` 0/0, `npm run build` thành công (còn cảnh báo bundle > 1 MB — KI-09).

### 8.12. Phản hồi tin nhắn (NC GĐ 7 — Ưu tiên 2)

`POST /api/conversations/{id}/messages` — **thêm trường** `replyToMessageId` (uuid?, tùy chọn) vào request ở mục 8.6.

| Điều kiện | Kết quả |
|---|---|
| Tin được trả lời không tồn tại **hoặc** thuộc hội thoại khác | `404 MESSAGE_NOT_FOUND` |
| Tin được trả lời đã bị thu hồi | `400 MESSAGE_ALREADY_DELETED` |
| Hợp lệ | Tạo tin bình thường; `MessageDto.replyTo` chứa ảnh chụp tin gốc |

`MessageQuoteDto`: `{ messageId, senderId, senderDisplayName, contentPreview, type, isDeleted }`.
Tin gốc bị thu hồi **sau đó** vẫn trả `replyTo` với `isDeleted = true`, `contentPreview = null` — client hiện "Tin nhắn đã bị thu hồi".

### 8.13. Chỉnh sửa tin nhắn (NC GĐ 7 — Ưu tiên 2)

`PATCH /api/conversations/{id}/messages/{messageId}`

| Trường | Kiểu | Bắt buộc | Ghi chú |
|---|---|---|---|
| `content` | string? | ✔ | ≤ 4000; được rỗng **chỉ khi** tin đã có tệp đính kèm |

- `200 OK` kèm `MessageDto` (có `editedAt`), phát `MessageUpdated` tới mọi thành viên còn hiệu lực.
- Chỉ **người gửi**, trong **24 giờ** kể từ `createdAt`; tin `System`/`Call` không sửa được.
- Lỗi: `CONVERSATION_NOT_FOUND` (404), `MESSAGE_NOT_FOUND` (404), `MESSAGE_NOT_OWNED` (403), `MESSAGE_EDIT_WINDOW_EXPIRED` (403 — quá 24 giờ), `MESSAGE_ALREADY_DELETED` (400), `MESSAGE_NOT_EDITABLE` (400 — loại tin), `MESSAGE_CONTENT_REQUIRED` (400 — sửa thành rỗng khi không có tệp), `VALIDATION_FAILED` (400 — quá dài).

### 8.14. Thu hồi tin nhắn (NC GĐ 7 — Ưu tiên 2)

`DELETE /api/conversations/{id}/messages/{messageId}` → `200 OK` kèm `MessageDto` đã thu hồi.

- Chỉ **người gửi**, trong **24 giờ**; tin `System`/`Call` không thu hồi được.
- Thu hồi **mềm**: đặt `deletedAt`, xoá `content`; API trả `attachments = []`, `reactions = []`, `replyTo = null`,
  `forwardedFrom = null`; dòng tin vẫn giữ để hiện khung "Tin nhắn đã bị thu hồi".
- Cảm xúc của tin bị xoá; nếu tin đang ghim thì **tự bỏ ghim** và phát `PinnedMessagesChanged`.
- Thu hồi **lặp lại** → `200` với cùng `deletedAt`, **không** phát lại sự kiện (idempotent).
- Lỗi: `CONVERSATION_NOT_FOUND` (404), `MESSAGE_NOT_FOUND` (404), `MESSAGE_NOT_OWNED` (403), `MESSAGE_RECALL_WINDOW_EXPIRED` (403), `MESSAGE_NOT_EDITABLE` (400).

### 8.15. Thả cảm xúc (NC GĐ 7 — Ưu tiên 2)

| Method | Đường dẫn | Body | Kết quả |
|---|---|---|---|
| `POST` | `/api/conversations/{id}/messages/{messageId}/reactions` | `{ "reaction": "like" }` | `200 MessageDto` |
| `DELETE` | `/api/conversations/{id}/messages/{messageId}/reactions/{reaction}` | — | `200 MessageDto` |

- Danh mục cố định 6 khoá: `like` 👍, `love` ❤️, `haha` 😂, `wow` 😮, `sad` 😢, `angry` 😡.
  API dùng **khoá chữ**; server quy đổi sang emoji lưu DB (quyết định D2).
- Một người được thả **nhiều loại** trên cùng một tin, mỗi loại một lần; thêm/bỏ lặp lại là idempotent
  (không phát lại sự kiện khi không có thay đổi).
- Mọi thành viên còn hiệu lực đều thả được; tin đã thu hồi → `400 MESSAGE_ALREADY_DELETED`.
- Lỗi khác: `CONVERSATION_NOT_FOUND` (404), `MESSAGE_NOT_FOUND` (404), `MESSAGE_REACTION_INVALID` (400 — khoá ngoài danh mục).

### 8.16. Sự kiện real-time và DTO bổ sung (NC GĐ 7 — Ưu tiên 2)

**Trường bổ sung trong DTO**

| DTO | Trường mới | Ý nghĩa |
|---|---|---|
| `MessageDto` | `replyTo`, `forwardedFrom`, `editedAt`, `deletedAt`, `reactions[]` | Dữ liệu hiển thị của NC Ưu tiên 2 |
| `ReactionSummaryDto` | `{ reaction, emoji, count, reactedByMe }` | Một dòng cho mỗi loại cảm xúc đang có |
| `ConversationLastMessageDto` | `isDeleted` | Tin cuối đã bị thu hồi — client hiện "Tin nhắn đã bị thu hồi" thay vì nội dung rỗng |

**SignalR** — hub `/hub/chat`, không thêm method client → server:

| Hướng | Tên | Payload | Gửi cho |
|---|---|---|---|
| Server → client | `MessageUpdated` | `MessageDto` đầy đủ | group `conversation:{id}` + `user:{id}` từng thành viên còn hiệu lực |
| Server → client | `PinnedMessagesChanged` | `{ conversationId, pinned: PinnedMessageDto[] }` | như trên (dùng từ Đợt B — ghim) |

- Client thay tin theo `id` trong cache; không cần tải lại trang. Sửa/thu hồi/cảm xúc **không** tạo push.

### 8.17. Mã lỗi mới (NC GĐ 7 — Ưu tiên 2, Đợt A)

| Mã | HTTP | Ý nghĩa |
|---|---|---|
| `MESSAGE_NOT_OWNED` | 403 | Không phải người gửi nên không sửa/thu hồi được |
| `MESSAGE_EDIT_WINDOW_EXPIRED` | 403 | Quá 24 giờ kể từ khi gửi |
| `MESSAGE_RECALL_WINDOW_EXPIRED` | 403 | Quá 24 giờ kể từ khi gửi |
| `MESSAGE_ALREADY_DELETED` | 400 | Tin đã bị thu hồi |
| `MESSAGE_NOT_EDITABLE` | 400 | Tin `System`/`Call` không sửa/thu hồi được |
| `MESSAGE_REACTION_INVALID` | 400 | Khoá cảm xúc không thuộc danh mục |
| `MESSAGE_PIN_LIMIT_REACHED` | 400 | Đã ghim đủ 10 tin (dùng từ Đợt B) |

### 8.18. Ghim tin nhắn (NC GĐ 7 — Ưu tiên 2)

| Method | Đường dẫn | Kết quả |
|---|---|---|
| `GET` | `/api/conversations/{id}/pinned` | `200 PinnedMessagesDto` |
| `PUT` | `/api/conversations/{id}/pinned/{messageId}` | `200 PinnedMessagesDto` |
| `DELETE` | `/api/conversations/{id}/pinned/{messageId}` | `200 PinnedMessagesDto` |

`PinnedMessagesDto`: `{ conversationId, pinned: [{ message: MessageDto, pinnedByUserId, pinnedByDisplayName, pinnedAt }] }`, mới ghim trước.
`PinnedMessageDto.message` là `MessageDto` đầy đủ (có trích dẫn/cảm xúc) để panel hiển thị nhất quán.

- Mọi **thành viên còn hiệu lực** được ghim/bỏ ghim; tối đa **10** tin/hội thoại → tin thứ 11 trả `400 MESSAGE_PIN_LIMIT_REACHED`.
- Ghim lặp lại / bỏ ghim tin không được ghim là **idempotent** (không phát lại sự kiện).
- Ghim tin đã thu hồi → `400 MESSAGE_ALREADY_DELETED`; tin không thuộc hội thoại → `404 MESSAGE_NOT_FOUND`; không phải thành viên → `404 CONVERSATION_NOT_FOUND`.
- Mỗi thay đổi phát `PinnedMessagesChanged` (mục 8.16); thu hồi một tin đang ghim → tự bỏ ghim + phát sự kiện.

### 8.19. Chuyển tiếp tin nhắn (NC GĐ 7 — Ưu tiên 2)

`POST /api/conversations/{targetId}/messages/forward`

| Trường | Kiểu | Bắt buộc | Ghi chú |
|---|---|---|---|
| `sourceMessageId` | uuid | ✔ | Tin gốc |
| `clientMessageId` | uuid | ✔ | Client sinh; chống chuyển tiếp trùng |

- `201 Created` tin mới trong hội thoại đích; `200 OK` nếu trùng `clientMessageId` (idempotent).
- **Server** sao chép nội dung + tệp, ghi `forwardedFromMessageId`; DTO có `forwardedFrom: { messageId, senderId, senderDisplayName }`.
- Người gọi phải là thành viên **cả** hội thoại nguồn lẫn đích. Không thuộc hội thoại nguồn → `404 MESSAGE_NOT_FOUND` (không lộ tin); không thuộc đích → `404 CONVERSATION_NOT_FOUND`.
- Tin nguồn đã thu hồi → `400 MESSAGE_ALREADY_DELETED`; tin `System`/`Call` → `400 MESSAGE_NOT_EDITABLE`.
- Phát `MessageCreated` tới thành viên hội thoại đích; push tuân theo tắt thông báo (mục 8.22).

### 8.20. Tìm kiếm tin nhắn (NC GĐ 7 — Ưu tiên 2)

| Method | Đường dẫn | Phạm vi |
|---|---|---|
| `GET` | `/api/conversations/{id}/messages/search?q=&limit=&cursor=` | Chỉ hội thoại đó (phải là thành viên) |
| `GET` | `/api/messages/search?q=&limit=&cursor=` | **Mọi** hội thoại người dùng còn là thành viên |

- `q` bắt buộc **2–100 ký tự** (ngắn/dài hơn → `400 VALIDATION_FAILED`); `limit` ≤ 50, mặc định 20; con trỏ `(created_at, id)`, mới nhất trước.
- Khớp **một phần từ**, **không phân biệt dấu** và chữ hoa/thường (`hop` khớp `họp`) — dùng `pg_trgm` + `immutable_unaccent` (quyết định D5).
- Không bao giờ trả tin ở hội thoại người dùng không còn là thành viên, kể cả bài `Public`; tin đã thu hồi bị loại.
- Trả `PagedList<MessageSearchResultDto>`: `{ messageId, conversationId, conversationTitle, conversationType, peerDisplayName, senderId, senderDisplayName, contentPreview, createdAt }`
  (`peerDisplayName` chỉ có với chat 1–1; nhóm dùng `conversationTitle`).
- Người ngoài tìm trong hội thoại → `404 CONVERSATION_NOT_FOUND` (không dùng 403).

### 8.21. Ngữ cảnh quanh một tin (NC GĐ 7 — Ưu tiên 2)

`GET /api/conversations/{id}/messages/{messageId}/context?limit=31`

- `limit` trong 3–51 (mặc định 31): khoảng một nửa tin **trước**, một nửa tin **sau** tin đích.
- `200 MessageContextDto`: `{ items: MessageDto[] (tăng dần theo thời gian), hasMoreBefore, hasMoreAfter }`.
- Dùng khi bấm kết quả tìm kiếm để mở đúng tin mà không phải tải ngược nhiều trang.
- Tin không thuộc hội thoại → `404 MESSAGE_NOT_FOUND`; không phải thành viên → `404 CONVERSATION_NOT_FOUND`.

### 8.22. Tắt thông báo từng cuộc (NC GĐ 7 — Ưu tiên 2)

`PUT /api/conversations/{id}/mute`

| Trường | Kiểu | Bắt buộc | Ghi chú |
|---|---|---|---|
| `duration` | string | ✔ | `Off` · `OneHour` · `EightHours` · `OneWeek` · `Forever` |

- `200 MuteStateDto`: `{ isMuted, mutedUntil }`. `Off` → `mutedUntil = null`; `Forever` lưu mốc `9999-12-31T23:59:59Z`.
- **Chỉ chặn push** (FCM) — tin real-time và số tin chưa đọc **không đổi** (quyết định D7).
- `ConversationDto` và `ConversationSummaryDto` thêm `isMuted`, `mutedUntil` **của chính người gọi**.
- Lỗi: `VALIDATION_FAILED` (400 — thời hạn lạ), `CONVERSATION_NOT_FOUND` (404).

---

## 9. Gọi trực tuyến (Giai đoạn 4)

> Trạng thái: **đề xuất — bắt đầu cài đặt (2026-10-19)**. Quyết định đã chốt: **D1** gọi nhóm dùng `conversation` loại `Group` ẩn; **D2** bật TURN với IP LAN (`docs/tasks/phase4_calls.md` mục 2).
> Media đi **trực tiếp** client ↔ LiveKit; backend chỉ cấp token ngắn hạn và ghi trạng thái.

### 9.1. Quy ước riêng

| Quy ước | Giá trị |
|---|---|
| Người ngoài cuộc gọi | `404 CALL_NOT_FOUND` cho **mọi** endpoint (kể cả xin token) |
| Token | JWT **HS256** ký bằng `LiveKit:ApiSecret`, TTL **10 phút**, chứa `video.room`, `roomJoin`, `canPublish`; backend **không** trả `ApiSecret` |
| Cuộc gọi nhóm | **Quyết định D6 (2026-10-23):** cuộc gọi luôn bắt đầu **trong một hội thoại** (1–1 hoặc nhóm thật, xem 8.2b). Mọi thành viên còn hiệu lực nhận `CallIncoming`. **Đã bỏ** nhánh tạo hội thoại nhóm "ẩn" từ `participantIds` |
| Đổ chuông | Tối đa **30 giây** (`CallRules.RingTimeoutSeconds`), quá hạn → `Missed`/`NoAnswer`. **Thực thi phía server** bởi `CallRingTimeoutService` (background, quét 5s/lần) — không phụ thuộc việc client còn mở tab; nghe sau 30s → `409 CALL_ALREADY_ENDED` |
| Ghi lịch sử | Khi kết thúc, ghi một tin nhắn loại `Call` vào hội thoại (`messages.call_id`) |

### 9.2. Bắt đầu cuộc gọi

`POST /api/calls`

| Trường | Kiểu | Bắt buộc | Ghi chú |
|---|---|---|---|
| `conversationId` | uuid | ✔ | Cuộc gọi diễn ra trong hội thoại này (1–1 hoặc nhóm); người gọi phải là thành viên |
| `type` | `Audio` \| `Video` | ✔ | |

- `201` + `CallDto`: `{ id, conversationId, type, status, roomName, initiatedBy, createdAt, answeredAt, endedAt, participants: [{ userId, displayName, status }] }`
- Phát SignalR `CallIncoming` tới `user:{id}` của từng người được mời (với nhóm: tất cả thành viên còn lại).
- Lỗi: `CALL_ALREADY_ACTIVE` (409), `CONVERSATION_NOT_FOUND` (404), `VALIDATION_FAILED` (400 — hội thoại chỉ còn 1 người).

### 9.3. Lấy token vào room

`POST /api/calls/{id}/token` → `200 { url, token, roomName, expiresAt }`

- `url` là WebSocket; lấy từ `LiveKit:PublicUrl` nếu được cấu hình, ngược lại chuyển `LiveKit:Host` sang `ws://`/`wss://`. Thiết bị thật/emulator **phải** đặt `LiveKit__PublicUrl` (ví dụ `ws://10.0.2.2:7880` hoặc `ws://192.168.1.43:7880`) vì `localhost` trên điện thoại trỏ về chính điện thoại (D7).
- Chỉ thành viên; cuộc gọi đã kết thúc → `409 CALL_ALREADY_ENDED`; LiveKit không chạy → `503 CALL_PROVIDER_UNAVAILABLE`.

### 9.4. Chấp nhận / từ chối / kết thúc

| Method | Đường dẫn | Kết quả |
|---|---|---|
| `POST` | `/api/calls/{id}/answer` | `status=InProgress`, participant `Joined`, `answered_at`; phát `CallUpdated` |
| `POST` | `/api/calls/{id}/decline` | participant `Declined`; nếu mọi người từ chối → `Declined` + `end_reason=Declined` |
| `POST` | `/api/calls/{id}/end` | `Completed`/`Cancelled`, `ended_at`, `duration_seconds`, gọi `EndRoomAsync`, ghi tin `Call`, phát `CallEnded` |
| `GET` | `/api/calls/{id}` | `CallDto` (chỉ thành viên) |
| `GET` | `/api/calls?cursor=&limit=` | `PagedList<CallSummaryDto>` — lịch sử của tôi, sắp `invited_at DESC` |

### 9.5. Webhook LiveKit

`POST /api/webhooks/livekit` — **AllowAnonymous**, bắt buộc xác thực JWT HS256 (`Authorization: Bearer …`) và đối chiếu claim `sha256` với hash của body.

> ⚠️ **Bẫy đã gặp thật (2026-10-19):** LiveKit gửi JWT **trần** trong header `Authorization` — **không** kèm tiền tố `Bearer `. Endpoint phải chấp nhận cả hai dạng, nếu không sẽ trả 401 và cuộc gọi không bao giờ được đóng từ webhook.
> Endpoint trả `200` cho mọi sự kiện đã xử lý (kể cả sự kiện không quan tâm, để LiveKit không gửi lại) và `401` khi chữ ký sai (kèm `{"error": "invalid_signature" | "missing_authorization"}` để chẩn đoán).

| Sự kiện | Xử lý |
|---|---|
| `room_started` | đặt `answered_at` nếu chưa có |
| `participant_joined` / `participant_left` | cập nhật `call_participants.status`, `joined_at`/`left_at` |
| `room_finished` | đặt `ended_at` + `duration_seconds`, `EndRoomAsync` (chống room treo), phát `CallEnded` |

### 9.6. SignalR (hub `/hub/chat`)

| Sự kiện | Gửi tới | Payload |
|---|---|---|
| `CallIncoming` | `user:{id}` người được mời | `CallDto` |
| `CallUpdated` | `user:{id}` thành viên | `CallDto` |
| `CallEnded` | `user:{id}` thành viên | `{ callId, status, endReason, durationSeconds }` |

> Gửi theo `user:{id}` (không dùng group hội thoại) — bài học GĐ 3: group hội thoại có thể chưa tồn tại lúc client kết nối.

### 9.7. Mã lỗi mới (GĐ 4)

| Mã | HTTP | Ý nghĩa |
|---|---|---|
| `CALL_NOT_FOUND` | 404 | Không có cuộc gọi **hoặc** không phải thành viên |
| `CALL_ALREADY_ACTIVE` | 409 | Hội thoại đang có cuộc gọi chưa kết thúc |
| `CALL_ALREADY_ENDED` | 409 | Cuộc gọi đã kết thúc |
| `CALL_NOT_RINGING` | 409 | Thao tác chỉ hợp lệ khi đang đổ chuông |
| `CALL_PROVIDER_UNAVAILABLE` | 503 | Không kết nối được LiveKit |

---

## 10. Thông báo và thiết bị (GĐ 6)

### 10.1. Đăng ký / hủy thiết bị nhận push

`POST /api/devices` (Bearer)

| Trường | Kiểu | Bắt buộc | Ghi chú |
|---|---|---|---|
| `pushToken` | string | ✔ | Token FCM/FID; 10–4096 ký tự |
| `platform` | `Web` \| `Android` \| `iOS` | ✔ | |
| `deviceName` | string | | Tối đa 100 ký tự |

→ `200 { id, platform, deviceName, lastSeenAt }`. Cùng token đăng nhập lại (kể cả tài khoản khác) sẽ được gán lại cho người dùng hiện tại.

`DELETE /api/devices?token=...` → `204` (đánh dấu `is_active=false`, idempotent).

### 10.2. Hộp thư thông báo

`GET /api/notifications?cursor=&limit=20` → `PagedList<NotificationDto>`:

```json
{
  "id": "uuid",
  "type": "FriendRequestReceived",
  "actorId": "uuid|null",
  "entityType": "Friendship",
  "entityId": "uuid|null",
  "data": "{\"type\":\"FriendRequest\"}",
  "isRead": false,
  "createdAt": "2026-10-27T...Z",
  "readAt": null
}
```

`POST /api/notifications/{id}/read` → `204` (idempotent; chỉ chủ sở hữu).

### 10.3. Push FCM

- Backend gửi FCM HTTP v1 qua service account (`Firebase:ServiceAccountJson` hoặc `Firebase:ServiceAccountPath`); **thiếu cấu hình thì bỏ qua push, không lỗi**.
- Push được gửi khi:
  - có tin nhắn mới (không tạo dòng `notifications`, data `type=Message`, `conversationId`, `messageId`);
  - lời mời kết bạn / chấp nhận (`FriendRequestReceived`/`FriendRequestAccepted`, có dòng `notifications`, data `type=FriendRequest`/`FriendAccepted`);
  - cuộc gọi đến (`type=CallIncoming`, `callId`, `conversationId`, `callType`; ưu tiên cao).
- App Flutter đăng ký token khi đăng nhập, xin quyền thông báo, bấm notification để mở chat/cuộc gọi. FCM là kênh **bổ sung** cho SignalR; quyền mic/camera vẫn xin khi vào cuộc gọi.

---

## 11. Lịch sử thay đổi

| Ngày | Thay đổi |
|---|---|
| 2026-10-15 | Chốt hợp đồng Giai đoạn 1: auth, hồ sơ, danh sách tài khoản admin, mã lỗi, quy ước SignalR |
| 2026-10-17 | Bổ sung `code` cho lỗi do framework sinh (400/401/403/404/500) — khắc phục lệch hợp đồng phát hiện khi kiểm chứng trên trình duyệt thật |
| 2026-10-17 | Thêm mục 7: hợp đồng mạng xã hội GĐ 2 (tìm người dùng, hồ sơ công khai, bạn bè, theo dõi — đã cài đặt; bài viết/thích/bình luận/media — đã chốt, chưa cài đặt) |
| 2026-10-18 | Sửa lỗi hợp đồng lỗi: body JSON hỏng trả `400 VALIDATION_FAILED` thay vì `500`; `title` suy ra từ `code` (mục 5) |
| 2026-10-18 | Thêm mục 8 (đề xuất): hợp đồng nhắn tin GĐ 3 — hội thoại 1–1, gửi/nhận tin, phân trang, ảnh trong tin, SignalR group theo hội thoại. Chờ chốt quyết định D1 |
| 2026-10-19 | Mục 8 đã cài đặt và kiểm chứng; `MessageCreated` phát thêm tới `user:{id}` của từng thành viên (sửa lỗi phải tải lại trang) |
| 2026-10-19 | Thêm mục 9 (đề xuất): hợp đồng gọi trực tuyến GĐ 4 — bắt đầu cuộc gọi, token LiveKit ngắn hạn, answer/decline/end, webhook, SignalR `CallIncoming`/`CallUpdated`/`CallEnded` |
| 2026-10-21 | Mục 9 đã cài đặt; **LiveKit gửi JWT trần** trong header `Authorization` (không có `Bearer `) — endpoint webhook chấp nhận cả hai dạng (nếu không sẽ 401) |
| 2026-10-21 | Bật TURN/UDP 3478 + dải relay 30500–30510; ghi chú `rtc.node_ip` phải là IP mà client tới được |
| 2026-10-23 | Thêm **mục 8.2b** `POST /api/conversations/group` (nhóm 3–10, chỉ bạn bè, người tạo `Owner`); danh sách hội thoại có thêm `memberCount`, `peer` nullable; thêm mã lỗi `MESSAGING_GROUP_TOO_FEW`/`MESSAGING_GROUP_TOO_MANY` |
| 2026-10-23 | **D6:** bỏ nhánh `participantIds` khỏi `POST /api/calls` — cuộc gọi luôn bắt đầu trong hội thoại (1–1 hoặc nhóm); đổ chuông quá 30s do **server** đóng (`Missed`/`NoAnswer`), nghe sau hạn → `409 CALL_ALREADY_ENDED` |
| 2026-10-24 | Thêm **mục 4 GĐ 5:** `POST /api/admin/users/{id}/lock` + `/unlock`, trường `lockedReason`/`lockedAt`, sự kiện `AccountLocked`; mã lỗi `USER_NOT_FOUND`, `ADMIN_CANNOT_LOCK_SELF`. Đăng nhập tài khoản bị khóa nay trả đúng `403 AUTH_ACCOUNT_LOCKED` (chỉ khi đúng mật khẩu) |
| 2026-10-24 | Migration **M5** `Admin_AddLockedByForeignKey`: FK `users.locked_by → users(id)` `ON DELETE SET NULL` + chỉ mục `ix_users_locked_by` (khớp `docs/03` mục 3.1); không đổi API |
| 2026-10-24 | **Tài khoản Admin ẩn:** không xuất hiện trong `GET /api/users?search=` (kể cả với Admin); hồ sơ/bài viết trang cá nhân của Admin → `404` cho người dùng thường; chặn kết bạn/theo dõi/chat 1–1/mời nhóm tới Admin từ người dùng thường. Trang `/api/admin/users` giữ nguyên |
| 2026-10-27 | **Mốc A:** thêm security scheme **Bearer JWT** vào OpenAPI (chỉ endpoint cần đăng nhập), chốt bản máy đọc `docs/openapi/comink.v1.json` (45 path); không đổi endpoint/DTO |
| 2026-10-27 | **GĐ 6:** thêm mục 10 — `POST/DELETE /api/devices`, `GET /api/notifications`, `POST /api/notifications/{id}/read`; push FCM cho tin nhắn/lời mời/cuộc gọi; migration `20261027_Notifications_InitialCreate` |
| 2026-11-17 | **GĐ 7 — Ưu tiên 1:** thêm `POST /api/conversations/{id}/read`; DTO hội thoại có `isOnline`/`lastSeenAt`/`lastReadMessageId`/`lastReadAt`; `unreadCount` là số thật; sự kiện `PresenceChanged`/`TypingChanged`/`MessagesRead` + hub method `Typing`/`Ping`; mã lỗi `MESSAGE_NOT_FOUND`; migration **M6** `Messaging_AddReadMarkers`. Đã kiểm chứng 28/28 bằng HTTP + 2 client SignalR |
