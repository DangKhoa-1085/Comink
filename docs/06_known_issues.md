# Comink — Danh sách lỗi/vấn đề đã biết (Known Issues)

> Cập nhật: **2026-10-27** (Mốc A — Web CB hoàn chỉnh).
> Mục đích: ghi lại các vấn đề **đang mở**, phân loại xem có chặn demo hay không, kèm cách xử lý tạm.
> Không dùng danh sách này thay cho `docs/04_roadmap.md` hay báo cáo kiểm thử.

## Mức độ

| Mức | Ý nghĩa |
|---|---|
| **BLOCKER** | Chặn một phần kịch bản demo bắt buộc (phải xử lý/tránh trước khi demo) |
| **MAJOR** | Ảnh hưởng trải nghiệm nhưng có cách tránh; không chặn toàn bộ demo |
| **MINOR** | Không ảnh hưởng kịch bản demo; cải thiện sau |
| **ENV** | Phụ thuộc môi trường/thiết bị, không phải lỗi mã nguồn |

## Tổng hợp

| ID | Nhóm | Vấn đề | Mức | Ảnh hưởng | Cách xử lý tạm | Kế hoạch |
|---|---|---|---|---|---|---|
| KI-01 | Gọi | **TURN chưa được chứng minh** khi hai máy ở hai mạng/NAT | **BLOCKER** *(chỉ kịch bản gọi 2 mạng)* | Không chắc media đi qua TURN; gọi cùng LAN/Tailscale vẫn chạy | Mở cả hai máy ở `…/calls/<id>?relay=1`; nếu nghe/nói được là media đã qua TURN `:3478` | Test 2 mạng, ghi kết quả, tick mục GĐ 4 |
| KI-02 | Gọi | **Camera/micro bị chặn khi mở web bằng IP LAN** (không phải `localhost`) | **BLOCKER** *(chỉ demo 2 máy qua LAN)* | `getUserMedia` undefined → không vào được media | Dùng `netsh portproxy` để mở bằng `http://localhost`, hoặc chạy Chrome với cờ `--unsafely-treat-insecure-origin-as-secure` | Làm HTTPS cục bộ (NC triển khai — GĐ 7) |
| KI-03 | Gọi | **`rtc.node_ip` chỉ quảng bá một IP**; đổi Wi-Fi/Tailscale là mất media | **BLOCKER** *(chỉ demo nhiều máy)* | Tín hiệu OK nhưng `could not establish pc connection` | Chạy `infra/set-lan-ip.ps1` (hoặc `-Tailscale`) sau mỗi lần đổi mạng; mọi máy dùng **cùng một đường mạng** | Tự động dò IP/HTTPS (GĐ 7) |
| KI-04 | Quản trị/Mạng xã hội | Tài khoản Admin đã ẩn khỏi tìm kiếm/hồ sơ, nhưng **danh sách bạn bè/người theo dõi và quan hệ cũ chưa lọc**; admin chủ động mời người thường vẫn hiện trong lời mời; bài Public của admin vẫn xem được qua `/api/posts/{id}` | MINOR | Chỉ là góc cạnh hiếm; admin không dùng mạng xã hội | Không dùng admin để kết bạn/đăng bài demo | Nếu cần “ẩn triệt để” (GĐ 7) |
| KI-05 | Xác thực | Access token cấp **trước** lúc khóa vẫn còn hạn 15 phút; nếu admin **mở khóa trong 15 phút đó**, token cũ có thể dùng lại | MINOR (bảo mật) | Browser đã bị đăng xuất, refresh token đã thu hồi nên khó xảy ra | Demo khóa tài khoản ở cuối kịch bản, không mở khóa ngay | Thêm security-stamp/mã phiên (GĐ 7 — quản lý phiên) |
| KI-06 | Push | **Chưa có service account Firebase/FCM cấu hình**; code backend + app đã có, sender đã test bằng mock OAuth/FCM nhưng chưa gửi push thật | ENV *(GĐ 6)* | Chưa có thông báo đẩy thực tế trên app | Thiếu cấu hình thì backend bỏ qua push, app vẫn chạy; dùng SignalR khi demo web | Điền `Firebase__ServiceAccountPath` + `--dart-define=FIREBASE_*` rồi test trên thiết bị thật |
| KI-07 | Môi trường | Android Studio 2026.2.2 + Android SDK (Platform 37.0) + `adb`/`emulator`/`flutter` đã cài và có trong PATH, `flutter build apk --debug` thành công (2026-11-03); **nhưng chưa có điện thoại thật nào kết nối** (`adb devices` trống) | ENV *(GĐ 6)* | Chưa kiểm chứng app chạy trên Android thật | Build APK và hướng dẫn kết nối máy thật ở `mobile/README.md` mục 3.1; có thể chạy emulator `Pixel_7` để xem UI | Cắm điện thoại (bật USB debugging) rồi chạy `flutter run --dart-define=API_BASE_URL=http://<IP-LAN>:5117` |
| KI-08 | Hạ tầng | `docker compose` chỉ dựng **hạ tầng** (Postgres/Redis/Mailpit/LiveKit); backend và web chạy riêng | MINOR | Người mới có thể hiểu là “một lệnh chạy tất cả” | Theo README: `docker compose up -d` → `dotnet run` → `npm run dev` | Cân nhắc thêm compose profile backend/web khi triển khai (GĐ 7) |
| KI-09 | Web | Bundle production **> 1 MB** (chủ yếu do LiveKit SDK) | MINOR | Tải trang chậm hơn ở mạng yếu; Vite chỉ cảnh báo | Chấp nhận cho demo cục bộ | Tách chunk theo route/`React.lazy` (GĐ 7–8) |
| KI-10 | Hợp đồng API | Webhook `/api/webhooks/livekit` **không có trong OpenAPI** (48 path còn lại đầy đủ) | MINOR | App không cần endpoint này | Không dùng cho client | Giữ nguyên (endpoint do LiveKit gọi, không phải client) |
| KI-11 | Seed demo | Bật `DemoSeed:Enabled` nhưng thiếu `DemoSeed:Password` → **bỏ qua seed** (chỉ log warning) | MINOR | Người mới tưởng seed lỗi | Đọc log backend + README mục “Dữ liệu mẫu” | Có thể thêm lệnh kiểm tra cấu hình khi khởi động (GĐ 7) |
| KI-12 | CI | Workflow chỉ chạy khi push/PR vào `main`/`develop`; nhánh `feature/develop` **chưa chạy CI** | MINOR | Không có tín hiệu CI trên nhánh tính năng | Mở PR vào `develop` (hoặc push `develop`) để CI chạy | Thống nhất quy trình nhánh/PR |
| KI-13 | Dữ liệu dev | Có tài khoản/dữ liệu thử trong DB local (`phase5.admin`, `phase5.target`, `an.demo`, `binh.demo`, `chi.demo`) | MINOR | Không ảnh hưởng code/môi trường khác | Chỉ dùng cho demo/dev; không commit DB | Xoá/khôi phục dữ liệu khi cần |

## Ghi chú cho buổi demo

- **Demo một máy (Mốc A):** không có BLOCKER nào ở trên; dùng 3 tài khoản demo + admin.
- **Demo hai máy:** đọc kỹ KI-02/03; dùng **cùng một đường mạng** (LAN hoặc Tailscale) và port forward nếu mở bằng IP.
- **Demo gọi qua hai mạng:** KI-01 là mục **duy nhất còn thiếu chứng minh** của GĐ 4.
- Tài khoản demo và admin: xem `docs/tasks/milestone_a_web_cb.md` + README (mật khẩu do bạn đặt trong `DemoSeed:Password`).
