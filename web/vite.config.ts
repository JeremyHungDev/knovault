import { fileURLToPath, URL } from 'node:url'
import { defineConfig } from 'vite'
import vue from '@vitejs/plugin-vue'
import { VitePWA } from 'vite-plugin-pwa'

// Knovault Vue SPA。打包輸出進 .NET 的 wwwroot，由 Kestrel 託管。
export default defineConfig({
  plugins: [
    vue(),
    VitePWA({
      registerType: 'autoUpdate',
      includeAssets: ['favicon.svg'],
      manifest: {
        name: 'Knovault 芝士庫',
        short_name: 'Knovault',
        description: '自託管個人書庫：數位 + 實體書整合管理',
        theme_color: '#18a058',
        background_color: '#101014',
        display: 'standalone',
        lang: 'zh-Hant',
        icons: [
          {
            src: 'favicon.svg',
            sizes: 'any',
            type: 'image/svg+xml',
            purpose: 'any maskable',
          },
        ],
      },
      workbox: {
        // 不要把 API 請求快取成離線；只快取 SPA 靜態資產。
        navigateFallbackDenylist: [/^\/api\//],
        globPatterns: ['**/*.{js,css,html,svg,png,ico,woff,woff2}'],
        // 新版立即接管，避免使用者看到舊的快取版本（自託管更新後立刻生效）。
        skipWaiting: true,
        clientsClaim: true,
      },
      // 開發時不啟用 SW，避免干擾 proxy。
      devOptions: { enabled: false },
    }),
  ],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
    },
  },
  build: {
    // 由 .NET Kestrel 託管：打包進 Api 專案的 wwwroot。
    outDir: '../src/Knovault.Api/wwwroot',
    emptyOutDir: true,
  },
  server: {
    port: 5173,
    proxy: {
      // 開發時把 /api 轉給後端 Kestrel（偏好埠 5279）。
      '/api': {
        target: 'http://localhost:5279',
        changeOrigin: true,
      },
    },
  },
})
