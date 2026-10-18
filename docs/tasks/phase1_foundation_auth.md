# Giai đoạn 1 — Nền tảng và Tài khoản (Bảo mật CB)

> Tài liệu 05, phân rã từ `docs/04_roadmap.md` (Giai đoạn 1) theo yêu cầu ở `AGENTS.md` mục 7.3.
> **Mục tiêu:** người dùng đăng ký, đăng nhập và có hồ sơ; hệ thống phân quyền hoạt động.
> **Phụ thuộc:** Giai đoạn 0 — **đã hoàn tất** (xem `docs/tasks/phase0_remediation.md`).
> **Trạng thái:** **HOÀN TẤT** (backend + web) — đã kiểm chứng trên **trình duyệt thật**.
>
> Chạy lại toàn bộ trong đợt kiểm chứng trình duyệt (2026-10-17):
> - `dotnet build`: 7/7 project, 0 warning, 0 error; `dotnet test`: **29 test pass** (15 unit + 13 tích hợp trên PostgreSQL thật + 1 domain).
> - `npm run lint`: 0 warning, 0 error; `npm run build`: thành công.
> - **Trình duyệt thật** (Chrome 155 qua BrowserSkill, cửa sổ agent, hạ tầng Docker + backend + Vite đang chạy): đăng ký → đăng nhập → sửa hồ sơ → tải lại trang → đổi ngôn ngữ → đăng xuất → route bảo vệ; và **phiên Admin**: đăng nhập → `/admin/users` → tìm kiếm → trạng thái rỗng → đăng xuất. Chi tiết ở mục 3.5.
> - **Phân quyền backend**: `GET /api/admin/users` bằng token `User` → `403 FORBIDDEN`; **SignalR** `/hub/chat` negotiate: không token → `401`, token hợp lệ → `200`, token sai → `401`.
> - **2 lỗi phát hiện trong đợt này đã sửa** (lỗi framework thiếu `code`, số nhiều tiếng Anh) — xem mục 3.9.
>
> Kết quả của các đợt trước vẫn giữ nguyên: **39/39 kiểm tra end-to-end** qua proxy Vite, **27/27 kiểm tra render** (SSR), **5/5 kiểm tra SignalR**.
>
> **Giới hạn đã biết:** đã đóng — trước đây chưa kiểm chứng được trên trình duyệt thật vì máy chưa có BrowserSkill; nay đã cài `bsk` CLI 0.3.3 + extension và kiểm chứng xong.
>
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

- [x] Thực thể/giá trị cho tài khoản đã có `ApplicationUser` (Infrastructure, do Identity); bổ sung phần Domain cần thiết (ví dụ quy tắc trạng thái tài khoản) nếu cần
- [x] Enum `AccountStatus` dùng chung (hiện là `string` + `CHECK` trong DB) — quyết định có tạo enum C# hay giữ hằng số

### 3.2. Backend — Application

- [x] `Identity/` — use case đăng ký (`RegisterCommand` + validator)
- [x] `Identity/` — use case đăng nhập (`LoginCommand`), phát access token + refresh token
- [x] `Identity/` — use case làm mới token (`RefreshTokenCommand`), có phát hiện dùng lại token cũ và thu hồi cả `family_id`
- [x] `Identity/` — use case đăng xuất (thu hồi refresh token)
- [x] `Identity/` — `ITokenService` (sinh access token JWT) khai báo ở Application
- [x] `Social/` — use case lấy và sửa hồ sơ cá nhân
- [x] `Common/` — quy ước mã lỗi và exception nghiệp vụ (dùng chung ProblemDetails)
- [x] DTO cho mọi request/response (không trả entity — `AGENTS.md` mục 8.1)

### 3.3. Backend — Infrastructure

- [x] `JwtTokenService` sinh access token từ `Jwt:*` trong cấu hình
- [x] Lưu refresh token **dạng băm** vào `refresh_tokens` (đã có schema từ M0)
- [x] Cấu hình `AddAuthentication` + `AddJwtBearer` (đọc `Jwt:Issuer`, `Jwt:Audience`, `Jwt:Secret`)
- [x] Chính sách phân quyền: policy cho role `Admin`
- [x] Kiểm tra `account_status`: tài khoản `Locked` không đăng nhập được; thu hồi refresh token khi bị khóa

### 3.4. Backend — Api

- [x] Nhóm endpoint Minimal API cho `auth` và `users/me` (vertical slice)
- [x] Middleware ProblemDetails trả mã lỗi thống nhất cho exception nghiệp vụ và lỗi validate
- [x] Hub SignalR đầu tiên, xác thực bằng access token qua query string `access_token`
- [x] `Jwt__Secret` phải là chuỗi ngẫu nhiên ≥ 32 ký tự (hiện `.env` còn giá trị tạm — xem `docs/tasks/phase0_remediation.md` mục 4)

### 3.5. Web

- [x] Trang đăng ký, đăng nhập, đăng xuất (React Hook Form + Zod)
- [x] Lưu access token trong bộ nhớ, refresh token qua luồng an toàn; axios interceptor tự làm mới khi 401
- [x] `Zustand` cho trạng thái phiên; `TanStack Query` cho dữ liệu server
- [x] Trang hồ sơ: xem và sửa `display_name`, `bio`
- [x] Route bảo vệ: chưa đăng nhập thì chuyển về trang đăng nhập; kiểm tra role cho khu vực `/admin`
- [x] Mọi chuỗi hiển thị qua `i18next` (`vi`, `en`) — không hard-code
- [x] Mọi màn hình có đủ trạng thái **tải / rỗng / lỗi**
- [x] Hiển thị lỗi theo mã lỗi backend (dịch ở client)

**Kiểm chứng trên trình duyệt thật (2026-10-17):** đã cài `bsk` CLI 0.3.3 + extension BrowserSkill và chạy hết luồng bằng Chrome 155 trong cửa sổ agent. Kết quả từng bước:

| Bước | Kết quả quan sát được |
|---|---|
| Trang chủ | Render đúng tiếng Việt, gọi `/health` báo "Backend đang hoạt động (healthy)" |
| Đăng ký | Tạo tài khoản mới thành công, toast "Tạo tài khoản thành công", chuyển về trang chủ, header hiện tên người dùng |
| Trạng thái tải | Nút đổi thành "Đang tạo tài khoản…" và bị vô hiệu hoá trong lúc gửi |
| Hồ sơ | Hiển thị email/tên đăng nhập/vai trò/ngày tham gia; sửa `display_name` + `bio` → toast "Đã cập nhật hồ sơ", header đổi tên theo |
| Tải lại trang | Vẫn đăng nhập và dữ liệu hồ sơ vừa sửa được tải lại từ backend. Log mạng: `401 GET /api/users/me` → `200 POST /api/auth/refresh` → `200 GET /api/users/me` (access token chỉ nằm trong bộ nhớ) |
| Đa ngôn ngữ | `vi → en` đổi toàn bộ giao diện; thông báo lỗi đang hiển thị cũng được dịch lại; `en → vi` đổi ngược lại đúng |
| Đăng xuất | Về trang đăng nhập, toast "Signed out", refresh token bị thu hồi trong `refresh_tokens` |
| Route bảo vệ | `/profile` khi chưa đăng nhập → chuyển về `/login` |
| Chặn khu vực quản trị | Tài khoản `User` vào `/admin/users` bị đưa về trang chủ, menu "Admin" không hiển thị |
| Sai mật khẩu | Hiện đúng thông báo đã dịch: "Incorrect email/username or password." |
| Validate phía client | Gửi form đăng ký rỗng → 4 thông báo lỗi đã dịch, không gửi request |
| Trùng email / trùng username | `409` + thông báo dịch đúng ("This email is already in use." / "Tên đăng nhập này đã được sử dụng.") |
| Trang 404 | Route không tồn tại → trang 404 với link "Back to home" |
| Giao diện sáng/tối | Nút đổi giao diện chuyển sang dark, màu theo theme |
| **Phiên Admin** | Đăng nhập `admin@comink.local` → menu "Admin" xuất hiện → `/admin/users` hiển thị bảng 5 tài khoản; mạng: `200 POST /api/auth/login` → `200 GET /api/users/me` → `200 GET /api/admin/users?limit=10` |
| Tìm kiếm quản trị | Nhập `khoa` → còn 1 dòng; nhập chuỗi không tồn tại → "No accounts found" |
| Đăng xuất admin | Về trang đăng nhập, refresh token của admin bị thu hồi |

Vẫn nên xem lại bố cục/responsive bằng mắt trên vài kích thước cửa sổ trước buổi demo — đây là phần không được kiểm chứng tự động.

**Chạy lại sau khi sửa 2 lỗi ở mục 3.9 (cùng ngày):** đăng nhập lại `admin@comink.local` trên backend đã build lại → `/admin/users` vẫn hiển thị đúng danh sách và tìm kiếm; dòng đếm hiển thị `Đang hiển thị 1 tài khoản` (vi) và `Showing 1 account` (en) — hết lỗi số nhiều.

**Phát sinh khi làm (đã khắc phục):**
- `@microsoft/signalr` bị gỡ khỏi `web/package.json` vì Giai đoạn 1 chưa dùng tới (sẽ thêm lại ở Giai đoạn 3 khi nối hub thật).
- `Jwt__Secret` trong `.env` đã được xoay thành chuỗi ngẫu nhiên 48 ký tự.
- Cấu hình i18n được bổ sung `supportedLngs`, `nonExplicitSupportedLngs`, `load: 'languageOnly'` và ghim khoá lưu vào `comink.language`.

### 3.6. Cơ sở dữ liệu

- [x] Không cần migration mới: `users`, `user_profiles`, `user_settings`, `refresh_tokens` đã có từ M0
- [x] Kiểm tra lại các chỉ mục phục vụ đăng nhập: `users.normalized_email`, `users.normalized_user_name`, `refresh_tokens.token_hash` (đã có)
- [x] Xác nhận có cần bổ sung cột/bảng nào không (nếu có → cập nhật `docs/03_database_design.md` cùng lúc)

### 3.7. Kiểm thử

- [x] Unit test: đăng nhập sai mật khẩu, tài khoản bị khóa, refresh token hết hạn, **phát hiện dùng lại refresh token → thu hồi cả họ**
- [x] Unit test: phân quyền (người thường gọi endpoint admin → 403)
- [x] Integration test (Testcontainers PostgreSQL + Redis): luồng đăng ký → đăng nhập → gọi endpoint cần xác thực
- [x] Thay thế 3 test `UnitTest1` placeholder hiện có
- [x] Thử thủ công bằng **ít nhất 2 tài khoản** (một `User`, một `Admin`) theo `AGENTS.md` mục 16

### 3.8. Tài liệu

- [x] Cập nhật `docs/03_database_design.md` nếu schema đổi
- [x] Ghi danh sách endpoint + mã lỗi + sự kiện SignalR vào tài liệu hợp đồng API (chưa có file — cần tạo, ví dụ `docs/05_api_contract.md`)
- [x] Cập nhật `README.md` nếu thêm bước chạy mới

### 3.9. Lỗi phát hiện khi kiểm chứng trên trình duyệt và cách sửa

Hai lỗi nhỏ phát hiện trong đợt kiểm chứng trình duyệt (2026-10-17) — không lỗi nào chặn demo, nhưng đều lệch với tài liệu nên đã sửa:

**1) Lỗi do framework sinh thiếu trường `code`.** `401` (chưa xác thực), `403` (thiếu quyền), `404` (không tìm thấy) do middleware của ASP.NET Core sinh ra **không** đi qua `ApiResults` nên chỉ có `type/title/status/traceId`, thiếu `code` — trái với `docs/05_api_contract.md` mục 5 (đáng lẽ là `UNAUTHORIZED` / `FORBIDDEN` / `NOT_FOUND`). Hệ quả: `lib/http.ts` không tìm thấy `code` nên gán `INTERNAL_ERROR`, và client hiển thị "Máy chủ gặp lỗi" thay vì "Bạn không có quyền…" — dù `web/src/locales/*.json` đã có sẵn 3 bản dịch đó.

- Sửa: `Program.cs` — `AddProblemDetails` được cấu hình `CustomizeProblemDetails` để bổ sung `code` theo mã HTTP (400 → `VALIDATION_FAILED`, 401 → `UNAUTHORIZED`, 403 → `FORBIDDEN`, 404 → `NOT_FOUND`, 500 → `INTERNAL_ERROR`). Lỗi nghiệp vụ do `ApiResults` tự đặt `code` nên được giữ nguyên.
- Kiểm chứng lại bằng request thật: `401 → "code":"UNAUTHORIZED"`, `403 → "code":"FORBIDDEN"`, `404 → "code":"NOT_FOUND"`, còn `POST /api/auth/login` sai mật khẩu vẫn trả `AUTH_INVALID_CREDENTIALS`.

**2) Sai số nhiều trong tiếng Anh.** Trang quản trị hiển thị "Showing **1 accounts**" vì khoá i18n `admin.total_shown` không có dạng số nhiều.

- Sửa: tách thành `total_shown_one` / `total_shown_other` trong `en.json`; `vi.json` dùng `total_shown_other` (tiếng Việt chỉ có một dạng số nhiều).
- Kiểm chứng: chạy i18next trên chính hai file locale — `en` count=1 → "Showing 1 account", count=5 → "Showing 5 accounts", `vi` → "Đang hiển thị N tài khoản".

**3) Tài khoản admin seed có khóa chính `Guid.Empty`** (phát hiện ngày 2026-10-17 khi chuẩn bị GĐ 2). `ApplicationUser` được cấu hình `ValueGeneratedNever()` (backend tự sinh khóa), nhưng `SeedAdminAsync` trong `MigrationExtensions` tạo `new ApplicationUser { ... }` **không gán `Id`** → EF ghi `00000000-0000-0000-0000-000000000000`. Đường đăng ký không bị vì `IdentityService` gán `Guid.CreateVersion7()`.

- Hệ quả: khóa chính trùng với giá trị "chưa đặt" mà code thường dùng để kiểm tra; JWT phát ra có `sub = 0…0`; mọi bảng của GĐ 2 tham chiếu tới user sẽ lưu id 0.
- Sửa: `SeedAdminAsync` gán `Id = Guid.CreateVersion7()`; cấp lại khóa cho dòng admin trong DB dev (UUIDv7 mới) và cập nhật toàn bộ bảng tham chiếu (`user_claims`, `user_logins`, `user_tokens`, `user_roles`, `user_profiles`, `user_settings`, `refresh_tokens`) trong **một transaction**.
- Kiểm chứng: `count(*) WHERE id/user_id = '000…'` = 0 ở mọi bảng; admin vẫn có `display_name = Administrator` và role `Admin`; thử chèn `user_roles` trỏ tới user không tồn tại vẫn bị chặn → khóa ngoại còn hiệu lực sau thao tác. `dotnet build`: 0 warning; `dotnet test`: 29/29.
- Lưu ý: phiên đăng nhập cũ của admin (JWT có `sub = 0…0`) trở nên không hợp lệ → phải đăng nhập lại.

**Còn thiếu (ghi nhận, chưa làm):** chưa có **test tự động ở tầng HTTP**; dự án hiện chỉ có test tầng service/DbContext, nên các sửa đổi trên được kiểm chứng bằng request thật và ghi lại kết quả, không phải bằng test. Khi thêm `WebApplicationFactory` (cần cẩn thận vì `Program.cs` có nạp `.env` thật) thì nên bổ sung test khẳng định `401/403/404` đều có `code`. Chưa có test cho seeder (khó unit-test vì phụ thuộc `UserManager`).

---

## 4. Tiêu chí hoàn thành (lấy từ `docs/04_roadmap.md` Giai đoạn 1)

- [x] Đăng ký, đăng nhập, đăng xuất, làm mới token hoạt động trên web *(đã kiểm chứng trên trình duyệt thật; luồng làm mới token xác nhận qua log mạng)*
- [x] Phân quyền hai vai trò `User` và `Admin`; endpoint chỉ-admin bị chặn với người thường *(đã kiểm chứng: token `User` nhận `403 FORBIDDEN`; giao diện cũng chuyển hướng khỏi `/admin/users`)*
- [x] Kiểm tra dữ liệu đầu vào ở backend cho mọi endpoint hiện có (trả mã lỗi thống nhất)
- [x] Hồ sơ cá nhân: xem và sửa tên hiển thị, tiểu sử *(đã kiểm chứng trên trình duyệt thật)*
- [x] Có tài khoản admin đầu tiên (seed từ cấu hình) — đã kiểm chứng đăng nhập được và có role `Admin`
- [x] Khung đa ngôn ngữ Việt/Anh chạy trên web (mọi chuỗi hiển thị đi qua i18n) *(đã kiểm chứng cả hai chiều vi↔en trên trình duyệt thật)*
- [x] SignalR kết nối được và xác thực bằng token *(đã kiểm chứng: không token bị từ chối, token hợp lệ kết nối được, token sai bị từ chối)*
- [x] Có test cho đăng nhập và phân quyền; bí mật/khóa API không nằm trong Git *(29 test pass; đã quét lại lịch sử Git)*

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
