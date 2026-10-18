# Comink — Lộ trình phát triển (Roadmap)

> Tài liệu 04, dựa trên `COMINK_SPEC.md` và các tài liệu 02, 03. Chia dự án thành các giai đoạn lớn, mỗi giai đoạn có **checklist tiêu chí hoàn thiện**: tick hết mới coi là xong và được chuyển sang giai đoạn kế tiếp.
>
> Thời gian: 3 tháng (~12 tuần), 2 lập trình viên full-stack. Mốc tuần chỉ là **gợi ý**, không phải cam kết cứng.

---

## 1. Quy tắc lộ trình

1. **Cơ bản trước, nâng cao sau:** xong mức cơ bản (CB) của nhóm tính năng hiện tại mới sang CB của nhóm kế tiếp.
2. **Nâng cao (NC) làm bất cứ lúc nào**, miễn là CB của chính nhóm đó đã xong (xem giai đoạn 7).
3. **Web trước, app sau:** web CB dùng để demo tính năng; app Flutter bắt đầu sau khi web CB ổn định.
4. **AI làm sau cùng,** khi các chức năng chính đã ổn định.
5. **Ngoại lệ có chủ đích:** đăng nhập và phân quyền (thuộc nhóm 6 Bảo mật CB) phải xong **trước** mạng xã hội, vì mọi tính năng khác cần tài khoản.
6. **Chạy demo trên máy cá nhân ổn định** là điều kiện trước khi truy cập từ thiết bị khác trong mạng hoặc triển khai Internet.

---

## 2. Tổng quan các giai đoạn

| GĐ | Tên | Nội dung chính | Tuần (gợi ý) |
|---|---|---|---|
| 0 | Khởi tạo | Repo, Docker Compose, CI, khung kiến trúc, chốt công nghệ còn bỏ ngỏ | 1 |
| 1 | Nền tảng và Tài khoản | Đăng nhập, phân quyền, hồ sơ, đa ngôn ngữ (Bảo mật CB) | 1–2 |
| 2 | Mạng xã hội CB + Ảnh trong bài viết | Bạn bè, theo dõi, bài viết, tương tác, tìm kiếm | 3–4 |
| 3 | Nhắn tin CB + Ảnh trong tin nhắn | Chat riêng, lưu và xem lịch sử, real-time | 5–6 |
| 4 | Gọi trực tuyến CB | Gọi thoại/video 1–1 và gọi nhóm | 7 |
| 5 | Quản trị viên CB | Quản lý và khóa/mở khóa tài khoản | 7 |
| ◆ | **Mốc A: Web CB hoàn chỉnh** | Demo được toàn bộ mức cơ bản trên web | cuối tuần 7 |
| 6 | App Flutter CB | Đưa toàn bộ mức cơ bản sang app, thêm push | 8–10 |
| ◆ | **Mốc B: Web + App CB** | Demo đủ web và app trên máy cá nhân | cuối tuần 10 |
| 7 | Nâng cao (chọn theo ưu tiên) | Các tính năng NC, AI, triển khai NC | 10–12 (có thể xen kẽ sớm hơn) |
| 8 | Hoàn thiện và Bảo vệ | Kiểm thử, sửa lỗi, tài liệu, chuẩn bị demo | 12 |

> Giai đoạn 4 (gọi nhóm có cả video + TURN) là phần **rủi ro kỹ thuật cao nhất**; nếu trễ, giai đoạn 5 và các việc nhỏ khác được xử lý song song để giữ Mốc A.

### Gợi ý chia việc (đề xuất, hai bạn thống nhất lại)

| Người | Phụ trách |
|---|---|
| Dev A | Tài khoản/bảo mật, mạng xã hội, ảnh/tệp, quản trị |
| Dev B | Nhắn tin real-time, gọi trực tuyến, thông báo/push |
| Chung | Khởi tạo, CI/CD, triển khai, tích hợp, kiểm thử, báo cáo |

---

## 3. Chi tiết từng giai đoạn

Mỗi giai đoạn gồm: **mục tiêu**, **phụ thuộc** và **checklist hoàn thiện**.

### Giai đoạn 0: Khởi tạo

**Mục tiêu:** hai người cùng chạy được dự án rỗng trên máy mình; mọi quyết định công nghệ đã chốt.
**Phụ thuộc:** không.

- [x] Repo monorepo có `README` hướng dẫn chạy từ đầu
- [x] `docker-compose` chạy được PostgreSQL, Redis, Mailpit, LiveKit (chế độ dev)
- [x] Khung backend theo Clean Architecture (Domain, Application, Infrastructure, Api) build được
- [x] Khung web (Vite + React + TS) và khung app (Flutter) chạy được "Hello"
- [x] CI chạy build + test + lint cho từng thành phần trên mỗi Pull Request
- [x] Quy tắc nhánh Git, commit, review, migration đã thống nhất
- [x] Đã chốt các mục ❓ còn lại ở `02_tech_stack_decisions.md` (nhà cung cấp gọi, nơi lưu tệp, LLM mặc định, thư viện UI)
- [x] Đã rà soát và chốt `03_database_design.md`; có migration M0 chạy thành công

> **Trạng thái (cập nhật 2026-10-15):** cả 8 mục đã được kiểm chứng bằng cách chạy thật trên máy phát triển: backend build 7/7 project, `flutter analyze` sạch, `npm run build` thành công, `docker compose` chạy 4/4 service, migration M0 đã áp lên PostgreSQL 17 và seed role/tài khoản admin hoạt động, hook chặn bí mật đã bật. Chi tiết và các mục chuyển tiếp ở `docs/tasks/phase0_remediation.md`.
> *Lưu ý:* mục CI mới chỉ được xác nhận là **đã có workflow** (`build + test + lint`); chưa có lần chạy thật trên GitHub Actions vì chưa mở Pull Request nào sau khi Phase 0 hoàn tất.

### Giai đoạn 1: Nền tảng và Tài khoản (Bảo mật CB)

**Mục tiêu:** người dùng đăng ký, đăng nhập và có hồ sơ; hệ thống phân quyền hoạt động.
**Phụ thuộc:** GĐ 0.

- [x] Đăng ký, đăng nhập, đăng xuất, làm mới token hoạt động trên web
- [x] Phân quyền hai vai trò `User` và `Admin`; endpoint chỉ-admin bị chặn với người thường
- [x] Kiểm tra dữ liệu đầu vào ở backend cho mọi endpoint hiện có (trả mã lỗi thống nhất)
- [x] Hồ sơ cá nhân: xem và sửa tên hiển thị, tiểu sử
- [x] Có tài khoản admin đầu tiên (seed từ cấu hình `AdminSeed`)
- [x] Khung đa ngôn ngữ Việt/Anh chạy trên web (mọi chuỗi hiển thị đi qua i18n)
- [x] SignalR kết nối được và xác thực bằng token (chưa cần nghiệp vụ)
- [x] Có test cho đăng nhập và phân quyền; bí mật/khóa API không nằm trong Git

> **Trạng thái (cập nhật 2026-10-17):** cả 8 mục đã đạt. Backend: 29 test pass (unit + tích hợp trên PostgreSQL thật). Web: `lint` + `build` sạch, 39 kiểm tra end-to-end qua proxy của Vite, 5 kiểm tra SignalR, 27 kiểm tra render (SSR).
> **Đã kiểm chứng trên trình duyệt thật** (Chrome qua BrowserSkill, hạ tầng Docker + backend + Vite): đăng ký → đăng nhập → hồ sơ → khôi phục phiên → đa ngôn ngữ → đăng xuất → route bảo vệ, và phiên `Admin` với `/admin/users` (danh sách, tìm kiếm, trạng thái rỗng). Hai lỗi nhỏ phát hiện trong đợt này (lỗi framework thiếu `code`, số nhiều tiếng Anh) đã sửa và kiểm chứng lại.
> Chi tiết ở `docs/tasks/phase1_foundation_auth.md`. Hợp đồng API: `docs/05_api_contract.md`.

### Giai đoạn 2: Mạng xã hội CB + Ảnh trong bài viết

**Mục tiêu:** có một mạng xã hội dùng được: kết nối bạn bè, đăng bài, tương tác.
**Phụ thuộc:** GĐ 1.

- [x] Kết bạn hai chiều: gửi, chấp nhận, từ chối lời mời; xem danh sách bạn
- [x] Theo dõi / bỏ theo dõi (một chiều)
- [x] Tìm kiếm người dùng (có hỗ trợ gõ không dấu)
- [x] Đăng, sửa, xóa bài viết; chọn phạm vi hiển thị (công khai/bạn bè/chỉ mình tôi)
- [x] Đính kèm ảnh vào bài viết (tải lên cloud, hiển thị đúng)
- [x] Bảng tin hiển thị bài của bạn bè và người theo dõi, phân trang cuộn
- [x] Thích, bình luận (có trả lời) và chia sẻ bài viết
- [x] Trang cá nhân hiển thị bài viết, số bạn bè/người theo dõi
- [x] Quyền xem bài viết đúng (không xem được bài `Friends` của người lạ)
- [x] Có test cho quyền xem bài và trạng thái kết bạn

> **Trạng thái (cập nhật 2026-10-17):** cả 10 mục đã **cài đặt và kiểm chứng** ở backend + web; **bạn đã tick đủ 10/10 mục** (người dùng tick theo quy ước `AGENTS.md` mục 12).
> - Backend: **81 test pass** (Domain 24 · Application 15 · Tích hợp 42 trên PostgreSQL thật), `dotnet build` 0 warning 0 error.
> - Web: `npm run lint` 0 warning/0 error; `npm run build` thành công; **kiểm chứng trên trình duyệt thật** (Chrome qua BrowserSkill): bảng tin + soạn/đăng bài + ảnh hiển thị + thích + bình luận (đếm tăng đúng) + tìm người dùng **không dấu** + trang bạn bè + trang cá nhân + nút quan hệ.
> - Kiểm chứng HTTP thật với **3 tài khoản**: 28/28 phép kiểm tra quan hệ bạn bè/theo dõi, 32/35 lượt kiểm tra bài viết (3 lỗi còn lại là do script test của tôi, đã sửa và chạy lại đạt), media 5/5.
> - **Hai lỗi thật đã sửa:** (1) `Visibility.Public` bị lưu thành `Friends` do bẫy enum `0` + `HasDefaultValue` (sửa bằng `HasSentinel`); (2) Vite thiếu proxy `/uploads` làm ảnh lưu cục bộ bị vỡ.
> - Migration: **M1** (`Social_InitialCreate`) + `Media_AddLocalProvider`. Chi tiết: `docs/tasks/phase2_social.md`; hợp đồng API: `docs/05_api_contract.md` mục 7.

> **Cập nhật 2026-10-18 (sau khi chuyển sang Cloudinary thật):**
> - **Cloudinary đã bật và kiểm chứng thật:** ký HMAC-SHA1 → client upload trực tiếp lên `comink/post`, đăng ký media (`provider = Cloudinary`), đăng bài kèm ảnh, ảnh truy cập công khai `HTTP 200 image/png`; `/uploads` trả `404` và `local-upload` bị chặn đúng thiết kế (400).
> - **(Sửa lỗi hợp đồng lỗi)** Body JSON không parse được trước đây trả `500 INTERNAL_ERROR`, nay trả `400 VALIDATION_FAILED` (`ExceptionStatusMapper`). Thêm nữa `title` nay suy ra từ `code` (`ApiResults.ApplyProblemDefaults`) nên lỗi cùng mã luôn cùng tiêu đề. `dotnet test` nay **92/92 pass** (Domain 24 · Application 15 · Tích hợp 53), `dotnet build` 0 warning 0 error.
> - Đã xoá bài test dùng ảnh lưu cục bộ (phương án a) — không còn bài "đang sống" nào tham chiếu ảnh `Local`.

### Giai đoạn 3: Nhắn tin CB + Ảnh trong tin nhắn

**Mục tiêu:** hai người dùng nhắn tin được với nhau theo thời gian thực và xem lại lịch sử.
**Phụ thuộc:** GĐ 1 (cần bạn bè từ GĐ 2 nếu giới hạn ai được nhắn cho ai).

- [ ] Tạo hoặc mở cuộc chat riêng 1–1 (mỗi cặp người chỉ có một cuộc chat)
- [ ] Gửi và nhận tin nhắn văn bản **theo thời gian thực** qua SignalR
- [ ] Tin nhắn được lưu vào DB; mở lại thấy đủ lịch sử, tải thêm khi cuộn lên (phân trang)
- [ ] Gửi ảnh trong tin nhắn
- [ ] Danh sách hội thoại sắp theo tin mới nhất, có xem trước tin cuối
- [ ] Gửi trùng khi mất mạng không tạo tin nhắn lặp (kiểm tra `client_message_id`)
- [ ] Mất kết nối rồi có lại thì client tự kết nối lại và lấy phần tin bị lỡ
- [ ] Người ngoài hội thoại **không** đọc được tin và **không** vào được group SignalR của hội thoại đó
- [ ] Có test cho quyền truy cập hội thoại và gửi/nhận tin

### Giai đoạn 4: Gọi trực tuyến CB

**Mục tiêu:** gọi thoại/video giữa hai người và gọi nhóm hoạt động ổn định.
**Phụ thuộc:** GĐ 3 (cuộc gọi gắn với hội thoại).

- [ ] Gọi thoại 1–1 hai máy khác nhau, nghe/nói được hai chiều
- [ ] Gọi video 1–1 hai chiều
- [ ] Gọi nhóm có cả thoại và video (tối thiểu 3 người tham gia)
- [ ] Backend cấp token gọi có thời hạn ngắn; người ngoài hội thoại không xin được token
- [ ] TURN hoạt động: gọi được khi hai máy ở **hai mạng khác nhau** hoặc sau NAT
- [ ] Cuộc gọi được ghi vào DB (người gọi, người nhận, loại, thời điểm kết thúc)
- [ ] Hiển thị được lúc cuộc gọi đến để người nhận có thể vào cuộc gọi
- [ ] Kết thúc cuộc gọi giải phóng đúng tài nguyên (không còn room "treo")

### Giai đoạn 5: Quản trị viên CB

**Mục tiêu:** admin quản lý được tài khoản người dùng.
**Phụ thuộc:** GĐ 1.

- [ ] Trang quản trị (web) chỉ admin vào được
- [ ] Xem, tìm kiếm danh sách tài khoản (phân trang)
- [ ] Khóa và mở khóa tài khoản
- [ ] Tài khoản bị khóa không đăng nhập được, phiên hiện tại bị thu hồi và kết nối SignalR bị ngắt
- [ ] Admin không tự khóa được chính mình

### ◆ Mốc A: Web CB hoàn chỉnh (cuối tuần 7)

Điều kiện để qua mốc: **tất cả checklist của GĐ 1 đến GĐ 5 đã tick**, cộng thêm:

- [ ] Kịch bản demo trên web chạy trọn vẹn với 3+ tài khoản: đăng ký → kết bạn → đăng bài → tương tác → nhắn tin → gọi → admin khóa tài khoản
- [ ] Có dữ liệu mẫu (seed) để dựng lại kịch bản demo trong vài phút
- [ ] Chạy ổn trên máy cá nhân bằng `docker compose up` theo `README`
- [ ] Danh sách lỗi đã biết được ghi lại và phân loại (chặn demo hay không)
- [ ] Hợp đồng API (OpenAPI) đủ ổn định để bắt đầu làm app

### Giai đoạn 6: App Flutter CB

**Mục tiêu:** đưa toàn bộ mức cơ bản sang app, dùng chung backend.
**Phụ thuộc:** Mốc A.

- [ ] Đăng ký, đăng nhập, lưu token an toàn (secure storage), tự làm mới token
- [ ] Hồ sơ, bạn bè, theo dõi, tìm kiếm người dùng
- [ ] Bảng tin, đăng bài kèm ảnh, thích, bình luận, chia sẻ
- [ ] Chat riêng real-time, xem lịch sử, gửi ảnh
- [ ] Gọi thoại/video 1–1 và gọi nhóm trên điện thoại
- [ ] Xin quyền micro, camera, thông báo; xử lý đẹp khi người dùng từ chối
- [ ] Push notification (FCM) cho tin nhắn mới, lời mời kết bạn, cuộc gọi đến
- [ ] Đa ngôn ngữ Việt/Anh
- [ ] Chạy ổn trên ít nhất một thiết bị thật Android (và iOS nếu có điều kiện), không chỉ giả lập

> Web push (FCM trên trình duyệt) có thể làm ở giai đoạn này hoặc trong giai đoạn 7.

### ◆ Mốc B: Web + App CB (cuối tuần 10)

- [ ] Kịch bản demo chạy trọn vẹn với **web và app cùng lúc** (ví dụ web gọi app và ngược lại)
- [ ] Toàn bộ CB của 8 nhóm tính năng hoàn thành (trừ AI và triển khai NC)
- [ ] Không còn lỗi chặn trong danh sách lỗi đã biết
- [ ] Báo cáo/tài liệu đã có bản nháp đầy đủ phần phân tích, thiết kế

### Giai đoạn 7: Nâng cao (chọn theo ưu tiên)

**Mục tiêu:** nâng chất lượng sản phẩm bằng các tính năng NC theo thứ tự ưu tiên của nhóm.
**Phụ thuộc:** mỗi mục chỉ bị chặn bởi CB của **chính nhóm đó** nên có thể làm xen kẽ từ trước Mốc B nếu có người rảnh.

Mỗi tính năng NC khi làm xong cần: chạy đúng trên web, có kiểm tra quyền/đầu vào, cập nhật tài liệu schema nếu có đổi, và (nếu thuộc phạm vi) có trên app.

**Gợi ý thứ tự ưu tiên** (giá trị demo cao và rủi ro thấp trước):

| Ưu tiên | Nhóm | Tính năng NC | Hoàn thành khi |
|---|---|---|---|
| 1 | Nhắn tin | Chat nhóm, trạng thái online, đang nhập, đã gửi/đã đọc | - [ ] Chat nhóm hoạt động<br>- [ ] Hiển thị online/offline đúng<br>- [ ] "Đang nhập" hiện và tự tắt<br>- [ ] Đã đọc cập nhật real-time |
| 2 | Nhắn tin | Phản hồi, thu hồi, chỉnh sửa, thả cảm xúc, ghim, chuyển tiếp, tìm kiếm tin, tắt thông báo từng cuộc | - [ ] Từng tính năng chạy trên web và app<br>- [ ] Tìm kiếm chỉ trong hội thoại người dùng là thành viên |
| 3 | Gọi | Cuộc gọi đến chấp nhận/từ chối, tắt mic, thời lượng, cuộc gọi nhỡ, lịch sử cuộc gọi | - [ ] Nhận/từ chối hoạt động<br>- [ ] Cuộc gọi nhỡ có thông báo<br>- [ ] Lịch sử và thời lượng đúng |
| 4 | Bảo mật | Xác minh email, đặt lại mật khẩu, chặn người dùng, quản lý phiên, giới hạn tần suất, chống spam, quyền riêng tư | - [ ] Email thật gửi được (Mailpit khi dev)<br>- [ ] Chặn người dùng áp dụng ở bảng tin, nhắn, gọi<br>- [ ] Rate limit chặn đúng đăng nhập, gửi tin, kết bạn<br>- [ ] Xem/thu hồi được các phiên đăng nhập |
| 5 | Mạng xã hội | Hashtag, lưu bài viết, gợi ý bạn bè | - [ ] Mỗi tính năng chạy đúng và có trang xem tương ứng |
| 6 | Hình ảnh và tệp | Video, tệp tài liệu, xem trước/tải xuống, album, giới hạn dung lượng, quản lý tệp | - [ ] Giới hạn dung lượng/định dạng chặn ở cả client và server<br>- [ ] Xóa tệp đồng bộ với cloud |
| 7 | Quản trị | Dashboard, quản lý bài viết/bình luận, báo cáo vi phạm, nhật ký quản trị, thống kê, kiểm duyệt nâng cao | - [ ] Luồng báo cáo → xử lý → ghi nhật ký chạy trọn vẹn<br>- [ ] Số liệu thống kê khớp dữ liệu thực |
| 8 | AI | Chatbot, hỗ trợ viết bài, tóm tắt tin nhắn | - [ ] Chỉ bắt đầu khi **các chức năng chính đã ổn định**<br>- [ ] Khóa API chỉ ở backend<br>- [ ] Có giới hạn số lần gọi mỗi ngày<br>- [ ] Tóm tắt chỉ dùng tin nhắn mà người yêu cầu có quyền xem |
| 9 | Triển khai | Truy cập từ thiết bị khác trong mạng (HTTPS), Docker image đầy đủ, CI/CD, cấu hình production | - [ ] Web và app thật truy cập được backend qua mạng LAN bằng HTTPS<br>- [ ] Gọi thoại/video hoạt động khi mở từ máy khác<br>- [ ] Pipeline tự build và chạy test<br>- [ ] Tách cấu hình dev/production, không lộ bí mật |

> Khi tiến độ trễ: giữ nguyên các mục ưu tiên 1–4, cắt từ cuối bảng lên (AI và triển khai Internet là phần dễ lùi nhất mà vẫn có đồ án đầy đủ).

### Giai đoạn 8: Hoàn thiện và Bảo vệ

**Mục tiêu:** sản phẩm ổn định, tài liệu đầy đủ, sẵn sàng demo.
**Phụ thuộc:** Mốc B.

- [ ] **Đóng băng tính năng** (không thêm tính năng mới từ đầu giai đoạn này)
- [ ] Sửa hết lỗi chặn; lỗi còn lại đã ghi nhận
- [ ] Kiểm thử thủ công theo kịch bản demo trên thiết bị thật (hai máy, hai mạng)
- [ ] Kiểm tra truy vấn chậm (bảng tin, danh sách hội thoại, tìm kiếm) với dữ liệu mẫu lớn
- [ ] Rà soát bảo mật theo checklist ở `02_tech_stack_decisions.md` (mục 4.3)
- [ ] Kiểm tra sao lưu và khôi phục DB ít nhất một lần
- [ ] Báo cáo đồ án hoàn chỉnh (phân tích, thiết kế, cài đặt, kiểm thử, kết luận)
- [ ] Slide và kịch bản bảo vệ; **diễn tập ít nhất 2 lần**, kể cả tình huống mất mạng
- [ ] Phương án dự phòng cho demo (video quay sẵn, dữ liệu mẫu, thiết bị dự phòng)

---

## 4. Tiêu chí hoàn thành chung (Definition of Done)

Một tính năng chỉ được coi là **xong** khi thỏa tất cả:

- [ ] Chạy đúng yêu cầu trong `COMINK_SPEC.md`, qua kịch bản thử với ít nhất 2 tài khoản
- [ ] Có kiểm tra dữ liệu đầu vào và quyền truy cập ở backend
- [ ] Có xử lý trạng thái tải, rỗng, lỗi trên giao diện; chuỗi hiển thị đi qua đa ngôn ngữ
- [ ] Có test cho phần nghiệp vụ quan trọng; CI xanh
- [ ] Đã review chéo bởi thành viên còn lại và gộp vào `main`
- [ ] Tài liệu liên quan (schema, hợp đồng API) đã cập nhật
- [ ] Có dữ liệu mẫu để thử nhanh

---

## 5. Rủi ro chính và cách xử lý

| Rủi ro | Dấu hiệu | Cách xử lý |
|---|---|---|
| Gọi nhóm + TURN mất nhiều thời gian hơn dự kiến | GĐ 4 vượt 1,5 tuần | Chốt phương án 1–1 trước, gọi nhóm làm sau; tận dụng cấu hình sẵn của LiveKit, đừng tự cấu hình TURN |
| Làm app khi web chưa ổn | App liên tục bị vỡ vì API đổi | Chỉ bắt đầu GĐ 6 sau Mốc A; khóa hợp đồng API, sinh client từ OpenAPI |
| Trễ vì cả hai cùng sửa một phần | Xung đột Git/migration thường xuyên | Chia theo tính năng; quy tắc một người tạo migration tại một thời điểm |
| Dịch vụ ngoài lỗi/hết hạn mức lúc demo | Cloudinary, LiveKit Cloud, API AI | Dùng bản self-host/dữ liệu mẫu cho demo; có phương án dự phòng |
| Demo qua mạng khác không chạy | Micro/camera bị chặn vì không có HTTPS | Chuẩn bị HTTPS cục bộ sớm (xem `02_tech_stack_decisions.md`, mục 3.8) |
| Quá nhiều tính năng NC | Mỗi tính năng làm dở | Chỉ bắt đầu NC mới khi NC trước đã qua Definition of Done; cắt từ cuối bảng ưu tiên |

---

## 6. Liên hệ với các tài liệu khác

| Giai đoạn | Migration tương ứng (`03_database_design.md`, mục 6.3) |
|---|---|
| GĐ 0–1 | M0 |
| GĐ 2 | M1 |
| GĐ 3 | M2 |
| GĐ 4 | M3 |
| GĐ 5 | M5 (phần quản trị cơ bản dùng sẵn cột trên `users` từ M0) |
| GĐ 6 | M4 (thiết bị push, thông báo) |
| GĐ 7 | Các migration N-x và nhóm AI cuối cùng |