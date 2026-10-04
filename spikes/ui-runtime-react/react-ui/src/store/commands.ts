// WJSS Stage 0.2.1A — UI-initiated synthetic requests.
// Each request is a single explicit user action: no queue, no retry, never replayed after
// reconnect. Relative URLs only (same origin as the harness).

import type { CloseRequestEvaluation } from '../../../contracts/operational';

export async function requestClose(fetchImpl: typeof fetch = fetch): Promise<CloseRequestEvaluation> {
  const res = await fetchImpl('/api/spike/close-request', { method: 'POST' });
  if (!res.ok) throw new Error(`close-request failed: ${res.status}`);
  return (await res.json()) as CloseRequestEvaluation;
}
