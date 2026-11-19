/**
 * Danh mục cảm xúc trên web — PHẢI khớp `ReactionCatalog` ở backend
 * (`backend/src/Comink.Domain/Messaging/ReactionCatalog.cs`). API trao đổi bằng khoá chữ,
 * emoji chỉ dùng để hiển thị.
 */
export const REACTIONS = [
  { key: 'like', emoji: '👍' },
  { key: 'love', emoji: '❤️' },
  { key: 'haha', emoji: '😂' },
  { key: 'wow', emoji: '😮' },
  { key: 'sad', emoji: '😢' },
  { key: 'angry', emoji: '😡' },
] as const

export type ReactionKey = (typeof REACTIONS)[number]['key']
