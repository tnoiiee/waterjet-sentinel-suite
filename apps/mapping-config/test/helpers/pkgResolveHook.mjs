// Test-only resolve hook: maps the browser specifier '/pkg/<file>' to the package source.
import { join, dirname } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const PKG_SRC = join(dirname(fileURLToPath(import.meta.url)), '..', '..', '..', '..', 'packages', 'mapping-config', 'src');

export async function resolve(specifier, context, nextResolve) {
  if (specifier.startsWith('/pkg/')) {
    return { url: pathToFileURL(join(PKG_SRC, specifier.slice('/pkg/'.length))).href, shortCircuit: true };
  }
  return nextResolve(specifier, context);
}
