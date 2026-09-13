// Vitest loads node_modules raw, so Angular's partially compiled packages fall back to JIT. The
// namespace is read so the side-effect import is not shaken out of the bundle.
import * as ngCompiler from '@angular/compiler';
import { provideHttpClient, withInterceptors } from '@angular/common/http';
import { provideZonelessChangeDetection } from '@angular/core';
import { provideRouter } from '@angular/router';
import { apiInterceptor } from './app/core/http/api.interceptor';
import { routes } from './app/app.routes';

if (!ngCompiler) {
  throw new Error('@angular/compiler must load before any Angular library');
}

/**
 * The TestBed assembly. It registers the same HTTP providers as `app.config.ts`; a provider that
 * exists in only one of the two is a difference no single test can see.
 */
export default [
  provideZonelessChangeDetection(),
  provideHttpClient(withInterceptors([apiInterceptor])),
  provideRouter(routes),
];
