# Giai đoạn 6 — App Flutter CB

> Phân rã từ `docs/04_roadmap.md` (Giai đoạn 6) theo `AGENTS.md` mục 7.3.
> **Mục tiêu:** đưa toàn bộ mức cơ bản của Comink sang app Flutter, dùng chung backend.
> **Phụ thuộc:** Mốc A. *Trạng thái thực tế:* phần tự động của Mốc A đã xong (seed, OpenAPI Bearer, known issues, demo HTTP 12/12); **TURN hai mạng vẫn chờ người dùng test**. Người dùng đã chỉ định bắt đầu GĐ 6 — rủi ro còn lại được ghi ở mục 8.
> **Trạng thái:** 🚧 **nền tảng + auth/hồ sơ/bảng tin có ảnh/bình luận/chia sẻ + bạn bè/hồ sơ công khai + chat real-time/tạo nhóm/gửi ảnh + gọi audio/video mobile + FCM/hộp thư thông báo** (2026-10-27). Đã chạy thử trên Chrome (Flutter web) với backend + LiveKit thật; **chưa chạy trên thiết bị Android thật** — 2026-11-03: Android Studio + SDK đã cài xong, `flutter doctor` mục Android xanh và `flutter build apk --debug` thành công, nhưng vẫn chưa có điện thoại cắm vào (`adb devices` trống).
> **Người phụ trách:** Dev B chính (Flutter/push/call) + Dev A hỗ trợ backend/API; xem `docs/04_roadmap.md` mục 2.

---

## 1. Phạm vi (9 mục roadmap)

| # | Yêu cầu | Ghi chú |
|---|---|---|
| 1 | Đăng ký, đăng nhập, lưu token an toàn, tự làm mới | `flutter_secure_storage` + interceptor 401 |
| 2 | Hồ sơ, bạn bè, theo dõi, tìm kiếm người dùng | API GĐ 1–2 |
| 3 | Bảng tin, đăng bài kèm ảnh, thích, bình luận, chia sẻ | Cloudinary sign → upload trực tiếp |
| 4 | Chat riêng real-time, xem lịch sử, gửi ảnh | SignalR + `after`/`before` cursor |
| 5 | Gọi thoại/video 1–1 và gọi nhóm | LiveKit mobile + token backend |
| 6 | Quyền mic/camera/thông báo; xử lý khi bị từ chối | `permission_handler` + thông báo rõ |
| 7 | Push notification FCM (tin mới, lời mời kết bạn, cuộc gọi đến) | Cần backend M4 + Firebase |
| 8 | Đa ngôn ngữ Việt/Anh | `flutter_localizations` + `intl` |
| 9 | Chạy ổn trên thiết bị Android thật | Emulator không đủ |

**Ngoài phạm vi GĐ 6:** các tính năng NC của GĐ 7 (trạng thái online, đã đọc, reaction, ghim, tìm kiếm tin…), trừ chat nhóm/gọi nhóm đã có ở CB.

---

## 2. Quyết định kỹ thuật

| Mã | Vấn đề | Phương án | Lý do |
|---|---|---|---|
| **D1** | Kiến trúc app | Riverpod 3 + `dio` + `go_router`; chia `core/` và `features/` | Đúng `AGENTS.md` mục 8.4; không thêm state library khác |
| **D2** | Địa chỉ API | `--dart-define=API_BASE_URL=…`; mặc định `http://10.0.2.2:5117` (Android emulator) | Máy thật dùng IP LAN; không hard-code localhost |
| **D3** | Lưu token | Refresh token + user trong secure storage; **access token chỉ trong bộ nhớ**; interceptor tự refresh khi 401 một lần | Giảm rủi ro lưu access token; khôi phục phiên bằng `/api/users/me` |
| **D4** | Đa ngôn ngữ | `flutter_localizations` + lớp `AppLocalizations` dạng map (không codegen) | Ít bước build, đủ vi/en; dễ thêm chuỗi |
| **D5** | Điều hướng | `go_router` với redirect theo trạng thái phiên; shell 4 tab | Đúng yêu cầu; deep link sau này |
| **D6** | Push FCM | Làm sau cùng trong GĐ 6; cần backend M4 (`user_devices`, `notifications`), `IPushSender` thật, file cấu hình Firebase | Không có credentials Firebase trong repo; không chặn các mục khác |
| **D7** | Gọi trên máy thật | **Đã làm:** `LiveKit:PublicUrl` (fallback `Host`) trong backend; client nhận `url` từ token; máy thật/emulator đặt `ws://10.0.2.2:7880` hoặc `ws://<IP-LAN>:7880` | Tránh hard-code IP; web vẫn dùng fallback `ws://localhost:7880` |
| **D8** | Ảnh | Client xin chữ ký → upload Cloudinary trực tiếp; dev fallback local upload | Đúng thiết kế GĐ 2; giới hạn 5 MB/ảnh |

---

## 3. Công việc

### 3.1. Nền tảng (đang làm)

- [x] Cấu trúc `lib/core` + `lib/features`; xóa màn hình counter mẫu
- [x] `AppConfig` (`API_BASE_URL` bằng `--dart-define`)
- [x] `ApiClient` (dio): base URL, gắn Bearer, tự refresh 401 một lần, phát sự kiện phiên hết hạn
- [x] `ApiException` + map mã lỗi sang i18n
- [x] `TokenStorage` (secure storage) + `SessionController` (Riverpod)
- [x] `AppLocalizations` vi/en + `LocaleController`
- [x] Theme Material 3 sáng/tối + lưu lựa chọn
- [x] `go_router`: `/splash`, `/login`, `/register`, shell `/` (Feed/Bạn bè/Tin nhắn/Hồ sơ)
- [x] Android: thêm `INTERNET`, cấu hình cleartext cho dev, tên app "Comink"

### 3.2. Xác thực và hồ sơ

- [x] Màn hình đăng nhập/đăng ký (validate, lỗi từ `code`, loading)
- [x] Khôi phục phiên/đăng xuất; chặn back về màn hình auth
- [x] Hồ sơ cá nhân: xem, sửa displayName/bio/location; đổi ngôn ngữ (PATCH settings)
- [ ] Đổi mật khẩu? *(ngoài CB — GĐ 7)*

### 3.3. Mạng xã hội và bảng tin

- [x] Bảng tin: danh sách, loading/empty/error, pull-to-refresh, cuộn/tải thêm theo cursor (20 bài/trang)
- [x] Đăng bài văn bản/ảnh + chọn visibility (upload Cloudinary/local; tối đa 10 ảnh, 5 MB/ảnh)
- [x] Thích/bỏ thích, bình luận + trả lời, chia sẻ bài viết
- [x] Tìm người dùng (không dấu), kết bạn/theo dõi, trang hồ sơ công khai + bài viết
- [x] Danh sách bạn bè, lời mời nhận/gửi, chấp nhận/từ chối/hủy kết bạn
- [x] Trang cá nhân công khai + bài viết, nhắn tin nhanh nếu đã là bạn

### 3.4. Chat real-time

- [x] Danh sách hội thoại (1–1 + nhóm), sắp theo tin mới, xem trước
- [x] Khung chat: lịch sử, gửi văn bản/ảnh, `clientMessageId`; nút **Tải tin cũ hơn** (before cursor) *(chọn ảnh UI chưa tự động hoá)*
- [x] SignalR: kết nối, xác thực token, nhận `MessageCreated`, tự kết nối lại + **poll 15s khi mở chat** để bù tin bị lỡ *(chưa test mất mạng thật)*
- [x] Tạo nhóm 3–10 người (chọn từ bạn bè, đặt tên, mở chat ngay)

### 3.5. Gọi thoại/video

- [x] Backend trả URL LiveKit phù hợp thiết bị (`LiveKit:PublicUrl`, test `GetToken_UsesPublicUrl_WhenConfigured`)
- [x] Màn hình cuộc gọi đến/đi, nghe/từ chối/kết thúc; gọi 1–1 và nhóm (dùng chung hội thoại)
- [x] LiveKit mobile: kết nối room, mic/camera bật/tắt, kết thúc, đồng hồ thời lượng; video grid
- [x] Quyền mic/camera (`permission_handler`) + thông báo lỗi khi từ chối; manifest Android + Info.plist iOS

### 3.6. Push, i18n, thiết bị

- [x] Backend M4: `user_devices`, `notifications`; `IPushSender` FCM (HTTP v1, fallback bỏ qua nếu thiếu service account)
- [x] Đăng ký FCM token, nhận push tin/lời mời/cuộc gọi (code; **chưa kiểm chứng do thiếu credentials Firebase**)
  - 2026-11-11: thêm log trạng thái lúc khởi động backend — `Push FCM: BẬT (service account: …)` / `Push FCM: TẮT — …` (kèm cảnh báo khi `Firebase__ServiceAccountPath` trỏ tới file không tồn tại) để phân biệt "push tắt" với "push lỗi"; thêm test `MissingServiceAccountFile_DoesNotThrowAndSkipsHttp`.
  - 2026-11-12: **phát hiện + sửa lỗi chặn push trên máy thật** — `firebase_messaging` 17 ném `IllegalStateException: API disabled … firebase_messaging_installation_id_enabled` khi thiếu Firebase Installations ID, làm app **không xin quyền thông báo, không đăng ký token** (hộp thư vẫn báo vì là SignalR). Đã thêm meta-data `firebase_messaging_installation_id_enabled=true` vào `android/app/src/main/AndroidManifest.xml`; phải **build lại + cài lại** app (hot reload không cập nhật manifest).
  - Hướng dẫn cấu hình đầy đủ (tạo Firebase project, bật Cloud Messaging API, service account, chạy app, kiểm chứng 2 tài khoản, lỗi thường gặp): `mobile/README.md` mục 3.2.
  - ✅ **Đã chốt phương án (A) — 2026-11-12:** thêm `flutter_local_notifications` 23.0.1 (BSD-3), bật core library desugaring trong `android/app/build.gradle.kts`; khi app **foreground**, tin nhắn/lời mời hiện **cả SnackBar lẫn thông báo khay** (`lib/features/notifications/data/local_notifications.dart`), bấm thông báo điều hướng như push; `CallIncoming` bỏ qua khay vì đã mở màn hình cuộc gọi. Khi app **nền/tắt** vẫn do hệ thống hiện như trước. Có 8 unit test cho payload/ID/quyết định hiện khay.
- [x] Hộp thư thông báo trong app: badge số chưa đọc, danh sách, đánh dấu đã đọc khi mở
- [x] Xin quyền thông báo; deep link vào chat/cuộc gọi
- [x] i18n toàn bộ chuỗi hiển thị (vi/en)
- [ ] Chạy trên thiết bị Android thật (và iOS nếu có)

### 3.7. Test và tài liệu

- [x] Unit test: parser DTO, i18n, lỗi API; widget test khởi động + validate login
- [x] Widget test: bảng tin rỗng/có bài + nút tải thêm, hộp thư rỗng/có thông báo (dùng override provider)
- [x] `flutter analyze` 0 issue; `flutter test` xanh
- [x] README: chạy app, `--dart-define`, quyền, thiết bị thật *(mục 2.1 + 3.1: build APK, emulator, 4 bước bật USB debugging, LAN/firewall, LiveKit `PublicUrl`, bảng lỗi build)*
- [ ] Cập nhật `docs/04_roadmap.md` khi xong (người dùng tick)

---

## 4. Tiêu chí hoàn thành

- [x] Đăng nhập/đăng ký/lưu token/refresh hoạt động trên máy thật *(2026-11-12: đăng ký `test2.demo` trên Samsung A50, mở lại app vẫn giữ phiên)*
- [ ] Bảng tin + đăng bài + ảnh + thích/bình luận/chia sẻ
- [ ] Chat 1–1 real-time, lịch sử, ảnh *(đã thử chat văn bản trên máy thật; ảnh trong chat chưa thử)*
- [ ] Gọi 1–1 và nhóm trên điện thoại *(đã thử gọi 1–1 app ↔ web; gọi nhóm chưa thử)*
- [ ] Quyền mic/camera/thông báo xử lý đúng khi bị từ chối
- [ ] Push FCM cho tin/lời mời/cuộc gọi *(đã nhận push tin nhắn thật; lời mời/cuộc gọi chưa ghi nhận)*
- [x] i18n vi/en đầy đủ *(toàn bộ chuỗi qua i18n; đã đổi vi/en trên web)*
- [x] `flutter analyze` sạch, `flutter test` xanh *(2026-11-04: analyze 0 issue; test 11/11)*
- [x] Chạy trên ít nhất một thiết bị Android thật *(2026-11-12: Samsung SM-A505F, Android 11 — xem mục 5.4)*

---

## 5. Kiểm chứng

### 5.1. Tự động (2026-10-27)

| Hạng mục | Kết quả |
|---|---|
| `flutter analyze` | **No issues found** |
| `flutter test` | **19/19** (11 test cũ + 8 test payload/ID/quyết định hiện khay của `local_notifications_test.dart`) — 2026-11-12 |
| `flutter pub get` | OK; thêm `shared_preferences 2.5.6`, `image_picker 1.2.4`, `permission_handler` (BSD-3/MIT), `flutter_local_notifications 23.0.1` (BSD-3) |
| Backend `CallServiceTests` | **14/14**, gồm test `PublicUrl` (D7) |
| Backend `NotificationServiceTests` | **4/4** (đăng ký thiết bị, hộp thư, mark read, push) |
| Backend `FcmPushSenderTests` | **4/4** (OAuth + FCM HTTP v1, nhiều token, thiếu cấu hình không gọi mạng, `ServiceAccountPath` trỏ file không tồn tại → bỏ qua an toàn) |
| Backend push trigger | message → chỉ người nhận, friend request → tạo thông báo, call → `CallIncoming` dữ liệu |
| Backend toàn bộ | **205/205** (Domain 68, Application 25, Integration 112) — 2026-11-11 |
| OpenAPI | tái sinh **48 path**, có `/api/devices`, `/api/notifications` |

### 5.2. Tích hợp thật trên Chrome (Flutter web) + backend `localhost:5117` — đạt

Chạy: `flutter run -d web-server --web-port 5173 --dart-define=API_BASE_URL=http://localhost:5117`, sau đó thao tác qua Chrome:

- Đăng nhập `an.demo` / mật khẩu demo → vào thẳng Bảng tin; token lưu/khôi phục qua secure storage web.
- Bảng tin hiển thị 5 bài mẫu (An/Chi/Bình), visibility `Chỉ mình tôi` chỉ hiện với chính An; nút thích gọi API được.
- Tab Hồ sơ: hiện đúng số bạn bè/người theo dõi, email, vai trò; đổi ngôn ngữ Việt → Anh qua `PATCH /api/users/me/settings`, UI đổi ngay; đổi lại tiếng Việt được.
- Tab Bạn bè: danh sách 2 bạn; tìm `chi` ra đúng người, hiện trạng thái “Bạn bè” + nút theo dõi.
- Tab Tin nhắn: 2 hội thoại (nhóm “Nhóm học tập” + 1–1 với Bình); mở chat thấy đúng lịch sử 3 tin.
- Gửi tin “Xin chào từ app Flutter” → xuất hiện ngay.
- **SignalR:** dùng API đăng nhập `binh.demo` gửi tin “Tin từ Bình qua SignalR” → khung chat của An cập nhật ngay, không cần refresh.
- **Bình luận:** mở sheet bình luận của bài “Ghi chú riêng”, gửi “Bình luận thử từ app” → hiện ngay, số đếm bình luận cập nhật.
- **Hồ sơ công khai:** bấm tên Bình trong bài viết → trang hồ sơ Bình (1 bạn bè, 0 người theo dõi, 1 đang theo dõi), có nút Theo dõi/Bạn bè/Nhắn tin.
- **Tạo nhóm:** từ tab Tin nhắn, chọn Chi + Bình, đặt tên “Nhóm kiểm thử” → tạo nhóm 3 người và mở khung chat ngay.
- **Gọi audio (LiveKit thật):** Bình gọi An qua API → app An hiện “Cuộc gọi thoại đến”; bấm Nghe → kết nối room, hiện avatar + mic/kết thúc; kết thúc → DB `Completed`, thời lượng 16s. Gọi từ app (nút điện thoại trong chat) cũng tạo room và kết nối được.
- **Ảnh bài viết:** upload ảnh PNG 96×96 lên Cloudinary qua chữ ký backend, đăng ký media, tạo bài “Bài có ảnh từ API” → bảng tin hiển thị đúng ảnh (`image` trong accessibility tree).
- **FCM:** đã đăng ký endpoint thiết bị/hộp thư và nối push vào message/friend/call; **sender đã test bằng mock OAuth/FCM**, nhưng **chưa gửi push thật** vì môi trường không có service account Firebase.
- **Hộp thư:** sau khi tạo lời mời kết bạn từ tài khoản mới, chuông Bảng tin có badge; mở trang Thông báo thấy “Lời mời kết bạn”; bấm vào → đánh dấu đã đọc và chuyển tab Bạn bè, lời mời hiện để Chấp nhận/Từ chối.
- Giao diện Material 3, dark mode theo hệ thống; toàn bộ chuỗi qua i18n.

### 5.3. Chưa kiểm chứng / còn thiếu

- **Thiết bị Android thật**: đã chạy được trên Samsung SM-A505F (Android 11) ngày 2026-11-12 — xem mục 5.4; còn thiếu các kịch bản như app tắt hẳn, push lời mời/cuộc gọi, gọi nhóm, video call.
  - Đã sửa 3 lỗi chặn trên máy thật: `compileSdk = 37`, `kotlin.incremental=false` (lỗi cache Kotlin 2.4 + AGP 9.1 + Gradle 9.3 trên Windows), và thiếu meta-data `firebase_messaging_installation_id_enabled` (FCM không khởi tạo → không xin quyền, không có token).
- **Chọn ảnh từ UI Flutter** chưa tự động hoá được trên web (file picker của trình duyệt); luồng upload Cloudinary đã kiểm chứng bằng script HTTP, code `image_picker`/`MediaService` mới chỉ `analyze`. Gửi ảnh trong chat cũng chưa thử upload từ UI.
- **Video call** chưa kiểm chứng (Chrome test không có camera); **gọi nhóm** mới kiểm chứng tạo nhóm chat, chưa gọi nhóm thật.
- Phân trang **bảng tin** đã có nút/auto tải thêm và widget test render; chưa thử với >20 bài thật.
- **Phân trang lịch sử chat** đã có nút tải cũ hơn nhưng chưa thử với >50 tin; poll 15s chưa thử mất mạng thật.
- **Push FCM**: đã nhận **push tin nhắn thật** trên Samsung A50 (2026-11-12, xem 5.4); còn kiểm chứng push **lời mời kết bạn**, **cuộc gọi đến**, và trường hợp **app tắt hẳn**. Không còn thiếu credentials — service account đặt ở `backend/secrets/` (git-ignore), log backend báo `Push FCM: BẬT`.

### 5.4. Máy thật Android — Samsung SM-A505F (2026-11-12)

| Hạng mục | Giá trị |
|---|---|
| Thiết bị | Samsung **SM-A505F** (Galaxy A50), **Android 11** (API 30), debug build cài qua `flutter run` |
| Mạng | Điện thoại + máy tính cùng Wi-Fi; backend `http://192.168.1.4:5117` (`0.0.0.0`), LiveKit `ws://192.168.1.4:7880` |
| Tài khoản | `test2.demo` trên app (Android), `test1.demo` trên web — đã kết bạn |
| Token FCM | `user_devices` có `test2.demo` với `platform=Android`, `device_name=android`, `is_active=t` (2026-11-11 17:31 UTC) |
| Kết quả | ✅ **Nhận thông báo push tin nhắn từ web lên app**; đăng nhập/giữ phiên, chat văn bản, gọi 1–1 app ↔ web hoạt động; push vẫn đến sau khi **rút cáp USB** (không cần debugger) |

**Lỗi đã gặp và sửa trong đợt này** (quan trọng cho lần sau):

1. `flutter build apk` lỗi `permission_handler_android requires compileSdk 37` → đặt `compileSdk = 37` ([build.gradle.kts](../../mobile/android/app/build.gradle.kts)).
2. Build lỗi `Could not close incremental caches … is already registered` (Kotlin 2.4 + AGP 9.1 trên Windows) → `kotlin.incremental=false` ([gradle.properties](../../mobile/android/gradle.properties)).
3. **Không có token, không hiện hộp thoại quyền, không có push** → log: `IllegalStateException: API disabled … firebase_messaging_installation_id_enabled`. Sửa bằng meta-data trong `AndroidManifest.xml` ([AndroidManifest.xml](../../mobile/android/app/src/main/AndroidManifest.xml)) — phải build lại + cài lại, hot reload không đủ.
4. Android 11 **không có hộp thoại quyền thông báo** (POST_NOTIFICATIONS chỉ có từ Android 13) — nên "không thấy hỏi quyền" trên máy này là bình thường, không phải lỗi.

**Chưa thử trên máy thật**: push khi app tắt hẳn, push lời mời kết bạn/cuộc gọi, gọi nhóm, video call, chọn/gửi ảnh, đăng bài từ app. Thông báo khay khi app **đang mở** (phương án A, `flutter_local_notifications`) đã code + build được nhưng **chưa kiểm chứng lại trên máy** — cần chạy lại `flutter run` với các `--dart-define` FCM rồi nhắn tin từ web lúc app đang mở.

---

## 6. Môi trường

| Hạng mục | Giá trị |
|---|---|
| Flutter SDK | `C:\src\flutter` (3.47.7, Dart 3.13.5) — đã có trong PATH; dùng `C:\src\flutter\bin\flutter.bat` |
| Android tooling | Android Studio 2026.2.2 + Android SDK ở `C:\Users\HP\AppData\Local\Android\Sdk` (Platform 34/35/36/37.0, Build-Tools 36.0.0, NDK 28.2, Platform-Tools 37.0.1); `adb`, `emulator`, `cmdline-tools`, `flutter` đã có trong PATH; license đã chấp nhận; AVD `Pixel_7` + `Medium_Phone_API_37.0`; JDK = JBR 25 của Android Studio |
| Build Android | `flutter build apk --debug` → OK (2026-11-03, `app-debug.apk`, compileSdk 37 / targetSdk 36 / minSdk 24) |
| Backend dev | `http://localhost:5117` (Windows); máy thật cùng LAN: `http://<IP-LAN>:5117` với backend chạy `ASPNETCORE_URLS=http://0.0.0.0:5117`; emulator dùng `http://10.0.2.2:5117` |
| Thiết bị kiểm chứng | Samsung **SM-A505F** (Galaxy A50), Android 11 (API 30), serial `R58M45SH22R` — app debug cài qua `flutter run` |
| Hợp đồng API | `docs/openapi/comink.v1.json` (48 path, Bearer) |
| Tài khoản demo | `an.demo`/`binh.demo`/`chi.demo` (mật khẩu `DemoSeed:Password` của bạn) |

---

## 7. Rủi ro

- Mốc A chưa tick/TURN chưa chứng minh → app có thể phải sửa nếu API thay đổi. Đã khóa OpenAPI để giảm rủi ro.
- Điện thoại thật đã có (Samsung A50, Android 11) và app chạy được; rủi ro còn lại là các kịch bản chưa thử (app tắt hẳn, push lời mời/cuộc gọi, gọi nhóm, ảnh) và máy chỉ có Android 11 nên chưa kiểm chứng hành vi riêng của Android 13+ (hộp thoại quyền thông báo).
- FCM đã chạy thật cho tin nhắn; service account chỉ đặt cục bộ ở `backend/secrets/` (git-ignore) nên máy dev khác cần tự tạo lại theo `mobile/README.md` mục 3.2.
- LiveKit URL `localhost` không dùng được trên thiết bị thật (D7) — đã có `LiveKit:PublicUrl`, cần đặt khi test máy thật.

---

## 8. Khi có thiết bị/Firebase (checklist cho người dùng)

1. Cài **Android Studio + SDK**, thêm `adb`/`flutter` vào PATH; chấp nhận license; kiểm tra `flutter doctor` xanh phần Android.
2. Bật backend với `LiveKit__PublicUrl`:
   - emulator: `ws://10.0.2.2:7880`
   - máy thật cùng LAN: `ws://<IP-máy-backend>:7880`
3. Tạo service account Firebase, đặt backend `Firebase__ServiceAccountPath` (không commit).
4. Chạy app với:
   ```bash
   flutter run \
     --dart-define=API_BASE_URL=http://10.0.2.2:5117 \
     --dart-define=FIREBASE_PROJECT_ID=... \
     --dart-define=FIREBASE_APP_ID=... \
     --dart-define=FIREBASE_API_KEY=... \
     --dart-define=FIREBASE_MESSAGING_SENDER_ID=...
   ```
5. Kiểm chứng: đăng nhập → chat real-time/ảnh/tải cũ → gọi audio/video 1–1 + nhóm → từ chối quyền mic/camera → push tin/lời mời/cuộc gọi khi app nền/tắt → bấm notification mở đúng màn hình.
6. Sau khi đạt, tick các mục GĐ 6 trong `docs/04_roadmap.md` (người dùng tick).
