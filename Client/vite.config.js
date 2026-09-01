import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import { VitePWA } from 'vite-plugin-pwa'
import { execSync } from 'child_process'

const formatDate = (date) => {
  const d = new Date(date)
  const day = String(d.getDate()).padStart(2, '0')
  const month = String(d.getMonth() + 1).padStart(2, '0')
  const year = d.getFullYear()
  return `${day}.${month}.${year}`
}

export default defineConfig({
  plugins: [
    react(),
    VitePWA({
      registerType: 'autoUpdate',
      injectRegister: null,
      updateViaCache: 'none',
      workbox: {
        skipWaiting: true,
        clientsClaim: true,
        cleanupOutdatedCaches: true,
        navigateFallback: null,
        navigateFallbackDenylist: [
          /^\/stream\/.*/,
          /^\/api\/.*/,
        ],
        runtimeCaching: [
            {
                urlPattern: /\/stream\//,
                handler: 'NetworkOnly',
                method: 'GET',
                options: {
                    plugins: [
                        {
                            handlerWillRespond: async ({ response }) => {
                                return response || null;
                            }
                        }
                    ]
                }
            }
        ]
      },
      devOptions: {
        enabled: true
      },
      manifest: {
        name: 'o0o0.online',
        short_name: 'o0o0.online',
        description: 'o0o0.online',
        theme_color: '#dad0be',
        background_color: '#dad0be',
        display: 'standalone',
        start_url: '/#/',
        icons: [
          {
            src: '/icon.svg',
            sizes: 'any',
            type: 'image/svg+xml',
            purpose: 'any'
          },
          {
            src: '/icon-192x192.png',
            sizes: '192x192',
            type: 'image/png'
          },
          {
            src: '/icon-512x512.png',
            sizes: '512x512',
            type: 'image/png'
          }
        ]
      }
    })
  ],
  define: {
    'process.env.GIT_COMMIT': JSON.stringify(
      execSync('git rev-parse --short HEAD').toString().trim()
    ),
    'process.env.GIT_DATE': JSON.stringify(
      formatDate(execSync('git log -1 --format=%cd').toString().trim())
    )
  }
})
