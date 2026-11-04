import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

const backendTarget = process.env.VITE_BACKEND_TARGET || 'https://localhost:7072';

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    strictPort: true,
    proxy: {
      '/api': {
        target: backendTarget,
        changeOrigin: false,
        secure: false,
      },
      '/hubs': {
        target: backendTarget,
        ws: true,
        changeOrigin: false,
        secure: false,
      },
    },
  },
});
