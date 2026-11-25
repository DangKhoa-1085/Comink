/** Trạng thái cuộc gọi đến toàn cục (Zustand — chỉ UI state, dữ liệu server vẫn qua TanStack Query). */
import { create } from 'zustand'
import type { CallDto } from '../api/calls'

interface CallState {
  /** Cuộc gọi đang đổ chuông chờ tôi trả lời (null nếu không có). */
  incoming: CallDto | null
  setIncoming: (call: CallDto | null) => void
  clearIncoming: () => void
}

export const useCallStore = create<CallState>((set) => ({
  incoming: null,
  setIncoming: (call) => set({ incoming: call }),
  clearIncoming: () => set({ incoming: null }),
}))
