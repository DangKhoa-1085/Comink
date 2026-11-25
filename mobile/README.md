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

### 3.1. Cấu hình một lần — không lặp lại chuỗi `--dart-define`

Flutter hỗ trợ đọc toàn bộ `--dart-define` từ **một file JSON**, nên không cần gõ lại 5 giá trị mỗi lần chạy:

1. Chép file mẫu (chạy ở thư mục gốc repo):

   ```powershell
   Copy-Item mobile\config\dev.example.json mobile\config\dev.json
   ```

   `mobile/config/dev.json` đã được **git-ignore** (chứa cấu hình Firebase của máy bạn); file mẫu
   `dev.example.json` mới được commit.

2. Điền `API_BASE_URL` và 4 giá trị Firebase trong `config/dev.json` (cách lấy giá trị ở mục 3.2).

3. Từ `mobile/`, chạy:

   ```powershell
   .\scripts\run-dev.ps1
   ```

   Script sẽ tự: chọn thiết bị Android đang cắm, chạy `adb reverse` khi dùng `127.0.0.1`,
   kiểm tra `GET /health` và chỉ chạy `flutter run` khi backend trả lời được.

Các lệnh tương đương:

```powershell
.\scripts\run-dev.ps1 -Device R58M45SH22R   # chọn thiết bị cụ thể
.\scripts\run-dev.ps1 -CheckOnly            # chỉ kiểm tra config/thiết bị/backend
.\scripts\run-dev.ps1 -BuildApk             # build APK debug với đúng cấu hình
flutter build apk --debug --dart-define-from-file=config/dev.json
```

### 3.1.1. Chạy trên emulator

Sửa `API_BASE_URL` trong `config/dev.json` thành `http://10.0.2.2:5117` (10.0.2.2 là localhost của máy host), rồi:

```bash
flutter emulators --launch Pixel_7
flutter run -d emulator-5554 --dart-define-from-file=config/dev.json
```

### 3.1.2. Chạy trên điện thoại thật

Có **hai cách**; cách A khuyến nghị vì không phụ thuộc IP Wi-Fi hay tường lửa:

**A. Qua cáp USB (`adb reverse`) — khuyến nghị**

1. Bật **Tùy chọn nhà phát triển** + **Gỡ lỗi USB**, cắm cáp, bấm **Cho phép** trên điện thoại.
2. `adb devices` phải hiện `<serial>  device`.
3. `config/dev.json` để `"API_BASE_URL": "http://127.0.0.1:5117"`.
4. Chạy `.\scripts\run-dev.ps1` — script tự chạy `adb reverse tcp:5117 tcp:5117`.

`adb reverse` chuyển cổng 5117 của điện thoại về máy tính qua cáp USB: **không cần LAN, không cần mở
tường lửa**, và WebSocket của SignalR cũng đi qua cổng này. Lưu ý: `adb reverse` **mất khi rút cáp,
khởi động lại điện thoại hoặc ADB restart** → chạy lại script là xong.

**B. Qua LAN (Wi-Fi)**

1. Backend phải lắng nghe `0.0.0.0:5117`, không chỉ `localhost`. Profile `http` trong
   `backend/src/Comink.Api/Properties/launchSettings.json` đã đặt `http://0.0.0.0:5117`, nên
   `dotnet run --project backend/src/Comink.Api` là đủ.
   Nếu chạy bằng `--no-launch-profile`, đặt `ASPNETCORE_URLS=http://0.0.0.0:5117` hoặc truyền `--urls`.
2. Điện thoại và máy tính **cùng một Wi-Fi/LAN**. Lấy IP máy tính bằng `ipconfig`
   (ví dụ `192.168.1.37`), rồi đặt `"API_BASE_URL": "http://192.168.1.37:5117"` trong `config/dev.json`.
3. Kiểm tra từ **trình duyệt trên điện thoại**: mở `http://192.168.1.37:5117/health` → phải thấy
   JSON `{"status":"healthy",...}`. Nếu không mở được thì app cũng không kết nối được.
4. Nếu trình duyệt điện thoại không mở được: thêm rule tường lửa (PowerShell **Run as administrator**):

   ```powershell
   New-NetFirewallRule -DisplayName "Comink API 5117" -Direction Inbound -Protocol TCP -LocalPort 5117 -Action Allow
   ```

   Hoặc đơn giản hơn: dùng cách A (`adb reverse`).

**Không cần đổi cổng backend**: backend luôn dùng **5117**. Khi IP máy tính thay đổi (đổi Wi-Fi/router),
chỉ cần cập nhật **host** trong `API_BASE_URL`, không đổi cổng.

**Chẩn đoán nhanh khi app báo "Không kết nối được tới máy chủ":**

| Kiểm tra | Kết quả mong đợi |
|---|---|
| `Get-NetTCPConnection -State Listen -LocalPort 5117` trên Windows | `LocalAddress` = `0.0.0.0` (LAN) hoặc chỉ `127.0.0.1` (USB reverse) |
| Mở `http://<URL đã cấu hình>/health` từ **trình duyệt điện thoại** | JSON `healthy` |
| `adb reverse --list` (khi dùng USB) | có dòng `tcp:5117 tcp:5117` |
| Điện thoại và máy tính | cùng subnet `192.168.1.x`; tắt Cloudflare WARP nếu nó chặn LAN |
| `config/dev.json` | `API_BASE_URL` đúng host, **không có dấu `/` ở cuối** |

Lưu ý cho máy thật:

- Gọi thoại/video cần LiveKit bind đúng IP LAN (xem các mục dưới) — kết nối API thành công chưa đủ để gọi được.
- Nếu gọi báo **"Không kết nối được phòng gọi"** (nhất là sau khi đổi Wi-Fi hoặc router cấp IP mới): chạy lại
  `powershell -ExecutionPolicy Bypass -File infra/set-lan-ip.ps1` (cập nhật `rtc.node_ip` + `turn.domain` rồi restart LiveKit),
  sau đó khởi động lại backend với `LiveKit__PublicUrl=ws://<ip-mới>:7880` và chạy lại app với `API_BASE_URL` mới — xem KI-03.
- Quyền mic/camera/thông báo do app xin lúc dùng; nếu người dùng từ chối, app hiện thông báo lỗi thay vì im lặng.
- App bật `usesCleartextTraffic` để gọi HTTP khi dev — **phải bỏ** khi chuyển sang HTTPS production.

**Lỗi build đã biết (đã xử lý trong repo):**

| Lỗi | Nguyên nhân | Cách xử lý trong repo |
|---|---|---|
| `permission_handler_android requires ... Android SDK version 37` | `permission_handler` 13 yêu cầu `compileSdk 37`, Flutter 3.47 mặc định 36 | `android/app/build.gradle.kts`: `compileSdk = 37` |
| `Could not close incremental caches ... is already registered` | Kotlin 2.4 + AGP 9.1 + Gradle 9.3 trên Windows lỗi cache biên dịch tăng dần của các module plugin | `android/gradle.properties`: `kotlin.incremental=false` (build chậm hơn một chút) |

### 3.2. Bật push FCM (Giai đoạn 6)

Code push đã có ở cả hai phía; phần dưới cần **tài khoản Google/Firebase của bạn** (repo không chứa credentials — xem KI-06). Làm một lần, sau đó mọi máy dev chỉ cần chép `.env`.

**Bước 1 — Tạo project + Android app trên Firebase**

1. [console.firebase.google.com](https://console.firebase.google.com) → **Add project** (hoặc dùng project có sẵn).
2. Project settings → General → *Your apps* → **Add app → Android**.
   - **Package name phải khớp** `applicationId` = `com.comink.comink_mobile` (khai báo ở `android/app/build.gradle.kts`).
   - **Không cần** tải `google-services.json` và không cần plugin Gradle `google-services`: app tự khởi tạo `FirebaseOptions` từ `--dart-define` (xem `lib/core/config/app_config.dart`).
3. Ghi lại 4 giá trị ở Project settings → General: **Project ID**, **App ID** (`1:…:android:…`), **API key**, **Messaging sender ID** (và Storage bucket nếu dùng).

**Bước 2 — Bật Cloud Messaging API**

Firebase Console → *Messaging* → *Send your first message* (chỉ để mở khoá API), hoặc Google Cloud Console → APIs & Services → **Firebase Cloud Messaging API** → Enable. Thiếu bước này backend sẽ nhận `403`.

**Bước 3 — Service account cho backend**

Project settings → **Service accounts** → **Generate new private key** → lưu file JSON vào:

```
D:\DACN\Comink\backend\secrets\fcm-service-account.json
```

Thư mục `backend/secrets/` đã được `.gitignore` bỏ qua toàn bộ. **Không commit, không dán file này vào chat.**

Thêm vào `.env` ở gốc repo (file `.env` không được commit):

```ini
Firebase__ServiceAccountPath=D:\DACN\Comink\backend\secrets\fcm-service-account.json
```

Dùng **đường dẫn tuyệt đối** vì đường dẫn tương đối được tính theo thư mục đang chạy lệnh.

**Bước 4 — Kiểm tra backend đã bật push**

Chạy backend, log khởi động phải có một trong hai dòng:

```
Push FCM: BẬT (service account: D:\DACN\Comink\backend\secrets\fcm-service-account.json)
Push FCM: TẮT — thiếu Firebase__ServiceAccountPath/Firebase__ServiceAccountJson; ...
```

Nếu báo `TẮT — không tìm thấy file service account tại …` thì sai đường dẫn; nếu báo `TẮT — thiếu …` thì chưa nạp `.env`.

**Bước 5 — Chạy app với cấu hình công khai**

Các giá trị này **không phải secret** (được nhúng vào app) và nằm trong `config/dev.json` (mục 3.1).
Sau khi điền, chạy:

```powershell
.\scripts\run-dev.ps1
# hoặc chỉ build APK:
.\scripts\run-dev.ps1 -BuildApk
```

Nếu muốn truyền trực tiếp không qua file (không khuyến nghị vì phải gõ lại mỗi lần):

```bash
flutter run \
  --dart-define=API_BASE_URL=http://<ip-lan>:5117 \
  --dart-define=FIREBASE_PROJECT_ID=<project-id> \
  --dart-define=FIREBASE_APP_ID=<app-id> \
  --dart-define=FIREBASE_API_KEY=<api-key> \
  --dart-define=FIREBASE_MESSAGING_SENDER_ID=<sender-id>
```

Đăng nhập trong app → app xin quyền thông báo và đăng ký token với backend (`POST /api/devices`); token lưu ở bảng `user_devices`.

**Bước 6 — Kiểm chứng (cần 2 tài khoản)**

1. Tài khoản A đăng nhập trên điện thoại, tài khoản B nhắn tin cho A.
2. Đưa app A xuống nền → phải có thông báo; **tắt hẳn app** → vẫn phải có thông báo.
3. Khi app A đang mở (foreground), tin nhắn vừa hiện **SnackBar** trong app vừa có **thông báo trên khay** (qua `flutter_local_notifications`); cuộc gọi đến thì mở thẳng màn hình cuộc gọi, không thêm thông báo khay.
4. Lặp lại với lời mời kết bạn và cuộc gọi đến; bấm thông báo phải mở đúng chat / hộp thư / màn hình cuộc gọi.

**Lỗi thường gặp**

| Hiện tượng | Nguyên nhân thường gặp |
|---|---|
| Backend log `FCM trả 401/403` | Chưa bật Cloud Messaging API, hoặc service account không thuộc đúng project |
| `FCM trả 404` kèm `UNREGISTERED` | Token cũ/bị thu hồi — mở lại app để đăng ký token mới |
| Không có log push nào | Thiếu `Firebase__ServiceAccountPath` trong `.env` (xem bước 4) |
| Có thông báo nhưng bấm không mở đúng màn hình | Payload `data` thiếu `type`/`conversationId` — đối chiếu `docs/05_api_contract.md` mục 10 |
| Không hiện hộp thoại xin quyền thông báo **và** không có token trong `user_devices` | `firebase_messaging` 17 ném `IllegalStateException: API disabled ... firebase_messaging_installation_id_enabled` (thiếu meta-data Firebase Installations ID) → app bỏ qua toàn bộ push. Log thấy dòng `flutter : Không khởi tạo được FCM: …`. **Đã sửa** bằng meta-data trong `android/app/src/main/AndroidManifest.xml`; nhớ **build lại và cài lại** app (hot reload không cập nhật manifest) |
| iOS không nhận | Cần APNs key trong Firebase + quyền thông báo; ngoài phạm vi GĐ 6 |

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
| Push | `firebase_messaging` (FCM) + `flutter_local_notifications` (hiện thông báo khay khi app đang mở) |

Quy ước: **không hard-code chuỗi hiển thị**; mọi văn bản đi qua hệ thống đa ngôn ngữ (xem `AGENTS.md` mục 8.4).

---

## 5. Lưu ý khi nối backend

- Web dev dùng proxy của Vite tới `http://localhost:5117`. **App không có proxy**, nên phải trỏ thẳng tới địa chỉ backend.
- Giả lập Android truy cập máy host qua `10.0.2.2` (không phải `localhost`).
- Thiết bị thật cần backend truy cập được qua mạng LAN và **HTTPS** — micro/camera chỉ hoạt động trên ngữ cảnh an toàn (xem `docs/02_tech_stack_decisions.md`, mục 3.8).
- `infra/docker-compose.yml` đã bind PostgreSQL/Redis về `127.0.0.1` để bảo mật; muốn app truy cập DB trực tiếp khi debug trên máy khác thì phải nới cấu hình có ý thức.
