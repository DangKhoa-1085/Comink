import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { QueryClientProvider } from '@tanstack/react-query'
import { queryClient } from './lib/queryClient'
import './lib/i18n'
import './index.css'
import App from './App'

// Áp dụng giao diện đã lưu TRƯỚC khi React render để không bị "nháy" sai màu.
// Cùng khoá và cùng quy tắc với `hooks/useTheme.ts`.
const storedTheme = localStorage.getItem('comink.theme')
const prefersDark = window.matchMedia('(prefers-color-scheme: dark)').matches
document.documentElement.classList.toggle(
  'dark',
  storedTheme === 'dark' || (storedTheme !== 'light' && prefersDark),
)

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <QueryClientProvider client={queryClient}>
      <App />
    </QueryClientProvider>
  </StrictMode>,
)
