/**
 * Mã hoá con trỏ phân trang giống `CursorCodec` ở backend: base64(`createdAt|id`).
 *
 * **Giữ nguyên chuỗi thời gian server trả về** (ví dụ `2026-10-19T22:15:05.123456+00:00`)
 * thay vì đưa qua `new Date().toISOString()` — hàm đó cắt về mili giây, làm con trỏ "cũ hơn"
 * thực tế nên tin cuối bị trả lại thêm một lần khi đồng bộ `after`.
 * Backend giải mã bằng `DateTimeOffset.TryParse` với InvariantCulture nên nhận trực tiếp.
 */
export function encodeCursor(createdAtIso: string, id: string): string {
  return btoa(`${createdAtIso}|${id}`)
}
