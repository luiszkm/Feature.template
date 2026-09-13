import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { Router } from '@angular/router';
import { catchError, switchMap, throwError } from 'rxjs';
import { API_BASE, LOCAL_403 } from '../api';
import { SessionStore } from '../session/session.store';
import { RefreshCoordinator } from './refresh-coordinator';

const TENANT_HEADER = 'X-Tenant';

/** Routes that must not trigger a refresh: they are the refresh path itself. */
const ANONYMOUS_PATHS = ['/identity/login', '/identity/refresh', '/identity/register'];

export const apiInterceptor: HttpInterceptorFn = (req, next) => {
  if (!req.url.startsWith(API_BASE)) {
    return next(req);
  }

  const session = inject(SessionStore);
  const router = inject(Router);
  const refresher = inject(RefreshCoordinator);

  return next(withCredentials(req, session)).pipe(
    catchError((error: unknown) => {
      if (!(error instanceof HttpErrorResponse)) {
        return throwError(() => error);
      }

      if (error.status === 403 && !req.context.get(LOCAL_403)) {
        void router.navigate(['/forbidden']);
        return throwError(() => error);
      }

      const renewable =
        error.status === 401 && !ANONYMOUS_PATHS.some((path) => req.url.includes(path));

      if (!renewable) {
        return throwError(() => error);
      }

      return refresher.refresh().pipe(switchMap(() => next(withCredentials(req, session))));
    }),
  );
};

function withCredentials<T>(req: HttpRequest<T>, session: SessionStore): HttpRequest<T> {
  const headers: Record<string, string> = { [TENANT_HEADER]: session.tenantKey() };
  const accessToken = session.accessToken();
  if (accessToken) {
    headers['Authorization'] = `Bearer ${accessToken}`;
  }
  // Without this the browser leaves the `pt_refresh` cookie at home and refresh always fails.
  return req.clone({ setHeaders: headers, withCredentials: true });
}
