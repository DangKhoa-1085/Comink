# Giai đoạn 2 — Mạng xã hội CB + Ảnh trong bài viết

> Tài liệu 06, phân rã từ `docs/04_roadmap.md` (Giai đoạn 2) theo yêu cầu ở `AGENTS.md` mục 7.3.
> **Mục tiêu:** có một mạng xã hội dùng được — kết nối bạn bè, theo dõi, đăng bài kèm ảnh, tương tác, bảng tin.
> **Phụ thuộc:** Giai đoạn 1 — **đã hoàn tất** (`docs/tasks/phase1_foundation_auth.md`).
> **Trạng thái:** ✅ **HOÀN TẤT** (backend + web), đã kiểm chứng trên trình duyệt thật (2026-10-17).
>
> Kiểm chứng cuối cùng:
> - `dotnet build`: 7/7 project, 0 warning, 0 error; `dotnet test`: **81 test pass** (Domain 24 · Application 15 · Tích hợp 42 trên PostgreSQL thật).
> - `npm run lint`: 0 warning, 0 error; `npm run build`: thành công.
> - **Trình duyệt thật** (Chrome qua BrowserSkill): đăng nhập → bảng tin có bài + ảnh hiển thị → soạn và đăng bài mới → thích → bình luận (đếm bình luận tăng 0 → 1) → tìm người dùng **không dấu** (`tran` → "Minh Trần") → trang Bạn bè (3 mục + trạng thái rỗng) → trang cá nhân công khai (số liệu + nút Hủy kết bạn/Theo dõi + danh sách bài).
> - Kiểm chứng HTTP thật với **3 tài khoản**: 28/28 phép kiểm tra quan hệ, 32/35 lượt kiểm tra bài viết (3 lỗi là do script test, chạy lại đạt), media 5/5.
> - **Ba lỗi thật đã sửa:** bẫy `Visibility.Public` → `Friends` (thiếu sentinel); Vite thiếu proxy `/uploads`; và lỗi body JSON hỏng trả `500 INTERNAL_ERROR` thay vì `400 VALIDATION_FAILED` (`BadHttpRequestException` chưa map — đã thêm `ExceptionStatusMapper` + 4 test).
> - **Checklist** GĐ 2 trong `docs/04_roadmap.md` (dòng 92–101) **đã được bạn tick đủ 10/10 mục**.
>
> **Ước lượng:** tuần 3–4 theo roadmap. Migration tương ứng: **M1** (`Social_InitialCreate`) + `Media_AddLocalProvider`.
> **Người phụ trách:** Dev A (mạng xã hội + ảnh/tệp + quản trị) — `docs/04_roadmap.md` mục 2.

---

## 1. Phạm vi

Lấy từ `docs/01_COMINK_SPEC.md` mục 3.1 **[CB]** (dòng 61–66) và 3.4 **[CB]** (ảnh trong bài viết), cùng checklist GĐ 2 của roadmap:

- Kết bạn hai chiều: gửi, chấp nhận, từ chối lời mời; xem danh sách bạn
- Theo dõi / bỏ theo dõi (một chiều)
- Tìm kiếm người dùng (gõ không dấu vẫn ra)
- Đăng, sửa, xóa bài viết; phạm vi hiển thị (công khai / bạn bè / chỉ mình tôi)
- Đính kèm ảnh vào bài viết (tải lên cloud, hiển thị đúng)
- Bảng tin: bài của bạn bè và người đang theo dõi, phân trang cuộn
- Thích, bình luận (có trả lời 1 cấp) và chia sẻ bài viết
- Trang cá nhân: bài viết + số bạn bè / người theo dõi
- Quyền xem bài đúng (không xem được bài `Friends` của người lạ)
- Test cho quyền xem bài và trạng thái kết bạn

**Ngoài phạm vi GĐ 2** (làm ở GĐ 7 theo bảng ưu tiên NC): hashtag, lưu bài viết, gợi ý bạn bè, chặn người dùng, video/tệp tài liệu, album, thông báo đẩy (GĐ 6), kiểm duyệt nâng cao (GĐ 5/7).

---

## 2. Điều kiện tiên quyết

| # | Hạng mục | Trạng thái |
|---|---|---|
| 1 | GĐ 1 hoàn tất (auth, hồ sơ, phân quyền, SignalR nền) | ✅ |
| 2 | Extension `pg_trgm` + `unaccent` + hàm `immutable_unaccent` | ✅ có từ M0 (`docs/03_database_design.md` mục 5.1) |
| 3 | Cột đếm `friendships`/`follows`/`posts`… trong `user_profiles` (`friend_count`, `follower_count`, `following_count`) | ✅ có từ M0 |
| 4 | Migration **M1** (7 bảng, xem 4.1) | ✅ đã tạo và áp (`20261017090235_Social_InitialCreate`) |
| 5 | Khoá Cloudinary khi dev | ✅ đã chọn **phương án B**: Cloudinary cho production + `LocalDiskMediaStorage` dự phòng khi dev thiếu khoá (xem 3.9) |
| 6 | Hợp đồng API ở mục 3 được chốt | ✅ đã chốt 2026-10-17, kèm 2 mặc định: lời mời chéo chiều **tự động chấp nhận**; bài không có quyền xem trả **404** |
| 7 | Sửa lỗi GĐ 1: admin seed có khóa `Guid.Empty` | ✅ xong 2026-10-17 (xem `phase1_foundation_auth.md` mục 3.9, lỗi 3) |

---

## 3. Hợp đồng API — ✅ đã chốt (2026-10-17)

> Bổ sung vào `docs/05_api_contract.md` khi code xong mục 4.5. Giữ nguyên quy ước chung: JSON `camelCase`, phân trang con trỏ `(created_at, id)` với `cursor` + `limit` (mặc định 20, tối đa 50), lỗi RFC 7807 kèm `code`, backend không trả câu chữ hiển thị.

### 3.1. Người dùng và tìm kiếm — `/api/users`

| Method | Đường dẫn | Mục đích |
|---|---|---|
| `GET` | `/api/users?search=&cursor=&limit=` | Tìm người dùng; `search` **bắt buộc** 1–100 ký tự, gõ không dấu vẫn khớp (`immutable_unaccent` + `pg_trgm`) |
| `GET` | `/api/users/{id}` | Hồ sơ công khai + quan hệ với người xem + số liệu |

`UserSummaryDto`:

```json
{
  "id": "0199...",
  "userName": "khoa.nguyen",
  "displayName": "Nguyễn Khoa",
  "avatarUrl": null,
  "bio": "Sinh viên CNTT",
  "relationship": { "friendship": "Friends", "following": true, "followedBy": false }
}
```

- `friendship` ∈ `None` · `OutgoingPending` · `IncomingPending` · `Friends` — để web biết hiện nút "Kết bạn" / "Đã gửi" / "Chấp nhận" / "Bạn bè".
- `GET /api/users/{id}` trả thêm `followerCount`, `followingCount`, `friendCount`, `createdAt`; người không tồn tại hoặc `account_status = Locked` → `NOT_FOUND`.

### 3.2. Bạn bè hai chiều — `/api/friendships`

| Method | Đường dẫn | Mục đích |
|---|---|---|
| `POST` | `/api/friendships/requests` | Gửi lời mời; body `{ "addresseeId": "uuid" }` → `201` `FriendshipDto` |
| `GET` | `/api/friendships/requests/incoming` | Lời mời tôi nhận được (phân trang) |
| `GET` | `/api/friendships/requests/outgoing` | Lời mời tôi đã gửi (phân trang) |
| `POST` | `/api/friendships/requests/{id}/accept` | Chấp nhận (chỉ người nhận) → `200` `FriendshipDto` |
| `DELETE` | `/api/friendships/requests/{id}` | Từ chối / thu hồi lời mời đã gửi → `204` |
| `GET` | `/api/friendships` | Danh sách bạn bè (phân trang) |
| `DELETE` | `/api/friendships/{userId}` | Hủy kết bạn (một trong hai người) → `204` |

`FriendshipDto`: `{ id, user: UserSummaryDto, status: "Pending"|"Accepted", createdAt, respondedAt }`

Quy tắc nghiệp vụ (đề xuất):

1. Mỗi cặp người chỉ một dòng, dùng chỉ mục duy nhất `LEAST/GREATEST` (đã có trong thiết kế DB) — API phải idempotent khi bấm hai lần.
2. Gửi lời mời khi **đã là bạn** → `409 SOCIAL_ALREADY_FRIENDS`.
3. Gửi lời mời khi **đã có lời mời cùng chiều đang chờ** → `200` trả lại lời mời đó (không tạo trùng).
4. Gửi lời mời khi **đối phương đã mời mình trước** → ❓ **tự động chấp nhận** (trả `200`, `status = Accepted`), thay vì báo lỗi. Lý do: tránh hai lời mời chéo nhau và giảm thao tác.
5. Tự kết bạn với chính mình → `400 SOCIAL_CANNOT_FRIEND_SELF`.
6. Chấp nhận/từ chối lời mời không phải của mình → `403 SOCIAL_NOT_REQUEST_ADDRESSEE`.
7. Chấp nhận lời mời đã `Accepted` → `200` (idempotent). Từ chối/hủy → `204` (idempotent, xóa dòng).
8. Khi thành bạn: cộng `user_profiles.friend_count` của **cả hai**; khi hủy kết bạn: trừ đi (không âm — dùng `GREATEST(0, ...)`).

### 3.3. Theo dõi một chiều — `/api/follows`

| Method | Đường dẫn | Mục đích |
|---|---|---|
| `POST` | `/api/follows/{userId}` | Theo dõi → `204` (idempotent) |
| `DELETE` | `/api/follows/{userId}` | Bỏ theo dõi → `204` (idempotent) |
| `GET` | `/api/users/{id}/followers` | Danh sách người theo dõi tôi (phân trang) |
| `GET` | `/api/users/{id}/following` | Danh sách tôi đang theo dõi (phân trang) |

- Tự theo dõi mình → `400 SOCIAL_CANNOT_FOLLOW_SELF`.
- Người dùng không tồn tại → `404 NOT_FOUND`.
- Theo dõi thành công: cộng `follower_count` (người được theo dõi) và `following_count` (người theo dõi); bỏ theo dõi trừ đi tương ứng.

### 3.4. Bài viết — `/api/posts`

| Method | Đường dẫn | Mục đích |
|---|---|---|
| `POST` | `/api/posts` | Tạo bài → `201` `PostDto` |
| `GET` | `/api/posts/{id}` | Xem một bài (kiểm tra quyền xem) |
| `PATCH` | `/api/posts/{id}` | Sửa nội dung / phạm vi (chỉ tác giả) → `200` |
| `DELETE` | `/api/posts/{id}` | Xóa mềm (chỉ tác giả) → `204` |
| `GET` | `/api/feed?cursor=&limit=` | Bảng tin: bài của bạn bè + người đang theo dõi + của chính mình |
| `GET` | `/api/users/{id}/posts?cursor=&limit=` | Bài trên trang cá nhân (lọc theo quyền xem) |

Request tạo bài:

```json
{
  "content": "Hôm nay học WebRTC",
  "visibility": "Friends",
  "mediaIds": ["0199...", "0199..."],
  "sharedPostId": null
}
```

Quy tắc (đề xuất):

1. `content` ≤ 5000 ký tự. Được phép rỗng **chỉ khi** có `mediaIds` hoặc `sharedPostId`; nếu không → `400 POST_CONTENT_REQUIRED`.
2. `visibility` ∈ `Public` · `Friends` · `OnlyMe`; bỏ trống thì lấy `user_settings.default_post_visibility` (M0), mặc định `Friends`.
3. `mediaIds` tối đa **10** ảnh, phải là tài sản **của chính người đăng** với `context = Post` và `deleted_at IS NULL` → sai thì `400 POST_MEDIA_NOT_OWNED`; quá số lượng → `400 POST_TOO_MANY_MEDIA`.
4. `sharedPostId` trỏ tới bài người dùng **được phép xem**; tăng `share_count` bài gốc; không cho chia sẻ lại bài đã xóa → `404 POST_NOT_FOUND`.
5. **Quyền xem bài** (áp dụng cho `GET /api/posts/{id}`, `/api/users/{id}/posts`, `/api/feed`, thích và bình luận):
   - `Public`: ai đăng nhập cũng xem được.
   - `Friends`: chỉ bạn bè (hoặc chính tác giả).
   - `OnlyMe`: chỉ tác giả.
   - Bài `status = Hidden` hoặc `deleted_at` khác null: coi như không tồn tại.
6. **Bài không được phép xem → `404 POST_NOT_FOUND`** (không dùng `403`) để không tiết lộ bài đó có tồn tại. ❓ cần xác nhận.
7. Sửa/xóa bài của người khác → `403 FORBIDDEN`.
8. Xóa mềm: đặt `deleted_at`; ảnh giữ nguyên (dọn sau), các cột đếm của bài gốc được cập nhật giảm.

`PostDto`:

```json
{
  "id": "0199...",
  "author": { "id": "0199...", "userName": "khoa.nguyen", "displayName": "Nguyễn Khoa", "avatarUrl": null, "bio": null,
              "relationship": { "friendship": "Friends", "following": true, "followedBy": false } },
  "content": "Hôm nay học WebRTC",
  "visibility": "Friends",
  "media": [ { "id": "0199...", "url": "https://...", "thumbnailUrl": "https://...", "width": 800, "height": 600 } ],
  "sharedPost": null,
  "likeCount": 0, "commentCount": 0, "shareCount": 0,
  "likedByMe": false, "isMine": false,
  "createdAt": "2026-10-17T02:00:00Z", "updatedAt": null
}
```

### 3.5. Thích — `/api/posts/{id}/likes`

| Method | Đường dẫn | Mục đích |
|---|---|---|
| `POST` | `/api/posts/{id}/likes` | Thích → `204` (idempotent) |
| `DELETE` | `/api/posts/{id}/likes` | Bỏ thích → `204` (idempotent) |

- Không được xem bài → `404 POST_NOT_FOUND`.
- Cập nhật `posts.like_count` trong cùng transaction; chỉ một người thích một lần (khóa chính kép).

### 3.6. Bình luận — `/api/posts/{id}/comments`, `/api/comments/{id}`

| Method | Đường dẫn | Mục đích |
|---|---|---|
| `POST` | `/api/posts/{id}/comments` | Tạo bình luận / trả lời → `201` `CommentDto` |
| `GET` | `/api/posts/{id}/comments` | Bình luận gốc của bài (phân trang) |
| `GET` | `/api/comments/{id}/replies` | Các trả lời của một bình luận (phân trang) |
| `PATCH` | `/api/comments/{id}` | Sửa nội dung (chỉ tác giả) → `200` |
| `DELETE` | `/api/comments/{id}` | Xóa mềm (tác giả bình luận **hoặc** chủ bài viết) → `204` |

- Body tạo: `{ "content": "...", "parentCommentId": "uuid|null" }`; `content` 1–2000 ký tự.
- **Chỉ 1 cấp trả lời**: `parentCommentId` phải trỏ tới bình luận có `parent_comment_id IS NULL`, nếu không → `400 COMMENT_REPLY_DEPTH_EXCEEDED`.
- `parentCommentId` không tồn tại / không thuộc bài → `404 COMMENT_NOT_FOUND`.
- Đếm: `comments.reply_count` (bình luận gốc) và `posts.comment_count` tăng/giảm trong transaction; xóa bình luận gốc thì các trả lời cũng bị coi là đã xóa (cascade mềm ở tầng ứng dụng).

`CommentDto`: `{ id, postId, parentCommentId, author: UserSummaryDto, content, replyCount, createdAt, updatedAt }`

### 3.7. Media (ảnh trong bài viết) — `/api/media`

| Method | Đường dẫn | Mục đích |
|---|---|---|
| `POST` | `/api/media/signature` | Cấp chữ ký để client upload **trực tiếp** lên Cloudinary |
| `POST` | `/api/media` | Đăng ký tài sản sau khi upload xong → `201` `MediaAssetDto` |

Request chữ ký: `{ "context": "Post", "resourceType": "Image" }` → trả về
`{ signature, apiKey, cloudName, timestamp, uploadPreset, folder, uploadUrl }`.

Request đăng ký: `{ providerPublicId, url, thumbnailUrl?, mimeType, sizeBytes, width?, height?, context }`.

Ràng buộc (đề xuất, kiểm tra **cả client và server**):

| Loại | Giới hạn |
|---|---|
| Định dạng ảnh cho bài viết | `image/jpeg`, `image/png`, `image/webp` |
| Dung lượng mỗi ảnh | ≤ 5 MB |
| Số ảnh mỗi bài | ≤ 10 |
| `provider` | `Cloudinary` (ghi vào DB) |

Lỗi: `MEDIA_UNSUPPORTED_TYPE` (400), `MEDIA_TOO_LARGE` (400), `MEDIA_NOT_FOUND` (404).

### 3.8. Mã lỗi mới (đề xuất)

| Mã | HTTP | Ý nghĩa |
|---|---|---|
| `SOCIAL_CANNOT_FRIEND_SELF` | 400 | Không thể kết bạn với chính mình |
| `SOCIAL_CANNOT_FOLLOW_SELF` | 400 | Không thể tự theo dõi mình |
| `SOCIAL_ALREADY_FRIENDS` | 409 | Đã là bạn bè |
| `SOCIAL_FRIEND_REQUEST_NOT_FOUND` | 404 | Lời mời không tồn tại / đã xử lý |
| `SOCIAL_NOT_REQUEST_ADDRESSEE` | 403 | Không phải người nhận lời mời |
| `POST_NOT_FOUND` | 404 | Không tìm thấy bài hoặc không có quyền xem |
| `POST_CONTENT_REQUIRED` | 400 | Bài rỗng: cần nội dung, ảnh hoặc bài chia sẻ |
| `POST_TOO_MANY_MEDIA` | 400 | Quá số ảnh cho phép |
| `POST_MEDIA_NOT_OWNED` | 400 | Ảnh không thuộc người đăng |
| `COMMENT_NOT_FOUND` | 404 | Không tìm thấy bình luận |
| `COMMENT_REPLY_DEPTH_EXCEEDED` | 400 | Chỉ cho phép trả lời 1 cấp |
| `MEDIA_UNSUPPORTED_TYPE` | 400 | Định dạng tệp không hỗ trợ |
| `MEDIA_TOO_LARGE` | 400 | Tệp vượt dung lượng cho phép |
| `MEDIA_NOT_FOUND` | 404 | Không tìm thấy tài sản media |

### 3.9. Cloudinary khi phát triển — ✅ đã chọn phương án **B**

`docs/02_tech_stack_decisions.md` mục 3.4 đã chốt: client upload **trực tiếp** lên Cloudinary bằng chữ ký do backend cấp, trừu tượng qua `IMediaStorage`. Vấn đề: cần tài khoản Cloudinary thật (CloudName/ApiKey/ApiSecret) mới upload được, trong khi `.env` hiện chỉ có placeholder.

**Quyết định (2026-10-17): phương án B** — cài **cả hai**, chọn theo cấu hình:

| Cài đặt | Khi nào dùng | Ghi chú |
|---|---|---|
| `CloudinaryMediaStorage` | Có đủ `MediaStorage__CloudName/ApiKey/ApiSecret` | Đúng kiến trúc đã chốt; ký HMAC SHA-1, upload trực tiếp từ client |
| `LocalDiskMediaStorage` | Thiếu khoá Cloudinary (máy dev của nhóm) | Ghi tệp vào `infra/uploads`, phục vụ qua static file **chỉ ở môi trường Development**; không dùng cho production |

Ba phương án đã cân nhắc:

| Phương án | Nội dung | Đánh đổi |
|---|---|---|
| **A. Dùng Cloudinary thật** *(khuyến nghị nếu nhóm đã có tài khoản)* | Chỉ cài `CloudinaryMediaStorage`; bạn điền 3 khoá vào `.env` | Đúng kiến trúc đã chốt, demo được ảnh thật; phụ thuộc mạng và hạn mức |
| **B. Thêm cài đặt dự phòng cho dev** | Cài `CloudinaryMediaStorage` cho production + `LocalDiskMediaStorage` (ghi vào `infra/uploads`, phục vụ qua static file) dùng khi thiếu khoá Cloudinary | Chạy/demo được offline, không cần tài khoản; thêm ~1 cài đặt và một route tĩnh phải bảo vệ |
| **C. Hoãn ảnh sang sau** | Làm hết phần chữ (bạn bè, theo dõi, bài viết không ảnh, tương tác) rồi mới làm ảnh | Ít rủi ro nhất, nhưng **không đạt** mục "đính kèm ảnh" của GĐ 2 |

---

## 4. Phân rã công việc

### 4.1. Cơ sở dữ liệu — migration **M1**

- [x] Tạo 7 bảng theo `docs/03_database_design.md` mục 3.2/3.5 và 6.3: `friendships`, `follows`, `posts`, `post_media`, `post_likes`, `comments`, `media_assets` *(migration `20261017090235_Social_InitialCreate`)*
- [x] Enum dạng `varchar` + `CHECK`: `Visibility`, `FriendshipStatus`, `ContentStatus`, `MediaResourceType`, `MediaContext`, `MediaProvider` — enum C# ở `Domain/Enums`, chuyển chuỗi bằng `HasConversion<string>()`
- [x] Chỉ mục: `ix_posts_author_id_created_at_id` (điều kiện `deleted_at IS NULL AND status='Active'`, DESC theo `created_at, id`), `ux_friendships_pair` (LEAST/GREATEST, tạo bằng SQL), `friendships(addressee_id,status)`, `friendships(requester_id,status)`, `follows(followee_id)`, `comments(post_id, created_at, id)` + `comments(parent_comment_id, created_at)`, `media_assets(owner_id)`, UQ `(provider, provider_public_id)`
- [x] Cột sinh `posts.search_vector` (dùng `immutable_unaccent`) + chỉ mục GIN; chỉ mục trigram cho `user_profiles(display_name)` và `users(normalized_user_name)` phục vụ tìm kiếm người dùng (tạo bằng SQL vì là chỉ mục biểu thức)
- [x] `xmin` làm concurrency token cho `posts` (cột hệ thống — đã kiểm chứng migration **không** sinh cột mới); `comments`/`messages` để khi cần
- [x] Chạy migration lên PostgreSQL dev và **đối chiếu SQL sinh ra** với tài liệu
- [x] Cập nhật `docs/03_database_design.md` nếu có sai khác so với bản đã chốt → **không có sai khác** (chỉ mục, cột sinh, CHECK đều khớp mục 3.2/3.5/5.2)

**Kiểm chứng đã chạy (2026-10-17):** `dotnet ef migrations add Social_InitialCreate` và `database update` thành công; 7 bảng có trong `information_schema`; `posts.search_vector` là cột sinh đúng biểu thức; **không** có cột `xmin`; thử hai ràng buộc then chốt bằng transaction + `ROLLBACK`: cặp ngược chiều bị chặn bởi `ux_friendships_pair`, tự kết bạn bị chặn bởi `ck_friendships_not_self`, số dòng `friendships` sau thử nghiệm = 0. Tìm không dấu đúng: `immutable_unaccent(lower('Tên Đã Sửa')) = 'ten da sua'`, tìm `khoa` ra "Khoa Nguyễn". `dotnet test`: **29/29 pass** (test tích hợp áp lại toàn bộ migration trên DB mới).

### 4.2. Domain ✅

- [x] Enum `Visibility`, `FriendshipStatus`, `ContentStatus`, `MediaResourceType`, `MediaContext`, `MediaProvider` (`Domain/Enums`)
- [x] Quy tắc nghiệp vụ test được: `PostVisibilityRules.CanView/IsVisible`, `FriendshipRules.IsSelfPair/CanAccept/CanSendRequest/Increment/Decrement`, `CommentRules.CanReplyTo/IsContentValid` (`Domain/Social`)
- [x] Không đặt logic truy cập DB ở Domain
- [x] Test quy tắc ở `Comink.Domain.Tests/SocialRulesTests.cs` (**23 test**: ma trận quyền xem 3×3, trạng thái bạn bè, độ sâu trả lời)
- [ ] `Post.EnsureContentOrMedia(...)` — sẽ thêm cùng `IPostService` (mục 4.3)

### 4.3. Application — `Social/` 🚧

- [x] `IFriendshipService`: gửi / chấp nhận / từ chối / hủy / danh sách bạn / đếm `friend_count`
- [x] `IFollowService`: theo dõi / bỏ theo dõi / danh sách / đếm
- [x] `IUserDirectoryService`: tìm người dùng (không dấu), hồ sơ công khai + quan hệ
- [ ] `IPostService`: tạo / xem / sửa / xóa / bảng tin / bài trên trang cá nhân + **kiểm tra quyền xem** tập trung một chỗ (quy tắc đã có ở `PostVisibilityRules`)
- [ ] `ILikeService`, `ICommentService` (1 cấp trả lời, đếm)
- [ ] `IMediaService`: cấp chữ ký, đăng ký tài sản, kiểm tra loại và dung lượng
- [x] DTO cho mọi request/response (không trả entity — `AGENTS.md` mục 8.1): `SocialDtos.cs`
- [x] FluentValidation cho request body và tham số truy vấn: `SocialValidators.cs`
- [x] Mã lỗi mới vào `ErrorCodes`: 5 mã `SOCIAL_*`
- [x] Chuyển `PagedList` + `CursorCodec` xuống `Application/Common` để cả hai tầng dùng chung (xóa bản trùng ở Api)

### 4.4. Infrastructure ✅

- [x] Cấu hình EF cho 7 bảng (quan hệ, chỉ mục có điều kiện, `CHECK`, cột sinh)
- [x] Ánh xạ hàm `immutable_unaccent` để dùng trong LINQ (`CominkDbFunctions` + `HasDbFunction`)
- [x] `UserDirectoryService`, `FriendshipService`, `FollowService`, `PostService`, `LikeService`, `CommentService`, `MediaService` (`Infrastructure/Social`)
- [x] `SocialLookup` (quan hệ bằng 3 truy vấn cố định), `PostAccess` (một chỗ kiểm tra quyền xem), `PostMapping` (5 truy vấn cố định cho cả trang — không N+1)
- [x] Cập nhật cột đếm **trong cùng một `SaveChanges`** với thao tác (bạn bè, theo dõi, thích, bình luận, chia sẻ)
- [x] Bảng tin theo `docs/03_database_design.md` mục 5.3: đối tượng = bạn bè ∪ đang theo dõi ∪ chính mình, lọc visibility ngay trong SQL, phân trang con trỏ
- [x] `CloudinaryMediaStorage` (chữ ký HMAC-SHA1) và `LocalDiskMediaStorage` (dev) + chọn theo cấu hình
- [x] Phục vụ tệp cục bộ qua `/uploads` **chỉ khi** không dùng Cloudinary

### 4.5. API — `/api` ✅

- [x] Nhóm endpoint `friendship`, `follow`, `user-directory`, `post`, `like`, `comment`, `media`
- [x] Mọi endpoint lấy `CurrentUser` từ token; **không tin id người dùng trong body** (`AGENTS.md` mục 9.2)
- [x] Phân trang con trỏ cho mọi danh sách (`PagedList` + `CursorCodec`)
- [x] Trả `code` đúng cho lỗi nghiệp vụ — **đã bổ sung toàn bộ mã GĐ 2 vào `ApiResults.StatusFor/TitleFor`**
- [x] Giới hạn kích thước/tham số đầu vào (`limit` ≤ 50, `search` 1–100, `content` ≤ 5000, ảnh ≤ 5 MB, ≤ 10 ảnh/bài)

> ⚠️ **Bẫy cần nhớ:** thêm mã lỗi mới vào `ErrorCodes` mà **quên** thêm vào `ApiResults.StatusFor/TitleFor` thì API trả **500** kèm `code` đúng — rất dễ nhầm là lỗi server. Đã gặp thật khi làm GĐ 2. Khi thêm mã mới, luôn cập nhật cả ba chỗ (`ErrorCodes`, `StatusFor`, `TitleFor`).

> ⚠️ **Bẫy thứ hai (đã gặp thật):** enum có giá trị `0` trùng `default(T)` **và** cột có `HasDefaultValue` khác giá trị đó → EF coi là "chưa gán" và bỏ cột khỏi `INSERT`, DB ghi giá trị mặc định. Cụ thể: `Visibility.Public = 0` từng bị lưu thành `Friends`. Đã sửa bằng `HasSentinel((Visibility)(-1))` ở `posts.visibility`. Khi thêm enum tương tự, phải đặt sentinel.

### 4.6. Web 🚧 (code xong, chờ kiểm chứng bằng mắt)

- [x] `api/` mới: `friendships.ts`, `follows.ts`, `users.ts` (mở rộng: tìm kiếm + hồ sơ công khai), `posts.ts`, `comments.ts`, `media.ts`
- [x] Trang **Bảng tin** (`/` khi đã đăng nhập, và `/feed`): `useInfiniteQuery` + `IntersectionObserver` (phân trang cuộn), đủ trạng thái tải / rỗng / lỗi
- [x] Trang **Trang cá nhân** (`/users/:id`): hồ sơ, số bạn bè/người theo dõi, nút Kết bạn / Chấp nhận / Hủy kết bạn / Theo dõi, danh sách bài
- [x] Trang **Tìm kiếm** (`/search`): debounce 350 ms, gõ không dấu, trạng thái rỗng/gợi ý
- [x] Trang **Bạn bè** (`/friends`): lời mời nhận được (chấp nhận/từ chối), lời mời đã gửi (thu hồi), danh sách bạn bè
- [x] Hộp **soạn bài**: nội dung, chọn phạm vi, chọn ảnh (upload bằng chữ ký → đăng ký tài sản), xem trước, xóa ảnh, giới hạn loại/dung lượng ngay ở client
- [x] Tương tác: thích (idempotent), bình luận + trả lời (xem trả lời), chia sẻ, xóa bài/bình luận của mình
- [x] **Sửa bài viết ngay trên bảng tin** (nút bút chì ở footer bài của mình): sửa nội dung + phạm vi, lưu → **đã kiểm chứng trên trình duyệt** (nội dung đổi đúng). Ghi chú: đặt nút này ở `<header>` của bài thì cú pháp đúng nhưng bấm không ăn (thử nhiều lần, không có lỗi console) — chuyển xuống `<footer>` thì hoạt động; nên giữ nút hành động ở footer.
- [x] **(Sửa lỗi GĐ 1) Giao diện sáng/tối:** Tailwind CSS v4 mặc định gắn `dark:` với `prefers-color-scheme`, nên máy đang bật chế độ tối thì nút "Đổi giao diện" không có tác dụng. Đã thêm `@custom-variant dark (&:where(.dark, .dark *))` vào `index.css` + áp theme trước khi React render (`main.tsx`) để không nháy màu. **Đã kiểm chứng: bấm nút chuyển rõ rệt tối ↔ sáng.**
- [x] Mọi chuỗi qua i18n `vi`/`en` — thêm mục `social` (~70 khoá) cho cả hai ngôn ngữ
- [x] Mọi màn hình có đủ ba trạng thái; xử lý lỗi bằng `code` (`translateErrorCode`)
- [x] Điều hướng: thêm **Tìm kiếm**, **Bạn bè** vào header; route mới `/search`, `/friends`, `/users/:id`, `/feed`
- [x] `npm run lint`: **0 warning, 0 error**; `npm run build`: thành công (687 kB)
- [x] **Kiểm chứng bằng mắt trên trình duyệt** với 3 tài khoản (Chrome qua BrowserSkill): bảng tin, đăng bài, ảnh, thích, bình luận (đếm 0 → 1), tìm không dấu, trang Bạn bè, trang cá nhân

### 4.7. Kiểm thử

- [x] Unit test: gửi lời mời trùng (idempotent), tự kết bạn, chuyển trạng thái bạn bè, đếm bạn bè khi chấp nhận/hủy → `Comink.Domain.Tests/SocialRulesTests.cs` + `Comink.Api.IntegrationTests/SocialServiceTests.cs`
- [x] Unit test: **quyền xem bài** (Public/Friends/OnlyMe × tác giả/bạn bè/người lạ) — mục tiêu chính của GĐ 2 → `PostVisibilityRulesTests` (8 ca `CanView` + 3 ca `IsVisible`)
- [ ] Unit test: tạo bài rỗng không ảnh → lỗi; quá 10 ảnh → lỗi; ảnh của người khác → lỗi (khi làm `PostService`)
- [x] Unit test: bình luận trả lời 2 cấp → lỗi (quy tắc `CommentRules.CanReplyTo`) — phần service khi làm `CommentService`
- [x] Integration test (Testcontainers PostgreSQL): tìm người dùng **không dấu**, luồng kết bạn → chấp nhận → cột đếm, lời mời chéo chiều tự động chấp nhận, quan hệ trong hồ sơ công khai
- [x] Integration test: "kết bạn → thấy bài `Friends`", người lạ **không** thấy, bảng tin đúng đối tượng và phân trang con trỏ (`PostServiceTests`)
- [x] Integration test: tạo bài rỗng không ảnh → lỗi; ảnh của người khác → lỗi; ảnh hợp lệ gắn được vào bài; upload cục bộ + đăng ký tài sản (`MediaServiceTests`)
- [x] Kiểm chứng HTTP thật với **2 tài khoản** (`khoa.test`, `minh.test`): **28/28 phép kiểm tra đạt** sau khi sửa (tìm không dấu, hoa/thường, lỗi tự kết bạn, gửi/chấp nhận/hủy, 403 người không phải người nhận, 409 đã là bạn, theo dõi/bỏ theo dõi và cột đếm)
- [x] Kiểm chứng HTTP thật cho bài viết/thích/bình luận với **3 tài khoản** (`khoa.test`, `minh.test`, `stranger.test`): **32/35 đạt ngay lần đầu**, 3 lỗi còn lại là **lỗi trong script kiểm thử** (dùng nhầm token, một khẳng định sai logic) — chạy lại đúng thì đạt hết. Đã kiểm chứng: `Public/Friends/OnlyMe` lưu đúng và lọc đúng, bảng tin chỉ chứa bạn bè/đang theo dõi, thích idempotent + đếm, bình luận 1 cấp + đếm + xóa cascade mềm, chia sẻ + `share_count`, sửa/xóa sai người → 403
- [x] Kiểm chứng HTTP thật cho media: upload ảnh PNG thật → đăng ký tài sản → gắn vào bài → **tải lại ảnh qua `/uploads/...` được `200 image/png`**, tệp có trên đĩa
- [x] Thử thủ công **3 tài khoản** trên giao diện web (`AGENTS.md` mục 16): `khoa.test` (đăng bài + bình luận), `minh.test` (bạn bè, bài chia sẻ), `stranger.test` (người lạ — không thấy bài `Friends`)

**Tổng số test hiện tại: 81** (Domain 24 · Application 15 · Tích hợp 42), tất cả pass.

### 4.8. Tài liệu 🚧

- [x] Thêm mục 7 (endpoint + DTO + mã lỗi) vào `docs/05_api_contract.md` — mục 7.1–7.6 đều đã ghi, đánh dấu rõ phần nào đã cài đặt
- [x] Cập nhật `docs/03_database_design.md`: thêm `Local` vào enum `MediaProvider` (chỉ dùng khi dev)
- [x] Cập nhật `.env.example`: cấu hình media (Cloudinary khi có khoá, đĩa cục bộ khi để trống)
- [x] Cập nhật `docs/04_roadmap.md` (ghi chú trạng thái GĐ 2) khi hoàn tất
- [x] Cập nhật `README.md`: thêm mục **6b. Ảnh trong bài viết** (Cloudinary hoặc đĩa cục bộ, proxy `/uploads`, giới hạn ảnh)

---

## 5. Tiêu chí hoàn thành (lấy từ `docs/04_roadmap.md` Giai đoạn 2)

- [ ] Kết bạn hai chiều: gửi, chấp nhận, từ chối lời mời; xem danh sách bạn
- [ ] Theo dõi / bỏ theo dõi (một chiều)
- [ ] Tìm kiếm người dùng (có hỗ trợ gõ không dấu)
- [ ] Đăng, sửa, xóa bài viết; chọn phạm vi hiển thị (công khai/bạn bè/chỉ mình tôi)
- [ ] Đính kèm ảnh vào bài viết (tải lên cloud, hiển thị đúng)
- [ ] Bảng tin hiển thị bài của bạn bè và người theo dõi, phân trang cuộn
- [ ] Thích, bình luận (có trả lời) và chia sẻ bài viết
- [ ] Trang cá nhân hiển thị bài viết, số bạn bè/người theo dõi
- [ ] Quyền xem bài viết đúng (không xem được bài `Friends` của người lạ)
- [ ] Có test cho quyền xem bài và trạng thái kết bạn

Cộng thêm **Definition of Done** chung ở `docs/04_roadmap.md` mục 4 và `AGENTS.md` mục 16.

---

## 6. Rủi ro và lưu ý

| Rủi ro | Cách xử lý |
|---|---|
| Bảng tin bị N+1 (mỗi bài lại truy vấn tác giả, ảnh, lượt thích) | Projection thẳng ra DTO trong một truy vấn; kiểm tra log SQL khi làm xong |
| Quyền xem bài bị rò rỉ qua đường khác (thích, bình luận, chia sẻ) | Đưa kiểm tra quyền vào **một** hàm dùng chung trong Application, test đủ ma trận 3×3 |
| Hai lời mời chéo nhau tạo hai dòng | Chỉ mục duy nhất `LEAST/GREATEST` là chốt chặn cuối; xử lý trong transaction + bắt `23505` |
| Cột đếm lệch với dữ liệu thật | Cập nhật trong cùng transaction; thêm truy vấn đối chiếu trong test tích hợp |
| Không có khoá Cloudinary | Xem mục 3.9 — chọn phương án trước khi bắt đầu 4.1 |
| Xung đột với Dev B (nhắn tin) | Không tạo migration khi Dev B đang tạo; báo nhau trước khi tạo M1 |
| Ảnh lớn làm chậm bảng tin | Giới hạn 5 MB/ảnh, dùng `thumbnailUrl` cho danh sách |
| Trùng lặp mã lỗi với GĐ 1 | Dùng đúng danh sách ở mục 3.8, không đặt mã mới tuỳ tiện |

---

## 7. Tài liệu liên quan

- `docs/04_roadmap.md` — Giai đoạn 2 và Definition of Done
- `docs/01_COMINK_SPEC.md` — mục 3.1 [CB], 3.4 [CB]
- `docs/02_tech_stack_decisions.md` — mục 3.1 (mạng xã hội), 3.4 (media), 4.1 (hợp đồng API trước), 4.3 (bảo mật)
- `docs/03_database_design.md` — mục 3.2, 3.5, 4 (enum), 5.2/5.3 (chỉ mục, truy vấn bảng tin), 6.3 (mốc M1)
- `docs/05_api_contract.md` — hợp đồng API (bổ sung mục 3 sau khi duyệt)
- `docs/tasks/phase1_foundation_auth.md` — tiền đề
