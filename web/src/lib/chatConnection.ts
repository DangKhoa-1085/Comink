/**
 * Kết nối SignalR tới hub `/hub/chat`.
 *
 * - Xác thực bằng `accessTokenFactory` (SignalR gửi token qua query string `access_token`).
 * - Tự kết nối lại; khi có lại, component gọi API để đồng bộ tin bị lỡ
 *   (`docs/02_tech_stack_decisions.md` mục 5.4).
 * - Group hội thoại do **server** tự thêm khi kết nối — client không tự join.
 */
import {
  HubConnectionBuilder,
  LogLevel,
  type HubConnection,
} from '@microsoft/signalr'
import { session } from './session'

/** Tên sự kiện — phải khớp hằng số `ChatEvents.MessageCreated` ở backend. */
export const MESSAGE_CREATED_EVENT = 'MessageCreated'

export function createChatConnection(): HubConnection {
  return new HubConnectionBuilder()
    .withUrl('/hub/chat', {
      accessTokenFactory: () => session.getAccessToken() ?? '',
    })
    .withAutomaticReconnect()
    .configureLogging(LogLevel.Warning)
    .build()
}
