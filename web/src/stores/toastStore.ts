/**
 * Thông báo nhanh (toast) tự viết — không thêm thư viện ngoài.
 * Chỉ chứa trạng thái UI nên dùng Zustand là phù hợp (AGENTS.md mục 8.3).
 */
import { create } from 'zustand'

export type ToastVariant = 'success' | 'error' | 'info'

export interface Toast {
  id: number
  variant: ToastVariant
  message: string
}

interface ToastState {
  toasts: Toast[]
  push: (variant: ToastVariant, message: string) => void
  dismiss: (id: number) => void
}

let nextId = 1
/** Thời gian tự ẩn của mỗi thông báo (ms). */
const AUTO_DISMISS_MS = 4000

export const useToastStore = create<ToastState>((set, get) => ({
  toasts: [],

  push(variant, message) {
    const id = nextId++
    set({ toasts: [...get().toasts, { id, variant, message }] })
    window.setTimeout(() => get().dismiss(id), AUTO_DISMISS_MS)
  },

  dismiss(id) {
    set({ toasts: get().toasts.filter((toast) => toast.id !== id) })
  },
}))

/** Tiện ích gọi thông báo ngoài component. */
export const toast = {
  success: (message: string) => useToastStore.getState().push('success', message),
  error: (message: string) => useToastStore.getState().push('error', message),
  info: (message: string) => useToastStore.getState().push('info', message),
}
