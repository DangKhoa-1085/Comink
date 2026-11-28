# Comink — Hướng dẫn triển khai Internet (Neon + Railway + Vercel)

> Tài liệu 07, bổ sung cho mục 3.8 của [`02_tech_stack_decisions.md`](02_tech_stack_decisions.md). Hướng dẫn đưa Comink lên Internet với bộ dịch vụ miễn phí/chi phí thấp: **Neon** (PostgreSQL 17), **Railway** (ASP.NET Core .NET 10 + Redis), **Vercel** (React + Vite), kết hợp **Cloudinary** (lưu ảnh) và **LiveKit Cloud** (gọi thoại/video + TURN).

---

## 1. Kiến trúc triển khai

| Thành phần | Nền tảng | Ghi chú |
|---|---|---|
| PostgreSQL 17 | **Neon** | Hỗ trợ sẵn extension `pg_trgm`, `unaccent` và TLS. |
| Backend `.NET 10` | **Railway** | Build từ [`backend/Dockerfile`](../backend/Dockerfile), hỗ trợ WebSocket cho SignalR (`/hub/chat`). |
| Redis | **Railway Redis** | Bắt buộc khi khởi động (presence, typing throttle); backend tự nhận URI `redis://` và đặt `abortConnect=false`. |
| Web (React + Vite) | **Vercel** | Có [`web/vercel.json`](../web/vercel.json) rewrite SPA về `/index.html`. |
| Ảnh / tệp | **Cloudinary** | Bắt buộc cấu hình trên Production (container Railway có ổ đĩa tạm thời, redeploy sẽ mất tệp cục bộ). |
| Gọi thoại / video | **LiveKit Cloud** | Railway không mở dải cổng UDP media/TURN; LiveKit Cloud cung cấp sẵn WSS + TURN. |

---

## 2. Bước 1 — Tạo PostgreSQL trên Neon và chạy Migration

1. Tạo Project mới trên [Neon Console](https://console.neon.tech) với **PostgreSQL 17**, chọn region gần người dùng (ví dụ Singapore `ap-southeast-1`).
2. Vào **Connection Details**, chọn **Direct connection** (host **không** chứa `-pooler`) và sao chép chuỗi `postgresql://...`.
   - Backend hỗ trợ cả URI `postgresql://...` lẫn chuỗi ADO.NET `Host=...` thông qua `ConnectionStringNormalizer`.
   - Với quy mô đồ án, nên dùng **Direct connection** cho cả migration lẫn runtime (tránh lỗi prepared statement của PgBouncer).
3. Từ máy cá nhân (trong thư mục `backend/`), chạy EF Core migration lên Neon:

```powershell
$env:ConnectionStrings__Postgres="postgresql://<USER>:<PASSWORD>@<DIRECT_HOST>/<DB>?sslmode=require"
dotnet ef database update --project src/Comink.Infrastructure --startup-project src/Comink.Api
```

> **Lưu ý:** Ở môi trường `Production`, backend **không** tự chạy `Database.MigrateAsync()` khi khởi động (theo [`AGENTS.md`](../AGENTS.md) mục 8.2), nhưng sẽ tự đảm bảo hai role `User`, `Admin` và tài khoản admin ban đầu (nếu có cấu hình `AdminSeed`) tồn tại.

### 2.1. Cập nhật khi có Migration CSDL mới (ví dụ thêm Comink Locket)

Mỗi khi backend có migration mới (như `Social_AddLockets`), chỉ cần chạy lại **3 bước** theo đúng thứ tự:

1. **Áp dụng migration lên Neon PostgreSQL** (từ thư mục `backend/` trên máy cá nhân, dùng **Direct connection** không có `-pooler`):
   ```powershell
   $env:ConnectionStrings__Postgres="postgresql://<USER>:<PASSWORD>@<DIRECT_HOST>/<DB>?sslmode=require"
   dotnet ef database update --project src/Comink.Infrastructure --startup-project src/Comink.Api
   ```
   *(Có thể kiểm tra trước danh sách migration chờ bằng `dotnet ef migrations list --project src/Comink.Infrastructure --startup-project src/Comink.Api` hoặc xuất SQL bằng `dotnet ef migrations script --idempotent` để dán chạy trực tiếp trong Neon SQL Editor nếu không muốn chạy từ máy.)*
2. **Redeploy Backend trên Railway:** push code lên GitHub (hoặc bấm **Deploy / Redeploy** trong dịch vụ Backend trên Railway Dashboard) để Railway build lại container mới khớp với schema vừa cập nhật.
3. **Redeploy Web trên Vercel:** Vercel tự build lại khi push nhánh chính (hoặc bấm **Redeploy** trong Vercel Dashboard).

---

## 3. Bước 2 — Deploy Backend & Redis lên Railway

1. Tạo Project mới trên [Railway](https://railway.app) từ repo GitHub của Comink.
2. Thêm **Redis** (`+ New → Database → Add Redis`) trong cùng Project.
3. Cấu hình service Backend:
   - **Root Directory:** `backend`
   - **Builder:** `Dockerfile` (tự nhận [`backend/Dockerfile`](../backend/Dockerfile))
   - **Healthcheck Path:** `/health`
   - **Networking → Public Networking:** bấm **Generate Domain** (chọn port `8080` nếu được hỏi).
4. Khai báo **Variables** cho service Backend:

| Biến môi trường | Giá trị / Cách lấy | Bắt buộc |
|---|---|---|
| `ASPNETCORE_ENVIRONMENT` | `Production` | Có |
| `FrontendUrl` | `https://<domain-cua-ban>.vercel.app` (nhiều URL cách nhau bởi `,`) | Có |
| `ConnectionStrings__Postgres` | Chuỗi `postgresql://...` (Direct) từ Neon | Có |
| `ConnectionStrings__Redis` | `${{Redis.REDIS_URL}}` (hoặc `${{Redis.REDISHOST}}:${{Redis.REDISPORT}},password=${{Redis.REDISPASSWORD}},abortConnect=false`) | Có |
| `Jwt__Issuer` | `comink-api` | Có |
| `Jwt__Audience` | `comink-clients` | Có |
| `Jwt__Secret` | Chuỗi ngẫu nhiên **≥ 32 ký tự** (khác với máy dev) | Có |
| `MediaStorage__CloudName` | Cloud Name trên Cloudinary | Có |
| `MediaStorage__ApiKey` | API Key trên Cloudinary | Có |
| `MediaStorage__ApiSecret` | API Secret trên Cloudinary | Có |
| `MediaStorage__UploadPreset` | Upload preset (nếu dùng) | Tuỳ chọn |
| `LiveKit__Host` | `https://<project>.livekit.cloud` | Có (nếu gọi) |
| `LiveKit__PublicUrl` | `wss://<project>.livekit.cloud` | Có (nếu gọi) |
| `LiveKit__ApiKey` | API Key từ LiveKit Cloud | Có (nếu gọi) |
| `LiveKit__ApiSecret` | API Secret từ LiveKit Cloud | Có (nếu gọi) |
| `AdminSeed__Email` | Email admin ban đầu | Tuỳ chọn |
| `AdminSeed__Password` | Mật khẩu mạnh (≥ 8 ký tự, đủ hoa/thường/số/ký tự đặc biệt) | Tuỳ chọn |
| `AdminSeed__DisplayName` | `Administrator` | Tuỳ chọn |
| `Firebase__ServiceAccountJson` | Chuỗi JSON 1 dòng của Firebase Service Account | Tuỳ chọn |

> **Lưu ý về LiveKit Cloud:**
> - `LiveKit__Host` chấp nhận cả dạng `https://<project>.livekit.cloud` lẫn `wss://<project>.livekit.cloud` (backend tự chuẩn hoá `wss://` → `https://` khi gọi Twirp API và `https://` → `wss://` cho client).
> - Nếu không khai báo các biến `LiveKit__*`, backend cũng tự đọc bộ biến mặc định của LiveKit Cloud (`LIVEKIT_URL`, `LIVEKIT_API_KEY`, `LIVEKIT_API_SECRET`) khi dán trực tiếp từ dashboard LiveKit Cloud.
> - Sau khi deploy, kiểm tra **Deploy Logs** trên Railway tìm dòng `Calls.LiveKit` (`LiveKit: BẬT (ServerUrl=..., ClientUrl=...)`) để xác nhận cấu hình đã nhận đúng.

---

## 4. Bước 3 — Deploy Web lên Vercel

1. Trên [Vercel](https://vercel.com), chọn **Add New… → Project** và trỏ tới repo GitHub.
2. Cấu hình:
   - **Root Directory:** `web`
   - **Framework Preset:** `Vite`
   - **Build Command:** `npm run build`
   - **Output Directory:** `dist`
3. Thêm biến môi trường:
   - `VITE_API_BASE_URL` = `https://<backend-domain>.up.railway.app` (không cần dấu `/` cuối; code tự cắt nếu có).
   - `VITE_APK_DOWNLOAD_URL` *(tuỳ chọn)* = đường dẫn tải trực tiếp file `app-release.apk` (khuyến nghị upload lên **GitHub Releases** rồi đặt `https://github.com/<owner>/<repo>/releases/latest/download/app-release.apk`) để hiện link tải & mã QR trong sidebar và menu Cài đặt trên điện thoại.
4. Deploy và sao chép URL Vercel vừa tạo.

---

## 5. Bước 4 — Nối CORS và LiveKit Webhook

1. Quay lại Railway → cập nhật `FrontendUrl` thành URL Vercel thực tế (có thể ghép nhiều domain ngăn cách bởi dấu `,`).
2. Vào **LiveKit Cloud → Settings → Webhooks** → tạo webhook trỏ tới:
   - `https://<backend-domain>.up.railway.app/api/webhooks/livekit`
   - Dùng đúng cặp API Key/Secret đã cấu hình cho backend.

---

## 6. Checklist kiểm tra sau khi deploy

- [ ] `GET https://<backend>.up.railway.app/health` trả về `200 OK`.
- [ ] Log Railway không có lỗi kết nối Postgres/Redis; đã seed thành công role `User`, `Admin`.
- [ ] Mở web Vercel, đăng ký tài khoản mới và đăng nhập không gặp lỗi CORS.
- [ ] Tải lại trang (F5) ở `/messages`, `/profile`, `/admin/users` không bị lỗi `404`.
- [ ] Đăng bài hoặc gửi tin nhắn kèm ảnh → upload thẳng lên Cloudinary và hiển thị bình thường.
- [ ] SignalR kết nối `wss://<backend>/hub/chat` đạt `101 Switching Protocols`; nhắn tin 2 tài khoản nhận real-time.
- [ ] Gọi thoại/video giữa 2 mạng khác nhau qua LiveKit Cloud nghe/nhìn 2 chiều và webhook ghi nhận kết thúc cuộc gọi.
