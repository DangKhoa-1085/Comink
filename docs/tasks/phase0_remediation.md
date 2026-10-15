# Giai đoạn 0 — Khắc phục các mục còn thiếu

> Tài liệu này được tạo sau buổi **kiểm chứng Giai đoạn 0** (theo `docs/04_roadmap.md`, mục "Giai đoạn 0: Khởi tạo").
> Kết quả kiểm chứng lần 1: khung backend và web chạy được, nhưng Giai đoạn 0 **chưa thể coi là hoàn thành**.
> Kết quả cập nhật lần 2 (sau khi cài Docker + SDK 10.0.402): hạ tầng chạy được và migration M0 đã áp thành công lên PostgreSQL thật.
> Trạng thái: **chỉ còn khung Flutter và các mục cần người dùng quyết.**

---

## 1. Lịch sử kiểm chứng

### Lần 1 — máy chưa có Docker, chưa có Flutter, SDK lệch ghim

| Lệnh | Kết quả |
|---|---|
| `dotnet build` (backend) | Thành công — 7/7 project, 0 warning, 0 error (sau khi nới `global.json` tạm thời) |
| `dotnet test --no-build` (backend) | 3/3 pass — cả 3 chỉ là `UnitTest1` placeholder |
| `npm ci` + `npm run lint` + `npm run build` (web) | Thành công — 105 package, 0 lỗi lint, bundle 325 kB JS |
| `docker compose` | Không chạy được (chưa cài Docker) |
| `flutter analyze` | Không chạy được (chưa cài Flutter, `mobile/` chưa có project) |

### Lần 2 — sau khi cài Docker Desktop và .NET SDK 10.0.402

| Hạng mục | Kết quả |
|---|---|
| `dotnet build` với đúng SDK ghim | Thành công — 7/7 project, 0 warning, 0 error, **không cần workaround** |
| `docker compose up -d` | Thành công — 4/4 service `Up` (postgres, redis, mailpit, livekit) |
| `dotnet ef database update` | Thành công — migration M0 đã ghi vào `__EFMigrationsHistory` (1 dòng) |
| Kiểm chứng schema thật trong PostgreSQL | `user_name`/`normalized_user_name` = `varchar(32)`; `users.updated_at` có; `user_settings.notification_settings` = `jsonb`; extension `pg_trgm` + `unaccent` có; hàm `immutable_unaccent` = `IMMUTABLE`, chạy đúng (`Xin chào Việt Nam` → `Xin chao Viet Nam`) |

---

## 2. Danh sách việc

### 2.1. Đã sửa

- [x] **SDK .NET không khớp**: `backend/global.json` ghim `10.0.400` — bản này **không tồn tại** trên kênh 10.0 (bản mới nhất là `10.0.402`), khiến mọi lệnh `dotnet build` thất bại ngay khi khởi động. Đã đổi ghim sang `10.0.402` và đã cài đúng bản trên máy.
- [x] **Schema M0 lệch tài liệu** — sinh lại migration M0 (`20261015165924_Identity_InitialCreate`):
  - [x] Thêm `CREATE EXTENSION pg_trgm`, `unaccent` và hàm `immutable_unaccent` (mục 5.1), có `DROP` đối xứng ở `Down`.
  - [x] `users.user_name` / `normalized_user_name` → `varchar(32)`.
  - [x] Thêm `users.updated_at` *(lượt đầu bị bỏ sót và báo nhầm là đã xong; lượt hai đã kiểm chứng trực tiếp trên database)*.
  - [x] Thêm `user_settings.notification_settings` (`jsonb`, mặc định bật hết).
  - [x] Xác minh migration khớp mô hình: `dotnet ef migrations has-pending-model-changes` → *"No changes have been made to the model since the last migration."*
- [x] **LiveKit crash loop**: container `comink_livekit` restart liên tục (RestartCount=12, ExitCode=1) vì hai lỗi trong `infra/livekit.yaml`:
  - `TURN domain is not correct` — TURN bật với `domain: localhost`, LiveKit từ chối.
  - `secret is too short, should be at least 32 characters` — khóa `devsecret` chỉ 9 ký tự.
  - Cách sửa: tắt khối `turn` khi dev (kèm giải thích lý do, và cả lý do cổng TURN chưa được publish), thay `api_secret` bằng chuỗi 48 ký tự. Đã cập nhật đồng bộ ở `infra/livekit.yaml`, `backend/src/Comink.Api/appsettings.Development.json` và `.env.example`. Sau khi recreate: `Up`, `RestartCount=0`, `ExitCode=0`.
- [x] **Port lệch ba nơi**: `web/vite.config.ts` đã trỏ proxy `/api`, `/hub` và `/health` về `http://localhost:5117` (khớp `launchSettings.json`).
- [x] **README sai tên solution**: `Comink.sln` → `Comink.slnx`.
- [x] **README sai địa chỉ backend**: `https://localhost:5001/swagger` → `http://localhost:5117/openapi/v1.json` (dự án dùng OpenAPI, chưa có Swagger UI).
- [x] **`docs/03_database_design.md` mâu thuẫn**: tách `profile_visibility` thành enum riêng `ProfileVisibility` (`Everyone`, `Friends`) ở phần tài liệu, không còn bị hiểu nhầm là có `Nobody`. *Lưu ý: đây mới là sửa tài liệu — enum `ProfileVisibility` trong mã C# chưa được tạo, cột hiện vẫn là `string`.*
- [x] **Seed role lệch tài liệu**: ghi rõ là seed **lúc khởi động** ở `MigrationExtensions.cs` (chỉ `Development`), migration thuần cấu trúc.
- [x] **Kiểm chứng `docker compose`** và **áp migration M0 lên PostgreSQL thật** (mục 2.2 của lượt trước) — đã xong ở lần 2.

### 2.2. Còn lại

- [ ] **Khung Flutter chưa tồn tại**: `mobile/` chỉ có `README.md`. Cần cài Flutter rồi chạy `flutter create --org com.comink --platforms=android,ios .` trong `mobile/`, sau đó `flutter analyze`.
- [ ] **Hai hệ tên biến môi trường**: `.env.example` dùng `ADMIN_EMAIL`, `POSTGRES_USER`… còn backend đọc `AdminSeed:Email`, `ConnectionStrings:Postgres`; không có lớp ánh xạ nào và ASP.NET Core **không tự đọc file `.env`**.
- [ ] **Bí mật trong `appsettings.Development.json`**: chứa `AdminSeed:Password` và mật khẩu PostgreSQL đã commit (`appsettings.Development.json` được thêm từ commit `dc82c1e`).
- [ ] **Ba test placeholder**: `UnitTest1` ở cả ba project test chưa kiểm tra gì; Giai đoạn 0 chưa cần, nhưng Giai đoạn 1 phải có test thật cho đăng nhập và phân quyền.
- [ ] **TURN chưa bật**: cần cấu hình đúng (domain/IP LAN thật, publish cổng UDP/TCP) ở Giai đoạn 4.

---

## 3. Công cụ và bản đang dùng

| Công cụ | Bản | Trạng thái |
|---|---|---|
| .NET SDK | `10.0.402` (+ `10.0.300`, `8.0.421`) | Đã cài, khớp `global.json` |
| Docker Desktop | `29.8.2`, Compose `v5.6.0` | Đã cài, daemon chạy |
| Flutter SDK | — | **Chưa cài** |

---

## 4. Ghi chú quá trình

- Migration M0 được **sinh lại hai lần** (lần hai để bổ sung `users.updated_at`). Cả hai lần đều thực hiện khi M0 **chưa được gộp vào `main`** và **chưa có dữ liệu thật**; database dev đã được tạo lại từ đầu. Sau khi M0 vào `main`, quy tắc "không sửa migration đã gộp" sẽ được áp dụng.
- `docker exec ... psql -c "SELECT ... \"__EFMigrationsHistory\" ..."` bị PowerShell 5.1 làm mất dấu ngoặc kép, khiến PostgreSQL hạ chữ thường tên bảng. Cách kiểm tra tránh lỗi này: truy vấn `information_schema.tables` / `information_schema.columns` thay vì gõ thẳng tên bảng.
- Bảng `user_profiles.avatar_media_id` / `cover_media_id` hiện **chưa có khóa ngoại** tới `media_assets` vì bảng đó thuộc mốc M1 — đúng theo mục 6.3 của tài liệu thiết kế CSDL.
- `AGENTS.md` mục 4 ghi `Comink.sln` trong khi thực tế là `Comink.slnx` — nên cập nhật ở lần sửa tài liệu tới.

---

## 5. Tài liệu liên quan

- `docs/04_roadmap.md` — checklist Giai đoạn 0
- `docs/03_database_design.md` — mục 3.1, 4, 5.1, 6.3, 6.5
- `AGENTS.md` — mục 7.3 (tạo file phân rã công việc), mục 8.2 (migration), mục 14 (lệnh đã kiểm chứng)
