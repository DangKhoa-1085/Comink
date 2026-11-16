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
import { session } from './session'

/** Tên sự kiện — phải khớp hằng số `ChatEvents` ở backend. */
export const MESSAGE_CREATED_EVENT = 'MessageCreated'
export const PRESENCE_CHANGED_EVENT = 'PresenceChanged'
export const TYPING_CHANGED_EVENT = 'TypingChanged'
export const MESSAGES_READ_EVENT = 'MessagesRead'

export function createChatConnection(): HubConnection {
  return new HubConnectionBuilder()
    .withUrl('/hub/chat', {
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
