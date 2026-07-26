import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import { readFileSync } from 'fs'
import { resolve } from 'path'

const appVersion = readFileSync(resolve(__dirname, '../../VERSION'), 'utf-8').trim()

export default defineConfig({
  plugins: [react()],
  define: {
    __APP_VERSION__: JSON.stringify(appVersion),
  },
  build: {
    outDir: '../Adnd.Server/wwwroot',
    emptyOutDir: true,
    rollupOptions: {
      output: {
        manualChunks: {
          vendor: ['react', 'react-dom'],
          mui: ['@mui/material'],
          signalr: ['@microsoft/signalr'],
        },
      },
    },
  },
  server: {
    port: 3000,
    proxy: {
      '/api': {
        target: 'http://localhost:5010',
        secure: false,
      },
      '/gamehub': {
        target: 'http://localhost:5010',
        ws: true,
      }
    }
  }
})
