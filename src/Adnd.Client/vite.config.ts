import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig({
  plugins: [react()],
  server: {
    port: 3000,
    proxy: {
      '/api': {
        target: 'https://localhost:5001',
        secure: false,
      },
      '/gamehub': {
        target: 'https://localhost:5001',
        ws: true,
      }
    }
  }
})
