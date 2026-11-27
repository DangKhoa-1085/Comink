/**
 * Cấu hình môi trường.
 * - Dev local: để trống → gọi API và SignalR qua proxy của Vite (`/api`, `/hub/chat`).
 * - Production (Vercel): đặt `VITE_API_BASE_URL=https://<backend>.up.railway.app`.
 */
export const API_BASE_URL = (import.meta.env.VITE_API_BASE_URL ?? '').replace(/\/+$/, '')

/** Liên kết tải file cài đặt Android (.apk), ví dụ trỏ tới GitHub Releases. */
export const APK_DOWNLOAD_URL = (import.meta.env.VITE_APK_DOWNLOAD_URL ?? '').trim()
