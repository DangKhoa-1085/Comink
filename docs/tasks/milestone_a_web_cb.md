# Mốc A — Web CB hoàn chỉnh

> Phân rã từ `docs/04_roadmap.md` mục **◆ Mốc A** theo `AGENTS.md` mục 7.3.
> **Mục tiêu:** web chạy trọn vẹn kịch bản demo mức cơ bản với 3+ tài khoản, có dữ liệu mẫu để dựng lại nhanh, danh sách lỗi đã phân loại và hợp đồng OpenAPI đủ ổn định để bắt đầu app Flutter.
> **Phụ thuộc:** GĐ 1–5 (CB) ✅ — GĐ 5 xong 2026-10-24. GĐ 4 còn **TURN** chưa chứng minh.
> **Trạng thái:** ✅ **hoàn tất phần tự động** (2026-10-27) — seed demo, OpenAPI Bearer + bản chụp, known issues, kiểm chứng demo/docker.
> ⏳ Còn **TURN hai mạng** (người dùng test) và **tick checklist GĐ 1–5** để chính thức đạt Mốc A.
> **Người phụ trách:** cả hai (Dev A: seed/quản trị/OpenAPI; Dev B: gọi/TURN/demo call); `docs/04_roadmap.md` mục 2.

---

## 1. Điều kiện hoàn thành Mốc A

- [x] Kịch bản demo trên web chạy trọn vẹn với **3+ tài khoản**: đăng ký → kết bạn → đăng bài → tương tác → nhắn tin → gọi → admin khóa tài khoản
  *(gọi 1–1/gọi nhóm đã kiểm chứng thật ở GĐ 4; đợt này kiểm chứng lại phần social/chat/admin bằng HTTP 12/12)*
- [x] Có **dữ liệu mẫu (seed)** để dựng lại kịch bản demo trong vài phút
- [x] Chạy ổn trên máy cá nhân bằng `docker compose up` theo `README`
- [x] **Danh sách lỗi đã biết** được ghi lại và phân loại (chặn demo hay không)
- [x] **Hợp đồng API (OpenAPI)** đủ ổn định để bắt đầu làm app
- [ ] Điều kiện tiên quyết của roadmap: **tất cả checklist GĐ 1–5 đã tick** (người dùng tick; TURN của GĐ 4 phải chứng minh xong)

---

## 2. Công việc

### 2.1. Dữ liệu mẫu (seed demo)

- [x] `Infrastructure/Seed/DemoDataSeeder`: 3 tài khoản mẫu (`an.demo`, `binh.demo`, `chi.demo`), hồ sơ tiếng Việt
- [x] Quan hệ: 2 bạn bè đã chấp nhận + 1 lời mời đang chờ; 3 theo dõi một chiều
- [x] Nội dung: 5 bài viết (có `Public`/`Friends`/`OnlyMe`, 1 bài chia sẻ), 3 bình luận (1 trả lời), 4 lượt thích
- [x] Hội thoại: 2 chat 1–1 + 1 nhóm 3 người, tổng 8 tin nhắn; cập nhật `last_message_id/at`
- [x] Cập nhật các cột đếm denormalized (`friend_count`, `follower_count`, `following_count`, `like_count`, `comment_count`, `share_count`, `reply_count`)
- [x] Idempotent: chạy lại không tạo trùng; có transaction để không để lại dữ liệu nửa vời
- [x] Chỉ chạy khi `DemoSeed:Enabled=true` **và** ở môi trường Development; mật khẩu lấy từ cấu hình, không hard-code
- [x] `.env.example` + README hướng dẫn bật seed và thông tin tài khoản mẫu
- [x] Test tích hợp: seed lần 1 tạo đúng số dòng; lần 2 không đổi dữ liệu

### 2.2. Danh sách lỗi đã biết

- [x] Tạo `docs/06_known_issues.md`: lỗi hiện tại, mức độ (chặn demo/không), cách xử lý tạm, mốc dự kiến
- [x] Gom từ các `docs/tasks/phase*.md` + kiểm tra thực tế; nêu rõ mục TURN chưa chứng minh

### 2.3. OpenAPI đủ ổn định cho app

- [x] Thêm `securitySchemes` Bearer JWT vào OpenAPI
- [x] Chỉ đánh dấu yêu cầu bảo mật cho endpoint cần đăng nhập (bỏ qua login/register/refresh/health/webhook)
- [x] Xuất hợp đồng `docs/openapi/comink.v1.json` + README cách tái sinh
- [x] Kiểm tra đủ 45 endpoint hiện có, không lỗi schema

### 2.4. Kịch bản demo + Docker

- [x] Viết kịch bản demo (kèm tài khoản/thao tác) vào README hoặc task file
- [x] Chạy thử HTTP với 3 tài khoản mẫu: feed, kết bạn, bình luận/thích, chat 1–1/nhóm, khóa/mở khóa
- [x] Kiểm tra `docker compose -f infra/docker-compose.yml up -d` + backend + web từ trạng thái sạch (theo README)
- [x] Ghi lại kết quả, lỗi gặp phải

### 2.5. TURN (GĐ 4 — cần người dùng)

- [x] Hướng dẫn test `?relay=1` giữa hai máy/hai mạng *(trong `docs/tasks/phase4_calls.md` mục 5 và `docs/06_known_issues.md` KI-01)*
- [ ] Người dùng test và xác nhận kết quả (TURN qua `:3478` + relay `30500–30510`)
- [ ] Ghi kết quả vào `docs/tasks/phase4_calls.md` và tick mục TURN (người dùng tick)

---

## 3. Tiêu chí hoàn thành

- [x] Seed chạy 1 lần tạo đủ dữ liệu; chạy lại không nhân đôi; không ảnh hưởng production
- [x] Demo 3 tài khoản chạy trọn vẹn trên web (bao gồm một cuộc gọi thật và admin khóa/mở khóa)
  *(cuộc gọi đã kiểm chứng thật ở GĐ 4; đợt này kiểm chứng social/chat/admin bằng HTTP — xem mục 4)*
- [x] `docker compose ... up -d` + backend + web hoạt động theo README
- [x] `docs/06_known_issues.md` có đủ lỗi đang mở, phân loại rõ
- [x] `docs/openapi/comink.v1.json` có Bearer scheme, 45 path, tái sinh được
- [x] `dotnet build` 0 warning; `dotnet test` xanh; `npm run lint`/`build` sạch
- [x] Tài liệu cập nhật: README, `.env.example`, `docs/04_roadmap.md`, task file này

---

## 4. Nhật ký kiểm chứng (2026-10-27)

### 4.1. Tự động

| Hạng mục | Kết quả |
|---|---|
| `dotnet build` | **0 warning, 0 error** |
| `dotnet test` | **193/193** (Domain 68 · Application 25 · Tích hợp **100**) — thêm `DemoDataSeederTests` |
| `npm run lint` / `npm run build` | 0/0 · thành công (không đổi web) |

`DemoDataSeederTests` kiểm tra: tắt/thiếu mật khẩu → không tạo gì; lần 1 tạo đúng 3 user, 3 friendship
(2 accepted), 3 follow, 5 post, 3 comment, 4 like, 3 conversation (1 group), 8 message; mọi hội thoại có
`last_message_id`; cột đếm đúng; lần 2 không nhân đôi.

### 4.2. Seed thật trên DB dev

- Bật bằng biến môi trường tạm `DemoSeed__Enabled=true` + `DemoSeed__Password=…` (không ghi vào repo).
- Log: `Đã tạo dữ liệu demo: 3 tài khoản …, 3 quan hệ bạn bè, 3 theo dõi, 5 bài viết, 3 bình luận, 4 lượt thích, 3 hội thoại, 8 tin nhắn`.
- PostgreSQL xác nhận 3 user `*.demo`, dữ liệu liên quan đầy đủ.

### 4.3. HTTP thật với 3 tài khoản demo — **12/12 đạt**

| # | Phép kiểm tra | Kết quả |
|---|---|---|
| 1 | Đăng nhập `an.demo`, `binh.demo`, `chi.demo` | `200` cả ba |
| 2 | Bảng tin của An có bài của Bình + Chi | `200`, 5 bài |
| 3 | Bài An có 2 like + 2 comment | đúng |
| 4 | Hồ sơ An: 2 bạn, 2 follower, 1 following | đúng |
| 5 | Danh sách bạn bè An | 2 `Accepted` |
| 6 | Chi có 1 lời mời đang chờ từ Bình | đúng |
| 7 | An có 2 hội thoại (1–1 + nhóm 3 người) | đúng |
| 8 | Lịch sử tin: 1–1 = 3, nhóm = 3 | đúng |
| 9 | Trang cá nhân An với Bình: chỉ 1 bài `Public` (bài `OnlyMe` ẩn) | đúng |
| 10 | Admin khóa Chi → đăng nhập `403 AUTH_ACCOUNT_LOCKED`; mở khóa → đăng nhập `200` | đúng |
| 11 | Tìm kiếm vẫn thấy tài khoản thường | đúng |
| 12 | OpenAPI có Bearer + 45 path | đúng |

### 4.4. OpenAPI

- `securitySchemes.Bearer`: `type=http`, `scheme=bearer`, `bearerFormat=JWT`.
- `/api/users` có `security: [{ Bearer: [] }]`; `/api/auth/login` và `/health` **không** có.
- Bản chụp: `docs/openapi/comink.v1.json` (~100 KB, 45 path); hướng dẫn tái sinh: `docs/openapi/README.md`.

### 4.5. Docker Compose

- `docker compose -f infra/docker-compose.yml config -q`: hợp lệ.
- `up -d`: 4/4 container `Running`; Postgres/Redis/Mailpit `healthy`.
- Backend (`localhost:5117`) + web (`localhost:5173`) chạy theo README; health check `healthy`.

### 4.6. Còn lại (cần người dùng)

- **TURN** — test `?relay=1` giữa hai mạng (KI-01). Sau khi đạt, tick mục GĐ 4.
- **Tick checklist GĐ 1–5** trong `docs/04_roadmap.md` (quy ước `AGENTS.md` mục 12) để chính thức đạt Mốc A.
- Mật khẩu tài khoản demo do bạn đặt trong `DemoSeed:Password`; lần kiểm chứng này dùng mật khẩu tạm (trao đổi trực tiếp, không commit).
