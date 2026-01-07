import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// https://vitejs.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    port: parseInt(process.env.PORT || '5173'),
    host: '0.0.0.0', // Allow external connections (needed for Aspire)
    proxy: {
      '/api': {
        // Aspire sets service__api__http__0 or services__api__https__0
        target: process.env.services__api__https__0 || process.env.services__api__http__0 || 'http://localhost:5000',
        changeOrigin: true,
        secure: false,
      },
    },
  },
})
