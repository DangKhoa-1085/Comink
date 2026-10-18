/**
 * Kiểu dữ liệu của API, khớp `docs/05_api_contract.md`.
 * Không dùng `any` — mọi phản hồi đều có kiểu tường minh.
 */

/** Phản hồi ProblemDetails có thêm mã lỗi (RFC 7807 + `code`). */
export interface ProblemDetails {
  type?: string
  title?: string
  status?: number
  code?: string
  traceId?: string
  errors?: Record<string, string[]>
}

/** Người dùng trả kèm kết quả xác thực. */
export interface AuthUser {
  id: string
  userName: string
  email: string
  displayName: string
  roles: string[]
}

/** Kết quả đăng ký / đăng nhập / làm mới token. */
export interface AuthResponse {
  accessToken: string
  accessTokenExpiresAt: string
  refreshToken: string
  refreshTokenExpiresAt: string
  user: AuthUser
}

/** Hồ sơ đầy đủ của chính người dùng đang đăng nhập. */
export interface MyProfile {
  id: string
  userName: string
  email: string
  displayName: string
  bio: string | null
  avatarUrl: string | null
  coverUrl: string | null
  location: string | null
  dateOfBirth: string | null
  followerCount: number
  followingCount: number
  friendCount: number
  language: 'vi' | 'en'
  roles: string[]
  createdAt: string
}

/** Một dòng trong danh sách tài khoản của trang quản trị. */
export interface AdminUserSummary {
  id: string
  userName: string
  email: string
  displayName: string
  accountStatus: string
  createdAt: string
}

/** Kết quả phân trang theo con trỏ. */
export interface PagedResult<T> {
  items: T[]
  nextCursor: string | null
  hasMore: boolean
}

// --- Yêu cầu gửi lên ---

export interface RegisterPayload {
  userName: string
  email: string
  password: string
  displayName: string
}

export interface LoginPayload {
  emailOrUserName: string
  password: string
  deviceName?: string
}

export interface UpdateProfilePayload {
  displayName?: string
  bio?: string
  location?: string
  dateOfBirth?: string
}

export interface UpdateSettingsPayload {
  language?: 'vi' | 'en'
}

// --- Giai đoạn 2: mạng xã hội (docs/05_api_contract.md mục 7) ---

/** Quan hệ giữa người xem và một người dùng khác. */
export interface Relationship {
  friendship: 'None' | 'OutgoingPending' | 'IncomingPending' | 'Friends'
  following: boolean
  followedBy: boolean
}

/** Thông tin rút gọn của một người dùng. */
export interface UserSummary {
  id: string
  userName: string
  displayName: string
  avatarUrl: string | null
  bio: string | null
  relationship: Relationship
}

/** Hồ sơ công khai của một người dùng. */
export interface PublicProfile {
  id: string
  userName: string
  displayName: string
  bio: string | null
  avatarUrl: string | null
  coverUrl: string | null
  location: string | null
  followerCount: number
  followingCount: number
  friendCount: number
  createdAt: string
  relationship: Relationship
}

/** Một dòng quan hệ bạn bè. */
export interface Friendship {
  id: string
  user: UserSummary
  status: 'Pending' | 'Accepted'
  createdAt: string
  respondedAt: string | null
}

/** Phạm vi hiển thị của bài viết. */
export type Visibility = 'Public' | 'Friends' | 'OnlyMe'

/** Ảnh đính kèm. */
export interface MediaItem {
  id: string
  url: string
  thumbnailUrl: string | null
  width: number | null
  height: number | null
}

/** Bài viết. */
export interface Post {
  id: string
  author: UserSummary
  content: string | null
  visibility: Visibility
  media: MediaItem[]
  sharedPost: Post | null
  likeCount: number
  commentCount: number
  shareCount: number
  likedByMe: boolean
  isMine: boolean
  createdAt: string
  updatedAt: string | null
}

/** Bình luận. */
export interface Comment {
  id: string
  postId: string
  parentCommentId: string | null
  author: UserSummary
  content: string
  replyCount: number
  createdAt: string
  updatedAt: string | null
}

export interface CreatePostPayload {
  content?: string | null
  visibility?: Visibility
  mediaIds?: string[]
  sharedPostId?: string | null
}

export interface UpdatePostPayload {
  content?: string | null
  visibility?: Visibility
}

/** Thông tin upload do backend cấp. */
export interface MediaSignature {
  provider: 'Cloudinary' | 'Local'
  uploadUrl: string
  signature: string
  apiKey: string
  cloudName: string
  timestamp: number
  uploadPreset: string
  folder: string
}

/** Kết quả tải tệp lên backend (khi dev dùng đĩa cục bộ). */
export interface LocalUploadResult {
  provider: string
  providerPublicId: string
  url: string
  mimeType: string
  sizeBytes: number
}

/** Tài sản media đã đăng ký. */
export interface MediaAsset {
  id: string
  url: string
  thumbnailUrl: string | null
  width: number | null
  height: number | null
}

export interface RegisterMediaPayload {
  providerPublicId: string
  url: string
  thumbnailUrl?: string | null
  mimeType: string
  sizeBytes: number
  width?: number | null
  height?: number | null
  context: 'Post' | 'Message' | 'Avatar' | 'Cover' | 'Album'
}
