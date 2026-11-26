/**
 * Giao diện sáng/tối, ghi nhớ lựa chọn của người dùng và đồng bộ giữa mọi component.
 */
import { useEffect, useState } from 'react'

const STORAGE_KEY = 'comink.theme'
const THEME_EVENT = 'comink:theme-change'

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

  useEffect(() => {
    const onThemeSync = (event: Event) => {
      const next = (event as CustomEvent<boolean>).detail
      setIsDark(next)
    }
    window.addEventListener(THEME_EVENT, onThemeSync)
    return () => window.removeEventListener(THEME_EVENT, onThemeSync)
  }, [])

  const setTheme = (next: boolean) => {
    setIsDark(next)
    window.dispatchEvent(new CustomEvent<boolean>(THEME_EVENT, { detail: next }))
  }

  return { isDark, toggleTheme: () => setTheme(!isDark), setTheme }
}
