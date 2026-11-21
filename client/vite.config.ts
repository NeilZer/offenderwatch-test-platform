import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

/**
 * Dev: Vite on :5173 proxies API, SignalR and Swagger calls to the ASP.NET Core API (same origin →
 * the session cookie just works). Build: output goes to the API's wwwroot so ONE process serves both.
 * The API address comes from the environment (PLATFORM_API), defaulting to the local dev port.
 */
const api = process.env.PLATFORM_API ?? 'http://localhost:5080';

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      '/api': { target: api, changeOrigin: true },
      '/hubs': { target: api, changeOrigin: true, ws: true },
      '/swagger': { target: api, changeOrigin: true },
    },
  },
  build: {
    outDir: '../server/src/TestPlatform.Api/wwwroot',
    emptyOutDir: true,
  },
});
