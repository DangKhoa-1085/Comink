# Giai đoạn 1 — Nền tảng và Tài khoản (Bảo mật CB)

> Tài liệu 05, phân rã từ `docs/04_roadmap.md` (Giai đoạn 1) theo yêu cầu ở `AGENTS.md` mục 7.3.
> **Mục tiêu:** người dùng đăng ký, đăng nhập và có hồ sơ; hệ thống phân quyền hoạt động.
> **Phụ thuộc:** Giai đoạn 0 — **đã hoàn tất** (xem `docs/tasks/phase0_remediation.md`).
> **Trạng thái:** chưa bắt đầu.
> **Ước lượng:** tuần 1–2 theo roadmap.

---

## 1. Phạm vi

Lấy từ `docs/01_COMINK_SPEC.md` mục 3.6 **[CB]** và 3.1 **[CB]** (phần hồ sơ):

- Đăng nhập (xác thực)
- Phân quyền (người dùng / quản trị viên)
- Kiểm tra dữ liệu đầu vào
- Hồ sơ cá nhân: xem và sửa tên hiển thị, tiểu sử
- Đa ngôn ngữ Việt/Anh chạy trên web
- SignalR kết nối được và xác thực bằng token (chưa cần nghiệp vụ)

**Ngoài phạm vi GĐ 1** (làm ở GĐ 7 theo bảng ưu tiên NC): xác minh email, đặt lại mật khẩu, chặn người dùng, quản lý phiên chi tiết, rate limiting, cài đặt quyền riêng tư.

---

## 2. Hợp đồng API cần chốt trước khi code

Theo `docs/02_tech_stack_decisions.md` mục 4.1 ("hợp đồng API trước, code sau"), chốt danh sách endpoint và mã lỗi **trước** khi cài đặt. Đề xuất ban đầu — cần nhóm xác nhận:

| Nhóm | Endpoint đề xuất | Ghi chú |
|---|---|---|
| Auth | `POST /api/auth/register` | đăng ký bằng email + user_name + mật khẩu |
| Auth | `POST /api/auth/login` | trả access token + refresh token |
| Auth | `POST /api/auth/refresh` | xoay refresh token (phát hiện dùng lại → thu hồi cả họ) |
| Auth | `POST /api/auth/logout` | thu hồi refresh token hiện tại |
| Profile | `GET /api/users/me` | hồ sơ + cài đặt của chính mình |
| Profile | `PATCH /api/users/me/profile` | sửa display_name, bio |
| Admin | `GET /api/admin/users` | danh sách tài khoản, phân trang con trỏ (kiểm tra role) |

**Mã lỗi dự kiến** (chuỗi tiếng Anh, client tự dịch — xem `AGENTS.md` mục 3):

`AUTH_INVALID_CREDENTIALS`, `AUTH_EMAIL_ALREADY_EXISTS`, `AUTH_USERNAME_ALREADY_EXISTS`, `AUTH_ACCOUNT_LOCKED`, `AUTH_REFRESH_TOKEN_INVALID`, `AUTH_REFRESH_TOKEN_REUSED`, `VALIDATION_FAILED`, `FORBIDDEN`.

**Sự kiện SignalR GĐ 1:** chưa có nghiệp vụ; chỉ cần hub xác thực được bằng `access_token` qua query string (`docs/02_tech_stack_decisions.md` mục 1.4).

> ❓ **Cần người dùng xác nhận:** danh sách endpoint và mã lỗi trên có được chốt không, trước khi tôi viết code GĐ 1.

---

## 3. Phân rã công việc

### 3.1. Backend — Domain

- [ ] Thực thể/giá trị cho tài khoản đã có `ApplicationUser` (Infrastructure, do Identity); bổ sung phần Domain cần thiết (ví dụ quy tắc trạng thái tài khoản) nếu cần
- [ ] Enum `AccountStatus` dùng chung (hiện là `string` + `CHECK` trong DB) — quyết định có tạo enum C# hay giữ hằng số

### 3.2. Backend — Application

- [ ] `Identity/` — use case đăng ký (`RegisterCommand` + validator)
- [ ] `Identity/` — use case đăng nhập (`LoginCommand`), phát access token + refresh token
- [ ] `Identity/` — use case làm mới token (`RefreshTokenCommand`), có phát hiện dùng lại token cũ và thu hồi cả `family_id`
- [ ] `Identity/` — use case đăng xuất (thu hồi refresh token)
- [ ] `Identity/` — `ITokenService` (sinh access token JWT) khai báo ở Application
- [ ] `Social/` — use case lấy và sửa hồ sơ cá nhân
- [ ] `Common/` — quy ước mã lỗi và exception nghiệp vụ (dùng chung ProblemDetails)
- [ ] DTO cho mọi request/response (không trả entity — `AGENTS.md` mục 8.1)

### 3.3. Backend — Infrastructure

- [ ] `JwtTokenService` sinh access token từ `Jwt:*` trong cấu hình
- [ ] Lưu refresh token **dạng băm** vào `refresh_tokens` (đã có schema từ M0)
- [ ] Cấu hình `AddAuthentication` + `AddJwtBearer` (đọc `Jwt:Issuer`, `Jwt:Audience`, `Jwt:Secret`)
- [ ] Chính sách phân quyền: policy cho role `Admin`
- [ ] Kiểm tra `account_status`: tài khoản `Locked` không đăng nhập được; thu hồi refresh token khi bị khóa

### 3.4. Backend — Api

- [ ] Nhóm endpoint Minimal API cho `auth` và `users/me` (vertical slice)
- [ ] Middleware ProblemDetails trả mã lỗi thống nhất cho exception nghiệp vụ và lỗi validate
- [ ] Hub SignalR đầu tiên, xác thực bằng access token qua query string `access_token`
- [ ] `Jwt__Secret` phải là chuỗi ngẫu nhiên ≥ 32 ký tự (hiện `.env` còn giá trị tạm — xem `docs/tasks/phase0_remediation.md` mục 4)

### 3.5. Web

- [ ] Trang đăng ký, đăng nhập, đăng xuất (React Hook Form + Zod)
- [ ] Lưu access token trong bộ nhớ, refresh token qua luồng an toàn; axios interceptor tự làm mới khi 401
- [ ] `Zustand` cho trạng thái phiên; `TanStack Query` cho dữ liệu server
- [ ] Trang hồ sơ: xem và sửa `display_name`, `bio`
- [ ] Route bảo vệ: chưa đăng nhập thì chuyển về trang đăng nhập; kiểm tra role cho khu vực `/admin`
- [ ] Mọi chuỗi hiển thị qua `i18next` (`vi`, `en`) — không hard-code
- [ ] Mọi màn hình có đủ trạng thái **tải / rỗng / lỗi**
- [ ] Hiển thị lỗi theo mã lỗi backend (dịch ở client)

### 3.6. Cơ sở dữ liệu

- [ ] Không cần migration mới: `users`, `user_profiles`, `user_settings`, `refresh_tokens` đã có từ M0
- [ ] Kiểm tra lại các chỉ mục phục vụ đăng nhập: `users.normalized_email`, `users.normalized_user_name`, `refresh_tokens.token_hash` (đã có)
- [ ] Xác nhận có cần bổ sung cột/bảng nào không (nếu có → cập nhật `docs/03_database_design.md` cùng lúc)

### 3.7. Kiểm thử

- [ ] Unit test: đăng nhập sai mật khẩu, tài khoản bị khóa, refresh token hết hạn, **phát hiện dùng lại refresh token → thu hồi cả họ**
- [ ] Unit test: phân quyền (người thường gọi endpoint admin → 403)
- [ ] Integration test (Testcontainers PostgreSQL + Redis): luồng đăng ký → đăng nhập → gọi endpoint cần xác thực
- [ ] Thay thế 3 test `UnitTest1` placeholder hiện có
- [ ] Thử thủ công bằng **ít nhất 2 tài khoản** (một `User`, một `Admin`) theo `AGENTS.md` mục 16

### 3.8. Tài liệu

- [ ] Cập nhật `docs/03_database_design.md` nếu schema đổi
- [ ] Ghi danh sách endpoint + mã lỗi + sự kiện SignalR vào tài liệu hợp đồng API (chưa có file — cần tạo, ví dụ `docs/05_api_contract.md`)
- [ ] Cập nhật `README.md` nếu thêm bước chạy mới

---

## 4. Tiêu chí hoàn thành (lấy từ `docs/04_roadmap.md` Giai đoạn 1)

- [ ] Đăng ký, đăng nhập, đăng xuất, làm mới token hoạt động trên web
- [ ] Phân quyền hai vai trò `User` và `Admin`; endpoint chỉ-admin bị chặn với người thường
- [ ] Kiểm tra dữ liệu đầu vào ở backend cho mọi endpoint hiện có (trả mã lỗi thống nhất)
- [ ] Hồ sơ cá nhân: xem và sửa tên hiển thị, tiểu sử
- [ ] Có tài khoản admin đầu tiên (seed từ cấu hình) — **đã có sẵn từ GĐ 0**, cần kiểm chứng lại
- [ ] Khung đa ngôn ngữ Việt/Anh chạy trên web (mọi chuỗi hiển thị đi qua i18n) — khung có từ GĐ 0, cần bổ sung chuỗi mới
- [ ] SignalR kết nối được và xác thực bằng token (chưa cần nghiệp vụ)
- [ ] Có test cho đăng nhập và phân quyền; bí mật/khóa API không nằm trong Git

Cộng thêm **Definition of Done** chung ở `docs/04_roadmap.md` mục 4 và `AGENTS.md` mục 16.

---

## 5. Rủi ro và lưu ý

| Rủi ro | Cách xử lý |
|---|---|
| Lộ `Jwt__Secret` | Sinh chuỗi ngẫu nhiên ≥ 32 ký tự, chỉ để trong user-secrets/`.env`; hook `pre-commit` đã chặn commit bí mật |
| Refresh token bị dùng lại (đánh cắp) | Thiết kế `family_id` ngay từ đầu: dùng lại token cũ → thu hồi toàn bộ họ token |
| Đổi hợp đồng API giữa chừng làm vỡ web | Chốt endpoint + DTO + mã lỗi trước khi code (mục 2) |
| Trùng lặp với công việc của Dev kia | Dev A phụ trách Tài khoản/bảo mật (xem `docs/04_roadmap.md` mục 2) |

---

## 6. Tài liệu liên quan

- `docs/04_roadmap.md` — Giai đoạn 1 và Definition of Done
- `docs/01_COMINK_SPEC.md` — mục 3.1, 3.6
- `docs/02_tech_stack_decisions.md` — mục 1.4 (SignalR), 2.1 (backend), 4.3 (bảo mật)
- `docs/03_database_design.md` — mục 3.1 (bảng Identity)
- `docs/tasks/phase0_remediation.md` — việc chuyển tiếp từ GĐ 0
