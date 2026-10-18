/** Gọi API bình luận — `/api/posts/{id}/comments`, `/api/comments/{id}` (docs/05_api_contract.md mục 7.5). */
import { http } from '../lib/http'
import type { Comment, PagedResult } from './types'

export const commentsApi = {
  async create(postId: string, content: string, parentCommentId?: string | null): Promise<Comment> {
    const { data } = await http.post<Comment>(`/api/posts/${postId}/comments`, {
      content,
      parentCommentId: parentCommentId ?? null,
    })
    return data
  },

  async list(postId: string, cursor?: string | null): Promise<PagedResult<Comment>> {
    const { data } = await http.get<PagedResult<Comment>>(`/api/posts/${postId}/comments`, {
      params: cursor ? { cursor } : undefined,
    })
    return data
  },

  async replies(commentId: string, cursor?: string | null): Promise<PagedResult<Comment>> {
    const { data } = await http.get<PagedResult<Comment>>(`/api/comments/${commentId}/replies`, {
      params: cursor ? { cursor } : undefined,
    })
    return data
  },

  async update(commentId: string, content: string): Promise<Comment> {
    const { data } = await http.patch<Comment>(`/api/comments/${commentId}`, { content })
    return data
  },

  async remove(commentId: string): Promise<void> {
    await http.delete(`/api/comments/${commentId}`)
  },
}
