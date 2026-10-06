// WJSS Stage 0.2.1A — UI-initiated synthetic requests.
// Each request is a single explicit user action: no queue, no retry, never replayed after
// reconnect. Relative URLs only (same origin as the harness).

import type { CloseRequestEvaluation } from '../../../contracts/operational';

export async function requestClose(fetchImpl: typeof fetch = fetch): Promise<CloseRequestEvaluation> {
  const res = await fetchImpl('/api/spike/close-request', { method: 'POST' });
  if (!res.ok) throw new Error(`close-request failed: ${res.status}`);
  return (await res.json()) as CloseRequestEvaluation;
}

// ---------------------------------------------------------------------------------------------
// Synthetic test control (spike Owner review tooling). Available only when the harness runs with
// --synthetic-test-controls. The per-run token is held in memory by the caller (never persisted,
// cleared on disconnect / reload). One explicit request per click: no queue, no retry, no replay.
// This is NOT an authentication model and is not part of any Production build decision.

export type TestControlsInfo = { enabled: true; token: string } | { enabled: false };

export async function fetchTestControls(fetchImpl: typeof fetch = fetch): Promise<TestControlsInfo> {
  const res = await fetchImpl('/api/spike/test-controls', { method: 'GET', cache: 'no-store', credentials: 'same-origin' });
  if (res.status === 404) return { enabled: false };
  if (!res.ok) throw new Error(`test-controls failed: ${res.status}`);
  const body = (await res.json()) as { enabled?: boolean; token?: string };
  return body.enabled && typeof body.token === 'string' ? { enabled: true, token: body.token } : { enabled: false };
}

export interface ScenarioResult {
  command: string;
  accepted: boolean;
  reason?: string | null;
  detail?: Record<string, unknown>;
}

export async function sendSyntheticScenario(token: string, command: string, params: Record<string, unknown>, fetchImpl: typeof fetch = fetch): Promise<ScenarioResult> {
  const res = await fetchImpl('/api/spike/scenario', {
    method: 'POST',
    headers: { 'content-type': 'application/json', 'x-spike-token': token },
    body: JSON.stringify({ command, params }),
    cache: 'no-store',
  });
  const body = (await res.json().catch(() => ({}))) as Partial<ScenarioResult>;
  return { command, accepted: res.ok && body.accepted === true, reason: body.reason ?? (res.ok ? null : `HTTP_${res.status}`), detail: body.detail };
}
