# Comink — Quyết định công nghệ (Tech Stack Decisions)

> Tài liệu 02, tiếp nối `COMINK_SPEC.md`. Mục đích: chốt công nghệ cho từng phần của dự án và thống nhất các nguyên tắc để hai thành viên cùng phát triển mà không lệch hướng.

## 0. Cách đọc tài liệu

Mỗi quyết định có một trạng thái:

| Trạng thái | Ý nghĩa |
|---|---|
| Đã chốt  | Nhóm đã quyết định (từ phần trao đổi và `COMINK_SPEC.md`) |
| Đề xuất  | Gợi ý mặc định kèm lý do, nhóm xác nhận hoặc đổi trước khi bắt đầu code |
| Cần chốt | Còn nhiều lựa chọn, cần nhóm quyết định (xem mục 7) |

Phiên bản thư viện: dùng bản **LTS / stable mới nhất tại thời điểm bắt đầu dự án**, rồi **khóa phiên bản** bằng `global.json`, `package-lock.json`, `.nvmrc`, `pubspec.lock` để hai máy build giống nhau.

---

## 1. Kiến trúc tổng thể

### 1.1. Sơ đồ tổng quan

```
 ┌──────────────┐   ┌──────────────┐
 │  Web (React  │   │ App (Flutter)│
 │  + TS + Vite)│   │              │
 └──────┬───────┘   └──────┬───────┘
        │  REST (HTTPS)    │   SignalR (WebSocket)
        └────────┬─────────┴──────────┐
                 ▼                    │
      ┌─────────────────────────────────────┐
      │   ASP.NET Core (Monolith)           │
      │   Clean Architecture                │
      │   API + SignalR Hubs + Background   │
      └───┬─────────┬──────────┬────────────┘
          │         │          │
          ▼         ▼          ▼
     PostgreSQL    Redis     Dịch vụ ngoài
     (dữ liệu)   (presence,   ├─ LiveKit (gọi thoại/video + TURN)
                  cache,      ├─ Cloudinary (ảnh, video, tệp)
                  backplane,  ├─ Firebase Cloud Messaging (push)
                  rate limit) ├─ SMTP/Email provider (xác minh, reset)
                              └─ LLM API: Claude/OpenAI/Gemini (AI)
```

### 1.2. Quyết định kiến trúc chính

| # | Quyết định | Trạng thái | Lý do |
|---|---|---|---|
| A1 | **Modular monolith** theo Clean Architecture, một backend dùng chung cho web và app | Đã chốt | Phù hợp nhóm 2 người, 3 tháng; dễ debug, dễ deploy bằng Docker Compose |
| A2 | **REST** cho thao tác dữ liệu, **SignalR** cho sự kiện thời gian thực | Đã chốt (SignalR) / Đề xuất (mô hình dùng) | Xem mục 1.4 |
| A3 | **PostgreSQL** là nguồn dữ liệu duy nhất; **Redis** chỉ chứa dữ liệu tạm | Đã chốt | Mất Redis không được làm mất dữ liệu nghiệp vụ |
| A4 | Media (gọi, lưu tệp) giao cho **dịch vụ bên thứ ba**, backend chỉ cấp token/chữ ký | Đã chốt | Không tự xây hạ tầng media |
| A5 | **Một API contract chung** (OpenAPI) sinh client cho web và Flutter | Đề xuất | Hai người làm hai nền tảng, tránh lệch kiểu dữ liệu |
| A6 | Mọi nghiệp vụ nằm ở backend; client chỉ hiển thị và gọi API | Đề xuất | Web và app dùng chung quy tắc, dễ bảo mật |

### 1.3. Cấu trúc backend (Clean Architecture)

```
Comink.sln
├── src/
│   ├── Comink.Domain          # Entity, value object, enum, domain event (không phụ thuộc gì)
│   ├── Comink.Application     # Use case, DTO, interface (IRepository, IAiProvider, ...), validation
│   ├── Comink.Infrastructure  # EF Core, Redis, Cloudinary, LiveKit, FCM, email, AI provider
│   └── Comink.Api             # Controller/Minimal API, SignalR Hub, middleware, DI, cấu hình
└── tests/
    ├── Comink.Domain.Tests
    ├── Comink.Application.Tests
    └── Comink.Api.IntegrationTests
```

Quy tắc phụ thuộc: `Api → Application → Domain` và `Infrastructure → Application → Domain`. **Domain và Application không tham chiếu Infrastructure.**

**Chia theo tính năng bên trong mỗi tầng** (vertical slice) để hai người ít đụng file nhau:

```
Comink.Application/
├── Identity/      # đăng ký, đăng nhập, phiên, xác minh email
├── Social/        # hồ sơ, bạn bè, theo dõi, bài viết, bình luận, hashtag
├── Messaging/     # hội thoại, tin nhắn, đã đọc, ghim, chuyển tiếp
├── Calls/         # trạng thái cuộc gọi, lịch sử, cấp token LiveKit
├── Media/         # tải lên, album, quản lý tệp
├── Admin/         # kiểm duyệt, báo cáo, thống kê, nhật ký
├── Notifications/ # thông báo trong app và push
└── Ai/            # chatbot, hỗ trợ viết, tóm tắt
```

### 1.4. Mô hình giao tiếp thời gian thực (đề xuất)

- **Gửi dữ liệu (tạo tin nhắn, bình luận, ...) qua REST**, sau đó server phát sự kiện qua SignalR tới các client liên quan. Lý do: dễ kiểm thử, dễ thử bằng Swagger, có mã lỗi HTTP rõ ràng, app mobile mất kết nối vẫn gọi lại được.
- **Chỉ những tín hiệu nhẹ, tạm thời đi thẳng qua Hub:** đang nhập (typing), ping presence.
- **Mỗi cuộc hội thoại là một SignalR group**; mỗi người dùng có thêm group riêng (`user:{id}`) để nhận thông báo và trạng thái cuộc gọi đến.
- **Client sinh sẵn `clientMessageId`** khi gửi tin để chống gửi trùng và cho phép hiển thị tin "đang gửi" (optimistic UI).
- **Phân trang theo con trỏ (cursor)** cho lịch sử tin nhắn và bảng tin, không dùng offset.
- **Đã đọc lưu theo `lastReadMessageId` của từng thành viên** trong hội thoại, không lưu một dòng cho mỗi tin nhắn.
- **Presence** lưu trong Redis với TTL; đếm số kết nối của mỗi người dùng để xử lý nhiều tab/thiết bị.
- **Xác thực SignalR** bằng access token truyền qua query string `access_token` (bắt buộc với WebSocket), kiểm tra quyền vào group ở phía server.
- Khi bật nhiều instance backend (giai đoạn production): dùng **Redis backplane** cho SignalR.

### 1.5. Luồng gọi thoại/video (đề xuất)

1. Người gọi gửi yêu cầu gọi qua REST → backend tạo bản ghi `Call` và một **room LiveKit**.
2. Backend phát sự kiện "cuộc gọi đến" qua SignalR tới người nhận (và push FCM nếu offline).
3. Người nhận chấp nhận/từ chối qua REST → backend cập nhật trạng thái, phát sự kiện cho các bên.
4. Mỗi bên xin **access token LiveKit** từ backend (token ngắn hạn, gắn quyền theo room) rồi kết nối trực tiếp tới LiveKit.
5. **Webhook của LiveKit** báo room kết thúc / người tham gia rời → backend ghi thời lượng, cuộc gọi nhỡ, lịch sử.

Gọi nhóm chỉ là room có nhiều thành viên, dùng chung luồng trên.

---

## 2. Tech stack theo nền tảng

### 2.1. Backend

| Hạng mục | Công nghệ | Trạng thái |
|---|---|---|
| Framework | ASP.NET Core (.NET LTS mới nhất) | Đã chốt |
| Real-time | SignalR | Đã chốt |
| ORM / truy cập dữ liệu | EF Core + Npgsql, migrations | Đề xuất |
| Cơ sở dữ liệu | PostgreSQL | Đã chốt |
| Cache, presence, rate limit, backplane | Redis (StackExchange.Redis) | Đã chốt |
| Xác thực | ASP.NET Core Identity + JWT access token + refresh token xoay vòng | Đề xuất |
| Phân quyền | Role (User/Admin) + policy theo tài nguyên (thành viên hội thoại, chủ bài viết) | Đề xuất |
| Validation | FluentValidation | Đề xuất |
| Rate limiting | `AddRateLimiter` có sẵn của ASP.NET Core, kết hợp Redis khi nhiều instance | Đề xuất |
| Tác vụ nền | `BackgroundService` / hosted service (cân nhắc Hangfire nếu cần lên lịch phức tạp) | Đề xuất |
| Log | Serilog (console + file, có correlation id) | Đề xuất |
| Tài liệu API | Swagger / OpenAPI | Đề xuất |
| Kiểm thử | xUnit + Testcontainers (PostgreSQL, Redis thật) | Đề xuất |

<!-- Lưu ý khi chọn thư viện: kiểm tra **giấy phép** trước khi dùng (một số thư viện .NET phổ biến đã chuyển sang bản thương mại). Ưu tiên tự viết lớp use case đơn giản thay vì thêm thư viện trung gian không cần thiết. -->

### 2.2. Web (React + TypeScript + Vite)

| Hạng mục | Công nghệ | Trạng thái |
|---|---|---|
| Ngôn ngữ / build | React + TypeScript (strict) + Vite | Đã chốt |
| Định tuyến | React Router | Đề xuất |
| Dữ liệu từ server | TanStack Query | Đề xuất |
| State phía client | Zustand (chỉ cho trạng thái giao diện, phiên, kết nối) | Đề xuất |
| Real-time | `@microsoft/signalr` | Đã chốt (SignalR) |
| Gọi thoại/video | SDK của nhà cung cấp (LiveKit: `livekit-client`, `@livekit/components-react`) | Đề xuất |
| Đa ngôn ngữ | i18next (vi, en) | Đề xuất |
| Giao diện | Tailwind CSS v4 + shadcn/ui | Đã chốt |
| Form / validate | React Hook Form + Zod | Đề xuất |
| Client API | Sinh tự động từ OpenAPI (ví dụ `orval` hoặc `openapi-typescript`) | Đề xuất |
| Push | Firebase Cloud Messaging + service worker | Đã chốt (push) |

### 2.3. Mobile (Flutter)

| Hạng mục | Công nghệ | Trạng thái |
|---|---|---|
| Framework | Flutter (stable) | Đã chốt |
| Quản lý state | Riverpod | Đề xuất |
| HTTP | dio (interceptor tự refresh token) | Đề xuất |
| Điều hướng | go_router | Đề xuất |
| Real-time | package SignalR cho Dart (ví dụ `signalr_netcore`) | Đã chốt (SignalR) |
| Gọi thoại/video | SDK Flutter của nhà cung cấp (LiveKit: `livekit_client`) | Đã chốt |
| Lưu token | `flutter_secure_storage` | Đề xuất |
| Đa ngôn ngữ | `flutter_localizations` + `intl` (hoặc `slang`) | Đề xuất |
| Push | `firebase_messaging` (FCM, thêm `flutter_local_notifications` để hiển thị) | Đã chốt (push) |
| Client API | Sinh từ OpenAPI (openapi-generator cho Dart) hoặc viết tay có kiểm soát | Đề xuất |

### 2.4. Hạ tầng và công cụ

| Hạng mục | Công nghệ | Trạng thái |
|---|---|---|
| Container | Docker + Docker Compose | Đã chốt |
| CI/CD | GitHub Actions (build, test, lint; sau đó build image, deploy) | Đã chốt |
| Quản lý mã nguồn | Git, 1 Monorepo (`comink/`) | Đã chốt |
| Email dev | Mailpit (nhận mail thử, chạy trong Compose cổng 1025/8025) | Đã chốt |
| Email production | Resend (tài khoản đã có, cấu hình qua SMTP/API) | Đã chốt |
| Reverse proxy / HTTPS | Caddy hoặc Nginx khi triển khai Internet | Đề xuất |

**Docker Compose cho môi trường dev** nên có sẵn: `postgres`, `redis`, `mailpit`, `livekit` (bản self-host chế độ dev), còn backend và frontend có thể chạy trực tiếp ngoài Docker để debug thuận tiện.

---

## 3. Tech stack theo từng tính năng

Phần này nối trực tiếp với các nhóm tính năng trong `COMINK_SPEC.md`.

### 3.1. Mạng xã hội
| Nhu cầu | Giải pháp |
|---|---|
| Hồ sơ, bài viết, bình luận, thích | EF Core + PostgreSQL; REST API |
| Bạn bè (hai chiều) và theo dõi (một chiều) | Hai bảng tách biệt: `Friendship` (có trạng thái chờ/đồng ý) và `Follow` |
| Bảng tin | Giai đoạn đầu: truy vấn trực tiếp bài của bạn bè và người đang theo dõi, sắp xếp theo thời gian, phân trang con trỏ. Chưa cần xếp hạng phức tạp |
| Tìm kiếm người dùng/bài viết | PostgreSQL full-text search (`tsvector`) và `pg_trgm` cho tìm gần đúng tên |
| Hashtag (NC) | Bảng `Hashtag` + bảng nối với bài viết, trích xuất khi lưu bài |
| Lưu bài viết (NC) | Bảng `SavedPost` |
| Gợi ý bạn bè (NC) | Truy vấn "bạn của bạn" (mutual friends) bằng SQL, chưa cần máy học |
| Đếm lượt thích/bình luận | Cột đếm (counter) cập nhật khi thao tác, có thể cache bằng Redis nếu cần |

### 3.2. Nhắn tin real-time
| Nhu cầu | Giải pháp |
|---|---|
| Kênh thời gian thực | SignalR Hub (`ChatHub`) + group theo hội thoại |
| Lưu và xem lịch sử | PostgreSQL, bảng `Conversation`, `ConversationMember`, `Message`; phân trang con trỏ |
| Chat riêng và nhóm | Dùng chung một mô hình `Conversation` (loại: direct/group), tránh viết hai luồng riêng |
| Trạng thái online | Redis (key có TTL) + sự kiện qua SignalR |
| Đang nhập | Tín hiệu qua Hub, không lưu DB, có throttle phía client |
| Đã gửi/đã đọc | `lastReadMessageId` mỗi thành viên; phát sự kiện khi cập nhật |
| Phản hồi, chuyển tiếp, ghim | Cột `ReplyToMessageId`, `ForwardedFromMessageId`, bảng/cờ ghim theo hội thoại |
| Thu hồi và chỉnh sửa | **Soft delete** (đánh dấu thu hồi, giữ khung), lưu `EditedAt`; phát sự kiện cập nhật |
| Cảm xúc | Bảng `MessageReaction` (mỗi người một cảm xúc/tin hoặc theo quy tắc nhóm chọn) |
| Tìm kiếm tin nhắn | PostgreSQL full-text search, **luôn lọc theo hội thoại mà người dùng là thành viên** |
| Tắt thông báo từng cuộc | Cột `MutedUntil` trong `ConversationMember` |

### 3.3. Gọi trực tuyến
| Nhu cầu | Giải pháp |
|---|---|
| Media, gọi 1–1 và nhóm, thoại và video | **LiveKit** (đề xuất): mã nguồn mở, có SDK cho web và Flutter, dùng được bản cloud miễn phí hoặc tự host |
| TURN | LiveKit có sẵn TURN tích hợp; khi tự host cần mở cổng UDP/TCP theo tài liệu |
| Signaling cuộc gọi đến / chấp nhận / từ chối | SignalR (kênh `user:{id}`) + REST |
| Thời lượng, nhỡ, lịch sử | Bảng `Call`, `CallParticipant`; cập nhật nhờ webhook LiveKit |
| Tắt mic/camera | Xử lý phía SDK; trạng thái tham gia lấy từ LiveKit |
| Cuộc gọi đến khi app đóng | Push FCM mức ưu tiên cao |

> Lý do đề xuất LiveKit thay vì Agora/Twilio: có thể chạy cục bộ bằng Docker khi demo (không phụ thuộc mạng/chi phí), có gói cloud miễn phí để thử, cùng một SDK cho web và Flutter. Nếu nhóm chọn nhà cung cấp khác, chỉ cần thay lớp `ICallProvider` trong Infrastructure.

### 3.4. Hình ảnh và tệp
| Nhu cầu | Giải pháp |
|---|---|
| Lưu trữ | Cloudinary (đề xuất; ảnh, video và tệp thô) |
| Cách tải lên | **Client tải thẳng lên Cloudinary bằng chữ ký do backend cấp** (không đi qua backend) |
| Dữ liệu lưu trong DB | `PublicId`, URL, loại, kích thước, chủ sở hữu, thời điểm tạo |
| Giới hạn dung lượng/định dạng | Kiểm tra **cả hai phía**: client chặn sớm, backend/preset Cloudinary chặn thật |
| Xem trước, tải xuống | Ảnh/video xem trực tiếp; tài liệu xem trước ở mức biểu tượng + tải xuống |
| Album (NC) | Bảng `Album`, `AlbumItem` |
| Quản lý tệp đã gửi (NC) | Truy vấn theo hội thoại/người gửi; xóa cả bản ghi và tệp trên cloud |

### 3.5. Quản trị viên
| Nhu cầu | Giải pháp |
|---|---|
| Giao diện quản trị | Một khu vực riêng trong web React (route `/admin`, bảo vệ bằng role) |
| Khóa/mở khóa | Cờ trạng thái trên tài khoản; khóa thì thu hồi refresh token và ngắt kết nối SignalR |
| Báo cáo vi phạm, kiểm duyệt (NC) | Bảng `Report` + trạng thái xử lý; hành động ẩn bài/xóa bình luận |
| Nhật ký quản trị (NC) | Bảng `AuditLog` ghi mọi thao tác của admin |
| Thống kê/dashboard (NC) | Truy vấn tổng hợp SQL, cache ngắn hạn trong Redis; biểu đồ bằng thư viện chart phía web |

App Flutter **không cần** giao diện quản trị trong phạm vi đồ án.

### 3.6. Bảo mật và quyền riêng tư
| Nhu cầu | Giải pháp |
|---|---|
| Đăng nhập, phân quyền | ASP.NET Core Identity + JWT; role User/Admin |
| Kiểm tra đầu vào | FluentValidation ở backend (bắt buộc), validate ở client chỉ để tiện cho người dùng |
| Xác minh email, đặt lại mật khẩu (NC) | Token do Identity sinh, gửi qua dịch vụ email; link có hạn dùng |
| Quản lý phiên (NC) | Bảng refresh token theo thiết bị, cho phép xem và đăng xuất từ xa |
| Chặn người dùng (NC) | Bảng `Block`; áp dụng ở tìm kiếm, nhắn tin, gọi, bảng tin |
| Quyền truy cập từng hội thoại (NC) | Kiểm tra thành viên ở **mọi** REST endpoint và **mọi** lần join group SignalR |
| Giới hạn tần suất, chống spam (NC) | Rate limiter theo IP/người dùng; giới hạn riêng cho đăng nhập, gửi tin, kết bạn, báo cáo |
| Quyền riêng tư (NC) | Cài đặt hiển thị hồ sơ/bài viết (công khai, bạn bè, chỉ mình tôi) áp dụng tại tầng truy vấn |

### 3.7. Tính năng AI
| Nhu cầu | Giải pháp |
|---|---|
| Nhà cung cấp | API ngoài Geimini Free |
| Cách tích hợp | Interface `IAiProvider` trong Application; mỗi nhà cung cấp là một lớp trong Infrastructure, chọn bằng cấu hình |
| Khóa API | **Chỉ nằm ở backend**, không bao giờ gửi xuống web/app |
| Chatbot, hỗ trợ viết | Endpoint backend gọi LLM, trả kết quả dạng streaming nếu cần |
| Tóm tắt tin nhắn | Chỉ lấy tin nhắn của hội thoại mà người yêu cầu là thành viên; báo rõ cho người dùng rằng nội dung sẽ gửi tới dịch vụ AI |
| Kiểm soát chi phí | Giới hạn số lần gọi theo người dùng, giới hạn độ dài đầu vào, ghi log mức sử dụng |

### 3.8. Triển khai
| Giai đoạn | Cách làm |
|---|---|
| Demo máy cá nhân (CB) | Docker Compose cho hạ tầng; backend/web chạy local; cấu hình qua biến môi trường |
| Truy cập từ thiết bị khác trong LAN (NC) | Backend bind `0.0.0.0`, cấu hình CORS, **dùng HTTPS** (xem lưu ý dưới) |
| Internet / production (NC) | Docker image cho từng thành phần, reverse proxy có HTTPS, biến môi trường/secret tách riêng, Redis backplane, sao lưu PostgreSQL |

> **Lưu ý quan trọng cho demo qua LAN:** trình duyệt chỉ cho phép dùng micro/camera và service worker (push) trên **ngữ cảnh an toàn** (HTTPS hoặc `localhost`). Khi mở web từ máy khác bằng địa chỉ IP qua HTTP, gọi thoại/video và web push sẽ **không hoạt động**. Cần chuẩn bị chứng chỉ HTTPS cục bộ (ví dụ `mkcert`) hoặc đường hầm HTTPS. Với app Flutter trên điện thoại thật, cần cho phép backend truy cập được từ mạng đó (và cấu hình cho phép HTTP/HTTPS tương ứng trên Android/iOS).

### 3.9. Tính năng xuyên suốt
| Nhu cầu | Giải pháp |
|---|---|
| Push notification | Firebase Cloud Messaging cho cả web và Flutter; backend lưu token thiết bị, gửi qua FCM |
| Thông báo trong app | Bảng `Notification` + sự kiện SignalR tới `user:{id}` |
| Đa ngôn ngữ vi/en | Web: i18next; Flutter: `intl`; **thông báo lỗi từ backend trả mã lỗi (error code), client tự dịch** |

---

## 4. Nguyên tắc chung khi phát triển

### 4.1. Nguyên tắc làm việc nhóm
1. **Chia việc theo tính năng (vertical slice):** mỗi người phụ trách trọn một tính năng (backend + web, sau đó mobile), hạn chế sửa chéo vào phần của người kia.
2. **Hợp đồng API trước, code sau:** thống nhất endpoint, DTO, tên sự kiện SignalR (viết vào tài liệu hoặc OpenAPI) trước khi cả hai bắt đầu cài đặt.
3. **Nhánh Git:** `main` luôn chạy được; làm trên nhánh `feature/<tên>`; gộp bằng Pull Request, **người còn lại review** (ngắn gọn cũng được, nhưng không tự gộp khi chưa ai xem).
4. **Commit nhỏ, thông điệp rõ ràng** (khuyến nghị Conventional Commits: `feat:`, `fix:`, `refactor:`, `docs:`, `chore:`).
5. **Xung đột migration:** chỉ một người tạo migration tại một thời điểm; trước khi tạo phải `pull` bản mới nhất. Không sửa migration đã gộp vào `main`, tạo migration mới.
6. **Họp ngắn định kỳ** (đầu tuần chốt việc, cuối tuần demo cho nhau), cập nhật bảng công việc (GitHub Projects hoặc tương đương).

### 4.2. Nguyên tắc về mã nguồn
1. **Giữ đúng ranh giới tầng:** không đưa logic nghiệp vụ vào controller/hub; không để Domain/Application phụ thuộc Infrastructure.
2. **DTO tách khỏi Entity:** không trả entity EF Core ra API.
3. **Mọi lời gọi dịch vụ ngoài đi qua interface** (`IMediaStorage`, `ICallProvider`, `IPushSender`, `IEmailSender`, `IAiProvider`) để dễ đổi nhà cung cấp và dễ test.
4. **Bất đồng bộ đến cùng** (`async/await`, truyền `CancellationToken`), không chặn luồng bằng `.Result`/`.Wait()`.
5. **Tránh N+1:** dùng `Include`/projection hợp lý; kiểm tra các truy vấn của bảng tin và danh sách hội thoại.
6. **Đặt tên:** C# theo quy ước .NET; TypeScript bật `strict`, không dùng `any` tùy tiện; Dart chạy `flutter analyze`.
7. **Cấu hình qua biến môi trường**, không hard-code URL, khóa hay chuỗi kết nối.
8. **Tên sự kiện SignalR và mã lỗi** là hằng số dùng chung, ghi trong tài liệu, không viết tay rải rác.

### 4.3. Nguyên tắc về bảo mật (bắt buộc cho mọi tính năng)
1. **Không bao giờ commit bí mật:** khóa API (Cloudinary, LiveKit, FCM, AI, SMTP), chuỗi kết nối, JWT secret. Dùng `.env` (đã nằm trong `.gitignore`), `dotnet user-secrets` khi phát triển, và chỉ commit file mẫu `.env.example`. Nếu lỡ commit, **đổi khóa ngay**.
2. **Kiểm tra quyền ở backend cho mọi truy cập tài nguyên:** không tin ID do client gửi; luôn kiểm tra người dùng có phải thành viên hội thoại, chủ bài viết, hoặc được phép xem không (tránh lỗi truy cập trái phép theo ID).
3. **Kiểm tra quyền khi vào SignalR group,** không chỉ ở REST.
4. **Validate đầu vào ở server;** giới hạn độ dài, kích thước, định dạng. Nội dung bài viết/tin nhắn hiển thị dưới dạng **văn bản thuần** (hoặc được làm sạch) để tránh XSS.
5. **Token:** access token ngắn hạn; refresh token xoay vòng và có thể thu hồi; trên mobile lưu bằng secure storage.
6. **Cloud media:** upload preset giới hạn loại tệp và dung lượng; chữ ký upload có hạn dùng ngắn.
7. **Dữ liệu cá nhân:** không ghi log mật khẩu, token, nội dung tin nhắn đầy đủ.
8. **AI:** chỉ gửi cho dịch vụ AI những dữ liệu người dùng đã yêu cầu xử lý và có quyền truy cập.

### 4.4. Nguyên tắc về hiệu năng và độ tin cậy
1. Mọi danh sách có thể dài (tin nhắn, bảng tin, thông báo) **phải phân trang con trỏ**.
2. Đánh **index** cho các cột thường lọc/sắp xếp (ví dụ `ConversationId + CreatedAt`, `UserId`, `CreatedAt` của bài viết).
3. Redis chỉ chứa dữ liệu tạm, **đặt TTL cho mọi khóa**, hệ thống phải chạy được (chậm hơn) khi Redis rỗng.
4. Client SignalR phải **tự kết nối lại** và **đồng bộ lại dữ liệu bị lỡ** (lấy tin nhắn mới hơn tin cuối đã có) sau khi mất kết nối.
5. Thao tác có thể bị gửi trùng (gửi tin, kết bạn) phải **idempotent** hoặc có kiểm tra trùng.

### 4.5. Nguyên tắc về kiểm thử và chất lượng
1. **Mức tối thiểu có thể bảo vệ được:** unit test cho nghiệp vụ lõi (quyền truy cập hội thoại, trạng thái kết bạn/chặn, trạng thái cuộc gọi) và vài integration test cho luồng chính (đăng nhập, gửi nhận tin nhắn).
2. **CI chạy tự động** trên mỗi Pull Request: build, test, lint (`dotnet format`, ESLint, `flutter analyze`). PR đỏ thì không gộp.
3. **Kiểm thử thủ công theo kịch bản demo** trước mỗi mốc: hai tài khoản, hai trình duyệt/thiết bị, gửi tin, gọi, thu hồi, mất mạng rồi có lại.
4. Mỗi tính năng khi xong phải **có hướng dẫn chạy và dữ liệu mẫu (seed)** để người kia (và hội đồng) thử được ngay.

### 4.6. Nguyên tắc về giao diện và trải nghiệm
1. **Mọi chuỗi hiển thị đi qua hệ thống đa ngôn ngữ ngay từ đầu** (không hard-code tiếng Việt trong component), để tránh phải sửa lại hàng loạt về sau.
2. Có đủ **trạng thái tải, rỗng, lỗi** cho mọi màn hình dữ liệu.
3. Giao diện web **responsive**; app Flutter dùng cùng tên gọi và hành vi như web để người dùng không bối rối.
4. Quyền micro/camera và thông báo: giải thích lý do trước khi xin quyền, và xử lý đẹp khi người dùng từ chối.

### 4.7. Nguyên tắc về phạm vi
1. **Giữ đúng thứ tự:** hoàn thành mức cơ bản của nhóm tính năng hiện tại rồi mới sang nhóm kế tiếp; mức nâng cao chỉ làm khi mức cơ bản của chính nhóm đó đã xong.
2. **Web trước, app sau:** không bắt đầu app cho một tính năng khi web của tính năng đó chưa ổn định.
3. **AI chỉ làm sau khi các chức năng chính ổn định.**
4. Ý tưởng mới ngoài `COMINK_SPEC.md` được ghi vào danh sách "để sau", **không chen vào giữa chặng** khi chưa cả hai thống nhất.
5. **Quyết định công nghệ nào đổi** thì cập nhật vào tài liệu này (mục 6) kèm lý do, để hai người luôn nhìn cùng một bức tranh.

---

## 5. Cấu trúc kho mã nguồn (đề xuất)

Một **monorepo** giúp hai người đồng bộ API và tài liệu dễ hơn:

```
comink/
├── docs/                  # COMINK_SPEC.md, 02_tech_stack_decisions.md, tài liệu API/sự kiện
├── backend/               # solution .NET (xem mục 1.3)
├── web/                   # React + TS + Vite
├── mobile/                # Flutter
├── infra/                 # docker-compose.yml, cấu hình LiveKit, proxy
├── .github/workflows/     # CI/CD
├── .env.example
└── README.md              # cách chạy toàn bộ hệ thống từ đầu
```

Nếu nhóm chọn tách 3 repo, giữ **hợp đồng API (OpenAPI) làm nguồn chung** để hai bên luôn khớp.

---

## 6. Nhật ký quyết định (Decision Log)

Mỗi khi chốt hoặc đổi quyết định, thêm một dòng ở đây.

| Ngày | Quyết định | Lý do | Ai quyết |
|---|---|---|---|
| 2026-10-09 | Backend monolith Clean Architecture + Redis | Phù hợp quy mô nhóm và thời gian | Nhóm |
| 2026-10-09 | SignalR cho real-time | Hợp với backend .NET | Nhóm |
| 2026-10-09 | Gọi thoại/video qua dịch vụ bên thứ ba, có TURN | Không tự xây hạ tầng media | Nhóm |
| 2026-10-09 | Lưu tệp ở cloud (Cloudinary/Firebase) | Không tự quản lý lưu trữ | Nhóm |
| 2026-10-09 | AI qua API ngoài | Không chạy mô hình cục bộ | Nhóm |
| 2026-10-09 | Docker Compose + CI/CD | Môi trường đồng nhất cho 2 người | Nhóm |
| 2026-10-10 | LiveKit làm nhà cung cấp gọi | Chạy dev tự host có TURN, đồng nhất SDK Web/Flutter | Nhóm |
| 2026-10-10 | Cloudinary làm nơi lưu tệp | Hỗ trợ upload trực tiếp signed, quản lý ảnh/video mạnh mẽ | Nhóm |
| 2026-10-10 | Monorepo (`comink/`) | Đồng bộ hợp đồng API, dễ quản lý CI/CD và tài liệu | Nhóm |
| 2026-10-10 | Tailwind CSS v4 + shadcn/ui cho Web | Giao diện hiện đại, tối ưu hóa CSS, hỗ trợ component linh hoạt | Nhóm |
| 2026-10-10 | Minimal API + Exception & ProblemDetails | Code gọn gàng, hiệu năng cao, chuẩn RFC 7807 | Nhóm |
| 2026-10-10 | Mailpit (dev) + Resend (production) | Test cục bộ dễ dàng và đã sẵn có tài khoản Resend | Nhóm |
| 2026-10-10 | Sẵn sàng switch LLM trả phí trong tương lai | Trừu tượng hóa qua `IAiProvider` | Nhóm |
| 2026-10-10 | .NET 10 | Phiên bản SDK mới nhất, thống nhất qua `global.json` | Nhóm |

---

## 7. Các quyết định đã chốt trước khi bắt đầu code

Toàn bộ 9 mục đã được thống nhất và đưa vào thực thi trong Milestone 0:
1. **Nhà cung cấp gọi:** LiveKit (self-hosted dev với TURN tích hợp; SDK `livekit-client`, `livekit_client`).
2. **Nơi lưu tệp:** Cloudinary (upload qua signed URL, interface `IMediaStorage`).
3. **LLM:** Sẵn sàng switch LLM trả phí trong tương lai, hiện tại dùng interface `IAiProvider`.
4. **Email:** Mailpit khi local dev (localhost:1025), Resend cho production.
5. **Cấu trúc repo:** Monorepo (`comink/`).
6. **Thư viện giao diện web:** Tailwind CSS v4 + shadcn/ui, i18next, TanStack Query, Zustand.
7. **Phân chia công việc:** Theo 2 phân hệ (Dev A: Tài khoản, Mạng xã hội, Ảnh/tệp, Admin; Dev B: Nhắn tin, Gọi thoại/video, Push).
8. **HTTPS cho demo:** `mkcert` hoặc tunnel khi demo qua mạng LAN.
9. **Quy tắc Git:** Conventional Commits, branch `feature/<name>`, review qua Pull Request, CI tự động.


---