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

## 8. Lịch sử thay đổi

| Ngày | Thay đổi |
|---|---|
| 2026-10-15 | Chốt hợp đồng Giai đoạn 1: auth, hồ sơ, danh sách tài khoản admin, mã lỗi, quy ước SignalR |
| 2026-10-17 | Bổ sung `code` cho lỗi do framework sinh (400/401/403/404/500) — khắc phục lệch hợp đồng phát hiện khi kiểm chứng trên trình duyệt thật |
| 2026-10-17 | Thêm mục 7: hợp đồng mạng xã hội GĐ 2 (tìm người dùng, hồ sơ công khai, bạn bè, theo dõi — đã cài đặt; bài viết/thích/bình luận/media — đã chốt, chưa cài đặt) |
