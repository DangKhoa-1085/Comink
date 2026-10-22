/**
 * Tiện ích LiveKit cho web.
 *
 * `?relay=1` trên URL buộc mọi media đi qua TURN (`iceTransportPolicy: 'relay'`) — dùng để
 * **chứng minh TURN hoạt động** (mục 5 roadmap GĐ 4). Không bật thì trình duyệt tự chọn
 * đường trực tiếp/TURN, phù hợp dùng bình thường.
 */
import { Room } from 'livekit-client'

/** Chế độ chỉ dùng TURN (ép relay) — bật bằng `?relay=1` trên URL. */
export const RELAY_ONLY =
  typeof window !== 'undefined' &&
  new URLSearchParams(window.location.search).get('relay') === '1'

export function createCallRoom(): Room {
  return new Room({
    adaptiveStream: true,
    dynacast: true,
    ...(RELAY_ONLY ? { rtcConfig: { iceTransportPolicy: 'relay' as const } } : {}),
  })
}

/**
 * Backend trả URL LiveKit theo cấu hình `LiveKit:Host` (đang là `localhost:7880`).
 * Khi web mở từ **máy khác** (`http://100.75.55.24:5173`), `localhost` sẽ trỏ vào chính máy đó
 * → thay bằng host của trang để trình duyệt nối đúng máy chủ LiveKit.
 * Chỉ đổi khi URL là localhost/127.0.0.1 nên không ảnh hưởng môi trường thật.
 *
 * Có thể **ép URL** khi cần debug đường mạng: `?livekit=ws://100.75.55.24:7880`.
 * (Ví dụ khi LiveKit nằm sau tunnel/Tailscale mà backend vẫn trả localhost.)
 */
export function normalizeLiveKitUrl(url: string): string {
  if (typeof window === 'undefined') {
    return url
  }

  const override = new URLSearchParams(window.location.search).get('livekit')
  if (override) {
    return override
  }

  const pageHost = window.location.hostname
  if (pageHost === 'localhost' || pageHost === '127.0.0.1') {
    return url
  }

  return url.replace('//localhost', `//${pageHost}`).replace('//127.0.0.1', `//${pageHost}`)
}
