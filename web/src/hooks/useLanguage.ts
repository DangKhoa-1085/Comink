/**
 * Chuyển ngôn ngữ giao diện (vi/en) và đồng bộ với cài đặt của người dùng ở backend.
 */
import { useTranslation } from 'react-i18next'
import { useMutation } from '@tanstack/react-query'
import { usersApi } from '../api/users'
import { useAuthStore } from '../stores/authStore'

export type SupportedLanguage = 'vi' | 'en'

export function useLanguage() {
  const { i18n } = useTranslation()
  const user = useAuthStore((state) => state.user)
  const setUser = useAuthStore((state) => state.setUser)

  const current: SupportedLanguage = i18n.language.startsWith('en') ? 'en' : 'vi'

  const mutation = useMutation({
    mutationFn: (language: SupportedLanguage) => usersApi.updateSettings({ language }),
    onSuccess: (profile) => {
      // Giữ thông tin người dùng trong store khớp với backend.
      if (user) {
        setUser({ ...user, displayName: profile.displayName })
      }
    },
  })

  const changeLanguage = (language: SupportedLanguage) => {
    void i18n.changeLanguage(language)
    // Chỉ lưu vào backend khi đã đăng nhập; lỗi không được chặn việc đổi giao diện.
    if (user) {
      mutation.mutate(language)
    }
  }

  const toggleLanguage = () => {
    changeLanguage(current === 'vi' ? 'en' : 'vi')
  }

  return { current, changeLanguage, toggleLanguage, isSaving: mutation.isPending }
}
