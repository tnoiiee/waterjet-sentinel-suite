// WJSS Stage 0.2.1A — jsdom test setup. jsdom has no matchMedia (uPlot reads it at import).
// Test-only polyfill; it does not exist in the production bundle.
if (typeof window !== 'undefined' && typeof window.matchMedia !== 'function') {
  window.matchMedia = ((query: string) => ({
    matches: false,
    media: query,
    onchange: null,
    addListener: () => {},
    removeListener: () => {},
    addEventListener: () => {},
    removeEventListener: () => {},
    dispatchEvent: () => false,
  })) as unknown as typeof window.matchMedia;
}

// Vitest globals are off, so Testing Library's auto-cleanup is registered explicitly.
import { afterEach } from 'vitest';
import { cleanup } from '@testing-library/react';
afterEach(() => cleanup());
