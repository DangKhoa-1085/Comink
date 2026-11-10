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

### 2.1. Android trên máy dev hiện tại

| Hạng mục | Giá trị |
|---|---|
| Android Studio | 2026.2.2 — `C:\Program Files\Android\Android Studio` |
| Android SDK | `C:\Users\HP\AppData\Local\Android\Sdk` — Platforms 34/35/36/37.0, Build-Tools 36.0.0, NDK 28.2.13676358, Platform-Tools 37.0.1 |
| JDK | JDK kèm Android Studio (JBR 25) — `flutter doctor` tự dùng, không cần cài JDK riêng |
| Emulator / AVD | Pixel_7 (Android 16, google_apis) và Medium_Phone_API_37.0 |
| PATH | `C:\src\flutter\bin`, `…\Android\Sdk\platform-tools`, `…\Android\Sdk\emulator`, `…\cmdline-tools\latest\bin` |
| License | đã chấp nhận — mục "Android toolchain" của `flutter doctor` phải xanh |

Kiểm tra nhanh: `flutter doctor -v` (mục Android toolchain `[√]`) và `adb devices`.

---

## 3. Lệnh thường dùng

```bash
flutter pub get           # cài dependency
flutter analyze           # phân tích tĩnh (phải sạch trước khi gộp PR)
flutter test              # chạy test
flutter run               # chạy app trên thiết bị/giả lập đang kết nối
dart format lib test      # định dạng mã
```

### 3.1. Build và chạy trên Android

**Build APK debug (không cần thiết bị cắm vào):**

```bash
flutter build apk --debug
# → build/app/outputs/flutter-apk/app-debug.apk
```

**Chạy trên emulator** (backend chạy trên máy host):

```bash
flutter emulators --launch Pixel_7
flutter run --dart-define=API_BASE_URL=http://10.0.2.2:5117
```

**Chạy trên điện thoại thật** (backend phải vào được qua LAN):

1. Trên điện thoại: `Cài đặt → Giới thiệu điện thoại → Nhấn "Số bản dựng" 7 lần` → bật **Tùy chọn nhà phát triển** → bật **Gỡ lỗi USB (USB debugging)**.
2. Cắm cáp USB, trên điện thoại chọn chế độ **Truyền tệp** và bấm **Cho phép** ở hộp thoại "Cho phép gỡ lỗi USB".
3. Kiểm tra: `adb devices` phải hiện `<serial>  device` (nếu là `unauthorized` thì bấm lại "Cho phép" trên điện thoại).
4. Lấy IP LAN của máy tính (`ipconfig`), rồi chạy:

```bash
flutter run --dart-define=API_BASE_URL=http://<ip-lan-máy-tính>:5117
```

Lưu ý cho máy thật:

- Điện thoại và máy tính phải cùng mạng LAN; tường lửa Windows phải cho phép cổng **5117** (backend) và **7880** (LiveKit).
- Backend phải lắng nghe trên `0.0.0.0`, không chỉ `localhost` (xem `backend/src/Comink.Api/Properties/launchSettings.json` hoặc biến `ASPNETCORE_URLS`).
- Gọi thoại/video cần `LiveKit__PublicUrl=ws://<ip-lan-máy-tính>:7880` khi khởi động backend, nếu không app sẽ nhận `ws://localhost:7880` và không kết nối được.
- Quyền mic/camera/thông báo do app xin lúc dùng; nếu người dùng từ chối, app hiện thông báo lỗi thay vì im lặng.
- App bật `usesCleartextTraffic` để gọi HTTP khi dev — **phải bỏ** khi chuyển sang HTTPS production.

**Lỗi build đã biết (đã xử lý trong repo):**

| Lỗi | Nguyên nhân | Cách xử lý trong repo |
|---|---|---|
| `permission_handler_android requires ... Android SDK version 37` | `permission_handler` 13 yêu cầu `compileSdk 37`, Flutter 3.47 mặc định 36 | `android/app/build.gradle.kts`: `compileSdk = 37` |
| `Could not close incremental caches ... is already registered` | Kotlin 2.4 + AGP 9.1 + Gradle 9.3 trên Windows lỗi cache biên dịch tăng dần của các module plugin | `android/gradle.properties`: `kotlin.incremental=false` (build chậm hơn một chút) |

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
