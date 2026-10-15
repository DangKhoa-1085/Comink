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
| `VALIDATION_FAILED` | 400 | Dữ liệu đầu vào không hợp lệ (kèm `errors`) |
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

## 7. Lịch sử thay đổi

| Ngày | Thay đổi |
|---|---|
| 2026-10-15 | Chốt hợp đồng Giai đoạn 1: auth, hồ sơ, danh sách tài khoản admin, mã lỗi, quy ước SignalR |
