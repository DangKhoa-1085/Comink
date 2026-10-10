# Comink Mobile (Flutter)

Module ứng dụng di động cho Comink, xây dựng bằng Flutter (iOS & Android).

> **Lưu ý lộ trình:** Theo quy tắc dự án và `docs/04_roadmap.md`, tính năng trên Web sẽ được hoàn thiện trước, sau đó mới triển khai sang ứng dụng di động.

---

## 1. Yêu cầu môi trường

- Flutter SDK: 3.24+ (kênh stable)
- Android Studio / Xcode
- Dart: 3.5+

---

## 2. Khởi tạo mã nguồn

Khi bắt đầu Phase Mobile, đứng tại thư mục gốc repository và thực hiện:

```bash
flutter create --org com.comink --platforms=android,ios mobile
```

---

## 3. Kiến trúc dự án đề xuất

- **State Management:** Riverpod (`flutter_riverpod`)
- **Networking:** Dio + Interceptor tự động refresh token
- **Navigation:** `go_router`
- **Lưu trữ an toàn:** `flutter_secure_storage` (cho JWT Access/Refresh tokens)
- **i18n:** `flutter_localizations` + `intl` (Việt / Anh, không hard-code văn bản hiển thị)
- **WebRTC / Calls:** `livekit_client`
- **Push Notifications:** `firebase_messaging` + `flutter_local_notifications`
