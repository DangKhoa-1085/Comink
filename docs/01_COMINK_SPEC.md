# Comink — Đặc tả dự án

> Đồ án chuyên ngành phần mềm: **Xây dựng web và app mạng xã hội thu nhỏ hỗ trợ gọi thoại và nhắn tin thời gian thực (real-time chat)**.

| Hạng mục | Nội dung |
|---|---|
| Tên dự án | Comink |
| Thời gian thực hiện | 3 tháng |
| Nhân sự | 2 lập trình viên full-stack, chia việc theo tính năng |
| Nền tảng | Web (demo tính năng trước) → App mobile Flutter (bắt buộc) |
| Quy mô demo | Vài chục người dùng; sau khi ổn định toàn bộ chức năng hướng tới vài trăm |

---

## 1. Ý tưởng dự án

Comink là mạng xã hội thu nhỏ dành cho **sinh viên và các cộng đồng nhỏ**, trong đó **nhắn tin thời gian thực và gọi thoại/video là trọng tâm**, còn bảng tin, bài viết và tương tác là lớp kết nối xã hội xung quanh.

Người dùng có thể tạo hồ sơ, kết bạn hoặc theo dõi nhau, đăng bài, tương tác, nhắn tin riêng hoặc theo nhóm và gọi trực tuyến ngay trong cùng một ứng dụng, thay vì phải chuyển qua lại giữa nhiều dịch vụ riêng lẻ.

### Vấn đề cần giải quyết
- Sinh viên và nhóm bạn thường dùng nhiều ứng dụng rời rạc cho đăng bài, nhắn tin và gọi.
- Cộng đồng nhỏ cần một không gian riêng, dễ kiểm soát, có quản trị viên và công cụ kiểm duyệt.

### Đối tượng người dùng
- **Chính:** sinh viên và nhóm bạn.
- **Mở rộng:** các cộng đồng nhỏ cùng sở thích hoặc cùng tổ chức.
- **Quản trị viên:** người quản lý tài khoản, nội dung và báo cáo vi phạm.

### Phạm vi nền tảng
- **Web (React + TypeScript + Vite):** xây trước ở mức cơ bản để demo tính năng.
- **Mobile (Flutter):** triển khai sau khi web đã chạy ổn, là sản phẩm bắt buộc của đồ án.
- **Backend dùng chung:** ASP.NET Core + PostgreSQL, phục vụ cả web và app.

---

## 2. Tầm nhìn dự án

### Tầm nhìn
Trở thành một nền tảng kết nối cộng đồng nhỏ gọn, nhanh và an toàn, nơi nhắn tin và gọi trực tuyến mượt mà là trải nghiệm cốt lõi.

### Mục tiêu
1. **Trong thời gian đồ án (3 tháng):** hoàn thành đầy đủ mức cơ bản của tất cả nhóm tính năng, chạy ổn định trên máy cá nhân để demo và bảo vệ, đồng thời có app mobile.
2. **Sau khi các chức năng chính ổn định:** bổ sung dần mức nâng cao, tích hợp AI, mở rộng lên vài trăm người dùng.
3. **Dài hạn:** phát triển thành sản phẩm thực tế, có thể triển khai lên Internet cho cộng đồng sử dụng.

### Nguyên tắc phát triển
- **Cơ bản trước, nâng cao sau:** mức cơ bản của nhóm tính năng trước hoàn thành thì mới chuyển sang mức cơ bản của nhóm kế tiếp.
- **Mức nâng cao linh hoạt:** có thể làm bất cứ lúc nào, chỉ bị chặn bởi mức cơ bản của chính nhóm tính năng đó.
- **Web trước, app sau:** web cơ bản dùng để kiểm chứng tính năng, sau đó mới triển khai sang Flutter.
- **AI làm sau cùng:** chỉ tích hợp khi các chức năng chính đã hoạt động ổn định.
- **Chạy cục bộ ổn định rồi mới mở rộng:** demo trên máy cá nhân trước, sau đó mới truy cập từ thiết bị khác trong cùng mạng, rồi đến triển khai Internet.

---

## 3. Các tính năng cốt lõi

Quy ước: **[CB]** là mức cơ bản, **[NC]** là mức nâng cao.

### 3.1. Mạng xã hội
**[CB]**
- Tài khoản và hồ sơ cá nhân
- Kết bạn (hai chiều) và theo dõi (một chiều)
- Tìm kiếm người dùng
- Đăng bài viết kèm ảnh, bảng tin
- Thích, bình luận, chia sẻ bài viết

**[NC]**
- Hashtag
- Lưu bài viết
- Gợi ý bạn bè

### 3.2. Nhắn tin real-time (SignalR)
**[CB]**
- Chat riêng 1–1
- Lưu tin nhắn
- Xem lịch sử trò chuyện

**[NC]**
- Chat nhóm
- Trạng thái online (presence)
- Hiển thị "đang nhập"
- Trạng thái đã gửi / đã đọc
- Phản hồi (reply) tin nhắn
- Thu hồi và chỉnh sửa tin nhắn
- Tìm kiếm tin nhắn
- Ghim tin nhắn
- Chuyển tiếp tin nhắn
- Thả cảm xúc vào tin nhắn
- Quản lý thông báo theo từng cuộc trò chuyện

### 3.3. Gọi trực tuyến
Sử dụng dịch vụ bên thứ ba (LiveKit, Agora hoặc Twilio) để xử lý media, kèm TURN server để đảm bảo kết nối qua NAT/firewall. Signaling và trạng thái cuộc gọi đi qua backend của Comink.

**[CB]**
- Gọi thoại và video 1–1
- Gọi nhóm (thoại và video)

**[NC]**
- Trạng thái cuộc gọi đến, chấp nhận / từ chối
- Bật / tắt mic (và camera)
- Hiển thị thời lượng cuộc gọi
- Cuộc gọi nhỡ
- Lịch sử cuộc gọi

### 3.4. Hình ảnh và tệp
Lưu trữ trên dịch vụ cloud (Cloudinary, Firebase hoặc tương đương).

**[CB]**
- Gửi ảnh trong bài viết và tin nhắn

**[NC]**
- Gửi ảnh, video, tệp tài liệu
- Xem trước nội dung và tải xuống
- Album ảnh
- Giới hạn dung lượng tải lên
- Quản lý tệp đã gửi

### 3.5. Quản trị viên
**[CB]**
- Quản lý tài khoản người dùng
- Khóa / mở khóa người dùng

**[NC]**
- Dashboard tổng quan
- Quản lý bài viết và bình luận
- Báo cáo vi phạm và xử lý nội dung
- Nhật ký quản trị (audit log)
- Thống kê hoạt động
- Công cụ kiểm duyệt nâng cao

### 3.6. Bảo mật và quyền riêng tư
**[CB]**
- Đăng nhập (xác thực)
- Phân quyền (người dùng / quản trị viên)
- Kiểm tra dữ liệu đầu vào

**[NC]**
- Xác minh email
- Đặt lại mật khẩu
- Chặn người dùng
- Cài đặt quyền riêng tư
- Kiểm tra quyền truy cập từng hội thoại
- Giới hạn tần suất (rate limiting)
- Quản lý phiên đăng nhập
- Chống spam
- Các biện pháp bảo vệ nâng cao

### 3.7. Tính năng AI
Chỉ triển khai sau khi các chức năng chính hoạt động ổn định, sử dụng API bên ngoài (Claude, OpenAI, Gemini...).
- Chatbot
- Hỗ trợ viết bài
- Tóm tắt tin nhắn

### 3.8. Mục tiêu triển khai
**[CB]**
- Chạy trên máy cá nhân, phục vụ demo và bảo vệ đồ án
- Container hóa bằng Docker / Docker Compose

**[NC]**
- Truy cập từ thiết bị khác trong cùng mạng (demo web và app trên nhiều thiết bị)
- Triển khai lên Internet, hỗ trợ nhiều người dùng đồng thời
- Bảo mật và cấu hình môi trường production
- CI/CD

### 3.9. Tính năng xuyên suốt
- **Thông báo đẩy (push notification)** cho web và mobile, cùng thông báo trong ứng dụng
- **Đa ngôn ngữ** giao diện Việt / Anh

---

## 4. Công nghệ và kiến trúc

| Thành phần | Lựa chọn |
|---|---|
| Backend | ASP.NET Core, monolith theo Clean Architecture |
| Real-time | SignalR |
| Cơ sở dữ liệu | PostgreSQL |
| Cache, presence, scale SignalR | Redis |
| Web | React + TypeScript + Vite |
| Mobile | Flutter |
| Gọi thoại / video | LiveKit / Agora / Twilio (chọn một) + TURN server |
| Lưu trữ tệp | Cloudinary / Firebase (chọn một) |
| AI | API ngoài (Claude / OpenAI / Gemini) |
| Đóng gói và tự động hóa | Docker / Docker Compose, CI/CD |

---

## 5. Lộ trình đề xuất (3 tháng)

> Đây là đề xuất để nhóm điều chỉnh, không phải ràng buộc.

| Giai đoạn | Nội dung chính |
|---|---|
| Tuần 1–2 | Khung dự án, Clean Architecture, Docker Compose, CI cơ bản, tài khoản và đăng nhập (3.6 CB), hồ sơ |
| Tuần 3–5 | Mạng xã hội CB (3.1), nhắn tin CB (3.2), ảnh CB (3.4), quản trị CB (3.5) trên web |
| Tuần 6–7 | Gọi thoại / video CB (3.3), hoàn thiện web cơ bản để demo |
| Tuần 8–10 | Triển khai app Flutter cho các tính năng cơ bản; push notification, đa ngôn ngữ |
| Tuần 11–12 | Chọn mức nâng cao theo mức ưu tiên (nhắn tin, bảo mật, gọi), tích hợp AI nếu hệ thống đã ổn định, kiểm thử, viết báo cáo, chuẩn bị demo |

Các mức nâng cao có thể xen kẽ vào bất kỳ giai đoạn nào miễn là mức cơ bản của nhóm đó đã xong. Việc chia công việc cho hai thành viên theo nhóm tính năng (ví dụ một người phụ trách mạng xã hội và quản trị, người còn lại phụ trách nhắn tin và gọi) nên được chốt riêng.

---

## 6. Ngoài phạm vi (giai đoạn đồ án)

- Tự xây hạ tầng media cho gọi thoại / video (dùng dịch vụ bên thứ ba)
- Tự huấn luyện hoặc chạy mô hình AI cục bộ
- Chia nhỏ backend thành microservices
- Triển khai Internet quy mô lớn (vài nghìn người dùng trở lên)
- Phát hành app lên App Store / Google Play (trừ khi nhóm quyết định làm thêm)

---

## 7. Tiêu chí hoàn thành

- **Tối thiểu để bảo vệ:** toàn bộ mức cơ bản của 3.1–3.6 và 3.8 hoạt động ổn định trên web và app, demo được với vài chục người dùng trên máy cá nhân.
- **Tốt:** có nhiều tính năng nâng cao ở nhắn tin, bảo mật và gọi; truy cập được từ nhiều thiết bị trong cùng mạng.
- **Xuất sắc:** tích hợp AI, hoàn thiện CI/CD, triển khai được lên Internet với cấu hình production, chịu tải vài trăm người dùng.

---

## 8. Các quyết định đã chốt (Hoàn thành Giai đoạn 0)

Toàn bộ các điểm mở đã được thống nhất và đưa vào thực thi:
- **Nhà cung cấp gọi thoại / video:** LiveKit (self-hosted dev với TURN tích hợp; trừu tượng hóa qua `ICallProvider` cho phép switch sang Cloud/Agora sau này).
- **Lưu trữ tệp & media:** Cloudinary (upload qua signed URL do backend cấp; trừu tượng hóa qua `IMediaStorage`).
- **Dịch vụ gửi email:** Mailpit khi local dev (localhost:1025); Resend qua SMTP/API cho production.
- **Push notification:** Firebase Cloud Messaging (FCM) cho cả Web và Mobile.
- **Nhà cung cấp AI:** Sẵn sàng switch LLM trả phí trong tương lai; trừu tượng hóa qua `IAiProvider`.
