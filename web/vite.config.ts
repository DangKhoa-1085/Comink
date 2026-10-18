import path from 'path'
import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [
    react(),
    tailwindcss(),
  ],
  resolve: {
    alias: {
      '@': path.resolve(import.meta.dirname, './src'),
    },
  },
  server: {
    port: 5173,
    proxy: {
      // Backend chạy ở http://localhost:5117
      // (xem backend/src/Comink.Api/Properties/launchSettings.json)
      '/api': {
        target: 'http://localhost:5117',
        changeOrigin: true,
      },
      '/hub': {
        target: 'http://localhost:5117',
        ws: true,
      },
      '/health': {
        target: 'http://localhost:5117',
        changeOrigin: true,
      },
      // Ảnh lưu cục bộ khi dev (phương án B của GĐ 2 — backend phục vụ /uploads).
      // Thiếu mục này thì ảnh trong bài viết rơi vào SPA fallback và bị vỡ.
      '/uploads': {
        target: 'http://localhost:5117',
        changeOrigin: true,
      },
    },
  },
  build: {
    // Bundle hiện ~650 kB vì đã gồm chuỗi thư viện thời gian thực
    // (@microsoft/signalr, livekit-client, firebase). Với quy mô demo cục bộ thì
    // chấp nhận được; nâng ngưỡng cảnh báo để build không còn tiếng ồn.
    // Khi web phình to hơn, việc cần làm là tách chunk theo route (React.lazy).
    chunkSizeWarningLimit: 1000,
  },
})
