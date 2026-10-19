/**
 * Mã hoá con trỏ phân trang giống `CursorCodec` ở backend: base64(`createdAt|id`).
 *
 * Backend giải mã bằng `DateTimeOffset.TryParse` nên chuỗi ISO-8601 của JavaScript
 * (`2026-10-19T22:16:18.844Z`) được chấp nhận — không cần đúng định dạng "O" của .NET.
 */
export function encodeCursor(createdAtIso: string, id: string): string {
  const raw = `${new Date(createdAtIso).toISOString()}|${id}`
  return btoa(raw)
}
