import i18n from 'i18next'
import { initReactI18next } from 'react-i18next'
import LanguageDetector from 'i18next-browser-languagedetector'
import vi from '../locales/vi.json'
import en from '../locales/en.json'

/**
 * Cấu hình đa ngôn ngữ (vi, en) — `docs/01_COMINK_SPEC.md` mục 3.9.
 *
 * Chỉ hai ngôn ngữ này được hỗ trợ; khai báo `supportedLngs` để trình dò ngôn ngữ
 * không thử tải những ngôn ngữ khác (và để hành vi nhất quán khi chạy ngoài trình duyệt).
 */
i18n
  .use(LanguageDetector)
  .use(initReactI18next)
  .init({
    resources: {
      vi: { translation: vi },
      en: { translation: en },
    },
    supportedLngs: ['vi', 'en'],
    fallbackLng: 'vi',
    // `en-US` được hiểu là `en` thay vì bị coi là ngôn ngữ không hỗ trợ.
    nonExplicitSupportedLngs: true,
    load: 'languageOnly',
    detection: {
      order: ['localStorage', 'navigator'],
      caches: ['localStorage'],
      lookupLocalStorage: 'comink.language',
    },
    interpolation: {
      // React đã tự chống XSS khi render; không cần escape lần nữa.
      escapeValue: false,
    },
  })

export default i18n
