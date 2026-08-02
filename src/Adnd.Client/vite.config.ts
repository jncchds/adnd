import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import { readFileSync } from 'fs'
import { resolve } from 'path'

// Falls back rather than throwing at config-load time. A missing VERSION file used to
// fail the Docker build with a stack trace that gave no hint about the real cause.
function readVersion(): string {
  try {
    return readFileSync(resolve(__dirname, '../../VERSION'), 'utf8').trim()
  } catch {
    console.warn('[vite] VERSION file not found; falling back to 0.0.0-dev')
    return '0.0.0-dev'
  }
}

// The release notes page is a static render of the repo's own RELEASE_NOTES.md, baked in at
// build time. Serving it from the API instead would mean shipping a repo file into the
// container and an endpoint that has to be anonymous — the page is public.
function readReleaseNotes(): string {
  try {
    return readFileSync(resolve(__dirname, '../../RELEASE_NOTES.md'), 'utf8')
  } catch {
    console.warn('[vite] RELEASE_NOTES.md not found; release notes page will be empty')
    return ''
  }
}

const version = readVersion()
const releaseNotes = readReleaseNotes()

export default defineConfig({
  plugins: [react()],
  define: {
    __APP_VERSION__: JSON.stringify(version),
    __RELEASE_NOTES__: JSON.stringify(releaseNotes),
  },
  build: {
    outDir: '../Adnd.Server/wwwroot',
    emptyOutDir: true,
    rollupOptions: {
      output: {
        manualChunks: {
          vendor: ['react', 'react-dom'],
          mui: ['@mui/material', '@mui/icons-material'],
          signalr: ['@microsoft/signalr'],
        },
      },
    },
  },
  server: {
    port: 3000,
    proxy: {
      '/api': 'http://localhost:5010',
      '/gamehub': {
        target: 'http://localhost:5010',
        ws: true,
        changeOrigin: true,
      },
    },
  },
})
