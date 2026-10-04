// WJSS Stage 0.2.1A — Vitest config (jsdom). Arena has no browser: jsdom results are not
// browser rendering results.
import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  server: { fs: { allow: ['..'] } },
  test: {
    environment: 'jsdom',
    include: ['test/**/*.test.{ts,tsx,mjs}'],
    setupFiles: ['test/setup.ts'],
    testTimeout: 30000,
    css: { modules: { classNameStrategy: 'non-scoped' } },
  },
});
