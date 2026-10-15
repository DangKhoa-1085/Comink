# Comink Mobile (Flutter)

Module ứng dụng di động cho Comink, xây dựng bằng Flutter (Android & iOS), dùng chung backend với web.

> **Lưu ý lộ trình:** theo `docs/04_roadmap.md`, tính năng trên web được hoàn thiện trước (Mốc A), sau đó mới triển khai sang app (Giai đoạn 6). Giai đoạn 0 chỉ yêu cầu **khung app chạy được "Hello"**.

---

## 1. Trạng thái hiện tại (Giai đoạn 0 — đã xong)

Khung dự án đã được tạo bằng:

```bash
flutter create --org com.comink --project-name comink_mobile --platforms=android,ios .
```

Kết quả: `android/`, `ios/`, `lib/main.dart`, `test/widget_test.dart`, `pubspec.yaml`, `analysis_options.yaml`.

---

## 2. Yêu cầu môi trường

- Flutter SDK: **kênh stable** (kiểm tra bằng `flutter --version`)
- Dart: theo bản Flutter stable đang dùng
- Android Studio (kèm Android SDK) để chạy trên Android
- Xcode + CocoaPods nếu build iOS (chỉ trên macOS)

Kiểm tra môi trường:

```bash
flutter doctor
```

---

## 3. Lệnh thường dùng

```bash
flutter pub get           # cài dependency
flutter analyze           # phân tích tĩnh (phải sạch trước khi gộp PR)
flutter test              # chạy test
flutter run               # chạy app trên thiết bị/giả lập đang kết nối
dart format lib test      # định dạng mã
```

---

## 4. Kiến trúc dự án (đã chốt trong `docs/02_tech_stack_decisions.md`, mục 2.3)

| Hạng mục | Công nghệ |
|---|---|
| Quản lý state | Riverpod (`flutter_riverpod`) |
| HTTP | dio (interceptor tự refresh token) |
| Điều hướng | go_router |
| Lưu token | `flutter_secure_storage` |
| Real-time | `signalr_netcore` |
| Gọi thoại/video | `livekit_client` |
| Đa ngôn ngữ | `flutter_localizations` + `intl` (vi, en) |
| Push | `firebase_messaging` + `flutter_local_notifications` (thêm ở Giai đoạn 6) |

Quy ước: **không hard-code chuỗi hiển thị**; mọi văn bản đi qua hệ thống đa ngôn ngữ (xem `AGENTS.md` mục 8.4).

---

## 5. Lưu ý khi nối backend

- Web dev dùng proxy của Vite tới `http://localhost:5117`. **App không có proxy**, nên phải trỏ thẳng tới địa chỉ backend.
- Giả lập Android truy cập máy host qua `10.0.2.2` (không phải `localhost`).
- Thiết bị thật cần backend truy cập được qua mạng LAN và **HTTPS** — micro/camera chỉ hoạt động trên ngữ cảnh an toàn (xem `docs/02_tech_stack_decisions.md`, mục 3.8).
- `infra/docker-compose.yml` đã bind PostgreSQL/Redis về `127.0.0.1` để bảo mật; muốn app truy cập DB trực tiếp khi debug trên máy khác thì phải nới cấu hình có ý thức.
