/**
 * Giao diện sáng/tối, ghi nhớ lựa chọn của người dùng.
 */
import { useEffect, useState } from 'react'

const STORAGE_KEY = 'comink.theme'

function readInitialTheme(): boolean {
  const stored = localStorage.getItem(STORAGE_KEY)
  if (stored === 'dark') return true
  if (stored === 'light') return false
  // Chưa chọn lần nào → theo cài đặt hệ điều hành.
  return window.matchMedia('(prefers-color-scheme: dark)').matches
}

export function useTheme() {
  const [isDark, setIsDark] = useState(readInitialTheme)

  useEffect(() => {
    document.documentElement.classList.toggle('dark', isDark)
    localStorage.setItem(STORAGE_KEY, isDark ? 'dark' : 'light')
  }, [isDark])

  return { isDark, toggleTheme: () => setIsDark((value) => !value) }
}
