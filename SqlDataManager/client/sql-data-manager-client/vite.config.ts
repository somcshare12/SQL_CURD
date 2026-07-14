import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    // Proxy API calls to the ASP.NET Core backend during development so the
    // frontend can use same-origin relative URLs (/api/...).
    proxy: {
      '/api': {
        target: process.env.VITE_API_PROXY_TARGET ?? 'http://localhost:8080',
        changeOrigin: true,
      },
    },
  },
  build: {
    // Emit the production build straight into the API's wwwroot so the backend
    // can serve the SPA in standalone/single-origin deployments.
    outDir: '../../src/SqlDataManager.Api/wwwroot',
    emptyOutDir: true,
  },
});
