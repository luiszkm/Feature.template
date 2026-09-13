// Vitest loads node_modules raw, so Angular's partially compiled packages need the JIT compiler
// present before the first one is evaluated. The namespace is read so the import is not shaken out.
import * as ngCompiler from '@angular/compiler';

if (!ngCompiler) {
  throw new Error('@angular/compiler must load before any Angular library');
}
import { setupServer } from 'msw/node';
import { afterAll, afterEach, beforeAll, beforeEach } from 'vitest';

/** One MSW server for the whole run; each test declares the handlers it needs. */
export const server = setupServer();

beforeAll(() => server.listen({ onUnhandledRequest: 'error' }));

beforeEach(() => {
  localStorage.clear();
  sessionStorage.clear();
});

afterEach(() => server.resetHandlers());

afterAll(() => server.close());
