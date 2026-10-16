/**
 * Dịch mã lỗi và lỗi validate từ backend sang chuỗi hiển thị.
 * Backend chỉ trả mã lỗi (không trả câu chữ) — client tự dịch (AGENTS.md mục 3).
 */
import type { TFunction } from 'i18next'

/** Dịch mã lỗi; mã không xác định thì dùng thông báo chung. */
export function translateErrorCode(t: TFunction, code: string | null | undefined): string {
  if (!code) {
    return ''
  }
  // `t` trả về chính key khi thiếu bản dịch → dùng thông báo chung thay vì hiện mã thô.
  const translated = t(`errors.${code}`, { defaultValue: '' })
  return translated || t('errors.INTERNAL_ERROR')
}
