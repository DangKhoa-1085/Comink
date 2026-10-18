/** Gọi API bài viết, bảng tin và thích — `/api/posts`, `/api/feed` (docs/05_api_contract.md mục 7.4–7.5). */
import { http } from '../lib/http'
import type { CreatePostPayload, PagedResult, Post, UpdatePostPayload } from './types'

export const postsApi = {
  async feed(cursor?: string | null): Promise<PagedResult<Post>> {
    const { data } = await http.get<PagedResult<Post>>('/api/feed', {
      params: cursor ? { cursor } : undefined,
    })
    return data
  },

  async userPosts(userId: string, cursor?: string | null): Promise<PagedResult<Post>> {
    const { data } = await http.get<PagedResult<Post>>(`/api/users/${userId}/posts`, {
      params: cursor ? { cursor } : undefined,
    })
    return data
  },

  async get(postId: string): Promise<Post> {
    const { data } = await http.get<Post>(`/api/posts/${postId}`)
    return data
  },

  async create(payload: CreatePostPayload): Promise<Post> {
    const { data } = await http.post<Post>('/api/posts', payload)
    return data
  },

  async update(postId: string, payload: UpdatePostPayload): Promise<Post> {
    const { data } = await http.patch<Post>(`/api/posts/${postId}`, payload)
    return data
  },

  async remove(postId: string): Promise<void> {
    await http.delete(`/api/posts/${postId}`)
  },

  async like(postId: string): Promise<void> {
    await http.post(`/api/posts/${postId}/likes`)
  },

  async unlike(postId: string): Promise<void> {
    await http.delete(`/api/posts/${postId}/likes`)
  },
}
