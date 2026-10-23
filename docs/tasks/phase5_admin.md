# Giai đoạn 5 — Quản trị viên CB (quản lý và khóa/mở khóa tài khoản)

> Tài liệu 06 (tiếp), phân rã từ `docs/04_roadmap.md` (Giai đoạn 5) theo `AGENTS.md` mục 7.3.
> **Mục tiêu:** quản trị viên đăng nhập web, xem/tìm kiếm danh sách tài khoản và **khóa/mở khóa** tài khoản; tài khoản bị khóa mất phiên ngay (REST + SignalR) và không đăng nhập lại được.
> **Phụ thuộc:** GĐ 1 — auth, phân quyền hai role, trang `/admin/users` nền, SignalR có xác thực ✅.
> **Trạng thái:** ✅ **CB XONG** (2026-10-24) — backend + web + test đầy đủ, đã kiểm chứng HTTP và SignalR thật.
> **Migration tương ứng:** **không cần migration mới** — các cột `account_status`, `locked_reason`, `locked_at`, `locked_by` đã có từ **M0** (`docs/03_database_design.md` mục 3.1 và 6.3, dòng M5).
> **Người phụ trách:** Dev A (Tài khoản/bảo mật, mạng xã hội, ảnh/tệp, quản trị) — `docs/04_roadmap.md` mục 2.

---

## 1. Phạm vi

### 1.1. Trong phạm vi (CB — 5 mục roadmap)

| # | Yêu cầu roadmap | Cách đáp ứng |
|---|---|---|
| 1 | Trang quản trị (web) chỉ admin vào được | Đã có từ GĐ 1: route `RequireRole` + policy `AdminOnly` ở backend (kiểm tra lại, giữ nguyên) |
| 2 | Xem, tìm kiếm danh sách tài khoản (phân trang) | Đã có `GET /api/admin/users` (con trỏ `(created_at, id)`), bổ sung thêm `lockedReason`/`lockedAt` để admin thấy lý do khóa |
| 3 | Khóa và mở khóa tài khoản | **Mới:** `POST /api/admin/users/{id}/lock` và `POST /api/admin/users/{id}/unlock` + nút thao tác trên web |
| 4 | Tài khoản bị khóa không đăng nhập được; phiên hiện tại bị thu hồi; SignalR bị ngắt | **Mới:** (a) đăng nhập trả `403 AUTH_ACCOUNT_LOCKED`; (b) mỗi request JWT kiểm tra lại `account_status`; (c) thu hồi **toàn bộ** refresh token; (d) gửi `AccountLocked` + `Abort()` mọi kết nối SignalR |
| 5 | Admin không tự khóa được chính mình | Quy tắc `AdminUserRules.EvaluateLock` (Domain) + kiểm tra lại ở endpoint; giao diện ẩn nút với chính mình |

### 1.2. Ngoài phạm vi (NC — GĐ 7)

Dashboard, quản lý bài viết/bình luận, báo cáo vi phạm, `audit_logs`, `reports`, thống kê, kiểm duyệt nâng cao, xóa hẳn tài khoản. **Không** tạo bảng M5 (`reports`, `audit_logs`) trong giai đoạn này.

---

## 2. Quyết định kỹ thuật

| Mã | Vấn đề | Phương án đã chọn | Lý do |
|---|---|---|---|
| **D1** | "Phiên hiện tại bị thu hồi" khi access token còn hạn 15 phút | **Kiểm tra `account_status` ở mọi request đã xác thực** (`JwtBearerEvents.OnTokenValidated`) → tài khoản `Locked` nhận `401` ngay lần gọi kế tiếp; đồng thời thu hồi toàn bộ refresh token | Access token là JWT tự chứa, không có cơ chế thu hồi; nếu chỉ chặn lúc làm mới token thì người bị khóa vẫn dùng được tới 15 phút. Đây là đánh đổi có chủ đích: thêm 1 truy vấn đọc chỉ mục theo khóa chính mỗi request, chấp nhận được ở quy mô vài trăm người dùng |
| **D2** | Ngắt kết nối SignalR đang mở | `IUserConnectionRegistry` (singleton) lưu `HubCallerContext` theo `user:{id}`; khi khóa, server gửi sự kiện `AccountLocked` rồi gọi `Abort()` từng kết nối | SignalR không có API "ngắt kết nối của người khác" ngoài `HubCallerContext.Abort()`; phải giữ context từ `OnConnectedAsync`. Client cũng tự đăng xuất khi nhận sự kiện (hai lớp bảo vệ) |
| **D3** | Mã lỗi khi khóa/mở khóa không hợp lệ | `USER_NOT_FOUND` (404), `ADMIN_CANNOT_LOCK_SELF` (400); khóa/mở khóa **idempotent** — khóa tài khoản đã `Locked` trả `200` không đổi dữ liệu | Khớp quy ước lỗi hiện có và yêu cầu idempotent ở `AGENTS.md` mục 8.1 |
| **D4** | Lý do khóa | `locked_reason` **tùy chọn**, tối đa **500** ký tự, validate bằng FluentValidation; **chỉ admin thấy**, không trả cho người dùng thường | Người dùng không thấy lý do để tránh lộ thông tin nội bộ; cột đã giới hạn 500 từ thiết kế |
| **D5** | Đăng nhập khi bị khóa (đã chốt với người dùng 2026-10-24) | Trả **`403 AUTH_ACCOUNT_LOCKED`** đúng hợp đồng §2, **chỉ khi mật khẩu đúng**; sai mật khẩu vẫn `401 AUTH_INVALID_CREDENTIALS` | Sửa lệch hợp đồng GĐ 1 (code cũ trả `AUTH_INVALID_CREDENTIALS`); vẫn không cho phép dò tài khoản vì phải biết mật khẩu mới nhận được mã này |

---

## 3. Hợp đồng API (sẽ chép vào `docs/05_api_contract.md` mục 4)

### 3.1. Khóa tài khoản

`POST /api/admin/users/{id}/lock` (role `Admin`) — body tùy chọn:

```json
{ "reason": "Vi phạm quy định cộng đồng" }
```

- `200 OK` + `AdminUserSummary` (trạng thái mới `Locked`).
- Khóa lại tài khoản đã khóa: `200` (idempotent, không đổi `lockedAt`/`lockedBy`).
- Lỗi: `USER_NOT_FOUND` (404), `ADMIN_CANNOT_LOCK_SELF` (400), `VALIDATION_FAILED` (400 — lý do > 500 ký tự), `FORBIDDEN` (403 — không phải admin).
- Tác dụng phụ: thu hồi toàn bộ refresh token còn hiệu lực; phát `AccountLocked` tới `user:{id}` và ngắt các kết nối SignalR.

### 3.2. Mở khóa tài khoản

`POST /api/admin/users/{id}/unlock` — không cần body.

- `200 OK` + `AdminUserSummary` (`Active`, `lockedReason`/`lockedAt` được xóa).
- Mở khóa tài khoản đang `Active`: `200` (idempotent).
- Lỗi: `USER_NOT_FOUND` (404), `FORBIDDEN` (403).

### 3.3. `AdminUserSummary` (bổ sung 2 trường)

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

### 3.4. SignalR

| Sự kiện | Gửi tới | Khi nào |
|---|---|---|
| `AccountLocked` | `user:{id}` | Ngay sau khi admin khóa tài khoản, trước khi ngắt kết nối |

---

## 4. Công việc

### 4.1. Domain

- [x] `Domain/Admin/AdminUserRules.cs`: `EvaluateLock` (chặn tự khóa, idempotent), `EvaluateUnlock` (idempotent), hằng số trạng thái + giới hạn lý do, `NormalizeLockReason`
- [x] Unit test cho mọi nhánh quy tắc

### 4.2. Application

- [x] `IIdentityService`: `GetAccountStatusAsync`, `SetAccountStatusAsync`; `IdentityUserData`/`AdminUserData` thêm `LockedReason`, `LockedAt`
- [x] `ITokenService`: `RevokeAllForUserAsync`
- [x] `IAdminService` + `AdminService`: luồng khóa/mở khóa, gọi thu hồi token + thông báo SignalR
- [x] `IAccountNotifier` (Application) — cài đặt SignalR ở tầng Api
- [x] `AdminEvents.AccountLocked`, DTO `LockUserRequest`, validator (lý do ≤ 500)
- [x] Sửa `IdentityService.ValidateCredentialsAsync`: kiểm mật khẩu **trước** khi xét trạng thái để trả đúng `AUTH_ACCOUNT_LOCKED`

### 4.3. Infrastructure

- [x] `IdentityService`: hiện thực 2 phương thức mới (cập nhật `account_status` + `locked_*`), trả `LockedReason`/`LockedAt` trong danh sách
- [x] `JwtTokenService.RevokeAllForUserAsync`: thu hồi mọi refresh token chưa thu hồi của người dùng
- [x] `JwtBearerEvents.OnTokenValidated`: chặn token của tài khoản không còn `Active`
- [x] Đăng ký DI `IAdminService`

### 4.4. API + SignalR

- [x] `AdminEndpoints`: 2 endpoint khóa/mở khóa + map mã lỗi vào `ApiResults`
- [x] `UserConnectionRegistry` + `SignalRAccountNotifier` (gửi `AccountLocked`, `Abort()` kết nối)
- [x] `ChatHub`: đăng ký/hủy đăng ký kết nối theo người dùng
- [x] `Program.cs`: đăng ký singleton registry + notifier; `ErrorCodes`/`ApiResults` thêm 2 mã lỗi

### 4.5. Web

- [x] `api/admin.ts` + `api/types.ts`: 2 lời gọi mới, kiểu `AdminUserSummary` bổ sung trường khóa
- [x] `AdminUsersPage`: cột **Thao tác**, nút Khóa/Mở khóa, ẩn nút với chính mình, hiện lý do khóa
- [x] Hộp thoại xác nhận khóa (nhập lý do) / mở khóa; thông báo thành công/thất bại
- [x] Xử lý `AccountLocked`: xóa phiên + tự đăng xuất + thông báo; xử lý `AUTH_ACCOUNT_LOCKED` khi gọi REST
- [x] i18n `vi`/`en` đầy đủ; trạng thái tải/rỗng/lỗi giữ nguyên

### 4.6. Test

- [x] Domain `AdminUserRulesTests`
- [x] Application `AdminServiceTests` (khóa thành công/thu hồi token/phát sự kiện, tự khóa, không tồn tại, idempotent, mở khóa)
- [x] Cập nhật `AuthServiceTests` (đăng nhập tài khoản khóa → `AUTH_ACCOUNT_LOCKED`) và fake tương ứng
- [x] Cập nhật `IdentityServiceTests` (ValidateCredentials + SetAccountStatus/GetAccountStatus)
- [x] Test tích hợp: cột `locked_*` được ghi/xóa đúng; `RevokeAllForUserAsync` thu hồi đúng số token
- [x] `dotnet build` 0 warning; `dotnet test` xanh; `npm run lint`/`npm run build` sạch

### 4.7. Tài liệu

- [x] `docs/05_api_contract.md` mục 4 + bảng mã lỗi + lịch sử thay đổi + SignalR
- [x] `docs/04_roadmap.md`: ghi chú trạng thái GĐ 5 (không tự tick checklist — `AGENTS.md` mục 12)
- [x] Ghi chú `docs/03_database_design.md` nếu cần (GĐ 5 CB không đổi schema)

---

## 5. Tiêu chí hoàn thành (đối chiếu roadmap GĐ 5)

- [x] Trang `/admin/users` chỉ admin vào được (route + backend)
- [x] Xem, tìm kiếm, tải thêm danh sách tài khoản (con trỏ, không OFFSET)
- [x] Khóa tài khoản từ web → dòng đổi trạng thái `Đã khóa`, ghi `locked_at`/`locked_by`/`locked_reason`
- [x] Người bị khóa: đăng nhập → `403 AUTH_ACCOUNT_LOCKED`; request đang dùng access token cũ → `401`; refresh token → không làm mới được; kết nối SignalR bị ngắt
- [x] Mở khóa → đăng nhập lại được; `locked_*` được xóa
- [x] Admin tự khóa mình → `400 ADMIN_CANNOT_LOCK_SELF` (kể cả khi gọi thẳng API)
- [x] `dotnet build` 0 warning, `dotnet test` xanh, `npm run lint`/`build` sạch

---

## 6. Bẫy và rủi ro đã biết

1. **Access token không thu hồi được**: bắt buộc kiểm tra trạng thái ở mỗi request, nếu không người bị khóa vẫn dùng được tới 15 phút (D1).
2. **SignalR giữ kết nối sau khi khóa**: phải lưu `HubCallerContext` từ lúc kết nối; nếu chỉ gửi sự kiện thì client không trung thực có thể bỏ qua (D2).
3. **Thu hồi token phải làm trước khi phát sự kiện**: người dùng có thể dùng refresh token trong khoảng thời gian giữa hai bước.
4. **Bẫy enum `0`/default của GĐ 2–4**: trạng thái tài khoản là **chuỗi** (`Active`/`Locked`), không dùng enum — tránh cùng lớp lỗi.
5. **Kiểm tra tự khóa ở cả hai nơi**: domain + endpoint; chỉ ẩn nút ở giao diện là không đủ (`AGENTS.md` mục 9.2).
6. **Không trả `locked_reason` cho người dùng thường**: chỉ có trong `AdminUserSummary`.
7. **Kiểm tra trạng thái theo từng request làm tăng tải DB**: chấp nhận ở quy mô đồ án; nếu chậm, thêm cache ngắn có chủ đích thu hồi (ghi chú ở mục 2 D1).

---

## 7. Nhật ký kiểm chứng

### 7.1. Test tự động (2026-10-24)

| Hạng mục | Kết quả |
|---|---|
| `dotnet build` | **0 warning, 0 error** |
| `dotnet test` (Domain) | **68/68** — thêm `AdminUserRulesTests` (tự khóa, idempotent, chuẩn hóa lý do, giới hạn 500) |
| `dotnet test` (Application) | **25/25** — thêm `AdminServiceTests` (khóa → thu hồi token + phát sự kiện, tự khóa, không tồn tại, idempotent, lý do quá dài, mở khóa) |
| `dotnet test` (Tích hợp, PostgreSQL thật) | **92/92** — thêm test `SetAccountStatus`/`GetAccountStatus` ghi đúng `locked_*`; cập nhật test `ValidateCredentials` (tài khoản khóa + mật khẩu đúng trả id để AuthService trả `AUTH_ACCOUNT_LOCKED`) |
| `npm run lint` | **0 warning, 0 error** |
| `npm run build` | thành công (còn cảnh báo cũ: bundle > 1 MB do SDK LiveKit) |

### 7.2. Kiểm chứng HTTP thật (backend chạy trên `localhost:5117`, PostgreSQL thật) — **17/17 đạt**

| # | Phép kiểm tra | Kết quả |
|---|---|---|
| 1 | Đăng nhập admin (role `Admin` trong DB dev) | `200`, roles `User,Admin` |
| 2 | Người thường gọi `GET /api/admin/users` | `403 FORBIDDEN` |
| 3 | Admin xem/tìm danh sách, có `lockedReason` | `200`, 2 tài khoản test |
| 4 | Admin tự khóa mình | `400 ADMIN_CANNOT_LOCK_SELF` |
| 5 | Khóa `phase5.target` kèm lý do tiếng Việt | `200`, `Locked`, lý do/`lockedAt` đúng |
| 6 | DB `locked_reason`/`locked_at`/`locked_by` | đúng (so khớp **md5** để tránh lỗi mã hoá console) |
| 7 | Dùng access token cũ của người bị khóa | `401 UNAUTHORIZED` |
| 8 | Dùng refresh token cũ | `401 AUTH_REFRESH_TOKEN_REUSED` (token đã bị thu hồi khi khóa) |
| 9 | Đăng nhập lại khi bị khóa (đúng mật khẩu) | `403 AUTH_ACCOUNT_LOCKED` |
| 10 | Đăng nhập khi bị khóa nhưng **sai** mật khẩu | `401 AUTH_INVALID_CREDENTIALS` |
| 11 | Khóa lặp lại (idempotent) | `200`, **không ghi đè** lý do khóa ban đầu |
| 12 | Khóa không kèm body / Content-Type | `200`, `lockedReason = null` |
| 13 | Mở khóa | `200`, `Active`, `lockedReason = null` |
| 14 | Đăng nhập lại sau mở khóa | `200` |
| 15 | Token của admin vẫn dùng được | `200` |

### 7.3. Kiểm chứng SignalR thật — đạt

- Client `@microsoft/signalr` đăng nhập `phase5.target` → kết nối `/hub/chat` thành công.
- Admin khóa tài khoản → client nhận **`AccountLocked`** `{ userId }` và kết nối bị đóng.
- Log backend: `Đã ngắt 1 kết nối SignalR của tài khoản bị khóa …`.

### 7.4. Kiểm chứng giao diện thật (Chrome qua BrowserSkill, Vite `localhost:5173` + backend `localhost:5117`) — đạt

- Đăng nhập tài khoản role `Admin` → menu **“Quản trị”** xuất hiện → `/admin/users` hiển thị bảng:
  cột **Thao tác**, mỗi dòng có nút **Khóa** (hoặc **Mở khóa**); dòng của chính admin hiện **“Bạn”**
  và **không có nút** (đúng yêu cầu tự khóa).
- Bấm **Khóa** trên `phase5.target` → hộp thoại “Khóa tài khoản @phase5.target?” + ô lý do (≤ 500) →
  nhập **“Vi phạm quy định cộng đồng”** → **Xác nhận** → toast **“Đã khóa tài khoản”**, dòng đổi sang
  **“Đã khóa”** + hiện lý do, nút đổi thành **“Mở khóa”**.
- Bấm **Mở khóa** → hộp thoại xác nhận → **Xác nhận** → toast **“Đã mở khóa tài khoản”**, dòng trở về
  **“Đang hoạt động”**, lý do khóa biến mất.
- Toàn bộ chuỗi hiển thị lấy từ i18n `vi` (cùng bộ khoá đã thêm cho `en`).

### 7.5. Lỗi/bẫy tự phát hiện trong quá trình làm

1. **Body tùy chọn của `POST .../lock` trả `415`** khi client không gửi body/`Content-Type`, kể cả với
   `[FromBody(EmptyBodyBehavior = Allow)]`. **Đã sửa:** đọc body thủ công bằng `HttpRequest.ReadFromJsonAsync`
   khi `ContentLength > 0`, còn lại coi như `reason = null`; JSON hỏng → `400 VALIDATION_FAILED`.
2. **Refresh token sau khi khóa trả `AUTH_REFRESH_TOKEN_REUSED`** thay vì `..._INVALID`: đây là hành vi
   đúng của cơ chế phát hiện dùng lại token có từ GĐ 1 (token bị đánh dấu thu hồi khi khóa). Đã ghi chú
   trong hợp đồng mục 4.4; client chỉ cần hiểu “phiên không còn hiệu lực”.
3. **PowerShell 5.1 làm sai lệch tiếng Việt** trong script kiểm thử (gửi body theo bảng mã ANSI) — không
   phải lỗi backend; đã gửi body dạng byte UTF-8 và so khớp DB bằng md5.
4. **Môi trường build bị chặn quyền ghi vào `obj/`** (Windows file permissions của thư mục dự án): đã sửa
   bằng công cụ chẩn đoán quyền tệp của DSH (cấp quyền toàn quyền cho tài khoản đang đăng nhập ở
   `backend/src/Comink.Domain/obj/Debug` + `net10.0`, có bản sao lưu/lệnh hoàn tác trong `D:\DACN\acl-reports`).

### 7.6. Dữ liệu thử đã tạo trong DB dev (có thể xoá khi không cần)

> Tạo để kiểm chứng thủ công, **không** nằm trong migration/seed. Không xoá tự động (quy định `AGENTS.md`).

- `phase5.admin` — role `User` + `Admin` (gán trực tiếp trong DB dev), mật khẩu test `MatKhau@123`.
- `phase5.target` — role `User`, mật khẩu test `MatKhau@123`; đã mở khóa lại sau khi thử.
- Các tài khoản này chỉ tồn tại trong database local; **không** ảnh hưởng môi trường khác.

### 7.7. Việc ghi nhận nhưng **không** sửa (ngoài phạm vi)

- `docs/03_database_design.md` mô tả `users.locked_by` là **FK → users**, nhưng model EF hiện **không** khai
  báo quan hệ này (không có ràng buộc khoá ngoại trong migration M0). GĐ 5 chỉ ghi giá trị; muốn khớp tài
  liệu cần một migration mới (M5) — chưa làm vì không cần cho mục tiêu CB và cần bạn chốt.
- **Access token cấp trước lúc khóa** vẫn còn hạn 15 phút; nếu admin **mở khóa trong khoảng 15 phút đó**
  thì token cũ *có thể* dùng lại (JWT không mang mã phiên). Trong luồng bình thường client đã xóa phiên và
  refresh token đã bị thu hồi khi khóa, nên không ảnh hưởng demo. Muốn chặn tuyệt đối cần thêm
  security-stamp/mã phiên vào access token — thuộc nhóm “quản lý phiên” của GĐ 7.
- Giao diện chưa có màn hình chi tiết người dùng; danh sách đã đủ cho mục tiêu CB.

