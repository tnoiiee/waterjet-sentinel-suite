// WJSS Stage 0.2.1A — Vite config (synthetic spike). Dev server and preview bind to loopback.
import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  base: '/',
  server: {
    host: '127.0.0.1',
    port: 5173,
    strictPort: true,
    fs: { allow: ['..'] },
    proxy: { '/api': 'http://127.0.0.1:5181', '/healthz': 'http://127.0.0.1:5181' },
  },
  preview: { host: '127.0.0.1' },
  build: {
    outDir: 'dist',
    target: 'es2022',
    sourcemap: false,
    reportCompressedSize: true,
  },
});
