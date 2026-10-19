# Comink

Mạng xã hội thu nhỏ hỗ trợ nhắn tin thời gian thực và gọi thoại/video.

> Đồ án chuyên ngành — 2 lập trình viên, 3 tháng.

---

## Yêu cầu

| Phần mềm | Phiên bản tối thiểu |
|---|---|
| [.NET SDK](https://dotnet.microsoft.com/) | 10.0 |
| [Node.js](https://nodejs.org/) | 20 LTS |
| [Flutter SDK](https://flutter.dev/) | stable |
| [Docker Desktop](https://www.docker.com/products/docker-desktop/) | 4.x |
| [Git](https://git-scm.com/) | 2.x |

---

## Khởi động từ đầu (First-time setup)

### 1. Clone repo

```bash
git clone <repo-url>
cd comink
```

### 2. Cấu hình biến môi trường

```bash
# Sao chép file mẫu
cp .env.example .env
# Sửa các giá trị placeholder trong .env theo môi trường của bạn
```

File `.env` phục vụ hai cơ chế:

- **Docker Compose** đọc biến hạ tầng (`POSTGRES_USER`, `POSTGRES_PASSWORD`, `REDIS_PASSWORD`…) để khởi động container.
- **Backend** nạp `.env` khi khởi động (thư viện `DotNetEnv`, gọi trong `Program.cs`) và đọc các khoá theo quy ước .NET: `ConnectionStrings__Postgres` tương ứng `ConnectionStrings:Postgres`.

Thứ tự ưu tiên (cao hơn thắng):
**biến môi trường hệ điều hành → user-secrets → file `.env` → `appsettings.Development.json`**

> Vì `appsettings.Development.json` thắng `.env`, không đặt bí mật trong file đó. Bí mật thuộc về `.env` (đã bị `.gitignore`) hoặc `dotnet user-secrets`.

Bí mật khi phát triển nên đặt bằng `dotnet user-secrets` (không bao giờ vào Git):

```bash
cd backend/src/Comink.Api
dotnet user-secrets set "AdminSeed:Password" "<mật-khẩu-admin>"
dotnet user-secrets set "Jwt:Secret" "<chuỗi-ngẫu-nhiên-ít-nhất-32-ký-tự>"
```

LiveKit yêu cầu API secret **dài tối thiểu 32 ký tự**; giá trị dev phải khớp ở ba nơi: `infra/livekit.yaml`, `.env` và `appsettings.Development.json`.

### 2b. Cài hook chặn bí mật (làm một lần)

Repository có hook `pre-commit` chặn commit chứa khoá API, mật khẩu, private key. Bật bằng:

```bash
git config core.hooksPath .githooks
chmod +x .githooks/pre-commit     # macOS/Linux
```

Hook chặn cả các file như `.env`, `serviceAccount*.json`, `*.pem`. Bỏ qua khi thật cần: `git commit --no-verify`.

### 3. Khởi động hạ tầng (PostgreSQL, Redis, Mailpit, LiveKit)

```bash
docker compose -f infra/docker-compose.yml up -d
```

Kiểm tra tất cả service đang chạy:

```bash
docker compose -f infra/docker-compose.yml ps
```

| Service | URL |
|---|---|
| PostgreSQL | `localhost:5432` |
| Redis | `localhost:6379` |
| Mailpit (UI) | http://localhost:8025 |
| LiveKit | `localhost:7880` |

### 4. Chạy migration và seed database

```bash
cd backend/src/Comink.Api
dotnet ef database update --project ../Comink.Infrastructure
```

### 5. Chạy backend

```bash
cd backend/src/Comink.Api
dotnet run
# OpenAPI: http://localhost:5117/openapi/v1.json
# Health check: http://localhost:5117/health
```

### 6. Chạy web

```bash
cd web
npm install
npm run dev
# http://localhost:5173
```

### 6c. Gọi thoại/video (Giai đoạn 4 — LiveKit + TURN)

LiveKit đã nằm trong `infra/docker-compose.yml` (`comink_livekit`), dùng chung `api_key`/`api_secret` với backend (`appsettings.Development.json` mục `LiveKit` và `.env`).

| Hạng mục | Giá trị dev |
|---|---|
| API/WebSocket | `http://localhost:7880` (WebRTC) |
| RTC | TCP `7881`, UDP `7882` |
| TURN/UDP | `3478` + dải relay `30500–30510` (khai báo trong `infra/livekit.yaml`) |
| TURN domain | **IP LAN của máy bạn** — sửa `turn.domain` trong `infra/livekit.yaml` rồi `docker restart comink_livekit` |

Lưu ý khi test gọi giữa hai máy:

- **Camera/micro chỉ chạy trên secure context**: `http://localhost` được coi là an toàn, còn `http://192.168.x.x:5173` **thì không** (trình duyệt chặn `getUserMedia`). Muốn test từ máy khác phải bật HTTPS cho Vite, hoặc mở Chrome với `--unsafely-treat-insecure-origin-as-secure=http://192.168.1.29:5173`.
- Mở firewall cho UDP `7882`, `3478` và dải `30500–30510` nếu Windows hỏi.
- TURN chỉ chạy UDP (LiveKit bắt buộc TLS cert nếu bật `tls_port`). Muốn TURN/TLS: cấu hình `cert_file`/`key_file` và mở cổng 5349.
- Webhook LiveKit trỏ về `http://host.docker.internal:5117/api/webhooks/livekit`; LiveKit gửi JWT **trần** trong header `Authorization` (không kèm `Bearer `) — backend đã xử lý cả hai dạng.

### 6b. Ảnh trong bài viết (Giai đoạn 2)

Ảnh được tải lên **trực tiếp** bằng chữ ký do backend cấp (`docs/02_tech_stack_decisions.md` mục 3.4). Chọn một trong hai cách:

| Cách | Cấu hình | Kết quả |
|---|---|---|
| **Cloudinary** (production) | Điền `MediaStorage__CloudName`, `MediaStorage__ApiKey`, `MediaStorage__ApiSecret` trong `.env` | Client tải thẳng lên Cloudinary; backend chỉ cấp chữ ký |
| **Đĩa cục bộ** (dev, mặc định khi để trống 3 khoá) | Không cần làm gì | Backend lưu tệp vào `infra/uploads/<context>/` và phục vụ tại `/uploads/...` |

- Proxy của Vite đã chuyển tiếp `/uploads` về backend — thiếu mục này thì ảnh trong bài viết sẽ bị vỡ.
- Giới hạn: `image/jpeg`, `image/png`, `image/webp`, ≤ 5 MB/ảnh, ≤ 10 ảnh/bài (kiểm tra ở cả client và server).
- Thư mục `infra/uploads` là dữ liệu dev — không commit (xem `.gitignore`).

### 7. Chạy app Flutter (tuỳ chọn)

```bash
cd mobile
flutter pub get
flutter run
```

---

## Cấu trúc dự án

```
comink/
├── docs/                   # Tài liệu dự án (nguồn chân lý)
│   ├── 01_COMINK_SPEC.md
│   ├── 02_tech_stack_decisions.md
│   ├── 03_database_design.md
│   └── 04_roadmap.md
├── backend/                # ASP.NET Core — Clean Architecture
│   ├── Comink.slnx
│   ├── src/
│   │   ├── Comink.Domain/
│   │   ├── Comink.Application/
│   │   ├── Comink.Infrastructure/
│   │   └── Comink.Api/
│   └── tests/
│       ├── Comink.Domain.Tests/
│       ├── Comink.Application.Tests/
│       └── Comink.Api.IntegrationTests/
├── web/                    # React + TypeScript + Vite
├── mobile/                 # Flutter
├── infra/                  # Docker Compose + cấu hình dịch vụ
├── .github/workflows/      # CI/CD (GitHub Actions)
├── .env.example            # Biến môi trường mẫu
└── README.md
```

---

## Lệnh thường dùng

| Lệnh | Mục đích |
|---|---|
| `docker compose -f infra/docker-compose.yml up -d` | Khởi động hạ tầng |
| `docker compose -f infra/docker-compose.yml down` | Dừng hạ tầng |
| `dotnet build` (trong `backend/`) | Build backend |
| `dotnet test` (trong `backend/`) | Chạy test backend |
| `dotnet format` (trong `backend/`) | Format code |
| `dotnet ef migrations add <Tên>` | Tạo migration mới |
| `dotnet ef database update` | Áp dụng migration |
| `npm run dev` (trong `web/`) | Chạy web dev server |
| `npm run build` (trong `web/`) | Build web production |
| `npm run lint` (trong `web/`) | Lint web |
| `flutter analyze` (trong `mobile/`) | Analyze Flutter |
| `flutter test` (trong `mobile/`) | Test Flutter |

---

## Quy tắc làm việc

- **Nhánh:** `main` luôn chạy được; làm việc trên `feature/<tên-tính-năng>`
- **Commit:** Conventional Commits (`feat:`, `fix:`, `docs:`, `chore:`, v.v.)
- **Migration:** chỉ một người tạo migration tại một thời điểm; `git pull` trước khi tạo
- **PR:** phải được review chéo trước khi merge vào `main`
- **Bí mật:** không bao giờ commit `.env` hoặc khóa API; chỉ commit `.env.example`

Xem thêm: [`AGENTS.md`](./AGENTS.md) và [`docs/`](./docs/).
