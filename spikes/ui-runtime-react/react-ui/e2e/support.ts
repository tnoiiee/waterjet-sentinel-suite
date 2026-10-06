// WJSS Stage 0.2.1A — Owner-local e2e helpers (synthetic scenario commands, diagnostics).
import type { APIRequestContext, Page } from '@playwright/test';

export const TOKEN = process.env.WJSS_SPIKE_TOKEN ?? '';

export async function scenario(request: APIRequestContext, command: string, params?: Record<string, unknown>) {
  const res = await request.post('/api/spike/scenario', { headers: { 'x-spike-token': TOKEN }, data: { command, params } });
  return res.json();
}

export async function diag(page: Page) {
  return page.evaluate(() => (window as unknown as { __WJSS_SPIKE_DIAG__: () => Record<string, number | string | null> }).__WJSS_SPIKE_DIAG__());
}

export async function metrics(request: APIRequestContext) {
  return (await request.get('/api/spike/metrics')).json();
}

/** Records every request host; the page must only talk to the loopback harness. */
export function trackHosts(page: Page): Set<string> {
  const hosts = new Set<string>();
  page.on('request', (r) => {
    const u = new URL(r.url());
    if (u.protocol.startsWith('http')) hosts.add(u.host);
  });
  return hosts;
}
