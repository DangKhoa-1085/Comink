/**
 * Kết nối SignalR tới hub `/hub/chat`.
 *
 * - Xác thực bằng `accessTokenFactory` (SignalR gửi token qua query string `access_token`).
 * - Tự kết nối lại; khi có lại, component gọi API để đồng bộ tin bị lỡ
 *   (`docs/02_tech_stack_decisions.md` mục 5.4).
 * - Group hội thoại do **server** tự thêm khi kết nối — client không tự join.
 * - Tín hiệu nhẹ ("đang nhập", ping presence) đi thẳng qua hub (NC GĐ 7).
 */
import {
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
  type HubConnection,
} from '@microsoft/signalr'
import { API_BASE_URL } from '../config/env'
import { session } from './session'

/** Tên sự kiện — phải khớp hằng số `ChatEvents` ở backend. */
export const MESSAGE_CREATED_EVENT = 'MessageCreated'
export const PRESENCE_CHANGED_EVENT = 'PresenceChanged'
export const TYPING_CHANGED_EVENT = 'TypingChanged'
export const MESSAGES_READ_EVENT = 'MessagesRead'
/** Tin vừa bị sửa/thu hồi/đổi cảm xúc (NC Ưu tiên 2). */
export const MESSAGE_UPDATED_EVENT = 'MessageUpdated'
/** Danh sách ghim của hội thoại vừa thay đổi (NC Ưu tiên 2). */
export const PINNED_MESSAGES_CHANGED_EVENT = 'PinnedMessagesChanged'
/** Sự kiện Comink Locket: có ảnh mới, cập nhật cảm xúc hoặc bị xóa sớm. */
export const LOCKET_CREATED_EVENT = 'LocketCreated'
export const LOCKET_UPDATED_EVENT = 'LocketUpdated'
export const LOCKET_DELETED_EVENT = 'LocketDeleted'
/** Sự kiện hộp thư thông báo và thay đổi quan hệ bạn bè theo thời gian thực. */
export const NOTIFICATION_CREATED_EVENT = 'NotificationCreated'
export const FRIENDSHIP_CHANGED_EVENT = 'FriendshipChanged'
export function createChatConnection(): HubConnection {
  return new HubConnectionBuilder()
    .withUrl(`${API_BASE_URL}/hub/chat`, {
      accessTokenFactory: () => session.getAccessToken() ?? '',
    })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build()
}

/**
 * Kết nối đang hoạt động — chỉ có một cho cả ứng dụng (mở ở `AppLayout`).
 * Trang chat dùng tham chiếu này để gửi tín hiệu "đang nhập"; nếu chưa kết nối thì bỏ qua
 * (tín hiệu tạm thời, không được làm hỏng UI).
 */
let activeConnection: HubConnection | null = null

export function setActiveChatConnection(connection: HubConnection | null): void {
  activeConnection = connection
}

export function getActiveChatConnection(): HubConnection | null {
  return activeConnection
}

/** Gửi tín hiệu "đang nhập". Lỗi mạng bị nuốt im lặng — đây là tín hiệu không quan trọng. */
export async function sendTyping(conversationId: string, isTyping: boolean): Promise<void> {
  const connection = activeConnection
  if (!connection || connection.state !== HubConnectionState.Connected) {
    return
  }

  try {
    await connection.invoke('Typing', conversationId, isTyping)
  } catch {
    // Mất kết nối giữa chừng: bỏ qua, lần gõ sau sẽ gửi lại.
  }
}

/** Làm mới TTL presence — client gửi mỗi 30 giây (backend TTL 90 giây). */
export async function sendPresencePing(): Promise<void> {
  const connection = activeConnection
  if (!connection || connection.state !== HubConnectionState.Connected) {
    return
  }

  try {
    await connection.invoke('Ping')
  } catch {
    // Bỏ qua: SignalR sẽ tự kết nối lại và gửi ping tiếp theo.
  }
}
