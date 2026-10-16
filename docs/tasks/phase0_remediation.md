# Giai đoạn 0 — Khắc phục các mục còn thiếu

> Tài liệu này được tạo sau buổi **kiểm chứng Giai đoạn 0** (theo `docs/04_roadmap.md`, mục "Giai đoạn 0: Khởi tạo").
> Trạng thái: **hoàn tất** — mọi mục của Giai đoạn 0 đã đạt và đã được kiểm chứng bằng cách chạy thật.

---

## 1. Lịch sử kiểm chứng

### Lần 1 — máy chưa có Docker, chưa có Flutter, SDK lệch ghim

| Lệnh | Kết quả |
|---|---|
| `dotnet build` (backend) | Thành công — 7/7 project (sau khi nới `global.json` tạm thời) |
| `dotnet test --no-build` | 3/3 pass — cả 3 chỉ là `UnitTest1` placeholder |
| `npm ci` + `npm run lint` + `npm run build` (web) | Thành công |
| `docker compose` | Không chạy được (chưa cài Docker) |
| `flutter analyze` | Không chạy được (chưa cài Flutter) |

### Lần 2 — sau khi cài Docker Desktop và .NET SDK 10.0.402

| Hạng mục | Kết quả |
|---|---|
| `dotnet build` với đúng SDK ghim | Thành công — 7/7 project, 0 warning, 0 error |
| `docker compose up -d` | Thành công — 4/4 service `Up` |
| `dotnet ef database update` | Thành công — migration M0 ghi vào `__EFMigrationsHistory` |
| Schema thật trong PostgreSQL | `user_name`/`normalized_user_name` = `varchar(32)`; `users.updated_at` có; `notification_settings` = `jsonb`; extension `pg_trgm` + `unaccent`; `immutable_unaccent` IMMUTABLE và chạy đúng |

### Lần 3 — hoàn tất (Flutter, `.env`, bảo mật)

| Hạng mục | Kết quả |
|---|---|
| Khung Flutter trong `mobile/` | Đã tạo (`flutter create --org com.comink`), `flutter analyze` sạch |
| Backend nạp `.env` | Log xác nhận: *"Đã nạp cấu hình từ D:\DACN\Comink\.env"* |
| Migration + seed khi khởi động | Log: *"Migration áp dụng thành công"*, *"Đã tạo role: User"*, *"Đã tạo role: Admin"*, *"Đã tạo tài khoản admin: admin@comink.local"* |
| `GET /health` | HTTP 200 `{"status":"healthy"}` |
| Thứ tự ưu tiên cấu hình | Đã kiểm chứng: `AdminSeed__Password` (sai) đặt trong `.env` **không** thắng user-secrets — tài khoản admin vẫn được tạo bằng mật khẩu trong user-secrets |
| Hook chặn bí mật | 4 ca kiểm thử: khoá AWS giả → chặn; mật khẩu trong file config → chặn; `.env.local` → chặn; file sạch → cho qua |

---

## 2. Việc đã hoàn thành

### 2.1. Sửa lỗi và đồng bộ

- [x] **SDK .NET không khớp**: `global.json` ghim `10.0.400` — bản không tồn tại trên kênh 10.0 — đổi sang `10.0.402`.
- [x] **Schema M0 lệch tài liệu** (sinh lại migration M0 `20261015172339_Identity_InitialCreate`):
  extension `pg_trgm`/`unaccent`/`immutable_unaccent`; `user_name` `varchar(32)`; `users.updated_at`; `user_settings.notification_settings` jsonb.
- [x] **LiveKit crash loop**: TURN bật với `domain: localhost` + secret 9 ký tự làm container thoát liên tục. Đã tắt TURN khi dev (kèm giải thích) và dùng secret 48 ký tự, đồng bộ ở `livekit.yaml`, `appsettings.Development.json`, `.env.example`.
- [x] **Port lệch ba nơi**: proxy Vite trỏ về `http://localhost:5117`, thêm route `/health`.
- [x] **README**: `Comink.sln` → `Comink.slnx`; bỏ địa chỉ Swagger sai; thêm hướng dẫn `.env`, thứ tự ưu tiên và cách cài hook.

### 2.2. `.env` liên kết với backend (hướng A)

- [x] Thêm `DotNetEnv 3.2.0` (giấy phép MIT) vào `Comink.Api`.
- [x] `Program.cs` dò ngược lên các thư mục cha để tìm `.env` (vì `dotnet run` có thư mục làm việc là thư mục project), nạp trước `WebApplication.CreateBuilder`, thiếu file thì bỏ qua.
- [x] `.env.example` viết lại theo quy ước .NET (`ConnectionStrings__Postgres`, `Jwt__Secret`, `LiveKit__ApiSecret`, `AdminSeed__Email`…), đồng thời giữ các biến hạ tầng cho Docker Compose.
- [x] Ghi rõ thứ tự ưu tiên: **biến môi trường hệ điều hành → user-secrets → `.env` → `appsettings.Development.json`**.

### 2.3. Biện pháp bảo mật

- [x] **Rủi ro (b)** — bind PostgreSQL/Redis về `127.0.0.1` trong `infra/docker-compose.yml`: máy khác trong cùng LAN không còn kết nối được vào DB dev.
- [x] **Rủi ro (a)** — bỏ `AdminSeed:Password` khỏi `appsettings.Development.json`; mật khẩu admin chuyển sang `dotnet user-secrets`.
- [x] **Rủi ro (c)** — thêm hook `.githooks/pre-commit` chặn khoá API/mật khẩu/private key và các file như `.env`, `serviceAccount*.json`, `*.pem`; bổ sung `.gitignore` (`appsettings.*.local.json`, `*.pem`, `*.key`, `*.p12`, `*.pfx`, `*serviceAccount*.json`).
- [x] Đã quét lại toàn bộ lịch sử Git: **không có bí mật thật** nào bị commit (chỉ placeholder và mật khẩu dev).

---

## 3. Sự cố trong quá trình làm

Trong lúc kiểm thử hook, một lệnh `git reset --hard` đã **xoá các thay đổi chưa commit** của đợt khôi phục Giai đoạn 0. Không có đường phục hồi tự động (`git reflog` không lưu thay đổi cây làm việc; 2 commit kiểm thử chỉ chứa file test).

- Toàn bộ thay đổi đã được **làm lại**, lần này commit theo từng nhóm nhỏ để luôn có điểm khôi phục.
- Hai commit kiểm thử (chứa khoá AWS giả, chưa từng push) đã được gỡ khỏi lịch sử bằng `git reset --hard 311e072`; lịch sử hiện sạch.
- Bài học đã áp dụng: trước mọi lệnh Git có tính phá huỷ, commit tạm phần việc đang dở.

### 3.1. Sự cố lần hai (2026-10-17) — khi kiểm thử hook chặn bí mật

Script kiểm thử hook tự gọi `git commit -m "hook test"`, và vì mọi file đang sửa đều nằm trong index, **16 commit rác `hook test` đã được tạo**, trong đó một commit chứa toàn bộ code web Giai đoạn 1; nhiều file test `zz*` cũng lọt vào lịch sử.

- Đã xử lý bằng `git reset --mixed df6c24c` (giữ nguyên cây làm việc), rồi commit lại **đúng hai nhóm**: code web và bản sửa hook. Không có thay đổi thật nào bị mất.
- **Bài học:** script kiểm thử Git phải chạy trong **repository tạm** hoặc phải chủ động `git reset` trước mỗi lần `git add`; không bao giờ để script tự commit trong khi cây làm việc đang có việc thật.
- Sự cố phụ: lệnh dọn dẹp `Get-ChildItem -Filter '.env.*'` đã **xoá nhầm file `.env`** (bị gitignore nên Git không bảo vệ). Đã tạo lại từ `.env.example` và xoay `Jwt__Secret` mới.
  **Bài học:** khi dọn file theo mẫu, phải loại trừ tường minh các file cấu hình thật; `.env` không nằm trong Git nên không có cách phục hồi nào khác.

### 3.2. Bài học về hook

Bản hook đầu tiên dùng mẫu quá rộng nên **bắt nhầm bản dịch i18n** (`"password": "Password"` trong `locales/*.json`). Quá trình sửa cho thấy hai cạm bẫy:

1. **Không dùng lookahead `(?=...)`** — `grep -E` của Git (BSD/musl) không hỗ trợ, mẫu sẽ im lặng không khớp và hook mất tác dụng mà không báo lỗi.
2. **`grep` khớp theo từng dòng** — không thể "cho phép theo nhãn" một cách an toàn, vì dòng vừa có nhãn vừa có bí mật sẽ được cho qua.

Kết luận: hook được **đơn giản hoá** — chỉ giữ các mẫu có độ tin cậy cao (private key, `AKIA...`, `sk-...`, `AIza...`, `Password=` trong chuỗi kết nối, `api_key =`) và danh sách file bị cấm. Giới hạn này được ghi rõ ngay trong file hook. Dòng `"password"` trong file dịch **không** còn bị bắt nhầm.

---

## 4. Việc còn lại (không thuộc Giai đoạn 0)

- [x] ~~**Ba test placeholder**~~ — đã thay bằng test thật ở Giai đoạn 1 (29 test).
- [x] ~~**`Jwt__Secret` trong `.env`**~~ — đã xoay thành chuỗi ngẫu nhiên 48 ký tự (2026-10-17).
- [ ] **TURN chưa bật**: cấu hình đúng ở Giai đoạn 4 (domain/IP LAN thật + publish cổng UDP/TCP).
- [ ] **Đổi cổng cho thiết bị thật**: khi test app Flutter trên điện thoại thật, cần cấu hình lại backend/DB cho truy cập qua LAN (xem `docs/02_tech_stack_decisions.md`, mục 3.8 về HTTPS).

---

## 5. Tài liệu liên quan

- `docs/04_roadmap.md` — checklist Giai đoạn 0
- `docs/03_database_design.md` — mục 3.1, 4, 5.1, 6.3, 6.5
- `AGENTS.md` — mục 7.3 (tạo file phân rã công việc), mục 8.2 (migration), mục 14 (lệnh đã kiểm chứng)
