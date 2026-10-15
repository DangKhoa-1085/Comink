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
    },
  },
})
