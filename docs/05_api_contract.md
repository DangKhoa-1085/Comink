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

### `GET /api/admin/users` — danh sách tài khoản

Tham số: `cursor`, `limit`, `search` (tùy chọn, tìm theo email/user_name/display_name).

Phản hồi `200 OK`: phân trang `AdminUserSummary`:

```json
{
  "id": "0199...",
  "userName": "khoa.nguyen",
  "email": "khoa@example.com",
  "displayName": "Nguyễn Khoa",
  "accountStatus": "Active",
  "createdAt": "2026-10-15T10:00:00Z"
}
```

> Khóa/mở khóa tài khoản là **Giai đoạn 5**, chưa thuộc GĐ 1.

---

## 5. Mã lỗi

| Mã | HTTP | Ý nghĩa |
|---|---|---|
| `VALIDATION_FAILED` | 400 | Dữ liệu đầu vào không hợp lệ (kèm `errors`) — **hoặc** body JSON không đọc được (sai định dạng, sai kiểu, thiếu trường bắt buộc); trường hợp này không có `errors` |
| `AUTH_INVALID_CREDENTIALS` | 401 | Sai tài khoản hoặc mật khẩu |
| `AUTH_ACCOUNT_LOCKED` | 403 | Tài khoản bị admin khóa (`account_status = Locked`) |
| `AUTH_REFRESH_TOKEN_INVALID` | 401 | Refresh token không hợp lệ/hết hạn/đã thu hồi |
| `AUTH_REFRESH_TOKEN_REUSED` | 401 | Phát hiện dùng lại refresh token cũ; đã thu hồi cả họ |
| `AUTH_EMAIL_ALREADY_EXISTS` | 409 | Email đã được dùng |
| `AUTH_USERNAME_ALREADY_EXISTS` | 409 | Tên đăng nhập đã được dùng |
| `FORBIDDEN` | 403 | Không đủ quyền |
| `UNAUTHORIZED` | 401 | Thiếu/token không hợp lệ |
| `NOT_FOUND` | 404 | Không tìm thấy tài nguyên |
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

### 7.3. Theo dõi một chiều — `/api/follows` và `/api/users/{id}/followers|following`

| Method | Đường dẫn | Mục đích |
|---|---|---|
| `POST` | `/api/follows/{userId}` | Theo dõi → `204` (idempotent) |
| `DELETE` | `/api/follows/{userId}` | Bỏ theo dõi → `204` (idempotent) |
| `GET` | `/api/users/{id}/followers` | Người theo dõi |
| `GET` | `/api/users/{id}/following` | Đang theo dõi |

Tự theo dõi → `400 SOCIAL_CANNOT_FOLLOW_SELF`; người dùng không tồn tại → `404 NOT_FOUND`.
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
> Phạm vi CB: **chỉ chat riêng 1–1**. Chat nhóm, trạng thái online, "đang nhập", đã gửi/đã đọc, trả lời, thu hồi/sửa, tìm kiếm, ghim, cảm xúc là **[NC]** — chưa có trong hợp đồng này.

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
- Trả `ConversationDto`: `{ id, type, title?, avatarUrl?, createdAt, lastMessageAt?, members: [{ userId, displayName, userName, avatarUrl }], lastMessage?: MessageDto }`
- Lỗi: `MESSAGING_CANNOT_MESSAGE_SELF` (400), `MESSAGING_NOT_FRIENDS` (403 — nếu D1 chọn "chỉ bạn bè"), `USER_NOT_FOUND` (404).

### 8.3. Danh sách hội thoại

`GET /api/conversations?limit=&cursor=`

- Sắp xếp `last_message_at DESC` (`NULLS LAST` cho hội thoại chưa có tin).
- Trả `PagedList<ConversationSummaryDto>`: `{ id, type, title?, peer: { userId, displayName, avatarUrl }, lastMessage?: { id, senderId, preview, type, createdAt }, unreadCount }` — `unreadCount = 0` ở CB.

### 8.4. Chi tiết hội thoại

`GET /api/conversations/{id}` → `ConversationDto` (cùng hình dạng 8.2). Người ngoài → `404 CONVERSATION_NOT_FOUND`.

### 8.5. Lịch sử tin nhắn

`GET /api/conversations/{id}/messages?limit=&before=|after=`

| Tham số | Ý nghĩa |
|---|---|
| `before` | Con trỏ `(created_at, id)` — lấy **cũ hơn** (cuộn lên). Trả về mới nhất trước |
| `after` | Con trỏ — lấy **mới hơn** (đồng bộ sau khi mất kết nối). Trả về cũ nhất trước |
| (không có) | 50 tin mới nhất |

- Gửi cả `before` và `after` → `400 VALIDATION_FAILED`.
- Trả `PagedList<MessageDto>`: `{ id, conversationId, senderId, type, content?, attachments: [{ mediaId, url, thumbnailUrl?, width?, height? }], clientMessageId, createdAt }`

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
| Tín hiệu client → server | Chưa có ở CB ("đang nhập"/presence là **[NC]**) |
| Đồng bộ sau khi mất kết nối | Client kết nối lại → gọi `GET /api/conversations/{id}/messages?after={lastMessageId}` và `GET /api/conversations` để làm mới |
| Quyền | Người ngoài không nằm trong group hội thoại ⇒ **không nhận** `MessageCreated`; mọi truy cập REST trả 404 |

### 8.8. Mã lỗi mới (GĐ 3)

| Mã | HTTP | Ý nghĩa |
|---|---|---|
| `CONVERSATION_NOT_FOUND` | 404 | Không tìm thấy hội thoại **hoặc** người dùng không phải thành viên |
| `MESSAGING_CANNOT_MESSAGE_SELF` | 400 | Không thể tự mở hội thoại với chính mình |
| `MESSAGING_NOT_FRIENDS` | 403 | Chưa là bạn bè nên chưa nhắn tin được *(chỉ khi D1 chọn "chỉ bạn bè")* |
| `MESSAGE_CONTENT_REQUIRED` | 400 | Tin không có nội dung và cũng không có tệp |
| `MESSAGE_TOO_MANY_ATTACHMENTS` | 400 | Quá 10 tệp trong một tin |
| `MESSAGE_CLIENT_ID_REQUIRED` | 400 | Thiếu `clientMessageId` |
| `MESSAGE_INVALID_CURSOR` | 400 | Con trỏ phân trang không hợp lệ |

---

## 9. Gọi trực tuyến (Giai đoạn 4)

> Trạng thái: **đề xuất — bắt đầu cài đặt (2026-10-19)**. Quyết định đã chốt: **D1** gọi nhóm dùng `conversation` loại `Group` ẩn; **D2** bật TURN với IP LAN (`docs/tasks/phase4_calls.md` mục 2).
> Media đi **trực tiếp** client ↔ LiveKit; backend chỉ cấp token ngắn hạn và ghi trạng thái.

### 9.1. Quy ước riêng

| Quy ước | Giá trị |
|---|---|
| Người ngoài cuộc gọi | `404 CALL_NOT_FOUND` cho **mọi** endpoint (kể cả xin token) |
| Token | JWT **HS256** ký bằng `LiveKit:ApiSecret`, TTL **10 phút**, chứa `video.room`, `roomJoin`, `canPublish`; backend **không** trả `ApiSecret` |
| Cuộc gọi nhóm | Không có `conversationId` → backend tạo conversation `Group` **ẩn** (không hiện trong danh sách chat) từ `participantIds` (≥2, phải là bạn bè) |
| Đổ chuông | Tối đa **30 giây**, quá hạn → `NoAnswer` |
| Ghi lịch sử | Khi kết thúc, ghi một tin nhắn loại `Call` vào hội thoại (`messages.call_id`) |

### 9.2. Bắt đầu cuộc gọi

`POST /api/calls`

| Trường | Kiểu | Bắt buộc | Ghi chú |
|---|---|---|---|
| `conversationId` | uuid? | ✖ | Có → cuộc gọi 1–1 trong hội thoại đó |
| `participantIds` | uuid[]? | ✖ | Có → cuộc gọi nhóm (≥2 người, phải là bạn bè); bỏ trống `conversationId` |
| `type` | `Audio` \| `Video` | ✔ | |

- `201` + `CallDto`: `{ id, conversationId, type, status, roomName, initiatedBy, createdAt, answeredAt, endedAt, participants: [{ userId, displayName, status }] }`
- Phát SignalR `CallIncoming` tới `user:{id}` của từng người được mời.
- Lỗi: `CALL_ALREADY_ACTIVE` (409), `CONVERSATION_NOT_FOUND` (404), `MESSAGING_NOT_FRIENDS` (403).

### 9.3. Lấy token vào room

`POST /api/calls/{id}/token` → `200 { url, token, roomName, expiresAt }`

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

## 10. Lịch sử thay đổi

| Ngày | Thay đổi |
|---|---|
| 2026-10-15 | Chốt hợp đồng Giai đoạn 1: auth, hồ sơ, danh sách tài khoản admin, mã lỗi, quy ước SignalR |
| 2026-10-17 | Bổ sung `code` cho lỗi do framework sinh (400/401/403/404/500) — khắc phục lệch hợp đồng phát hiện khi kiểm chứng trên trình duyệt thật |
| 2026-10-17 | Thêm mục 7: hợp đồng mạng xã hội GĐ 2 (tìm người dùng, hồ sơ công khai, bạn bè, theo dõi — đã cài đặt; bài viết/thích/bình luận/media — đã chốt, chưa cài đặt) |
| 2026-10-18 | Sửa lỗi hợp đồng lỗi: body JSON hỏng trả `400 VALIDATION_FAILED` thay vì `500`; `title` suy ra từ `code` (mục 5) |
| 2026-10-18 | Thêm mục 8 (đề xuất): hợp đồng nhắn tin GĐ 3 — hội thoại 1–1, gửi/nhận tin, phân trang, ảnh trong tin, SignalR group theo hội thoại. Chờ chốt quyết định D1 |
| 2026-10-19 | Mục 8 đã cài đặt và kiểm chứng; `MessageCreated` phát thêm tới `user:{id}` của từng thành viên (sửa lỗi phải tải lại trang) |
| 2026-10-19 | Thêm mục 9 (đề xuất): hợp đồng gọi trực tuyến GĐ 4 — bắt đầu cuộc gọi, token LiveKit ngắn hạn, answer/decline/end, webhook, SignalR `CallIncoming`/`CallUpdated`/`CallEnded` |
