// Stage Library Selection Gate - narrowly scoped boundary-scan S3 exception (Owner approved for this Stage).
//
// S3 forbids vendor/transport vocabulary in the Product tree. The only exception permitted is the
// approved vendor TCP client library vocabulary (assembled in the scanner as 'mo'+'db'+'us'),
// and ONLY inside the adapter, central package pin, and two exact dependency-lock paths. It is not
// a blanket exemption: every other Product file keeps its S3 finding.
//
// The exception covers that literal substring (case-insensitive, assembled in the scanner
// as 'mo'+'db'+'us') when it appears in:
//   - the central package management file that pins the approved library version
//   - the application-owned TC acquisition adapter (physical, read-only, disabled by default)
//   - two exact restore-generated dependency-lock metadata paths (Owner narrow revision)
// Any other path, any spelling variant outside the allow-list, or any other vendor term keeps
// its S3 finding.
//
// Historical note: the scanner assembles VENDOR_TERMS from fragments so the scanner source itself
// does not contain the words; this exception file therefore also assembles the allowed term from
// fragments so it does not itself introduce a second literal that would need excepting.

const ALLOWED_TERM = 'mo' + 'db' + 'us';

const ALLOWED_PATHS = Object.freeze([
  'Directory.Packages.props',
  'adapters/tc-acquisition/Wjss.Adapters.TcAcquisition.csproj',
  // Dependency metadata only: not source, a directory prefix, or a generic lock exemption.
  'apps/tc-bench/packages.lock.json',
  'tests/tc-acquisition.tests/packages.lock.json',
]);

const ALLOWED_PREFIX = 'adapters/tc-acquisition/';

/**
 * True only when the S3 finding for `term` in `relPath` is covered by the narrowly scoped
 * library-selection exception. Matching is exact on the allow-list above; any other file,
 * any other term, or any path variant returns false.
 */
export function isS3Exception(relPath, term) {
  if (typeof relPath !== 'string' || typeof term !== 'string') return false;
  if (term.toLowerCase() !== ALLOWED_TERM) return false;
  // Reject non-normalized or traversal paths before prefix check so
  // 'adapters/tc-acquisition/../tc-acquisition/evil.cs' cannot expand the exception.
  if (relPath.includes('..') || relPath.includes('//') || relPath.includes('\\')) return false;
  if (relPath.includes('/./')) return false;
  if (relPath.startsWith('./') || relPath.startsWith('/')) return false;
  if (relPath.endsWith('/') || relPath.endsWith(' ')) return false;
  if (ALLOWED_PATHS.includes(relPath)) return true;
  if (relPath.startsWith(ALLOWED_PREFIX)) return true;
  return false;
}
