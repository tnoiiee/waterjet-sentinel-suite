// WJSS Stage 0.2.1A — bounded wait for the self-hosted UI font (Google Sans, local WOFF2).
// Never blocks indefinitely: after `timeoutMs` the app renders with the fallback stack
// ("Segoe UI", system-ui, sans-serif). No network access other than the same-origin asset.

export const UI_FONT_FAMILY = 'Google Sans';
/** Weights used by the UI; the variable font covers 400..700. */
export const UI_FONT_FACES = [`400 13px "${UI_FONT_FAMILY}"`, `700 13px "${UI_FONT_FAMILY}"`];

export async function waitForUiFont(timeoutMs = 2500, fonts: FontFaceSet | undefined = typeof document !== 'undefined' ? document.fonts : undefined): Promise<boolean> {
  if (!fonts || typeof fonts.load !== 'function') return false;
  let timer: ReturnType<typeof setTimeout> | undefined;
  const timeout = new Promise<boolean>((resolve) => {
    timer = setTimeout(() => resolve(false), timeoutMs);
  });
  const load = Promise.all(UI_FONT_FACES.map((f) => fonts.load(f)))
    .then(() => fonts.check(UI_FONT_FACES[1]))
    .catch(() => false);
  try {
    return await Promise.race([load, timeout]);
  } finally {
    clearTimeout(timer);
  }
}
